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
/// Represents an Oracle-mode TIMESTAMP WITH TIME ZONE value.
/// </summary>
public struct OracleTimeStampTZ : IComparable, IComparable<OracleTimeStampTZ>, IEquatable<OracleTimeStampTZ>, INullable, IOracleNullable, IXmlSerializable
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

	private readonly sbyte m_offsetHours;

	private readonly byte m_offsetMinutes;

	private readonly string? m_timeZoneName;

	public static OracleTimeStampTZ Null => new OracleTimeStampTZ(isNull: true, 0, 0, 0, 0, 0, 0, 0, 9, 0, 0, null);

	public bool IsNull => m_isNull;

	public int Year
	{
		get
		{
			if (!IsNull)
			{
				return m_year;
			}
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
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
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
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
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
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
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
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
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
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
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
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
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
		}
	}

	public sbyte OffsetHours
	{
		get
		{
			if (!IsNull)
			{
				return m_offsetHours;
			}
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
		}
	}

	public byte OffsetMinutes
	{
		get
		{
			if (!IsNull)
			{
				return m_offsetMinutes;
			}
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
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
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
		}
	}

	public string TimeZone
	{
		get
		{
			if (!IsNull)
			{
				return GetTimeZone();
			}
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
		}
	}

	public string? TimeZoneName
	{
		get
		{
			if (!IsNull)
			{
				return m_timeZoneName;
			}
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
		}
	}

	public DateTime Value
	{
		get
		{
			if (!IsNull)
			{
				return ToDateTimeOffset().DateTime;
			}
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
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
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
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
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
		}
	}

	private OracleTimeStampTZ(bool isNull, int year, int month, int day, int hour, int minute, int second, int nanoseconds, byte scale, sbyte offsetHours, byte offsetMinutes, string? timeZoneName)
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
		m_offsetHours = offsetHours;
		m_offsetMinutes = offsetMinutes;
		m_timeZoneName = timeZoneName;
	}

	public OracleTimeStampTZ(int year, int month, int day, int hour, int minute, int second, int nanosecond, string timeZone)
		: this(isNull: false, year, month, day, hour, minute, second, nanosecond, 9, GetOffsetHours(timeZone), GetOffsetMinutes(timeZone), timeZone)
	{
	}

	public OracleTimeStampTZ(OracleTimeStamp timeStamp, string timeZone)
		: this(timeStamp.Year, timeStamp.Month, timeStamp.Day, timeStamp.Hour, timeStamp.Minute, timeStamp.Second, timeStamp.Nanoseconds, timeZone)
	{
	}

	internal static OracleTimeStampTZ FromWire(int year, int month, int day, int hour, int minute, int second, int nanoseconds, int scale, sbyte offsetHours, byte offsetMinutes, string? timeZoneName = null)
	{
		if ((uint)scale > 9u)
		{
			throw new ArgumentOutOfRangeException("scale");
		}
		if ((uint)nanoseconds > 999999999u)
		{
			throw new ArgumentOutOfRangeException("nanoseconds");
		}
		if (offsetMinutes > 59)
		{
			throw new ArgumentOutOfRangeException("offsetMinutes");
		}
		return new OracleTimeStampTZ(isNull: false, year, month, day, hour, minute, second, nanoseconds, (byte)scale, offsetHours, offsetMinutes, timeZoneName);
	}

	public TimeSpan GetOffset()
	{
		if (IsNull)
		{
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
		}
		int num = ((m_offsetHours >= 0) ? 1 : (-1));
		return TimeSpan.FromMinutes(m_offsetHours * 60 + num * m_offsetMinutes);
	}

	public int GetTimeZoneHour()
	{
		return OffsetHours;
	}

	public int GetTimeZoneMinute()
	{
		return OffsetMinutes;
	}

	public TimeSpan GetTimeZoneOffset()
	{
		return GetOffset();
	}

	public OracleTimeStamp GetOracleTimeStamp()
	{
		if (!IsNull)
		{
			return OracleTimeStamp.FromWire(m_year, m_month, m_day, m_hour, m_minute, m_second, m_nanoseconds, m_scale);
		}
		throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
	}

	public OracleTimeStampLTZ GetOracleTimeStampLTZ()
	{
		if (!IsNull)
		{
			return OracleTimeStampLTZ.FromWire(m_year, m_month, m_day, m_hour, m_minute, m_second, m_nanoseconds, m_scale, TimeSpan.Zero);
		}
		throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
	}

	public OracleTimeStamp ToLocalTime()
	{
		if (IsNull)
		{
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
		}
		DateTimeOffset dateTimeOffset = ToDateTimeOffset().ToLocalTime();
		return OracleTimeStamp.FromWire(dateTimeOffset.Year, dateTimeOffset.Month, dateTimeOffset.Day, dateTimeOffset.Hour, dateTimeOffset.Minute, dateTimeOffset.Second, (int)(dateTimeOffset.Ticks % 10000000 * 100), 9);
	}

	public OracleTimeStamp ToUniversalTime()
	{
		if (IsNull)
		{
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
		}
		DateTimeOffset dateTimeOffset = ToDateTimeOffset().ToUniversalTime();
		return OracleTimeStamp.FromWire(dateTimeOffset.Year, dateTimeOffset.Month, dateTimeOffset.Day, dateTimeOffset.Hour, dateTimeOffset.Minute, dateTimeOffset.Second, (int)(dateTimeOffset.Ticks % 10000000 * 100), 9);
	}

	public string ToIsoString()
	{
		if (IsNull)
		{
			return "NULL";
		}
		return ToString(CultureInfo.InvariantCulture);
	}

	public string ToString(IFormatProvider provider)
	{
		if (IsNull)
		{
			return "NULL";
		}
		string text = ToDateTimeOffset().DateTime.ToString("yyyy-MM-dd HH:mm:ss", provider);
		string text2 = "";
		if (m_scale != 0)
		{
			int num = 1;
			for (int i = 0; i < 9 - m_scale; i++)
			{
				num *= 10;
			}
			text2 = "." + (m_nanoseconds / num).ToString(provider).PadLeft(m_scale, '0');
		}
		string timeZone = GetTimeZone();
		return text + text2 + " " + timeZone;
	}

	public byte[] ToBytes()
	{
		byte[] obj = new byte[16]
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
			0,
			0,
			0,
			0,
			0
		};
		BinaryPrimitives.WriteInt32LittleEndian(obj.AsSpan(7), m_nanoseconds);
		obj[11] = 9;
		obj[12] = (byte)m_offsetHours;
		obj[13] = m_offsetMinutes;
		obj[14] = 0;
		obj[15] = 0;
		return obj;
	}

	public static OracleTimeStampTZ GetSysDate()
	{
		DateTimeOffset now = DateTimeOffset.Now;
		return FromWire(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, (int)(now.Ticks % 10000000 * 100), 9, (sbyte)now.Offset.Hours, (byte)Math.Abs(now.Offset.Minutes));
	}

	public static OracleTimeStampTZ Parse(string s)
	{
		if (string.IsNullOrWhiteSpace(s))
		{
			throw new ArgumentNullException("s");
		}
		if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
		{
			byte scale = (byte)(Enumerable.Contains(s, '.') ? 9u : 0u);
			int nanoseconds = (int)(result.Ticks % 10000000 * 100);
			return FromWire(result.Year, result.Month, result.Day, result.Hour, result.Minute, result.Second, nanoseconds, scale, (sbyte)result.Offset.Hours, (byte)Math.Abs(result.Offset.Minutes));
		}
		int num = s.LastIndexOf(' ');
		if (num > 0 && num + 1 < s.Length)
		{
			string text = s.Substring(0, num);
			string text2 = s.Substring(num + 1);
			DateTime dateTime = DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
			byte scale2 = (byte)(Enumerable.Contains(text, '.') ? 9u : 0u);
			int nanoseconds2 = (int)(dateTime.Ticks % 10000000 * 100);
			return new OracleTimeStampTZ(isNull: false, dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, nanoseconds2, scale2, GetOffsetHours(text2), GetOffsetMinutes(text2), text2);
		}
		throw new FormatException("Invalid OracleTimeStampTZ string '" + s + "'.");
	}

	public static explicit operator DateTimeOffset(OracleTimeStampTZ value)
	{
		return value.ToDateTimeOffset();
	}

	private static sbyte GetOffsetHours(string timeZone)
	{
		if (timeZone == null)
		{
			return 0;
		}
		if (string.Equals(timeZone, "UTC", StringComparison.OrdinalIgnoreCase))
		{
			return 0;
		}
		if (timeZone.Length == 6 && (timeZone[0] == '+' || timeZone[0] == '-') && timeZone[3] == ':')
		{
			int num = ((timeZone[0] != '-') ? 1 : (-1));
			int num2 = timeZone[1] - 48;
			int num3 = timeZone[2] - 48;
			if ((uint)num2 <= 9u && (uint)num3 <= 9u)
			{
				return (sbyte)(num * (num2 * 10 + num3));
			}
		}
		return 0;
	}

	private static byte GetOffsetMinutes(string timeZone)
	{
		if (timeZone == null)
		{
			return 0;
		}
		if (string.Equals(timeZone, "UTC", StringComparison.OrdinalIgnoreCase))
		{
			return 0;
		}
		if (timeZone.Length == 6 && (timeZone[0] == '+' || timeZone[0] == '-') && timeZone[3] == ':')
		{
			int num = timeZone[4] - 48;
			int num2 = timeZone[5] - 48;
			if ((uint)num <= 9u && (uint)num2 <= 9u)
			{
				return (byte)(num * 10 + num2);
			}
		}
		return 0;
	}

	private string GetTimeZone()
	{
		string timeZoneName = m_timeZoneName;
		if (timeZoneName != null && timeZoneName.Length > 0)
		{
			return m_timeZoneName;
		}
		TimeSpan offset = GetOffset();
		string obj = ((offset < TimeSpan.Zero) ? "-" : "+");
		TimeSpan timeSpan = offset.Duration();
		return obj + timeSpan.Hours.ToString("00", CultureInfo.InvariantCulture) + ":" + timeSpan.Minutes.ToString("00", CultureInfo.InvariantCulture);
	}

	public DateTimeOffset ToDateTimeOffset()
	{
		if (IsNull)
		{
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
		}
		return new DateTimeOffset(new DateTime(m_year, m_month, m_day, m_hour, m_minute, m_second, DateTimeKind.Unspecified).AddTicks(m_nanoseconds / 100), GetOffset());
	}

	public override string ToString()
	{
		if (IsNull)
		{
			return "NULL";
		}
		string text = ToDateTimeOffset().DateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
		string text2 = "";
		if (m_scale != 0)
		{
			int num = 1;
			for (int i = 0; i < 9 - m_scale; i++)
			{
				num *= 10;
			}
			text2 = "." + (m_nanoseconds / num).ToString(CultureInfo.InvariantCulture).PadLeft(m_scale, '0');
		}
		string timeZone = GetTimeZone();
		return text + text2 + " " + timeZone;
	}

	public int CompareTo(object? obj)
	{
		if (!(obj is OracleTimeStampTZ other))
		{
			throw new ArgumentException("CompareTo can only be called with another OracleTimeStampTZ", "obj");
		}
		return CompareTo(other);
	}

	public int CompareTo(OracleTimeStampTZ other)
	{
		if (IsNull || other.IsNull)
		{
			throw new InvalidOperationException("OracleTimeStampTZ is NULL.");
		}
		return ToDateTimeOffset().UtcDateTime.CompareTo(other.ToDateTimeOffset().UtcDateTime);
	}

	public bool Equals(OracleTimeStampTZ other)
	{
		if (m_isNull == other.m_isNull)
		{
			if (!m_isNull)
			{
				if (m_year == other.m_year && m_month == other.m_month && m_day == other.m_day && m_hour == other.m_hour && m_minute == other.m_minute && m_second == other.m_second && m_nanoseconds == other.m_nanoseconds && m_scale == other.m_scale && m_offsetHours == other.m_offsetHours)
				{
					return m_offsetMinutes == other.m_offsetMinutes;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	public override bool Equals(object? obj)
	{
		if (obj is OracleTimeStampTZ other)
		{
			return Equals(other);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return ((((((((((((((((((((m_isNull ? 1 : 0) * 397) ^ m_year) * 397) ^ m_month) * 397) ^ m_day) * 397) ^ m_hour) * 397) ^ m_minute) * 397) ^ m_second) * 397) ^ m_nanoseconds) * 397) ^ m_scale) * 397) ^ m_offsetHours) * 397) ^ m_offsetMinutes;
	}

	public static bool operator ==(OracleTimeStampTZ left, OracleTimeStampTZ right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(OracleTimeStampTZ left, OracleTimeStampTZ right)
	{
		return !left.Equals(right);
	}

	public static bool operator <(OracleTimeStampTZ left, OracleTimeStampTZ right)
	{
		return left.CompareTo(right) < 0;
	}

	public static bool operator <=(OracleTimeStampTZ left, OracleTimeStampTZ right)
	{
		return left.CompareTo(right) <= 0;
	}

	public static bool operator >(OracleTimeStampTZ left, OracleTimeStampTZ right)
	{
		return left.CompareTo(right) > 0;
	}

	public static bool operator >=(OracleTimeStampTZ left, OracleTimeStampTZ right)
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
		DateTimeOffset dateTimeOffset = DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
		byte scale = (byte)(Enumerable.Contains(text, '.') ? 9u : 0u);
		int nanoseconds = (int)(dateTimeOffset.Ticks % 10000000 * 100);
		TimeSpan offset = dateTimeOffset.Offset;
		sbyte offsetHours = (sbyte)offset.Hours;
		byte offsetMinutes = (byte)Math.Abs(offset.Minutes);
		this = FromWire(dateTimeOffset.Year, dateTimeOffset.Month, dateTimeOffset.Day, dateTimeOffset.Hour, dateTimeOffset.Minute, dateTimeOffset.Second, nanoseconds, scale, offsetHours, offsetMinutes);
	}

	void IXmlSerializable.WriteXml(XmlWriter writer)
	{
		writer.WriteString(ToString(CultureInfo.InvariantCulture));
	}
}
