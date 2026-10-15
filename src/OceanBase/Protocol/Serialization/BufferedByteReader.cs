using System;
using System.Threading.Tasks;

namespace OceanBase.Protocol.Serialization;

internal sealed class BufferedByteReader
{
	private readonly byte[] m_buffer;

	private ArraySegment<byte> m_remainingData;

	public BufferedByteReader()
	{
		m_buffer = new byte[16384];
	}

	public ValueTask<ArraySegment<byte>> ReadBytesAsync(IByteHandler byteHandler, int count, IOBehavior ioBehavior)
	{
		if (m_remainingData.Count >= count)
		{
			ArraySegment<byte> result = m_remainingData.Slice(0, count);
			m_remainingData = m_remainingData.Slice(count);
			return new ValueTask<ArraySegment<byte>>(result);
		}
		byte[] array = ((count > m_buffer.Length) ? new byte[count] : m_buffer);
		if (m_remainingData.Count > 0)
		{
			Buffer.BlockCopy(m_remainingData.Array, m_remainingData.Offset, array, 0, m_remainingData.Count);
			m_remainingData = new ArraySegment<byte>(array, 0, m_remainingData.Count);
		}
		return ReadBytesAsync(byteHandler, new ArraySegment<byte>(array, m_remainingData.Count, array.Length - m_remainingData.Count), count, ioBehavior);
	}

	private async ValueTask<ArraySegment<byte>> ReadBytesAsync(IByteHandler byteHandler, ArraySegment<byte> buffer, int totalBytesToRead, IOBehavior ioBehavior)
	{
		int num2;
		while (true)
		{
			int num = await byteHandler.ReadBytesAsync(buffer, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
			if (num == 0)
			{
				ArraySegment<byte> remainingData = m_remainingData;
				m_remainingData = default(ArraySegment<byte>);
				return remainingData;
			}
			num2 = buffer.Offset + num;
			if (num2 >= totalBytesToRead)
			{
				break;
			}
			buffer = buffer.Slice(num);
		}
		ArraySegment<byte> arraySegment = new ArraySegment<byte>(buffer.Array, 0, num2);
		ArraySegment<byte> result = arraySegment.Slice(0, totalBytesToRead);
		m_remainingData = arraySegment.Slice(totalBytesToRead);
		return result;
	}
}
