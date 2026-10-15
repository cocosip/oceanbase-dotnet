using System;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class NullColumnReader : ColumnReader
{
	public static NullColumnReader Instance { get; } = new NullColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return DBNull.Value;
	}
}
