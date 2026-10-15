using System;
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
		byte[] array2 = data.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			Array.Reverse((Array)array2, i * 4, 4);
			array[i] = BitConverter.ToSingle(array2, i * 4);
		}
		return new ReadOnlyMemory<float>(array);
	}
}
