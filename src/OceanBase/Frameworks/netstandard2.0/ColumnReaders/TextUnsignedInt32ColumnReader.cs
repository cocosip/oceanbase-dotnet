using System;
using System.Buffers.Text;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class TextUnsignedInt32ColumnReader : ColumnReader
{
	public static TextUnsignedInt32ColumnReader Instance { get; } = new TextUnsignedInt32ColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return DoReadValue(data);
	}

	public override int? TryReadInt32(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return checked((int)DoReadValue(data));
	}

	private static uint DoReadValue(ReadOnlySpan<byte> data)
	{
		if (Utf8Parser.TryParse(data, out uint value, out int bytesConsumed, '\0') && bytesConsumed == data.Length)
		{
			return value;
		}
		throw new FormatException();
	}
}
