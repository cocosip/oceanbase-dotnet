using System;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Protocol.Payloads;

internal readonly struct StatementPrepareResponsePayload
{
	public int StatementId { get; }

	public int ColumnCount { get; }

	public int ParameterCount { get; }

	public static StatementPrepareResponsePayload Create(ReadOnlySpan<byte> span)
	{
		ByteArrayReader byteArrayReader = new ByteArrayReader(span);
		byteArrayReader.ReadByte(0);
		int statementId = byteArrayReader.ReadInt32();
		int columnCount = byteArrayReader.ReadUInt16();
		int parameterCount = byteArrayReader.ReadUInt16();
		byteArrayReader.ReadByte(0);
		byteArrayReader.ReadInt16();
		return new StatementPrepareResponsePayload(statementId, columnCount, parameterCount);
	}

	private StatementPrepareResponsePayload(int statementId, int columnCount, int parameterCount)
	{
		StatementId = statementId;
		ColumnCount = columnCount;
		ParameterCount = parameterCount;
	}
}
