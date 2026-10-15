using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using OceanBase.Utilities;

namespace OceanBase.Protocol.Serialization;

internal sealed class SocketByteHandler : IByteHandler, IDisposable
{
	private readonly Socket m_socket;

	private readonly Action m_closeSocket;

	public int RemainingTimeout { get; set; }

	public SocketByteHandler(Socket socket)
	{
		m_socket = socket;
		m_closeSocket = socket.Dispose;
		RemainingTimeout = int.MaxValue;
	}

	public void Dispose()
	{
	}

	public ValueTask<int> ReadBytesAsync(Memory<byte> buffer, IOBehavior ioBehavior)
	{
		if (ioBehavior != IOBehavior.Asynchronous)
		{
			return DoReadBytesSync(buffer);
		}
		return DoReadBytesAsync(buffer);
	}

	private ValueTask<int> DoReadBytesSync(Memory<byte> buffer)
	{
		try
		{
			if (RemainingTimeout == int.MaxValue)
			{
				return new ValueTask<int>(m_socket.Receive(buffer.Span, SocketFlags.None));
			}
			while (RemainingTimeout > 0)
			{
				int tickCount = Environment.TickCount;
				if (m_socket.Poll(Math.Min(2147483, RemainingTimeout) * 1000, SelectMode.SelectRead))
				{
					int result = m_socket.Receive(buffer.Span, SocketFlags.None);
					RemainingTimeout -= Environment.TickCount - tickCount;
					return new ValueTask<int>(result);
				}
				RemainingTimeout -= Environment.TickCount - tickCount;
			}
			return ValueTaskExtensions.FromException<int>(MySqlException.CreateForTimeout());
		}
		catch (Exception exception)
		{
			return ValueTaskExtensions.FromException<int>(exception);
		}
	}

	private async ValueTask<int> DoReadBytesAsync(Memory<byte> buffer)
	{
		int startTime = ((RemainingTimeout != int.MaxValue) ? Environment.TickCount : 0);
		int remainingTimeout = RemainingTimeout;
		if (remainingTimeout <= 0)
		{
			throw MySqlException.CreateForTimeout();
		}
		uint num = ((remainingTimeout != int.MaxValue) ? TimerQueue.Instance.Add(RemainingTimeout, m_closeSocket) : 0u);
		uint timerId = num;
		int result;
		try
		{
			result = await m_socket.ReceiveAsync(buffer, SocketFlags.None).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (SocketException innerException)
		{
			if (RemainingTimeout != int.MaxValue)
			{
				RemainingTimeout -= Environment.TickCount - startTime;
				if (!TimerQueue.Instance.Remove(timerId))
				{
					throw MySqlException.CreateForTimeout(innerException);
				}
			}
			throw;
		}
		if (RemainingTimeout != int.MaxValue)
		{
			RemainingTimeout -= Environment.TickCount - startTime;
			if (!TimerQueue.Instance.Remove(timerId))
			{
				throw MySqlException.CreateForTimeout();
			}
		}
		return result;
	}

	public ValueTask WriteBytesAsync(ReadOnlyMemory<byte> data, IOBehavior ioBehavior)
	{
		if (ioBehavior == IOBehavior.Asynchronous)
		{
			return DoWriteBytesAsync(data);
		}
		try
		{
			m_socket.Send(data, SocketFlags.None);
			return default(ValueTask);
		}
		catch (Exception exception)
		{
			return ValueTaskExtensions.FromException(exception);
		}
	}

	private async ValueTask DoWriteBytesAsync(ReadOnlyMemory<byte> data)
	{
		await m_socket.SendAsync(data, SocketFlags.None).ConfigureAwait(continueOnCapturedContext: false);
	}
}
