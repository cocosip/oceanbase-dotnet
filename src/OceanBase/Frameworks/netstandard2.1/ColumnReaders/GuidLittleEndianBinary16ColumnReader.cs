using System;
using System.Runtime.CompilerServices;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class GuidLittleEndianBinary16ColumnReader : ColumnReader
{
	public static GuidLittleEndianBinary16ColumnReader Instance { get; } = new GuidLittleEndianBinary16ColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return ReadGuid(data);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Guid ReadGuid(ReadOnlySpan<byte> data)
	{
		return new Guid(data);
	}
}
