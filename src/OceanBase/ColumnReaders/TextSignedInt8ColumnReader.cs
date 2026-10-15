using System;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class TextSignedInt8ColumnReader : ColumnReader
{
	public static TextSignedInt8ColumnReader Instance { get; } = new TextSignedInt8ColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return DoReadValue(data);
	}

	public override int? TryReadInt32(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return DoReadValue(data);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static sbyte DoReadValue(ReadOnlySpan<byte> data)
	{
		if (Utf8Parser.TryParse(data, out sbyte value, out int bytesConsumed, '\0') && bytesConsumed == data.Length)
		{
			return value;
		}
		throw new FormatException();
	}
}
