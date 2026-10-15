using System.Collections.Generic;
using System.Threading;

namespace OceanBase.Core;

internal sealed class RoundRobinLoadBalancer : ILoadBalancer
{
	#if NET9_0_OR_GREATER
	#if NET9_0_OR_GREATER
	private readonly Lock m_lock;
	#else
	private readonly object m_lock;
	#endif
	#else
	private readonly object m_lock;
	#endif

	private uint m_counter;

	public RoundRobinLoadBalancer()
	{
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

	public IReadOnlyList<string> LoadBalance(IReadOnlyList<string> hosts)
	{
		int num;
		lock (m_lock)
		{
			num = (int)(m_counter++ % hosts.Count);
		}
		List<string> list = new List<string>(hosts.Count);
		for (int i = num; i < hosts.Count; i++)
		{
			list.Add(hosts[i]);
		}
		for (int j = 0; j < num; j++)
		{
			list.Add(hosts[j]);
		}
		return list;
	}
}
