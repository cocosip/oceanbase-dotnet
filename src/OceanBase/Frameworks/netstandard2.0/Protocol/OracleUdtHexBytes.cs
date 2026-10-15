using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace OceanBase.Protocol;

internal static class OracleUdtHexBytes
{
	public static bool TryParseHexRawElement(string element, [NotNullWhen(true)] out byte[]? bytes)
	{
		bytes = null;
		element = element.Trim();
		if (element.StartsWith("hextoraw(", StringComparison.OrdinalIgnoreCase) && element.EndsWith(")", StringComparison.Ordinal))
		{
			string text = element.Substring("hextoraw(".Length, element.Length - "hextoraw(".Length - 1).Trim();
			if (text.Length >= 2 && text[0] == '\'' && text[text.Length - 1] == '\'')
			{
				return TryDecodeHex(UnescapeQuoted(text.Substring(1, text.Length - 2)), out bytes);
			}
			return false;
		}
		if (element.Length >= 2 && element[0] == '\'' && element[element.Length - 1] == '\'')
		{
			return TryDecodeHex(UnescapeQuoted(element.Substring(1, element.Length - 2)), out bytes);
		}
		return TryDecodeHex(element, out bytes);
	}

	/// <summary>
	/// Decodes hex after stripping non-hex characters (OceanBase text UDT literals may embed punctuation).
	/// </summary>
	public static bool TryDecodeHexDigitsOnly(string text, [NotNullWhen(true)] out byte[]? bytes)
	{
		bytes = null;
		if (string.IsNullOrEmpty(text))
		{
			bytes = Array.Empty<byte>();
			return true;
		}
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		foreach (char c in text)
		{
			if (FromHexDigit(c) >= 0)
			{
				stringBuilder.Append(c);
			}
		}
		if (stringBuilder.Length == 0)
		{
			return false;
		}
		return TryDecodeHex(stringBuilder.ToString(), out bytes);
	}

	public static bool LooksLikeHexLiteral(string element)
	{
		element = element.Trim();
		if (element.StartsWith("hextoraw(", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (element.Length >= 2 && element[0] == '\'' && element[element.Length - 1] == '\'')
		{
			element = element.Substring(1, element.Length - 2).Trim();
		}
		if (element.Length < 2)
		{
			return false;
		}
		byte[] bytes;
		return TryDecodeHexDigitsOnly(element, out bytes);
	}

	public static bool TryCoerceToRawBytes(object? value, [NotNullWhen(true)] out byte[]? bytes)
	{
		bytes = null;
		if ((value == null || value is DBNull) ? true : false)
		{
			return false;
		}
		if (value is byte[] array)
		{
			bytes = array;
			return true;
		}
		if (!(value is string text))
		{
			return false;
		}
		if (TryParseHexRawElement(text, out bytes))
		{
			return true;
		}
		string text2 = text.Trim().Trim(new char[1] { '\'' });
		if (TryDecodeHex(text2, out bytes))
		{
			return true;
		}
		return TryDecodeHexDigitsOnly(text2, out bytes);
	}

	public static bool TryDecodeHex(string hex, [NotNullWhen(true)] out byte[]? bytes)
	{
		bytes = null;
		hex = hex.Trim();
		if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			string text = hex;
			hex = text.Substring(2, text.Length - 2);
		}
		if (hex.Length == 0)
		{
			bytes = Array.Empty<byte>();
			return true;
		}
		if ((hex.Length & 1) != 0)
		{
			if (!IsAllHexDigits(hex))
			{
				return false;
			}
			hex = "0" + hex;
		}
		byte[] array = new byte[hex.Length / 2];
		for (int i = 0; i < array.Length; i++)
		{
			int num = FromHexDigit(hex[2 * i]);
			int num2 = FromHexDigit(hex[2 * i + 1]);
			if (num < 0 || num2 < 0)
			{
				return false;
			}
			array[i] = (byte)((num << 4) | num2);
		}
		bytes = array;
		return true;
	}

	private static bool IsAllHexDigits(string hex)
	{
		for (int i = 0; i < hex.Length; i++)
		{
			if (FromHexDigit(hex[i]) < 0)
			{
				return false;
			}
		}
		return true;
	}

	private static string UnescapeQuoted(string quoted)
	{
		return quoted.Replace("''", "'");
	}

	private static int FromHexDigit(char c)
	{
		switch (c)
		{
		case '0':
		case '1':
		case '2':
		case '3':
		case '4':
		case '5':
		case '6':
		case '7':
		case '8':
		case '9':
			return c - 48;
		case 'A':
		case 'B':
		case 'C':
		case 'D':
		case 'E':
		case 'F':
			return c - 65 + 10;
		case 'a':
		case 'b':
		case 'c':
		case 'd':
		case 'e':
		case 'f':
			return c - 97 + 10;
		default:
			return -1;
		}
	}
}
