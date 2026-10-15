using System.Collections.Generic;

namespace OceanBase.Core;

internal interface ILoadBalancer
{
	/// <summary>
	/// Returns an <see cref="T:System.Collections.Generic.IEnumerable`1" /> containing <paramref name="hosts" /> in the order they
	/// should be tried to satisfy the load balancing policy.
	/// </summary>
	IReadOnlyList<string> LoadBalance(IReadOnlyList<string> hosts);
}
