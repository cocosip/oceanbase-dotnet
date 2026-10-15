using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OceanBase.Core;

internal sealed class RandomLoadBalancer : ILoadBalancer
{
	private readonly Random m_random;

	#if NET9_0_OR_GREATER

	#if NET9_0_OR_GREATER

	private readonly Lock m_lock;

	#else

	private readonly object m_lock;

	#endif

	#else

	private readonly object m_lock;

	#endif

	public static ILoadBalancer Instance { get; } = new RandomLoadBalancer();

	public IReadOnlyList<string> LoadBalance(IReadOnlyList<string> hosts)
	{
		string[] array = hosts.ToArray();
		lock (m_lock)
		{
			m_random.Shuffle(array);
			return array;
		}
	}

	private RandomLoadBalancer()
	{
		m_random = new Random();
		#if NET9_0_OR_GREATER
		#if NET9_0_OR_GREATER
		m_lock = new Lock();
		#else
		m_lock = new object();
		#endif
		#else
		m_lock = new object();
		#endif
	}
}
