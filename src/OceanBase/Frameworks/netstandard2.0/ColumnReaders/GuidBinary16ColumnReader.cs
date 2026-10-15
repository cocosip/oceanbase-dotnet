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
	public static Guid ReadGuid(ReadOnlySpan<byte> data)
	{
		return new Guid(new byte[16]
		{
			data[3],
			data[2],
			data[1],
			data[0],
			data[5],
			data[4],
			data[7],
			data[6],
			data[8],
			data[9],
			data[10],
			data[11],
			data[12],
			data[13],
			data[14],
			data[15]
		});
	}
}
