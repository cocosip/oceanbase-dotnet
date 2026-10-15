using System;
using System.Text;

namespace OceanBase;

public sealed class MySqlHelper
{
	[Obsolete("Use MySqlConnection.ClearAllPools or MySqlConnection.ClearAllPoolsAsync")]
	public static void ClearConnectionPools()
	{
		MySqlConnection.ClearAllPools();
	}

	/// <summary>
	/// Escapes single and double quotes, and backslashes in <paramref name="value" />.
	/// </summary>
	public static string EscapeString(string value)
	{
		ArgumentNullException.ThrowIfNull(value, "value");
		StringBuilder stringBuilder = null;
		int num = -1;
		for (int i = 0; i < value.Length; i++)
		{
			char c = value[i];
			if ((c == '"' || c == '\'' || c == '\\') ? true : false)
			{
				if (stringBuilder == null)
				{
					stringBuilder = new StringBuilder();
				}
				stringBuilder.Append(value, num + 1, i - (num + 1));
				stringBuilder.Append('\\');
				stringBuilder.Append(value[i]);
				num = i;
			}
		}
		stringBuilder?.Append(value, num + 1, value.Length - (num + 1));
		return stringBuilder?.ToString() ?? value;
	}
}
