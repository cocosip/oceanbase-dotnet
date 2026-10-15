using System;
using System.Buffers.Text;
using System.Text;

namespace OceanBase.Core;

internal sealed class ServerVersion
{
	public string OriginalString { get; }

	public Version Version { get; }

	public bool IsMariaDb { get; }

	public static ServerVersion Empty { get; } = new ServerVersion();

	public ServerVersion(ReadOnlySpan<byte> versionString)
	{
		OriginalString = Encoding.ASCII.GetString(versionString);
		if (versionString.StartsWith("5.5.5-"u8))
		{
			versionString = versionString.Slice(6, versionString.Length - 6);
			IsMariaDb = true;
		}
		else if (versionString.IndexOf("MariaDB"u8) != -1)
		{
			IsMariaDb = true;
		}
		int value = 0;
		int value2 = 0;
		int value3 = 0;
		if (Utf8Parser.TryParse(versionString, out int value4, out int bytesConsumed, '\0'))
		{
			int num = bytesConsumed;
			versionString = versionString.Slice(num, versionString.Length - num);
			if (versionString.Length >= 1 && versionString[0] == 46)
			{
				versionString = versionString.Slice(1, versionString.Length - 1);
				if (Utf8Parser.TryParse(versionString, out value, out bytesConsumed, '\0'))
				{
					num = bytesConsumed;
					versionString = versionString.Slice(num, versionString.Length - num);
					if (versionString.Length >= 1 && versionString[0] == 46)
					{
						versionString = versionString.Slice(1, versionString.Length - 1);
						if (Utf8Parser.TryParse(versionString, out value2, out bytesConsumed, '\0'))
						{
							num = bytesConsumed;
							versionString = versionString.Slice(num, versionString.Length - num);
							if (versionString.Length >= 1 && versionString[0] == 46)
							{
								versionString = versionString.Slice(1, versionString.Length - 1);
								Utf8Parser.TryParse(versionString, out value3, out num, '\0');
							}
						}
					}
				}
			}
		}
		Version = ((value3 == 0) ? new Version(value4, value, value2) : new Version(value4, value, value2, value3));
	}

	private ServerVersion()
	{
		OriginalString = "";
		Version = new Version();
	}
}
