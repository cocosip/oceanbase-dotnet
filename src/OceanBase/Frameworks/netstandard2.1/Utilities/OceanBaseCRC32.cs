using System;

namespace OceanBase.Utilities;

internal static class OceanBaseCRC32
{
	private const uint c_polynomial = 2197175160u;

	private static readonly uint[] s_table = CreateTable();

	public static uint Calculate(ReadOnlySpan<byte> buffer)
	{
		uint num = 0u;
		ReadOnlySpan<byte> readOnlySpan = buffer;
		for (int i = 0; i < readOnlySpan.Length; i++)
		{
			byte b = readOnlySpan[i];
			num = (num >> 8) ^ s_table[(num ^ b) & 0xFF];
		}
		return num;
	}

	private static uint[] CreateTable()
	{
		uint[] array = new uint[256];
		for (uint num = 0u; num < array.Length; num++)
		{
			uint num2 = num;
			for (int i = 0; i < 8; i++)
			{
				num2 = (((num2 & 1) == 0) ? (num2 >> 1) : ((num2 >> 1) ^ 0x82F63B78u));
			}
			array[num] = num2;
		}
		return array;
	}
}
