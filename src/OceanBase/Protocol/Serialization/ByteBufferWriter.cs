using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Text;

namespace OceanBase.Protocol.Serialization;

internal sealed class ByteBufferWriter : IBufferWriter<byte>
{
	private readonly Encoding m_textEncoding;

	private Encoder? m_textEncoder;

	private byte[] m_buffer;

	private Memory<byte> m_output;

	public int Position => m_buffer.Length - m_output.Length;

	public ArraySegment<byte> ArraySegment => new ArraySegment<byte>(m_buffer, 0, Position);

	public ByteBufferWriter(int capacity = 0)
		: this(capacity, Encoding.UTF8)
	{
	}

	/// <summary>
	/// Creates a <see cref="T:OceanBase.Protocol.Serialization.ByteBufferWriter" /> that encodes text using the specified <paramref name="textEncoding" />.
	/// When <paramref name="textEncoding" /> is <c>null</c>, defaults to <see cref="P:System.Text.Encoding.UTF8" />.
	/// The encoding is used by <see cref="M:OceanBase.Protocol.Serialization.ByteBufferWriter.Write(System.ReadOnlySpan{System.Char})" /> and <see cref="M:OceanBase.Protocol.Serialization.ByteBufferWriter.Write(System.String)" /> —
	/// typically for SQL query text that must match the server's <c>character_set_client</c>.
	/// Protocol-level metadata (column names, length-encoded strings) always uses UTF-8.
	/// </summary>
	public ByteBufferWriter(int capacity, Encoding? textEncoding)
	{
		m_buffer = ArrayPool<byte>.Shared.Rent(Math.Max(capacity, 128));
		m_output = m_buffer;
		m_textEncoding = textEncoding ?? Encoding.UTF8;
	}

	public PayloadData ToPayloadData()
	{
		return new PayloadData(ArraySegment, isPooled: true);
	}

	public Memory<byte> GetMemory(int sizeHint = 0)
	{
		if (sizeHint > m_output.Length)
		{
			Reallocate(sizeHint);
		}
		return m_output;
	}

	public Span<byte> GetSpan(int sizeHint = 0)
	{
		if (sizeHint > m_output.Length)
		{
			Reallocate(sizeHint);
		}
		return m_output.Span;
	}

	public void Advance(int count)
	{
		ref Memory<byte> output = ref m_output;
		m_output = output.Slice(count, output.Length - count);
	}

	public void TrimEnd(int byteCount)
	{
		m_output = m_buffer.AsMemory(Position - byteCount);
	}

	public void Write(byte value)
	{
		if (m_output.Length < 1)
		{
			Reallocate();
		}
		m_output.Span[0] = value;
		ref Memory<byte> output = ref m_output;
		m_output = output.Slice(1, output.Length - 1);
	}

	public void Write(ushort value)
	{
		if (m_output.Length < 2)
		{
			Reallocate(2);
		}
		BinaryPrimitives.WriteUInt16LittleEndian(m_output.Span, value);
		ref Memory<byte> output = ref m_output;
		m_output = output.Slice(2, output.Length - 2);
	}

	public void Write(int value)
	{
		if (m_output.Length < 4)
		{
			Reallocate(4);
		}
		BinaryPrimitives.WriteInt32LittleEndian(m_output.Span, value);
		ref Memory<byte> output = ref m_output;
		m_output = output.Slice(4, output.Length - 4);
	}

	public void Write(uint value)
	{
		if (m_output.Length < 4)
		{
			Reallocate(4);
		}
		BinaryPrimitives.WriteUInt32LittleEndian(m_output.Span, value);
		ref Memory<byte> output = ref m_output;
		m_output = output.Slice(4, output.Length - 4);
	}

	public void Write(ulong value)
	{
		if (m_output.Length < 8)
		{
			Reallocate(8);
		}
		BinaryPrimitives.WriteUInt64LittleEndian(m_output.Span, value);
		ref Memory<byte> output = ref m_output;
		m_output = output.Slice(8, output.Length - 8);
	}

	public void Write(ArraySegment<byte> arraySegment)
	{
		Write(arraySegment.AsSpan());
	}

	public void Write(ReadOnlySpan<byte> span)
	{
		if (m_output.Length < span.Length)
		{
			Reallocate(span.Length);
		}
		span.CopyTo(m_output.Span);
		ref Memory<byte> output = ref m_output;
		int length = span.Length;
		m_output = output.Slice(length, output.Length - length);
	}

	public void Write(string value)
	{
		Write(value.AsSpan());
	}

	public void WriteAscii(string value)
	{
		WriteAscii(value.AsSpan());
	}

	public void Write(string value, int offset, int length)
	{
		Write(value.AsSpan(offset, length));
	}

	/// <summary>
	/// Writes a character span using <see cref="F:OceanBase.Protocol.Serialization.ByteBufferWriter.m_textEncoding" /> (the session charset for SQL query text,
	/// or UTF-8 for protocol-level metadata).
	/// </summary>
	public void Write(ReadOnlySpan<char> chars)
	{
		if (m_output.Length < chars.Length * 3)
		{
			int byteCount = m_textEncoding.GetByteCount(chars);
			if (m_output.Length < byteCount)
			{
				Reallocate(byteCount);
			}
		}
		ref Memory<byte> output = ref m_output;
		int bytes = m_textEncoding.GetBytes(chars, m_output.Span);
		m_output = output.Slice(bytes, output.Length - bytes);
	}

	public void Write(ReadOnlySpan<char> chars, bool flush)
	{
		if (m_textEncoder == null)
		{
			m_textEncoder = m_textEncoding.GetEncoder();
		}
		int num;
		while (chars.Length > 0)
		{
			if (m_output.Length < 4)
			{
				Reallocate();
			}
			m_textEncoder.Convert(chars, m_output.Span, flush: false, out var charsUsed, out var bytesUsed, out var completed);
			num = charsUsed;
			chars = chars.Slice(num, chars.Length - num);
			ref Memory<byte> output = ref m_output;
			num = bytesUsed;
			m_output = output.Slice(num, output.Length - num);
			if (!completed)
			{
				Reallocate();
			}
		}
		if (flush && m_textEncoder != null)
		{
			if (m_output.Length < 4)
			{
				Reallocate();
			}
			m_textEncoder.Convert("".AsSpan(), m_output.Span, flush: true, out num, out var bytesUsed2, out var _);
			ref Memory<byte> output = ref m_output;
			num = bytesUsed2;
			m_output = output.Slice(num, output.Length - num);
		}
	}

	public void WriteAscii(ReadOnlySpan<char> chars)
	{
		if (m_output.Length < chars.Length)
		{
			Reallocate(chars.Length);
		}
		Ascii.FromUtf16(chars, m_output.Span, out var bytesWritten);
		ref Memory<byte> output = ref m_output;
		int num = bytesWritten;
		m_output = output.Slice(num, output.Length - num);
	}

	public void WriteLengthEncodedString(StringBuilder stringBuilder)
	{
		int position = Position;
		if (m_output.Length < 9)
		{
			Reallocate(9);
		}
		Advance(9);
		Encoder encoder = Encoding.UTF8.GetEncoder();
		StringBuilder.ChunkEnumerator enumerator = stringBuilder.GetChunks().GetEnumerator();
		ref Memory<byte> output = ref m_output;
		int num;
		while (enumerator.MoveNext())
		{
			ReadOnlySpan<char> chars = enumerator.Current.Span;
			while (chars.Length > 0)
			{
				if (m_output.Length < 4)
				{
					Reallocate();
				}
				encoder.Convert(chars, m_output.Span, flush: false, out var charsUsed, out var bytesUsed, out var completed);
				num = charsUsed;
				chars = chars.Slice(num, chars.Length - num);
				output = ref m_output;
				num = bytesUsed;
				m_output = output.Slice(num, output.Length - num);
				if (!completed)
				{
					Reallocate();
				}
			}
		}
		if (m_output.Length < 4)
		{
			Reallocate();
		}
		encoder.Convert("".AsSpan(), m_output.Span, flush: true, out num, out var bytesUsed2, out var _);
		output = ref m_output;
		num = bytesUsed2;
		m_output = output.Slice(num, output.Length - num);
		int num2 = Position - (position + 9);
		m_buffer[position] = 254;
		BinaryPrimitives.WriteUInt64LittleEndian(m_buffer.AsSpan(position + 1), (ulong)num2);
	}

	public void WriteString(short value)
	{
		Utf8Formatter.TryFormat(value, GetSpan(6), out var bytesWritten);
		ref Memory<byte> output = ref m_output;
		int num = bytesWritten;
		m_output = output.Slice(num, output.Length - num);
	}

	public void WriteString(ushort value)
	{
		Utf8Formatter.TryFormat(value, GetSpan(5), out var bytesWritten);
		ref Memory<byte> output = ref m_output;
		int num = bytesWritten;
		m_output = output.Slice(num, output.Length - num);
	}

	public void WriteString(int value)
	{
		Utf8Formatter.TryFormat(value, GetSpan(11), out var bytesWritten);
		ref Memory<byte> output = ref m_output;
		int num = bytesWritten;
		m_output = output.Slice(num, output.Length - num);
	}

	public void WriteString(uint value)
	{
		Utf8Formatter.TryFormat(value, GetSpan(10), out var bytesWritten);
		ref Memory<byte> output = ref m_output;
		int num = bytesWritten;
		m_output = output.Slice(num, output.Length - num);
	}

	public void WriteString(long value)
	{
		Utf8Formatter.TryFormat(value, GetSpan(20), out var bytesWritten);
		ref Memory<byte> output = ref m_output;
		int num = bytesWritten;
		m_output = output.Slice(num, output.Length - num);
	}

	public void WriteString(ulong value)
	{
		Utf8Formatter.TryFormat(value, GetSpan(20), out var bytesWritten);
		ref Memory<byte> output = ref m_output;
		int num = bytesWritten;
		m_output = output.Slice(num, output.Length - num);
	}

	private void Reallocate(int additional = 0)
	{
		int position = Position;
		byte[] array = ArrayPool<byte>.Shared.Rent(Math.Max(position + additional, m_buffer.Length * 2));
		m_buffer.AsSpan(0, position).CopyTo(array);
		ArrayPool<byte>.Shared.Return(m_buffer);
		m_buffer = array;
		m_output = new Memory<byte>(m_buffer, position, m_buffer.Length - position);
	}
}
