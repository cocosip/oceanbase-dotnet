using System;
using System.Text;
using OceanBase.Utilities;

namespace OceanBase.Protocol.Serialization;

internal static class ByteBufferWriterExtensions
{
	public static void WriteLengthEncodedInteger(this ByteBufferWriter writer, ulong value)
	{
		if (value < 65536)
		{
			if (value < 251)
			{
				writer.Write((byte)value);
				return;
			}
			writer.Write((byte)252);
			writer.Write((ushort)value);
		}
		else if (value < 16777216)
		{
			writer.Write((uint)((value << 8) | 0xFD));
		}
		else
		{
			writer.Write((byte)254);
			writer.Write(value);
		}
	}

	public static void WriteLengthEncodedString(this ByteBufferWriter writer, string value)
	{
		writer.WriteLengthEncodedString(value.AsSpan());
	}

	public static void WriteLengthEncodedString(this ByteBufferWriter writer, ReadOnlySpan<char> value)
	{
		int byteCount = Utility.GetByteCount(Encoding.UTF8, value);
		writer.WriteLengthEncodedInteger((ulong)byteCount);
		Span<byte> span = writer.GetSpan(byteCount);
		int bytes = Utility.GetBytes(Encoding.UTF8, value, span);
		writer.Advance(bytes);
	}

	public static void WriteLengthEncodedAsciiString(this ByteBufferWriter writer, string value)
	{
		writer.WriteLengthEncodedInteger((ulong)value.Length);
		writer.WriteAscii(value.AsSpan());
	}

	public static void WriteNullTerminatedString(this ByteBufferWriter writer, string value)
	{
		writer.Write(value);
		writer.Write((byte)0);
	}
}
