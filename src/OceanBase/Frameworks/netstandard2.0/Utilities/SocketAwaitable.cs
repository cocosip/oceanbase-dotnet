using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace OceanBase.Utilities;

internal sealed class SocketAwaitable : INotifyCompletion
{
	private static readonly Action s_sentinel = delegate
	{
	};

	private Action? m_continuation;

	public bool IsCompleted => WasCompleted;

	internal bool WasCompleted { get; set; }

	internal SocketAsyncEventArgs EventArgs { get; }

	public SocketAwaitable(SocketAsyncEventArgs eventArgs)
	{
		EventArgs = eventArgs ?? throw new ArgumentNullException("eventArgs");
		eventArgs.Completed += delegate
		{
			(m_continuation ?? Interlocked.CompareExchange(ref m_continuation, s_sentinel, null))?.Invoke();
		};
	}

	public SocketAwaitable GetAwaiter()
	{
		return this;
	}

	public void OnCompleted(Action continuation)
	{
		if ((Delegate?)m_continuation == (Delegate?)s_sentinel || (Delegate?)Interlocked.CompareExchange(ref m_continuation, continuation, null) == (Delegate?)s_sentinel)
		{
			Task.Run(continuation);
		}
	}

	public void GetResult()
	{
		if (EventArgs.SocketError != SocketError.Success)
		{
			throw new SocketException((int)EventArgs.SocketError);
		}
	}

	internal void Reset()
	{
		WasCompleted = false;
		m_continuation = null;
	}
}
