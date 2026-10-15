using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class BinaryUnsignedInt16ColumnReader : ColumnReader
{
	public static BinaryUnsignedInt16ColumnReader Instance { get; } = new BinaryUnsignedInt16ColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return DoReadValue(data);
	}

	public override int? TryReadInt32(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return DoReadValue(data);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ushort DoReadValue(ReadOnlySpan<byte> data)
	{
		return MemoryMarshal.Read<ushort>(data);
	}
}
