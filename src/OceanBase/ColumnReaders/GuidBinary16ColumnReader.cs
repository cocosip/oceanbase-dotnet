using System;
using System.Runtime.CompilerServices;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class GuidBinary16ColumnReader : ColumnReader
{
	public static GuidBinary16ColumnReader Instance { get; } = new GuidBinary16ColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return ReadGuid(data);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static Guid ReadGuid(ReadOnlySpan<byte> data)
	{
		return new Guid(data, bigEndian: true);
	}
}
