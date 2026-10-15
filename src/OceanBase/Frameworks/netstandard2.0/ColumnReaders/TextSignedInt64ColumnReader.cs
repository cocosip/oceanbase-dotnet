using System;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class TextSignedInt64ColumnReader : ColumnReader
{
	public static TextSignedInt64ColumnReader Instance { get; } = new TextSignedInt64ColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return DoReadValue(data);
	}

	public override int? TryReadInt32(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return checked((int)DoReadValue(data));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static long DoReadValue(ReadOnlySpan<byte> data)
	{
		if (Utf8Parser.TryParse(data, out long value, out int bytesConsumed, '\0') && bytesConsumed == data.Length)
		{
			return value;
		}
		throw new FormatException();
	}
}
