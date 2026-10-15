using System;
using System.Buffers.Binary;
using System.Data.SqlTypes;
using System.Globalization;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace OceanBase;

/// <summary>
/// Represents an Oracle INTERVAL YEAR TO MONTH value.
/// </summary>
public struct OracleIntervalYM : IComparable, IComparable<OracleIntervalYM>, IEquatable<OracleIntervalYM>, INullable, IOracleNullable, IXmlSerializable
{
	private readonly bool m_isNull;

	private readonly long m_totalMonths;

	/// <summary>Gets a NULL interval.</summary>
	public static OracleIntervalYM Null => new OracleIntervalYM(isNull: true, 0L);

	/// <summary>Returns <c>true</c> if this value is NULL.</summary>
	public bool IsNull => m_isNull;

	/// <summary>The total number of months represented by this interval (signed).</summary>
	public long TotalMonths
	{
		get
		{
			if (!m_isNull)
			{
				return m_totalMonths;
			}
			throw new InvalidOperationException("OracleIntervalYM is NULL.");
		}
	}

	/// <summary>The sign of this interval.</summary>
	public bool IsNegative => TotalMonths < 0;

	/// <summary>The year component (signed, derived from <see cref="P:OceanBase.OracleIntervalYM.TotalMonths" />).</summary>
	public long Years => TotalMonths / 12;

	/// <summary>The month component (0..11, derived from <see cref="P:OceanBase.OracleIntervalYM.TotalMonths" />).</summary>
	public int Months => (int)(Math.Abs(TotalMonths) % 12);

	public long Value
	{
		get
		{
			if (!IsNull)
			{
				return TotalMonths;
			}
			throw new InvalidOperationException("OracleIntervalYM is NULL.");
		}
	}

	private OracleIntervalYM(bool isNull, long totalMonths)
	{
		m_isNull = isNull;
		m_totalMonths = totalMonths;
	}

	/// <summary>
	/// Initializes a new <see cref="T:OceanBase.OracleIntervalYM" /> from total months (signed).
	/// </summary>
	public OracleIntervalYM(long totalMonths)
	{
		this = FromTotalMonths(totalMonths);
	}

	/// <summary>
	/// Initializes a new <see cref="T:OceanBase.OracleIntervalYM" /> from year/month components.
	/// Example: <c>new OracleIntervalYM(1, 6)</c> represents 1 year 6 months.
	/// </summary>
	public OracleIntervalYM(int years, int months)
	{
		long num = years;
		bool flag = num < 0 || months < 0;
		long num2 = Math.Abs(num);
		int num3 = Math.Abs(months);
		if ((uint)num3 > 11u)
		{
			throw new ArgumentOutOfRangeException("months", "months must be between 0 and 11.");
		}
		long num4 = checked(num2 * 12 + num3);
		this = FromTotalMonths(flag ? (-num4) : num4);
	}

	public static OracleIntervalYM FromTotalMonths(long totalMonths)
	{
		return new OracleIntervalYM(isNull: false, totalMonths);
	}

	/// <summary>
	/// Creates an <see cref="T:OceanBase.OracleIntervalYM" /> from wire components.
	/// </summary>
	internal static OracleIntervalYM FromWire(bool isNegative, int years, int months)
	{
		return FromYearsMonths(years, months, isNegative);
	}

	public static OracleIntervalYM FromYearsMonths(int years, int months, bool isNegative = false)
	{
		return FromYearsMonths((long)years, months, isNegative);
	}

	public static OracleIntervalYM FromYearsMonths(long years, int months, bool isNegative = false)
	{
		if ((uint)months > 11u)
		{
			throw new ArgumentOutOfRangeException("months", "months must be between 0 and 11.");
		}
		long num = checked(years * 12 + months);
		return FromTotalMonths(isNegative ? (-num) : num);
	}

	public static OracleIntervalYM FromTimeSpan(TimeSpan timeSpan)
	{
		bool flag = timeSpan.Ticks < 0;
		double totalDays = (flag ? timeSpan.Negate() : timeSpan).TotalDays;
		long years = (long)(totalDays / 365.0);
		int months = (int)(totalDays % 365.0 / 30.0);
		return FromYearsMonths(years, months, flag);
	}

	public string ToOracleLiteral()
	{
		if (!IsNull)
		{
			return "INTERVAL '" + ToString() + "' YEAR(9) TO MONTH";
		}
		return "NULL";
	}

	public override string ToString()
	{
		if (!IsNull)
		{
			return (IsNegative ? "-" : "") + $"{Math.Abs(Years)}-{Months:00}";
		}
		return "NULL";
	}

	public byte[] ToBytes()
	{
		byte[] obj = new byte[7]
		{
			IsNegative ? ((byte)1) : ((byte)0),
			0,
			0,
			0,
			0,
			0,
			0
		};
		long years = Years;
		_ = Months;
		BinaryPrimitives.WriteInt32LittleEndian(obj.AsSpan(1), (int)years);
		obj[5] = (byte)Months;
		obj[6] = (byte)years.ToString(CultureInfo.InvariantCulture).Length;
		return obj;
	}

	public int CompareTo(object? obj)
	{
		if (!(obj is OracleIntervalYM other))
		{
			throw new ArgumentException("CompareTo can only be called with another OracleIntervalYM", "obj");
		}
		return CompareTo(other);
	}

	public int CompareTo(OracleIntervalYM other)
	{
		if (IsNull || other.IsNull)
		{
			throw new InvalidOperationException("OracleIntervalYM is NULL.");
		}
		return m_totalMonths.CompareTo(other.m_totalMonths);
	}

	public bool Equals(OracleIntervalYM other)
	{
		if (m_isNull == other.m_isNull)
		{
			if (!m_isNull)
			{
				return m_totalMonths == other.m_totalMonths;
			}
			return true;
		}
		return false;
	}

	public override bool Equals(object? obj)
	{
		if (obj is OracleIntervalYM other)
		{
			return Equals(other);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(m_isNull, m_totalMonths);
	}

	public static bool operator ==(OracleIntervalYM left, OracleIntervalYM right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(OracleIntervalYM left, OracleIntervalYM right)
	{
		return !left.Equals(right);
	}

	public static bool operator <(OracleIntervalYM left, OracleIntervalYM right)
	{
		return left.CompareTo(right) < 0;
	}

	public static bool operator <=(OracleIntervalYM left, OracleIntervalYM right)
	{
		return left.CompareTo(right) <= 0;
	}

	public static bool operator >(OracleIntervalYM left, OracleIntervalYM right)
	{
		return left.CompareTo(right) > 0;
	}

	public static bool operator >=(OracleIntervalYM left, OracleIntervalYM right)
	{
		return left.CompareTo(right) >= 0;
	}

	XmlSchema? IXmlSerializable.GetSchema()
	{
		return null;
	}

	void IXmlSerializable.ReadXml(XmlReader reader)
	{
		string text = reader.ReadElementContentAsString();
		if (string.IsNullOrWhiteSpace(text) || string.Equals(text, "NULL", StringComparison.OrdinalIgnoreCase))
		{
			this = Null;
			return;
		}
		bool flag = text.Length != 0 && text[0] == '-';
		if (flag)
		{
			string text2 = text;
			text = text2.Substring(1, text2.Length - 1);
		}
		int num = text.IndexOf('-');
		if (num <= 0 || num == text.Length - 1)
		{
			throw new FormatException("Invalid OracleIntervalYM format: '" + text + "'.");
		}
		string s = text.Substring(0, num);
		string s2 = text.Substring(num + 1);
		if (!long.TryParse(s, out var result) || !int.TryParse(s2, out var result2))
		{
			throw new FormatException("Invalid OracleIntervalYM format: '" + text + "'.");
		}
		this = FromYearsMonths(result, result2, flag);
	}

	void IXmlSerializable.WriteXml(XmlWriter writer)
	{
		writer.WriteString(ToString());
	}
}
