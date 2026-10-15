using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class BinarySignedInt64ColumnReader : ColumnReader
{
	public static BinarySignedInt64ColumnReader Instance { get; } = new BinarySignedInt64ColumnReader();

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
		return MemoryMarshal.Read<long>(data);
	}
}
