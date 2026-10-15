using System;
using System.Text;
using OceanBase.ColumnReaders;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 日期/时间列读取器测试（internal，无需服务端）。
///
/// <see cref="TextDateTimeColumnReader.ParseDateTime"/> 与
/// <see cref="BinaryDateTimeColumnReader.ReadValue"/> 负责把协议里的文本/二进制
/// 日期时间解析成 <see cref="DateTime"/> 或 <see cref="MySqlDateTime"/>；
/// 零值日期（<c>0000-00-00</c>）的处理策略由连接串的
/// <c>AllowZeroDateTime</c> / <c>ConvertZeroDateTime</c> 决定。
/// </summary>
public sealed class DateTimeReaderTests
{
	// ---------- 文本协议 ----------

	private static object ParseText(string text, bool convertZero = false, bool allowZero = false, DateTimeKind kind = DateTimeKind.Unspecified)
	{
		return TextDateTimeColumnReader.ParseDateTime(Encoding.ASCII.GetBytes(text), convertZero, allowZero, kind);
	}

	[Fact]
	public void Text_parses_date_only()
	{
		var value = Assert.IsType<DateTime>(ParseText("2024-03-15"));
		Assert.Equal(new DateTime(2024, 3, 15, 0, 0, 0), value);
	}

	[Fact]
	public void Text_parses_date_and_time()
	{
		var value = Assert.IsType<DateTime>(ParseText("2024-03-15 13:45:59"));
		Assert.Equal(new DateTime(2024, 3, 15, 13, 45, 59), value);
	}

	[Fact]
	public void Text_parses_fractional_seconds_as_ticks()
	{
		// 小数位不足 7 位时右侧补零：".5" → 5000000 ticks
		var value = Assert.IsType<DateTime>(ParseText("2024-03-15 13:45:59.5"));
		Assert.Equal(new DateTime(2024, 3, 15, 13, 45, 59).AddTicks(5_000_000), value);
	}

	[Fact]
	public void Text_parses_seven_digit_fraction()
	{
		var value = Assert.IsType<DateTime>(ParseText("2024-03-15 13:45:59.1234567"));
		Assert.Equal(new DateTime(2024, 3, 15, 13, 45, 59).AddTicks(1_234_567), value);
	}

	[Theory]
	[InlineData("2024-03-15 00:00:00")]
	[InlineData("1970-01-01 23:59:59")]
	[InlineData("9999-12-31 23:59:59")]
	public void Text_parses_various_valid_values(string text)
	{
		var value = Assert.IsType<DateTime>(ParseText(text));
		Assert.Equal(DateTime.Parse(text, System.Globalization.CultureInfo.InvariantCulture), value);
	}

	[Fact]
	public void Text_applies_requested_DateTimeKind()
	{
		Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(ParseText("2024-03-15", kind: DateTimeKind.Utc)).Kind);
		Assert.Equal(DateTimeKind.Local, Assert.IsType<DateTime>(ParseText("2024-03-15", kind: DateTimeKind.Local)).Kind);
	}

	// ---------- 零值日期 ----------

	[Fact]
	public void Text_zero_date_throws_by_default()
	{
		// 默认策略：既不允许也不转换 → 直接抛 InvalidCastException（不经 FormatException 包装），
		// 并在消息里提示需要设置 AllowZeroDateTime / ConvertZeroDateTime。
		var ex = Assert.Throws<InvalidCastException>(() => ParseText("0000-00-00"));
		Assert.Contains("AllowZeroDateTime", ex.Message);
		Assert.Contains("ConvertZeroDateTime", ex.Message);
	}

	[Fact]
	public void Text_zero_date_converts_to_MinValue_when_configured()
	{
		Assert.Equal(DateTime.MinValue, Assert.IsType<DateTime>(ParseText("0000-00-00", convertZero: true)));
	}

	[Fact]
	public void Text_zero_date_returns_MySqlDateTime_when_allowed()
	{
		var value = Assert.IsType<MySqlDateTime>(ParseText("0000-00-00", allowZero: true));
		Assert.False(value.IsValidDateTime);
		Assert.Equal(0, value.Year);
		Assert.Equal("0000-00-00", value.ToString());
	}

	[Fact]
	public void Text_convertZero_takes_precedence_over_allowZero()
	{
		// 两者同时为真时先判 convertZeroDateTime
		Assert.Equal(DateTime.MinValue, Assert.IsType<DateTime>(ParseText("0000-00-00", convertZero: true, allowZero: true)));
	}

	[Fact]
	public void Text_zero_datetime_with_time_part_is_also_zero()
	{
		// 年/月/日全为 0 即判为零值，即便带时间部分
		Assert.Equal(DateTime.MinValue, Assert.IsType<DateTime>(ParseText("0000-00-00 00:00:00", convertZero: true)));
	}

	// ---------- 文本：非法输入 ----------

	[Theory]
	[InlineData("")]
	[InlineData("not-a-date")]
	[InlineData("2024")]
	[InlineData("2024-03")]
	[InlineData("2024/03/15")]          // 分隔符必须是 '-'
	[InlineData("2024-03-15T13:45:59")] // 日期与时间之间必须是空格
	[InlineData("2024-03-15 13:45")]    // 缺秒
	[InlineData("2024-13-01")]          // 月份越界
	[InlineData("2024-02-30")]          // 该月无此日
	public void Text_rejects_invalid_values(string text)
	{
		Assert.Throws<FormatException>(() => ParseText(text));
	}

	[Fact]
	public void Text_error_message_includes_offending_text()
	{
		var ex = Assert.Throws<FormatException>(() => ParseText("bogus"));
		Assert.Contains("bogus", ex.Message);
	}

	[Fact]
	public void Text_sub_microsecond_precision_rejected_in_MySqlDateTime_mode()
	{
		// MySqlDateTime 只到微秒：第 7 位小数非 0 时无法表示
		var ex = Assert.Throws<FormatException>(
			() => ParseText("2024-03-15 13:45:59.1234567", allowZero: true));
		Assert.IsType<NotSupportedException>(ex.InnerException);
	}

	[Fact]
	public void Text_seven_digit_fraction_divisible_by_ten_is_accepted()
	{
		// 末位为 0 时可整除 10 → 微秒精度无损
		var value = Assert.IsType<MySqlDateTime>(ParseText("2024-03-15 13:45:59.1234560", allowZero: true));
		Assert.Equal(123456, value.Microsecond);
	}

	// ---------- Time 列 ----------

	[Fact]
	public void Text_time_reader_parses_time_span()
	{
		var reader = TextTimeColumnReader.Instance;
		var value = Assert.IsType<TimeSpan>(reader.ReadValue(Encoding.ASCII.GetBytes("13:45:59"), null!));
		Assert.Equal(new TimeSpan(13, 45, 59), value);
	}

	[Fact]
	public void Text_time_reader_parses_negative_and_large_spans()
	{
		var reader = TextTimeColumnReader.Instance;
		Assert.Equal(TimeSpan.FromHours(-5), Assert.IsType<TimeSpan>(
			reader.ReadValue(Encoding.ASCII.GetBytes("-05:00:00"), null!)));
		Assert.Equal(new TimeSpan(100, 0, 0), Assert.IsType<TimeSpan>(
			reader.ReadValue(Encoding.ASCII.GetBytes("100:00:00"), null!)));
	}

	// ---------- 二进制协议 ----------

	/// <summary>按 MySQL 二进制日期时间布局组装字节：年(2) 月 日 [时 分 秒 [微秒(4)]]。</summary>
	private static byte[] BinaryDateTime(int year, int month, int day, int hour = 0, int minute = 0, int second = 0, int? microsecond = null)
	{
		if (microsecond is null)
		{
			return hour == 0 && minute == 0 && second == 0
				? new[] { (byte)(year & 0xFF), (byte)(year >> 8), (byte)month, (byte)day }
				: new[] { (byte)(year & 0xFF), (byte)(year >> 8), (byte)month, (byte)day, (byte)hour, (byte)minute, (byte)second };
		}

		var value = microsecond.Value;
		return new[]
		{
			(byte)(year & 0xFF), (byte)(year >> 8), (byte)month, (byte)day,
			(byte)hour, (byte)minute, (byte)second,
			(byte)(value & 0xFF), (byte)((value >> 8) & 0xFF), (byte)((value >> 16) & 0xFF), (byte)((value >> 24) & 0xFF),
		};
	}

	private static object ReadBinary(byte[] data, bool convertZero = false, bool allowZero = false, DateTimeKind kind = DateTimeKind.Unspecified)
	{
		var connection = new MySqlConnection(BuildConnectionString(convertZero, allowZero, kind));
		var reader = CreateBinaryReader(connection);
		return reader.ReadValue(data, null!);
	}

	private static string BuildConnectionString(bool convertZero, bool allowZero, DateTimeKind kind)
	{
		var csb = new MySqlConnectionStringBuilder { Server = "localhost" };
		if (convertZero)
			csb.ConvertZeroDateTime = true;
		if (allowZero)
			csb.AllowZeroDateTime = true;
		if (kind != DateTimeKind.Unspecified)
			csb.DateTimeKind = (MySqlDateTimeKind)(int)kind;
		return csb.ConnectionString;
	}

	/// <summary>
	/// 构造 <c>BinaryDateTimeColumnReader</c>。
	/// 读取器在构造时就会读 <c>AllowZeroDateTime</c>/<c>ConvertZeroDateTime</c>/<c>DateTimeKind</c>，
	/// 而这些属性取自 <c>m_connectionSettings</c>（仅在 Open 时填充），
	/// 因此这里显式注入一份 ConnectionSettings，避免依赖真实连接。
	/// </summary>
	private static ColumnReader CreateBinaryReader(MySqlConnection connection)
	{
		var assembly = typeof(MySqlConnection).Assembly;
		var settingsType = assembly.GetType("OceanBase.Core.ConnectionSettings", throwOnError: true)!;
		var csb = new MySqlConnectionStringBuilder(connection.ConnectionString);
		var settings = Activator.CreateInstance(settingsType, csb)!;
		typeof(MySqlConnection)
			.GetField("m_connectionSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
			.SetValue(connection, settings);

		var facadeType = assembly.GetType("OceanBase.Core.ConnectionFacade", throwOnError: true)!;
		var facade = Activator.CreateInstance(facadeType, connection)!;
		var readerType = assembly.GetType("OceanBase.ColumnReaders.BinaryDateTimeColumnReader", throwOnError: true)!;
		return (ColumnReader)Activator.CreateInstance(readerType, facade)!;
	}

	[Fact]
	public void Binary_parses_date_only_payload()
	{
		var value = Assert.IsType<DateTime>(ReadBinary(BinaryDateTime(2024, 3, 15)));
		Assert.Equal(new DateTime(2024, 3, 15), value);
	}

	[Fact]
	public void Binary_parses_date_and_time_payload()
	{
		var value = Assert.IsType<DateTime>(ReadBinary(BinaryDateTime(2024, 3, 15, 13, 45, 59)));
		Assert.Equal(new DateTime(2024, 3, 15, 13, 45, 59), value);
	}

	[Fact]
	public void Binary_parses_microseconds_into_ticks()
	{
		// 二进制微秒字段按 1000 纳秒 = 1 微秒换算，123456 μs = 1234560 ticks
		var value = Assert.IsType<DateTime>(ReadBinary(BinaryDateTime(2024, 3, 15, 13, 45, 59, 123456)));
		Assert.Equal(new DateTime(2024, 3, 15, 13, 45, 59).AddTicks(1_234_560), value);
	}

	[Fact]
	public void Binary_year_is_little_endian()
	{
		// 0x07E8 = 2024；交换字节后应得到 0xE807 之类的大年份而抛错或不同值
		var value = Assert.IsType<DateTime>(ReadBinary(new byte[] { 0xE8, 0x07, 3, 15 }));
		Assert.Equal(2024, value.Year);
	}

	[Fact]
	public void Binary_applies_DateTimeKind()
	{
		Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(ReadBinary(BinaryDateTime(2024, 3, 15), kind: DateTimeKind.Utc)).Kind);
	}

	[Fact]
	public void Binary_empty_payload_throws_by_default()
	{
		// 与文本路径一致：零值日期在默认策略下直接抛 InvalidCastException
		var ex = Assert.Throws<InvalidCastException>(() => ReadBinary(Array.Empty<byte>()));
		Assert.Contains("Unable to convert MySQL date/time", ex.Message);
	}

	[Fact]
	public void Binary_empty_payload_converts_to_MinValue()
	{
		Assert.Equal(DateTime.MinValue, Assert.IsType<DateTime>(ReadBinary(Array.Empty<byte>(), convertZero: true)));
	}

	[Fact]
	public void Binary_empty_payload_returns_MySqlDateTime_when_allowed()
	{
		var value = Assert.IsType<MySqlDateTime>(ReadBinary(Array.Empty<byte>(), allowZero: true));
		Assert.False(value.IsValidDateTime);
	}

	[Fact]
	public void Binary_zero_date_with_allowZero_returns_MySqlDateTime()
	{
		var value = Assert.IsType<MySqlDateTime>(ReadBinary(BinaryDateTime(0, 0, 0), allowZero: true));
		Assert.False(value.IsValidDateTime);
		Assert.Equal(0, value.Year);
	}

	[Fact]
	public void Binary_invalid_date_wrapped_in_FormatException()
	{
		// 月份 13 越界：内部异常被包成 FormatException
		var ex = Assert.Throws<FormatException>(() => ReadBinary(BinaryDateTime(2024, 13, 1)));
		Assert.IsType<ArgumentOutOfRangeException>(ex.InnerException);
	}
}
