using System;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class DecimalColumnReader : ColumnReader
{
	public static DecimalColumnReader Instance { get; } = new DecimalColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return DoReadValue(data);
	}

	public override int? TryReadInt32(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return (int)DoReadValue(data);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static decimal DoReadValue(ReadOnlySpan<byte> data)
	{
		if (Utf8Parser.TryParse(data, out decimal value, out int bytesConsumed, '\0') && bytesConsumed == data.Length)
		{
			return value;
		}
		throw new FormatException();
	}
}
