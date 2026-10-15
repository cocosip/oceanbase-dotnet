using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OceanBase.Core;
using OceanBase.Logging;
using OceanBase.Protocol;
using OceanBase.Protocol.Payloads;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;

namespace OceanBase;

public sealed class OracleDataReader : DbDataReader, IDbColumnSchemaGenerator, IResultSetOwner
{
	private readonly ResultSet m_resultSet;

	private readonly Queue<int> m_pendingRefCursorIds = new Queue<int>();

	private CommandBehavior m_behavior;

	private ICommandPayloadCreator? m_payloadCreator;

	private IDictionary<string, CachedProcedure?>? m_cachedProcedures;

	private CommandListPosition m_commandListPosition;

	private bool m_closed;

	private bool m_hasWarnings;

	private bool m_hasMoreResults;

	private DataTable? m_schemaTable;

	public override int FieldCount
	{
		get
		{
			VerifyNotDisposed();
			if (m_resultSet != null)
			{
				if (!m_resultSet.ContainsCommandParameters)
				{
					return m_resultSet.FieldCount;
				}
				return 0;
			}
			throw new InvalidOperationException("There is no current result set.");
		}
	}

	public override object this[int ordinal] => GetValue(ordinal);

	public override object this[string name] => GetValue(GetOrdinal(name));

	public override bool HasRows
	{
		get
		{
			VerifyNotDisposed();
			if (m_resultSet != null)
			{
				if (!m_resultSet.ContainsCommandParameters)
				{
					return m_resultSet.HasRows;
				}
				return false;
			}
			throw new InvalidOperationException("There is no current result set.");
		}
	}

	public override bool IsClosed => !Command.HasValue;

	/// <summary>
	/// Gets the number of rows changed, inserted, or deleted by execution of the SQL statement.
	/// </summary>
	/// <remarks>For UPDATE, INSERT, and DELETE statements, the return value is the number of rows affected by the command.
	/// For stored procedures, the return value is the number of rows affected by the last statement in the stored procedure,
	/// or zero if the last statement is a SELECT. For all other types of statements, the return value is -1.</remarks>
	public override int RecordsAffected
	{
		get
		{
			ulong? realRecordsAffected = RealRecordsAffected;
			if (realRecordsAffected.HasValue)
			{
				ulong valueOrDefault = realRecordsAffected.GetValueOrDefault();
				return checked((int)valueOrDefault);
			}
			return -1;
		}
	}

	public override int Depth => GetResultSet().Depth;

	public override int VisibleFieldCount => FieldCount;

	internal Activity? Activity { get; private set; }

	internal CommandFacade? Command { get; private set; }

	internal ConnectionFacade? Connection => Command?.Connection;

	internal ulong? RealRecordsAffected { get; set; }

	internal ServerSession? Session => Command?.Session;

	internal PreparedStatement? LastUsedPreparedStatement => m_commandListPosition.LastUsedPreparedStatement;

	Activity? IResultSetOwner.Activity => Activity;

	CommandFacade? IResultSetOwner.Command => Command;

	ConnectionFacade? IResultSetOwner.Connection => Connection;

	ulong? IResultSetOwner.RealRecordsAffected
	{
		get
		{
			return RealRecordsAffected;
		}
		set
		{
			RealRecordsAffected = value;
		}
	}

	PreparedStatement? IResultSetOwner.LastUsedPreparedStatement => LastUsedPreparedStatement;

	bool IResultSetOwner.ExpectOraclePrepareExecuteResponse => m_commandListPosition.ExpectOraclePrepareExecuteResponse;

	ServerSession? IResultSetOwner.Session => Session;

	public override bool NextResult()
	{
		Command?.CancellableCommand.ResetCommandTimeout();
		return NextResultAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
	}

	public override bool Read()
	{
		VerifyNotDisposed();
		Command.Value.CancellableCommand.ResetCommandTimeout();
		return m_resultSet.Read();
	}

	public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
	{
		VerifyNotDisposed();
		Command.Value.CancellableCommand.ResetCommandTimeout();
		using (Command.Value.CancellableCommand.RegisterCancel(cancellationToken))
		{
			return await m_resultSet.ReadAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	internal Task<bool> ReadAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		return m_resultSet.ReadAsync(ioBehavior, cancellationToken);
	}

	public override async Task<bool> NextResultAsync(CancellationToken cancellationToken)
	{
		VerifyNotDisposed();
		Command.Value.CancellableCommand.ResetCommandTimeout();
		using (Command.Value.CancellableCommand.RegisterCancel(cancellationToken))
		{
			return await NextResultAsync(Command?.Connection?.AsyncIOBehavior ?? IOBehavior.Asynchronous, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	internal async Task<bool> NextResultAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		VerifyNotDisposed();
		try
		{
			while (true)
			{
				await m_resultSet.ReadEntireAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				await ScanResultSetAsync(ioBehavior, m_resultSet, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (m_hasMoreResults && m_resultSet.ContainsCommandParameters)
				{
					await ReadOutParametersAsync(m_resultSet, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					continue;
				}
				if (!m_hasMoreResults)
				{
					bool flag = await TryStartPendingRefCursorResultSetAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					if (flag)
					{
						break;
					}
					if (!flag && m_commandListPosition.CommandIndex < m_commandListPosition.CommandCount)
					{
						Command = m_commandListPosition.CommandAt(m_commandListPosition.CommandIndex);
						using (Command.Value.CancellableCommand.RegisterCancel(cancellationToken))
						{
							await m_payloadCreator.SendCommandPrologueAsync(m_commandListPosition, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
							ByteBufferWriter byteBufferWriter = new ByteBufferWriter();
							if (!Command.Value.Session.IsCancelingQuery && m_payloadCreator.WriteQueryCommand(ref m_commandListPosition, m_cachedProcedures, byteBufferWriter, appendSemicolon: false))
							{
								using PayloadData payload = byteBufferWriter.ToPayloadData();
								await Command.Value.Session.SendAsync(payload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
								await m_resultSet.ReadResultSetHeaderAsync(ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
								ActivateResultSet(cancellationToken);
								m_hasMoreResults = true;
							}
						}
					}
				}
				else
				{
					ActivateResultSet(cancellationToken);
				}
				if (!m_hasMoreResults || (Command.Value.CommandBehavior & (CommandBehavior.SingleResult | CommandBehavior.SingleRow)) == 0)
				{
					break;
				}
			}
			if (!m_hasMoreResults)
			{
				m_resultSet.Reset();
			}
			m_schemaTable = null;
			return m_hasMoreResults;
		}
		catch (MySqlException)
		{
			m_resultSet.Reset();
			m_hasMoreResults = false;
			m_schemaTable = null;
			throw;
		}
	}

	private void ActivateResultSet(CancellationToken cancellationToken)
	{
		if (m_resultSet.ReadResultSetHeaderException != null)
		{
			MySqlException ex = m_resultSet.ReadResultSetHeaderException.SourceException as MySqlException;
			if (ex?.SqlState == null)
			{
				Command.Value.Session.SetFailed(m_resultSet.ReadResultSetHeaderException.SourceException);
			}
			if (ex != null && ex.ErrorCode == MySqlErrorCode.QueryInterrupted && cancellationToken.IsCancellationRequested)
			{
				throw new OperationCanceledException(ex.Message, ex, cancellationToken);
			}
			if (ex != null && ex.ErrorCode == MySqlErrorCode.QueryInterrupted && Command.Value.CancellableCommand.IsTimedOut)
			{
				throw MySqlException.CreateForTimeout(ex);
			}
			if (ex != null)
			{
				ServerSession.ThrowIfStatementContainsDelimiter(ex, Command.Value);
				m_resultSet.ReadResultSetHeaderException.Throw();
			}
			throw new MySqlException("Failed to read the result set.", m_resultSet.ReadResultSetHeaderException.SourceException);
		}
		m_hasWarnings = m_resultSet.WarningCount != 0;
	}

	private async ValueTask ScanResultSetAsync(IOBehavior ioBehavior, ResultSet resultSet, CancellationToken cancellationToken)
	{
		if (!m_hasMoreResults)
		{
			return;
		}
		ResultSetState bufferState = resultSet.BufferState;
		if ((bufferState == ResultSetState.None || bufferState == ResultSetState.NoMoreData) ? true : false)
		{
			m_hasMoreResults = false;
			return;
		}
		if (resultSet.BufferState != ResultSetState.HasMoreData)
		{
			throw new InvalidOperationException($"Invalid state: {resultSet.BufferState}");
		}
		using (Command.Value.CancellableCommand.RegisterCancel(cancellationToken))
		{
			try
			{
				await resultSet.ReadResultSetHeaderAsync(ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
				m_hasMoreResults = resultSet.BufferState != ResultSetState.NoMoreData;
			}
			catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.QueryInterrupted)
			{
				m_hasMoreResults = false;
				cancellationToken.ThrowIfCancellationRequested();
				throw;
			}
		}
	}

	public override string GetName(int ordinal)
	{
		return GetResultSet().GetName(ordinal);
	}

	public override int GetValues(object[] values)
	{
		if (values == null)
		{
			throw new ArgumentNullException("values");
		}
		int num = Math.Min(GetResultSet().ColumnDefinitions.Length, values.Length);
		for (int i = 0; i < num; i++)
		{
			values[i] = GetValue(i);
		}
		return num;
	}

	public override bool IsDBNull(int ordinal)
	{
		return GetResultSet().GetCurrentRow().IsDBNull(ordinal);
	}

	public override int GetOrdinal(string name)
	{
		return GetResultSet().GetOrdinal(name);
	}

	public override bool GetBoolean(int ordinal)
	{
		return GetResultSet().GetCurrentRow().GetBoolean(ordinal);
	}

	public bool GetBoolean(string name)
	{
		return GetBoolean(GetOrdinal(name));
	}

	public override byte GetByte(int ordinal)
	{
		return GetResultSet().GetCurrentRow().GetByte(ordinal);
	}

	public byte GetByte(string name)
	{
		return GetByte(GetOrdinal(name));
	}

	public sbyte GetSByte(int ordinal)
	{
		return GetResultSet().GetCurrentRow().GetSByte(ordinal);
	}

	public sbyte GetSByte(string name)
	{
		return GetSByte(GetOrdinal(name));
	}

	public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
	{
		ResultSet resultSet = GetResultSet();
		if (resultSet.ColumnDefinitions[ordinal].ColumnType == ColumnType.OracleBlob)
		{
			return CopyBytesToBuffer((byte[])GetValue(ordinal), dataOffset, buffer, bufferOffset, length);
		}
		return resultSet.GetCurrentRow().GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
	}

	private static long CopyBytesToBuffer(byte[] source, long dataOffset, byte[]? buffer, int bufferOffset, int length)
	{
		long num = Math.Max(0L, source.Length - dataOffset);
		long num2 = Math.Min(num, length);
		if (buffer != null && num2 > 0)
		{
			Array.Copy(source, dataOffset, buffer, bufferOffset, num2);
		}
		return num;
	}

	public long GetBytes(string name, long dataOffset, byte[]? buffer, int bufferOffset, int length)
	{
		return GetBytes(GetOrdinal(name), dataOffset, buffer, bufferOffset, length);
	}

	public override char GetChar(int ordinal)
	{
		return GetResultSet().GetCurrentRow().GetChar(ordinal);
	}

	public char GetChar(string name)
	{
		return GetChar(GetOrdinal(name));
	}

	public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
	{
		return GetResultSet().GetCurrentRow().GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
	}

	public override Guid GetGuid(int ordinal)
	{
		return GetResultSet().GetCurrentRow().GetGuid(ordinal);
	}

	public Guid GetGuid(string name)
	{
		return GetGuid(GetOrdinal(name));
	}

	public override short GetInt16(int ordinal)
	{
		return GetResultSet().GetCurrentRow().GetInt16(ordinal);
	}

	public short GetInt16(string name)
	{
		return GetInt16(GetOrdinal(name));
	}

	public override int GetInt32(int ordinal)
	{
		return GetResultSet().GetCurrentRow().GetInt32(ordinal);
	}

	public int GetInt32(string name)
	{
		return GetInt32(GetOrdinal(name));
	}

	public override long GetInt64(int ordinal)
	{
		return GetResultSet().GetCurrentRow().GetInt64(ordinal);
	}

	public long GetInt64(string name)
	{
		return GetInt64(GetOrdinal(name));
	}

	public override string GetDataTypeName(int ordinal)
	{
		return GetResultSet().GetDataTypeName(ordinal);
	}

	public Type GetFieldType(string name)
	{
		return GetFieldType(GetOrdinal(name));
	}

	public override Type GetFieldType(int ordinal)
	{
		return GetResultSet().GetFieldType(ordinal);
	}

	public override Type GetProviderSpecificFieldType(int ordinal)
	{
		return GetFieldType(ordinal);
	}

	private object GetRawValue(int ordinal)
	{
		return GetResultSet().GetCurrentRow().GetValue(ordinal);
	}

	public override object GetValue(int ordinal)
	{
		object rawValue = GetRawValue(ordinal);
		if (rawValue is OracleUdtValue value)
		{
			return OracleUdt.CreateObject(Connection?.AsOracleConnection, value) ?? rawValue;
		}
		if (!(rawValue is byte[] array))
		{
			return rawValue;
		}
		switch (GetResultSet().ColumnDefinitions[ordinal].ColumnType)
		{
		case ColumnType.OracleBlob:
		{
			using OracleBlob oracleBlob = new OracleBlob(hasLocator: true, array, null, Connection?.AsOracleConnection);
			oracleBlob.SetOracleMode(isOracleMode: true);
			byte[] array2 = oracleBlob.TryGetInMemoryBytes();
			if (array2 != null)
			{
				return array2;
			}
			try
			{
				return oracleBlob.GetBytes(1L, checked((int)oracleBlob.Length));
			}
			catch (InvalidOperationException ex2) when (ex2.Message.IndexOf("already in use", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				throw new InvalidOperationException("Cannot materialize Oracle BLOB bytes while the DataReader is open (the connection is already in use). Close the reader first, then call GetValue, or use GetOracleBlob and read it after the reader is closed.", ex2);
			}
		}
		case ColumnType.OracleClob:
		{
			OracleConnection oracleConnection = Connection?.AsOracleConnection;
			string oracleClobEncodingName = GetOracleClobEncodingName(oracleConnection);
			using OracleClob oracleClob = new OracleClob(hasLocator: true, array, oracleClobEncodingName, oracleConnection);
			oracleClob.SetOracleMode(isOracleMode: true);
			if (oracleClob.TryGetInMemoryBytes() != null)
			{
				return oracleClob.GetString();
			}
			try
			{
				return oracleClob.GetSubString(1L, int.MaxValue) ?? "";
			}
			catch (InvalidOperationException ex) when (ex.Message.IndexOf("already in use", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				throw new InvalidOperationException("Cannot materialize Oracle CLOB bytes while the DataReader is open (the connection is already in use). Close the reader first, then call GetValue, or use GetOracleClob and read it after the reader is closed.", ex);
			}
		}
		default:
			return rawValue;
		}
	}

	public override object GetProviderSpecificValue(int ordinal)
	{
		return GetValue(ordinal);
	}

	public object GetOracleValue(int ordinal)
	{
		object rawValue = GetRawValue(ordinal);
		if (rawValue is OracleUdtValue value)
		{
			object obj = OracleUdt.CreateObject(Connection?.AsOracleConnection, value);
			if (obj != null && !(obj is OracleUdtValue))
			{
				return obj;
			}
			return new OracleUdt(value);
		}
		return rawValue;
	}

	public override int GetProviderSpecificValues(object[] values)
	{
		if (values == null)
		{
			throw new ArgumentNullException("values");
		}
		int num = Math.Min(FieldCount, values.Length);
		for (int i = 0; i < num; i++)
		{
			values[i] = GetProviderSpecificValue(i);
		}
		return num;
	}

	public int GetOracleValues(object[] values)
	{
		return GetProviderSpecificValues(values);
	}

	public override IEnumerator GetEnumerator()
	{
		return new DbEnumerator(this, closeReader: false);
	}

	protected override DbDataReader GetDbDataReader(int ordinal)
	{
		throw new NotSupportedException();
	}

	public override DateTime GetDateTime(int ordinal)
	{
		object value = GetValue(ordinal);
		if (!(value is OracleTimeStamp { Value: var value2 }))
		{
			if (!(value is OracleTimeStampLTZ { Value: var value3 }))
			{
				if (!(value is OracleTimeStampTZ { Value: var value4 }))
				{
					if (value is DateTime result)
					{
						return result;
					}
					return GetResultSet().GetCurrentRow().GetDateTime(ordinal);
				}
				return value4;
			}
			return value3;
		}
		return value2;
	}

	public DateTime GetDateTime(string name)
	{
		return GetDateTime(GetOrdinal(name));
	}

	public DateTimeOffset GetDateTimeOffset(int ordinal)
	{
		object value = GetValue(ordinal);
		if (!(value is OracleTimeStampTZ oracleTimeStampTZ))
		{
			if (value is DateTimeOffset result)
			{
				return result;
			}
			return GetResultSet().GetCurrentRow().GetDateTimeOffset(ordinal);
		}
		return oracleTimeStampTZ.ToDateTimeOffset();
	}

	public DateTimeOffset GetDateTimeOffset(string name)
	{
		return GetDateTimeOffset(GetOrdinal(name));
	}

	public MySqlDateTime GetMySqlDateTime(int ordinal)
	{
		return GetResultSet().GetCurrentRow().GetMySqlDateTime(ordinal);
	}

	public MySqlDateTime GetMySqlDateTime(string name)
	{
		return GetMySqlDateTime(GetOrdinal(name));
	}

	public MySqlGeometry GetMySqlGeometry(int ordinal)
	{
		return GetResultSet().GetCurrentRow().GetMySqlGeometry(ordinal);
	}

	public MySqlGeometry GetMySqlGeometry(string name)
	{
		return GetMySqlGeometry(GetOrdinal(name));
	}

	public MySqlDecimal GetMySqlDecimal(int ordinal)
	{
		return GetResultSet().GetCurrentRow().GetMySqlDecimal(ordinal);
	}

	public MySqlDecimal GetMySqlDecimal(string name)
	{
		return GetMySqlDecimal(GetOrdinal(name));
	}

	public TimeSpan GetTimeSpan(int ordinal)
	{
		object value = GetValue(ordinal);
		if (!(value is OracleIntervalDS oracleIntervalDS))
		{
			return (TimeSpan)value;
		}
		return oracleIntervalDS.ToTimeSpan();
	}

	public TimeSpan GetTimeSpan(string name)
	{
		return GetTimeSpan(GetOrdinal(name));
	}

	public OracleIntervalDS GetOracleIntervalDS(int ordinal)
	{
		object value = GetValue(ordinal);
		if (value is DBNull)
		{
			throw new InvalidCastException("Data is Null. This method or property cannot be called on Null values.");
		}
		if (!(value is OracleIntervalDS result))
		{
			if (value is TimeSpan timeSpan)
			{
				return OracleIntervalDS.FromTimeSpan(timeSpan);
			}
			throw new InvalidCastException("Can't convert " + GetDataTypeName(ordinal) + " to OracleIntervalDS.");
		}
		return result;
	}

	public OracleIntervalDS GetOracleIntervalDS(string name)
	{
		return GetOracleIntervalDS(GetOrdinal(name));
	}

	public OracleIntervalYM GetOracleIntervalYM(int ordinal)
	{
		object value = GetValue(ordinal);
		if (value is DBNull)
		{
			throw new InvalidCastException("Data is Null. This method or property cannot be called on Null values.");
		}
		if (!(value is OracleIntervalYM result))
		{
			if (!(value is long totalMonths))
			{
				if (value is int num)
				{
					return OracleIntervalYM.FromTotalMonths(num);
				}
				throw new InvalidCastException("Can't convert " + GetDataTypeName(ordinal) + " to OracleIntervalYM.");
			}
			return OracleIntervalYM.FromTotalMonths(totalMonths);
		}
		return result;
	}

	public OracleIntervalYM GetOracleIntervalYM(string name)
	{
		return GetOracleIntervalYM(GetOrdinal(name));
	}

	public OracleBlob GetOracleBlob(int ordinal)
	{
		object rawValue = GetRawValue(ordinal);
		if (rawValue is DBNull)
		{
			throw new InvalidCastException("Data is Null. This method or property cannot be called on Null values.");
		}
		bool flag = GetResultSet().ColumnDefinitions[ordinal].ColumnType == ColumnType.OracleBlob;
		if (rawValue is byte[] array)
		{
			OracleBlob oracleBlob = ((!flag) ? new OracleBlob(array) : new OracleBlob(hasLocator: true, array, null, Connection?.AsOracleConnection));
			oracleBlob.SetOracleMode(isOracleMode: true);
			return oracleBlob;
		}
		throw new InvalidCastException("Can't convert " + GetDataTypeName(ordinal) + " to OracleBlob.");
	}

	public OracleBlob GetOracleBlob(string name)
	{
		return GetOracleBlob(GetOrdinal(name));
	}

	public OracleClob GetOracleClob(int ordinal)
	{
		object rawValue = GetRawValue(ordinal);
		if (rawValue is DBNull)
		{
			throw new InvalidCastException("Data is Null. This method or property cannot be called on Null values.");
		}
		bool flag = GetResultSet().ColumnDefinitions[ordinal].ColumnType == ColumnType.OracleClob;
		string oracleClobEncodingName = GetOracleClobEncodingName(Connection?.AsOracleConnection);
		if (rawValue is byte[] array)
		{
			OracleClob oracleClob = ((!flag) ? new OracleClob(hasLocator: false, array, oracleClobEncodingName, Connection?.AsOracleConnection) : new OracleClob(hasLocator: true, array, oracleClobEncodingName, Connection?.AsOracleConnection));
			oracleClob.SetOracleMode(isOracleMode: true);
			return oracleClob;
		}
		throw new InvalidCastException("Can't convert " + GetDataTypeName(ordinal) + " to OracleClob.");
	}

	public OracleClob GetOracleClob(string name)
	{
		return GetOracleClob(GetOrdinal(name));
	}

	private static string GetOracleClobEncodingName(OracleConnection? oracleConnection)
	{
		return oracleConnection?.CharacterSet ?? "UTF-8";
	}

	public OracleTimeStamp GetOracleTimeStamp(int ordinal)
	{
		object value = GetValue(ordinal);
		if (value is DBNull)
		{
			throw new InvalidCastException("Data is Null. This method or property cannot be called on Null values.");
		}
		if (!(value is OracleTimeStamp result))
		{
			if (value is DateTime dateTime)
			{
				return OracleTimeStamp.FromWire(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, (int)(dateTime.Ticks % 10000000 * 100), 9);
			}
			throw new InvalidCastException("Can't convert " + GetDataTypeName(ordinal) + " to OracleTimeStamp.");
		}
		return result;
	}

	public OracleTimeStamp GetOracleTimeStamp(string name)
	{
		return GetOracleTimeStamp(GetOrdinal(name));
	}

	public OracleTimeStampTZ GetOracleTimeStampTZ(int ordinal)
	{
		object value = GetValue(ordinal);
		if (value is DBNull)
		{
			throw new InvalidCastException("Data is Null. This method or property cannot be called on Null values.");
		}
		if (!(value is OracleTimeStampTZ result))
		{
			if (value is DateTimeOffset { Year: var year, Month: var month, Day: var day, Hour: var hour, Minute: var minute, Second: var second } dateTimeOffset)
			{
				return OracleTimeStampTZ.FromWire(year, month, day, hour, minute, second, (int)(dateTimeOffset.Ticks % 10000000 * 100), 9, (sbyte)dateTimeOffset.Offset.Hours, (byte)Math.Abs(dateTimeOffset.Offset.Minutes));
			}
			throw new InvalidCastException("Can't convert " + GetDataTypeName(ordinal) + " to OracleTimeStampTZ.");
		}
		return result;
	}

	public OracleTimeStampTZ GetOracleTimeStampTZ(string name)
	{
		return GetOracleTimeStampTZ(GetOrdinal(name));
	}

	public OracleTimeStampLTZ GetOracleTimeStampLTZ(int ordinal)
	{
		object value = GetValue(ordinal);
		if (value is DBNull)
		{
			throw new InvalidCastException("Data is Null. This method or property cannot be called on Null values.");
		}
		if (!(value is OracleTimeStampLTZ result))
		{
			if (value is DateTime dateTime)
			{
				return OracleTimeStampLTZ.FromWire(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, (int)(dateTime.Ticks % 10000000 * 100), 9, TimeSpan.Zero);
			}
			throw new InvalidCastException("Can't convert " + GetDataTypeName(ordinal) + " to OracleTimeStampLTZ.");
		}
		return result;
	}

	public OracleTimeStampLTZ GetOracleTimeStampLTZ(string name)
	{
		return GetOracleTimeStampLTZ(GetOrdinal(name));
	}

	public override Stream GetStream(int ordinal)
	{
		return GetResultSet().GetCurrentRow().GetStream(ordinal);
	}

	public Stream GetStream(string name)
	{
		return GetStream(GetOrdinal(name));
	}

	public override TextReader GetTextReader(int ordinal)
	{
		return new StringReader(GetString(ordinal));
	}

	public TextReader GetTextReader(string name)
	{
		return new StringReader(GetString(name));
	}

	public override string GetString(int ordinal)
	{
		switch (GetResultSet().ColumnDefinitions[ordinal].ColumnType)
		{
		case ColumnType.OracleClob:
		{
			if (GetRawValue(ordinal) is DBNull)
			{
				return "";
			}
			using OracleClob oracleClob = GetOracleClob(ordinal);
			return oracleClob.GetSubString(1L, int.MaxValue) ?? "";
		}
		case ColumnType.OracleBlob:
			throw new InvalidCastException("Can't convert " + GetDataTypeName(ordinal) + " to String.");
		default:
		{
			object value = GetValue(ordinal);
			return (value is string text) ? text : (Convert.ToString(value, CultureInfo.InvariantCulture) ?? throw new InvalidCastException());
		}
		}
	}

	public string GetString(string name)
	{
		return GetString(GetOrdinal(name));
	}

	public override decimal GetDecimal(int ordinal)
	{
		return GetResultSet().GetCurrentRow().GetDecimal(ordinal);
	}

	public decimal GetDecimal(string name)
	{
		return GetDecimal(GetOrdinal(name));
	}

	public override double GetDouble(int ordinal)
	{
		return GetResultSet().GetCurrentRow().GetDouble(ordinal);
	}

	public double GetDouble(string name)
	{
		return GetDouble(GetOrdinal(name));
	}

	public override float GetFloat(int ordinal)
	{
		return GetResultSet().GetCurrentRow().GetFloat(ordinal);
	}

	public float GetFloat(string name)
	{
		return GetFloat(GetOrdinal(name));
	}

	public ushort GetUInt16(int ordinal)
	{
		return GetResultSet().GetCurrentRow().GetUInt16(ordinal);
	}

	public ushort GetUInt16(string name)
	{
		return GetUInt16(GetOrdinal(name));
	}

	public uint GetUInt32(int ordinal)
	{
		return GetResultSet().GetCurrentRow().GetUInt32(ordinal);
	}

	public uint GetUInt32(string name)
	{
		return GetUInt32(GetOrdinal(name));
	}

	public ulong GetUInt64(int ordinal)
	{
		return GetResultSet().GetCurrentRow().GetUInt64(ordinal);
	}

	public ulong GetUInt64(string name)
	{
		return GetUInt64(GetOrdinal(name));
	}

	/// <summary>
	/// Returns a <see cref="T:System.Data.DataTable" /> that contains metadata about the columns in the result set.
	/// </summary>
	/// <returns>A <see cref="T:System.Data.DataTable" /> containing metadata about the columns in the result set.</returns>
	public override DataTable? GetSchemaTable()
	{
		return m_schemaTable ?? (m_schemaTable = BuildSchemaTable());
	}

	/// <summary>
	/// Returns a <see cref="T:System.Data.DataTable" /> that contains metadata about the columns in the result set.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A <see cref="T:System.Data.DataTable" /> containing metadata about the columns in the result set.</returns>
	/// <remarks>This method runs synchronously; prefer to call <see cref="M:OceanBase.OracleDataReader.GetSchemaTable" /> to avoid the overhead of allocating an unnecessary <c>Task</c>.</remarks>
	public Task<DataTable?> GetSchemaTableAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(GetSchemaTable());
	}

	public override void Close()
	{
		DisposeAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Returns metadata about the columns in the result set.
	/// </summary>
	/// <returns>A <see cref="T:System.Collections.ObjectModel.ReadOnlyCollection`1" /> containing metadata about the result set.</returns>
	public ReadOnlyCollection<DbColumn> GetColumnSchema()
	{
		if (!m_resultSet.HasResultSet || m_resultSet.ContainsCommandParameters)
		{
			return new ReadOnlyCollection<DbColumn>(new List<DbColumn>());
		}
		ReadOnlySpan<ColumnDefinitionPayload> columnDefinitions = m_resultSet.ColumnDefinitions;
		ResultSet resultSet = GetResultSet();
		List<DbColumn> list = new List<DbColumn>(columnDefinitions.Length);
		for (int i = 0; i < columnDefinitions.Length; i++)
		{
			list.Add(new OracleDbColumn(i, columnDefinitions[i], Connection?.AllowZeroDateTime ?? false, resultSet.GetOracleColumnType(i)));
		}
		return list.AsReadOnly();
	}

	/// <summary>
	/// Returns metadata about the columns in the result set.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task" /> containing <see cref="T:System.Collections.ObjectModel.ReadOnlyCollection`1" /> containing metadata about the result set.</returns>
	/// <remarks>This method runs synchronously; prefer to call <see cref="M:OceanBase.OracleDataReader.GetColumnSchema" /> to avoid the overhead of allocating an unnecessary <c>Task</c>.</remarks>
	public Task<ReadOnlyCollection<DbColumn>> GetColumnSchemaAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(GetColumnSchema());
	}

	public override T GetFieldValue<T>(int ordinal)
	{
		if (typeof(T) == typeof(bool))
		{
			return (T)(object)GetBoolean(ordinal);
		}
		if (typeof(T) == typeof(byte))
		{
			return (T)(object)GetByte(ordinal);
		}
		if (typeof(T) == typeof(sbyte))
		{
			return (T)(object)GetSByte(ordinal);
		}
		if (typeof(T) == typeof(short))
		{
			return (T)(object)GetInt16(ordinal);
		}
		if (typeof(T) == typeof(ushort))
		{
			return (T)(object)GetUInt16(ordinal);
		}
		if (typeof(T) == typeof(int))
		{
			return (T)(object)GetInt32(ordinal);
		}
		if (typeof(T) == typeof(uint))
		{
			return (T)(object)GetUInt32(ordinal);
		}
		if (typeof(T) == typeof(long))
		{
			return (T)(object)GetInt64(ordinal);
		}
		if (typeof(T) == typeof(ulong))
		{
			return (T)(object)GetUInt64(ordinal);
		}
		if (typeof(T) == typeof(char))
		{
			return (T)(object)GetChar(ordinal);
		}
		if (typeof(T) == typeof(decimal))
		{
			return (T)(object)GetDecimal(ordinal);
		}
		if (typeof(T) == typeof(double))
		{
			return (T)(object)GetDouble(ordinal);
		}
		if (typeof(T) == typeof(float))
		{
			return (T)(object)GetFloat(ordinal);
		}
		if (typeof(T) == typeof(string))
		{
			return (T)(object)GetString(ordinal);
		}
		if (typeof(T) == typeof(DateTime))
		{
			return (T)(object)GetDateTime(ordinal);
		}
		if (typeof(T) == typeof(DateTimeOffset))
		{
			return (T)(object)GetDateTimeOffset(ordinal);
		}
		if (typeof(T) == typeof(Guid))
		{
			return (T)(object)GetGuid(ordinal);
		}
		if (typeof(T) == typeof(MySqlGeometry))
		{
			return (T)(object)GetMySqlGeometry(ordinal);
		}
		if (typeof(T) == typeof(Stream))
		{
			return (T)(object)GetStream(ordinal);
		}
		if (typeof(T) == typeof(TextReader) || typeof(T) == typeof(StringReader))
		{
			return (T)(object)GetTextReader(ordinal);
		}
		if (typeof(T) == typeof(TimeSpan))
		{
			return (T)(object)GetTimeSpan(ordinal);
		}
		if (typeof(T) == typeof(OracleBlob))
		{
			return (T)(object)GetOracleBlob(ordinal);
		}
		if (typeof(T) == typeof(OracleClob))
		{
			return (T)(object)GetOracleClob(ordinal);
		}
		if (typeof(T) == typeof(OracleTimeStamp))
		{
			return (T)(object)GetOracleTimeStamp(ordinal);
		}
		if (typeof(T) == typeof(OracleTimeStampTZ))
		{
			return (T)(object)GetOracleTimeStampTZ(ordinal);
		}
		if (typeof(T) == typeof(MySqlDecimal))
		{
			return (T)(object)GetMySqlDecimal(ordinal);
		}
		return base.GetFieldValue<T>(ordinal);
	}

	protected override void Dispose(bool disposing)
	{
		try
		{
			if (disposing)
			{
				DisposeAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
			}
		}
		finally
		{
			base.Dispose(disposing);
		}
	}

	public Task DisposeAsync()
	{
		return DisposeAsync(Connection?.AsyncIOBehavior ?? IOBehavior.Asynchronous, CancellationToken.None);
	}

	internal async Task InitAsync(CommandListPosition commandListPosition, ICommandPayloadCreator payloadCreator, IDictionary<string, CachedProcedure?>? cachedProcedures, CommandFacade command, CommandBehavior behavior, Activity? activity, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (m_hasMoreResults)
		{
			throw new InvalidOperationException("Expected m_hasMoreResults to be false");
		}
		if (m_resultSet.BufferState != ResultSetState.None || m_resultSet.State != ResultSetState.None)
		{
			throw new InvalidOperationException("Expected BufferState and State to be ResultSetState.None.");
		}
		m_closed = false;
		m_hasWarnings = false;
		RealRecordsAffected = null;
		m_pendingRefCursorIds.Clear();
		m_commandListPosition = commandListPosition;
		m_payloadCreator = payloadCreator;
		m_cachedProcedures = cachedProcedures;
		Command = command;
		m_behavior = behavior;
		Activity = activity;
		command.SetActiveReader(this);
		try
		{
			await m_resultSet.ReadResultSetHeaderAsync(ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
			ActivateResultSet(cancellationToken);
			m_hasMoreResults = true;
			if (m_resultSet.ContainsCommandParameters)
			{
				await ReadOutParametersAsync(m_resultSet, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			bool enableRefCursorResultSets = command.InnerCommand is OracleCommand oracleCommand && oracleCommand.EnableStoredProcedureRefCursorResultSets;
			while (m_resultSet.State == ResultSetState.NoMoreData && (commandListPosition.CommandIndex < commandListPosition.CommandCount || (enableRefCursorResultSets && m_pendingRefCursorIds.Count > 0)))
			{
				await NextResultAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		catch (Exception exception)
		{
			if (activity != null && activity.IsAllDataRequested)
			{
				activity.SetException(exception);
				activity.Stop();
			}
			Dispose();
			throw;
		}
	}

	internal DataTable? BuildSchemaTable()
	{
		if (!m_resultSet.HasResultSet || m_resultSet.ContainsCommandParameters)
		{
			return null;
		}
		DataTable dataTable = new DataTable("SchemaTable")
		{
			Locale = CultureInfo.InvariantCulture,
			MinimumCapacity = m_resultSet.ColumnDefinitions.Length
		};
		DataColumn column = new DataColumn(SchemaTableColumn.ColumnName, typeof(string));
		DataColumn dataColumn = new DataColumn(SchemaTableColumn.ColumnOrdinal, typeof(int));
		DataColumn column2 = new DataColumn(SchemaTableColumn.ColumnSize, typeof(int));
		DataColumn dataColumn2 = new DataColumn(SchemaTableColumn.NumericPrecision, typeof(int));
		DataColumn dataColumn3 = new DataColumn(SchemaTableColumn.NumericScale, typeof(int));
		DataColumn column3 = new DataColumn(SchemaTableColumn.DataType, typeof(Type));
		DataColumn column4 = new DataColumn(SchemaTableColumn.ProviderType, typeof(int));
		DataColumn dataColumn4 = new DataColumn(SchemaTableColumn.IsLong, typeof(bool));
		DataColumn column5 = new DataColumn(SchemaTableColumn.AllowDBNull, typeof(bool));
		DataColumn column6 = new DataColumn(SchemaTableOptionalColumn.IsReadOnly, typeof(bool));
		DataColumn column7 = new DataColumn(SchemaTableOptionalColumn.IsRowVersion, typeof(bool));
		DataColumn column8 = new DataColumn(SchemaTableColumn.IsUnique, typeof(bool));
		DataColumn column9 = new DataColumn(SchemaTableColumn.IsKey, typeof(bool));
		DataColumn column10 = new DataColumn(SchemaTableOptionalColumn.IsAutoIncrement, typeof(bool));
		DataColumn column11 = new DataColumn(SchemaTableOptionalColumn.IsHidden, typeof(bool));
		DataColumn column12 = new DataColumn(SchemaTableOptionalColumn.BaseCatalogName, typeof(string));
		DataColumn column13 = new DataColumn(SchemaTableColumn.BaseSchemaName, typeof(string));
		DataColumn column14 = new DataColumn(SchemaTableColumn.BaseTableName, typeof(string));
		DataColumn column15 = new DataColumn(SchemaTableColumn.BaseColumnName, typeof(string));
		DataColumn column16 = new DataColumn(SchemaTableColumn.IsAliased, typeof(bool));
		DataColumn column17 = new DataColumn(SchemaTableColumn.IsExpression, typeof(bool));
		DataColumn column18 = new DataColumn("IsIdentity", typeof(bool));
		dataColumn.DefaultValue = 0;
		dataColumn2.DefaultValue = 0;
		dataColumn3.DefaultValue = 0;
		dataColumn4.DefaultValue = false;
		DataColumnCollection columns = dataTable.Columns;
		columns.Add(column);
		columns.Add(dataColumn);
		columns.Add(column2);
		columns.Add(dataColumn2);
		columns.Add(dataColumn3);
		columns.Add(column8);
		columns.Add(column9);
		columns.Add(column12);
		columns.Add(column15);
		columns.Add(column13);
		columns.Add(column14);
		columns.Add(column3);
		columns.Add(column5);
		columns.Add(column4);
		columns.Add(column16);
		columns.Add(column17);
		columns.Add(column18);
		columns.Add(column10);
		columns.Add(column7);
		columns.Add(column11);
		columns.Add(dataColumn4);
		columns.Add(column6);
		foreach (OracleDbColumn item in GetColumnSchema())
		{
			DataRow dataRow = dataTable.NewRow();
			dataRow[column] = item.ColumnName;
			dataRow[dataColumn] = item.ColumnOrdinal;
			dataRow[column3] = item.DataType;
			dataRow[column2] = item.ColumnSize;
			dataRow[column4] = (int)item.ProviderType;
			dataRow[dataColumn4] = item.IsLong;
			dataRow[column8] = false;
			dataRow[column9] = item.IsKey;
			dataRow[column5] = item.AllowDBNull;
			dataRow[dataColumn3] = item.NumericScale;
			dataRow[dataColumn2] = item.NumericPrecision.GetValueOrDefault();
			dataRow[column12] = item.BaseCatalogName;
			dataRow[column15] = item.BaseColumnName;
			dataRow[column13] = item.BaseSchemaName;
			dataRow[column14] = item.BaseTableName;
			dataRow[column10] = item.IsAutoIncrement;
			dataRow[column7] = false;
			dataRow[column6] = item.IsReadOnly;
			dataTable.Rows.Add(dataRow);
			dataRow.AcceptChanges();
		}
		return dataTable;
	}

	internal OracleDataReader()
	{
		m_resultSet = new ResultSet(this);
	}

	internal async Task DisposeAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (m_closed)
		{
			return;
		}
		m_closed = true;
		OracleConnection oracleConnection = Connection?.AsOracleConnection;
		int currentRefCursorId = 0;
		bool flag = false;
		int[] pendingRefCursorIds = null;
		if (Command?.InnerCommand is OracleCommand oracleCommand)
		{
			if (oracleCommand.IsRefCursorFetchCommand)
			{
				currentRefCursorId = LastUsedPreparedStatement?.StatementId ?? 0;
				flag = oracleCommand.CloseRefCursorResultSetAfterScalar;
			}
			if (m_pendingRefCursorIds.Count != 0)
			{
				pendingRefCursorIds = m_pendingRefCursorIds.ToArray();
			}
		}
		if (m_resultSet != null)
		{
			CommandFacade? command = Command;
			if (command.HasValue && command.GetValueOrDefault().Connection?.ConnectionState == ConnectionState.Open)
			{
				Command.Value.Session.SetTimeout(int.MaxValue);
				try
				{
					if (flag)
					{
						await m_resultSet.ReadEntireAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
						m_resultSet.Reset();
					}
					else
					{
						while (await NextResultAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
						{
						}
					}
				}
				catch (MySqlException ex)
				{
					if (ex.ErrorCode != MySqlErrorCode.QueryInterrupted)
					{
						Log.IgnoringExceptionInDisposeAsync(Command.Value.Logger, ex, Command.Value.Connection?.SessionId ?? "", ex.Message, Command.Value.CommandText);
					}
				}
			}
		}
		m_hasMoreResults = false;
		Command.Value.CancellableCommand.SetTimeout(int.MaxValue);
		Command.Value.FinishQuerying(m_hasWarnings);
		if (oracleConnection != null)
		{
			if (currentRefCursorId != 0)
			{
				await OracleRefCursor.TryCloseCursorNoThrowAsync(oracleConnection, currentRefCursorId, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			if (pendingRefCursorIds != null)
			{
				int[] array = pendingRefCursorIds;
				foreach (int cursorId in array)
				{
					await OracleRefCursor.TryCloseCursorNoThrowAsync(oracleConnection, cursorId, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
		}
		Activity?.Stop();
		Activity = null;
		if ((m_behavior & CommandBehavior.CloseConnection) != CommandBehavior.Default)
		{
			OracleConnection oracleConnection2 = Command?.Connection?.AsOracleConnection;
			if (oracleConnection2 != null)
			{
				await oracleConnection2.CloseAsync(ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		Command = null;
		m_commandListPosition = default(CommandListPosition);
		m_payloadCreator = null;
		m_cachedProcedures = null;
	}

	private async Task ReadOutParametersAsync(ResultSet resultSet, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		CommandFacade command = Command.Value;
		ResetOracleParametersFromServerMetadata(command, resultSet);
		DbParameterCollection outParameters = command.OutParameters ?? throw new InvalidOperationException("Expected output parameters.");
		await resultSet.ReadAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		Row currentRow = resultSet.GetCurrentRow();
		bool isPsProtocol = resultSet.DataReader.LastUsedPreparedStatement != null;
		int num = ((!isPsProtocol) ? 1 : 0);
		if (!isPsProtocol && currentRow.GetString(0) != SingleCommandPayloadCreator.OutParameterSentinelColumnName)
		{
			throw new InvalidOperationException("Expected out parameter values.");
		}
		Dictionary<string, DbParameter> dictionary = new Dictionary<string, DbParameter>(StringComparer.OrdinalIgnoreCase);
		DbParameter dbParameter = null;
		for (int i = 0; i < outParameters.Count; i++)
		{
			DbParameter dbParameter2 = outParameters[i];
			string text = ((!(dbParameter2 is OracleParameter oracleParameter)) ? OracleParameter.NormalizeParameterName(dbParameter2.ParameterName) : oracleParameter.NormalizedParameterName);
			string text2 = text;
			if (!string.IsNullOrEmpty(text2))
			{
				dictionary[text2] = dbParameter2;
			}
			if (dbParameter2 is OracleParameter { OracleDbType: OracleDbType.RefCursor } && dbParameter == null)
			{
				dbParameter = dbParameter2;
			}
		}
		bool flag = false;
		if (dictionary.Count > 0)
		{
			bool flag2 = false;
			int num2 = 0;
			HashSet<DbParameter> hashSet = new HashSet<DbParameter>();
			bool[] array = new bool[resultSet.ColumnDefinitions.Length];
			for (int j = num; j < resultSet.ColumnDefinitions.Length; j++)
			{
				string name = resultSet.ColumnDefinitions[j].Name;
				if (string.IsNullOrEmpty(name))
				{
					continue;
				}
				flag2 = true;
				string key = OracleParameter.NormalizeParameterName(name);
				if (dictionary.TryGetValue(key, out var value))
				{
					if (resultSet.ColumnDefinitions[j].ColumnType == ColumnType.OracleCursor && value is OracleParameter { OracleDbType: not OracleDbType.RefCursor } && dbParameter != null)
					{
						value = dbParameter;
					}
					AssignOutParameterValue(value, currentRow, j);
					hashSet.Add(value);
					array[j] = true;
					num2++;
				}
			}
			if (flag2 && num2 > 0)
			{
				int num3 = 0;
				for (int k = num; k < resultSet.ColumnDefinitions.Length; k++)
				{
					if (array[k])
					{
						continue;
					}
					while (num3 < outParameters.Count)
					{
						DbParameter dbParameter3 = outParameters[num3++];
						if (hashSet.Add(dbParameter3))
						{
							AssignOutParameterValue(dbParameter3, currentRow, k);
							break;
						}
					}
				}
				flag = true;
			}
		}
		if (!flag && isPsProtocol && command.RawParameters is OracleParameterCollection oracleParameterCollection)
		{
			PreparedStatement lastUsedPreparedStatement = resultSet.DataReader.LastUsedPreparedStatement;
			if (lastUsedPreparedStatement != null)
			{
				Dictionary<string, OracleParameter> dictionary2 = new Dictionary<string, OracleParameter>(StringComparer.OrdinalIgnoreCase);
				for (int l = 0; l < oracleParameterCollection.Count; l++)
				{
					OracleParameter oracleParameter4 = oracleParameterCollection[l];
					dictionary2[oracleParameter4.NormalizedParameterName] = oracleParameter4;
				}
				ParsedStatement statement = lastUsedPreparedStatement.Statement;
				if (TryMapOutParametersByServerBindInoutType(resultSet, currentRow, lastUsedPreparedStatement, statement, dictionary2, dbParameter))
				{
					flag = true;
				}
				else
				{
					List<OracleParameter> list = new List<OracleParameter>(resultSet.ColumnDefinitions.Length);
					for (int m = 0; m < statement.NormalizedParameterNames.Count; m++)
					{
						string text3 = statement.NormalizedParameterNames[m];
						if (text3 != null && text3.Length != 0)
						{
							OracleParameter value2;
							bool flag3 = dictionary2.TryGetValue(text3, out value2);
							if (flag3)
							{
								ParameterDirection direction = value2.Direction;
								bool flag4 = (((uint)(direction - 2) <= 1u || direction == ParameterDirection.ReturnValue) ? true : false);
								flag3 = flag4;
							}
							if (flag3)
							{
								list.Add(value2);
							}
						}
					}
					if (list.Count == resultSet.ColumnDefinitions.Length && list.Count > 0)
					{
						for (int n = 0; n < resultSet.ColumnDefinitions.Length; n++)
						{
							DbParameter dbParameter4 = list[n];
							if (resultSet.ColumnDefinitions[n].ColumnType == ColumnType.OracleCursor && dbParameter4 is OracleParameter { OracleDbType: not OracleDbType.RefCursor } && dbParameter != null)
							{
								dbParameter4 = dbParameter;
							}
							AssignOutParameterValue(dbParameter4, currentRow, n);
						}
						flag = true;
					}
				}
			}
		}
		if (!flag && resultSet.ColumnDefinitions.Length == outParameters.Count + num)
		{
			for (int num4 = 0; num4 < outParameters.Count; num4++)
			{
				DbParameter param = outParameters[num4];
				int columnIndex = num4 + num;
				AssignOutParameterValue(param, currentRow, columnIndex);
			}
		}
		if (await resultSet.ReadAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
		{
			throw new InvalidOperationException("Expected only one row.");
		}
		if (isPsProtocol && resultSet.Session.UseOracleMode && !RealRecordsAffected.HasValue)
		{
			bool? bIsDefineInSelect = null;
			bool? bIsDmlReturn = null;
			if (ResultSet.GetSqlStatementType(command.CommandText, ref bIsDefineInSelect, ref bIsDmlReturn) == SqlStatementType.DML)
			{
				RealRecordsAffected = 1uL;
			}
		}
		QueuePendingRefCursorIds(command);
	}

	private static void ResetOracleParametersFromServerMetadata(CommandFacade command, ResultSet resultSet)
	{
		if (!ShouldResetOracleParametersFromServerMetadata(command, resultSet, out PreparedStatement preparedStatement, out ColumnDefinitionPayload[] serverParameters, out OracleParameterCollection oracleParameters) || !TryResolveBoundOracleParameters(preparedStatement, serverParameters.Length, oracleParameters, out List<OracleParameter> boundParameters))
		{
			return;
		}
		Dictionary<OracleParameter, ParameterDirection> dictionary = new Dictionary<OracleParameter, ParameterDirection>();
		for (int i = 0; i < serverParameters.Length; i++)
		{
			OracleParameter oracleParameter = boundParameters[i];
			ParameterDirection parameterDirection = GetOracleParameterDirection(serverParameters[i].InoutType, oracleParameter.Direction);
			if (dictionary.TryGetValue(oracleParameter, out var value))
			{
				parameterDirection = MergeOracleParameterDirections(value, parameterDirection);
			}
			dictionary[oracleParameter] = parameterDirection;
		}
		foreach (KeyValuePair<OracleParameter, ParameterDirection> item in dictionary)
		{
			OracleParameter key = item.Key;
			ParameterDirection value2 = item.Value;
			if (key.Direction != ParameterDirection.ReturnValue && key.Direction != value2)
			{
				key.Direction = value2;
			}
		}
		RefreshOracleOutputParameterBindings(command, oracleParameters);
	}

	private static bool ShouldResetOracleParametersFromServerMetadata(CommandFacade command, ResultSet resultSet, out PreparedStatement preparedStatement, out ColumnDefinitionPayload[] serverParameters, out OracleParameterCollection oracleParameters)
	{
		preparedStatement = null;
		serverParameters = null;
		oracleParameters = null;
		bool flag = command.CommandType == CommandType.StoredProcedure;
		if (!flag)
		{
			string text = command.CommandText ?? "";
			if (!string.IsNullOrWhiteSpace(text))
			{
				text = text.TrimStart(Array.Empty<char>());
				if (text.StartsWith("call", StringComparison.OrdinalIgnoreCase))
				{
					flag = true;
				}
				else
				{
					bool? bIsDefineInSelect = null;
					bool? bIsDmlReturn = null;
					flag = ResultSet.GetSqlStatementType(text, ref bIsDefineInSelect, ref bIsDmlReturn) == SqlStatementType.PLSQL;
				}
			}
		}
		if (command.IsOracleCommand && command.Session.UseOraclePrepareExecute && command.Session.UseOracleServerParameterMetadata && command.Session.SupportsAccurateParameterInfo && command.RawParameters is OracleParameterCollection { Count: >0 } oracleParameterCollection)
		{
			PreparedStatement lastUsedPreparedStatement = resultSet.DataReader.LastUsedPreparedStatement;
			if (lastUsedPreparedStatement != null)
			{
				ColumnDefinitionPayload[] parameters = lastUsedPreparedStatement.Parameters;
				if (parameters != null && parameters.Length > 0 && flag)
				{
					preparedStatement = lastUsedPreparedStatement;
					serverParameters = parameters;
					oracleParameters = oracleParameterCollection;
					return true;
				}
			}
		}
		return false;
	}

	private static bool TryResolveBoundOracleParameters(PreparedStatement preparedStatement, int expectedCount, OracleParameterCollection oracleParameters, out List<OracleParameter> boundParameters)
	{
		boundParameters = new List<OracleParameter>(expectedCount);
		List<string> normalizedParameterNames = preparedStatement.Statement.NormalizedParameterNames;
		List<int> parameterIndexes = preparedStatement.Statement.ParameterIndexes;
		if (normalizedParameterNames.Count < expectedCount || parameterIndexes.Count < expectedCount)
		{
			return false;
		}
		for (int i = 0; i < expectedCount; i++)
		{
			string text = normalizedParameterNames[i];
			int num = ((text != null) ? oracleParameters.UnsafeIndexOf(text) : parameterIndexes[i]);
			if (num < 0 || num >= oracleParameters.Count)
			{
				return false;
			}
			boundParameters.Add(oracleParameters[num]);
		}
		return true;
	}

	private static ParameterDirection GetOracleParameterDirection(byte inoutType, ParameterDirection fallbackDirection)
	{
		return inoutType switch
		{
			1 => ParameterDirection.Input, 
			2 => ParameterDirection.Output, 
			3 => ParameterDirection.InputOutput, 
			_ => fallbackDirection, 
		};
	}

	private static ParameterDirection MergeOracleParameterDirections(ParameterDirection left, ParameterDirection right)
	{
		if (left == right)
		{
			return left;
		}
		if (left == ParameterDirection.ReturnValue || right == ParameterDirection.ReturnValue)
		{
			return ParameterDirection.ReturnValue;
		}
		if (left == ParameterDirection.InputOutput || right == ParameterDirection.InputOutput)
		{
			return ParameterDirection.InputOutput;
		}
		if ((left == ParameterDirection.Input && right == ParameterDirection.Output) || (left == ParameterDirection.Output && right == ParameterDirection.Input))
		{
			return ParameterDirection.InputOutput;
		}
		return right;
	}

	private static void RefreshOracleOutputParameterBindings(CommandFacade command, OracleParameterCollection oracleParameters)
	{
		OracleParameterCollection oracleParameterCollection = null;
		OracleParameter oracleParameter = null;
		for (int i = 0; i < oracleParameters.Count; i++)
		{
			OracleParameter oracleParameter2 = oracleParameters[i];
			switch (oracleParameter2.Direction)
			{
			case ParameterDirection.Output:
			case ParameterDirection.InputOutput:
				if (oracleParameterCollection == null)
				{
					oracleParameterCollection = new OracleParameterCollection();
				}
				oracleParameterCollection.Add(oracleParameter2);
				break;
			case ParameterDirection.ReturnValue:
				if (command.CommandType == CommandType.StoredProcedure)
				{
					oracleParameter = oracleParameter2;
					break;
				}
				if (oracleParameterCollection == null)
				{
					oracleParameterCollection = new OracleParameterCollection();
				}
				oracleParameterCollection.Add(oracleParameter2);
				break;
			}
		}
		command.OutParameters = ((oracleParameterCollection != null && oracleParameterCollection.Count > 0) ? oracleParameterCollection : null);
		command.ReturnParameter = ((command.CommandType == CommandType.StoredProcedure) ? oracleParameter : null);
	}

	private void QueuePendingRefCursorIds(CommandFacade command)
	{
		if (!command.IsOracleCommand || !(command.OutParameters is OracleParameterCollection oracleParameterCollection) || !(command.InnerCommand is OracleCommand { EnableStoredProcedureRefCursorResultSets: not false }))
		{
			return;
		}
		for (int i = 0; i < oracleParameterCollection.Count; i++)
		{
			OracleParameter oracleParameter = oracleParameterCollection[i];
			if (oracleParameter != null && oracleParameter.OracleDbType == OracleDbType.RefCursor && oracleParameter.Value is OracleRefCursor { IsNull: false } oracleRefCursor)
			{
				m_pendingRefCursorIds.Enqueue(oracleRefCursor.CursorId);
			}
		}
	}

	private async ValueTask<bool> TryStartPendingRefCursorResultSetAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (m_pendingRefCursorIds.Count == 0 || !Command.HasValue)
		{
			return false;
		}
		if (!(Command.Value.InnerCommand is OracleCommand { EnableStoredProcedureRefCursorResultSets: not false }))
		{
			return false;
		}
		int num = m_pendingRefCursorIds.Dequeue();
		m_commandListPosition.ExpectOraclePrepareExecuteResponse = false;
		m_commandListPosition.LastUsedPreparedStatement = new PreparedStatement(num, new ParsedStatement
		{
			StatementBytes = default(ArraySegment<byte>)
		}, null, null);
		int rowsToFetch = 1024;
		if (Command.Value.InnerCommand is OracleCommand oracleCommand2)
		{
			oracleCommand2.IsRefCursorFetchCommand = true;
			if (oracleCommand2.RefCursorRowsToFetchPerRoundTrip > 0)
			{
				rowsToFetch = oracleCommand2.RefCursorRowsToFetchPerRoundTrip;
			}
		}
		ByteBufferWriter byteBufferWriter = new ByteBufferWriter();
		RefCursorFetchPayloadCreator.WriteFetch(byteBufferWriter, num, rowsToFetch);
		using PayloadData payload = byteBufferWriter.ToPayloadData();
		await Command.Value.Session.SendAsync(payload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		await m_resultSet.ReadResultSetHeaderAsync(ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
		ActivateResultSet(cancellationToken);
		m_hasMoreResults = m_resultSet.BufferState != ResultSetState.NoMoreData;
		return m_hasMoreResults;
	}

	private bool TryMapOutParametersByServerBindInoutType(ResultSet resultSet, Row row, PreparedStatement preparedStatement, ParsedStatement parsed, Dictionary<string, OracleParameter> byName, DbParameter? refCursorParam)
	{
		ColumnDefinitionPayload[] parameters = preparedStatement.Parameters;
		if (parameters == null || parameters.Length <= 0)
		{
			return false;
		}
		int num = Math.Min(parameters.Length, parsed.NormalizedParameterNames.Count);
		int num2 = 0;
		for (int i = 0; i < num; i++)
		{
			byte inoutType = parameters[i].InoutType;
			if ((uint)(inoutType - 2) <= 1u)
			{
				num2++;
			}
		}
		if (num2 == 0 || num2 != resultSet.ColumnDefinitions.Length)
		{
			return false;
		}
		int num3 = 0;
		OracleParameter value = default(OracleParameter);
		for (int j = 0; j < num; j++)
		{
			if (num3 >= resultSet.ColumnDefinitions.Length)
			{
				break;
			}
			byte inoutType = parameters[j].InoutType;
			if ((uint)(inoutType - 2) > 1u)
			{
				continue;
			}
			string text = parsed.NormalizedParameterNames[j];
			bool flag = text != null && text.Length > 0 && byName.TryGetValue(text, out value);
			if (flag)
			{
				ParameterDirection direction = value.Direction;
				bool flag2 = (((uint)(direction - 2) <= 1u || direction == ParameterDirection.ReturnValue) ? true : false);
				flag = flag2;
			}
			if (flag)
			{
				DbParameter dbParameter = value;
				if (resultSet.ColumnDefinitions[num3].ColumnType == ColumnType.OracleCursor && dbParameter is OracleParameter { OracleDbType: not OracleDbType.RefCursor } && refCursorParam != null)
				{
					dbParameter = refCursorParam;
				}
				AssignOutParameterValue(dbParameter, row, num3);
			}
			num3++;
		}
		return true;
	}

	private void AssignOutParameterValue(DbParameter param, Row row, int columnIndex)
	{
		if (row.IsDBNull(columnIndex))
		{
			param.Value = DBNull.Value;
			return;
		}
		object obj = row.GetValue(columnIndex);
		if (obj is OracleUdtValue value)
		{
			object obj2 = OracleUdt.CreateObject(Connection?.AsOracleConnection, value);
			if (obj2 != null)
			{
				obj = obj2;
			}
		}
		if (param is OracleParameter { OracleDbType: OracleDbType.RefCursor } && obj is OracleDataReader)
		{
			return;
		}
		if (param is OracleParameter oracleParameter2 && obj is byte[] array && array.Length != 0)
		{
			OracleConnection oracleConnection = Connection?.AsOracleConnection;
			OracleDbType oracleDbType = oracleParameter2.OracleDbType;
			if ((oracleDbType == OracleDbType.Clob || oracleDbType == OracleDbType.NClob) ? true : false)
			{
				string oracleClobEncodingName = GetOracleClobEncodingName(oracleConnection);
				OracleClob oracleClob = new OracleClob(hasLocator: true, array, oracleClobEncodingName, oracleConnection);
				oracleClob.SetOracleMode(isOracleMode: true);
				param.Value = oracleClob;
				return;
			}
			if (oracleParameter2.OracleDbType == OracleDbType.Blob)
			{
				OracleBlob oracleBlob = new OracleBlob(hasLocator: true, array, null, oracleConnection);
				oracleBlob.SetOracleMode(isOracleMode: true);
				param.Value = oracleBlob;
				return;
			}
		}
		if ((param is MySqlParameter mySqlParameter) ? mySqlParameter.HasSetDbType : (param is OracleParameter oracleParameter3 && oracleParameter3.HasSetDbType))
		{
			DbTypeMapping dbTypeMapping = TypeMapper.Instance.GetDbTypeMapping(param.DbType);
			if (dbTypeMapping != null && param.DbType != DbType.Object)
			{
				param.Value = dbTypeMapping.DoConversion(obj);
				return;
			}
		}
		param.Value = obj;
	}

	private void VerifyNotDisposed()
	{
		if (!Command.HasValue)
		{
			throw new InvalidOperationException("Can't call this method when OracleDataReader is closed.");
		}
	}

	private ResultSet GetResultSet()
	{
		VerifyNotDisposed();
		if (m_resultSet != null && !m_resultSet.ContainsCommandParameters)
		{
			return m_resultSet;
		}
		throw new InvalidOperationException("There is no current result set.");
	}
}
