using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class BinaryUnsignedInt64ColumnReader : ColumnReader
{
	public static BinaryUnsignedInt64ColumnReader Instance { get; } = new BinaryUnsignedInt64ColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return DoReadValue(data);
	}

	public override int? TryReadInt32(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return checked((int)DoReadValue(data));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong DoReadValue(ReadOnlySpan<byte> data)
	{
		return MemoryMarshal.Read<ulong>(data);
	}
}
