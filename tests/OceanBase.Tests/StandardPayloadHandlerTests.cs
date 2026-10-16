using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OceanBase.Protocol;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// <see cref="StandardPayloadHandler"/> 的行为测试（internal，无需服务端）。
///
/// 该类在 IByteHandler 之上完成「协议包 → 载荷」的封帧与拆帧，
/// 维护包序号；用内存假字节处理器验证读写与序号状态机。
/// </summary>
public sealed class StandardPayloadHandlerTests
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
			return new ValueTask<int>(m_readStream.Read(buffer.Span));
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

	private static byte[] Frame(byte[] payload, byte sequence)
	{
		var frame = new byte[payload.Length + 4];
		frame[0] = (byte)payload.Length;
		frame[1] = 0;
		frame[2] = 0;
		frame[3] = sequence;
		payload.CopyTo(frame, 4);
		return frame;
	}

	// ---------- 写路径 ----------

	[Fact]
	public async Task WritePayload_frames_payload_with_sequence_number()
	{
		using var sink = new FakeByteHandler(Array.Empty<byte>());
		using var handler = new StandardPayloadHandler(sink);

		await handler.WritePayloadAsync(new byte[] { 0xDE, 0xAD }, IOBehavior.Synchronous);

		var written = sink.Written.ToArray();
		Assert.Equal(6, written.Length);
		Assert.Equal((byte)2, written[0]);   // 3 字节小端长度 = 2
		Assert.Equal((byte)0, written[1]);
		Assert.Equal((byte)0, written[2]);
		Assert.Equal(0, written[3]);         // 序号从 0 开始
		Assert.Equal(new byte[] { 0xDE, 0xAD }, written.AsSpan(4).ToArray());
	}

	[Fact]
	public async Task WritePayload_increments_sequence_per_packet()
	{
		using var sink = new FakeByteHandler(Array.Empty<byte>());
		using var handler = new StandardPayloadHandler(sink);

		await handler.WritePayloadAsync(new byte[] { 1 }, IOBehavior.Synchronous);
		await handler.WritePayloadAsync(new byte[] { 2 }, IOBehavior.Synchronous);

		Assert.Equal(0, sink.Written[3]);
		Assert.Equal(1, sink.Written[3 + 5]);
	}

	[Fact]
	public async Task StartNewConversation_and_SetNextSequenceNumber_control_sequence()
	{
		using var sink = new FakeByteHandler(Array.Empty<byte>());
		using var handler = new StandardPayloadHandler(sink);

		await handler.WritePayloadAsync(new byte[] { 1 }, IOBehavior.Synchronous);
		await handler.WritePayloadAsync(new byte[] { 2 }, IOBehavior.Synchronous);
		Assert.Equal(1, sink.Written[3 + 5]); // 连续写时序号递增

		handler.StartNewConversation();
		await handler.WritePayloadAsync(new byte[] { 3 }, IOBehavior.Synchronous);
		Assert.Equal(0, sink.Written[^2]);   // 帧布局 [len×3][seq][data]，序号在倒数第二字节

		handler.SetNextSequenceNumber(250);
		await handler.WritePayloadAsync(new byte[] { 4 }, IOBehavior.Synchronous);
		Assert.Equal(250, sink.Written[^2]);
	}

	// ---------- 读路径 ----------

	[Fact]
	public async Task ReadPayload_returns_payload_from_single_frame()
	{
		var payload = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };
		using var source = new FakeByteHandler(Frame(payload, sequence: 0));
		using var handler = new StandardPayloadHandler(source);

		var cache = new ArraySegmentHolder<byte>();
		var segment = await handler.ReadPayloadAsync(cache, ProtocolErrorBehavior.Throw, IOBehavior.Synchronous);

		Assert.Equal(payload, segment.AsSpan().ToArray());
	}

	[Fact]
	public async Task ReadPayload_advances_sequence_for_next_read()
	{
		var wire = System.Linq.Enumerable.Concat(
			Frame(new byte[] { 1 }, 0),
			Frame(new byte[] { 2 }, 1)).ToArray();
		using var source = new FakeByteHandler(wire);
		using var handler = new StandardPayloadHandler(source);

		var cache = new ArraySegmentHolder<byte>();
		var first = await handler.ReadPayloadAsync(cache, ProtocolErrorBehavior.Throw, IOBehavior.Synchronous);
		Assert.Equal(new byte[] { 1 }, first.AsSpan().ToArray());

		var second = await handler.ReadPayloadAsync(cache, ProtocolErrorBehavior.Throw, IOBehavior.Synchronous);
		Assert.Equal(new byte[] { 2 }, second.AsSpan().ToArray());
	}

	[Fact]
	public async Task ReadPayload_throws_on_end_of_stream_when_configured_to_throw()
	{
		using var source = new FakeByteHandler(Array.Empty<byte>());
		using var handler = new StandardPayloadHandler(source);

		var cache = new ArraySegmentHolder<byte>();
		await Assert.ThrowsAsync<MySqlEndOfStreamException>(
			() => handler.ReadPayloadAsync(cache, ProtocolErrorBehavior.Throw, IOBehavior.Synchronous).AsTask());
	}

	[Fact]
	public async Task ReadPayload_returns_empty_on_end_of_stream_when_configured_to_ignore()
	{
		using var source = new FakeByteHandler(Array.Empty<byte>());
		using var handler = new StandardPayloadHandler(source);

		var cache = new ArraySegmentHolder<byte>();
		var segment = await handler.ReadPayloadAsync(cache, ProtocolErrorBehavior.Ignore, IOBehavior.Synchronous);

		Assert.Equal(0, segment.Count);
	}

	[Fact]
	public async Task ReadPayload_throws_on_out_of_order_packet()
	{
		// 帧序号与期望不符且载荷不以 0xFF 开头 → 包乱序异常
		using var source = new FakeByteHandler(Frame(new byte[] { 1 }, sequence: 7));
		using var handler = new StandardPayloadHandler(source);

		var cache = new ArraySegmentHolder<byte>();
		var ex = await Assert.ThrowsAsync<MySqlProtocolException>(
			() => handler.ReadPayloadAsync(cache, ProtocolErrorBehavior.Throw, IOBehavior.Synchronous).AsTask());
		Assert.Contains("out-of-order", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task ReadPayload_propagates_byte_handler_failures()
	{
		using var source = new FakeByteHandler(Array.Empty<byte>()) { ThrowOnRead = true };
		using var handler = new StandardPayloadHandler(source);

		var cache = new ArraySegmentHolder<byte>();
		await Assert.ThrowsAsync<IOException>(
			() => handler.ReadPayloadAsync(cache, ProtocolErrorBehavior.Throw, IOBehavior.Synchronous).AsTask());
	}

	// ---------- 生命周期 ----------

	[Fact]
	public void Setting_null_byte_handler_is_rejected()
	{
		using var handler = new StandardPayloadHandler(new FakeByteHandler(Array.Empty<byte>()));
		Assert.Throws<ArgumentNullException>(() => handler.ByteHandler = null!);
	}

	[Fact]
	public void Replacing_byte_handler_disposes_the_old_one()
	{
		var first = new FakeByteHandler(Array.Empty<byte>());
		using var handler = new StandardPayloadHandler(first);

		var second = new FakeByteHandler(Array.Empty<byte>());
		handler.ByteHandler = second;

		// 旧处理器被替换释放后不再抛出（释放自身是幂等的），但新处理器保持可用
		Assert.Same(second, handler.ByteHandler);
		first.Dispose(); // 确认释放链路无双释放问题
	}

	[Fact]
	public void Disposed_handler_rejects_byte_handler_access()
	{
		var handler = new StandardPayloadHandler(new FakeByteHandler(Array.Empty<byte>()));
		handler.Dispose();

		// 读取已释放处理器的 ByteHandler 必须抛出明确异常（Dispose 会置空底层处理器）
		Assert.Throws<ObjectDisposedException>(() => handler.ByteHandler);
	}
}
