using System;
using System.Buffers.Text;
using System.Text;
using OceanBase.Protocol.Payloads;
using OceanBase.Utilities;

namespace OceanBase.ColumnReaders;

internal sealed class TextFloatColumnReader : ColumnReader
{
	public static TextFloatColumnReader Instance { get; } = new TextFloatColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		if (Utf8Parser.TryParse(data, out float value, out int bytesConsumed, '\0') && bytesConsumed == data.Length)
		{
			return value;
		}
		if (data.SequenceEqual("-inf"u8))
		{
			return float.NegativeInfinity;
		}
		if (data.SequenceEqual("inf"u8))
		{
			return float.PositiveInfinity;
		}
		if (data.SequenceEqual("nan"u8))
		{
			return float.NaN;
		}
		throw new FormatException("Couldn't parse value as float: " + Utility.GetString(Encoding.UTF8, data));
	}
}
