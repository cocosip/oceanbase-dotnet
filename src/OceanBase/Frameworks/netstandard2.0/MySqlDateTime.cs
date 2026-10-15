using System;

namespace OceanBase;

/// <summary>
/// Represents a MySQL date/time value. This type can be used to store <c>DATETIME</c> values such
/// as <c>0000-00-00</c> that can be stored in MySQL (when <see cref="P:OceanBase.MySqlConnectionStringBuilder.AllowZeroDateTime" />
/// is true) but can't be stored in a <see cref="T:System.DateTime" /> value.
/// </summary>
/// <param name="year">The year.</param>
/// <param name="month">The (one-based) month.</param>
/// <param name="day">The (one-based) day of the month.</param>
/// <param name="hour">The hour.</param>
/// <param name="minute">The minute.</param>
/// <param name="second">The second.</param>
/// <param name="microsecond">The microsecond.</param>
public struct MySqlDateTime(int year, int month, int day, int hour, int minute, int second, int microsecond) : IComparable, IComparable<MySqlDateTime>, IConvertible, IEquatable<MySqlDateTime>
{
	/// <summary>
	/// Returns <c>true</c> if this value is a valid <see cref="T:System.DateTime" />.
	/// </summary>
	public readonly bool IsValidDateTime
	{
		get
		{
			if (Year != 0 && Month != 0)
			{
				return Day != 0;
			}
			return false;
		}
	}

	/// <summary>
	/// Gets or sets the year.
	/// </summary>
	public int Year { get; set; } = year;

	/// <summary>
	/// Gets or sets the month.
	/// </summary>
	public int Month { get; set; } = month;

	/// <summary>
	/// Gets or sets the day of the month.
	/// </summary>
	public int Day { get; set; } = day;

	/// <summary>
	/// Gets or sets the hour.
	/// </summary>
	public int Hour { get; set; } = hour;

	/// <summary>
	/// Gets or sets the minute.
	/// </summary>
	public int Minute { get; set; } = minute;

	/// <summary>
	/// Gets or sets the second.
	/// </summary>
	public int Second { get; set; } = second;

	/// <summary>
	/// Gets or sets the microseconds.
	/// </summary>
	public int Microsecond { get; set; } = microsecond;

	/// <summary>
	/// Gets or sets the milliseconds.
	/// </summary>
	public int Millisecond
	{
		readonly get
		{
			return Microsecond / 1000;
		}
		set
		{
			Microsecond = value * 1000;
		}
	}

	/// <summary>
	/// Initializes a new instance of <see cref="T:OceanBase.MySqlDateTime" /> from a <see cref="T:System.DateTime" />.
	/// </summary>
	/// <param name="dt">The <see cref="T:System.DateTime" /> whose values will be copied.</param>
	public MySqlDateTime(DateTime dt)
		: this(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, (int)(dt.Ticks % 10000000) / 10)
	{
	}

	/// <summary>
	/// Initializes a new instance of <see cref="T:OceanBase.MySqlDateTime" /> from another <see cref="T:OceanBase.MySqlDateTime" />.
	/// </summary>
	/// <param name="other">The <see cref="T:OceanBase.MySqlDateTime" /> whose values will be copied.</param>
	public MySqlDateTime(MySqlDateTime other)
		: this(other.Year, other.Month, other.Day, other.Hour, other.Minute, other.Second, other.Microsecond)
	{
	}

	/// <summary>
	/// Returns a <see cref="T:System.DateTime" /> value (if <see cref="P:OceanBase.MySqlDateTime.IsValidDateTime" /> is <c>true</c>), or throws a
	/// <see cref="T:OceanBase.MySqlConversionException" />.
	/// </summary>
	public readonly DateTime GetDateTime()
	{
		if (IsValidDateTime)
		{
			return new DateTime(Year, Month, Day, Hour, Minute, Second, DateTimeKind.Unspecified).AddTicks(Microsecond * 10);
		}
		throw new MySqlConversionException("Cannot convert MySqlDateTime to DateTime when IsValidDateTime is false.");
	}

	/// <summary>
	/// Converts this object to a <see cref="T:System.String" />.
	/// </summary>
	public override readonly string ToString()
	{
		if (!IsValidDateTime)
		{
			return "0000-00-00";
		}
		return GetDateTime().ToString();
	}

	/// <summary>
	/// Converts this object to a <see cref="T:System.DateTime" />.
	/// </summary>
	public static explicit operator DateTime(MySqlDateTime val)
	{
		if (val.IsValidDateTime)
		{
			return val.GetDateTime();
		}
		return DateTime.MinValue;
	}

	/// <summary>
	/// Returns <c>true</c> if this <see cref="T:OceanBase.MySqlDateTime" /> is equal to <paramref name="obj" />.
	/// </summary>
	/// <param name="obj">The object to compare against for equality.</param>
	/// <returns><c>true</c> if the objects are equal, otherwise <c>false</c>.</returns>
	public override bool Equals(object? obj)
	{
		if (obj is MySqlDateTime other)
		{
			return ((IEquatable<MySqlDateTime>)this).Equals(other);
		}
		return false;
	}

	/// <summary>
	/// Returns a hash code for this instance.
	/// </summary>
	public override int GetHashCode()
	{
		return (((((((((((Year * 33) ^ Month) * 33) ^ Day) * 33) ^ Hour) * 33) ^ Minute) * 33) ^ Second) * 33) ^ Microsecond;
	}

	public static bool operator ==(MySqlDateTime left, MySqlDateTime right)
	{
		return ((IComparable<MySqlDateTime>)left).CompareTo(right) == 0;
	}

	public static bool operator !=(MySqlDateTime left, MySqlDateTime right)
	{
		return ((IComparable<MySqlDateTime>)left).CompareTo(right) != 0;
	}

	public static bool operator <(MySqlDateTime left, MySqlDateTime right)
	{
		return ((IComparable<MySqlDateTime>)left).CompareTo(right) < 0;
	}

	public static bool operator <=(MySqlDateTime left, MySqlDateTime right)
	{
		return ((IComparable<MySqlDateTime>)left).CompareTo(right) <= 0;
	}

	public static bool operator >(MySqlDateTime left, MySqlDateTime right)
	{
		return ((IComparable<MySqlDateTime>)left).CompareTo(right) > 0;
	}

	public static bool operator >=(MySqlDateTime left, MySqlDateTime right)
	{
		return ((IComparable<MySqlDateTime>)left).CompareTo(right) >= 0;
	}

	/// <summary>
	/// Compares this object to another <see cref="T:OceanBase.MySqlDateTime" />.
	/// </summary>
	/// <param name="obj">The object to compare to.</param>
	/// <returns>An <see cref="T:System.Int32" /> giving the results of the comparison: a negative value if this
	/// object is less than <paramref name="obj" />, zero if this object is equal, or a positive value if this
	/// object is greater.</returns>
	readonly int IComparable.CompareTo(object? obj)
	{
		if (!(obj is MySqlDateTime other))
		{
			throw new ArgumentException("CompareTo can only be called with another MySqlDateTime", "obj");
		}
		return ((IComparable<MySqlDateTime>)this).CompareTo(other);
	}

	/// <summary>
	/// Compares this object to another <see cref="T:OceanBase.MySqlDateTime" />.
	/// </summary>
	/// <param name="other">The <see cref="T:OceanBase.MySqlDateTime" /> to compare to.</param>
	/// <returns>An <see cref="T:System.Int32" /> giving the results of the comparison: a negative value if this
	/// object is less than <paramref name="other" />, zero if this object is equal, or a positive value if this
	/// object is greater.</returns>
	readonly int IComparable<MySqlDateTime>.CompareTo(MySqlDateTime other)
	{
		if (Year < other.Year)
		{
			return -1;
		}
		if (Year > other.Year)
		{
			return 1;
		}
		if (Month < other.Month)
		{
			return -1;
		}
		if (Month > other.Month)
		{
			return 1;
		}
		if (Day < other.Day)
		{
			return -1;
		}
		if (Day > other.Day)
		{
			return 1;
		}
		if (Hour < other.Hour)
		{
			return -1;
		}
		if (Hour > other.Hour)
		{
			return 1;
		}
		if (Minute < other.Minute)
		{
			return -1;
		}
		if (Minute > other.Minute)
		{
			return 1;
		}
		if (Second < other.Second)
		{
			return -1;
		}
		if (Second > other.Second)
		{
			return 1;
		}
		return Microsecond.CompareTo(other.Microsecond);
	}

	readonly bool IEquatable<MySqlDateTime>.Equals(MySqlDateTime other)
	{
		return ((IComparable<MySqlDateTime>)this).CompareTo(other) == 0;
	}

	DateTime IConvertible.ToDateTime(IFormatProvider? provider)
	{
		if (!IsValidDateTime)
		{
			throw new InvalidCastException();
		}
		return GetDateTime();
	}

	string IConvertible.ToString(IFormatProvider? provider)
	{
		if (!IsValidDateTime)
		{
			return "0000-00-00";
		}
		return GetDateTime().ToString(provider);
	}

	object IConvertible.ToType(Type conversionType, IFormatProvider? provider)
	{
		if (!(conversionType == typeof(DateTime)))
		{
			if (!(conversionType == typeof(string)))
			{
				throw new InvalidCastException();
			}
			return ((IConvertible)this).ToString(provider);
		}
		return GetDateTime();
	}

	TypeCode IConvertible.GetTypeCode()
	{
		return TypeCode.Object;
	}

	bool IConvertible.ToBoolean(IFormatProvider? provider)
	{
		throw new InvalidCastException();
	}

	char IConvertible.ToChar(IFormatProvider? provider)
	{
		throw new InvalidCastException();
	}

	sbyte IConvertible.ToSByte(IFormatProvider? provider)
	{
		throw new InvalidCastException();
	}

	byte IConvertible.ToByte(IFormatProvider? provider)
	{
		throw new InvalidCastException();
	}

	short IConvertible.ToInt16(IFormatProvider? provider)
	{
		throw new InvalidCastException();
	}

	ushort IConvertible.ToUInt16(IFormatProvider? provider)
	{
		throw new InvalidCastException();
	}

	int IConvertible.ToInt32(IFormatProvider? provider)
	{
		throw new InvalidCastException();
	}

	uint IConvertible.ToUInt32(IFormatProvider? provider)
	{
		throw new InvalidCastException();
	}

	long IConvertible.ToInt64(IFormatProvider? provider)
	{
		throw new InvalidCastException();
	}

	ulong IConvertible.ToUInt64(IFormatProvider? provider)
	{
		throw new InvalidCastException();
	}

	float IConvertible.ToSingle(IFormatProvider? provider)
	{
		throw new InvalidCastException();
	}

	double IConvertible.ToDouble(IFormatProvider? provider)
	{
		throw new InvalidCastException();
	}

	decimal IConvertible.ToDecimal(IFormatProvider? provider)
	{
		throw new InvalidCastException();
	}
}
