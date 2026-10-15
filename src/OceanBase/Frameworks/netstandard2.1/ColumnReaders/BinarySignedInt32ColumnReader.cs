using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class BinarySignedInt32ColumnReader : ColumnReader
{
	public static BinarySignedInt32ColumnReader Instance { get; } = new BinarySignedInt32ColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return DoReadValue(data);
	}

	public override int? TryReadInt32(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return DoReadValue(data);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int DoReadValue(ReadOnlySpan<byte> data)
	{
		return MemoryMarshal.Read<int>(data);
	}
}
