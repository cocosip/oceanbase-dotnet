using System;
using System.Buffers.Binary;
using System.Data.SqlTypes;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace OceanBase;

/// <summary>
/// Represents an Oracle-mode TIMESTAMP (nanosecond precision) value.
/// </summary>
public struct OracleTimeStamp : IComparable, IComparable<OracleTimeStamp>, IEquatable<OracleTimeStamp>, INullable, IOracleNullable, IXmlSerializable, IConvertible
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

	public static OracleTimeStamp Null => new OracleTimeStamp(isNull: true, 0, 0, 0, 0, 0, 0, 0, 9);

	public bool IsNull => m_isNull;

	public int Year
	{
		get
		{
			if (!IsNull)
			{
				return m_year;
			}
			throw new InvalidOperationException("OracleTimeStamp is NULL.");
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
			throw new InvalidOperationException("OracleTimeStamp is NULL.");
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
			throw new InvalidOperationException("OracleTimeStamp is NULL.");
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
			throw new InvalidOperationException("OracleTimeStamp is NULL.");
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
			throw new InvalidOperationException("OracleTimeStamp is NULL.");
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
			throw new InvalidOperationException("OracleTimeStamp is NULL.");
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
			throw new InvalidOperationException("OracleTimeStamp is NULL.");
		}
	}

	public int Nanosecond => Nanoseconds;

	public int Millisecond
	{
		get
		{
			if (!IsNull)
			{
				return m_nanoseconds / 1000000;
			}
			throw new InvalidOperationException("OracleTimeStamp is NULL.");
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
			throw new InvalidOperationException("OracleTimeStamp is NULL.");
		}
	}

	public static OracleTimeStamp MinValue => new OracleTimeStamp(isNull: false, 1, 1, 1, 0, 0, 0, 0, 9);

	public static OracleTimeStamp MaxValue => new OracleTimeStamp(isNull: false, 9999, 12, 31, 23, 59, 59, 999999999, 9);

	public DateTime Value
	{
		get
		{
			if (!IsNull)
			{
				return ToDateTime();
			}
			throw new InvalidOperationException("OracleTimeStamp is NULL.");
		}
	}

	private OracleTimeStamp(bool isNull, int year, int month, int day, int hour, int minute, int second, int nanoseconds, byte scale)
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

	public OracleTimeStamp(DateTime value)
		: this(isNull: false, value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, (int)(value.Ticks % 10000000 * 100), 9)
	{
	}

	public OracleTimeStamp(int year, int month, int day, int hour, int minute, int second, int nanosecond)
		: this(isNull: false, year, month, day, hour, minute, second, nanosecond, 9)
	{
	}

	internal static OracleTimeStamp FromWire(int year, int month, int day, int hour, int minute, int second, int nanoseconds, int scale)
	{
		if ((uint)scale > 9u)
		{
			throw new ArgumentOutOfRangeException("scale");
		}
		if ((uint)nanoseconds > 999999999u)
		{
			throw new ArgumentOutOfRangeException("nanoseconds");
		}
		return new OracleTimeStamp(isNull: false, year, month, day, hour, minute, second, nanoseconds, (byte)scale);
	}

	public DateTime ToDateTime()
	{
		if (IsNull)
		{
			throw new InvalidOperationException("OracleTimeStamp is NULL.");
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

	public string ToIsoString()
	{
		if (IsNull)
		{
			return "NULL";
		}
		string text = new DateTime(m_year, m_month, m_day, m_hour, m_minute, m_second, DateTimeKind.Unspecified).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
		int nanoseconds = m_nanoseconds;
		string text2 = nanoseconds.ToString("D9", CultureInfo.InvariantCulture);
		return text + "." + text2;
	}

	public static OracleTimeStamp Parse(string s)
	{
		if (string.IsNullOrWhiteSpace(s))
		{
			throw new ArgumentNullException("s");
		}
		DateTime dateTime = DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
		int nanoseconds = (int)(dateTime.Ticks % 10000000 * 100);
		return FromWire(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, nanoseconds, Enumerable.Contains(s, '.') ? 9 : 0);
	}

	public static explicit operator DateTime(OracleTimeStamp value)
	{
		return value.Value;
	}

	TypeCode IConvertible.GetTypeCode()
	{
		return TypeCode.Object;
	}

	bool IConvertible.ToBoolean(IFormatProvider? provider)
	{
		return Value.Ticks != 0;
	}

	byte IConvertible.ToByte(IFormatProvider? provider)
	{
		return Convert.ToByte(Value.Ticks, provider);
	}

	char IConvertible.ToChar(IFormatProvider? provider)
	{
		return Convert.ToChar(Value.Ticks, provider);
	}

	DateTime IConvertible.ToDateTime(IFormatProvider? provider)
	{
		return Value;
	}

	decimal IConvertible.ToDecimal(IFormatProvider? provider)
	{
		return Convert.ToDecimal(Value.Ticks, provider);
	}

	double IConvertible.ToDouble(IFormatProvider? provider)
	{
		return Convert.ToDouble(Value.Ticks, provider);
	}

	short IConvertible.ToInt16(IFormatProvider? provider)
	{
		return Convert.ToInt16(Value.Ticks, provider);
	}

	int IConvertible.ToInt32(IFormatProvider? provider)
	{
		return Convert.ToInt32(Value.Ticks, provider);
	}

	long IConvertible.ToInt64(IFormatProvider? provider)
	{
		return Value.Ticks;
	}

	sbyte IConvertible.ToSByte(IFormatProvider? provider)
	{
		return Convert.ToSByte(Value.Ticks, provider);
	}

	float IConvertible.ToSingle(IFormatProvider? provider)
	{
		return Convert.ToSingle(Value.Ticks, provider);
	}

	string IConvertible.ToString(IFormatProvider? provider)
	{
		return ToString();
	}

	object IConvertible.ToType(Type conversionType, IFormatProvider? provider)
	{
		if (conversionType == typeof(DateTime))
		{
			return Value;
		}
		if (conversionType == typeof(long))
		{
			return Value.Ticks;
		}
		return Convert.ChangeType(Value.Ticks, conversionType, provider);
	}

	ushort IConvertible.ToUInt16(IFormatProvider? provider)
	{
		return Convert.ToUInt16(Value.Ticks, provider);
	}

	uint IConvertible.ToUInt32(IFormatProvider? provider)
	{
		return Convert.ToUInt32(Value.Ticks, provider);
	}

	ulong IConvertible.ToUInt64(IFormatProvider? provider)
	{
		return Convert.ToUInt64(Value.Ticks, provider);
	}

	public int CompareTo(object? obj)
	{
		if (!(obj is OracleTimeStamp other))
		{
			throw new ArgumentException("CompareTo can only be called with another OracleTimeStamp", "obj");
		}
		return CompareTo(other);
	}

	public int CompareTo(OracleTimeStamp other)
	{
		if (IsNull || other.IsNull)
		{
			throw new InvalidOperationException("OracleTimeStamp is NULL.");
		}
		return Value.CompareTo(other.Value);
	}

	public bool Equals(OracleTimeStamp other)
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
		if (obj is OracleTimeStamp other)
		{
			return Equals(other);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return ((((((((((((((((m_isNull ? 1 : 0) * 397) ^ m_year) * 397) ^ m_month) * 397) ^ m_day) * 397) ^ m_hour) * 397) ^ m_minute) * 397) ^ m_second) * 397) ^ m_nanoseconds) * 397) ^ m_scale;
	}

	public static bool operator ==(OracleTimeStamp left, OracleTimeStamp right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(OracleTimeStamp left, OracleTimeStamp right)
	{
		return !left.Equals(right);
	}

	public static bool operator <(OracleTimeStamp left, OracleTimeStamp right)
	{
		return left.CompareTo(right) < 0;
	}

	public static bool operator <=(OracleTimeStamp left, OracleTimeStamp right)
	{
		return left.CompareTo(right) <= 0;
	}

	public static bool operator >(OracleTimeStamp left, OracleTimeStamp right)
	{
		return left.CompareTo(right) > 0;
	}

	public static bool operator >=(OracleTimeStamp left, OracleTimeStamp right)
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
		DateTime dateTime = DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
		byte scale = (byte)(Enumerable.Contains(text, '.') ? 9u : 0u);
		int nanoseconds = (int)(dateTime.Ticks % 10000000 * 100);
		this = FromWire(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, nanoseconds, scale);
	}

	void IXmlSerializable.WriteXml(XmlWriter writer)
	{
		writer.WriteString(ToString());
	}
}
