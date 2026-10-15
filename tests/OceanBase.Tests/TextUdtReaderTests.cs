using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OceanBase.ColumnReaders;
using OceanBase.Protocol;
using OceanBase.Protocol.Payloads;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// Oracle 模式文本协议 UDT（OBJECT/ARRAY/STRUCT）解析测试（internal，无需服务端）。
///
/// OceanBase Oracle 模式把 UDT 值以构造字面量形式返回，例如
/// <c>TYPE_NAME(attr1, attr2)</c> 或 <c>SCHEMA.TYPE_NAME(...)</c>。
/// <see cref="TextOracleUdtColumnReader.TryParseUdtLiteral"/> 负责把它拆成
/// 类型名 + 元素序列，并依据注册的映射推断每个属性的标量类型。
/// </summary>
public sealed class TextUdtReaderTests
{
	/// <summary>已注册的 UDT（见 OracleUdtCodecTests）：N(int) / S(string) / R(byte[])。</summary>
	private const string SmallType = "UDTCODEC_SMALL";

	private static OracleUdtValue Parse(string literal, bool isArray = false)
	{
		var ok = TextOracleUdtColumnReader.TryParseUdtLiteral(literal, isArray, out var value);
		Assert.True(ok, $"应能解析：{literal}");
		return value!;
	}

	private static bool TryParse(string literal, bool isArray = false)
	{
		return TextOracleUdtColumnReader.TryParseUdtLiteral(literal, isArray, out _);
	}

	// ---------- 基本结构 ----------

	[Fact]
	public void Parses_simple_struct_literal()
	{
		var value = Parse("MY_TYPE(1, 'abc')");
		Assert.Equal("MY_TYPE", value.TypeName);
		Assert.False(value.IsArray);
		Assert.Equal(new object?[] { 1L, "abc" }, value.Values);
	}

	[Fact]
	public void Parses_qualified_type_name()
	{
		var value = Parse("MY_SCHEMA.MY_TYPE(1)");
		Assert.Equal("MY_SCHEMA.MY_TYPE", value.TypeName);
	}

	[Fact]
	public void Parses_single_element()
	{
		Assert.Equal(new object?[] { 42L }, Parse("T(42)").Values);
	}

	[Fact]
	public void Parses_empty_element_list()
	{
		// 空体 → 空元素列表（SplitElements 对空白体返回空列表）
		var value = Parse("T()");
		Assert.Empty(value.Values);
	}

	[Fact]
	public void Parses_null_literal()
	{
		// 整个字面量为 NULL 时给出空类型名、非数组、空值列表
		var value = Parse("NULL");
		Assert.Equal("", value.TypeName);
		Assert.False(value.IsArray);
		Assert.Empty(value.Values);
	}

	[Fact]
	public void Parses_null_literal_case_insensitively()
	{
		Assert.Equal("", Parse("null").TypeName);
	}

	[Fact]
	public void Trims_surrounding_whitespace()
	{
		var value = Parse("   T(1)   ");
		Assert.Equal("T", value.TypeName);
	}

	// ---------- 非法输入 ----------

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("NO_PARENS")]
	[InlineData("T(1")]           // 缺右括号
	[InlineData("T")]             // 无括号
	public void Rejects_malformed_literals(string literal)
	{
		Assert.False(TryParse(literal));
	}

	// ---------- 数组 ----------

	[Fact]
	public void Array_flag_is_honoured()
	{
		var value = Parse("T(1, 2, 3)", isArray: true);
		Assert.True(value.IsArray);
		Assert.Equal(new object?[] { 1L, 2L, 3L }, value.Values);
	}

	[Fact]
	public void Array_from_registered_type_name()
	{
		// UDTCODEC_INT_ARR 注册为数组工厂（元素类型 int）→ 被识别为数组
		var value = Parse("UDTCODEC_INT_ARR(1, 2)");
		Assert.True(value.IsArray);
	}

	// ---------- 元素分隔规则 ----------

	[Fact]
	public void Commas_inside_quotes_do_not_split()
	{
		var value = Parse("T('a,b', 2)");
		Assert.Equal(new object?[] { "a,b", 2L }, value.Values);
	}

	[Fact]
	public void Commas_inside_nested_parens_do_not_split()
	{
		var value = Parse("T(1, INNER('x,y', 2), 3)");
		Assert.Equal(3, value.Values.Count);
		Assert.Equal(1L, value.Values[0]);
		Assert.Equal(3L, value.Values[2]);
		Assert.IsType<OracleUdtValue>(value.Values[1]);
	}

	[Fact]
	public void Nested_udt_is_parsed_recursively()
	{
		var outer = Parse("OUTER(1, INNER(2, 'x'))");
		var inner = Assert.IsType<OracleUdtValue>(outer.Values[1]);
		Assert.Equal("INNER", inner.TypeName);
		Assert.Equal(new object?[] { 2L, "x" }, inner.Values);
	}

	[Fact]
	public void Escaped_quotes_inside_string_are_unescaped()
	{
		// Oracle 的 '' 表示一个字面单引号
		var value = Parse("T('it''s')");
		Assert.Equal(new object?[] { "it's" }, value.Values);
	}

	// ---------- 标量推断 ----------

	[Theory]
	[InlineData("T(42)", 42L)]
	[InlineData("T(-7)", -7L)]
	[InlineData("T(0)", 0L)]
	public void Integer_elements_become_long(string literal, long expected)
	{
		// 纯整数一律走 long.TryParse → 装箱为 long
		var value = Assert.IsType<long>(Parse(literal).Values[0]);
		Assert.Equal(expected, value);
	}

	[Fact]
	public void Integer_beyond_long_range_stays_decimal()
	{
		var value = Assert.IsType<decimal>(Parse("T(99999999999999999999)").Values[0]);
		Assert.Equal(99999999999999999999m, value);
	}

	[Fact]
	public void Fractional_elements_become_decimal()
	{
		var value = Assert.IsType<decimal>(Parse("T(1.5)").Values[0]);
		Assert.Equal(1.5m, value);
	}

	[Fact]
	public void Boolean_elements_are_recognized()
	{
		Assert.Equal(new object?[] { true }, Parse("T(true)").Values);
		Assert.Equal(new object?[] { false }, Parse("T(FALSE)").Values);
	}

	[Fact]
	public void Unquoted_identifiers_stay_strings()
	{
		Assert.Equal(new object?[] { "abc" }, Parse("T(abc)").Values);
	}

	[Fact]
	public void Null_elements_become_DBNull()
	{
		Assert.Equal(new object?[] { DBNull.Value }, Parse("T(NULL)").Values);
		Assert.Equal(new object?[] { DBNull.Value }, Parse("T(null)").Values);
	}

	[Fact]
	public void Empty_element_list_yields_no_values()
	{
		// 注意：体为空时 SplitElements 返回空列表（而非一个 DBNull）
		Assert.Empty(Parse("T()").Values);
	}

	[Fact]
	public void Empty_elements_become_DBNull()
	{
		// "T(1, )" 的第二个元素是空串 → DBNull
		var value = Parse("T(1, )");
		Assert.Equal(DBNull.Value, value.Values[1]);
	}

	[Fact]
	public void Quoted_empty_string_stays_empty_string()
	{
		// '' 是空字符串，与 NULL 不同
		Assert.Equal(new object?[] { "" }, Parse("T('')").Values);
	}

	// ---------- hextoraw ----------

	[Fact]
	public void Hextoraw_element_for_raw_attribute_becomes_bytes()
	{
		// R 属性（第 3 个）推断为 Raw → hextoraw(...) 解码为字节
		var value = Parse($"{SmallType}(1, 'x', hextoraw('414243'))");
		Assert.Equal(new byte[] { 0x41, 0x42, 0x43 }, Assert.IsType<byte[]>(value.Values[2]));
	}

	[Fact]
	public void Hextoraw_element_without_raw_attribute_parses_as_nested_udt()
	{
		// 属性类型未知时，带括号的元素先按嵌套 UDT 字面量尝试解析，
		// 因此 hextoraw(...) 会得到 TypeName="hextoraw" 的嵌套值，而不是字节数组。
		var value = Parse("T(hextoraw('414243'))");
		var nested = Assert.IsType<OracleUdtValue>(value.Values[0]);
		Assert.Equal("hextoraw", nested.TypeName);
	}

	[Fact]
	public void Bare_quoted_hex_for_raw_attribute_becomes_bytes()
	{
		// R 属性推断为 Raw，因此 '414243' 按十六进制解码
		var value = Parse($"{SmallType}(1, 'x', '414243')");
		Assert.Equal(new byte[] { 0x41, 0x42, 0x43 }, Assert.IsType<byte[]>(value.Values[2]));
	}

	// ---------- 注册类型的属性数归一化 ----------

	[Fact]
	public void Registered_type_pads_missing_elements()
	{
		// UDTCODEC_SMALL 注册了 3 个属性；只给 1 个元素时补空 → DBNull
		var value = Parse($"{SmallType}(1)");
		Assert.Equal(3, value.Values.Count);
		Assert.Equal(1L, value.Values[0]);
		Assert.Equal(DBNull.Value, value.Values[1]);
		Assert.Equal(DBNull.Value, value.Values[2]);
	}

	[Fact]
	public void Registered_type_trims_trailing_empty_elements()
	{
		// 多出的尾部空白元素会被裁掉
		var value = Parse($"{SmallType}(1, 'x', '414243', )");
		Assert.Equal(3, value.Values.Count);
	}

	[Fact]
	public void Unregistered_type_keeps_element_count()
	{
		var value = Parse("UNKNOWN_TYPE(1, 2, 3, 4)");
		Assert.Equal(4, value.Values.Count);
	}

	[Fact]
	public void Registered_type_name_is_matched_case_insensitively()
	{
		var value = Parse($"{SmallType.ToLowerInvariant()}(1)");
		Assert.Equal(3, value.Values.Count);
	}

	[Fact]
	public void Schema_qualified_registered_name_is_matched()
	{
		// 注册名可带 schema 前缀（TypeNameMatches 支持后缀匹配）
		var value = Parse($"MY_SCHEMA.{SmallType}(1)");
		Assert.Equal(3, value.Values.Count);
	}

	// ---------- 多行文本归一化 ----------

	[Fact]
	public void Carriage_returns_and_newlines_are_stripped()
	{
		var value = Parse("T(1,\r\n2,\n3)");
		Assert.Equal(new object?[] { 1L, 2L, 3L }, value.Values);
	}

	[Fact]
	public void Newlines_inside_quoted_strings_are_preserved()
	{
		// 引号内的换行属于数据，不应被剥离
		var value = Parse("T('a\nb')");
		Assert.Equal(new object?[] { "a\nb" }, value.Values);
	}

	// ---------- ReadValue（经由列定义）----------

	private static ColumnDefinitionPayload CreateColumnDefinition(string complexTypeName, byte protocolType = 160)
	{
		var w = new ByteBufferWriter(128);
		w.WriteLengthEncodedString("def");
		w.WriteLengthEncodedString("db");
		w.WriteLengthEncodedString("t");
		w.WriteLengthEncodedString("t");
		w.WriteLengthEncodedString("c");
		w.WriteLengthEncodedString("c");
		w.Write((byte)12);
		w.Write((ushort)CharacterSet.Utf8Mb4GeneralCaseInsensitive);
		w.Write(100u);
		w.Write(protocolType);
		w.Write((ushort)0);
		w.Write((byte)0);
		w.Write((byte)0);
		w.Write((byte)0);
		w.WriteLengthEncodedString("");
		w.WriteLengthEncodedString(complexTypeName);
		var bytes = w.ToPayloadData().Span.ToArray();

		var resizable = new ResizableArray<byte>();
		Utility.Resize(ref resizable, bytes.Length);
		bytes.CopyTo(resizable.Array!.AsSpan(0));
		ColumnDefinitionPayload payload = null!;
		ColumnDefinitionPayload.Initialize(ref payload, new ResizableArraySegment<byte>(resizable, 0, bytes.Length), isOracleMode: true);
		return payload;
	}

	[Fact]
	public void ReadValue_parses_text_payload()
	{
		var reader = new TextOracleUdtColumnReader(isArray: false);
		var column = CreateColumnDefinition("MY_TYPE");
		var value = Assert.IsType<OracleUdtValue>(reader.ReadValue(Encoding.UTF8.GetBytes("MY_TYPE(1, 'x')"), column));
		Assert.Equal("MY_TYPE", value.TypeName);
		Assert.Equal(new object?[] { 1L, "x" }, value.Values);
	}

	[Fact]
	public void ReadValue_uses_column_complex_type_name_for_array_detection()
	{
		// 列定义里注册为数组类型 → 即使 reader 的 isArray 为 false 也按数组解析
		var reader = new TextOracleUdtColumnReader(isArray: false);
		var column = CreateColumnDefinition("UDTCODEC_INT_ARR");
		var value = Assert.IsType<OracleUdtValue>(reader.ReadValue(Encoding.UTF8.GetBytes("UDTCODEC_INT_ARR(1, 2)"), column));
		Assert.True(value.IsArray);
	}

	[Fact]
	public void ReadValue_throws_for_unparseable_payload()
	{
		var reader = new TextOracleUdtColumnReader(isArray: false);
		var column = CreateColumnDefinition("MY_TYPE");
		var ex = Assert.Throws<FormatException>(
			() => reader.ReadValue(Encoding.UTF8.GetBytes("garbage"), column));
		Assert.Contains("garbage", ex.Message);
	}

	[Fact]
	public void ReadValue_honours_custom_encoding()
	{
		// 用 Latin-1 编码的载荷应能被同编码的 reader 正确解码
		var reader = new TextOracleUdtColumnReader(isArray: false, encoding: Encoding.Latin1);
		var column = CreateColumnDefinition("MY_TYPE");
		var value = Assert.IsType<OracleUdtValue>(reader.ReadValue(Encoding.Latin1.GetBytes("MY_TYPE('café')"), column));
		Assert.Equal(new object?[] { "café" }, value.Values);
	}

	[Fact]
	public void Reader_defaults_to_utf8_when_encoding_is_null()
	{
		var reader = new TextOracleUdtColumnReader(isArray: false, encoding: null);
		var column = CreateColumnDefinition("MY_TYPE");
		var value = Assert.IsType<OracleUdtValue>(reader.ReadValue(Encoding.UTF8.GetBytes("MY_TYPE('中文')"), column));
		Assert.Equal(new object?[] { "中文" }, value.Values);
	}
}
