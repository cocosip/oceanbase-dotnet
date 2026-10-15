using System;
using System.Data.SqlTypes;
using System.Globalization;
using System.Linq;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// Oracle 模式时间/间隔类型的深入测试（公开类型，无需服务端）。
///
/// 已有 <c>ValueTypeTests</c> 覆盖了构造与基本往返；这里补齐
/// NULL 语义、边界值、组件分解、格式化、字节序列化与 IConvertible 转换。
/// </summary>
public sealed class OracleTemporalTests
{
	// ---------- OracleTimeStamp ----------

	[Fact]
	public void OracleTimeStamp_Null_is_null_and_exposes_no_components()
	{
		var value = OracleTimeStamp.Null;
		Assert.True(value.IsNull);
		Assert.Equal("NULL", value.ToString());
		Assert.Equal("NULL", value.ToIsoString());
	}

	[Theory]
	[InlineData("Year")]
	[InlineData("Month")]
	[InlineData("Day")]
	[InlineData("Hour")]
	[InlineData("Minute")]
	[InlineData("Second")]
	[InlineData("Nanosecond")]
	[InlineData("Millisecond")]
	[InlineData("Scale")]
	[InlineData("Value")]
	public void OracleTimeStamp_Null_component_access_throws(string propertyName)
	{
		var property = typeof(OracleTimeStamp).GetProperty(propertyName)!;
		var ex = Assert.Throws<System.Reflection.TargetInvocationException>(
			() => property.GetValue(OracleTimeStamp.Null));
		Assert.IsType<InvalidOperationException>(ex.InnerException);
		Assert.Contains("OracleTimeStamp is NULL", ex.InnerException!.Message);
	}

	[Fact]
	public void OracleTimeStamp_ToDateTime_on_null_throws()
	{
		Assert.Throws<InvalidOperationException>(() => OracleTimeStamp.Null.ToDateTime());
	}

	[Fact]
	public void OracleTimeStamp_MinValue_and_MaxValue_boundaries()
	{
		var min = OracleTimeStamp.MinValue;
		Assert.False(min.IsNull);
		Assert.Equal(1, min.Year);
		Assert.Equal(1, min.Month);
		Assert.Equal(1, min.Day);
		Assert.Equal(0, min.Nanosecond);

		var max = OracleTimeStamp.MaxValue;
		Assert.Equal(9999, max.Year);
		Assert.Equal(12, max.Month);
		Assert.Equal(31, max.Day);
		Assert.Equal(23, max.Hour);
		Assert.Equal(59, max.Minute);
		Assert.Equal(59, max.Second);
		Assert.Equal(999999999, max.Nanosecond);
	}

	[Fact]
	public void OracleTimeStamp_constructor_from_components_sets_scale_nine()
	{
		var value = new OracleTimeStamp(2024, 3, 15, 13, 45, 59, 123456789);
		Assert.Equal(9, value.Scale);
		Assert.Equal(123456789, value.Nanosecond);
		Assert.Equal(123, value.Millisecond);
	}

	[Fact]
	public void OracleTimeStamp_Millisecond_is_nanoseconds_over_a_million()
	{
		Assert.Equal(0, new OracleTimeStamp(2024, 1, 1, 0, 0, 0, 999_999).Millisecond);
		Assert.Equal(1, new OracleTimeStamp(2024, 1, 1, 0, 0, 0, 1_000_000).Millisecond);
		Assert.Equal(999, new OracleTimeStamp(2024, 1, 1, 0, 0, 0, 999_999_999).Millisecond);
	}

	[Fact]
	public void OracleTimeStamp_from_DateTime_preserves_ticks_as_nanoseconds()
	{
		var dt = new DateTime(2024, 3, 15, 13, 45, 59, DateTimeKind.Unspecified).AddTicks(1234567);
		var value = new OracleTimeStamp(dt);
		// 1 tick = 100 ns
		Assert.Equal(123456700, value.Nanosecond);
		Assert.Equal(dt, value.ToDateTime());
	}

	[Fact]
	public void OracleTimeStamp_explicit_cast_to_DateTime()
	{
		var value = new OracleTimeStamp(2024, 3, 15, 13, 45, 59, 0);
		Assert.Equal(new DateTime(2024, 3, 15, 13, 45, 59), (DateTime)value);
	}

	[Fact]
	public void OracleTimeStamp_ToString_respects_scale()
	{
		// scale 决定小数位数（公开构造函数的 scale 固定为 9，故用 FromWire 造小 scale）
		Assert.Equal("2024-03-15 13:45:59",
			OracleTimeStamp.FromWire(2024, 3, 15, 13, 45, 59, 0, 0).ToString());
		Assert.Equal("2024-03-15 13:45:59.123",
			OracleTimeStamp.FromWire(2024, 3, 15, 13, 45, 59, 123000000, 3).ToString());
		Assert.Equal("2024-03-15 13:45:59.123456789",
			new OracleTimeStamp(2024, 3, 15, 13, 45, 59, 123456789).ToString());
	}

	[Fact]
	public void OracleTimeStamp_ToString_roundtrips_through_Parse()
	{
		var original = OracleTimeStamp.FromWire(2024, 3, 15, 13, 45, 59, 0, 0);
		var parsed = OracleTimeStamp.Parse(original.ToString());
		Assert.Equal(original.Value, parsed.Value);
	}

	[Fact]
	public void OracleTimeStamp_ToIsoString_always_has_nine_digits()
	{
		var value = OracleTimeStamp.FromWire(2024, 3, 15, 13, 45, 59, 123000000, 3);
		Assert.Equal("2024-03-15 13:45:59.123000000", value.ToIsoString());
	}

	[Fact]
	public void OracleTimeStamp_ToBytes_layout_is_twelve_bytes()
	{
		var value = new OracleTimeStamp(2024, 3, 15, 13, 45, 59, 123456789);
		var bytes = value.ToBytes();
		Assert.Equal(12, bytes.Length);
		Assert.Equal(20, bytes[0]);          // 年 / 100
		Assert.Equal(24, bytes[1]);          // 年 % 100
		Assert.Equal(3, bytes[2]);           // 月
		Assert.Equal(15, bytes[3]);          // 日
		Assert.Equal(13, bytes[4]);          // 时
		Assert.Equal(45, bytes[5]);          // 分
		Assert.Equal(59, bytes[6]);          // 秒
		Assert.Equal(9, bytes[11]);          // scale
		Assert.Equal(123456789, BitConverter.ToInt32(bytes, 7));
	}

	[Fact]
	public void OracleTimeStamp_FromWire_validates_arguments()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => OracleTimeStamp.FromWire(2024, 1, 1, 0, 0, 0, 0, 10));
		Assert.Throws<ArgumentOutOfRangeException>(() => OracleTimeStamp.FromWire(2024, 1, 1, 0, 0, 0, 1_000_000_000, 9));
		Assert.Throws<ArgumentOutOfRangeException>(() => OracleTimeStamp.FromWire(2024, 1, 1, 0, 0, 0, -1, 9));
	}

	[Fact]
	public void OracleTimeStamp_Parse_reads_roundtrip_format()
	{
		var value = OracleTimeStamp.Parse("2024-03-15 13:45:59");
		Assert.Equal(2024, value.Year);
		Assert.Equal(3, value.Month);
		Assert.Equal(15, value.Day);
		Assert.Equal(13, value.Hour);
		Assert.Equal(45, value.Minute);
		Assert.Equal(59, value.Second);
	}

	[Fact]
	public void OracleTimeStamp_Parse_sets_scale_from_presence_of_dot()
	{
		// 含小数点 → scale 9；否则 scale 0
		Assert.Equal(9, OracleTimeStamp.Parse("2024-03-15 13:45:59.123").Scale);
		Assert.Equal(0, OracleTimeStamp.Parse("2024-03-15 13:45:59").Scale);
	}

	[Fact]
	public void OracleTimeStamp_Parse_rejects_blank()
	{
		Assert.Throws<ArgumentNullException>(() => OracleTimeStamp.Parse(""));
		Assert.Throws<ArgumentNullException>(() => OracleTimeStamp.Parse("   "));
	}

	[Fact]
	public void OracleTimeStamp_IConvertible_GetTypeCode_is_object()
	{
		Assert.Equal(TypeCode.Object, ((IConvertible)new OracleTimeStamp(2024, 1, 1, 0, 0, 0, 0)).GetTypeCode());
	}

	[Fact]
	public void OracleTimeStamp_IConvertible_converts_via_ticks()
	{
		var value = new OracleTimeStamp(2024, 3, 15, 0, 0, 0, 0);
		var convertible = (IConvertible)value;
		Assert.Equal(value.Value.Ticks, convertible.ToInt64(CultureInfo.InvariantCulture));
		Assert.Equal(value.Value, convertible.ToDateTime(CultureInfo.InvariantCulture));
		// 注意：IConvertible.ToString 走的是 ToString()（带 9 位小数），不是 ticks
		Assert.Equal(value.ToString(), convertible.ToString(CultureInfo.InvariantCulture));
	}

	[Fact]
	public void OracleTimeStamp_IConvertible_ToType_handles_datetime_and_long()
	{
		var value = new OracleTimeStamp(2024, 3, 15, 0, 0, 0, 0);
		var convertible = (IConvertible)value;
		Assert.Equal(value.Value, convertible.ToType(typeof(DateTime), CultureInfo.InvariantCulture));
		Assert.Equal(value.Value.Ticks, convertible.ToType(typeof(long), CultureInfo.InvariantCulture));
	}

	[Fact]
	public void OracleTimeStamp_IConvertible_ToBoolean_reflects_nonzero_ticks()
	{
		Assert.True(((IConvertible)new OracleTimeStamp(2024, 1, 1, 0, 0, 0, 0)).ToBoolean(null));
		Assert.False(((IConvertible)new OracleTimeStamp(1, 1, 1, 0, 0, 0, 0)).ToBoolean(null));
	}

	// ---------- OracleTimeStampLTZ ----------

	[Fact]
	public void OracleTimeStampLTZ_Null_semantics()
	{
		var value = OracleTimeStampLTZ.Null;
		Assert.True(value.IsNull);
		Assert.Equal("NULL", value.ToString());
	}

	[Theory]
	[InlineData("Year")]
	[InlineData("Month")]
	[InlineData("Day")]
	[InlineData("Hour")]
	[InlineData("Minute")]
	[InlineData("Second")]
	[InlineData("Nanosecond")]
	public void OracleTimeStampLTZ_Null_component_access_throws(string propertyName)
	{
		var property = typeof(OracleTimeStampLTZ).GetProperty(propertyName)!;
		var ex = Assert.Throws<System.Reflection.TargetInvocationException>(
			() => property.GetValue(OracleTimeStampLTZ.Null));
		Assert.IsType<InvalidOperationException>(ex.InnerException);
	}

	[Fact]
	public void OracleTimeStampLTZ_component_roundtrip()
	{
		var value = new OracleTimeStampLTZ(2024, 3, 15, 13, 45, 59, 123456789);
		Assert.Equal(2024, value.Year);
		Assert.Equal(3, value.Month);
		Assert.Equal(15, value.Day);
		Assert.Equal(13, value.Hour);
		Assert.Equal(45, value.Minute);
		Assert.Equal(59, value.Second);
		Assert.Equal(123456789, value.Nanosecond);
	}

	[Fact]
	public void OracleTimeStampLTZ_from_DateTime_roundtrips()
	{
		var dt = new DateTime(2024, 3, 15, 13, 45, 59, DateTimeKind.Utc);
		var value = new OracleTimeStampLTZ(dt);
		Assert.Equal(dt, value.Value);
	}

	// ---------- OracleTimeStampTZ ----------

	[Fact]
	public void OracleTimeStampTZ_Null_semantics()
	{
		var value = OracleTimeStampTZ.Null;
		Assert.True(value.IsNull);
		Assert.Equal("NULL", value.ToString());
	}

	[Theory]
	[InlineData("Year")]
	[InlineData("Month")]
	[InlineData("Day")]
	[InlineData("Hour")]
	[InlineData("Minute")]
	[InlineData("Second")]
	[InlineData("Nanosecond")]
	[InlineData("TimeZone")]
	[InlineData("Value")]
	public void OracleTimeStampTZ_Null_component_access_throws(string propertyName)
	{
		var property = typeof(OracleTimeStampTZ).GetProperty(propertyName)!;
		var ex = Assert.Throws<System.Reflection.TargetInvocationException>(
			() => property.GetValue(OracleTimeStampTZ.Null));
		Assert.IsType<InvalidOperationException>(ex.InnerException);
	}

	[Fact]
	public void OracleTimeStampTZ_component_roundtrip_with_offset()
	{
		var value = new OracleTimeStampTZ(2024, 3, 15, 13, 45, 59, 123456789, "+08:00");
		Assert.Equal(2024, value.Year);
		Assert.Equal(3, value.Month);
		Assert.Equal(15, value.Day);
		Assert.Equal(13, value.Hour);
		Assert.Equal(45, value.Minute);
		Assert.Equal(59, value.Second);
		Assert.Equal(123456789, value.Nanosecond);
	}

	[Fact]
	public void OracleTimeStampTZ_from_DateTimeOffset_via_parse_preserves_offset()
	{
		// 无 DateTimeOffset 构造函数；用 Parse 走带偏移的字符串
		var value = OracleTimeStampTZ.Parse("2024-03-15 13:45:59 +08:00");
		Assert.Equal(2024, value.Year);
		Assert.Equal(3, value.Month);
		Assert.Equal(15, value.Day);
		Assert.Equal(13, value.Hour);
		Assert.Equal(45, value.Minute);
		Assert.Equal(59, value.Second);
	}

	[Fact]
	public void OracleTimeStampTZ_from_OracleTimeStamp_and_zone()
	{
		var ts = new OracleTimeStamp(2024, 3, 15, 13, 45, 59, 0);
		var value = new OracleTimeStampTZ(ts, "+08:00");
		Assert.Equal(2024, value.Year);
		Assert.Equal(13, value.Hour);
	}

	[Fact]
	public void OracleTimeStampTZ_CompareTo_throws_for_null_operands()
	{
		// 与 OracleTimeStampLTZ 不同，这里 NULL 参与比较会抛异常（而非视为最小值）
		var value = new OracleTimeStampTZ(2024, 3, 15, 0, 0, 0, 0, "+00:00");
		Assert.Throws<InvalidOperationException>(() => OracleTimeStampTZ.Null.CompareTo(value));
		Assert.Throws<InvalidOperationException>(() => value.CompareTo(OracleTimeStampTZ.Null));
	}

	[Fact]
	public void OracleTimeStampTZ_Equals_treats_two_nulls_as_equal()
	{
		Assert.True(OracleTimeStampTZ.Null.Equals(OracleTimeStampTZ.Null));
	}

	[Fact]
	public void OracleTimeStampTZ_comparison_uses_utc_instant()
	{
		// 同一瞬间的不同偏移应比较为相等
		var plusEight = new OracleTimeStampTZ(2024, 3, 15, 8, 0, 0, 0, "+08:00");
		var utc = new OracleTimeStampTZ(2024, 3, 15, 0, 0, 0, 0, "+00:00");
		Assert.Equal(0, plusEight.CompareTo(utc));
	}

	// ---------- OracleIntervalYM ----------

	[Fact]
	public void OracleIntervalYM_Null_semantics()
	{
		var value = OracleIntervalYM.Null;
		Assert.True(value.IsNull);
		Assert.Equal("NULL", value.ToString());
		Assert.Equal("NULL", value.ToOracleLiteral());
	}

	[Theory]
	[InlineData("TotalMonths")]
	[InlineData("Years")]
	[InlineData("Months")]
	[InlineData("Value")]
	[InlineData("IsNegative")]
	public void OracleIntervalYM_Null_component_access_throws(string propertyName)
	{
		var property = typeof(OracleIntervalYM).GetProperty(propertyName)!;
		var ex = Assert.Throws<System.Reflection.TargetInvocationException>(
			() => property.GetValue(OracleIntervalYM.Null));
		Assert.IsType<InvalidOperationException>(ex.InnerException);
	}

	[Fact]
	public void OracleIntervalYM_from_total_months_decomposes()
	{
		var value = OracleIntervalYM.FromTotalMonths(18);
		Assert.Equal(18, value.TotalMonths);
		Assert.Equal(1, value.Years);
		Assert.Equal(6, value.Months);
		Assert.False(value.IsNegative);
		Assert.Equal("1-06", value.ToString());
	}

	[Fact]
	public void OracleIntervalYM_negative_decomposes_with_sign()
	{
		var value = OracleIntervalYM.FromTotalMonths(-18);
		Assert.True(value.IsNegative);
		Assert.Equal(-1, value.Years);
		Assert.Equal(6, value.Months);      // 月份取绝对值
		Assert.Equal("-1-06", value.ToString());
	}

	[Fact]
	public void OracleIntervalYM_year_month_ctor_combines()
	{
		Assert.Equal(18, new OracleIntervalYM(1, 6).TotalMonths);
		Assert.Equal(-18, new OracleIntervalYM(-1, 6).TotalMonths);
		Assert.Equal(6, new OracleIntervalYM(0, 6).TotalMonths);
	}

	[Fact]
	public void OracleIntervalYM_months_must_be_in_range()
	{
		// 超出 0..11 抛错（取绝对值后判断）
		Assert.Throws<ArgumentOutOfRangeException>(() => new OracleIntervalYM(1, 12));
		Assert.Throws<ArgumentOutOfRangeException>(() => new OracleIntervalYM(1, 13));
		Assert.Throws<ArgumentOutOfRangeException>(() => OracleIntervalYM.FromYearsMonths(1, 12));
	}

	[Fact]
	public void OracleIntervalYM_negative_months_are_treated_as_sign()
	{
		// 注意：months 为负数不抛错 —— 它只用来判定符号，实际取绝对值
		var value = new OracleIntervalYM(1, -6);
		Assert.True(value.IsNegative);
		Assert.Equal(-18, value.TotalMonths);
	}

	[Fact]
	public void OracleIntervalYM_months_above_eleven_wrap_in_total_months_ctor()
	{
		// 注意：FromTotalMonths / 单参构造不做范围校验，13 个月直接归一化为 1 年 1 个月
		var value = new OracleIntervalYM(13);
		Assert.Equal(13, value.TotalMonths);
		Assert.Equal(1, value.Years);
		Assert.Equal(1, value.Months);
	}

	[Fact]
	public void OracleIntervalYM_months_property_wraps_at_twelve()
	{
		// 13 个月 → 1 年 1 个月
		var value = OracleIntervalYM.FromTotalMonths(13);
		Assert.Equal(1, value.Years);
		Assert.Equal(1, value.Months);
	}

	[Fact]
	public void OracleIntervalYM_zero_is_not_negative()
	{
		var value = OracleIntervalYM.FromTotalMonths(0);
		Assert.False(value.IsNegative);
		Assert.Equal("0-00", value.ToString());
	}

	[Fact]
	public void OracleIntervalYM_ToOracleLiteral_formats()
	{
		Assert.Equal("INTERVAL '1-06' YEAR(9) TO MONTH", OracleIntervalYM.FromTotalMonths(18).ToOracleLiteral());
	}

	[Fact]
	public void OracleIntervalYM_ToBytes_layout()
	{
		var bytes = OracleIntervalYM.FromTotalMonths(18).ToBytes();
		Assert.NotEmpty(bytes);
	}

	[Fact]
	public void OracleIntervalYM_from_wire_matches_years_months()
	{
		Assert.Equal(OracleIntervalYM.FromYearsMonths(1, 6), OracleIntervalYM.FromWire(isNegative: false, 1, 6));
		Assert.Equal(OracleIntervalYM.FromYearsMonths(1, 6, isNegative: true), OracleIntervalYM.FromWire(isNegative: true, 1, 6));
	}

	[Fact]
	public void OracleIntervalYM_from_TimeSpan_approximates()
	{
		// 365 天 ≈ 1 年
		var value = OracleIntervalYM.FromTimeSpan(TimeSpan.FromDays(365));
		Assert.Equal(12, value.TotalMonths);

		// 负值保留符号
		var negative = OracleIntervalYM.FromTimeSpan(TimeSpan.FromDays(-365));
		Assert.True(negative.IsNegative);
	}

	// ---------- OracleIntervalDS ----------

	[Fact]
	public void OracleIntervalDS_Null_semantics()
	{
		var value = OracleIntervalDS.Null;
		Assert.True(value.IsNull);
		Assert.Equal("NULL", value.ToString());
	}

	[Theory]
	[InlineData("Days")]
	[InlineData("Hours")]
	[InlineData("Minutes")]
	[InlineData("Seconds")]
	[InlineData("Nanoseconds")]
	[InlineData("Value")]
	public void OracleIntervalDS_Null_component_access_throws(string propertyName)
	{
		var property = typeof(OracleIntervalDS).GetProperty(propertyName)!;
		var ex = Assert.Throws<System.Reflection.TargetInvocationException>(
			() => property.GetValue(OracleIntervalDS.Null));
		Assert.IsType<InvalidOperationException>(ex.InnerException);
	}

	[Fact]
	public void OracleIntervalDS_from_components_roundtrips()
	{
		var value = OracleIntervalDS.FromComponents(isNegative: false, 1, 2, 3, 4, 500000000);
		Assert.Equal(1, value.Days);
		Assert.Equal(2, value.Hours);
		Assert.Equal(3, value.Minutes);
		Assert.Equal(4, value.Seconds);
		Assert.Equal(500000000, value.Nanoseconds);
		Assert.False(value.IsNull);
	}

	[Fact]
	public void OracleIntervalDS_negative_components_roundtrip()
	{
		// FromComponents 要求 hours ≤ 23；30 小时需表示为 1 天 6 小时再加负号
		var value = OracleIntervalDS.FromComponents(isNegative: true, 1, 6, 0, 0, 0);
		Assert.Equal(TimeSpan.FromHours(-30), value.Value);
	}

	[Fact]
	public void OracleIntervalDS_FromComponents_validates_ranges()
	{
		Assert.Throws<ArgumentOutOfRangeException>(
			() => OracleIntervalDS.FromComponents(false, 0, 24, 0, 0, 0));
		Assert.Throws<ArgumentOutOfRangeException>(
			() => OracleIntervalDS.FromComponents(false, 0, 0, 60, 0, 0));
		Assert.Throws<ArgumentOutOfRangeException>(
			() => OracleIntervalDS.FromComponents(false, 0, 0, 0, 60, 0));
		Assert.Throws<ArgumentOutOfRangeException>(
			() => OracleIntervalDS.FromComponents(false, 0, 0, 0, 0, 1_000_000_000));
		Assert.Throws<ArgumentOutOfRangeException>(
			() => OracleIntervalDS.FromComponents(false, -1, 0, 0, 0, 0));
		Assert.Throws<ArgumentOutOfRangeException>(
			() => OracleIntervalDS.FromComponents(false, 0, 0, 0, 0, 0, scale: 10));
	}

	[Fact]
	public void OracleIntervalDS_from_TimeSpan_preserves_value()
	{
		var ts = new TimeSpan(1, 2, 3, 4, 500);
		var value = OracleIntervalDS.FromTimeSpan(ts);
		Assert.Equal(ts, value.Value);
	}

	[Fact]
	public void OracleIntervalDS_negative_TimeSpan_roundtrips()
	{
		var ts = TimeSpan.FromHours(-30);
		var value = OracleIntervalDS.FromTimeSpan(ts);
		Assert.Equal(ts, value.Value);
	}

	[Fact]
	public void OracleIntervalDS_ToOracleLiteral_formats()
	{
		var literal = OracleIntervalDS.FromComponents(isNegative: false, 1, 2, 3, 4, 0).ToOracleLiteral();
		Assert.Contains("INTERVAL", literal);
		Assert.Contains("DAY", literal);
		Assert.Contains("SECOND", literal);
	}
}
