using System;
using System.Buffers.Text;
using System.Text;
using OceanBase.Protocol.Payloads;
using OceanBase.Utilities;

namespace OceanBase.ColumnReaders;

internal sealed class GuidChar36ColumnReader : ColumnReader
{
	public static GuidChar36ColumnReader Instance { get; } = new GuidChar36ColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		if (!Utf8Parser.TryParse(data, out Guid value, out int bytesConsumed, 'D') || bytesConsumed != 36)
		{
			throw new FormatException("Could not parse CHAR(36) value as Guid: " + Utility.GetString(Encoding.UTF8, data));
		}
		return value;
	}
}
