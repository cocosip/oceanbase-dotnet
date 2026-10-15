using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace OceanBase;

/// <summary>
/// <see cref="T:OceanBase.MySqlDecimal" /> represents a MySQL <c>DECIMAL</c> value that is too large to fit in a .NET <see cref="T:System.Decimal" />.
/// </summary>
public readonly struct MySqlDecimal
{
	private static readonly Regex s_pattern = new Regex("^-?([0-9]+)(\\.([0-9]+))?$");

	private readonly string m_value;

	/// <summary>
	/// Gets the value of this <see cref="T:OceanBase.MySqlDecimal" /> as a <see cref="T:System.Decimal" />.
	/// </summary>
	/// <remarks>This method will throw an <see cref="T:System.OverflowException" /> if the value is too large to be represented.</remarks>
	public decimal Value => decimal.Parse(m_value, CultureInfo.InvariantCulture);

	/// <summary>
	/// Gets the value of this <see cref="T:OceanBase.MySqlDecimal" /> as a <see cref="T:System.Double" />.
	/// </summary>
	/// <remarks>The return value may have lost precision.</remarks>
	public double ToDouble()
	{
		return double.Parse(m_value, CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// Gets the original value of this <see cref="T:OceanBase.MySqlDecimal" /> as a <see cref="T:System.String" />.
	/// </summary>
	public override string ToString()
	{
		return m_value;
	}

	internal MySqlDecimal(string value)
	{
		Match match = s_pattern.Match(value);
		if (match != null && match.Success)
		{
			int length = match.Groups[1].Length;
			int length2 = match.Groups[3].Value.TrimEnd('0').Length;
			bool num = length + length2 <= 65 && length2 <= 30;
			bool flag = value[0] == '-' && match.Groups[1].Value == "0" && length2 == 0;
			if (num && !flag)
			{
				m_value = value;
				return;
			}
		}
		throw new FormatException("Could not parse the value as a MySqlDecimal: " + value);
	}
}
