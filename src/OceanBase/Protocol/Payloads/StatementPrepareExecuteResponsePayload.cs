using System;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Protocol.Payloads;

/// <summary>
/// Parses the OceanBase Oracle-mode COM_STMT_PREPARE_EXECUTE (0xA1) header OK packet.
/// This is not the standard MySQL OK packet layout.
/// </summary>
internal readonly struct StatementPrepareExecuteResponsePayload
{
	public const int NotReturningResultSet = 0;

	public const int ReturningResultSetWithoutField = 1;

	public const int ArrayBindingField = 2;

	public const int ReturningResultSetWithField = 3;

	public const int PlOutParameter = 4;

	public int StatementId { get; }

	public int ColumnCount { get; }

	public int ParameterCount { get; }

	public int WarningCount { get; }

	public int ExtendFlag { get; }

	public bool HasResultSet { get; }

	/// <summary>
	/// Returns true when the server signals that the result set is an ArrayBinding internal result set
	/// (each row carries sql_no / error_code / affect_rows for one iteration).
	/// Such a result set must NOT be exposed to the user's OracleDataReader.
	/// </summary>
	public bool IsArrayBindingResult => (ExtendFlag & 2) != 0;

	public static StatementPrepareExecuteResponsePayload Create(ReadOnlySpan<byte> span)
	{
		ByteArrayReader byteArrayReader = new ByteArrayReader(span);
		byteArrayReader.ReadByte(0);
		int statementId = byteArrayReader.ReadInt32();
		int columnCount = byteArrayReader.ReadUInt16();
		int parameterCount = byteArrayReader.ReadUInt16();
		byteArrayReader.ReadByte(0);
		int warningCount = byteArrayReader.ReadInt16();
		int extendFlag = byteArrayReader.ReadInt32();
		bool hasResultSet = byteArrayReader.ReadByte() != 0;
		return new StatementPrepareExecuteResponsePayload(statementId, columnCount, parameterCount, warningCount, extendFlag, hasResultSet);
	}

	private StatementPrepareExecuteResponsePayload(int statementId, int columnCount, int parameterCount, int warningCount, int extendFlag, bool hasResultSet)
	{
		StatementId = statementId;
		ColumnCount = columnCount;
		ParameterCount = parameterCount;
		WarningCount = warningCount;
		ExtendFlag = extendFlag;
		HasResultSet = hasResultSet;
	}
}
