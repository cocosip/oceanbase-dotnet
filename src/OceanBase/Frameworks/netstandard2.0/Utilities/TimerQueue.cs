using System;
using System.Collections.Generic;
using System.Threading;

namespace OceanBase.Utilities;

internal sealed class TimerQueue
{
	private readonly struct Data(uint id, int time, Action action)
	{
		public uint Id { get; } = id;

		public int Time { get; } = time;

		public Action Action { get; } = action;
	}

	private readonly object m_lock;

	private readonly Timer m_timer;

	private readonly List<Data> m_timeoutActions;

	private uint m_counter;

	private bool m_isTimerEnabled;

	private int m_nextTimerTick;

	public static TimerQueue Instance { get; } = new TimerQueue();

	/// <summary>
	/// Adds a timer that will invoke <paramref name="action" /> in approximately <paramref name="delay" /> milliseconds.
	/// </summary>
	/// <param name="delay">The time (in milliseconds) to wait before invoking <paramref name="action" />.</param>
	/// <param name="action">The callback to invoke.</param>
	/// <returns>A timer ID that can be passed to <see cref="M:OceanBase.Utilities.TimerQueue.Remove(System.UInt32)" /> to cancel the timer.</returns>
	public uint Add(int delay, Action action)
	{
		ExceptionExtensions.ThrowIfNegative(delay, "delay");
		int tickCount = Environment.TickCount;
		lock (m_lock)
		{
			uint num = ++m_counter;
			if (num == 0)
			{
				num = ++m_counter;
			}
			int num2 = m_timeoutActions.Count;
			while (num2 > 0 && delay < m_timeoutActions[num2 - 1].Time - tickCount)
			{
				num2--;
			}
			int time = tickCount + delay;
			m_timeoutActions.Insert(num2, new Data(num, time, action));
			if (!m_isTimerEnabled || (num2 == 0 && m_nextTimerTick - tickCount > delay))
			{
				UnsafeSetTimer(delay);
			}
			return num;
		}
	}

	/// <summary>
	/// Cancels the timer with the specified ID.
	/// </summary>
	/// <param name="id">The timer ID (returned from <see cref="M:OceanBase.Utilities.TimerQueue.Add(System.Int32,System.Action)" />).</param>
	/// <returns><c>true</c> if the timer was removed; otherwise, <c>false</c>. This method will return <c>false</c> if the specified timer has already fired.</returns>
	public bool Remove(uint id)
	{
		lock (m_lock)
		{
			for (int i = 0; i < m_timeoutActions.Count; i++)
			{
				if (m_timeoutActions[i].Id == id)
				{
					m_timeoutActions.RemoveAt(i);
					return true;
				}
			}
		}
		return false;
	}

	private TimerQueue()
	{
		m_lock = new object();
		m_timer = new Timer(Callback, this, -1, -1);
		m_timeoutActions = new List<Data>();
	}

	private void Callback(object? obj)
	{
		List<Action> list = new List<Action>();
		lock (m_lock)
		{
			while (m_timeoutActions.Count > 0 && m_timeoutActions[0].Time - Environment.TickCount < 15)
			{
				list.Add(m_timeoutActions[0].Action);
				m_timeoutActions.RemoveAt(0);
			}
			if (m_timeoutActions.Count == 0)
			{
				UnsafeClearTimer();
			}
			else
			{
				int delay = Math.Max(250, m_timeoutActions[0].Time - Environment.TickCount);
				UnsafeSetTimer(delay);
			}
		}
		foreach (Action item in list)
		{
			item();
		}
	}

	private void UnsafeSetTimer(int delay)
	{
		m_nextTimerTick = Environment.TickCount + delay;
		m_isTimerEnabled = true;
		m_timer.Change(delay, -1);
	}

	private void UnsafeClearTimer()
	{
		m_nextTimerTick = 0;
		m_isTimerEnabled = false;
		m_timer.Change(-1, -1);
	}
}
