using System;
using System.Buffers.Text;
using System.Text;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class GuidChar32ColumnReader : ColumnReader
{
	public static GuidChar32ColumnReader Instance { get; } = new GuidChar32ColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		if (!Utf8Parser.TryParse(data, out Guid value, out int bytesConsumed, 'N') || bytesConsumed != 32)
		{
			throw new FormatException("Could not parse CHAR(32) value as Guid: " + Encoding.UTF8.GetString(data));
		}
		return value;
	}
}
