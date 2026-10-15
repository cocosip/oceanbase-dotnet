using System;
using System.Buffers.Binary;
using System.Data.SqlTypes;
using System.Globalization;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace OceanBase;

/// <summary>
/// Represents an Oracle INTERVAL DAY TO SECOND value.
/// </summary>
public struct OracleIntervalDS : IComparable, IComparable<OracleIntervalDS>, IEquatable<OracleIntervalDS>, INullable, IOracleNullable, IXmlSerializable
{
	private readonly bool m_isNull;

	private readonly bool m_isNegative;

	private readonly int m_days;

	private readonly int m_hours;

	private readonly int m_minutes;

	private readonly int m_seconds;

	private readonly int m_nanoseconds;

	private readonly byte m_scale;

	/// <summary>Gets a NULL interval.</summary>
	public static OracleIntervalDS Null => new OracleIntervalDS(isNull: true, isNegative: false, 0, 0, 0, 0, 0, 9);

	/// <summary>Returns <c>true</c> if this value is NULL.</summary>
	public bool IsNull => m_isNull;

	public bool IsNegative
	{
		get
		{
			if (!IsNull)
			{
				return m_isNegative;
			}
			return false;
		}
	}

	public int Days
	{
		get
		{
			if (!IsNull)
			{
				return m_days;
			}
			throw new InvalidOperationException("OracleIntervalDS is NULL.");
		}
	}

	public int Hours
	{
		get
		{
			if (!IsNull)
			{
				return m_hours;
			}
			throw new InvalidOperationException("OracleIntervalDS is NULL.");
		}
	}

	public int Minutes
	{
		get
		{
			if (!IsNull)
			{
				return m_minutes;
			}
			throw new InvalidOperationException("OracleIntervalDS is NULL.");
		}
	}

	public int Seconds
	{
		get
		{
			if (!IsNull)
			{
				return m_seconds;
			}
			throw new InvalidOperationException("OracleIntervalDS is NULL.");
		}
	}

	public double Milliseconds
	{
		get
		{
			if (!IsNull)
			{
				return (double)m_nanoseconds / 1000000.0;
			}
			throw new InvalidOperationException("OracleIntervalDS is NULL.");
		}
	}

	public int Nanoseconds
	{
		get
		{
			if (!IsNull)
			{
				return m_nanoseconds;
			}
			throw new InvalidOperationException("OracleIntervalDS is NULL.");
		}
	}

	public byte Scale
	{
		get
		{
			if (!IsNull)
			{
				return m_scale;
			}
			throw new InvalidOperationException("OracleIntervalDS is NULL.");
		}
	}

	public TimeSpan Value
	{
		get
		{
			if (!IsNull)
			{
				return ToTimeSpan();
			}
			throw new InvalidOperationException("OracleIntervalDS is NULL.");
		}
	}

	/// <summary>
	/// Returns a TimeSpan-style string using the Oracle fractional seconds <see cref="P:OceanBase.OracleIntervalDS.Scale" /> (e.g. scale=6 prints 6 digits).
	/// This matches the OceanBase Java driver's formatting semantics where the fractional field is nanoseconds and scale
	/// controls how many digits are displayed.
	/// </summary>
	public string ValueString
	{
		get
		{
			if (!IsNull)
			{
				return ToTimeSpanString();
			}
			throw new InvalidOperationException("OracleIntervalDS is NULL.");
		}
	}

	private OracleIntervalDS(bool isNull, bool isNegative, int days, int hours, int minutes, int seconds, int nanoseconds, byte scale)
	{
		m_isNull = isNull;
		m_isNegative = isNegative;
		m_days = days;
		m_hours = hours;
		m_minutes = minutes;
		m_seconds = seconds;
		m_nanoseconds = nanoseconds;
		m_scale = scale;
	}

	/// <summary>
	/// Initializes a new <see cref="T:OceanBase.OracleIntervalDS" /> from a <see cref="T:System.TimeSpan" />.
	/// </summary>
	public OracleIntervalDS(TimeSpan timeSpan)
	{
		this = FromTimeSpan(timeSpan);
	}

	/// <summary>
	/// Initializes a new <see cref="T:OceanBase.OracleIntervalDS" /> by parsing a string value.
	/// Accepts "NULL" (case-insensitive) or whitespace as <see cref="P:OceanBase.OracleIntervalDS.Null" />, otherwise parses "[-]D HH:MM:SS.NNNNNNNNN".
	/// </summary>
	public OracleIntervalDS(string value)
	{
		ArgumentNullException.ThrowIfNull(value, "value");
		this = Parse(value);
	}

	public static OracleIntervalDS FromComponents(bool isNegative, int days, int hours, int minutes, int seconds, int nanoseconds)
	{
		return FromComponents(isNegative, days, hours, minutes, seconds, nanoseconds, 9);
	}

	public static OracleIntervalDS FromComponents(bool isNegative, int days, int hours, int minutes, int seconds, int nanoseconds, byte scale)
	{
		if ((uint)hours > 23u)
		{
			throw new ArgumentOutOfRangeException("hours");
		}
		if ((uint)minutes > 59u)
		{
			throw new ArgumentOutOfRangeException("minutes");
		}
		if ((uint)seconds > 59u)
		{
			throw new ArgumentOutOfRangeException("seconds");
		}
		if ((uint)nanoseconds > 999999999u)
		{
			throw new ArgumentOutOfRangeException("nanoseconds");
		}
		if (days < 0)
		{
			throw new ArgumentOutOfRangeException("days", "days must be non-negative; use isNegative.");
		}
		if (scale > 9)
		{
			throw new ArgumentOutOfRangeException("scale");
		}
		return new OracleIntervalDS(isNull: false, isNegative, days, hours, minutes, seconds, nanoseconds, scale);
	}

	/// <summary>
	/// Creates an <see cref="T:OceanBase.OracleIntervalDS" /> from wire components. Handles fractional scaling and normalizes overflow.
	/// </summary>
	internal static OracleIntervalDS FromWire(bool isNegative, int days, int hours, int minutes, int seconds, int fractionalNanoseconds, int scale)
	{
		if ((uint)scale > 9u)
		{
			throw new ArgumentOutOfRangeException("scale");
		}
		int days2 = Math.Abs(days);
		int hours2 = Math.Abs(hours);
		int minutes2 = Math.Abs(minutes);
		int seconds2 = Math.Abs(seconds);
		long num = Math.Abs((long)fractionalNanoseconds);
		if (num > 999999999)
		{
			throw new FormatException($"Invalid OracleIntervalDS: fractional nanoseconds out of range: {num}.");
		}
		return FromComponents(isNegative, days2, hours2, minutes2, seconds2, (int)num, 9);
	}

	public static OracleIntervalDS FromTimeSpan(TimeSpan timeSpan)
	{
		bool num = timeSpan.Ticks < 0;
		TimeSpan timeSpan2 = (num ? timeSpan.Negate() : timeSpan);
		int days = timeSpan2.Days;
		int hours = timeSpan2.Hours;
		int minutes = timeSpan2.Minutes;
		int seconds = timeSpan2.Seconds;
		checked
		{
			int nanoseconds = (int)(unchecked(timeSpan2.Ticks % 10000000) * 100);
			return FromComponents(num, days, hours, minutes, seconds, nanoseconds);
		}
	}

	/// <summary>
	/// Parses a string representation of an INTERVAL DAY TO SECOND.
	/// Accepts "NULL" (case-insensitive) or whitespace as <see cref="P:OceanBase.OracleIntervalDS.Null" />.
	/// </summary>
	public static OracleIntervalDS Parse(string value)
	{
		ArgumentNullException.ThrowIfNull(value, "value");
		if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "NULL", StringComparison.OrdinalIgnoreCase))
		{
			return Null;
		}
		string text = value;
		bool flag = text.Length != 0 && text[0] == '-';
		if (flag)
		{
			string text2 = text;
			text = text2.Substring(1, text2.Length - 1);
		}
		int num = text.IndexOf(' ');
		if (num <= 0 || num == text.Length - 1)
		{
			throw new FormatException("Invalid OracleIntervalDS format: '" + text + "'.");
		}
		string s = text.Substring(0, num);
		string text3 = text.Substring(num + 1);
		if (!int.TryParse(s, out var result))
		{
			throw new FormatException("Invalid OracleIntervalDS format: '" + text + "'.");
		}
		int num2 = text3.IndexOf('.');
		if (num2 <= 0)
		{
			throw new FormatException("Invalid OracleIntervalDS format: '" + text + "'.");
		}
		string text4 = text3.Substring(0, num2);
		string text5 = ((num2 == text3.Length - 1) ? "" : text3.Substring(num2 + 1));
		int num3 = text4.IndexOf(':');
		int num4 = ((num3 < 0) ? (-1) : text4.IndexOf(':', num3 + 1));
		if (num3 <= 0 || num4 <= num3 + 1 || num4 == text4.Length - 1)
		{
			throw new FormatException("Invalid OracleIntervalDS format: '" + text + "'.");
		}
		string s2 = text4.Substring(0, num3);
		string s3 = text4.Substring(num3 + 1, num4 - num3 - 1);
		string s4 = text4.Substring(num4 + 1);
		if (!int.TryParse(s2, out var result2) || !int.TryParse(s3, out var result3) || !int.TryParse(s4, out var result4))
		{
			throw new FormatException("Invalid OracleIntervalDS format: '" + text + "'.");
		}
		byte scale = (byte)Math.Min(9, text5.Length);
		if (text5.Length > 9)
		{
			text5 = text5.Substring(0, 9);
		}
		while (text5.Length < 9)
		{
			text5 += "0";
		}
		if (!int.TryParse((text5.Length == 0) ? "0" : text5, out var result5))
		{
			throw new FormatException("Invalid OracleIntervalDS format: '" + text + "'.");
		}
		return FromComponents(flag, result, result2, result3, result4, result5, scale);
	}

	/// <summary>Converts to <see cref="T:System.TimeSpan" /> by truncating to 100ns tick resolution.</summary>
	public TimeSpan ToTimeSpan()
	{
		if (IsNull)
		{
			throw new InvalidOperationException("OracleIntervalDS is NULL.");
		}
		long num = checked(m_days * 864000000000L) + checked(m_hours * 36000000000L) + checked(unchecked((long)m_minutes) * 600000000L) + checked(unchecked((long)m_seconds) * 10000000L) + m_nanoseconds / 100;
		if (!m_isNegative)
		{
			return new TimeSpan(num);
		}
		return new TimeSpan(-num);
	}

	public string ToTimeSpanString()
	{
		if (IsNull)
		{
			throw new InvalidOperationException("OracleIntervalDS is NULL.");
		}
		string value = (m_isNegative ? "-" : "");
		if (m_scale != 0)
		{
			int num = 1;
			for (int i = 0; i < 9 - m_scale; i++)
			{
				num *= 10;
			}
			string value2 = (m_nanoseconds / num).ToString(CultureInfo.InvariantCulture).PadLeft(m_scale, '0');
			return $"{value}{m_days}.{m_hours:00}:{m_minutes:00}:{m_seconds:00}.{value2}";
		}
		return $"{value}{m_days}.{m_hours:00}:{m_minutes:00}:{m_seconds:00}";
	}

	public string ToOracleLiteral()
	{
		if (!IsNull)
		{
			return "INTERVAL '" + ToString() + "' DAY(9) TO SECOND(9)";
		}
		return "NULL";
	}

	public override string ToString()
	{
		if (IsNull)
		{
			return "NULL";
		}
		string value = (m_isNegative ? "-" : "");
		if (m_scale != 0)
		{
			int num = 1;
			for (int i = 0; i < 9 - m_scale; i++)
			{
				num *= 10;
			}
			int num2 = m_nanoseconds / num;
			return $"{value}{m_days} {m_hours:00}:{m_minutes:00}:{m_seconds:00}.{num2.ToString(CultureInfo.InvariantCulture).PadLeft(m_scale, '0')}";
		}
		return $"{value}{m_days} {m_hours:00}:{m_minutes:00}:{m_seconds:00}";
	}

	public byte[] ToBytes()
	{
		byte[] obj = new byte[14]
		{
			m_isNegative ? ((byte)1) : ((byte)0),
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0
		};
		BinaryPrimitives.WriteInt32LittleEndian(obj.AsSpan(1), m_days);
		obj[5] = (byte)m_hours;
		obj[6] = (byte)m_minutes;
		obj[7] = (byte)m_seconds;
		BinaryPrimitives.WriteInt32LittleEndian(obj.AsSpan(8), m_nanoseconds);
		obj[12] = (byte)m_days.ToString(CultureInfo.InvariantCulture).Length;
		obj[13] = m_scale;
		return obj;
	}

	public int CompareTo(object? obj)
	{
		if (!(obj is OracleIntervalDS other))
		{
			throw new ArgumentException("CompareTo can only be called with another OracleIntervalDS", "obj");
		}
		return CompareTo(other);
	}

	public int CompareTo(OracleIntervalDS other)
	{
		if (IsNull || other.IsNull)
		{
			throw new InvalidOperationException("OracleIntervalDS is NULL.");
		}
		long ticks = ToTimeSpan().Ticks;
		long ticks2 = other.ToTimeSpan().Ticks;
		int num = ticks.CompareTo(ticks2);
		if (num != 0)
		{
			return num;
		}
		return 0;
	}

	public bool Equals(OracleIntervalDS other)
	{
		if (m_isNull == other.m_isNull)
		{
			if (!m_isNull)
			{
				if (m_isNegative == other.m_isNegative && m_days == other.m_days && m_hours == other.m_hours && m_minutes == other.m_minutes && m_seconds == other.m_seconds)
				{
					return m_nanoseconds == other.m_nanoseconds;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	public override bool Equals(object? obj)
	{
		if (obj is OracleIntervalDS other)
		{
			return Equals(other);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(m_isNull, m_isNegative, m_days, m_hours, m_minutes, m_seconds, m_nanoseconds, m_scale);
	}

	public static bool operator ==(OracleIntervalDS left, OracleIntervalDS right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(OracleIntervalDS left, OracleIntervalDS right)
	{
		return !left.Equals(right);
	}

	public static bool operator <(OracleIntervalDS left, OracleIntervalDS right)
	{
		return left.CompareTo(right) < 0;
	}

	public static bool operator <=(OracleIntervalDS left, OracleIntervalDS right)
	{
		return left.CompareTo(right) <= 0;
	}

	public static bool operator >(OracleIntervalDS left, OracleIntervalDS right)
	{
		return left.CompareTo(right) > 0;
	}

	public static bool operator >=(OracleIntervalDS left, OracleIntervalDS right)
	{
		return left.CompareTo(right) >= 0;
	}

	XmlSchema? IXmlSerializable.GetSchema()
	{
		return null;
	}

	void IXmlSerializable.ReadXml(XmlReader reader)
	{
		string value = reader.ReadElementContentAsString();
		this = Parse(value);
	}

	void IXmlSerializable.WriteXml(XmlWriter writer)
	{
		writer.WriteString(ToString());
	}
}
