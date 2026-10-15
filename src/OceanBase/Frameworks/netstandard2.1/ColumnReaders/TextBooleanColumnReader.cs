using System;
using System.Runtime.CompilerServices;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class TextBooleanColumnReader : ColumnReader
{
	public static TextBooleanColumnReader Instance { get; } = new TextBooleanColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return DoReadValue(data);
	}

	public override int? TryReadInt32(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return DoReadValue(data) ? 1 : 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool DoReadValue(ReadOnlySpan<byte> data)
	{
		return data[0] != 48;
	}
}
