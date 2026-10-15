using System;
using System.Buffers.Binary;
using System.Data.SqlTypes;
using System.Globalization;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace OceanBase;

/// <summary>
/// Represents an Oracle-mode TIMESTAMP WITH LOCAL TIME ZONE value.
/// Note: proper session time zone conversion is connection/session dependent and is not implemented here; this type stores the raw components.
/// </summary>
public struct OracleTimeStampLTZ : IComparable, IComparable<OracleTimeStampLTZ>, IEquatable<OracleTimeStampLTZ>, INullable, IOracleNullable, IXmlSerializable
{
	private readonly bool m_isNull;

	private readonly int m_year;

	private readonly int m_month;

	private readonly int m_day;

	private readonly int m_hour;

	private readonly int m_minute;

	private readonly int m_second;

	private readonly int m_nanoseconds;

	private readonly byte m_scale;

	public static OracleTimeStampLTZ Null => new OracleTimeStampLTZ(isNull: true, 0, 0, 0, 0, 0, 0, 0, 9);

	public bool IsNull => m_isNull;

	public int Year
	{
		get
		{
			if (!IsNull)
			{
				return m_year;
			}
			throw new InvalidOperationException("OracleTimeStampLTZ is NULL.");
		}
	}

	public int Month
	{
		get
		{
			if (!IsNull)
			{
				return m_month;
			}
			throw new InvalidOperationException("OracleTimeStampLTZ is NULL.");
		}
	}

	public int Day
	{
		get
		{
			if (!IsNull)
			{
				return m_day;
			}
			throw new InvalidOperationException("OracleTimeStampLTZ is NULL.");
		}
	}

	public int Hour
	{
		get
		{
			if (!IsNull)
			{
				return m_hour;
			}
			throw new InvalidOperationException("OracleTimeStampLTZ is NULL.");
		}
	}

	public int Minute
	{
		get
		{
			if (!IsNull)
			{
				return m_minute;
			}
			throw new InvalidOperationException("OracleTimeStampLTZ is NULL.");
		}
	}

	public int Second
	{
		get
		{
			if (!IsNull)
			{
				return m_second;
			}
			throw new InvalidOperationException("OracleTimeStampLTZ is NULL.");
		}
	}

	public int Nanosecond
	{
		get
		{
			if (!IsNull)
			{
				return m_nanoseconds;
			}
			throw new InvalidOperationException("OracleTimeStampLTZ is NULL.");
		}
	}

	public DateTime Value
	{
		get
		{
			if (!IsNull)
			{
				return ToDateTime();
			}
			throw new InvalidOperationException("OracleTimeStampLTZ is NULL.");
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
			throw new InvalidOperationException("OracleTimeStampLTZ is NULL.");
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
			throw new InvalidOperationException("OracleTimeStampLTZ is NULL.");
		}
	}

	public DateTime Date
	{
		get
		{
			if (!IsNull)
			{
				return new DateTime(m_year, m_month, m_day, 0, 0, 0, DateTimeKind.Unspecified);
			}
			throw new InvalidOperationException("OracleTimeStampLTZ is NULL.");
		}
	}

	public TimeSpan Time
	{
		get
		{
			if (!IsNull)
			{
				return new TimeSpan(m_hour, m_minute, m_second).Add(TimeSpan.FromTicks(m_nanoseconds / 100));
			}
			throw new InvalidOperationException("OracleTimeStampLTZ is NULL.");
		}
	}

	private OracleTimeStampLTZ(bool isNull, int year, int month, int day, int hour, int minute, int second, int nanoseconds, byte scale)
	{
		m_isNull = isNull;
		m_year = year;
		m_month = month;
		m_day = day;
		m_hour = hour;
		m_minute = minute;
		m_second = second;
		m_nanoseconds = nanoseconds;
		m_scale = scale;
	}

	public OracleTimeStampLTZ(DateTime value)
		: this(isNull: false, value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, (int)(value.Ticks % 10000000 * 100), 9)
	{
	}

	public OracleTimeStampLTZ(int year, int month, int day, int hour, int minute, int second, int nanosecond)
		: this(isNull: false, year, month, day, hour, minute, second, nanosecond, 9)
	{
	}

	internal static OracleTimeStampLTZ FromWire(int year, int month, int day, int hour, int minute, int second, int nanoseconds, int scale, TimeSpan zero)
	{
		if ((uint)scale > 9u)
		{
			throw new ArgumentOutOfRangeException("scale");
		}
		if ((uint)nanoseconds > 999999999u)
		{
			throw new ArgumentOutOfRangeException("nanoseconds");
		}
		return new OracleTimeStampLTZ(isNull: false, year, month, day, hour, minute, second, nanoseconds, (byte)scale);
	}

	internal static OracleTimeStampLTZ FromWire(int year, int month, int day, int hour, int minute, int second, int nanoseconds, int scale)
	{
		return FromWire(year, month, day, hour, minute, second, nanoseconds, scale, TimeSpan.Zero);
	}

	public DateTime ToDateTime()
	{
		if (IsNull)
		{
			throw new InvalidOperationException("OracleTimeStampLTZ is NULL.");
		}
		return new DateTime(m_year, m_month, m_day, m_hour, m_minute, m_second, DateTimeKind.Unspecified).AddTicks(m_nanoseconds / 100);
	}

	public override string ToString()
	{
		if (IsNull)
		{
			return "NULL";
		}
		string text = new DateTime(m_year, m_month, m_day, m_hour, m_minute, m_second, DateTimeKind.Unspecified).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
		if (m_scale == 0)
		{
			return text;
		}
		int num = 1;
		for (int i = 0; i < 9 - m_scale; i++)
		{
			num *= 10;
		}
		string text2 = (m_nanoseconds / num).ToString(CultureInfo.InvariantCulture).PadLeft(m_scale, '0');
		return text + "." + text2;
	}

	public byte[] ToBytes()
	{
		byte[] obj = new byte[12]
		{
			(byte)(m_year / 100),
			(byte)(m_year % 100),
			(byte)m_month,
			(byte)m_day,
			(byte)m_hour,
			(byte)m_minute,
			(byte)m_second,
			0,
			0,
			0,
			0,
			0
		};
		BinaryPrimitives.WriteInt32LittleEndian(obj.AsSpan(7), m_nanoseconds);
		obj[11] = 9;
		return obj;
	}

	public int CompareTo(object? obj)
	{
		if (!(obj is OracleTimeStampLTZ other))
		{
			throw new ArgumentException("CompareTo can only be called with another OracleTimeStampLTZ", "obj");
		}
		return CompareTo(other);
	}

	public int CompareTo(OracleTimeStampLTZ other)
	{
		if (IsNull || other.IsNull)
		{
			throw new InvalidOperationException("OracleTimeStampLTZ is NULL.");
		}
		return Value.CompareTo(other.Value);
	}

	public bool Equals(OracleTimeStampLTZ other)
	{
		if (m_isNull == other.m_isNull)
		{
			if (!m_isNull)
			{
				if (m_year == other.m_year && m_month == other.m_month && m_day == other.m_day && m_hour == other.m_hour && m_minute == other.m_minute && m_second == other.m_second && m_nanoseconds == other.m_nanoseconds)
				{
					return m_scale == other.m_scale;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	public override bool Equals(object? obj)
	{
		if (obj is OracleTimeStampLTZ other)
		{
			return Equals(other);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return ((((((((((((((((m_isNull ? 1 : 0) * 397) ^ m_year) * 397) ^ m_month) * 397) ^ m_day) * 397) ^ m_hour) * 397) ^ m_minute) * 397) ^ m_second) * 397) ^ m_nanoseconds) * 397) ^ m_scale;
	}

	public static bool operator ==(OracleTimeStampLTZ left, OracleTimeStampLTZ right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(OracleTimeStampLTZ left, OracleTimeStampLTZ right)
	{
		return !left.Equals(right);
	}

	public static bool operator <(OracleTimeStampLTZ left, OracleTimeStampLTZ right)
	{
		return left.CompareTo(right) < 0;
	}

	public static bool operator <=(OracleTimeStampLTZ left, OracleTimeStampLTZ right)
	{
		return left.CompareTo(right) <= 0;
	}

	public static bool operator >(OracleTimeStampLTZ left, OracleTimeStampLTZ right)
	{
		return left.CompareTo(right) > 0;
	}

	public static bool operator >=(OracleTimeStampLTZ left, OracleTimeStampLTZ right)
	{
		return left.CompareTo(right) >= 0;
	}

	public static TimeSpan operator -(OracleTimeStampLTZ left, OracleTimeStampLTZ right)
	{
		if (left.IsNull || right.IsNull)
		{
			throw new InvalidOperationException("OracleTimeStampLTZ is NULL.");
		}
		return left.Value - right.Value;
	}

	public static TimeSpan Subtract(OracleTimeStampLTZ left, OracleTimeStampLTZ right)
	{
		return left - right;
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
		DateTime dateTime = DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
		byte scale = (byte)(text.Contains('.') ? 9u : 0u);
		int nanoseconds = (int)(dateTime.Ticks % 10000000 * 100);
		this = FromWire(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, nanoseconds, scale, TimeSpan.Zero);
	}

	void IXmlSerializable.WriteXml(XmlWriter writer)
	{
		writer.WriteString(ToString());
	}
}
