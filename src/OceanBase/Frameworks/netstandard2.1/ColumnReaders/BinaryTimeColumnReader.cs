using System;
using System.Runtime.InteropServices;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class BinaryTimeColumnReader : ColumnReader
{
	public static BinaryTimeColumnReader Instance { get; } = new BinaryTimeColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		if (data.Length == 0)
		{
			return TimeSpan.Zero;
		}
		byte num = data[0];
		int num2 = MemoryMarshal.Read<int>(data.Slice(1, data.Length - 1));
		int num3 = data[5];
		int num4 = data[6];
		int num5 = data[7];
		int num6;
		if (data.Length != 8)
		{
			num6 = MemoryMarshal.Read<int>(data.Slice(8, data.Length - 8));
		}
		else
		{
			num6 = 0;
		}
		int num7 = num6;
		if (num != 0)
		{
			num2 = -num2;
			num3 = -num3;
			num4 = -num4;
			num5 = -num5;
			num7 = -num7;
		}
		return new TimeSpan(num2, num3, num4, num5) + TimeSpan.FromTicks(num7 * 10);
	}
}
