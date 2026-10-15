using System;
using System.Buffers.Binary;

namespace OceanBase.Protocol.Serialization;

internal ref struct ByteArrayReader(ReadOnlySpan<byte> buffer)
{
	private readonly ReadOnlySpan<byte> m_buffer = buffer;

	private readonly int m_maxOffset = buffer.Length;

	private int m_offset = 0;

	public int Offset
	{
		readonly get
		{
			return m_offset;
		}
		set
		{
			ArgumentOutOfRangeException.ThrowIfNegative(value, "value");
			ArgumentOutOfRangeException.ThrowIfGreaterThan(value, m_maxOffset, "value");
			m_offset = value;
		}
	}

	public readonly int BytesRemaining => m_maxOffset - m_offset;

	public byte ReadByte()
	{
		VerifyRead(1);
		return m_buffer[m_offset++];
	}

	public void ReadByte(byte value)
	{
		if (ReadByte() != value)
		{
			throw new FormatException($"Expected to read 0x{value:X2} but got 0x{m_buffer[m_offset - 1]:X2}");
		}
	}

	public short ReadInt16()
	{
		VerifyRead(2);
		ReadOnlySpan<byte> buffer = m_buffer;
		int offset = m_offset;
		short result = BinaryPrimitives.ReadInt16LittleEndian(buffer.Slice(offset, buffer.Length - offset));
		m_offset += 2;
		return result;
	}

	public ushort ReadUInt16()
	{
		VerifyRead(2);
		ReadOnlySpan<byte> buffer = m_buffer;
		int offset = m_offset;
		ushort result = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(offset, buffer.Length - offset));
		m_offset += 2;
		return result;
	}

	public int ReadInt32()
	{
		VerifyRead(4);
		ReadOnlySpan<byte> buffer = m_buffer;
		int offset = m_offset;
		int result = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(offset, buffer.Length - offset));
		m_offset += 4;
		return result;
	}

	public uint ReadUInt32()
	{
		VerifyRead(4);
		ReadOnlySpan<byte> buffer = m_buffer;
		int offset = m_offset;
		uint result = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(offset, buffer.Length - offset));
		m_offset += 4;
		return result;
	}

	public long ReadUInt64()
	{
		VerifyRead(8);
		ReadOnlySpan<byte> buffer = m_buffer;
		int offset = m_offset;
		ulong result = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(offset, buffer.Length - offset));
		m_offset += 8;
		return (long)result;
	}

	public int ReadInt3Bytes()
	{
		VerifyRead(3);
		int result = m_buffer[m_offset] | (m_buffer[m_offset + 1] << 8) | (m_buffer[m_offset + 2] << 16);
		m_offset += 3;
		return result;
	}

	public uint ReadFixedLengthUInt32(int length)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(length, 0, "length");
		ArgumentOutOfRangeException.ThrowIfGreaterThan(length, 4, "length");
		VerifyRead(length);
		uint num = 0u;
		for (int i = 0; i < length; i++)
		{
			num |= (uint)(m_buffer[m_offset + i] << 8 * i);
		}
		m_offset += length;
		return num;
	}

	public ulong ReadFixedLengthUInt64(int length)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(length, 0, "length");
		ArgumentOutOfRangeException.ThrowIfGreaterThan(length, 8, "length");
		VerifyRead(length);
		ulong num = 0uL;
		for (int i = 0; i < length; i++)
		{
			num |= (ulong)m_buffer[m_offset + i] << 8 * i;
		}
		m_offset += length;
		return num;
	}

	public ReadOnlySpan<byte> ReadNullTerminatedByteString()
	{
		int i;
		for (i = m_offset; i < m_maxOffset && m_buffer[i] != 0; i++)
		{
		}
		if (i == m_maxOffset)
		{
			throw new FormatException("Read past end of buffer looking for NUL.");
		}
		ReadOnlySpan<byte> buffer = m_buffer;
		int offset = m_offset;
		ReadOnlySpan<byte> result = buffer.Slice(offset, i - offset);
		m_offset = i + 1;
		return result;
	}

	public ReadOnlySpan<byte> ReadNullOrEofTerminatedByteString()
	{
		int i;
		for (i = m_offset; i < m_maxOffset && m_buffer[i] != 0; i++)
		{
		}
		ReadOnlySpan<byte> buffer = m_buffer;
		int offset = m_offset;
		ReadOnlySpan<byte> result = buffer.Slice(offset, i - offset);
		if (i < m_maxOffset && m_buffer[i] == 0)
		{
			i++;
		}
		m_offset = i;
		return result;
	}

	public ReadOnlySpan<byte> ReadByteString(int length)
	{
		VerifyRead(length);
		ReadOnlySpan<byte> result = m_buffer.Slice(m_offset, length);
		m_offset += length;
		return result;
	}

	public ulong ReadLengthEncodedInteger()
	{
		byte b = m_buffer[m_offset++];
		return b switch
		{
			251 => throw new FormatException("Length-encoded integer cannot have 0xFB prefix byte."), 
			252 => ReadFixedLengthUInt32(2), 
			253 => ReadFixedLengthUInt32(3), 
			254 => ReadFixedLengthUInt64(8), 
			byte.MaxValue => throw new FormatException("Length-encoded integer cannot have 0xFF prefix byte."), 
			_ => b, 
		};
	}

	public int ReadLengthEncodedIntegerOrNull()
	{
		if (m_buffer[m_offset] == 251)
		{
			m_offset++;
			return -1;
		}
		return checked((int)ReadLengthEncodedInteger());
	}

	public ReadOnlySpan<byte> ReadLengthEncodedByteString()
	{
		int num = checked((int)ReadLengthEncodedInteger());
		ReadOnlySpan<byte> result = m_buffer.Slice(m_offset, num);
		m_offset += num;
		return result;
	}

	private readonly void VerifyRead(int length)
	{
		if (m_offset + length > m_maxOffset)
		{
			throw new InvalidOperationException("Read past end of buffer.");
		}
	}
}
