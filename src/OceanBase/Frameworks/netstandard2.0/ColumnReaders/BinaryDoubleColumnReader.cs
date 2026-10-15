using System;
using System.Runtime.InteropServices;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class BinaryDoubleColumnReader : ColumnReader
{
	public static BinaryDoubleColumnReader Instance { get; } = new BinaryDoubleColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return MemoryMarshal.Read<double>(data);
	}
}
