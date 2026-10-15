using System;
using System.Globalization;

namespace OceanBase.Protocol;

internal static class OracleUdtTextScalars
{
	public static bool TryParseTimestamp(string element, out DateTime timestamp)
	{
		timestamp = default(DateTime);
		element = element.Trim();
		if (element.Length == 0 || string.Equals(element, "NULL", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (OracleUdtHexBytes.LooksLikeHexLiteral(element))
		{
			return false;
		}
		if (element.StartsWith("TIMESTAMP ", StringComparison.OrdinalIgnoreCase))
		{
			string text = element;
			int length = "TIMESTAMP ".Length;
			element = text.Substring(length, text.Length - length).Trim();
		}
		else if (element.StartsWith("TIMESTAMP'", StringComparison.OrdinalIgnoreCase))
		{
			string text = element;
			int length = "TIMESTAMP".Length;
			element = text.Substring(length, text.Length - length);
		}
		if (element.Length >= 2 && element[0] == '\'' && element[element.Length - 1] == '\'')
		{
			string text = element;
			element = text.Substring(1, text.Length - 1 - 1).Replace("''", "'");
		}
		if (DateTime.TryParseExact(element, "yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp))
		{
			return true;
		}
		if (DateTime.TryParseExact(element, "yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp))
		{
			return true;
		}
		if (DateTime.TryParseExact(element, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp))
		{
			return true;
		}
		if (DateTime.TryParseExact(element, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp))
		{
			return true;
		}
		return DateTime.TryParse(element, CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp);
	}

	public static bool TryParseIntervalYm(string element, out OracleIntervalYM interval)
	{
		interval = OracleIntervalYM.Null;
		element = element.Trim();
		if (element.Length == 0 || string.Equals(element, "NULL", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (OracleUdtHexBytes.LooksLikeHexLiteral(element))
		{
			if (OracleUdtHexBytes.TryCoerceToRawBytes(element, out byte[] bytes) && OracleUdtTdsDecoder.TryParseIntervalYmWireHex(bytes, out interval))
			{
				return true;
			}
			return false;
		}
		string text = ExtractIntervalPayload(element);
		if (text == null)
		{
			return false;
		}
		bool flag = text.Length > 0 && text[0] == '-';
		if (flag)
		{
			string text2 = text;
			text = text2.Substring(1, text2.Length - 1);
		}
		int num = text.IndexOf('-');
		if (num <= 0)
		{
			return false;
		}
		if (int.TryParse(text.Substring(0, num), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			string text2 = text;
			int num2 = num + 1;
			if (int.TryParse(text2.Substring(num2, text2.Length - num2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2))
			{
				interval = OracleIntervalYM.FromYearsMonths(result, result2, flag);
				return true;
			}
		}
		return false;
	}

	public static bool TryParseIntervalDs(string element, out OracleIntervalDS interval)
	{
		interval = OracleIntervalDS.Null;
		element = element.Trim();
		if (element.Length == 0 || string.Equals(element, "NULL", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (OracleUdtHexBytes.LooksLikeHexLiteral(element))
		{
			if (OracleUdtHexBytes.TryCoerceToRawBytes(element, out byte[] bytes) && OracleUdtTdsDecoder.TryParseIntervalDsWireHex(bytes, out interval))
			{
				return true;
			}
			return false;
		}
		string text = ExtractIntervalPayload(element);
		if (text == null)
		{
			return false;
		}
		try
		{
			interval = OracleIntervalDS.Parse(text);
			return !interval.IsNull;
		}
		catch (FormatException)
		{
			return false;
		}
	}

	private static string? ExtractIntervalPayload(string element)
	{
		element = element.Trim();
		if (element.Length == 0 || string.Equals(element, "NULL", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		if (element.StartsWith("INTERVAL ", StringComparison.OrdinalIgnoreCase))
		{
			string text = element;
			int length = "INTERVAL ".Length;
			element = text.Substring(length, text.Length - length).Trim();
			if (element.Length >= 2 && element[0] == '\'')
			{
				int num = element.IndexOf('\'', 1);
				if (num > 0)
				{
					element = element.Substring(1, num - 1).Replace("''", "'");
				}
			}
		}
		else if (element.Length >= 2 && element[0] == '\'')
		{
			string text2 = element;
			if (text2[text2.Length - 1] == '\'')
			{
				string text = element;
				element = text.Substring(1, text.Length - 1 - 1).Replace("''", "'");
			}
		}
		return element;
	}

	/// <summary>Normalizes a parsed STRUCT scalar after text UDT element parsing.</summary>
	internal static object? CoerceStructScalarValue(OracleUdtComplexScalarType scalarType, object? value)
	{
		if ((value == null || value is DBNull) ? true : false)
		{
			return value;
		}
		switch (scalarType)
		{
		case OracleUdtComplexScalarType.Raw:
		{
			if (!OracleUdtHexBytes.TryCoerceToRawBytes(value, out byte[] bytes))
			{
				return value;
			}
			return bytes;
		}
		case OracleUdtComplexScalarType.Timestamp:
			if (value is DateTime)
			{
				return value;
			}
			if (value is string text3)
			{
				if (string.IsNullOrWhiteSpace(text3) || OracleUdtHexBytes.LooksLikeHexLiteral(text3))
				{
					return DBNull.Value;
				}
				if (TryParseTimestamp(text3, out var timestamp))
				{
					return timestamp;
				}
			}
			return value;
		case OracleUdtComplexScalarType.IntervalYM:
			if (value is OracleIntervalYM)
			{
				return value;
			}
			if (value is string text2)
			{
				if (string.IsNullOrWhiteSpace(text2) || OracleUdtHexBytes.LooksLikeHexLiteral(text2))
				{
					return OracleIntervalYM.Null;
				}
				if (TryParseIntervalYm(text2, out var interval2))
				{
					return interval2;
				}
			}
			return value;
		case OracleUdtComplexScalarType.IntervalDS:
			if (value is OracleIntervalDS)
			{
				return value;
			}
			if (value is string text)
			{
				if (string.IsNullOrWhiteSpace(text) || OracleUdtHexBytes.LooksLikeHexLiteral(text))
				{
					return OracleIntervalDS.Null;
				}
				if (TryParseIntervalDs(text, out var interval))
				{
					return interval;
				}
			}
			return value;
		default:
			return value;
		}
	}
}
