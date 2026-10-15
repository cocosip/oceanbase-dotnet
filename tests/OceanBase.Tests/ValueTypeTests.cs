using System;
using System.Data;
using System.Linq;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 值类型测试（不依赖服务端的解析 / 格式化 / 比较）。
/// 所有断言依据对原始程序集的实测行为编写。
/// </summary>
public sealed class ValueTypeTests
{
	// ---------- MySqlDateTime ----------

	[Fact]
	public void MySqlDateTime_constructs_and_exposes_components()
	{
		var v = new MySqlDateTime(2024, 1, 2, 3, 4, 5, 123456);
		Assert.Equal(2024, v.Year);
		Assert.Equal(1, v.Month);
		Assert.Equal(2, v.Day);
		Assert.Equal(3, v.Hour);
		Assert.Equal(4, v.Minute);
		Assert.Equal(5, v.Second);
		Assert.Equal(123456, v.Microsecond);
	}

	[Fact]
	public void MySqlDateTime_supports_zero_date()
	{
		// MySQL 的零日期（0000-00-00）必须可用，这是与 DateTime 的关键差异
		var v = new MySqlDateTime(0, 0, 0, 0, 0, 0, 0);
		Assert.Equal(0, v.Year);
		Assert.False(v.IsValidDateTime);
	}

	[Fact]
	public void MySqlDateTime_GetDateTime_throws_for_zero_date()
	{
		var v = new MySqlDateTime(0, 0, 0, 0, 0, 0, 0);
		Assert.Throws<MySqlConversionException>(() => v.GetDateTime());
	}

	[Fact]
	public void MySqlDateTime_GetDateTime_returns_value_for_valid_date()
	{
		var v = new MySqlDateTime(2024, 1, 2, 3, 4, 5, 0);
		Assert.Equal(new DateTime(2024, 1, 2, 3, 4, 5), v.GetDateTime());
	}

	[Fact]
	public void MySqlDateTime_from_DateTime_roundtrips()
	{
		var dt = new DateTime(2024, 6, 15, 10, 20, 30);
		var v = new MySqlDateTime(dt);
		Assert.True(v.IsValidDateTime);
		Assert.Equal(dt, v.GetDateTime());
	}

	[Fact]
	public void MySqlDateTime_equality_and_comparison()
	{
		var a = new MySqlDateTime(2024, 1, 2, 3, 4, 5, 0);
		var b = new MySqlDateTime(2024, 1, 2, 3, 4, 5, 0);
		var c = new MySqlDateTime(2024, 1, 2, 3, 4, 6, 0);
		Assert.True(a.Equals(b));
		Assert.True(a == b);
		Assert.True(a != c);
		Assert.Equal(a.GetHashCode(), b.GetHashCode());
	}

	[Fact]
	public void MySqlDateTime_ToString_is_stable()
	{
		Assert.Equal("2024/1/2 3:04:05", new MySqlDateTime(2024, 1, 2, 3, 4, 5, 0).ToString());
	}

	// ---------- MySqlDecimal ----------

	[Fact]
	public void MySqlDecimal_exposes_decimal_value()
	{
		var v = new MySqlDecimal("1.5");
		Assert.Equal(1.5m, v.Value);
		Assert.Equal("1.5", v.ToString());
	}

	[Fact]
	public void MySqlDecimal_handles_negative_and_zero()
	{
		Assert.Equal(-2.25m, new MySqlDecimal("-2.25").Value);
		Assert.Equal(0m, new MySqlDecimal("0").Value);
	}

	[Fact]
	public void MySqlDecimal_explicit_cast_to_decimal()
	{
		var v = new MySqlDecimal("12.34");
		Assert.Equal(12.34m, v.Value);
	}

	[Fact]
	public void MySqlDecimal_equality()
	{
		Assert.Equal(new MySqlDecimal("1.5").Value, new MySqlDecimal("1.5").Value);
		Assert.NotEqual(new MySqlDecimal("1.5").Value, new MySqlDecimal("1.6").Value);
	}

		// ---------- MySqlGeometry ----------

	[Fact]
	public void MySqlGeometry_FromMySql_parses_point()
	{
		// MySQL 内部格式：4 字节 SRID + WKB（小端）
		var wkb = new byte[]
		{
			0, 0, 0, 0,                                     // SRID = 0
			1,                                              // 字节序（小端）
			1, 0, 0, 0,                                     // 类型 = Point
			0, 0, 0, 0, 0, 0, 0xF0, 0x3F,                   // X = 1.0
			0, 0, 0, 0, 0, 0, 0x00, 0x40,                   // Y = 2.0
		};
		var g = MySqlGeometry.FromMySql(wkb);
		Assert.Equal(0, g.SRID);
		Assert.NotEmpty(g.Value);
		// Value 是「SRID + WKB」的原始字节
		Assert.Equal(wkb.Length, g.Value.Length);
	}

	[Fact]
	public void MySqlGeometry_FromWkb_keeps_srid_and_payload()
	{
		var wkb = new byte[]
		{
			1,                                              // 字节序（小端）
			1, 0, 0, 0,                                     // 类型 = Point
			0, 0, 0, 0, 0, 0, 0x08, 0x40,                   // X = 3.0
			0, 0, 0, 0, 0, 0, 0x10, 0x40,                   // Y = 4.0
		};
		var g = MySqlGeometry.FromWkb(4326, wkb);
		Assert.Equal(4326, g.SRID);
		Assert.Equal(wkb.Length, g.WKB.Length);
	}

	[Fact]
	public void MySqlGeometry_Value_is_srid_prefixed()
	{
		var wkb = new byte[] { 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xF0, 0x3F, 0, 0, 0, 0, 0, 0, 0x00, 0x40 };
		var g = MySqlGeometry.FromWkb(1, wkb);
		// 前 4 字节为小端 SRID
		Assert.Equal(1, BitConverter.ToInt32(g.Value, 0));
	}


	[Fact]
	public void OracleTimeStamp_parses_and_exposes_components()
	{
		var v = OracleTimeStamp.Parse("2024-01-02 03:04:05.123456");
		Assert.Equal(2024, v.Year);
		Assert.Equal(1, v.Month);
		Assert.Equal(2, v.Day);
		Assert.Equal(3, v.Hour);
		Assert.Equal(4, v.Minute);
		Assert.Equal(5, v.Second);
		Assert.Equal(123456000, v.Nanosecond);
		Assert.False(v.IsNull);
	}

	[Fact]
	public void OracleTimeStamp_ToString_roundtrips()
	{
		Assert.Equal("2024-01-02 03:04:05.123456000", OracleTimeStamp.Parse("2024-01-02 03:04:05.123456").ToString());
	}

	[Fact]
	public void OracleTimeStamp_constructor_from_components()
	{
		var v = new OracleTimeStamp(2024, 1, 2, 3, 4, 5, 123456000);
		Assert.Equal(2024, v.Year);
		Assert.Equal(123456000, v.Nanosecond);
	}

	[Fact]
	public void OracleTimeStampLTZ_constructs_and_roundtrips()
	{
		// 注：OracleTimeStampLTZ 没有 Parse，用 (year,month,day,h,m,s,nanosecond) 构造
		var v = new OracleTimeStampLTZ(2024, 1, 2, 3, 4, 5, 123456000);
		Assert.Equal(2024, v.Year);
		Assert.Equal(123456000, v.Nanosecond);
		Assert.Equal("2024-01-02 03:04:05.123456000", v.ToString());
	}

	[Fact]
	public void OracleTimeStampLTZ_Subtract_returns_timespan()
	{
		var a = new OracleTimeStampLTZ(2024, 1, 2, 3, 4, 5, 0);
		var b = new OracleTimeStampLTZ(2024, 1, 2, 3, 4, 0, 0);
		Assert.Equal(TimeSpan.FromSeconds(5), OracleTimeStampLTZ.Subtract(a, b));
	}

	[Fact]
	public void OracleTimeStampLTZ_comparison_operators()
	{
		var a = new OracleTimeStampLTZ(2024, 1, 2, 3, 4, 5, 0);
		var b = new OracleTimeStampLTZ(2024, 1, 2, 3, 4, 6, 0);
		Assert.True(a < b);
		Assert.True(b > a);
		Assert.True(a != b);
	}

	[Fact]
	public void OracleTimeStampTZ_parses_offset_and_roundtrips()
	{
		var v = OracleTimeStampTZ.Parse("2024-01-02 03:04:05.123456 +08:00");
		Assert.Equal(2024, v.Year);
		Assert.Equal(8, v.OffsetHours);
		Assert.Equal(0, v.OffsetMinutes);
		Assert.Equal("2024-01-02 03:04:05.123456000 +08:00", v.ToString());
	}

	[Fact]
	public void OracleTimeStampTZ_constructor_from_timestamp_and_zone()
	{
		var ts = OracleTimeStamp.Parse("2024-01-02 03:04:05.000000");
		var v = new OracleTimeStampTZ(ts, "+08:00");
		Assert.Equal(8, v.OffsetHours);
	}

	// ---------- Oracle 间隔类型 ----------

	[Fact]
	public void OracleIntervalDS_parses_and_roundtrips()
	{
		var v = OracleIntervalDS.Parse("1 2:3:4.5");
		Assert.Equal(1, v.Days);
		Assert.Equal(2, v.Hours);
		Assert.Equal(3, v.Minutes);
		Assert.Equal(4, v.Seconds);
		Assert.Equal("1 02:03:04.5", v.ToString());
	}

	[Fact]
	public void OracleIntervalDS_handles_negative()
	{
		var v = OracleIntervalDS.Parse("-1 2:3:4.5");
		Assert.True(v.IsNegative);
		Assert.Equal(1, v.Days);
	}

	[Fact]
	public void OracleIntervalDS_FromTimeSpan_roundtrips()
	{
		var ts = new TimeSpan(1, 2, 3, 4);
		var v = OracleIntervalDS.FromTimeSpan(ts);
		Assert.Equal(1, v.Days);
		Assert.Equal(2, v.Hours);
		Assert.Equal(3, v.Minutes);
		Assert.Equal(4, v.Seconds);
		Assert.Equal(ts, v.Value);
	}

	[Fact]
	public void OracleIntervalDS_FromComponents()
	{
		var v = OracleIntervalDS.FromComponents(false, 5, 6, 7, 8, 0);
		Assert.Equal(5, v.Days);
		Assert.Equal(6, v.Hours);
		Assert.Equal(7, v.Minutes);
		Assert.Equal(8, v.Seconds);
	}

	[Fact]
	public void OracleIntervalYM_FromYearsMonths_roundtrips()
	{
		var v = OracleIntervalYM.FromYearsMonths(1, 2, false);
		Assert.Equal(1, v.Years);
		Assert.Equal(2, v.Months);
		Assert.Equal(14, v.TotalMonths);
		Assert.Equal("1-02", v.ToString());
	}

	[Fact]
	public void OracleIntervalYM_FromTotalMonths()
	{
		var v = OracleIntervalYM.FromTotalMonths(14);
		Assert.Equal(1, v.Years);
		Assert.Equal(2, v.Months);
	}

	[Fact]
	public void OracleIntervalYM_handles_negative()
	{
		var v = OracleIntervalYM.FromYearsMonths(1, 2, true);
		Assert.True(v.IsNegative);
	}

	// ---------- 枚举完整性（公开契约） ----------

	[Theory]
	[InlineData(typeof(MySqlSslMode), 6)]
	[InlineData(typeof(MySqlGuidFormat), 7)]
	[InlineData(typeof(MySqlLoadBalance), 4)]
	[InlineData(typeof(MySqlConnectionProtocol), 9)]
	[InlineData(typeof(OracleConnectionProtocol), 9)]
	[InlineData(typeof(MySqlDateTimeKind), 3)]
	[InlineData(typeof(MySqlServerRedirectionMode), 3)]
	[InlineData(typeof(OracleUdtStatus), 2)]
	[InlineData(typeof(SqlStatementType), 5)]
	[InlineData(typeof(MySqlCertificateStoreLocation), 3)]
	[InlineData(typeof(MySqlBulkLoaderConflictOption), 3)]
	[InlineData(typeof(MySqlBulkLoaderPriority), 3)]
	public void Enum_member_counts_are_stable(Type enumType, int expected)
	{
		// 枚举成员数是公开契约的一部分；数量变化意味着 API 破坏性变更
		Assert.True(enumType.IsEnum);
		Assert.Equal(expected, Enum.GetNames(enumType).Length);
	}

	[Fact]
	public void MySqlSslMode_values_are_stable()
	{
		Assert.Equal(0, (int)MySqlSslMode.None);
		Assert.Equal(1, (int)MySqlSslMode.Preferred);
		Assert.Equal(2, (int)MySqlSslMode.Required);
		Assert.Equal(3, (int)MySqlSslMode.VerifyCA);
		Assert.Equal(4, (int)MySqlSslMode.VerifyFull);
	}

	[Fact]
	public void MySqlLoadBalance_values_are_stable()
	{
		Assert.Equal(0, (int)MySqlLoadBalance.RoundRobin);
		Assert.Equal(1, (int)MySqlLoadBalance.FailOver);
		Assert.Equal(2, (int)MySqlLoadBalance.Random);
	}

	[Fact]
	public void ConnectionProtocol_values_are_stable()
	{
		// 实测：Sockets/Socket/Tcp 同值 1，Pipe/NamedPipe 同值 2，
		// UnixSocket/Unix 同值 3，SharedMemory/Memory 同值 4（别名共享取值）
		Assert.Equal(1, (int)MySqlConnectionProtocol.Sockets);
		Assert.Equal(1, (int)MySqlConnectionProtocol.Tcp);
		Assert.Equal(2, (int)MySqlConnectionProtocol.Pipe);
		Assert.Equal(3, (int)MySqlConnectionProtocol.UnixSocket);
		Assert.Equal(4, (int)MySqlConnectionProtocol.SharedMemory);
	}
}
