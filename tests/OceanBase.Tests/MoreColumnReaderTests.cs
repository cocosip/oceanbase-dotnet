using System;
using System.Buffers.Binary;
using System.Text;
using OceanBase.ColumnReaders;
using OceanBase.Core;
using OceanBase.Protocol;
using OceanBase.Protocol.Payloads;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 补充的列读取器测试（internal 类型，无需服务端）。
///
/// 覆盖 BIT / VECTOR / TIME / Oracle 间隔与时间戳 / 文本浮点等读取器的字节解析，
/// 以及 <see cref="ColumnReader.Create"/> 按列类型与连接选项选择读取器的分发矩阵。
/// </summary>
public sealed class MoreColumnReaderTests
{
	// ---------- 列定义构造（与 TypeMapperTests 相同的线上格式）----------

	private static byte[] CreateColumnDefinitionBytes(CharacterSet characterSet, uint columnLength, byte protocolType, ColumnFlags flags, bool oracleMode)
	{
		var w = new ByteBufferWriter(128);
		w.WriteLengthEncodedString("def");
		w.WriteLengthEncodedString("db");
		w.WriteLengthEncodedString("t");
		w.WriteLengthEncodedString("t");
		w.WriteLengthEncodedString("c");
		w.WriteLengthEncodedString("c");
		w.Write((byte)12);
		w.Write((ushort)characterSet);
		w.Write(columnLength);
		w.Write(protocolType);
		w.Write((ushort)flags);
		w.Write((byte)0);  // decimals
		w.Write((byte)0);  // precision
		w.Write((byte)0);  // inout
		return w.ToPayloadData().Span.ToArray();
	}

	private static ColumnDefinitionPayload CreateColumnDefinition(CharacterSet characterSet, uint columnLength, byte protocolType, ColumnFlags flags = 0, bool oracleMode = false)
	{
		var bytes = CreateColumnDefinitionBytes(characterSet, columnLength, protocolType, flags, oracleMode);
		var resizable = new ResizableArray<byte>();
		Utility.Resize(ref resizable, bytes.Length);
		bytes.CopyTo(resizable.Array!.AsSpan(0));
		ColumnDefinitionPayload payload = null!;
		ColumnDefinitionPayload.Initialize(ref payload, new ResizableArraySegment<byte>(resizable, 0, bytes.Length), isOracleMode: oracleMode);
		return payload;
	}

	private static ConnectionFacade CreateFacade(MySqlConnection connection)
	{
		var type = typeof(MySqlConnection).Assembly.GetType("OceanBase.Core.ConnectionFacade", throwOnError: true)!;
		return (ConnectionFacade)Activator.CreateInstance(type, connection)!;
	}

	private static ConnectionFacade CreateOracleFacade(OracleConnection connection)
	{
		var type = typeof(MySqlConnection).Assembly.GetType("OceanBase.Core.ConnectionFacade", throwOnError: true)!;
		return (ConnectionFacade)Activator.CreateInstance(type, connection)!;
	}

	private static string CreateReaderName(MySqlConnection connection, bool isBinary, CharacterSet charset, uint length, byte protocolType, ColumnFlags flags = 0, bool oracleMode = false)
	{
		var reader = ColumnReader.Create(isBinary, CreateColumnDefinition(charset, length, protocolType, flags, oracleMode), CreateFacade(connection));
		Assert.NotNull(reader);
		return reader.GetType().Name;
	}

	// ---------- ColumnReader.Create 分发矩阵 ----------
	//
	// 注意：未打开的连接上 Session getter 会抛异常、TreatTinyAsBoolean/GuidFormat 等
	// 选项属性未初始化（NRE），因此 TINY 布尔、GUID、日期时间读取器等依赖连接状态的
	// 分支无法离线覆盖，这里只验证纯离线可达的分发分支。

	public static TheoryData<string, bool, bool, uint, byte, int, bool, bool, string> FactoryCases => new()
	{
		// key, treatTinyAsBoolean, isBinary, columnLength, protocolType, flags, oracleMode, binaryCharset, expectedReaderName
		// 整数族（isBinary × Unsigned 组合）
		{ "Short_text", false, false, 6, 2, 0, false, false, "TextSignedInt16ColumnReader" },
		{ "Short_u16_binary", false, true, 6, 2, (int)ColumnFlags.Unsigned, false, false, "BinaryUnsignedInt16ColumnReader" },
		{ "Long_text", false, false, 11, 3, 0, false, false, "TextSignedInt32ColumnReader" },
		{ "Int24_u32_binary", false, true, 11, 9, (int)ColumnFlags.Unsigned, false, false, "BinaryUnsignedInt32ColumnReader" },
		{ "Longlong_text", false, false, 20, 8, 0, false, false, "TextSignedInt64ColumnReader" },
		{ "Longlong_u64_binary", false, true, 20, 8, (int)ColumnFlags.Unsigned, false, false, "BinaryUnsignedInt64ColumnReader" },
		// BIT
		{ "Bit", false, true, 8, 16, 0, false, false, "BitColumnReader" },
		// 二进制字符集的字符串族 → 字节读取器（长度≠16 时不触碰 GUID 选项）
		{ "Varbinary_bytes", false, true, 100, 15, 0, false, true, "BytesColumnReader" },
		{ "NVarchar2_binary", false, true, 100, 207, 0, false, true, "BytesColumnReader" },
		{ "NChar_binary", false, true, 100, 208, 0, false, true, "BytesColumnReader" },
		// Oracle 专有类型
		{ "OracleRaw", false, true, 2000, 203, 0, false, false, "BytesColumnReader" },
		{ "OracleBlob", false, true, 4000, 210, 0, false, false, "BytesColumnReader" },
		{ "OracleClob", false, true, 4000, 211, 0, false, false, "BytesColumnReader" },
		{ "OracleRowId", false, false, 18, 209, 0, false, false, "StringColumnReader" },
		{ "OracleCursor", false, true, 4, 163, 0, false, false, "OracleCursorColumnReader" },
		{ "Json", false, false, 1000, 245, 0, false, false, "StringColumnReader" },
		// TIME / YEAR
		{ "Time_text", false, false, 10, 11, 0, false, false, "TextTimeColumnReader" },
		{ "Time_binary", false, true, 10, 11, 0, false, false, "BinaryTimeColumnReader" },
		{ "Year_text", false, false, 4, 13, 0, false, false, "TextSignedInt32ColumnReader" },
		{ "Year_binary", false, true, 4, 13, 0, false, false, "BinaryYearColumnReader" },
		// Oracle 间隔 / 时区时间戳
		{ "IntervalDS", false, true, 14, 205, 0, false, false, "BinaryOracleIntervalDSColumnReader" },
		{ "IntervalYM", false, true, 7, 204, 0, false, false, "BinaryOracleIntervalYMColumnReader" },
		{ "TimeStampLTZ", false, true, 12, 201, 0, false, false, "BinaryOracleTimeStampLTZColumnReader" },
		{ "TimeStampTZ", false, true, 14, 200, 0, false, false, "BinaryOracleTimeStampTZColumnReader" },
		{ "TimeStampNano", false, true, 12, 202, 0, false, false, "BinaryOracleTimeStampNanoColumnReader" },
		// 浮点（Oracle 模式下协议字节 3/4/5 映射为 NUMBER/BINARY_FLOAT/BINARY_DOUBLE）
		{ "Float_text", false, false, 12, 4, 0, false, false, "TextFloatColumnReader" },
		{ "Float_binary", false, true, 12, 4, 0, false, false, "BinaryFloatColumnReader" },
		{ "OracleNumber_text", false, false, 20, 3, 0, true, false, "TextSignedInt32ColumnReader" },
		{ "OracleNumberFloat", false, false, 20, 206, 0, false, false, "TextDoubleColumnReader" },
		{ "OracleBinaryFloat_binary", false, true, 12, 4, 0, true, false, "BinaryFloatColumnReader" },
		{ "Double_text", false, false, 22, 5, 0, false, false, "TextDoubleColumnReader" },
		{ "OracleBinaryDouble_binary", false, true, 22, 5, 0, true, false, "BinaryDoubleColumnReader" },
		// DECIMAL / 其他
		{ "Decimal", false, true, 20, 246, 0, false, false, "DecimalColumnReader" },
		{ "Geometry", false, true, 100, 255, 0, false, false, "BytesColumnReader" },
		{ "Null_type", false, true, 0, 6, 0, false, false, "NullColumnReader" },
		{ "Vector", false, true, 16, 242, 0, false, false, "VectorColumnReader" },
	};

	[Theory]
	[MemberData(nameof(FactoryCases))]
	public void ColumnReaderCreate_selects_reader_by_type(string _, bool treatTinyAsBoolean, bool isBinary, uint columnLength, byte protocolType, int flags, bool oracleMode, bool binaryCharset, string expected)
	{
		var builder = new MySqlConnectionStringBuilder
		{
			Server = "h",
			TreatTinyAsBoolean = treatTinyAsBoolean,
		};
		using var connection = new MySqlConnection(builder.ConnectionString);
		var charset = binaryCharset ? CharacterSet.Binary : CharacterSet.Latin1SwedishCaseInsensitive;

		Assert.Equal(expected, CreateReaderName(connection, isBinary, charset, columnLength, protocolType, (ColumnFlags)flags, oracleMode));
	}

	[Fact]
	public void ColumnReaderCreate_throws_for_unsupported_type()
	{
		using var connection = new MySqlConnection("Server=h");
		var facade = CreateFacade(connection);
		var definition = CreateColumnDefinition(CharacterSet.Binary, 19, 17); // Timestamp2 未实现

		Assert.Throws<NotImplementedException>(() => ColumnReader.Create(true, definition, facade));
	}

	// ---------- BitColumnReader ----------

	[Fact]
	public void BitColumnReader_accumulates_bytes_in_text_mode()
	{
		// 无 Binary 标志：按大端逐字节累加
		var value = (ulong)BitColumnReader.Instance.ReadValue(new byte[] { 1, 2 }, CreateColumnDefinition(CharacterSet.Binary, 16, 16));
		Assert.Equal(258ul, value);
	}

	[Fact]
	public void BitColumnReader_reads_small_bit_values()
	{
		// Binary 标志 + ColumnLength<=5 + 单字节且低于 (1<<len)：直接返回
		var definition = CreateColumnDefinition(CharacterSet.Binary, 4, 16, ColumnFlags.Binary);
		Assert.Equal(10ul, (ulong)BitColumnReader.Instance.ReadValue(new byte[] { 0x0A }, definition));
	}

	[Fact]
	public void BitColumnReader_parses_utf8_digits_for_wide_bits()
	{
		var definition = CreateColumnDefinition(CharacterSet.Binary, 64, 16, ColumnFlags.Binary);
		Assert.Equal(255ul, (ulong)BitColumnReader.Instance.ReadValue("255"u8.ToArray(), definition));
	}

	[Fact]
	public void BitColumnReader_rejects_non_numeric_binary_bit()
	{
		var definition = CreateColumnDefinition(CharacterSet.Binary, 64, 16, ColumnFlags.Binary);
		Assert.Throws<FormatException>(() => BitColumnReader.Instance.ReadValue("abc"u8.ToArray(), definition));
	}

	[Fact]
	public void BitColumnReader_try_read_int32_converts_value()
	{
		var definition = CreateColumnDefinition(CharacterSet.Binary, 16, 16);
		Assert.Equal(258, BitColumnReader.Instance.TryReadInt32(new byte[] { 1, 2 }, definition));
	}

	// ---------- VectorColumnReader ----------

	[Fact]
	public void VectorColumnReader_decodes_little_endian_floats()
	{
		var data = new byte[8];
		BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(0), 1.5f);
		BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(4), -2.25f);

		var value = Assert.IsType<ReadOnlyMemory<float>>(VectorColumnReader.Instance.ReadValue(data, CreateColumnDefinition(CharacterSet.Binary, 8, 242)));

		Assert.Equal(new float[] { 1.5f, -2.25f }, value.ToArray());
	}

	// ---------- BinaryTimeColumnReader ----------

	[Fact]
	public void BinaryTime_returns_zero_for_empty_data()
	{
		Assert.Equal(TimeSpan.Zero, BinaryTimeColumnReader.Instance.ReadValue(Array.Empty<byte>(), CreateColumnDefinition(CharacterSet.Binary, 10, 11)));
	}

	[Fact]
	public void BinaryTime_reads_positive_interval_without_microseconds()
	{
		// [符号][int32 天][时][分][秒] 共 8 字节
		var value = Assert.IsType<TimeSpan>(BinaryTimeColumnReader.Instance.ReadValue(new byte[] { 0, 1, 0, 0, 0, 2, 3, 4 }, CreateColumnDefinition(CharacterSet.Binary, 10, 11)));
		Assert.Equal(new TimeSpan(1, 2, 3, 4), value);
	}

	[Fact]
	public void BinaryTime_reads_negative_interval_with_microseconds()
	{
		// [符号=1][int32 天][时][分][秒][int32 微秒] 共 12 字节，各段取负
		var data = new byte[12];
		data[0] = 1;
		BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(1), 2);
		data[5] = 3;
		data[6] = 4;
		data[7] = 5;
		BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(8), 500_000);

		var value = Assert.IsType<TimeSpan>(BinaryTimeColumnReader.Instance.ReadValue(data, CreateColumnDefinition(CharacterSet.Binary, 10, 11)));
		Assert.Equal(TimeSpan.FromDays(-2) - TimeSpan.FromHours(3) - TimeSpan.FromMinutes(4) - TimeSpan.FromSeconds(5) - TimeSpan.FromMilliseconds(500), value);
	}

	// ---------- Oracle 间隔 ----------

	[Fact]
	public void IntervalDS_decodes_positive_wire_format()
	{
		var data = new byte[14];
		data[0] = 0;                                                    // '+'
		BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(1), 2);     // 天
		data[5] = 3;                                                    // 时
		data[6] = 4;                                                    // 分
		data[7] = 5;                                                    // 秒
		BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(8), 123_000_000); // 纳秒
		data[12] = 2;                                                   // 天字符串长度
		data[13] = 9;                                                   // 纳秒精度

		var value = Assert.IsType<OracleIntervalDS>(BinaryOracleIntervalDSColumnReader.Instance.ReadValue(data, CreateColumnDefinition(CharacterSet.Binary, 14, 205)));

		Assert.False(value.IsNegative);
		Assert.Equal(2, value.Days);
		Assert.Equal(3, value.Hours);
		Assert.Equal(4, value.Minutes);
		Assert.Equal(5, value.Seconds);
		Assert.Equal(123_000_000, value.Nanoseconds);
		Assert.Equal(9, value.Scale);
	}

	[Fact]
	public void IntervalDS_marks_negative_and_rejects_wrong_length()
	{
		var data = new byte[14];
		data[0] = 1; // '-'
		BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(1), 1);

		var value = Assert.IsType<OracleIntervalDS>(BinaryOracleIntervalDSColumnReader.Instance.ReadValue(data, CreateColumnDefinition(CharacterSet.Binary, 14, 205)));
		Assert.True(value.IsNegative);

		Assert.Throws<FormatException>(() => BinaryOracleIntervalDSColumnReader.Instance.ReadValue(new byte[13], CreateColumnDefinition(CharacterSet.Binary, 14, 205)));
	}

	[Fact]
	public void IntervalYM_decodes_wire_format_and_rejects_wrong_length()
	{
		var data = new byte[7];
		data[0] = 0;
		BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(1), 5);
		data[5] = 6;
		data[6] = 1;

		var value = Assert.IsType<OracleIntervalYM>(BinaryOracleIntervalYMColumnReader.Instance.ReadValue(data, CreateColumnDefinition(CharacterSet.Binary, 7, 204)));
		Assert.False(value.IsNegative);
		Assert.Equal(5, value.Years);
		Assert.Equal(6, value.Months);

		Assert.Throws<FormatException>(() => BinaryOracleIntervalYMColumnReader.Instance.ReadValue(new byte[6], CreateColumnDefinition(CharacterSet.Binary, 7, 204)));
	}

	// ---------- Oracle 时间戳 ----------

	[Fact]
	public void TimeStampTZ_decodes_with_and_without_time_zone_name()
	{
		var data = new byte[14];
		data[0] = 20; data[1] = 26;           // 2026
		data[2] = 10; data[3] = 16;           // 10-16
		data[4] = 12; data[5] = 34; data[6] = 56;
		BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(7), 123_456_789);
		data[11] = 9;                          // scale
		data[12] = 8;                          // +08
		data[13] = 0;

		var value = Assert.IsType<OracleTimeStampTZ>(BinaryOracleTimeStampTZColumnReader.Instance.ReadValue(data, CreateColumnDefinition(CharacterSet.Binary, 14, 200)));
		Assert.Equal(2026, value.Year);
		Assert.Equal(10, value.Month);
		Assert.Equal(16, value.Day);
		Assert.Equal(12, value.Hour);
		Assert.Equal(34, value.Minute);
		Assert.Equal(56, value.Second);
		Assert.Equal(123_456_789, value.Nanosecond);
		Assert.Equal(9, value.Scale);
		Assert.Equal((sbyte)8, value.OffsetHours);
		Assert.Equal((byte)0, value.OffsetMinutes);

		// 追加时区名：[长度]["Asia/Shanghai"]
		var withName = new byte[14 + 1 + "Asia/Shanghai"u8.Length];
		data.CopyTo(withName, 0);
		withName[14] = (byte)"Asia/Shanghai"u8.Length;
		"Asia/Shanghai"u8.CopyTo(withName.AsSpan(15));

		var named = Assert.IsType<OracleTimeStampTZ>(BinaryOracleTimeStampTZColumnReader.Instance.ReadValue(withName, CreateColumnDefinition(CharacterSet.Binary, 14, 200)));
		Assert.Equal("Asia/Shanghai", named.TimeZoneName);

		Assert.Throws<FormatException>(() => BinaryOracleTimeStampTZColumnReader.Instance.ReadValue(new byte[13], CreateColumnDefinition(CharacterSet.Binary, 14, 200)));
	}

	[Fact]
	public void TimeStampLTZ_decodes_wire_format_and_rejects_wrong_length()
	{
		var data = new byte[12];
		data[0] = 20; data[1] = 26;
		data[2] = 10; data[3] = 16;
		data[4] = 12; data[5] = 34; data[6] = 56;
		BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(7), 999_999_999);
		data[11] = 9;

		var value = Assert.IsType<OracleTimeStampLTZ>(BinaryOracleTimeStampLTZColumnReader.Instance.ReadValue(data, CreateColumnDefinition(CharacterSet.Binary, 12, 201)));
		Assert.Equal(2026, value.Year);
		Assert.Equal(999_999_999, value.Nanosecond);

		Assert.Throws<FormatException>(() => BinaryOracleTimeStampLTZColumnReader.Instance.ReadValue(new byte[11], CreateColumnDefinition(CharacterSet.Binary, 12, 201)));
	}

	[Fact]
	public void TimeStampNano_decodes_to_OracleTimeStamp_and_rejects_wrong_length()
	{
		var data = new byte[12];
		data[0] = 20; data[1] = 26;
		data[2] = 10; data[3] = 16;
		data[4] = 12; data[5] = 34; data[6] = 56;
		BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(7), 1);
		data[11] = 9;

		var value = Assert.IsType<OracleTimeStamp>(BinaryOracleTimeStampNanoColumnReader.Instance.ReadValue(data, CreateColumnDefinition(CharacterSet.Binary, 12, 202)));
		Assert.Equal(2026, value.Year);
		Assert.Equal(1, value.Nanosecond);

		Assert.Throws<FormatException>(() => BinaryOracleTimeStampNanoColumnReader.Instance.ReadValue(new byte[12 + 1], CreateColumnDefinition(CharacterSet.Binary, 12, 202)));
	}

	// ---------- 文本浮点 ----------

	[Theory]
	[InlineData("1.5", 1.5f)]
	[InlineData("-0.25", -0.25f)]
	public void TextFloat_parses_decimal_text(string text, float expected)
	{
		Assert.Equal(expected, TextFloatColumnReader.Instance.ReadValue(Encoding.ASCII.GetBytes(text), CreateColumnDefinition(CharacterSet.Binary, 12, 4)));
	}

	[Theory]
	[InlineData("inf")]
	[InlineData("-inf")]
	[InlineData("nan")]
	public void TextFloat_parses_special_tokens(string text)
	{
		var value = (float)TextFloatColumnReader.Instance.ReadValue(Encoding.ASCII.GetBytes(text), CreateColumnDefinition(CharacterSet.Binary, 12, 4));

		if (text == "inf") Assert.Equal(float.PositiveInfinity, value);
		else if (text == "-inf") Assert.Equal(float.NegativeInfinity, value);
		else Assert.Equal(float.NaN, value);
	}

	[Fact]
	public void TextFloat_rejects_invalid_text()
	{
		Assert.Throws<FormatException>(() => TextFloatColumnReader.Instance.ReadValue("abc"u8.ToArray(), CreateColumnDefinition(CharacterSet.Binary, 12, 4)));
	}

	[Fact]
	public void TextDouble_parses_text_and_special_tokens()
	{
		var definition = CreateColumnDefinition(CharacterSet.Binary, 22, 5);
		Assert.Equal(2.5, TextDoubleColumnReader.Instance.ReadValue("2.5"u8.ToArray(), definition));
		Assert.Equal(double.PositiveInfinity, TextDoubleColumnReader.Instance.ReadValue("inf"u8.ToArray(), definition));
		Assert.Equal(double.NegativeInfinity, TextDoubleColumnReader.Instance.ReadValue("-inf"u8.ToArray(), definition));
		Assert.Equal(double.NaN, TextDoubleColumnReader.Instance.ReadValue("nan"u8.ToArray(), definition));
		Assert.Throws<FormatException>(() => TextDoubleColumnReader.Instance.ReadValue("xx"u8.ToArray(), definition));
	}

	// ---------- OracleCursorColumnReader ----------

	[Fact]
	public void OracleCursor_rejects_empty_payload()
	{
		using var connection = new OracleConnection("Server=h;Database=s");
		var reader = new OracleCursorColumnReader(CreateOracleFacade(connection));

		Assert.Throws<FormatException>(() => reader.ReadValue(Array.Empty<byte>(), CreateColumnDefinition(CharacterSet.Binary, 4, 163)));
	}

	[Fact]
	public void OracleCursor_rejects_non_numeric_text()
	{
		using var connection = new OracleConnection("Server=h;Database=s");
		var reader = new OracleCursorColumnReader(CreateOracleFacade(connection));

		Assert.Throws<FormatException>(() => reader.ReadValue("abc"u8.ToArray(), CreateColumnDefinition(CharacterSet.Binary, 4, 163)));
	}

	[Fact]
	public void OracleCursor_reads_binary_and_text_cursor_ids()
	{
		using var connection = new OracleConnection("Server=h;Database=s");
		var reader = new OracleCursorColumnReader(CreateOracleFacade(connection));
		var definition = CreateColumnDefinition(CharacterSet.Binary, 4, 163);

		// 4 字节小端二进制
		var binary = new byte[4];
		BinaryPrimitives.WriteInt32LittleEndian(binary, 42);
		var refCursor = Assert.IsType<OracleRefCursor>(reader.ReadValue(binary, definition));
		Assert.False(refCursor.IsNull);

		// 非 4 字节长度走 UTF-8 文本解析
		var textCursor = Assert.IsType<OracleRefCursor>(reader.ReadValue(Encoding.ASCII.GetBytes("7"), definition));
		Assert.False(textCursor.IsNull);
	}

	// ---------- OracleNStringColumnReader ----------
	//
	// 构造函数读取 connection.Session（未打开连接会抛异常），无法离线构造；
	// 解码逻辑改由工厂分发矩阵覆盖其可达分支（二进制字符集 → BytesColumnReader）。
}
