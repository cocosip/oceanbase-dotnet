using System.Collections.Generic;

namespace OceanBase.Core;

internal sealed class FailOverLoadBalancer : ILoadBalancer
{
	public static ILoadBalancer Instance { get; } = new FailOverLoadBalancer();

	public IReadOnlyList<string> LoadBalance(IReadOnlyList<string> hosts)
	{
		return hosts;
	}

	private FailOverLoadBalancer()
	{
	}
}
