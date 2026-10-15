using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class VectorColumnReader : ColumnReader
{
	public static VectorColumnReader Instance { get; } = new VectorColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		if (BitConverter.IsLittleEndian)
		{
			return new ReadOnlyMemory<float>(MemoryMarshal.Cast<byte, float>(data).ToArray());
		}
		float[] array = new float[data.Length / 4];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = BinaryPrimitives.ReadSingleLittleEndian(data.Slice(i * 4));
		}
		return new ReadOnlyMemory<float>(array);
	}
}
