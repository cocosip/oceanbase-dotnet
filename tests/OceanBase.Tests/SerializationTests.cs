using System;
using System.Buffers.Binary;
using System.Text;
using OceanBase.Protocol.Serialization;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 协议字节编解码测试（internal 类型，无需服务端）。
///
/// 覆盖 <see cref="ByteBufferWriter"/> 与 <see cref="ByteArrayReader"/> ——
/// 全部 MySQL/OceanBase 报文的读写都建立在这两个类型上，
/// 它们的字节序/长度编码一旦出错，整个协议层都会错位。
/// </summary>
public sealed class SerializationTests
{
	// ---------- ByteBufferWriter ----------

	[Fact]
	public void ByteBufferWriter_writes_single_byte()
	{
		var w = new ByteBufferWriter(8);
		w.Write((byte)0xAB);
		var payload = w.ToPayloadData();
		var span = payload.Span;
		Assert.Equal(1, span.Length);
		Assert.Equal(0xAB, span[0]);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(127)]
	[InlineData(128)]
	[InlineData(16383)]
	[InlineData(16384)]
	[InlineData(2097151)]
	[InlineData(2097152)]
	[InlineData(int.MaxValue)]
	public void ByteBufferWriter_length_encoded_int_roundtrips(int value)
	{
		// MySQL 的 length-encoded integer：1/3/4/9 字节变长编码
		var w = new ByteBufferWriter(16);
		WriteLenEncInt(w, (ulong)value);
		var bytes = w.ToPayloadData().Span.ToArray();

		var r = new ByteArrayReader(bytes);
		Assert.Equal((ulong)value, (ulong)r.ReadLengthEncodedInteger());
	}

	[Fact]
	public void ByteBufferWriter_length_encoded_integer_uses_compact_encoding()
	{
		// 验证编码长度符合协议规定（不是统一用 9 字节）
		Assert.Single(WriteLen(0));
		Assert.Single(WriteLen(250));
		Assert.Equal(3, WriteLen(251).Length);
		Assert.Equal(3, WriteLen(65535).Length);
		Assert.Equal(4, WriteLen(65536).Length);
		Assert.Equal(4, WriteLen(16777215).Length);
		Assert.Equal(9, WriteLen(16777216).Length);

		static byte[] WriteLen(ulong v)
		{
			var w = new ByteBufferWriter(16);
			WriteLenEncInt(w, v);
			return w.ToPayloadData().Span.ToArray();
		}
	}

	[Fact]
	public void ByteBufferWriter_writes_int16_little_endian()
	{
		var w = new ByteBufferWriter(8);
		w.Write((ushort)0x1234);
		var b = w.ToPayloadData().Span.ToArray();
		Assert.Equal(2, b.Length);
		Assert.Equal(new byte[] { 0x34, 0x12 }, b);
	}

	[Fact]
	public void ByteBufferWriter_writes_int32_little_endian()
	{
		var w = new ByteBufferWriter(8);
		w.Write(0x12345678);
		var b = w.ToPayloadData().Span.ToArray();
		Assert.Equal(4, b.Length);
		Assert.Equal(0x12345678, BinaryPrimitives.ReadInt32LittleEndian(b));
	}

	[Fact]
	public void ByteBufferWriter_writes_uint64_little_endian()
	{
		var w = new ByteBufferWriter(16);
		w.Write(0x0102030405060708UL);
		var b = w.ToPayloadData().Span.ToArray();
		Assert.Equal(8, b.Length);
		Assert.Equal(0x0102030405060708UL, BinaryPrimitives.ReadUInt64LittleEndian(b));
	}

	[Fact]
	public void ByteBufferWriter_writes_csharp_string_verbatim()
	{
		// "NUL 结尾" 的 .NET 字符串（无长度前缀）
		var w = new ByteBufferWriter(32);
		w.Write("hello");
		var b = w.ToPayloadData().Span.ToArray();
		Assert.Equal(Encoding.UTF8.GetBytes("hello"), b);
	}

	[Fact]
	public void ByteBufferWriter_writes_null_terminated_string()
	{
		var w = new ByteBufferWriter(32);
		w.Write(Encoding.UTF8.GetBytes("hi\0"));
		var b = w.ToPayloadData().Span.ToArray();
		Assert.Equal(Encoding.UTF8.GetBytes("hi\0"), b);
	}

	[Fact]
	public void ByteBufferWriter_writes_length_encoded_string()
	{
		var w = new ByteBufferWriter(32);
		WriteLenEncBytes(w, Encoding.UTF8.GetBytes("abc"));
		var b = w.ToPayloadData().Span.ToArray();
		Assert.Equal(4, b.Length);        // 1 字节长度 + 3 字节内容
		Assert.Equal(3, b[0]);
		Assert.Equal(Encoding.UTF8.GetBytes("abc"), b[1..]);
	}

	[Fact]
	public void ByteBufferWriter_grows_beyond_initial_capacity()
	{
		var w = new ByteBufferWriter(1);
		for (var i = 0; i < 100; i++)
			w.Write((byte)i);
		var b = w.ToPayloadData().Span.ToArray();
		Assert.Equal(100, b.Length);
		for (var i = 0; i < 100; i++)
			Assert.Equal((byte)i, b[i]);
	}

	[Fact]
	public void ByteBufferWriter_TrimEnd_removes_trailing_bytes()
	{
		var w = new ByteBufferWriter(8);
		w.Write((byte)1);
		w.Write((byte)2);
		w.Write((byte)3);
		w.TrimEnd(1);
		Assert.Equal(new byte[] { 1, 2 }, w.ToPayloadData().Span.ToArray());
	}

	/// <summary>按 MySQL 协议写 length-encoded 整数（1/3/4/9 字节变长）。</summary>
	private static void WriteLenEncInt(ByteBufferWriter w, ulong value)
	{
		if (value < 251)
		{
			w.Write((byte)value);
		}
		else if (value < 65536)
		{
			w.Write((byte)0xFC);
			w.Write((ushort)value);
		}
		else if (value < 16777216)
		{
			w.Write((byte)0xFD);
			w.Write((byte)(value & 0xFF));
			w.Write((byte)((value >> 8) & 0xFF));
			w.Write((byte)((value >> 16) & 0xFF));
		}
		else
		{
			w.Write((byte)0xFE);
			w.Write(value);
		}
	}

	/// <summary>按 MySQL 协议写 length-encoded 字节串（长度前缀 + 内容）。</summary>
	private static void WriteLenEncBytes(ByteBufferWriter w, byte[] data)
	{
		WriteLenEncInt(w, (ulong)data.Length);
		w.Write(data);
	}

	// ---------- ByteArrayReader ----------

	[Fact]
	public void ByteArrayReader_reads_bytes_in_order()
	{
		var r = new ByteArrayReader(new byte[] { 0xAA, 0xBB, 0xCC });
		Assert.Equal(0xAA, r.ReadByte());
		Assert.Equal(0xBB, r.ReadByte());
		Assert.Equal(0xCC, r.ReadByte());
	}

	[Fact]
	public void ByteArrayReader_reads_int16_little_endian()
	{
		var r = new ByteArrayReader(new byte[] { 0x34, 0x12 });
		Assert.Equal((short)0x1234, r.ReadInt16());
	}

	[Fact]
	public void ByteArrayReader_reads_uint16_little_endian()
	{
		var r = new ByteArrayReader(new byte[] { 0x34, 0x12 });
		Assert.Equal(0x1234, r.ReadUInt16());
	}

	[Fact]
	public void ByteArrayReader_reads_int32_little_endian()
	{
		var r = new ByteArrayReader(new byte[] { 0x78, 0x56, 0x34, 0x12 });
		Assert.Equal(0x12345678, r.ReadInt32());
	}

	[Fact]
	public void ByteArrayReader_reads_uint32_little_endian()
	{
		var r = new ByteArrayReader(new byte[] { 0x78, 0x56, 0x34, 0x12 });
		Assert.Equal(0x12345678u, r.ReadUInt32());
	}

	[Fact]
	public void ByteArrayReader_reads_uint64_little_endian()
	{
		var r = new ByteArrayReader(new byte[] { 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 });
		Assert.Equal(0x0102030405060708L, r.ReadUInt64());
	}

	[Fact]
	public void ByteArrayReader_reads_3_byte_int()
	{
		// MySQL 里 3 字节整数用于 length-encoded 与协议版本等
		var r = new ByteArrayReader(new byte[] { 0x01, 0x02, 0x03 });
		Assert.Equal(0x030201, r.ReadInt3Bytes());
	}

	[Fact]
	public void ByteArrayReader_length_encoded_integer_roundtrips()
	{
		foreach (var v in new ulong[] { 0, 1, 250, 251, 65535, 65536, 16777215, 16777216, ulong.MaxValue })
		{
			var w = new ByteBufferWriter(16);
			WriteLenEncInt(w, v);
			var r = new ByteArrayReader(w.ToPayloadData().Span);
			Assert.Equal(v, (ulong)r.ReadLengthEncodedInteger());
		}
	}

	[Fact]
	public void ByteArrayReader_reads_length_encoded_string()
	{
		var w = new ByteBufferWriter(32);
		WriteLenEncBytes(w, Encoding.UTF8.GetBytes("hello"));
		var r = new ByteArrayReader(w.ToPayloadData().Span);
		Assert.Equal("hello", Encoding.UTF8.GetString(r.ReadLengthEncodedByteString()));
	}

	[Fact]
	public void ByteArrayReader_reads_null_terminated_string()
	{
		var data = Encoding.UTF8.GetBytes("world\0");
		var r = new ByteArrayReader(data);
		Assert.Equal("world", Encoding.UTF8.GetString(r.ReadNullTerminatedByteString()));
	}

	[Fact]
	public void ByteArrayReader_bytes_remaining_decreases()
	{
		var r = new ByteArrayReader(new byte[] { 1, 2, 3, 4 });
		Assert.Equal(4, r.BytesRemaining);
		r.ReadByte();
		Assert.Equal(3, r.BytesRemaining);
		r.ReadInt16();
		Assert.Equal(1, r.BytesRemaining);
	}

	[Fact]
	public void ByteArrayReader_throws_when_read_past_end()
	{
		// 注：ByteArrayReader 是 ref struct，不能捕获进 lambda，故手写 try/catch
		var r = new ByteArrayReader(new byte[] { 1 });
		var threw = false;
		try { r.ReadInt32(); }
		catch (InvalidOperationException) { threw = true; }
		Assert.True(threw, "读取越界应抛 InvalidOperationException");
	}

	// ---------- 往返一致性 ----------

	[Fact]
	public void Writer_then_reader_preserves_a_mixed_sequence()
	{
		var w = new ByteBufferWriter(64);
		w.Write((byte)0x01);
		w.Write((ushort)0xBEEF);
		w.Write(0xDEADBEEF);
		w.Write(0x1122334455667788UL);
		WriteLenEncBytes(w, Encoding.UTF8.GetBytes("混合 mixed 序列"));
		w.Write((byte)0xFF);

		var r = new ByteArrayReader(w.ToPayloadData().Span);
		Assert.Equal(0x01, r.ReadByte());
		Assert.Equal(0xBEEF, r.ReadUInt16());
		Assert.Equal(unchecked((int)0xDEADBEEF), r.ReadInt32());
		Assert.Equal(0x1122334455667788L, r.ReadUInt64());
		Assert.Equal("混合 mixed 序列", Encoding.UTF8.GetString(r.ReadLengthEncodedByteString()));
		Assert.Equal(0xFF, r.ReadByte());
		Assert.Equal(0, r.BytesRemaining);
	}
}
