using System.Collections.Generic;

namespace OceanBase.Core;

internal sealed class RoundRobinLoadBalancer : ILoadBalancer
{
	private readonly object m_lock;

	private uint m_counter;

	public RoundRobinLoadBalancer()
	{
		m_lock = new object();
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
