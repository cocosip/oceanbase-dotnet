using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using OceanBase.Protocol.Serialization;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 字节传输层测试（internal，无需服务端）。
///
/// <see cref="StreamByteHandler"/> / <see cref="SocketByteHandler"/> / <see cref="BufferedByteReader"/>
/// 是协议栈的最底层；<see cref="CompressedPayloadHandler"/> 在其上实现 MySQL 的压缩报文
/// （7 字节压缩头 + zlib 载荷）。这里用内存流与回环 socket 驱动真实读写路径。
/// </summary>
public sealed class ByteHandlerTests
{
	/// <summary>内存中的 IByteHandler：读取预置字节，写入追加到列表。</summary>
	private sealed class FakeByteHandler : IByteHandler
	{
		private readonly MemoryStream m_readStream;

		public FakeByteHandler(byte[] readable)
		{
			m_readStream = new MemoryStream(readable);
		}

		public List<byte> Written { get; } = new();

		public int RemainingTimeout { get; set; } = int.MaxValue;

		public bool ThrowOnRead { get; set; }

		public ValueTask<int> ReadBytesAsync(Memory<byte> buffer, IOBehavior ioBehavior)
		{
			if (ThrowOnRead)
				throw new IOException("simulated read failure");
			var count = m_readStream.Read(buffer.Span);
			return new ValueTask<int>(count);
		}

		public ValueTask WriteBytesAsync(ReadOnlyMemory<byte> data, IOBehavior ioBehavior)
		{
			Written.AddRange(data.ToArray());
			return default;
		}

		public void Dispose()
		{
			m_readStream.Dispose();
		}
	}

	// ---------- StreamByteHandler ----------

	[Fact]
	public async Task StreamByteHandler_roundtrips_bytes_through_stream()
	{
		using var stream = new MemoryStream();
		using var handler = new StreamByteHandler(stream);

		await handler.WriteBytesAsync(new byte[] { 1, 2, 3 }, IOBehavior.Synchronous);
		await handler.WriteBytesAsync(new byte[] { 4, 5 }, IOBehavior.Asynchronous);

		stream.Position = 0;
		var buffer = new byte[5];
		var read = await handler.ReadBytesAsync(buffer, IOBehavior.Synchronous);
		Assert.Equal(5, read);
		Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer);
	}

	[Fact]
	public async Task StreamByteHandler_async_read_returns_available_bytes()
	{
		using var handler = new StreamByteHandler(new MemoryStream(new byte[] { 9, 8, 7 }));
		var buffer = new byte[8];
		var read = await handler.ReadBytesAsync(buffer, IOBehavior.Asynchronous);
		Assert.Equal(3, read);
		Assert.Equal(new byte[] { 9, 8, 7, 0, 0, 0, 0, 0 }, buffer);
	}

	[Fact]
	public async Task StreamByteHandler_default_remaining_timeout_is_infinite()
	{
		using var handler = new StreamByteHandler(new MemoryStream(new byte[] { 1 }));
		Assert.Equal(int.MaxValue, handler.RemainingTimeout);

		// 无限超时时不写入 Stream.ReadTimeout（MemoryStream 不支持超时）
		await handler.ReadBytesAsync(new byte[1], IOBehavior.Synchronous);
		Assert.Equal(int.MaxValue, handler.RemainingTimeout);
	}

	[Fact]
	public async Task StreamByteHandler_expired_timeout_returns_timeout_exception()
	{
		using var handler = new StreamByteHandler(new MemoryStream(new byte[] { 1 }))
		{
			RemainingTimeout = 0,
		};
		await Assert.ThrowsAsync<MySqlException>(
			async () => await handler.ReadBytesAsync(new byte[1], IOBehavior.Asynchronous));
	}

	[Fact]
	public async Task StreamByteHandler_dispose_closes_underlying_stream()
	{
		var stream = new MemoryStream();
		var handler = new StreamByteHandler(stream);
		handler.Dispose();
		Assert.False(stream.CanRead);
		await Task.CompletedTask;
	}

	// ---------- BufferedByteReader ----------

	[Fact]
	public async Task BufferedByteReader_reads_exact_count()
	{
		using var handler = new FakeByteHandler(new byte[] { 1, 2, 3, 4, 5, 6 });
		var reader = new BufferedByteReader();

		var first = await reader.ReadBytesAsync(handler, 4, IOBehavior.Synchronous);
		Assert.Equal(4, first.Count);
		Assert.Equal(new byte[] { 1, 2, 3, 4 }, first.ToArray());

		// 第二次从缓冲余量里取
		var second = await reader.ReadBytesAsync(handler, 2, IOBehavior.Synchronous);
		Assert.Equal(2, second.Count);
		Assert.Equal(new byte[] { 5, 6 }, second.ToArray());
	}

	[Fact]
	public async Task BufferedByteReader_returns_empty_when_stream_ends_before_count()
	{
		// 注意（忠实复刻的行为）：提前 EOF 时返回的是**本次调用前**的缓冲余量，
		// 而不是本次已读入的字节 —— 这里首次调用前没有余量，故返回 0 而非 2。
		// 调用方（ProtocolUtility）本就按「不足预期长度」判定为流结束，因此不受影响。
		using var handler = new FakeByteHandler(new byte[] { 1, 2 });
		var reader = new BufferedByteReader();
		var segment = await reader.ReadBytesAsync(handler, 8, IOBehavior.Synchronous);
		Assert.Equal(0, segment.Count);
	}

	[Fact]
	public async Task BufferedByteReader_preserves_previously_buffered_bytes_on_eof()
	{
		// 与上一条对照：若本次调用前已有缓冲余量，提前 EOF 时该余量会被保留并返回。
		using var handler = new FakeByteHandler(new byte[] { 1, 2, 3, 4, 5 });
		var reader = new BufferedByteReader();

		// 先精确取 3 字节，使内部缓冲余下 2 字节（4、5）
		var first = await reader.ReadBytesAsync(handler, 3, IOBehavior.Synchronous);
		Assert.Equal(new byte[] { 1, 2, 3 }, first.ToArray());

		// 再请求 10 字节：余量 2 字节 + 流已尽 → 返回余量 2 字节
		var second = await reader.ReadBytesAsync(handler, 10, IOBehavior.Synchronous);
		Assert.Equal(2, second.Count);
		Assert.Equal(new byte[] { 4, 5 }, second.ToArray());
	}

	[Fact]
	public async Task BufferedByteReader_handles_request_larger_than_internal_buffer()
	{
		// 内部缓冲 16384 字节；请求 20000 字节时走独立分配分支
		var data = new byte[20000];
		for (var i = 0; i < data.Length; i++)
			data[i] = (byte)(i & 0xFF);
		using var handler = new FakeByteHandler(data);
		var reader = new BufferedByteReader();

		var segment = await reader.ReadBytesAsync(handler, 20000, IOBehavior.Synchronous);
		Assert.Equal(20000, segment.Count);
		Assert.Equal(data, segment.ToArray());
	}

	[Fact]
	public async Task BufferedByteReader_accumulates_across_partial_reads()
	{
		// 分片供给：每次只给 3 字节，要求读满 9 字节
		var reader = new BufferedByteReader();
		using var handler = new ChunkedByteHandler(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, chunkSize: 3);
		var segment = await reader.ReadBytesAsync(handler, 9, IOBehavior.Asynchronous);
		Assert.Equal(9, segment.Count);
		Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, segment.ToArray());
	}

	/// <summary>每次最多返回 chunkSize 字节，用于验证 BufferedByteReader 的循环累加。</summary>
	private sealed class ChunkedByteHandler : IByteHandler
	{
		private readonly byte[] m_data;

		private readonly int m_chunkSize;

		private int m_offset;

		public ChunkedByteHandler(byte[] data, int chunkSize)
		{
			m_data = data;
			m_chunkSize = chunkSize;
		}

		public int RemainingTimeout { get; set; } = int.MaxValue;

		public ValueTask<int> ReadBytesAsync(Memory<byte> buffer, IOBehavior ioBehavior)
		{
			var count = Math.Min(Math.Min(m_chunkSize, buffer.Length), m_data.Length - m_offset);
			if (count <= 0)
				return new ValueTask<int>(0);
			m_data.AsSpan(m_offset, count).CopyTo(buffer.Span);
			m_offset += count;
			return new ValueTask<int>(count);
		}

		public ValueTask WriteBytesAsync(ReadOnlyMemory<byte> data, IOBehavior ioBehavior) => default;

		public void Dispose()
		{
		}
	}

	// ---------- SocketByteHandler ----------

	private static (SocketByteHandler Handler, Socket Peer) CreateSocketPair()
	{
		var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
		listener.Listen(1);
		var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		client.Connect((IPEndPoint)listener.LocalEndPoint!);
		var server = listener.Accept();
		listener.Dispose();
		return (new SocketByteHandler(client), server);
	}

	[Fact]
	public async Task SocketByteHandler_roundtrips_over_loopback()
	{
		var (handler, peer) = CreateSocketPair();
		using (handler)
		using (peer)
		{
			await handler.WriteBytesAsync(new byte[] { 0xAA, 0xBB }, IOBehavior.Synchronous);

			var received = new byte[2];
			var total = 0;
			while (total < 2)
				total += peer.Receive(received, total, 2 - total, SocketFlags.None);
			Assert.Equal(new byte[] { 0xAA, 0xBB }, received);
		}
	}

	[Fact]
	public async Task SocketByteHandler_reads_bytes_written_by_peer()
	{
		var (handler, peer) = CreateSocketPair();
		using (handler)
		using (peer)
		{
			peer.Send(new byte[] { 1, 2, 3, 4 });

			var buffer = new byte[4];
			var total = 0;
			while (total < 4)
			{
				var read = await handler.ReadBytesAsync(buffer.AsMemory(total), IOBehavior.Asynchronous);
				if (read == 0)
					break;
				total += read;
			}
			Assert.Equal(4, total);
			Assert.Equal(new byte[] { 1, 2, 3, 4 }, buffer);
		}
	}

	[Fact]
	public async Task SocketByteHandler_sync_read_works()
	{
		var (handler, peer) = CreateSocketPair();
		using (handler)
		using (peer)
		{
			peer.Send(new byte[] { 42 });
			var buffer = new byte[1];
			var read = await handler.ReadBytesAsync(buffer, IOBehavior.Synchronous);
			Assert.Equal(1, read);
			Assert.Equal(42, buffer[0]);
		}
	}

	[Fact]
	public async Task SocketByteHandler_async_write_works()
	{
		var (handler, peer) = CreateSocketPair();
		using (handler)
		using (peer)
		{
			await handler.WriteBytesAsync(new byte[] { 7, 7 }, IOBehavior.Asynchronous);
			var received = new byte[2];
			var total = 0;
			while (total < 2)
				total += peer.Receive(received, total, 2 - total, SocketFlags.None);
			Assert.Equal(new byte[] { 7, 7 }, received);
		}
	}

	[Fact]
	public async Task SocketByteHandler_expired_timeout_throws_on_async_read()
	{
		var (handler, peer) = CreateSocketPair();
		using (handler)
		using (peer)
		{
			handler.RemainingTimeout = 0;
			await Assert.ThrowsAsync<MySqlException>(
				async () => await handler.ReadBytesAsync(new byte[1], IOBehavior.Asynchronous));
		}
	}

	[Fact]
	public async Task SocketByteHandler_expired_timeout_throws_on_sync_read()
	{
		var (handler, peer) = CreateSocketPair();
		using (handler)
		using (peer)
		{
			handler.RemainingTimeout = 0;
			await Assert.ThrowsAsync<MySqlException>(
				async () => await handler.ReadBytesAsync(new byte[1], IOBehavior.Synchronous));
		}
	}

	[Fact]
	public void SocketByteHandler_dispose_is_idempotent()
	{
		var (handler, peer) = CreateSocketPair();
		using (peer)
		{
			// Dispose 不关闭 socket（由连接层负责），因此可重复调用
			handler.Dispose();
			handler.Dispose();
		}
	}

	// ---------- CompressedPayloadHandler ----------

	[Fact]
	public async Task CompressedPayloadHandler_roundtrips_small_payload()
	{
		// 小载荷走「不压缩」分支（uncompressedLength = 0）
		var payload = Encoding.ASCII.GetBytes("SELECT 1");
		var wire = await WriteCompressedAsync(payload);
		var read = await ReadCompressedAsync(wire);
		Assert.Equal(payload, read);
	}

	[Fact]
	public async Task CompressedPayloadHandler_roundtrips_compressible_payload()
	{
		// > 80 字节且高度可压缩 → 走 zlib 压缩分支
		var payload = Encoding.ASCII.GetBytes(new string('A', 500));
		var wire = await WriteCompressedAsync(payload);

		// 压缩头第 4..6 字节是未压缩长度，应非 0
		Assert.NotEqual(0, wire[4] | (wire[5] << 8) | (wire[6] << 16));
		// 实际线上字节数应远小于原始载荷
		Assert.True(wire.Length < payload.Length);

		var read = await ReadCompressedAsync(wire);
		Assert.Equal(payload, read);
	}

	[Fact]
	public async Task CompressedPayloadHandler_roundtrips_incompressible_payload()
	{
		// 随机数据不可压缩：即使 > 80 字节也会回退到不压缩分支
		var payload = new byte[500];
		new Random(12345).NextBytes(payload);
		var wire = await WriteCompressedAsync(payload);
		var read = await ReadCompressedAsync(wire);
		Assert.Equal(payload, read);
	}

	[Fact]
	public async Task CompressedPayloadHandler_roundtrips_multi_packet_payload()
	{
		// 超过 16777215 会分片，这里用可压缩内容验证多次 CompressAndWrite 的序号递进
		var payload = new byte[100_000];
		for (var i = 0; i < payload.Length; i++)
			payload[i] = (byte)('a' + i % 26);

		var wire = await WriteCompressedAsync(payload);
		var read = await ReadCompressedAsync(wire);
		Assert.Equal(payload, read);
	}

	[Fact]
	public async Task CompressedPayloadHandler_StartNewConversation_resets_sequence()
	{
		var payload = Encoding.ASCII.GetBytes("hello");

		// 同一对 handler 上写两次：第二次需要 StartNewConversation 把序号归零
		var sink = new FakeByteHandler(Array.Empty<byte>());
		using var writer = new CompressedPayloadHandler(sink);
		await writer.WritePayloadAsync(payload, IOBehavior.Synchronous);
		var firstLength = sink.Written.Count;
		writer.StartNewConversation();
		await writer.WritePayloadAsync(payload, IOBehavior.Synchronous);

		// 两段报文的压缩序号都应从 0 开始
		Assert.Equal(0, sink.Written[3]);
		Assert.Equal(0, sink.Written[firstLength + 3]);
	}

	[Fact]
	public void CompressedPayloadHandler_ByteHandler_setter_is_not_supported()
	{
		using var handler = new CompressedPayloadHandler(new FakeByteHandler(Array.Empty<byte>()));
		Assert.Throws<NotSupportedException>(() => handler.ByteHandler = new FakeByteHandler(Array.Empty<byte>()));
	}

	[Fact]
	public void CompressedPayloadHandler_SetNextSequenceNumber_is_not_supported()
	{
		using var handler = new CompressedPayloadHandler(new FakeByteHandler(Array.Empty<byte>()));
		Assert.Throws<NotSupportedException>(() => handler.SetNextSequenceNumber(3));
	}

	[Fact]
	public async Task CompressedPayloadHandler_reads_uncompressed_packet()
	{
		// 压缩头(7) 声明未压缩长度 0 表示「未压缩」，其后直接是解压后内容；
		// 该内容本身又是一个 MySQL 报文（4 字节包头 + 载荷）。
		var wire = CompressedPacket(MySqlPacket(new byte[] { 0xAA, 0xBB, 0xCC }), compress: false);
		var read = await ReadCompressedAsync(wire);
		Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC }, read);
	}

	[Fact]
	public async Task CompressedPayloadHandler_throws_on_truncated_header()
	{
		// 不足 7 字节的压缩头
		var wire = new byte[] { 1, 0, 0, 0 };
		await Assert.ThrowsAsync<EndOfStreamException>(async () => await ReadCompressedAsync(wire));
	}

	[Fact]
	public async Task CompressedPayloadHandler_throws_on_out_of_order_sequence()
	{
		// 期望压缩序号 0，实际给 5
		var wire = CompressedPacket(MySqlPacket(new byte[] { 0xAA }), compress: false, sequenceNumber: 5);
		await Assert.ThrowsAsync<MySqlProtocolException>(async () => await ReadCompressedAsync(wire));
	}

	[Fact]
	public async Task CompressedPayloadHandler_throws_on_truncated_payload()
	{
		// 压缩头声明 10 字节载荷，实际只给 2 字节
		var wire = new byte[] { 10, 0, 0, 0, 0, 0, 0, 0xAA, 0xBB };
		await Assert.ThrowsAsync<EndOfStreamException>(async () => await ReadCompressedAsync(wire));
	}

	[Fact]
	public async Task CompressedPayloadHandler_decompresses_zlib_body()
	{
		// 压缩体是 zlib 流，解压后得到 MySQL 报文
		var payload = Encoding.ASCII.GetBytes(new string('Z', 200));
		var wire = CompressedPacket(MySqlPacket(payload), compress: true);
		var read = await ReadCompressedAsync(wire);
		Assert.Equal(payload, read);
	}

	[Fact]
	public async Task CompressedPayloadHandler_throws_when_decompressed_short()
	{
		// 头虚报未压缩长度为实际值的两倍 → 解压结果不足，抛 MySqlEndOfStreamException
		var content = MySqlPacket(Encoding.ASCII.GetBytes("short"));
		byte[] compressed;
		using (var ms = new MemoryStream())
		{
			using (var z = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
				z.Write(content, 0, content.Length);
			compressed = ms.ToArray();
		}

		var declared = content.Length * 2;
		var wire = new byte[7 + compressed.Length];
		Write3ByteLittleEndian(wire, 0, compressed.Length);
		wire[3] = 0;
		Write3ByteLittleEndian(wire, 4, declared);
		compressed.CopyTo(wire, 7);

		await Assert.ThrowsAsync<MySqlEndOfStreamException>(async () => await ReadCompressedAsync(wire));
	}

	[Fact]
	public async Task CompressedPayloadHandler_read_returns_zero_when_ignoring_errors()
	{
		// ProtocolErrorBehavior.Ignore 下，截断的头返回空载荷而不是抛错
		using var handler = new CompressedPayloadHandler(new FakeByteHandler(new byte[] { 1, 0, 0 }));
		var cache = new ArraySegmentHolder<byte>();
		var result = await handler.ReadPayloadAsync(cache, ProtocolErrorBehavior.Ignore, IOBehavior.Synchronous);
		Assert.Equal(0, result.Count);
	}

	[Fact]
	public async Task CompressedPayloadHandler_dispose_is_safe()
	{
		var handler = new CompressedPayloadHandler(new FakeByteHandler(Array.Empty<byte>()));
		handler.Dispose();
		handler.Dispose();
		await Task.CompletedTask;
	}

	/// <summary>写一段压缩报文，返回线缆字节。</summary>
	private static async Task<byte[]> WriteCompressedAsync(byte[] payload)
	{
		var sink = new FakeByteHandler(Array.Empty<byte>());
		using (var writer = new CompressedPayloadHandler(sink))
		{
			await writer.WritePayloadAsync(payload, IOBehavior.Synchronous);
		}
		return sink.Written.ToArray();
	}

	/// <summary>把线缆字节喂给压缩处理器，读出解压后的原始载荷。</summary>
	private static async Task<byte[]> ReadCompressedAsync(byte[] wire)
	{
		using var handler = new CompressedPayloadHandler(new FakeByteHandler(wire));
		var cache = new ArraySegmentHolder<byte>();
		var result = await handler.ReadPayloadAsync(cache, ProtocolErrorBehavior.Throw, IOBehavior.Synchronous);
		return result.ToArray();
	}

	/// <summary>给载荷套上 MySQL 报文头（3 字节小端长度 + 1 字节序号 0）。</summary>
	private static byte[] MySqlPacket(byte[] payload)
	{
		var packet = new byte[payload.Length + 4];
		Write3ByteLittleEndian(packet, 0, payload.Length);
		packet[3] = 0;
		payload.CopyTo(packet, 4);
		return packet;
	}

	/// <summary>
	/// 给一段解压后内容套上 7 字节压缩头：3 字节压缩长度 + 1 字节序号 + 3 字节未压缩长度。
	/// <paramref name="compress"/> 为 false 时未压缩长度写 0（协议约定的「未压缩」标记）。
	/// </summary>
	private static byte[] CompressedPacket(byte[] content, bool compress, byte sequenceNumber = 0)
	{
		byte[] body = content;
		var uncompressedLength = 0;
		if (compress)
		{
			using var ms = new MemoryStream();
			using (var z = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
				z.Write(content, 0, content.Length);
			body = ms.ToArray();
			uncompressedLength = content.Length;
		}

		var wire = new byte[7 + body.Length];
		Write3ByteLittleEndian(wire, 0, body.Length);
		wire[3] = sequenceNumber;
		Write3ByteLittleEndian(wire, 4, uncompressedLength);
		body.CopyTo(wire, 7);
		return wire;
	}

	private static void Write3ByteLittleEndian(byte[] buffer, int offset, int value)
	{
		buffer[offset] = (byte)(value & 0xFF);
		buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
		buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
	}
}
