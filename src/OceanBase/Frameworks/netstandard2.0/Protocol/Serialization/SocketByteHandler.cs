using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using OceanBase.Utilities;

namespace OceanBase.Protocol.Serialization;

internal sealed class SocketByteHandler : IByteHandler, IDisposable
{
	private readonly Socket m_socket;

	private readonly SocketAwaitable m_socketAwaitable;

	private readonly Action m_closeSocket;

	public int RemainingTimeout { get; set; }

	public SocketByteHandler(Socket socket)
	{
		m_socket = socket;
		m_socketAwaitable = new SocketAwaitable(new SocketAsyncEventArgs());
		m_closeSocket = socket.Dispose;
		RemainingTimeout = int.MaxValue;
	}

	public void Dispose()
	{
		m_socketAwaitable.EventArgs.Dispose();
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
		MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out ArraySegment<byte> segment);
		try
		{
			if (RemainingTimeout == int.MaxValue)
			{
				return new ValueTask<int>(m_socket.Receive(segment.Array, segment.Offset, segment.Count, SocketFlags.None));
			}
			while (RemainingTimeout > 0)
			{
				int tickCount = Environment.TickCount;
				if (m_socket.Poll(Math.Min(2147483, RemainingTimeout) * 1000, SelectMode.SelectRead))
				{
					int result = m_socket.Receive(segment.Array, segment.Offset, segment.Count, SocketFlags.None);
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
		SocketExtensions.SetBuffer(m_socketAwaitable.EventArgs, buffer);
		int bytesTransferred;
		try
		{
			await m_socket.ReceiveAsync(m_socketAwaitable);
			bytesTransferred = m_socketAwaitable.EventArgs.BytesTransferred;
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
		return bytesTransferred;
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
		SocketExtensions.SetBuffer(m_socketAwaitable.EventArgs, MemoryMarshal.AsMemory(data));
		await m_socket.SendAsync(m_socketAwaitable);
	}
}
