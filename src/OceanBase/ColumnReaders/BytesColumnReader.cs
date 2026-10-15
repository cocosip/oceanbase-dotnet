using System;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class BytesColumnReader : ColumnReader
{
	public static BytesColumnReader Instance { get; } = new BytesColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return data.ToArray();
	}
}
