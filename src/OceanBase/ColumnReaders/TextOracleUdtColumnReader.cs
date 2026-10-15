using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using OceanBase.Protocol;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

/// <summary>
/// Text-protocol reader for Oracle UDT columns (OBJECT/ARRAY/STRUCT).
/// OceanBase Oracle mode returns UDT values as constructor literals such as
/// <c>TYPE_NAME(attr1, attr2)</c> or <c>SCHEMA.TYPE_NAME(attr1, attr2)</c>.
/// This reader parses the literal into an <see cref="T:OceanBase.OracleUdtValue" />.
/// </summary>
internal sealed class TextOracleUdtColumnReader : ColumnReader
{
	private readonly bool m_isArray;

	private readonly Encoding m_encoding;

	public TextOracleUdtColumnReader(bool isArray, Encoding? encoding = null)
	{
		m_isArray = isArray;
		m_encoding = encoding ?? Encoding.UTF8;
	}

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		string text = m_encoding.GetString(data);
		bool isArray = OracleUdt.ShouldDecodeAsArray(columnDefinition.ComplexTypeName ?? columnDefinition.SchemaName ?? string.Empty, m_isArray);
		if (!TryParseUdtLiteral(text, isArray, out OracleUdtValue udtValue))
		{
			throw new FormatException("Unable to parse Oracle UDT text value: " + text);
		}
		return ((object)udtValue) ?? ((object)DBNull.Value);
	}

	internal static bool TryParseUdtLiteral(string literal, bool isArray, [NotNullWhen(true)] out OracleUdtValue? udtValue)
	{
		udtValue = null;
		literal = literal.Trim();
		if (literal.Length == 0)
		{
			return false;
		}
		if (string.Equals(literal, "NULL", StringComparison.OrdinalIgnoreCase))
		{
			udtValue = new OracleUdtValue("", isArray: false, Array.Empty<object>());
			return true;
		}
		int num = literal.IndexOf('(');
		if (num < 0)
		{
			return false;
		}
		string text = literal;
		if (text[text.Length - 1] != ')')
		{
			return false;
		}
		string typeName = literal.Substring(0, num).Trim();
		string text2 = literal;
		int num2 = num + 1;
		List<string> elements = SplitElements(NormalizeOceanBaseUdtTextBody(text2.Substring(num2, text2.Length - 1 - num2)));
		bool flag = isArray || OracleUdt.ShouldDecodeAsArray(typeName, wireSaysArray: false);
		if (!flag)
		{
			RepairOceanBaseTextStructElements(typeName, elements);
		}
		elements = ReconcileElements(typeName, flag, elements);
		int? num3 = OracleUdt.TryGetRegisteredAttributeCount(typeName);
		elements = NormalizeStructElementCount(num3, elements);
		int valueCount = num3 ?? elements.Count;
		IReadOnlyList<OracleUdtComplexScalarType> readOnlyList = (flag ? null : OracleUdtComplexScalarInference.InferStructAttributeTypes(typeName, valueCount));
		List<object> list = new List<object>();
		for (int i = 0; i < elements.Count; i++)
		{
			OracleUdtComplexScalarType scalarType = ((readOnlyList != null && i < readOnlyList.Count) ? readOnlyList[i] : OracleUdtComplexScalarType.Unknown);
			if (!TryParseElement(elements[i], scalarType, out object value))
			{
				return false;
			}
			list.Add(value);
		}
		if (readOnlyList != null)
		{
			for (int j = 0; j < list.Count && j < readOnlyList.Count; j++)
			{
				list[j] = OracleUdtTextScalars.CoerceStructScalarValue(readOnlyList[j], list[j]);
			}
		}
		udtValue = new OracleUdtValue(typeName, flag, list);
		return true;
	}

	private static string NormalizeOceanBaseUdtTextBody(string body)
	{
		if (body.AsSpan().IndexOfAny("\r\n".AsSpan()) < 0)
		{
			return body;
		}
		StringBuilder stringBuilder = new StringBuilder(body.Length);
		int num = 0;
		bool inString = false;
		for (int i = 0; i < body.Length; i++)
		{
			char c = body[i];
			if (c == '\'')
			{
				int num2 = i;
				AdvanceQuotedStringDelimiter(body.AsSpan(), ref i, ref inString, num);
				stringBuilder.Append(body, num2, i - num2 + 1);
			}
			else if (inString)
			{
				stringBuilder.Append(c);
			}
			else if (c == '\r')
			{
				if (i + 1 < body.Length && body[i + 1] == '\n')
				{
					i++;
				}
			}
			else if (c != '\n')
			{
				switch (c)
				{
				case '(':
					num++;
					break;
				case ')':
					num--;
					break;
				}
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	private static List<string> ReconcileElements(string typeName, bool isArray, List<string> elements)
	{
		if (isArray || OracleUdt.ShouldDecodeAsArray(typeName, wireSaysArray: false) || elements.Count <= 1)
		{
			return elements;
		}
		int? num = OracleUdt.TryGetRegisteredAttributeCount(typeName);
		bool flag = ((!num.HasValue || num.GetValueOrDefault() < 2) ? true : false);
		if (flag || elements.Count <= num)
		{
			return elements;
		}
		IReadOnlyList<OracleUdtComplexScalarType> readOnlyList = OracleUdtComplexScalarInference.InferStructAttributeTypes(typeName, num.Value);
		List<string> list = new List<string>(elements);
		while (list.Count > num.Value)
		{
			bool flag2 = false;
			for (int i = 0; i < list.Count - 1 && i < readOnlyList.Count; i++)
			{
				if (CanMergeSplitTextAttribute(readOnlyList[i]) && (readOnlyList[i] != OracleUdtComplexScalarType.Timestamp || !OracleUdtHexBytes.LooksLikeHexLiteral(list[i + 1])))
				{
					list[i] = list[i] + "," + list[i + 1];
					list.RemoveAt(i + 1);
					flag2 = true;
					break;
				}
			}
			if (!flag2)
			{
				int num2 = list.Count - num.Value + 1;
				List<string> list2 = new List<string>(num.Value);
				list2.Add(list[0]);
				list2.Add(string.Join(",", list.Skip(1).Take(num2)));
				list2.AddRange(list.Skip(1 + num2));
				return list2;
			}
		}
		return list;
	}

	private static bool CanMergeSplitTextAttribute(OracleUdtComplexScalarType scalarType)
	{
		switch (scalarType)
		{
		case OracleUdtComplexScalarType.Unknown:
		case OracleUdtComplexScalarType.Varchar2:
		case OracleUdtComplexScalarType.Timestamp:
		case OracleUdtComplexScalarType.IntervalYM:
		case OracleUdtComplexScalarType.IntervalDS:
			return true;
		default:
			return false;
		}
	}

	private static List<string> NormalizeStructElementCount(int? expectedCount, List<string> elements)
	{
		bool flag = ((!expectedCount.HasValue || expectedCount.GetValueOrDefault() < 1) ? true : false);
		if (flag || elements.Count == expectedCount)
		{
			return elements;
		}
		if (elements.Count > expectedCount)
		{
			while (elements.Count > expectedCount)
			{
				if (string.IsNullOrWhiteSpace(elements[elements.Count - 1]))
				{
					elements.RemoveAt(elements.Count - 1);
					continue;
				}
				break;
			}
		}
		while (elements.Count < expectedCount)
		{
			elements.Add(string.Empty);
		}
		return elements;
	}

	/// <summary>
	/// Splits the parenthesized body of a UDT constructor literal into argument spans.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Delimiter rules are type-agnostic: commas at parenthesis depth 0 separate arguments;
	/// commas inside single-quoted VARCHAR literals do not split (e.g. <c>'a,b'</c>).
	/// </para>
	/// <para>
	/// Quoted-string rules follow Oracle SQL (<c>''</c> → one apostrophe in the payload).
	/// OceanBase text result rows sometimes deviate (undoubled interior quotes, or <c>''</c>
	/// immediately before the next argument comma); <see cref="M:OceanBase.ColumnReaders.TextOracleUdtColumnReader.AdvanceQuotedStringDelimiter(System.ReadOnlySpan{System.Char},System.Int32@,System.Boolean@,System.Int32)" />
	/// applies those as format rules on the wire text, not per-type or per-element-count logic.
	/// </para>
	/// </remarks>
	private static List<string> SplitElements(string body)
	{
		List<string> list = new List<string>();
		if (string.IsNullOrWhiteSpace(body))
		{
			return list;
		}
		int num = 0;
		bool inString = false;
		int num2 = 0;
		for (int i = 0; i < body.Length; i++)
		{
			char c = body[i];
			if (c == '\'')
			{
				AdvanceQuotedStringDelimiter(body.AsSpan(), ref i, ref inString, num);
			}
			else
			{
				if (inString)
				{
					continue;
				}
				switch (c)
				{
				case '(':
					num++;
					break;
				case ')':
					num--;
					break;
				case ',':
					if (num == 0)
					{
						int num3 = num2;
						list.Add(body.Substring(num3, i - num3));
						num2 = i + 1;
					}
					break;
				}
			}
		}
		if (num2 <= body.Length)
		{
			int num3 = num2;
			list.Add(body.Substring(num3, body.Length - num3));
		}
		return list;
	}

	/// <summary>
	/// Updates <paramref name="inString" /> when <paramref name="body" />[<paramref name="index" />] is <c>'</c>.
	/// </summary>
	private static void AdvanceQuotedStringDelimiter(ReadOnlySpan<char> body, ref int index, ref bool inString, int parenDepth)
	{
		if (!inString)
		{
			inString = true;
		}
		else if (index + 1 < body.Length && body[index + 1] == '\'')
		{
			if (parenDepth == 0 && index + 2 < body.Length && body[index + 2] == ',')
			{
				index++;
				inString = false;
			}
			else
			{
				index++;
			}
		}
		else if (parenDepth == 0 && index + 1 < body.Length && body[index + 1] == ',')
		{
			inString = false;
		}
		else if (!IsEmbeddedStringQuote(body, index))
		{
			inString = false;
		}
	}

	private static bool IsEmbeddedStringQuote(ReadOnlySpan<char> body, int quoteIndex)
	{
		if (quoteIndex + 1 >= body.Length)
		{
			return false;
		}
		char c = body[quoteIndex + 1];
		if (c != ',' && c != ')')
		{
			return !char.IsWhiteSpace(c);
		}
		return false;
	}

	private static bool TryParseElement(string element, OracleUdtComplexScalarType scalarType, [NotNullWhen(true)] out object? value)
	{
		value = null;
		element = element.Trim();
		if (element.Length == 0 || string.Equals(element, "NULL", StringComparison.OrdinalIgnoreCase))
		{
			value = DBNull.Value;
			return true;
		}
		if (element.Contains('('))
		{
			string text = element;
			if (text[text.Length - 1] == ')')
			{
				if (scalarType == OracleUdtComplexScalarType.Raw && OracleUdtHexBytes.TryParseHexRawElement(element, out byte[] bytes))
				{
					value = bytes;
					return true;
				}
				if (TryParseUdtLiteral(element, isArray: false, out OracleUdtValue udtValue))
				{
					value = udtValue;
					return true;
				}
			}
		}
		if (scalarType != OracleUdtComplexScalarType.Unknown && TryParseElementByScalarType(element, scalarType, out value))
		{
			return true;
		}
		if (scalarType == OracleUdtComplexScalarType.Raw && OracleUdtHexBytes.TryParseHexRawElement(element, out byte[] bytes2))
		{
			value = bytes2;
			return true;
		}
		if (element.Length >= 2 && element[0] == '\'')
		{
			string text2 = element;
			if (text2[text2.Length - 1] == '\'')
			{
				if (scalarType == OracleUdtComplexScalarType.Raw && OracleUdtHexBytes.TryParseHexRawElement(element, out byte[] bytes3))
				{
					value = bytes3;
					return true;
				}
				string text3 = element;
				value = UnescapeQuotedString(text3.Substring(1, text3.Length - 1 - 1));
				return true;
			}
		}
		if (string.Equals(element, "true", StringComparison.OrdinalIgnoreCase))
		{
			value = true;
			return true;
		}
		if (string.Equals(element, "false", StringComparison.OrdinalIgnoreCase))
		{
			value = false;
			return true;
		}
		if (long.TryParse(element, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			value = result;
			return true;
		}
		if (decimal.TryParse(element, NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
		{
			if (result2 == Math.Truncate(result2))
			{
				if (result2 >= -2147483648m && result2 <= 2147483647m)
				{
					value = (int)result2;
				}
				else if (result2 >= -9223372036854775808m && result2 <= 9223372036854775807m)
				{
					value = (long)result2;
				}
				else
				{
					value = result2;
				}
			}
			else
			{
				value = result2;
			}
			return true;
		}
		value = element;
		return true;
	}

	private static bool TryParseElementByScalarType(string element, OracleUdtComplexScalarType scalarType, [NotNullWhen(true)] out object? value)
	{
		value = null;
		switch (scalarType)
		{
		case OracleUdtComplexScalarType.Raw:
		{
			if (OracleUdtHexBytes.TryParseHexRawElement(element, out byte[] bytes))
			{
				value = bytes;
				return true;
			}
			return false;
		}
		case OracleUdtComplexScalarType.Varchar2:
			if (element.Length >= 2 && element[0] == '\'')
			{
				if (element[element.Length - 1] == '\'')
				{
					string text = element;
					value = UnescapeQuotedString(text.Substring(1, text.Length - 1 - 1));
					return true;
				}
			}
			return false;
		case OracleUdtComplexScalarType.Char:
			if (element.Length >= 2 && element[0] == '\'')
			{
				if (element[element.Length - 1] == '\'')
				{
					string text = element;
					string text2 = UnescapeQuotedString(text.Substring(1, text.Length - 1 - 1));
					value = ((text2.Length > 0) ? text2[0] : '\0');
					return true;
				}
			}
			return false;
		case OracleUdtComplexScalarType.Timestamp:
		{
			if (OracleUdtTextScalars.TryParseTimestamp(element, out var timestamp))
			{
				value = timestamp;
				return true;
			}
			return false;
		}
		case OracleUdtComplexScalarType.IntervalYM:
		{
			if (OracleUdtTextScalars.TryParseIntervalYm(element, out var interval2))
			{
				value = interval2;
				return true;
			}
			return false;
		}
		case OracleUdtComplexScalarType.IntervalDS:
		{
			if (OracleUdtTextScalars.TryParseIntervalDs(element, out var interval))
			{
				value = interval;
				return true;
			}
			return false;
		}
		default:
			return false;
		}
	}

	private static void RepairOceanBaseTextStructElements(string typeName, List<string> elements)
	{
		if (TryGetRegisteredStructLayout(typeName, out int expectedCount, out IReadOnlyList<OracleUdtComplexScalarType> attrTypes, out int rawIndex))
		{
			FixDuplicateRawHexBeforeTimestamp(elements, attrTypes);
			CollapseConsecutiveDuplicateHexElements(elements, expectedCount, rawIndex);
			StripMisplacedRawHexDuplicates(elements, attrTypes, rawIndex);
		}
	}

	private static bool TryGetRegisteredStructLayout(string typeName, out int expectedCount, out IReadOnlyList<OracleUdtComplexScalarType> attrTypes, out int rawIndex)
	{
		expectedCount = 0;
		attrTypes = Array.Empty<OracleUdtComplexScalarType>();
		rawIndex = -1;
		int? num = OracleUdt.TryGetRegisteredAttributeCount(typeName);
		if (!num.HasValue)
		{
			return false;
		}
		expectedCount = num.Value;
		attrTypes = OracleUdtComplexScalarInference.InferStructAttributeTypes(typeName, expectedCount);
		for (int i = 0; i < attrTypes.Count; i++)
		{
			if (attrTypes[i] == OracleUdtComplexScalarType.Raw)
			{
				rawIndex = i;
				break;
			}
		}
		return true;
	}

	private static void FixDuplicateRawHexBeforeTimestamp(List<string> elements, IReadOnlyList<OracleUdtComplexScalarType> attrTypes)
	{
		for (int i = 0; i + 1 < attrTypes.Count && i + 1 < elements.Count; i++)
		{
			if (attrTypes[i] == OracleUdtComplexScalarType.Raw && attrTypes[i + 1] == OracleUdtComplexScalarType.Timestamp && OracleUdtHexBytes.LooksLikeHexLiteral(elements[i]) && OracleUdtHexBytes.LooksLikeHexLiteral(elements[i + 1]) && HexElementsEquivalent(elements[i], elements[i + 1]))
			{
				elements.RemoveAt(i + 1);
				break;
			}
		}
	}

	private static void CollapseConsecutiveDuplicateHexElements(List<string> elements, int expectedCount, int rawIndex)
	{
		if (rawIndex < 0 || rawIndex >= elements.Count || !OracleUdtHexBytes.TryParseHexRawElement(elements[rawIndex], out byte[] _))
		{
			return;
		}
		int num = elements.Count - 1;
		while (num > rawIndex && elements.Count > expectedCount)
		{
			if (OracleUdtHexBytes.LooksLikeHexLiteral(elements[num]) && HexElementsEquivalent(elements[rawIndex], elements[num]))
			{
				elements.RemoveAt(num);
			}
			num--;
		}
	}

	/// <summary>
	/// OceanBase text UDT literals sometimes repeat the RAW hex in TIMESTAMP/INTERVAL slots after line breaks.
	/// Clear those placeholders so the registered attribute type can be parsed or mapped to NULL.
	/// </summary>
	private static void StripMisplacedRawHexDuplicates(List<string> elements, IReadOnlyList<OracleUdtComplexScalarType> attrTypes, int rawIndex)
	{
		if (rawIndex < 0 || rawIndex >= elements.Count || !OracleUdtHexBytes.TryParseHexRawElement(elements[rawIndex], out byte[] _))
		{
			return;
		}
		for (int i = 0; i < elements.Count && i < attrTypes.Count; i++)
		{
			if (i != rawIndex && attrTypes[i] != OracleUdtComplexScalarType.Raw && OracleUdtHexBytes.LooksLikeHexLiteral(elements[i]) && HexElementsEquivalent(elements[rawIndex], elements[i]))
			{
				elements[i] = string.Empty;
			}
		}
	}

	private static bool HexElementsEquivalent(string left, string right)
	{
		if (!OracleUdtHexBytes.TryParseHexRawElement(left, out byte[] bytes) || !OracleUdtHexBytes.TryParseHexRawElement(right, out byte[] bytes2))
		{
			return false;
		}
		return ((ReadOnlySpan<byte>)bytes.AsSpan()).SequenceEqual((ReadOnlySpan<byte>)bytes2);
	}

	private static string UnescapeQuotedString(string quoted)
	{
		return quoted.Replace("''", "'");
	}
}
