using System;

namespace OceanBase.Protocol.Serialization;

internal static class SerializationUtility
{
	public static uint ReadUInt32(ReadOnlySpan<byte> span)
	{
		uint num = 0u;
		for (int i = 0; i < span.Length; i++)
		{
			num |= (uint)(span[i] << 8 * i);
		}
		return num;
	}

	public static void WriteUInt32(uint value, byte[] buffer, int offset, int count)
	{
		for (int i = 0; i < count; i++)
		{
			buffer[offset + i] = (byte)(value & 0xFF);
			value >>= 8;
		}
	}
}
