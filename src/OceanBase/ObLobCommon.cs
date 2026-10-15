namespace OceanBase;

internal sealed class ObLobCommon
{
	public static class Flag
	{
		public const int isInit = 1;

		public const int isEmpty = 2;

		public const int inRow = 4;
	}

	private readonly int m_flags;

	public ObLobCommon(int flags)
	{
		m_flags = flags;
	}

	public bool IsInit()
	{
		return (m_flags & 1) != 0;
	}

	public bool InRow()
	{
		return (m_flags & 4) != 0;
	}
}
