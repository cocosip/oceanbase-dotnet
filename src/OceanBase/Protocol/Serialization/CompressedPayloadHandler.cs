using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using OceanBase.Utilities;

namespace OceanBase.Protocol.Serialization;

internal sealed class CompressedPayloadHandler : IPayloadHandler, IDisposable
{
	private sealed class CompressedByteHandler : IByteHandler, IDisposable
	{
		private readonly CompressedPayloadHandler m_compressedPayloadHandler;

		private readonly ProtocolErrorBehavior m_protocolErrorBehavior;

		public int RemainingTimeout
		{
			get
			{
				return m_compressedPayloadHandler.ByteHandler.RemainingTimeout;
			}
			set
			{
				m_compressedPayloadHandler.ByteHandler.RemainingTimeout = value;
			}
		}

		public CompressedByteHandler(CompressedPayloadHandler compressedPayloadHandler, ProtocolErrorBehavior protocolErrorBehavior)
		{
			m_compressedPayloadHandler = compressedPayloadHandler;
			m_protocolErrorBehavior = protocolErrorBehavior;
		}

		public void Dispose()
		{
		}

		public ValueTask<int> ReadBytesAsync(Memory<byte> buffer, IOBehavior ioBehavior)
		{
			return m_compressedPayloadHandler.ReadBytesAsync(buffer, m_protocolErrorBehavior, ioBehavior);
		}

		public ValueTask WriteBytesAsync(ReadOnlyMemory<byte> data, IOBehavior ioBehavior)
		{
			throw new NotSupportedException();
		}
	}

	private readonly BufferedByteReader m_bufferedByteReader;

	private readonly BufferedByteReader m_compressedBufferedByteReader;

	private MemoryStream? m_uncompressedStream;

	private IByteHandler? m_uncompressedStreamByteHandler;

	private IByteHandler? m_byteHandler;

	private byte m_compressedSequenceNumber;

	private byte m_uncompressedSequenceNumber;

	private ArraySegment<byte> m_remainingData;

	private bool m_isContinuationPacket;

	public IByteHandler ByteHandler
	{
		get
		{
			return m_byteHandler;
		}
		set
		{
			throw new NotSupportedException();
		}
	}

	public CompressedPayloadHandler(IByteHandler byteHandler)
	{
		m_uncompressedStream = new MemoryStream();
		m_uncompressedStreamByteHandler = new StreamByteHandler(m_uncompressedStream);
		m_byteHandler = byteHandler;
		m_bufferedByteReader = new BufferedByteReader();
		m_compressedBufferedByteReader = new BufferedByteReader();
	}

	public void Dispose()
	{
		Utility.Dispose(ref m_byteHandler);
		Utility.Dispose(ref m_uncompressedStreamByteHandler);
		Utility.Dispose(ref m_uncompressedStream);
	}

	public void StartNewConversation()
	{
		m_compressedSequenceNumber = (m_uncompressedSequenceNumber = 0);
	}

	public void SetNextSequenceNumber(int sequenceNumber)
	{
		throw new NotSupportedException();
	}

	public ValueTask<ArraySegment<byte>> ReadPayloadAsync(ArraySegmentHolder<byte> cache, ProtocolErrorBehavior protocolErrorBehavior, IOBehavior ioBehavior)
	{
		using CompressedByteHandler byteHandler = new CompressedByteHandler(this, protocolErrorBehavior);
		return ProtocolUtility.ReadPayloadAsync(m_bufferedByteReader, byteHandler, () => -1, cache, protocolErrorBehavior, ioBehavior);
	}

	public async ValueTask WritePayloadAsync(ReadOnlyMemory<byte> payload, IOBehavior ioBehavior)
	{
		await ProtocolUtility.WritePayloadAsync(m_uncompressedStreamByteHandler, GetNextUncompressedSequenceNumber, payload, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
		if (m_uncompressedStream.Length != 0L)
		{
			if (!m_uncompressedStream.TryGetBuffer(out var buffer))
			{
				throw new InvalidOperationException("Couldn't get uncompressed stream buffer.");
			}
			await CompressAndWrite(buffer, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
			m_uncompressedStream.SetLength(0L);
		}
	}

	private async ValueTask<int> ReadBytesAsync(Memory<byte> buffer, ProtocolErrorBehavior protocolErrorBehavior, IOBehavior ioBehavior)
	{
		int num;
		if (m_remainingData.Count > 0)
		{
			num = Math.Min(m_remainingData.Count, buffer.Length);
			m_remainingData.AsSpan(0, num).CopyTo(buffer.Span);
			m_remainingData = m_remainingData.Slice(num);
			return num;
		}
		ArraySegment<byte> segment = await m_compressedBufferedByteReader.ReadBytesAsync(m_byteHandler, 7, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
		if (segment.Count < 7)
		{
			if (protocolErrorBehavior == ProtocolErrorBehavior.Ignore)
			{
				return 0;
			}
			throw new EndOfStreamException($"Wanted to read 7 bytes but only read {segment.Count:d} when reading compressed packet header");
		}
		int payloadLength = (int)SerializationUtility.ReadUInt32(segment.AsSpan(0, 3));
		byte b = segment.Array[segment.Offset + 3];
		int uncompressedLength = (int)SerializationUtility.ReadUInt32(segment.AsSpan(4, 3));
		byte nextCompressedSequenceNumber = GetNextCompressedSequenceNumber();
		if (b != nextCompressedSequenceNumber)
		{
			if (protocolErrorBehavior == ProtocolErrorBehavior.Ignore)
			{
				return 0;
			}
			throw MySqlProtocolException.CreateForPacketOutOfOrder(nextCompressedSequenceNumber, b);
		}
		if (!m_isContinuationPacket)
		{
			m_uncompressedSequenceNumber = b;
		}
		m_isContinuationPacket = payloadLength == 16777215 || uncompressedLength == 16777215;
		ArraySegment<byte> remainingData = await m_compressedBufferedByteReader.ReadBytesAsync(m_byteHandler, payloadLength, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
		if (remainingData.Count < payloadLength)
		{
			if (protocolErrorBehavior == ProtocolErrorBehavior.Ignore)
			{
				return 0;
			}
			throw new EndOfStreamException($"Wanted to read {payloadLength:d} bytes but only read {remainingData.Count:d} when reading compressed payload");
		}
		if (uncompressedLength == 0)
		{
			m_remainingData = remainingData;
		}
		else
		{
			byte[] array = new byte[uncompressedLength];
			using MemoryStream stream = new MemoryStream(remainingData.Array, remainingData.Offset, remainingData.Count);
			using ZLibStream zLibStream = new ZLibStream(stream, CompressionMode.Decompress);
			int num2 = zLibStream.ReadAtLeast(array, uncompressedLength, throwOnEndOfStream: false);
			if (num2 != uncompressedLength && protocolErrorBehavior == ProtocolErrorBehavior.Throw)
			{
				throw new MySqlEndOfStreamException(uncompressedLength, num2);
			}
			m_remainingData = new ArraySegment<byte>(array, 0, num2);
		}
		num = Math.Min(m_remainingData.Count, buffer.Length);
		m_remainingData.AsSpan(0, num).CopyTo(buffer.Span);
		m_remainingData = m_remainingData.Slice(num);
		return num;
	}

	private byte GetNextCompressedSequenceNumber()
	{
		return m_compressedSequenceNumber++;
	}

	private int GetNextUncompressedSequenceNumber()
	{
		return m_uncompressedSequenceNumber++;
	}

	private async ValueTask CompressAndWrite(ArraySegment<byte> remainingUncompressedData, IOBehavior ioBehavior)
	{
		int num = Math.Min(remainingUncompressedData.Count, 16777215);
		ArraySegment<byte> buffer = default(ArraySegment<byte>);
		if (num > 80)
		{
			using MemoryStream memoryStream = new MemoryStream();
			using (ZLibStream zLibStream = new ZLibStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
			{
				zLibStream.Write(remainingUncompressedData.Array, remainingUncompressedData.Offset, num);
			}
			if (!memoryStream.TryGetBuffer(out buffer))
			{
				throw new InvalidOperationException("Couldn't get compressed stream buffer.");
			}
		}
		uint value = (uint)num;
		if (buffer.Array == null || buffer.Count >= num)
		{
			value = 0u;
			buffer = remainingUncompressedData.Slice(0, num);
		}
		byte[] array = new byte[buffer.Count + 7];
		SerializationUtility.WriteUInt32((uint)buffer.Count, array, 0, 3);
		array[3] = GetNextCompressedSequenceNumber();
		SerializationUtility.WriteUInt32(value, array, 4, 3);
		Buffer.BlockCopy(buffer.Array, buffer.Offset, array, 7, buffer.Count);
		remainingUncompressedData = remainingUncompressedData.Slice(num);
		await m_byteHandler.WriteBytesAsync(new ArraySegment<byte>(array, 0, array.Length), ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
		if (remainingUncompressedData.Count != 0)
		{
			await CompressAndWrite(remainingUncompressedData, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
		}
	}
}
