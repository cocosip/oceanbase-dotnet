using System;
using System.Buffers.Text;
using System.Text;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class TextDoubleColumnReader : ColumnReader
{
	public static TextDoubleColumnReader Instance { get; } = new TextDoubleColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		if (Utf8Parser.TryParse(data, out double value, out int bytesConsumed, '\0') && bytesConsumed == data.Length)
		{
			return value;
		}
		if (data.SequenceEqual("-inf"u8))
		{
			return double.NegativeInfinity;
		}
		if (data.SequenceEqual("inf"u8))
		{
			return double.PositiveInfinity;
		}
		if (data.SequenceEqual("nan"u8))
		{
			return double.NaN;
		}
		throw new FormatException("Couldn't parse value as double: " + Encoding.UTF8.GetString(data));
	}
}
