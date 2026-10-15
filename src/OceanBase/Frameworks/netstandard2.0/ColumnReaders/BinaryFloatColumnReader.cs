using System;
using System.Runtime.InteropServices;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class BinaryFloatColumnReader : ColumnReader
{
	public static BinaryFloatColumnReader Instance { get; } = new BinaryFloatColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return MemoryMarshal.Read<float>(data);
	}
}
