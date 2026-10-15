using System;
using System.Runtime.CompilerServices;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class BinaryUnsignedInt8ColumnReader : ColumnReader
{
	public static BinaryUnsignedInt8ColumnReader Instance { get; } = new BinaryUnsignedInt8ColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return DoReadValue(data);
	}

	public override int? TryReadInt32(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return DoReadValue(data);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static byte DoReadValue(ReadOnlySpan<byte> data)
	{
		return data[0];
	}
}
