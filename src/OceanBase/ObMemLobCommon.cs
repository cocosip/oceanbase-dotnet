namespace OceanBase;

internal sealed class ObMemLobCommon
{
	public static class Flag
	{
		public const int readOnly = 1;

		public const int hasInRowData = 2;

		public const int isOpen = 4;

		public const int isSimple = 8;

		public const int hasExtern = 16;
	}

	private long Magic { get; }

	private byte Version { get; }

	private byte Type { get; }

	private int Flags { get; }

	public ObMemLobCommon(long magicCode, long version)
	{
		Magic = magicCode;
		Version = (byte)(version & 0xFF);
		Type = (byte)((version >> 8) & 0xF);
		Flags = (int)((version >> 12) & 0xFFFFF);
	}

	public bool HasInrowData()
	{
		return (Flags & 2) == 2;
	}

	public bool IsSimple()
	{
		return (Flags & 8) == 8;
	}

	public bool HasExtern()
	{
		return (Flags & 0x10) == 16;
	}
}
