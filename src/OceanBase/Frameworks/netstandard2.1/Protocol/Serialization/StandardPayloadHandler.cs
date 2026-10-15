using System;
using System.Threading.Tasks;
using OceanBase.Utilities;

namespace OceanBase.Protocol.Serialization;

internal sealed class StandardPayloadHandler : IPayloadHandler, IDisposable
{
	private readonly Func<int> m_getNextSequenceNumber;

	private IByteHandler? m_byteHandler;

	private BufferedByteReader? m_bufferedByteReader;

	private byte m_sequenceNumber;

	public IByteHandler ByteHandler
	{
		get
		{
			if (m_byteHandler == null)
			{
				throw new ObjectDisposedException("StandardPayloadHandler");
			}
			return m_byteHandler;
		}
		set
		{
			IByteHandler? byteHandler = m_byteHandler;
			ExceptionExtensions.ThrowIfNull(value, "value");
			m_byteHandler = value;
			byteHandler?.Dispose();
			m_bufferedByteReader = new BufferedByteReader();
		}
	}

	public StandardPayloadHandler(IByteHandler byteHandler)
	{
		ByteHandler = byteHandler;
		m_getNextSequenceNumber = () => m_sequenceNumber++;
	}

	public void Dispose()
	{
		m_bufferedByteReader = null;
		Utility.Dispose(ref m_byteHandler);
	}

	public void StartNewConversation()
	{
		m_sequenceNumber = 0;
	}

	public void SetNextSequenceNumber(int sequenceNumber)
	{
		m_sequenceNumber = (byte)sequenceNumber;
	}

	public ValueTask<ArraySegment<byte>> ReadPayloadAsync(ArraySegmentHolder<byte> cache, ProtocolErrorBehavior protocolErrorBehavior, IOBehavior ioBehavior)
	{
		return ProtocolUtility.ReadPayloadAsync(m_bufferedByteReader, m_byteHandler, m_getNextSequenceNumber, cache, protocolErrorBehavior, ioBehavior);
	}

	public ValueTask WritePayloadAsync(ReadOnlyMemory<byte> payload, IOBehavior ioBehavior)
	{
		return ProtocolUtility.WritePayloadAsync(m_byteHandler, m_getNextSequenceNumber, payload, ioBehavior);
	}
}
