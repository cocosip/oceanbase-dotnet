using System.Threading;

namespace OceanBase.Core;

internal sealed class NonPooledConnectionPoolMetadata : IConnectionPoolMetadata
{
	private int m_lastId;

	public static IConnectionPoolMetadata Instance { get; } = new NonPooledConnectionPoolMetadata();

	public ConnectionPool? ConnectionPool => null;

	public int Id => 0;

	public int Generation => 0;

	public int GetNewSessionId()
	{
		return Interlocked.Increment(ref m_lastId);
	}
}
