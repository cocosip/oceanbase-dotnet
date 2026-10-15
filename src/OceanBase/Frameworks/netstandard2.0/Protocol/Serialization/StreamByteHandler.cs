using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using OceanBase.Utilities;

namespace OceanBase.Protocol.Serialization;

internal sealed class StreamByteHandler : IByteHandler, IDisposable
{
	private readonly Stream m_stream;

	private readonly Action m_closeStream;

	public int RemainingTimeout { get; set; }

	public StreamByteHandler(Stream stream)
	{
		m_stream = stream;
		m_closeStream = m_stream.Dispose;
		RemainingTimeout = int.MaxValue;
	}

	public void Dispose()
	{
		m_stream.Dispose();
	}

	public ValueTask<int> ReadBytesAsync(Memory<byte> buffer, IOBehavior ioBehavior)
	{
		if (RemainingTimeout > 0)
		{
			if (ioBehavior != IOBehavior.Asynchronous)
			{
				if (!m_stream.CanTimeout)
				{
					return DoReadBytesSyncOverAsync(buffer);
				}
				return DoReadBytesSync(buffer);
			}
			return new ValueTask<int>(DoReadBytesAsync(buffer));
		}
		return ValueTaskExtensions.FromException<int>(MySqlException.CreateForTimeout());
		async Task<int> DoReadBytesAsync(Memory<byte> buffer2)
		{
			int startTime = ((RemainingTimeout != int.MaxValue) ? Environment.TickCount : 0);
			uint timerId = ((RemainingTimeout != int.MaxValue) ? TimerQueue.Instance.Add(RemainingTimeout, m_closeStream) : 0u);
			int result;
			try
			{
				result = await Utility.ReadAsync(m_stream, buffer2).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (Exception ex) when (((ex is ObjectDisposedException || ex is IOException) ? 1 : 0) != 0)
			{
				if (RemainingTimeout != int.MaxValue)
				{
					RemainingTimeout -= Environment.TickCount - startTime;
					if (!TimerQueue.Instance.Remove(timerId))
					{
						throw MySqlException.CreateForTimeout(ex);
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
		ValueTask<int> DoReadBytesSync(Memory<byte> buffer2)
		{
			m_stream.ReadTimeout = ((RemainingTimeout == int.MaxValue) ? (-1) : RemainingTimeout);
			int num = ((RemainingTimeout != int.MaxValue) ? Environment.TickCount : 0);
			int result;
			try
			{
				result = m_stream.Read(buffer2);
			}
			catch (Exception ex)
			{
				return (RemainingTimeout != int.MaxValue && ex is IOException && ex.InnerException is SocketException { SocketErrorCode: SocketError.TimedOut }) ? ValueTaskExtensions.FromException<int>(MySqlException.CreateForTimeout(ex)) : ValueTaskExtensions.FromException<int>(ex);
			}
			if (RemainingTimeout != int.MaxValue)
			{
				RemainingTimeout -= Environment.TickCount - num;
			}
			return new ValueTask<int>(result);
		}
		ValueTask<int> DoReadBytesSyncOverAsync(Memory<byte> buffer2)
		{
			try
			{
				return new ValueTask<int>(DoReadBytesAsync(buffer2).GetAwaiter().GetResult());
			}
			catch (Exception exception)
			{
				return ValueTaskExtensions.FromException<int>(exception);
			}
		}
	}

	public ValueTask WriteBytesAsync(ReadOnlyMemory<byte> data, IOBehavior ioBehavior)
	{
		if (ioBehavior == IOBehavior.Asynchronous)
		{
			return DoWriteBytesAsync(data);
		}
		try
		{
			m_stream.Write(data);
			return default(ValueTask);
		}
		catch (Exception exception)
		{
			return ValueTaskExtensions.FromException(exception);
		}
		async ValueTask DoWriteBytesAsync(ReadOnlyMemory<byte> data2)
		{
			await Utility.WriteAsync(m_stream, data2).ConfigureAwait(continueOnCapturedContext: false);
		}
	}
}
