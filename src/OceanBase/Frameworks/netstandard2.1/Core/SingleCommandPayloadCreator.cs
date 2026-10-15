using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OceanBase.Logging;
using OceanBase.Protocol;
using OceanBase.Protocol.Payloads;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;

namespace OceanBase.Core;

internal sealed class SingleCommandPayloadCreator : ICommandPayloadCreator
{
	public static ICommandPayloadCreator Instance { get; } = new SingleCommandPayloadCreator();

	public static string OutParameterSentinelColumnName => "\ue001\b\v";

	public async ValueTask SendCommandPrologueAsync(CommandListPosition commandListPosition, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		CommandFacade command = commandListPosition.CommandAt(commandListPosition.CommandIndex);
		bool num = command.Session?.UseOracleMode ?? false;
		PreparedStatements preparedStatements = commandListPosition.PreparedStatements ?? command.TryGetPreparedStatements();
		bool? bIsDefineInSelect = null;
		bool? bIsDmlReturn = false;
		bool flag = command.CommandType == CommandType.StoredProcedure || (command.CommandType == CommandType.Text && ResultSet.GetSqlStatementType(command.CommandText, ref bIsDefineInSelect, ref bIsDmlReturn) == SqlStatementType.PLSQL);
		if (num && preparedStatements == null && !flag && (bIsDmlReturn ?? false))
		{
			flag = true;
		}
		if (num && preparedStatements == null && !flag && GetOracleArrayBindCount(command) > 0)
		{
			flag = true;
		}
		if (num && preparedStatements == null && flag)
		{
			await command.Session.PrepareAsync(command, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			preparedStatements = command.TryGetPreparedStatements();
		}
		if (preparedStatements == null)
		{
			return;
		}
		PreparedStatement preparedStatement = preparedStatements.Statements[commandListPosition.PreparedStatementIndex];
		ColumnDefinitionPayload[] parameters = preparedStatement.Parameters;
		if (parameters == null)
		{
			return;
		}
		byte[] buffer = null;
		try
		{
			for (int i = 0; i < parameters.Length; i++)
			{
				string text = preparedStatement.Statement.NormalizedParameterNames[i];
				int num2 = ((text == null) ? preparedStatement.Statement.ParameterIndexes[i] : (command.MySqlRawParameters?.UnsafeIndexOf(text) ?? (-1)));
				MySqlParameterCollection mySqlRawParameters = command.MySqlRawParameters;
				if (mySqlRawParameters == null || num2 < 0 || num2 >= mySqlRawParameters.Count)
				{
					continue;
				}
				MySqlParameter mySqlParameter = mySqlRawParameters[num2];
				if (mySqlParameter == null)
				{
					continue;
				}
				object value = mySqlParameter.Value;
				if (!(value is Stream stream) || stream is MemoryStream)
				{
					continue;
				}
				while (true)
				{
					if (buffer == null)
					{
						buffer = ArrayPool<byte>.Shared.Rent(16000007);
					}
					buffer[0] = 24;
					BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(1), preparedStatement.StatementId);
					BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(5), (ushort)i);
					int totalBytesRead = 0;
					int num3;
					do
					{
						int count = 16000000 - totalBytesRead;
						num3 = ((ioBehavior != IOBehavior.Synchronous) ? (await stream.ReadAsync(buffer, 7 + totalBytesRead, count, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) : stream.Read(buffer, 7 + totalBytesRead, count));
						totalBytesRead += num3;
					}
					while (num3 > 0 && totalBytesRead < 16000000);
					if (totalBytesRead == 0)
					{
						break;
					}
					using PayloadData payload = new PayloadData(buffer.AsMemory(0, 7 + totalBytesRead));
					await command.Session.SendAsync(payload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
		}
		finally
		{
			if (buffer != null)
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}
		}
	}

	public bool WriteQueryCommand(ref CommandListPosition commandListPosition, IDictionary<string, CachedProcedure?> cachedProcedures, ByteBufferWriter writer, bool appendSemicolon)
	{
		if (commandListPosition.CommandIndex == commandListPosition.CommandCount)
		{
			return false;
		}
		CommandFacade command = commandListPosition.CommandAt(commandListPosition.CommandIndex);
		commandListPosition.PreparedStatements = command.TryGetPreparedStatements();
		if (commandListPosition.PreparedStatements == null)
		{
			Log.PreparingCommandPayload(command.Logger, command.SessionId, command.CommandText);
			writer.Write((byte)3);
			bool num = command.Session?.SupportsQueryAttributes ?? false;
			bool flag = command.Session?.UseOracleMode ?? false;
			if (num)
			{
				MySqlAttributeCollection rawAttributes = command.RawAttributes;
				writer.WriteLengthEncodedInteger((uint)(rawAttributes?.Count ?? 0));
				writer.Write((byte)1);
				if (rawAttributes != null && rawAttributes.Count > 0)
				{
					if (!flag)
					{
						WriteBinaryParameters(writer, rawAttributes.Select((MySqlAttribute x) => x.ToParameter()).ToArray(), command, supportsQueryAttributes: true, 0);
					}
					else
					{
						WriteBinaryParameter(writer, rawAttributes.Select((MySqlAttribute x) => x.ToOracleParameter()).ToArray(), command, supportsQueryAttributes: true, 0);
					}
				}
			}
			else
			{
				MySqlAttributeCollection? rawAttributes2 = command.RawAttributes;
				if (rawAttributes2 != null && rawAttributes2.Count > 0)
				{
					Log.QueryAttributesNotSupported(command.Logger, command.SessionId, command.CommandText);
				}
			}
			WriteQueryPayload(command, cachedProcedures, writer, appendSemicolon, isFirstCommand: true, isLastCommand: true);
			commandListPosition.LastUsedPreparedStatement = null;
			commandListPosition.ExpectOraclePrepareExecuteResponse = false;
			commandListPosition.CommandIndex++;
		}
		else
		{
			PreparedStatement preparedStatement = commandListPosition.PreparedStatements.Statements[commandListPosition.PreparedStatementIndex];
			bool flag2 = command.Session?.UseOraclePrepareExecute ?? false;
			writer.Write((byte)(flag2 ? 161u : 23u));
			commandListPosition.ExpectOraclePrepareExecuteResponse = flag2;
			commandListPosition.LastUsedPreparedStatement = preparedStatement;
			if (flag2)
			{
				WritePreparedStatementPrepareExecute(command, preparedStatement, writer);
			}
			else
			{
				WritePreparedStatement(command, preparedStatement, writer);
			}
			if (++commandListPosition.PreparedStatementIndex == commandListPosition.PreparedStatements.Statements.Count)
			{
				commandListPosition.CommandIndex++;
				commandListPosition.PreparedStatementIndex = 0;
				commandListPosition.PreparedStatements = null;
			}
		}
		return true;
	}

	/// <summary>
	/// Writes the text of <paramref name="command" /> to <paramref name="writer" />, encoded in UTF-8.
	/// </summary>
	/// <param name="command">The command.</param>
	/// <param name="cachedProcedures">The cached procedures.</param>
	/// <param name="writer">The output writer.</param>
	/// <param name="appendSemicolon">Whether a statement-separating semicolon should be appended if it's missing.</param>
	/// <param name="isFirstCommand">Whether this command is the first one.</param>
	/// <param name="isLastCommand">Whether this command is the last one.</param>
	/// <returns><c>true</c> if a complete command was written; otherwise, <c>false</c>.</returns>
	public static bool WriteQueryPayload(CommandFacade command, IDictionary<string, CachedProcedure?> cachedProcedures, ByteBufferWriter writer, bool appendSemicolon, bool isFirstCommand, bool isLastCommand)
	{
		if (command.CommandType != CommandType.StoredProcedure)
		{
			return WriteCommand(command, writer, appendSemicolon, isFirstCommand, isLastCommand);
		}
		return WriteStoredProcedure(command, cachedProcedures, writer);
	}

	private static void WritePreparedStatement(CommandFacade command, PreparedStatement preparedStatement, ByteBufferWriter writer)
	{
		Log.PreparingCommandPayloadWithId(command.Logger, command.SessionId, preparedStatement.StatementId, command.CommandText);
		MySqlAttributeCollection rawAttributes = command.RawAttributes;
		bool supportsQueryAttributes = command.Session?.SupportsQueryAttributes ?? false;
		writer.Write(preparedStatement.StatementId);
		bool flag = command.Session?.UseOracleMode ?? false;
		int num = preparedStatement.Statement.ParameterNames?.Count ?? 0;
		if (flag && preparedStatement.Parameters != null && preparedStatement.Parameters.Length < num)
		{
			num = preparedStatement.Parameters.Length;
		}
		int num2 = rawAttributes?.Count ?? 0;
		if (flag)
		{
			if (command.CommandType == CommandType.StoredProcedure)
			{
				SetupStoredProcedureOutputParametersOracle(command);
			}
			else if (command.CommandType == CommandType.Text)
			{
				SetupTextCommandOutputParametersOracle(command);
			}
		}
		if (num2 > 0)
		{
			Log.QueryAttributesNotSupportedWithId(command.Logger, command.SessionId, preparedStatement.StatementId);
			num2 = 0;
		}
		writer.Write((byte)0);
		writer.Write(1);
		if (num <= 0 && num2 <= 0)
		{
			return;
		}
		if (!flag)
		{
			MySqlParameter[] array = new MySqlParameter[num + num2];
			MySqlParameterCollection mySqlRawParameters = command.MySqlRawParameters;
			for (int i = 0; i < num; i++)
			{
				string text = preparedStatement.Statement.NormalizedParameterNames[i];
				int num3 = ((text == null) ? preparedStatement.Statement.ParameterIndexes[i] : (mySqlRawParameters?.UnsafeIndexOf(text) ?? (-1)));
				if (num3 == -1 && text != null)
				{
					throw new MySqlException("Parameter '" + preparedStatement.Statement.ParameterNames[i] + "' must be defined.");
				}
				if (num3 < 0 || num3 >= (mySqlRawParameters?.Count ?? 0))
				{
					throw new MySqlException(string.Format("Parameter index {0} is invalid when only {1} parameter{2} defined.", num3, mySqlRawParameters?.Count ?? 0, (mySqlRawParameters != null && mySqlRawParameters.Count == 1) ? " is" : "s are"));
				}
				array[i] = mySqlRawParameters[num3];
			}
			for (int j = 0; j < num2; j++)
			{
				array[num + j] = rawAttributes[j].ToParameter();
			}
			WriteBinaryParameters(writer, array, command, supportsQueryAttributes, num);
			return;
		}
		OracleParameter[] array2 = new OracleParameter[num + num2];
		OracleParameterCollection oracleRawParameters = command.OracleRawParameters;
		for (int k = 0; k < num; k++)
		{
			string text2 = preparedStatement.Statement.NormalizedParameterNames[k];
			int num4 = ((text2 == null) ? preparedStatement.Statement.ParameterIndexes[k] : (oracleRawParameters?.UnsafeIndexOf(text2) ?? (-1)));
			if (num4 == -1 && text2 != null)
			{
				throw new MySqlException("Parameter '" + preparedStatement.Statement.ParameterNames[k] + "' must be defined.");
			}
			if (num4 < 0 || num4 >= (oracleRawParameters?.Count ?? 0))
			{
				throw new MySqlException(string.Format("Parameter index {0} is invalid when only {1} parameter{2} defined.", num4, oracleRawParameters?.Count ?? 0, (oracleRawParameters != null && oracleRawParameters.Count == 1) ? " is" : "s are"));
			}
			array2[k] = oracleRawParameters[num4];
		}
		for (int l = 0; l < num2; l++)
		{
			array2[num + l] = rawAttributes[l].ToOracleParameter();
		}
		WriteBinaryParameter(writer, array2, command, supportsQueryAttributes, num);
	}

	private static void WritePreparedStatementPrepareExecute(CommandFacade command, PreparedStatement preparedStatement, ByteBufferWriter writer)
	{
		Log.PreparingCommandPayloadWithId(command.Logger, command.SessionId, preparedStatement.StatementId, command.CommandText);
		ServerSession? session = command.Session;
		if (session == null || !session.UseOracleMode)
		{
			throw new InvalidOperationException("COM_STMT_PREPARE_EXECUTE is only supported in Oracle mode.");
		}
		MySqlAttributeCollection rawAttributes = command.RawAttributes;
		int num = rawAttributes?.Count ?? 0;
		if (num > 0)
		{
			Log.QueryAttributesNotSupportedWithId(command.Logger, command.SessionId, preparedStatement.StatementId);
			num = 0;
		}
		if (command.CommandType == CommandType.StoredProcedure)
		{
			SetupStoredProcedureOutputParametersOracle(command);
		}
		else if (command.CommandType == CommandType.Text)
		{
			SetupTextCommandOutputParametersOracle(command);
		}
		int num2 = preparedStatement.Statement.ParameterNames?.Count ?? 0;
		int oracleArrayBindCount = GetOracleArrayBindCount(command);
		bool? bIsDefineInSelect = null;
		bool? bIsDmlReturn = null;
		bool flag = oracleArrayBindCount <= 0 && command.CommandType == CommandType.Text && ResultSet.GetSqlStatementType(command.CommandText, ref bIsDefineInSelect, ref bIsDmlReturn) == SqlStatementType.SELECT;
		writer.Write(preparedStatement.StatementId);
		writer.Write(flag ? ((byte)1) : ((byte)0));
		writer.Write((oracleArrayBindCount > 0) ? oracleArrayBindCount : ((!flag) ? 1 : (command.Session?.PrepareExecuteFetchSize ?? 10)));
		ArraySegment<byte> statementBytes = preparedStatement.Statement.StatementBytes;
		if (statementBytes.Array == null || statementBytes.Count <= 1)
		{
			throw new InvalidOperationException("Prepared statement bytes are missing.");
		}
		Span<byte> span = statementBytes.AsSpan(1);
		writer.WriteLengthEncodedInteger((ulong)span.Length);
		writer.Write(span);
		writer.Write(num2);
		if (oracleArrayBindCount > 0 && num2 == 0)
		{
			throw new InvalidOperationException("ArrayBinding requires at least one input parameter.");
		}
		if (num2 > 0 || num > 0)
		{
			OracleParameter[] array = new OracleParameter[num2 + num];
			OracleParameterCollection oracleRawParameters = command.OracleRawParameters;
			for (int i = 0; i < num2; i++)
			{
				string text = preparedStatement.Statement.NormalizedParameterNames[i];
				int num3 = ((text == null) ? preparedStatement.Statement.ParameterIndexes[i] : (oracleRawParameters?.UnsafeIndexOf(text) ?? (-1)));
				if (num3 == -1 && text != null)
				{
					throw new MySqlException("Parameter '" + preparedStatement.Statement.ParameterNames[i] + "' must be defined.");
				}
				if (num3 < 0 || num3 >= (oracleRawParameters?.Count ?? 0))
				{
					throw new MySqlException(string.Format("Parameter index {0} is invalid when only {1} parameter{2} defined.", num3, oracleRawParameters?.Count ?? 0, (oracleRawParameters != null && oracleRawParameters.Count == 1) ? " is" : "s are"));
				}
				array[i] = oracleRawParameters[num3];
			}
			for (int j = 0; j < num; j++)
			{
				array[num2 + j] = rawAttributes[j].ToOracleParameter();
			}
			WriteOraclePrepareExecuteParameters(writer, array, command, num2, oracleArrayBindCount);
		}
		writer.Write(0);
		writer.Write(0);
		writer.Write(OceanBaseCRC32.Calculate(span));
		writer.Write(0);
	}

	private static void SetupStoredProcedureOutputParametersOracle(CommandFacade command)
	{
		OracleParameterCollection oracleParameterCollection = command.RawParameters as OracleParameterCollection;
		OracleParameter returnParameter = null;
		OracleParameterCollection oracleParameterCollection2 = new OracleParameterCollection();
		for (int i = 0; i < (oracleParameterCollection?.Count ?? 0); i++)
		{
			OracleParameter oracleParameter = oracleParameterCollection[i];
			switch (oracleParameter.Direction)
			{
			case ParameterDirection.Output:
			case ParameterDirection.InputOutput:
				oracleParameterCollection2.Add(oracleParameter);
				break;
			case ParameterDirection.ReturnValue:
				returnParameter = oracleParameter;
				break;
			}
		}
		command.OutParameters = ((oracleParameterCollection2.Count == 0) ? null : oracleParameterCollection2);
		command.ReturnParameter = returnParameter;
	}

	private static void SetupTextCommandOutputParametersOracle(CommandFacade command)
	{
		if (command.RawParameters is OracleParameterCollection { Count: not 0 } oracleParameterCollection)
		{
			OracleParameterCollection oracleParameterCollection2 = null;
			for (int i = 0; i < oracleParameterCollection.Count; i++)
			{
				OracleParameter oracleParameter = oracleParameterCollection[i];
				ParameterDirection direction = oracleParameter.Direction;
				if ((uint)(direction - 2) <= 1u || direction == ParameterDirection.ReturnValue)
				{
					if (oracleParameterCollection2 == null)
					{
						oracleParameterCollection2 = new OracleParameterCollection();
					}
					oracleParameterCollection2.Add(oracleParameter);
				}
			}
			if (oracleParameterCollection2 != null)
			{
				command.OutParameters = oracleParameterCollection2;
				command.ReturnParameter = null;
			}
			else
			{
				command.OutParameters = null;
				command.ReturnParameter = null;
			}
		}
		else
		{
			command.OutParameters = null;
			command.ReturnParameter = null;
		}
	}

	private static void WriteBinaryParameters(ByteBufferWriter writer, MySqlParameter[] parameters, CommandFacade command, bool supportsQueryAttributes, int parameterCount)
	{
		byte b = 0;
		for (int i = 0; i < parameters.Length; i++)
		{
			MySqlParameter mySqlParameter = parameters[i];
			if (mySqlParameter.Value == null || mySqlParameter.Value == DBNull.Value)
			{
				b |= (byte)(1 << i % 8);
			}
			if (i % 8 == 7)
			{
				writer.Write(b);
				b = 0;
			}
		}
		if (parameters.Length % 8 != 0)
		{
			writer.Write(b);
		}
		writer.Write((byte)1);
		for (int j = 0; j < parameters.Length; j++)
		{
			MySqlParameter mySqlParameter2 = parameters[j];
			MySqlDbType mySqlDbType = mySqlParameter2.MySqlDbType;
			DbTypeMapping dbTypeMapping = ((mySqlParameter2.Value == null || mySqlParameter2.Value == DBNull.Value) ? null : TypeMapper.Instance.GetDbTypeMapping(mySqlParameter2.Value.GetType()));
			if (dbTypeMapping != null)
			{
				DbType dbType = dbTypeMapping.DbTypes[0];
				mySqlDbType = TypeMapper.Instance.GetMySqlDbTypeForDbType(dbType);
			}
			if (mySqlDbType == MySqlDbType.Vector)
			{
				ServerSession? session = command.Session;
				if (session != null && session.ServerVersion.IsMariaDb)
				{
					mySqlDbType = MySqlDbType.LongBlob;
				}
			}
			writer.Write(TypeMapper.ConvertToColumnTypeAndFlags(mySqlDbType, command.Connection?.GuidFormat ?? MySqlGuidFormat.Default));
			if (supportsQueryAttributes)
			{
				if (j < parameterCount)
				{
					writer.Write((byte)0);
				}
				else
				{
					writer.WriteLengthEncodedString(mySqlParameter2.ParameterName);
				}
			}
		}
		StatementPreparerOptions options = command.CreateStatementPreparerOptions();
		for (int k = 0; k < parameters.Length; k++)
		{
			parameters[k].AppendBinary(writer, options);
		}
	}

	private static void WriteBinaryParameter(ByteBufferWriter writer, OracleParameter[] parameters, CommandFacade command, bool supportsQueryAttributes, int parameterCount)
	{
		byte b = 0;
		for (int i = 0; i < parameters.Length; i++)
		{
			if (OracleUdt.IsNullParameterValue(parameters[i].Value))
			{
				b |= (byte)(1 << i % 8);
			}
			if (i % 8 == 7)
			{
				writer.Write(b);
				b = 0;
			}
		}
		if (parameters.Length % 8 != 0)
		{
			writer.Write(b);
		}
		writer.Write((byte)1);
		for (int j = 0; j < parameters.Length; j++)
		{
			OracleParameter oracleParameter = parameters[j];
			OracleDbType oracleDbType = oracleParameter.OracleDbType;
			DbTypeMapping dbTypeMapping = (OracleUdt.IsNullParameterValue(oracleParameter.Value) ? null : TypeMapper.Instance.GetDbTypeMapping(oracleParameter.Value.GetType()));
			if (dbTypeMapping != null && !oracleParameter.ShouldSerializeByOracleDbType())
			{
				oracleDbType = TypeMapper.GetOracleDbTypeForDbType(dbTypeMapping.DbTypes[0]);
			}
			writer.Write(GetOraclePrepareParameterColumnTypeAndFlags(command, oracleParameter, oracleDbType));
			bool flag = (uint)(oracleDbType - 128) <= 1u;
			if (flag && ShouldWriteUdtPrepareTypeInfoHeader(command, oracleParameter))
			{
				string? obj = oracleParameter.UdtTypeName ?? string.Empty;
				if (string.IsNullOrEmpty(obj))
				{
					throw new ArgumentException("OBA-50028: Invalid parameter binding");
				}
				var (schemaName, typeName) = ParseUdtTypeName(obj);
				WriteUdtSchemaTypeVersionHeader(writer, schemaName, typeName, 0uL);
			}
			if (supportsQueryAttributes)
			{
				if (j < parameterCount)
				{
					writer.Write((byte)0);
				}
				else
				{
					writer.WriteLengthEncodedString(oracleParameter.ParameterName);
				}
			}
		}
		StatementPreparerOptions options = command.CreateStatementPreparerOptions();
		for (int k = 0; k < parameters.Length; k++)
		{
			parameters[k].AppendBinary(writer, options, command.Session);
		}
	}

	/// <summary>
	/// JDBC <c>CallableStatement.validAllParameters</c>: non-IN OUT binds are replaced with
	/// <c>NullParameter</c> (wire <see cref="F:OceanBase.Protocol.ColumnType.Null" />), not <c>COMPLEX</c> + UDT metadata.
	/// </summary>
	private static bool ShouldUseRoutineOutNullParameterType(CommandFacade command, OracleParameter parameter)
	{
		bool flag = command.IsOracleCommand && command.CommandType == CommandType.StoredProcedure;
		if (flag)
		{
			ParameterDirection direction = parameter.Direction;
			bool flag2 = ((direction == ParameterDirection.Output || direction == ParameterDirection.ReturnValue) ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			return OracleUdt.IsNullParameterValue(parameter.Value);
		}
		return false;
	}

	private static bool ShouldWriteUdtPrepareTypeInfoHeader(CommandFacade command, OracleParameter parameter)
	{
		return !ShouldUseRoutineOutNullParameterType(command, parameter);
	}

	private static ushort GetOraclePrepareParameterColumnTypeAndFlags(CommandFacade command, OracleParameter parameter, OracleDbType oracleDbType)
	{
		if (ShouldUseRoutineOutNullParameterType(command, parameter))
		{
			return 6;
		}
		return TypeMapper.ConvertToOracleColumnTypeAndFlags(oracleDbType);
	}

	private static void WriteOraclePrepareExecuteParameters(ByteBufferWriter writer, OracleParameter[] parameters, CommandFacade command, int parameterCount, int arrayBindCount)
	{
		if (arrayBindCount <= 0)
		{
			WriteBinaryParameter(writer, parameters, command, supportsQueryAttributes: false, parameterCount);
			return;
		}
		Array[] array = new Array[parameters.Length];
		OracleDbType[] array2 = new OracleDbType[parameters.Length];
		ValidateOracleArrayBindingParameters(parameters, arrayBindCount, array, array2);
		if (arrayBindCount == 1)
		{
			OracleParameter[] array3 = new OracleParameter[parameters.Length];
			for (int i = 0; i < parameters.Length; i++)
			{
				OracleParameter oracleParameter = parameters[i].Clone();
				object value = array[i].GetValue(0);
				oracleParameter.Value = (IsArrayBindingCellNull(value) ? DBNull.Value : value);
				array3[i] = oracleParameter;
			}
			WriteBinaryParameter(writer, array3, command, supportsQueryAttributes: false, parameterCount);
			return;
		}
		for (int j = 0; j < (parameters.Length + 7) / 8; j++)
		{
			writer.Write((byte)0);
		}
		writer.Write((byte)1);
		for (int k = 0; k < parameters.Length; k++)
		{
			OracleParameter oracleParameter2 = parameters[k];
			OracleDbType oracleDbType = array2[k];
			writer.Write((ushort)160);
			switch (oracleDbType)
			{
			case OracleDbType.Array:
				WriteArrayTypeInfoHeader(writer, oracleParameter2, 0uL);
				break;
			case OracleDbType.Object:
			{
				writer.WriteLengthEncodedInteger(0uL);
				writer.WriteLengthEncodedInteger(0uL);
				writer.WriteLengthEncodedInteger(0uL);
				writer.Write((byte)160);
				string udtTypeName = oracleParameter2.UdtTypeName;
				if (string.IsNullOrEmpty(udtTypeName) || udtTypeName == null)
				{
					throw new ArgumentException("OBA-50028: Invalid parameter binding");
				}
				WriteArrayBindingUdtElementTypeHeader(writer, udtTypeName);
				break;
			}
			default:
				writer.WriteLengthEncodedInteger(0uL);
				writer.WriteLengthEncodedInteger(0uL);
				writer.WriteLengthEncodedInteger(0uL);
				writer.Write((byte)TypeMapper.ConvertToOracleColumnTypeAndFlags(oracleDbType));
				break;
			}
		}
		StatementPreparerOptions options = command.CreateStatementPreparerOptions();
		for (int l = 0; l < parameters.Length; l++)
		{
			OracleParameter oracleParameter3 = parameters[l];
			Array array4 = array[l];
			byte[] array5 = new byte[(arrayBindCount + 7) / 8];
			for (int m = 0; m < arrayBindCount; m++)
			{
				if (IsArrayBindingCellNull(array4.GetValue(m)))
				{
					array5[m / 8] |= (byte)(1 << m % 8);
				}
			}
			writer.WriteLengthEncodedInteger((ulong)arrayBindCount);
			writer.Write(array5);
			for (int n = 0; n < arrayBindCount; n++)
			{
				object value2 = array4.GetValue(n);
				if (!IsArrayBindingCellNull(value2))
				{
					oracleParameter3.AppendBinaryValue(writer, value2, options, command.Session);
				}
			}
		}
	}

	private static int GetOracleArrayBindCount(CommandFacade command)
	{
		int oracleArrayBindCount = command.OracleArrayBindCount;
		if (oracleArrayBindCount <= 0)
		{
			return 0;
		}
		OracleParameterCollection oracleRawParameters = command.OracleRawParameters;
		if (oracleRawParameters == null || oracleRawParameters.Count == 0)
		{
			return 0;
		}
		ServerSession session = command.Session;
		if (session == null)
		{
			return 0;
		}
		if (!session.UseArrayBinding)
		{
			throw new InvalidOperationException("ArrayBinding is disabled. Set UseArrayBinding=true in the connection string.");
		}
		if (!command.IsOracleCommand || !session.UseOracleMode)
		{
			throw new InvalidOperationException("ArrayBinding is only supported for OracleCommand in Oracle mode.");
		}
		if (!session.UseOraclePrepareExecute)
		{
			throw new InvalidOperationException("ArrayBinding requires UseOraclePrepareExecute=true and an OceanBase server version that supports COM_STMT_PREPARE_EXECUTE.");
		}
		if (command.CommandType == CommandType.StoredProcedure)
		{
			throw new NotSupportedException("ArrayBinding for stored procedures is not supported.");
		}
		if (session.OracleAutoCommit)
		{
			throw new InvalidOperationException("ArrayBinding requires AutoCommit=false (session autocommit must be disabled).");
		}
		return oracleArrayBindCount;
	}

	private static void ValidateOracleArrayBindingParameters(OracleParameter[] parameters, int arrayBindCount, Array[] arrayValues, OracleDbType[] elementTypes)
	{
		if (parameters.Length == 0)
		{
			throw new InvalidOperationException("ArrayBinding requires at least one input parameter.");
		}
		for (int i = 0; i < parameters.Length; i++)
		{
			OracleParameter oracleParameter = parameters[i];
			if (oracleParameter.Direction != ParameterDirection.Input)
			{
				throw new NotSupportedException($"ArrayBinding only supports input parameters. Parameter '{oracleParameter.ParameterName}' has direction {oracleParameter.Direction}.");
			}
			Array array = oracleParameter.Value as Array;
			bool flag = array == null;
			if (!flag)
			{
				object value = oracleParameter.Value;
				bool flag2 = ((value is byte[] || value is char[]) ? true : false);
				flag = flag2;
			}
			if (flag)
			{
				throw new InvalidOperationException("ArrayBinding requires parameter '" + oracleParameter.ParameterName + "' Value to be a one-dimensional non-binary array; use byte[][] for RAW values.");
			}
			if (array.Rank != 1)
			{
				throw new InvalidOperationException("ArrayBinding requires parameter '" + oracleParameter.ParameterName + "' Value to be a one-dimensional array.");
			}
			if (array.Length != arrayBindCount)
			{
				throw new InvalidOperationException($"ArrayBinding parameter '{oracleParameter.ParameterName}' length ({array.Length}) must equal ArrayBindCount ({arrayBindCount}).");
			}
			int[] arrayBindSize = oracleParameter.ArrayBindSize;
			if (arrayBindSize != null && arrayBindSize.Length != arrayBindCount)
			{
				throw new InvalidOperationException($"ArrayBindSize length for parameter '{oracleParameter.ParameterName}' must equal ArrayBindCount ({arrayBindCount}).");
			}
			ValidateArrayBindingElementTypes(oracleParameter, array);
			arrayValues[i] = array;
			elementTypes[i] = GetArrayBindingElementOracleDbType(oracleParameter, array);
		}
	}

	private static void ValidateArrayBindingElementTypes(OracleParameter parameter, Array values)
	{
		Type type = null;
		for (int i = 0; i < values.Length; i++)
		{
			object value = values.GetValue(i);
			if (!IsArrayBindingCellNull(value))
			{
				Type type2 = value.GetType();
				if ((object)type == null)
				{
					type = type2;
				}
				if (type2 != type)
				{
					throw new InvalidOperationException("ArrayBinding parameter '" + parameter.ParameterName + "' contains mixed element types: " + type.Name + " and " + type2.Name + ".");
				}
				if (parameter.HasSetDbType)
				{
					ValidateArrayBindingElementConversion(parameter, value, i);
				}
			}
		}
	}

	private static void ValidateArrayBindingElementConversion(OracleParameter parameter, object value, int index)
	{
		try
		{
			switch (parameter.OracleDbType)
			{
			case OracleDbType.Byte:
				Convert.ToByte(value, CultureInfo.InvariantCulture);
				break;
			case OracleDbType.Int16:
				Convert.ToInt16(value, CultureInfo.InvariantCulture);
				break;
			case OracleDbType.Int32:
				Convert.ToInt32(value, CultureInfo.InvariantCulture);
				break;
			case OracleDbType.Int64:
				Convert.ToInt64(value, CultureInfo.InvariantCulture);
				break;
			case OracleDbType.Single:
			case OracleDbType.BinaryFloat:
				Convert.ToSingle(value, CultureInfo.InvariantCulture);
				break;
			case OracleDbType.Double:
			case OracleDbType.BinaryDouble:
				Convert.ToDouble(value, CultureInfo.InvariantCulture);
				break;
			case OracleDbType.Decimal:
				Convert.ToDecimal(value, CultureInfo.InvariantCulture);
				break;
			case OracleDbType.Boolean:
				Convert.ToBoolean(value, CultureInfo.InvariantCulture);
				break;
			}
		}
		catch (Exception ex) when (((ex is InvalidCastException || ex is FormatException || ex is OverflowException) ? 1 : 0) != 0)
		{
			throw new InvalidOperationException($"ArrayBinding parameter '{parameter.ParameterName}' element at index {index} " + $"cannot be converted to {parameter.OracleDbType}.", ex);
		}
	}

	internal static void ValidateOracleArrayBindingParameters(CommandFacade command)
	{
		int oracleArrayBindCount = GetOracleArrayBindCount(command);
		if (oracleArrayBindCount > 0)
		{
			OracleParameterCollection oracleRawParameters = command.OracleRawParameters;
			if (oracleRawParameters == null)
			{
				throw new InvalidOperationException("ArrayBinding requires at least one input parameter.");
			}
			OracleParameter[] array = new OracleParameter[oracleRawParameters.Count];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = oracleRawParameters[i];
			}
			ValidateOracleArrayBindingParameters(array, oracleArrayBindCount, new Array[array.Length], new OracleDbType[array.Length]);
		}
	}

	private static OracleDbType GetArrayBindingElementOracleDbType(OracleParameter parameter, Array values)
	{
		if (parameter.HasSetDbType)
		{
			return parameter.OracleDbType;
		}
		for (int i = 0; i < values.Length; i++)
		{
			object value = values.GetValue(i);
			if (!IsArrayBindingCellNull(value))
			{
				if ((value is TimeSpan || value is OracleIntervalDS) ? true : false)
				{
					return OracleDbType.IntervalDS;
				}
				if (value is OracleIntervalYM)
				{
					return OracleDbType.IntervalYM;
				}
				DbTypeMapping dbTypeMapping = TypeMapper.Instance.GetDbTypeMapping(value.GetType());
				if (dbTypeMapping == null)
				{
					break;
				}
				return TypeMapper.GetOracleDbTypeForDbType(dbTypeMapping.DbTypes[0]);
			}
		}
		return parameter.OracleDbType;
	}

	private static bool IsArrayBindingCellNull(object? value)
	{
		return OracleUdt.IsNullParameterValue(value);
	}

	private static bool WriteStoredProcedure(CommandFacade command, IDictionary<string, CachedProcedure?> cachedProcedures, ByteBufferWriter writer)
	{
		if (command.IsMySqlCommand)
		{
			return WriteStoredProcedureMySql(command, cachedProcedures, writer);
		}
		return WriteStoredProcedureOracle(command, cachedProcedures, writer);
	}

	private static bool WriteStoredProcedureMySql(CommandFacade command, IDictionary<string, CachedProcedure?> cachedProcedures, ByteBufferWriter writer)
	{
		MySqlParameterCollection mySqlParameterCollection = command.MySqlRawParameters;
		CachedProcedure cachedProcedure = cachedProcedures[command.CommandText];
		if (cachedProcedure != null)
		{
			mySqlParameterCollection = cachedProcedure.AlignParamsWithDb(mySqlParameterCollection);
		}
		MySqlParameter mySqlParameter = null;
		MySqlParameterCollection mySqlParameterCollection2 = new MySqlParameterCollection();
		List<string> list = new List<string>();
		MySqlParameterCollection mySqlParameterCollection3 = new MySqlParameterCollection();
		List<string> list2 = new List<string>();
		string text = "";
		for (int i = 0; i < (mySqlParameterCollection?.Count ?? 0); i++)
		{
			MySqlParameter mySqlParameter2 = mySqlParameterCollection[i];
			string text2 = "@inParam" + i;
			string text3 = "@outParam" + i;
			switch (mySqlParameter2.Direction)
			{
			case ParameterDirection.Input:
			case ParameterDirection.InputOutput:
			{
				MySqlParameter parameter = mySqlParameter2.WithParameterName(text2);
				mySqlParameterCollection3.Add(parameter);
				if (mySqlParameter2.Direction == ParameterDirection.InputOutput)
				{
					text = text + "SET " + text3 + "=" + text2 + "; ";
					goto case ParameterDirection.Output;
				}
				list2.Add(text2);
				break;
			}
			case ParameterDirection.Output:
			{
				mySqlParameterCollection2.Add(mySqlParameter2);
				list2.Add(text3);
				MySqlDbType mySqlDbType = mySqlParameter2.MySqlDbType;
				int size = mySqlParameter2.Size;
				if (mySqlDbType == MySqlDbType.Guid)
				{
					switch (size)
					{
					case 16:
						list.Add("CAST(" + text3 + " AS BINARY(16))");
						goto end_IL_008a;
					case 32:
						list.Add("CAST(" + text3 + " AS CHAR(32))");
						goto end_IL_008a;
					case 36:
						list.Add("CAST(" + text3 + " AS CHAR(36))");
						goto end_IL_008a;
					}
				}
				list.Add(text3);
				break;
			}
			case ParameterDirection.ReturnValue:
				{
					mySqlParameter = mySqlParameter2;
					break;
				}
				end_IL_008a:
				break;
			}
		}
		string text4 = command.CommandText + "(" + string.Join(", ", list2) + ");";
		if (mySqlParameter == null)
		{
			text4 = text + "CALL " + text4;
			if (mySqlParameterCollection2.Count > 0 && (command.CommandBehavior & CommandBehavior.SchemaOnly) == 0)
			{
				text4 = text4 + "SELECT '" + OutParameterSentinelColumnName + "' AS '" + OutParameterSentinelColumnName + "', " + string.Join(", ", list);
			}
		}
		else
		{
			text4 = "SELECT " + text4;
		}
		command.OutParameters = mySqlParameterCollection2;
		command.ReturnParameter = mySqlParameter;
		string commandText = text4;
		StatementPreparerOptions options = command.CreateStatementPreparerOptions();
		ServerSession session = command.Session;
		return new StatementPreparer(commandText, mySqlParameterCollection3, options, null, session).ParseAndBindParameters(writer);
	}

	private static bool WriteStoredProcedureOracle(CommandFacade command, IDictionary<string, CachedProcedure?> cachedProcedures, ByteBufferWriter writer)
	{
		OracleParameterCollection oracleParameterCollection = command.RawParameters as OracleParameterCollection;
		if (cachedProcedures != null)
		{
			string commandText = command.CommandText;
			if (commandText != null && commandText.Length > 0 && cachedProcedures.TryGetValue(commandText, out CachedProcedure value) && value != null)
			{
				oracleParameterCollection = value.AlignOracleStoredProcedureParameters(oracleParameterCollection);
			}
		}
		OracleParameter oracleParameter = null;
		OracleParameterCollection oracleParameterCollection2 = new OracleParameterCollection();
		List<string> list = new List<string>();
		OracleParameterCollection oracleParameterCollection3 = new OracleParameterCollection();
		List<string> list2 = new List<string>();
		string text = "";
		for (int i = 0; i < (oracleParameterCollection?.Count ?? 0); i++)
		{
			OracleParameter oracleParameter2 = oracleParameterCollection[i];
			string text2 = "@inParam" + i;
			string text3 = "@outParam" + i;
			switch (oracleParameter2.Direction)
			{
			case ParameterDirection.Input:
			case ParameterDirection.InputOutput:
			{
				OracleParameter parameter = oracleParameter2.WithParameterName(text2);
				oracleParameterCollection3.Add(parameter);
				if (oracleParameter2.Direction == ParameterDirection.InputOutput)
				{
					text = text + "SET " + text3 + "=" + text2 + "; ";
					goto case ParameterDirection.Output;
				}
				list2.Add(text2);
				break;
			}
			case ParameterDirection.Output:
				oracleParameterCollection2.Add(oracleParameter2);
				list2.Add(text3);
				list.Add(text3);
				break;
			case ParameterDirection.ReturnValue:
				oracleParameter = oracleParameter2;
				break;
			}
		}
		string text4 = command.CommandText + "(" + string.Join(", ", list2) + ");";
		if (oracleParameter == null)
		{
			text4 = text + "CALL " + text4;
			if (oracleParameterCollection2.Count > 0 && (command.CommandBehavior & CommandBehavior.SchemaOnly) == 0)
			{
				text4 = text4 + "SELECT '" + OutParameterSentinelColumnName + "' AS '" + OutParameterSentinelColumnName + "', " + string.Join(", ", list);
			}
		}
		else
		{
			text4 = "SELECT " + text4;
		}
		command.OutParameters = oracleParameterCollection2;
		command.ReturnParameter = oracleParameter;
		string commandText2 = text4;
		StatementPreparerOptions options = command.CreateStatementPreparerOptions();
		ServerSession session = command.Session;
		return new StatementPreparer(commandText2, oracleParameterCollection3, options, null, session).ParseAndBindParameters(writer);
	}

	private static bool WriteCommand(CommandFacade command, ByteBufferWriter writer, bool appendSemicolon, bool isFirstCommand, bool isLastCommand)
	{
		if (command.IsOracleCommand && command.OracleArrayBindCount > 0)
		{
			OracleParameterCollection oracleRawParameters = command.OracleRawParameters;
			if (oracleRawParameters != null && oracleRawParameters.Count > 0 && oracleRawParameters.Cast<OracleParameter>().Any((OracleParameter p) => p.Value is Array array && !(array is byte[]) && !(array is char[])))
			{
				throw new InvalidOperationException("Oracle ArrayBinding requires Oracle mode + UseOraclePrepareExecute=true + UseArrayBinding=true. Current command is using text protocol, which cannot serialize array parameter values.");
			}
		}
		bool flag = (command.CommandBehavior & CommandBehavior.SchemaOnly) != 0;
		bool flag2 = (command.CommandBehavior & CommandBehavior.SingleRow) != 0;
		if ((flag || flag2) && isFirstCommand)
		{
			ConnectionFacade? connection = command.Connection;
			ReadOnlySpan<byte> span = ((connection.HasValue && connection.GetValueOrDefault().SupportsPerQueryVariables) ? (flag2 ? "SET STATEMENT sql_select_limit=1 FOR "u8 : "SET STATEMENT sql_select_limit=0 FOR "u8) : (flag2 ? "SET sql_select_limit=1;\n"u8 : "SET sql_select_limit=0;\n"u8));
			writer.Write(span);
		}
		string? commandText = command.CommandText;
		DbParameterCollection? rawParameters = command.RawParameters;
		int options = (int)command.CreateStatementPreparerOptions() | ((appendSemicolon || flag || flag2) ? 512 : 0);
		ServerSession session = command.Session;
		bool flag3 = new StatementPreparer(commandText, rawParameters, (StatementPreparerOptions)options, null, session).ParseAndBindParameters(writer);
		if ((flag || flag2) && isLastCommand && flag3)
		{
			ConnectionFacade? connection = command.Connection;
			if (!connection.HasValue || !connection.GetValueOrDefault().SupportsPerQueryVariables)
			{
				writer.Write("\nSET sql_select_limit=default;"u8);
			}
		}
		return flag3;
	}

	/// <summary>
	/// Returns the elem_type byte for an ArrayBinding parameter slot.
	/// </summary>
	private static byte GetArrayBindingElementTypeByte(OracleDbType elementType, OracleParameter parameter)
	{
		if (parameter.HasSetDbType)
		{
			return (byte)TypeMapper.ConvertToOracleColumnTypeAndFlags(parameter.OracleDbType);
		}
		return GetArrayElementTypeByte(elementType);
	}

	/// <summary>
	/// Writes UDT element metadata after <c>elem_type</c> in ArrayBinding type headers (6.md).
	/// <paramref name="udtTypeName" /> may be unqualified (e.g. <c>UDT2_BASIC_O</c>): schema is sent as empty <c>lenenc_str</c>.
	/// </summary>
	private static void WriteArrayBindingUdtElementTypeHeader(ByteBufferWriter writer, string udtTypeName)
	{
		var (schemaName, typeName) = ParseUdtTypeName(udtTypeName);
		WriteUdtSchemaTypeVersionHeader(writer, schemaName, typeName, 0uL);
	}

	/// <summary>
	/// Splits <c>OWNER.TYPE_NAME</c> into schema and type; unqualified names use an empty schema.
	/// </summary>
	private static (string SchemaName, string TypeName) ParseUdtTypeName(string udtTypeName)
	{
		string item = string.Empty;
		string item2 = udtTypeName;
		int num = udtTypeName.IndexOf('.');
		if (num >= 0)
		{
			item = udtTypeName.Substring(0, num);
			item2 = udtTypeName.Substring(num + 1);
		}
		return (SchemaName: item, TypeName: item2);
	}

	private static void WriteUdtSchemaTypeVersionHeader(ByteBufferWriter writer, string schemaName, string typeName, ulong version = 0uL)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(schemaName);
		writer.WriteLengthEncodedInteger((ulong)bytes.Length);
		writer.Write(bytes);
		byte[] bytes2 = Encoding.UTF8.GetBytes(typeName);
		writer.WriteLengthEncodedInteger((ulong)bytes2.Length);
		writer.Write(bytes2);
		writer.WriteLengthEncodedInteger(version);
	}

	/// <summary>
	/// Mirrors JDBC 2.4.19 <c>OBArrayParameter#storeArrayTypeInfo</c>: always writes
	/// schema + typeName + version; appends <c>elem_type</c> (and nested UDT trailer) only when
	/// the parsed type name is empty (anonymous array per 6.md). Scalar Prepare requires
	/// <see cref="P:OceanBase.OracleParameter.UdtTypeName" /> and uses <see cref="M:OceanBase.Core.SingleCommandPayloadCreator.WriteUdtSchemaTypeVersionHeader(OceanBase.Protocol.Serialization.ByteBufferWriter,System.String,System.String,System.UInt64)" /> directly instead.
	/// </summary>
	private static void WriteArrayTypeInfoHeader(ByteBufferWriter writer, OracleParameter parameter, ulong version = 0uL)
	{
		var (schemaName, text) = ParseUdtTypeName(parameter.UdtTypeName ?? string.Empty);
		WriteUdtSchemaTypeVersionHeader(writer, schemaName, text, version);
		if (!string.IsNullOrEmpty(text))
		{
			return;
		}
		byte arrayElementTypeByte = GetArrayElementTypeByte(InferArrayElementType(parameter));
		writer.Write(arrayElementTypeByte);
		if (arrayElementTypeByte >= 160 && arrayElementTypeByte <= 162)
		{
			string text2 = TryInferArrayElementUdtTypeName(parameter);
			if (text2 != null)
			{
				var (schemaName2, typeName) = ParseUdtTypeName(text2);
				WriteUdtSchemaTypeVersionHeader(writer, schemaName2, typeName, 0uL);
			}
		}
	}

	private static string? TryInferArrayElementUdtTypeName(OracleParameter parameter)
	{
		if (parameter.Value is IEnumerable enumerable && !(enumerable is string))
		{
			foreach (object? item in enumerable)
			{
				if (!OracleUdt.IsNullParameterValue(item))
				{
					if (item is OracleUdtValue oracleUdtValue)
					{
						return oracleUdtValue.TypeName;
					}
					if (item is IOracleCustomType)
					{
						OracleCustomTypeMappingAttribute customAttribute = item.GetType().GetCustomAttribute<OracleCustomTypeMappingAttribute>();
						if (customAttribute != null && !string.IsNullOrWhiteSpace(customAttribute.UdtTypeName))
						{
							return customAttribute.UdtTypeName;
						}
						break;
					}
					break;
				}
			}
		}
		return null;
	}

	/// <summary>
	/// Maps an inferred OracleDbType to the protocol type byte used for an array's element-type header.
	/// This must match JDBC ComplexUtil#getMysqlType so the server can bind the scalar collection correctly.
	/// </summary>
	private static byte GetArrayElementTypeByte(OracleDbType elementType)
	{
		ColumnType columnType;
		switch (elementType)
		{
		case OracleDbType.Byte:
		case OracleDbType.Decimal:
		case OracleDbType.Double:
		case OracleDbType.Int16:
		case OracleDbType.Int32:
		case OracleDbType.Int64:
		case OracleDbType.Single:
		case OracleDbType.BinaryDouble:
		case OracleDbType.BinaryFloat:
			columnType = ColumnType.Long;
			break;
		case OracleDbType.Char:
		case OracleDbType.Clob:
		case OracleDbType.NClob:
		case OracleDbType.NChar:
		case OracleDbType.NVarchar2:
		case OracleDbType.Varchar2:
			columnType = ColumnType.VarChar;
			break;
		case OracleDbType.LongRaw:
		case OracleDbType.Raw:
			columnType = ColumnType.OracleRaw;
			break;
		case OracleDbType.Date:
		case OracleDbType.TimeStamp:
		case OracleDbType.TimeStampLTZ:
		case OracleDbType.TimeStampTZ:
			columnType = ColumnType.DateTime;
			break;
		case OracleDbType.Array:
		case OracleDbType.Object:
			columnType = ColumnType.OracleComplex;
			break;
		default:
			columnType = ColumnType.Long;
			break;
		}
		return (byte)columnType;
	}

	/// <summary>
	/// Infers the OracleDbType of an array's elements from the parameter value.
	/// For empty arrays, uses the CLR element type (mirrors JDBC metadata from <c>createArrayOf</c>).
	/// </summary>
	private static OracleDbType InferArrayElementType(OracleParameter parameter)
	{
		if (parameter.Value == null || parameter.Value == DBNull.Value)
		{
			return OracleDbType.Int32;
		}
		Type collectionElementClrType = GetCollectionElementClrType(parameter.Value.GetType());
		if ((object)collectionElementClrType != null)
		{
			OracleDbType? oracleDbType = InferOracleDbTypeFromElementClrType(collectionElementClrType);
			if (oracleDbType.HasValue)
			{
				return oracleDbType.Value;
			}
		}
		if (OracleUdt.TryCreateValue(null, parameter.Value, parameter.UdtTypeName, treatAsArray: true, out OracleUdtValue udtValue))
		{
			foreach (object value in udtValue.Values)
			{
				if (value != null && value != DBNull.Value)
				{
					return InferOracleDbTypeFromValue(value);
				}
			}
		}
		if (parameter.Value is IEnumerable enumerable && !(enumerable is string))
		{
			foreach (object? item in enumerable)
			{
				if (item != null && item != DBNull.Value)
				{
					return InferOracleDbTypeFromValue(item);
				}
			}
		}
		return OracleDbType.Int32;
	}

	private static Type? GetCollectionElementClrType(Type valueType)
	{
		if (valueType.IsArray)
		{
			return valueType.GetElementType();
		}
		Type[] interfaces = valueType.GetInterfaces();
		foreach (Type type in interfaces)
		{
			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
			{
				return type.GetGenericArguments()[0];
			}
		}
		return null;
	}

	private static OracleDbType? InferOracleDbTypeFromElementClrType(Type elementType)
	{
		elementType = Nullable.GetUnderlyingType(elementType) ?? elementType;
		if (elementType == typeof(string) || elementType == typeof(char))
		{
			return OracleDbType.Varchar2;
		}
		if (elementType == typeof(byte[]))
		{
			return OracleDbType.Raw;
		}
		if (elementType == typeof(bool))
		{
			return OracleDbType.Boolean;
		}
		if (elementType == typeof(int) || elementType == typeof(uint) || elementType == typeof(short) || elementType == typeof(ushort) || elementType == typeof(byte) || elementType == typeof(sbyte) || elementType == typeof(long) || elementType == typeof(ulong))
		{
			return OracleDbType.Int32;
		}
		if (elementType == typeof(float) || elementType == typeof(double) || elementType == typeof(decimal))
		{
			return OracleDbType.Double;
		}
		if (elementType == typeof(DateTime) || elementType == typeof(OracleTimeStamp))
		{
			return OracleDbType.TimeStamp;
		}
		if (elementType == typeof(DateTimeOffset))
		{
			return OracleDbType.TimeStampTZ;
		}
		if (elementType == typeof(OracleTimeStampTZ))
		{
			return OracleDbType.TimeStampTZ;
		}
		if (elementType == typeof(OracleTimeStampLTZ))
		{
			return OracleDbType.TimeStampLTZ;
		}
		if (elementType == typeof(OracleIntervalDS))
		{
			return OracleDbType.IntervalDS;
		}
		if (elementType == typeof(OracleIntervalYM))
		{
			return OracleDbType.IntervalYM;
		}
		if (typeof(IOracleCustomType).IsAssignableFrom(elementType))
		{
			return OracleDbType.Object;
		}
		return null;
	}

	/// <summary>
	/// Maps a .NET value to its corresponding OracleDbType for array element type inference.
	/// </summary>
	private static OracleDbType InferOracleDbTypeFromValue(object value)
	{
		if (!(value is int) && !(value is uint) && !(value is short) && !(value is ushort) && !(value is byte) && !(value is sbyte) && !(value is long) && !(value is ulong))
		{
			if (!(value is float) && !(value is double) && !(value is decimal))
			{
				if (!(value is bool))
				{
					if (!(value is string) && !(value is char))
					{
						if (!(value is byte[]))
						{
							if (!(value is DateTime) && !(value is OracleTimeStamp))
							{
								if (!(value is DateTimeOffset))
								{
									if (!(value is OracleTimeStampTZ))
									{
										if (!(value is OracleTimeStampLTZ))
										{
											if (!(value is OracleIntervalDS))
											{
												if (!(value is OracleIntervalYM))
												{
													if (value is IOracleCustomType || value is OracleUdtValue)
													{
														return OracleDbType.Object;
													}
													return OracleDbType.Varchar2;
												}
												return OracleDbType.IntervalYM;
											}
											return OracleDbType.IntervalDS;
										}
										return OracleDbType.TimeStampLTZ;
									}
									return OracleDbType.TimeStampTZ;
								}
								return OracleDbType.TimeStampTZ;
							}
							return OracleDbType.TimeStamp;
						}
						return OracleDbType.Raw;
					}
					return OracleDbType.Varchar2;
				}
				return OracleDbType.Boolean;
			}
			return OracleDbType.Double;
		}
		return OracleDbType.Int32;
	}
}
