using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class BinaryUnsignedInt32ColumnReader : ColumnReader
{
	public static BinaryUnsignedInt32ColumnReader Instance { get; } = new BinaryUnsignedInt32ColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return DoReadValue(data);
	}

	public override int? TryReadInt32(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return checked((int)DoReadValue(data));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint DoReadValue(ReadOnlySpan<byte> data)
	{
		return MemoryMarshal.Read<uint>(data);
	}
}
