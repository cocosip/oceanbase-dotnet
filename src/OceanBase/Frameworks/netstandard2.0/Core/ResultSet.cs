using System;
using System.Buffers;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OceanBase.Protocol;
using OceanBase.Protocol.Payloads;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;

namespace OceanBase.Core;

internal sealed class ResultSet(IResultSetOwner dataReader)
{
	private ResizableArray<byte>? m_columnDefinitionPayloadBytes;

	private int m_columnDefinitionPayloadUsedBytes;

	private Queue<Row>? m_readBuffer;

	private Row? m_row;

	private bool m_hasRows;

	private ReadOnlyMemory<ColumnDefinitionPayload> m_columnDefinitions;

	private ColumnDefinitionPayload[]? m_columnDefinitionPayloadCache;

	private bool? m_isDefineInSelect;

	private bool m_oraclePrepareExecuteTailPending;

	public int Depth => 0;

	public int FieldCount => ColumnDefinitions.Length;

	public bool HasRows
	{
		get
		{
			if (BufferState == ResultSetState.ReadResultSetHeader)
			{
				return BufferReadAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult() != null;
			}
			return m_hasRows;
		}
	}

	public IResultSetOwner DataReader { get; } = dataReader;

	public ExceptionDispatchInfo? ReadResultSetHeaderException { get; private set; }

	public CommandFacade Command => DataReader.Command.Value;

	public ConnectionFacade Connection => DataReader.Connection.Value;

	public ServerSession Session => DataReader.Session;

	public ResultSetState BufferState { get; private set; }

	public ReadOnlySpan<ColumnDefinitionPayload> ColumnDefinitions => m_columnDefinitions.Span;

	public int WarningCount { get; private set; }

	public ResultSetState State { get; private set; }

	public bool HasResultSet
	{
		get
		{
			if (State != ResultSetState.None)
			{
				return ColumnDefinitions.Length != 0;
			}
			return false;
		}
	}

	public bool ContainsCommandParameters { get; private set; }

	public void Reset()
	{
		BufferState = ResultSetState.None;
		m_columnDefinitions = default(ReadOnlyMemory<ColumnDefinitionPayload>);
		WarningCount = 0;
		State = ResultSetState.None;
		ContainsCommandParameters = false;
		m_oraclePrepareExecuteTailPending = false;
		m_columnDefinitionPayloadUsedBytes = 0;
		m_readBuffer?.Clear();
		m_row = null;
		m_hasRows = false;
		ReadResultSetHeaderException = null;
	}

	public async Task ReadResultSetHeaderAsync(IOBehavior ioBehavior)
	{
		Reset();
		try
		{
			if (Session.UseOraclePrepareExecute && DataReader.ExpectOraclePrepareExecuteResponse)
			{
				await ReadPrepareExecuteResultSetHeaderAsync(ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
				return;
			}
			while (true)
			{
				PayloadData payload = await Session.ReceiveReplyAsync(ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
				switch (payload.HeaderByte)
				{
				case 0:
					if (!HandleOkPayload(payload))
					{
						return;
					}
					break;
				case 251:
					await HandleLocalInfilePayloadAsync(payload, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
					break;
				default:
					await ReadResultSetHeaderFromColumnCountAsync(payload, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
					return;
				}
			}
		}
		catch (Exception source)
		{
			ReadResultSetHeaderException = ExceptionDispatchInfo.Capture(source);
		}
		finally
		{
			BufferState = State;
		}
	}

	private async Task ReadPrepareExecuteResultSetHeaderAsync(IOBehavior ioBehavior)
	{
		PayloadData payloadData = await Session.ReceiveReplyAsync(ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
		StatementPrepareExecuteResponsePayload header = StatementPrepareExecuteResponsePayload.Create(payloadData.Span);
		PreparedStatement lastUsedPreparedStatement = DataReader.LastUsedPreparedStatement;
		if (lastUsedPreparedStatement != null)
		{
			lastUsedPreparedStatement.StatementId = header.StatementId;
		}
		ResizableArray<byte> columnsAndParameters = new ResizableArray<byte>();
		int columnsAndParametersSize = 0;
		if (header.ParameterCount > 0)
		{
			PreparedStatement ps = DataReader.LastUsedPreparedStatement;
			if (ps != null)
			{
				PreparedStatement preparedStatement = ps;
				if (preparedStatement.Parameters == null)
				{
					preparedStatement.Parameters = new ColumnDefinitionPayload[header.ParameterCount];
				}
				for (int i = 0; i < header.ParameterCount; i++)
				{
					payloadData = await Session.ReceiveReplyAsync(ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
					int length = payloadData.Span.Length;
					Utility.Resize(ref columnsAndParameters, columnsAndParametersSize + length);
					payloadData.Span.CopyTo(columnsAndParameters.AsSpan(columnsAndParametersSize));
					ColumnDefinitionPayload.Initialize(ref ps.Parameters[i], new ResizableArraySegment<byte>(columnsAndParameters, columnsAndParametersSize, length), Session.UseOracleMode);
					columnsAndParametersSize += length;
				}
				if (!Session.SupportsDeprecateEof)
				{
					await ReadEofPacketAsync(ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
		}
		if (header.ColumnCount > 0)
		{
			await ReadAndCacheColumnDefinitionsAsync(header.ColumnCount, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
			if (!Session.SupportsDeprecateEof)
			{
				await ReadEofPacketAsync(ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		DetermineContainsCommandParameters();
		WarningCount = 0;
		if (header.HasResultSet)
		{
			if (header.IsArrayBindingResult)
			{
				await ReadArrayBindingInternalResultSetAsync(ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
				return;
			}
			m_oraclePrepareExecuteTailPending = true;
			State = ResultSetState.ReadResultSetHeader;
			return;
		}
		ProtocolErrorBehavior protocolErrorBehavior = (Session.IgnorePrepareExecuteSequenceCheck ? ProtocolErrorBehavior.Ignore : ProtocolErrorBehavior.Throw);
		PayloadData payload = await Session.ReceiveReplyAsync(protocolErrorBehavior, ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
		if (payload.HeaderByte == byte.MaxValue)
		{
			throw ErrorPayload.Create(payload.Span).ToException();
		}
		HandleOkPayload(payload);
		State = ResultSetState.NoMoreData;
	}

	/// <summary>
	/// Reads and internally consumes the ArrayBinding internal result set returned by OceanBase Oracle-mode
	/// COM_STMT_PREPARE_EXECUTE when the server's extendFlag has ArrayBindingField (0x0002) set.
	///
	/// Newer servers include sql_no, error_code, error_message, and affect_rows.
	/// Older servers omit affect_rows and expose only per-iteration success/error status.
	///
	/// Rows are consumed here and NEVER forwarded to the user's OracleDataReader.
	/// After all rows are drained the trailing OK/ERR tail packet is read.
	/// If any rows failed (error_code != 0) an aggregated exception is thrown after
	/// ArrayBindRowsAffected is set so the caller can still inspect partial results.
	/// </summary>
	private async Task ReadArrayBindingInternalResultSetAsync(IOBehavior ioBehavior)
	{
		int oracleArrayBindCount = Command.OracleArrayBindCount;
		long[] rowsAffected = ((oracleArrayBindCount > 0) ? new long[oracleArrayBindCount] : Array.Empty<long>());
		List<(int SqlNo, int ErrorCode)> errors = null;
		int sqlNoOrdinal = GetArrayBindingColumnOrdinal("sql_no", 0);
		int errorCodeOrdinal = GetArrayBindingColumnOrdinal("error_code", 1);
		int affectRowsOrdinal = GetArrayBindingColumnOrdinal("affect_rows", -1);
		Row row = new Row(isBinary: true, this);
		int iterationIndex = 0;
		while (true)
		{
			PayloadData payloadData = await Session.ReceiveReplyAsync(ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
			if (payloadData.HeaderByte == 254)
			{
				break;
			}
			if (payloadData.HeaderByte == byte.MaxValue)
			{
				throw ErrorPayload.Create(payloadData.Span).ToException();
			}
			row.SetData(payloadData.Memory);
			long num = Convert.ToInt64(row.GetValue(sqlNoOrdinal), CultureInfo.InvariantCulture);
			int num2 = Convert.ToInt32(row.GetValue(errorCodeOrdinal), CultureInfo.InvariantCulture);
			int num3 = iterationIndex;
			long num4 = ((num2 == 0) ? num : 0);
			if (affectRowsOrdinal >= 0)
			{
				num3 = checked((int)num);
				object value = row.GetValue(affectRowsOrdinal);
				num4 = ((value is DBNull) ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture));
			}
			iterationIndex++;
			if (num3 >= 0 && num3 < rowsAffected.Length)
			{
				rowsAffected[num3] = num4;
			}
			if (num2 != 0)
			{
				if (errors == null)
				{
					errors = new List<(int, int)>();
				}
				errors.Add((num3, num2));
			}
		}
		Command.SetOracleArrayBindRowsAffected(rowsAffected);
		ProtocolErrorBehavior protocolErrorBehavior = (Session.IgnorePrepareExecuteSequenceCheck ? ProtocolErrorBehavior.Ignore : ProtocolErrorBehavior.Throw);
		PayloadData payload;
		try
		{
			payload = await Session.ReceiveReplyAsync(protocolErrorBehavior, ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (MySqlException)
		{
			State = ResultSetState.NoMoreData;
			throw;
		}
		if (payload.HeaderByte == byte.MaxValue)
		{
			State = ResultSetState.NoMoreData;
			throw ErrorPayload.Create(payload.Span).ToException();
		}
		HandleOkPayload(payload);
		State = ResultSetState.NoMoreData;
		if (errors != null && errors.Count > 0)
		{
			string text = string.Join("; ", errors.Select(((int SqlNo, int ErrorCode) e) => $"sql_no={e.SqlNo} error_code={e.ErrorCode}"));
			throw new MySqlException("OBE-24381: error(s) in array DML: " + text);
		}
	}

	private int GetArrayBindingColumnOrdinal(string name, int fallbackOrdinal)
	{
		for (int i = 0; i < ColumnDefinitions.Length; i++)
		{
			if (ColumnDefinitions[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}
		if (fallbackOrdinal >= 0 && fallbackOrdinal < ColumnDefinitions.Length)
		{
			return fallbackOrdinal;
		}
		return -1;
	}

	private bool HandleOkPayload(PayloadData payload)
	{
		OkPayload okPayload = OkPayload.Create(payload.Span, Session);
		bool flag = State != ResultSetState.ReadResultSetHeader || okPayload.AffectedRowCount != 0;
		if (!flag && Session.UseOracleMode)
		{
			bool? bIsDmlReturn = null;
			if (GetSqlStatementType(Command.CommandText, ref m_isDefineInSelect, ref bIsDmlReturn) == SqlStatementType.DML)
			{
				flag = true;
			}
		}
		if (flag)
		{
			if (Session.UseOracleMode)
			{
				bool? bIsDmlReturn2 = null;
				if (GetSqlStatementType(Command.CommandText, ref m_isDefineInSelect, ref bIsDmlReturn2) == SqlStatementType.DML)
				{
					DataReader.RealRecordsAffected = DataReader.RealRecordsAffected.GetValueOrDefault() + okPayload.AffectedRowCount;
				}
			}
			else
			{
				DataReader.RealRecordsAffected = DataReader.RealRecordsAffected.GetValueOrDefault() + okPayload.AffectedRowCount;
			}
		}
		if (okPayload.LastInsertId != 0L)
		{
			Command.SetLastInsertedId((long)okPayload.LastInsertId);
		}
		WarningCount = okPayload.WarningCount;
		if (okPayload.NewSchema != null)
		{
			Session.DatabaseOverride = okPayload.NewSchema;
		}
		m_columnDefinitions = default(ReadOnlyMemory<ColumnDefinitionPayload>);
		State = (((okPayload.ServerStatus & ServerStatus.MoreResultsExist) == 0) ? ResultSetState.NoMoreData : ResultSetState.HasMoreData);
		return State != ResultSetState.NoMoreData;
	}

	private async Task HandleLocalInfilePayloadAsync(PayloadData payload, IOBehavior ioBehavior)
	{
		try
		{
			if (!Connection.AllowLoadLocalInfile)
			{
				throw new NotSupportedException("To use LOAD DATA LOCAL INFILE, set AllowLoadLocalInfile=true in the connection string. See https://mysqlconnector.net/load-data");
			}
			LocalInfilePayload localInfilePayload = LocalInfilePayload.Create(payload.Span);
			bool flag = localInfilePayload.FileName.StartsWith(":SOURCE:", StringComparison.Ordinal);
			if (!IsHostVerified(Connection.SslMode) && !flag)
			{
				throw new NotSupportedException("Use SourceStream or SslMode >= VerifyCA for LOAD DATA LOCAL INFILE. See https://mysqlconnector.net/load-data");
			}
			object obj = (flag ? MySqlBulkLoader.GetAndRemoveSource(localInfilePayload.FileName) : File.OpenRead(localInfilePayload.FileName));
			if (!(obj is Stream stream))
			{
				if (!(obj is MySqlBulkCopy mySqlBulkCopy))
				{
					throw new InvalidOperationException("Unsupported Source type: " + obj.GetType().Name);
				}
				await mySqlBulkCopy.SendDataReaderAsync(ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
			}
			else
			{
				byte[] buffer = ArrayPool<byte>.Shared.Rent(1048576);
				try
				{
					int count;
					while ((count = await stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false)) > 0)
					{
						PayloadData payload2 = new PayloadData(new ArraySegment<byte>(buffer, 0, count));
						await Session.SendReplyAsync(payload2, ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
					}
				}
				finally
				{
					ArrayPool<byte>.Shared.Return(buffer);
					stream.Dispose();
				}
			}
		}
		catch (Exception innerException)
		{
			ReadResultSetHeaderException = ExceptionDispatchInfo.Capture(new MySqlException("Error during LOAD DATA LOCAL INFILE", innerException));
		}
		await Session.SendReplyAsync(EmptyPayload.Instance, ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task ReadResultSetHeaderFromColumnCountAsync(PayloadData payload, IOBehavior ioBehavior)
	{
		ColumnCountPayload columnCountPayload = ColumnCountPayload.Create(payload.Span, Session.SupportsCachedPreparedMetadata);
		int columnCount = columnCountPayload.ColumnCount;
		if (columnCountPayload.MetadataFollows)
		{
			await ReadAndCacheColumnDefinitionsAsync(columnCount, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
		}
		else
		{
			ReusePreviousMetadata(columnCount);
		}
		if (!Session.SupportsDeprecateEof)
		{
			await ReadEofPacketAsync(ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
		}
		DetermineContainsCommandParameters();
		WarningCount = 0;
		State = ResultSetState.ReadResultSetHeader;
		Activity activity = DataReader.Activity;
		if (activity != null && activity.IsAllDataRequested && (Connection.DataSource?.TracingOptions.EnableResultSetHeaderEvent ?? MySqlConnectorTracingOptions.Default.EnableResultSetHeaderEvent))
		{
			DataReader.Activity.AddEvent(new ActivityEvent("read-result-set-header"));
		}
	}

	private void ReusePreviousMetadata(int columnCount)
	{
		m_columnDefinitions = DataReader.LastUsedPreparedStatement.Columns;
		if (m_columnDefinitions.Length != columnCount)
		{
			throw new InvalidOperationException($"Expected result set to have {m_columnDefinitions.Length} columns, but it contains {columnCount} columns");
		}
	}

	private async Task ReadAndCacheColumnDefinitionsAsync(int columnCount, IOBehavior ioBehavior)
	{
		Utility.Resize(ref m_columnDefinitionPayloadBytes, columnCount * 96);
		if (m_columnDefinitionPayloadCache == null)
		{
			m_columnDefinitionPayloadCache = new ColumnDefinitionPayload[columnCount];
		}
		else if (m_columnDefinitionPayloadCache.Length < columnCount)
		{
			Array.Resize(ref m_columnDefinitionPayloadCache, Math.Max(columnCount, m_columnDefinitionPayloadCache.Length * 2));
		}
		m_columnDefinitions = m_columnDefinitionPayloadCache.AsMemory(0, columnCount);
		ColumnDefinitionPayload[] preparedColumns = ((!Session.SupportsCachedPreparedMetadata) ? null : DataReader.LastUsedPreparedStatement?.Columns);
		for (int column = 0; column < columnCount; column++)
		{
			PayloadData payloadData = await Session.ReceiveReplyAsync(ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
			int length = payloadData.Span.Length;
			if (m_columnDefinitionPayloadUsedBytes + length > m_columnDefinitionPayloadBytes.Count)
			{
				Utility.Resize(ref m_columnDefinitionPayloadBytes, m_columnDefinitionPayloadUsedBytes + length);
			}
			payloadData.Span.CopyTo(m_columnDefinitionPayloadBytes.AsSpan(m_columnDefinitionPayloadUsedBytes));
			ResizableArraySegment<byte> arraySegment = new ResizableArraySegment<byte>(m_columnDefinitionPayloadBytes, m_columnDefinitionPayloadUsedBytes, length);
			ColumnDefinitionPayload.Initialize(ref m_columnDefinitionPayloadCache[column], arraySegment, Session.UseOracleMode);
			if (preparedColumns != null)
			{
				ColumnDefinitionPayload.Initialize(ref preparedColumns[column], arraySegment, Session.UseOracleMode);
			}
			m_columnDefinitionPayloadUsedBytes += length;
		}
	}

	private async Task ReadEofPacketAsync(IOBehavior ioBehavior)
	{
		EofPayload.Create((await Session.ReceiveReplyAsync(ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false)).Span);
	}

	private void DetermineContainsCommandParameters()
	{
		if (Session.UseOracleMode && Command.IsOracleCommand && Command.InnerCommand is OracleCommand { IsRefCursorFetchCommand: not false })
		{
			return;
		}
		DbParameterCollection outParameters = Command.OutParameters;
		if (outParameters != null && outParameters.Count > 0)
		{
			bool num = ColumnDefinitions.Length == outParameters.Count + 1 && ColumnDefinitions[0].Name == SingleCommandPayloadCreator.OutParameterSentinelColumnName;
			bool flag = DataReader.LastUsedPreparedStatement != null && ColumnDefinitions.Length == outParameters.Count;
			bool flag2 = IsOracleModePsOutParameterResultSet(outParameters);
			if (num || (!Session.UseOracleMode && flag) || flag2)
			{
				ContainsCommandParameters = true;
				return;
			}
		}
		if (IsOracleModePsOutParameterResultSetFromServerMetadata())
		{
			ContainsCommandParameters = true;
		}
	}

	private bool IsOracleModePsOutParameterResultSet(DbParameterCollection outParameters)
	{
		bool flag = false;
		if (Session.UseOracleMode && DataReader.LastUsedPreparedStatement != null && outParameters.Count > 0 && ColumnDefinitions.Length >= outParameters.Count)
		{
			for (int i = 0; i < ColumnDefinitions.Length; i++)
			{
				if (ColumnDefinitions[i].InoutType != 0)
				{
					flag = true;
					break;
				}
			}
			if (!flag && Command.RawParameters is OracleParameterCollection oracleParameterCollection)
			{
				Dictionary<string, OracleParameter> dictionary = new Dictionary<string, OracleParameter>(StringComparer.OrdinalIgnoreCase);
				for (int j = 0; j < oracleParameterCollection.Count; j++)
				{
					OracleParameter oracleParameter = oracleParameterCollection[j];
					dictionary[oracleParameter.NormalizedParameterName] = oracleParameter;
				}
				int num = 0;
				ParsedStatement statement = DataReader.LastUsedPreparedStatement.Statement;
				for (int k = 0; k < statement.NormalizedParameterNames.Count; k++)
				{
					string text = statement.NormalizedParameterNames[k];
					if (text != null && text.Length != 0 && dictionary.TryGetValue(text, out var value))
					{
						ParameterDirection direction = value.Direction;
						if (((uint)(direction - 2) <= 1u || direction == ParameterDirection.ReturnValue) ? true : false)
						{
							num++;
						}
					}
				}
				if (num > 0 && ColumnDefinitions.Length == num)
				{
					flag = true;
				}
			}
			if (!flag)
			{
				HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				for (int l = 0; l < outParameters.Count; l++)
				{
					if (outParameters[l] is OracleParameter oracleParameter2)
					{
						hashSet.Add(oracleParameter2.NormalizedParameterName);
						continue;
					}
					DbParameter dbParameter = outParameters[l];
					if (dbParameter != null)
					{
						hashSet.Add(OracleParameter.NormalizeParameterName(dbParameter.ParameterName));
					}
				}
				flag = hashSet.Count > 0;
				int num2 = 0;
				while (flag && num2 < ColumnDefinitions.Length)
				{
					string name = ColumnDefinitions[num2].Name;
					if (string.IsNullOrEmpty(name) || !hashSet.Contains(OracleParameter.NormalizeParameterName(name)))
					{
						flag = false;
					}
					num2++;
				}
			}
		}
		return flag;
	}

	private bool IsOracleModePsOutParameterResultSetFromServerMetadata()
	{
		if (Session.UseOracleMode)
		{
			ColumnDefinitionPayload[] array = DataReader.LastUsedPreparedStatement?.Parameters;
			if (array != null && array.Length > 0 && ColumnDefinitions.Length != 0)
			{
				int num = 0;
				for (int i = 0; i < array.Length; i++)
				{
					byte inoutType = array[i].InoutType;
					if ((uint)(inoutType - 2) <= 1u)
					{
						num++;
					}
				}
				if (num == 0)
				{
					return false;
				}
				for (int j = 0; j < ColumnDefinitions.Length; j++)
				{
					if (ColumnDefinitions[j].InoutType != 0)
					{
						return true;
					}
				}
				return ColumnDefinitions.Length == num;
			}
		}
		return false;
	}

	private static bool IsHostVerified(MySqlSslMode sslMode)
	{
		if ((uint)(sslMode - 3) <= 1u)
		{
			return true;
		}
		return false;
	}

	public async Task ReadEntireAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		while (true)
		{
			ResultSetState state = State;
			if ((uint)(state - 1) > 1u)
			{
				break;
			}
			await ReadAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public bool Read()
	{
		return ReadAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
	}

	public Task<bool> ReadAsync(CancellationToken cancellationToken)
	{
		return ReadAsync(Connection.AsyncIOBehavior, cancellationToken);
	}

	public async Task<bool> ReadAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		Queue<Row>? readBuffer = m_readBuffer;
		Row row = ((readBuffer == null || readBuffer.Count <= 0) ? (await ScanRowAsync(ioBehavior, m_row, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) : m_readBuffer.Dequeue());
		m_row = row;
		CommandFacade command = Command;
		if (command.ReturnParameter != null && m_row != null)
		{
			command.ReturnParameter.Value = m_row.GetValue(0);
			command.ReturnParameter = null;
		}
		if (m_row == null)
		{
			State = BufferState;
			return false;
		}
		State = ResultSetState.ReadingRows;
		return true;
	}

	public async Task<Row?> BufferReadAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		Row row = await ScanRowAsync(ioBehavior, null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (row == null)
		{
			return null;
		}
		if (m_readBuffer == null)
		{
			m_readBuffer = new Queue<Row>();
		}
		m_readBuffer.Enqueue(row);
		return row;
	}

	private async ValueTask<Row?> ScanRowAsync(IOBehavior ioBehavior, Row? row, CancellationToken cancellationToken)
	{
		ResultSetState bufferState = BufferState;
		if ((bufferState == ResultSetState.None || (uint)(bufferState - 3) <= 1u) ? true : false)
		{
			return null;
		}
		PayloadData payload;
		while (true)
		{
			try
			{
				payload = await Session.ReceiveReplyAsync(ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (MySqlException ex)
			{
				ResultSet resultSet = this;
				bufferState = (State = ResultSetState.NoMoreData);
				resultSet.BufferState = bufferState;
				if (ex.ErrorCode == MySqlErrorCode.QueryInterrupted && cancellationToken.IsCancellationRequested)
				{
					throw new OperationCanceledException(ex.Message, ex, cancellationToken);
				}
				if (ex.ErrorCode == MySqlErrorCode.QueryInterrupted && Command.CancellableCommand.IsTimedOut)
				{
					throw MySqlException.CreateForTimeout(ex);
				}
				throw;
			}
			if (payload.HeaderByte != 254)
			{
				break;
			}
			ServerStatus serverStatus = ((Session.SupportsDeprecateEof && OkPayload.IsOk(payload.Span, Session)) ? OkPayload.Create(payload.Span, Session).ServerStatus : ((!Session.SupportsDeprecateEof && EofPayload.IsEof(payload)) ? EofPayload.Create(payload.Span).ServerStatus : ((ServerStatus)0)));
			if (Session.UseOracleMode && (serverStatus & ServerStatus.CursorExists) != 0 && (serverStatus & ServerStatus.LastRowSent) == 0 && Command.IsOracleCommand && Command.InnerCommand is OracleCommand oracleCommand)
			{
				if (oracleCommand.IsRefCursorFetchCommand)
				{
					if (oracleCommand.CloseRefCursorResultSetAfterScalar)
					{
						State = ResultSetState.NoMoreData;
						BufferState = State;
						return null;
					}
					await SendRefCursorFetchAsync(ioBehavior, oracleCommand.RefCursorRowsToFetchPerRoundTrip, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					await ReadResultSetHeaderAsync(ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
					continue;
				}
				if (DataReader.LastUsedPreparedStatement != null)
				{
					if (m_oraclePrepareExecuteTailPending)
					{
						m_oraclePrepareExecuteTailPending = false;
						ProtocolErrorBehavior protocolErrorBehavior = (Session.IgnorePrepareExecuteSequenceCheck ? ProtocolErrorBehavior.Ignore : ProtocolErrorBehavior.Throw);
						PayloadData payload2 = await Session.ReceiveReplyAsync(protocolErrorBehavior, ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
						if (payload2.HeaderByte == byte.MaxValue)
						{
							throw ErrorPayload.Create(payload2.Span).ToException();
						}
						HandleOkPayload(payload2);
					}
					await SendRefCursorFetchAsync(ioBehavior, Session.PrepareExecuteFetchSize, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					await ReadResultSetHeaderAsync(ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
					continue;
				}
			}
			if (m_oraclePrepareExecuteTailPending)
			{
				m_oraclePrepareExecuteTailPending = false;
				ProtocolErrorBehavior protocolErrorBehavior2 = (Session.IgnorePrepareExecuteSequenceCheck ? ProtocolErrorBehavior.Ignore : ProtocolErrorBehavior.Throw);
				PayloadData payload3 = await Session.ReceiveReplyAsync(protocolErrorBehavior2, ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
				if (payload3.HeaderByte == byte.MaxValue)
				{
					throw ErrorPayload.Create(payload3.Span).ToException();
				}
				HandleOkPayload(payload3);
				State = ResultSetState.NoMoreData;
				BufferState = State;
				return null;
			}
			BufferState = (((serverStatus & ServerStatus.MoreResultsExist) == 0) ? ResultSetState.NoMoreData : ResultSetState.HasMoreData);
			return null;
		}
		if (row == null)
		{
			row = new Row(DataReader.LastUsedPreparedStatement != null, this);
		}
		row.SetData(payload.Memory);
		m_hasRows = true;
		BufferState = ResultSetState.ReadingRows;
		return row;
	}

	private async ValueTask SendRefCursorFetchAsync(IOBehavior ioBehavior, int rowsToFetch, CancellationToken cancellationToken)
	{
		int num = DataReader.LastUsedPreparedStatement?.StatementId ?? 0;
		if (num == 0)
		{
			throw new InvalidOperationException("Missing cursor id for COM_STMT_FETCH.");
		}
		ByteBufferWriter byteBufferWriter = new ByteBufferWriter();
		RefCursorFetchPayloadCreator.WriteFetch(byteBufferWriter, num, rowsToFetch);
		using PayloadData payload = byteBufferWriter.ToPayloadData();
		await Session.SendAsync(payload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public string GetName(int ordinal)
	{
		if (!HasResultSet)
		{
			throw new InvalidOperationException("There is no current result set.");
		}
		if (ordinal < 0 || ordinal >= ColumnDefinitions.Length)
		{
			throw new IndexOutOfRangeException($"value must be between 0 and {ColumnDefinitions.Length - 1}");
		}
		return ColumnDefinitions[ordinal].Name;
	}

	public string GetDataTypeName(int ordinal)
	{
		if (!HasResultSet)
		{
			throw new InvalidOperationException("There is no current result set.");
		}
		if (ordinal < 0 || ordinal >= ColumnDefinitions.Length)
		{
			throw new IndexOutOfRangeException($"value must be between 0 and {ColumnDefinitions.Length - 1}");
		}
		if (Session.UseOracleMode)
		{
			OracleDbType oracleColumnType = GetOracleColumnType(ordinal);
			return TypeMapper.Instance.GetColumnTypeMetadata(oracleColumnType).SimpleDataTypeName;
		}
		MySqlDbType columnType = GetColumnType(ordinal);
		if (columnType == MySqlDbType.String)
		{
			return string.Format(CultureInfo.InvariantCulture, "CHAR({0})", ColumnDefinitions[ordinal].ColumnLength / ProtocolUtility.GetBytesPerCharacter(ColumnDefinitions[ordinal].CharacterSet));
		}
		return TypeMapper.Instance.GetColumnTypeMetadata(columnType).SimpleDataTypeName;
	}

	public Type GetFieldType(int ordinal)
	{
		if (!HasResultSet)
		{
			throw new InvalidOperationException("There is no current result set.");
		}
		if (ordinal < 0 || ordinal >= ColumnDefinitions.Length)
		{
			throw new IndexOutOfRangeException($"value must be between 0 and {ColumnDefinitions.Length - 1}");
		}
		Type type = (Session.UseOracleMode ? TypeMapper.Instance.GetColumnTypeMetadata(GetOracleColumnType(ordinal)).DbTypeMapping.ClrType : TypeMapper.Instance.GetColumnTypeMetadata(GetColumnType(ordinal)).DbTypeMapping.ClrType);
		if (Connection.AllowZeroDateTime && type == typeof(DateTime))
		{
			type = typeof(MySqlDateTime);
		}
		return type;
	}

	public MySqlDbType GetColumnType(int ordinal)
	{
		return TypeMapper.ConvertToMySqlDbType(ColumnDefinitions[ordinal], Connection.TreatTinyAsBoolean, Connection.GuidFormat);
	}

	public OracleDbType GetOracleColumnType(int ordinal)
	{
		return TypeMapper.ConvertToOracleDbType(ColumnDefinitions[ordinal], Connection.TreatTinyAsBoolean);
	}

	public int GetOrdinal(string name)
	{
		if (name == null)
		{
			throw new ArgumentNullException("name");
		}
		if (!HasResultSet)
		{
			throw new InvalidOperationException("There is no current result set.");
		}
		for (int i = 0; i < ColumnDefinitions.Length; i++)
		{
			if (name.Equals(ColumnDefinitions[i].Name, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}
		throw new IndexOutOfRangeException("The column name '" + name + "' does not exist in the result set.");
	}

	public Row GetCurrentRow()
	{
		if (State != ResultSetState.ReadingRows)
		{
			throw new InvalidOperationException("Read must be called first.");
		}
		return m_row ?? throw new InvalidOperationException("There is no current row.");
	}

	/// <summary>
	/// Determines the SQL statement type by examining the command text.
	/// This is a simple string prefix match, not a full SQL parser.
	/// maybe this method will be used by other methods in the future.
	/// </summary>
	internal static SqlStatementType GetSqlStatementType(string? cmdText, ref bool? bIsDefineInSelect, ref bool? bIsDmlReturn)
	{
		if (string.IsNullOrWhiteSpace(cmdText))
		{
			return SqlStatementType.OTHERS;
		}
		string text = cmdText.TrimStart(Array.Empty<char>());
		while (text.Length > 0)
		{
			if (text.StartsWith("--", StringComparison.Ordinal))
			{
				int num = text.IndexOf('\n');
				if (num >= 0)
				{
					string text2 = text;
					int num2 = num + 1;
					text = text2.Substring(num2, text2.Length - num2).TrimStart(Array.Empty<char>());
					continue;
				}
				return SqlStatementType.OTHERS;
			}
			if (text.Length > 0 && text[0] == '#')
			{
				int num3 = text.IndexOf('\n');
				if (num3 >= 0)
				{
					string text2 = text;
					int num2 = num3 + 1;
					text = text2.Substring(num2, text2.Length - num2).TrimStart(Array.Empty<char>());
					continue;
				}
				return SqlStatementType.OTHERS;
			}
			if (!text.StartsWith("/*", StringComparison.Ordinal))
			{
				break;
			}
			int num4 = text.IndexOf("*/", StringComparison.Ordinal);
			if (num4 >= 0)
			{
				string text2 = text;
				int num2 = num4 + 2;
				text = text2.Substring(num2, text2.Length - num2).TrimStart(Array.Empty<char>());
				continue;
			}
			return SqlStatementType.OTHERS;
		}
		if (text.Length == 0)
		{
			return SqlStatementType.OTHERS;
		}
		SqlStatementType result;
		if (text.StartsWith("SELECT", StringComparison.InvariantCultureIgnoreCase) || text.StartsWith("WITH", StringComparison.InvariantCultureIgnoreCase))
		{
			result = SqlStatementType.SELECT;
			if (bIsDefineInSelect.HasValue)
			{
				int num5 = 0;
				int num6 = 0;
				int num7 = 0;
				num5 = text.IndexOf("select", StringComparison.OrdinalIgnoreCase);
				num6 = text.IndexOf("from", StringComparison.OrdinalIgnoreCase);
				while (num5 != -1 && num6 != -1)
				{
					num7 = text.IndexOf(':');
					if (num7 > num5 && num7 < num6)
					{
						bIsDefineInSelect = true;
						break;
					}
					num5 = text.IndexOf("select", num6 + 3, StringComparison.OrdinalIgnoreCase);
					if (num5 != -1)
					{
						num6 = text.IndexOf("from", num5 + 5, StringComparison.OrdinalIgnoreCase);
					}
				}
			}
		}
		else if (text.StartsWith("INSERT", StringComparison.InvariantCultureIgnoreCase) || text.StartsWith("DELETE", StringComparison.InvariantCultureIgnoreCase) || text.StartsWith("UPDATE", StringComparison.InvariantCultureIgnoreCase))
		{
			result = SqlStatementType.DML;
			if (bIsDmlReturn.HasValue)
			{
				foreach (Match item in new Regex("'[^']*(''|[^'])*'|(RETURN(ING)?\\s+(?<columns>[\\s\\S]+?)\\s+INTO\\s+(?<variables>:[\\w]+))", RegexOptions.IgnoreCase | RegexOptions.Multiline).Matches(text))
				{
					if (item.Groups["columns"].Success)
					{
						bIsDmlReturn = true;
						break;
					}
				}
			}
		}
		else
		{
			result = (text.StartsWith("MERGE", StringComparison.InvariantCultureIgnoreCase) ? SqlStatementType.DML : ((!text.StartsWith("begin", StringComparison.InvariantCultureIgnoreCase) && !text.StartsWith("BEGIN", StringComparison.InvariantCultureIgnoreCase) && !text.StartsWith("declare", StringComparison.InvariantCultureIgnoreCase) && !text.StartsWith("DECLARE", StringComparison.InvariantCultureIgnoreCase)) ? SqlStatementType.OTHERS : SqlStatementType.PLSQL));
		}
		return result;
	}
}
