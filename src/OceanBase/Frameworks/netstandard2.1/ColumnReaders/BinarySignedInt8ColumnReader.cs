using System;
using System.Runtime.CompilerServices;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class BinarySignedInt8ColumnReader : ColumnReader
{
	public static BinarySignedInt8ColumnReader Instance { get; } = new BinarySignedInt8ColumnReader();

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
		return (sbyte)data[0];
	}
}
