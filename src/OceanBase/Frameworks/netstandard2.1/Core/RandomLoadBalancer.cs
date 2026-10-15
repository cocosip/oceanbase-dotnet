using System;
using System.Collections.Generic;

namespace OceanBase.Core;

internal sealed class RandomLoadBalancer : ILoadBalancer
{
	private readonly Random m_random;

	private readonly object m_lock;

	public static ILoadBalancer Instance { get; } = new RandomLoadBalancer();

	public IReadOnlyList<string> LoadBalance(IReadOnlyList<string> hosts)
	{
		List<string> list = new List<string>(hosts);
		for (int num = hosts.Count - 1; num >= 1; num--)
		{
			int num2;
			lock (m_lock)
			{
				num2 = m_random.Next(num + 1);
			}
			if (num != num2)
			{
				List<string> list2 = list;
				int index = num2;
				List<string> list3 = list;
				int index2 = num;
				string value = list[num];
				string value2 = list[num2];
				list2[index] = value;
				list3[index2] = value2;
			}
		}
		return list;
	}

	private RandomLoadBalancer()
	{
		m_random = new Random();
		m_lock = new object();
	}
}
