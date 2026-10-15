using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using OceanBase.Protocol;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// Oracle UDT（自定义对象类型）纯逻辑编解码测试（internal，无需服务端）。
///
/// 覆盖 <see cref="OracleUdtTdsEncoder"/> / <see cref="OracleUdtTdsDecoder"/> 的
/// 文本字面量与二进制线格式（「编码→解码」往返 + 字节级断言）、
/// <see cref="OracleUdtTextScalars"/> / <see cref="OracleUdtHexBytes"/> 文本标量、
/// <see cref="OracleUdtComplexScalarInference"/> 标量类型推断、
/// <see cref="OracleUdtTypeRegistry"/> 类型注册表，以及
/// <see cref="OracleUdtContext"/> / <see cref="OracleUdt"/> 可离线驱动的部分。
/// 测试用自定义类型定义在本类内部，注册表通过反射扫描测试程序集发现它们。
/// </summary>
public sealed class OracleUdtCodecTests
{
	// ---------- 测试用自定义类型（模拟用户注册的 UDT 映射）----------

	[OracleCustomTypeMapping("UDTCODEC_SMALL")]
	public sealed class UdtCodecSmall : IOracleCustomType
	{
		[OracleObjectMapping("N")]
		public int N { get; set; }

		[OracleObjectMapping("S")]
		public string? S { get; set; }

		[OracleObjectMapping("R")]
		public byte[]? R { get; set; }

		public void FromCustomObject(OracleConnection? con, object? udt)
		{
		}

		public void ToCustomObject(OracleConnection? con, object? udt)
		{
			var ctx = (OracleUdtContext)udt!;
			if (ctx.GetValue("N") is int n)
			{
				N = n;
			}
			if (ctx.GetValue("S") is string s)
			{
				S = s;
			}
			if (ctx.GetValue("R") is byte[] r)
			{
				R = r;
			}
		}
	}

	[OracleCustomTypeMapping("UDTCODEC_SMALL")]
	public sealed class UdtCodecSmallFactory : IOracleCustomTypeFactory
	{
		public IOracleCustomType CreateObject()
		{
			return new UdtCodecSmall();
		}
	}

	/// <summary>覆盖全部标量种类的宽结构（仅用于字面量/推断，不做二进制解码）。</summary>
	[OracleCustomTypeMapping("UDTCODEC_WIDE")]
	public sealed class UdtCodecWide : IOracleCustomType
	{
		[OracleObjectMapping("N")]
		public int N { get; set; }

		[OracleObjectMapping("D")]
		public decimal D { get; set; }

		[OracleObjectMapping("S")]
		public string? S { get; set; }

		[OracleObjectMapping("C")]
		public char C { get; set; }

		[OracleObjectMapping("RW")]
		public byte[]? RW { get; set; }

		[OracleObjectMapping("T")]
		public DateTime T { get; set; }

		[OracleObjectMapping("F")]
		public float F { get; set; }

		[OracleObjectMapping("DB")]
		public double DB { get; set; }

		[OracleObjectMapping("YM")]
		public OracleIntervalYM YM { get; set; }

		[OracleObjectMapping("DS")]
		public OracleIntervalDS DS { get; set; }

		public void FromCustomObject(OracleConnection? con, object? udt)
		{
		}

		public void ToCustomObject(OracleConnection? con, object? udt)
		{
		}
	}

	/// <summary>六属性结构：YM/DS/时间戳/浮点/双精度/CHAR 的二进制往返。</summary>
	[OracleCustomTypeMapping("UDTCODEC_INTERVALS")]
	public sealed class UdtCodecIntervals : IOracleCustomType
	{
		[OracleObjectMapping("YM")]
		public OracleIntervalYM YM { get; set; }

		[OracleObjectMapping("DS")]
		public OracleIntervalDS DS { get; set; }

		[OracleObjectMapping("T")]
		public DateTime T { get; set; }

		[OracleObjectMapping("F")]
		public float F { get; set; }

		[OracleObjectMapping("DB")]
		public double DB { get; set; }

		[OracleObjectMapping("C")]
		public char C { get; set; }

		public void FromCustomObject(OracleConnection? con, object? udt)
		{
		}

		public void ToCustomObject(OracleConnection? con, object? udt)
		{
		}
	}

	/// <summary>嵌套结构：属性为自定义对象 + int 数组。</summary>
	[OracleCustomTypeMapping("UDTCODEC_HOLDER")]
	public sealed class UdtCodecHolder : IOracleCustomType
	{
		[OracleObjectMapping("INNER")]
		public UdtCodecSmall? Inner { get; set; }

		[OracleObjectMapping("ITEMS")]
		public int[]? Items { get; set; }

		public void FromCustomObject(OracleConnection? con, object? udt)
		{
		}

		public void ToCustomObject(OracleConnection? con, object? udt)
		{
		}
	}

	/// <summary>同时含对象映射与数组映射成员的结构（数组映射成员不参与标量推断）。</summary>
	[OracleCustomTypeMapping("UDTCODEC_MIXED")]
	public sealed class UdtCodecMixed : IOracleCustomType
	{
		[OracleObjectMapping("A")]
		public int A { get; set; }

		[OracleArrayMapping]
		public int[]? B { get; set; }

		[OracleObjectMapping("C")]
		public string? C { get; set; }

		public void FromCustomObject(OracleConnection? con, object? udt)
		{
		}

		public void ToCustomObject(OracleConnection? con, object? udt)
		{
		}
	}

	[OracleCustomTypeMapping("UDTCODEC_TS")]
	public sealed class UdtCodecTs : IOracleCustomType
	{
		[OracleObjectMapping("T")]
		public DateTime T { get; set; }

		public void FromCustomObject(OracleConnection? con, object? udt)
		{
		}

		public void ToCustomObject(OracleConnection? con, object? udt)
		{
		}
	}

	[OracleCustomTypeMapping("UDTCODEC_YM")]
	public sealed class UdtCodecYm : IOracleCustomType
	{
		[OracleObjectMapping("M")]
		public OracleIntervalYM M { get; set; }

		public void FromCustomObject(OracleConnection? con, object? udt)
		{
		}

		public void ToCustomObject(OracleConnection? con, object? udt)
		{
		}
	}

	[OracleCustomTypeMapping("UDTCODEC_DS")]
	public sealed class UdtCodecDs : IOracleCustomType
	{
		[OracleObjectMapping("D")]
		public OracleIntervalDS D { get; set; }

		public void FromCustomObject(OracleConnection? con, object? udt)
		{
		}

		public void ToCustomObject(OracleConnection? con, object? udt)
		{
		}
	}

	[OracleCustomTypeMapping("UDTCODEC_INT_ARR")]
	public sealed class UdtCodecIntArrayFactory : IOracleArrayTypeFactory
	{
		public Array CreateArray(int numElems)
		{
			return new int[numElems];
		}

		public Array? CreateStatusArray(int numElems)
		{
			return new OracleUdtStatus[numElems];
		}
	}

	[OracleCustomTypeMapping("UDTCODEC_ORDER")]
	public sealed class UdtCodecOrder : IOracleCustomType
	{
		[OracleObjectMapping("A")]
		public string? A { get; set; }

		[OracleObjectMapping("B")]
		public string? B { get; set; }

		// FromCustomObject 阶段用来覆盖 B 的取值（验证其优先级高于成员回填）
		public string? BOverride { get; set; }

		public void FromCustomObject(OracleConnection? con, object? udt)
		{
			if (BOverride != null)
			{
				((OracleUdtContext)udt!).SetValue("B", BOverride);
			}
		}

		public void ToCustomObject(OracleConnection? con, object? udt)
		{
		}
	}

	/// <summary>实现接口但未标注映射特性的类型（解析类型名应失败）。</summary>
	public sealed class UdtCodecUnmapped : IOracleCustomType
	{
		public void FromCustomObject(OracleConnection? con, object? udt)
		{
		}

		public void ToCustomObject(OracleConnection? con, object? udt)
		{
		}
	}

	private enum CodecTestEnum
	{
		A,
		B
	}

	// ---------- 辅助 ----------

	private static OracleUdtValue Struct(string typeName, params object?[] values)
	{
		return new OracleUdtValue(typeName, isArray: false, values);
	}

	private static OracleUdtValue ArrayValue(string typeName, params object?[] values)
	{
		return new OracleUdtValue(typeName, isArray: true, values);
	}

	private static string EncodeLiteralText(OracleUdtValue value, bool noBackslashEscapes = false)
	{
		return Encoding.UTF8.GetString(OracleUdtTdsEncoder.EncodeLiteralValue(value, noBackslashEscapes));
	}

	private static OracleUdtValue Decode(byte[] data, string typeName, bool isArray, Encoding? encoding = null)
	{
		return OracleUdtTdsDecoder.ReadValue(data, typeName, isArray, encoding);
	}

	private static string ToHexUpper(byte[] bytes)
	{
		return string.Concat(bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
	}

	// ---------- OracleUdtComplexScalarInference ----------

	[Theory]
	[InlineData(typeof(string), 2)]            // Varchar2
	[InlineData(typeof(char), 6)]              // Char
	[InlineData(typeof(bool), 2)]              // bool → Varchar2
	[InlineData(typeof(DateTime), 5)]          // Timestamp
	[InlineData(typeof(DateTimeOffset), 5)]
	[InlineData(typeof(OracleTimeStamp), 5)]
	[InlineData(typeof(OracleTimeStampLTZ), 5)]
	[InlineData(typeof(OracleTimeStampTZ), 5)]
	[InlineData(typeof(OracleIntervalYM), 7)]  // IntervalYM
	[InlineData(typeof(OracleIntervalDS), 8)]  // IntervalDS
	[InlineData(typeof(float), 9)]             // BinaryFloat
	[InlineData(typeof(double), 10)]           // BinaryDouble
	[InlineData(typeof(decimal), 1)]           // Number
	[InlineData(typeof(byte), 1)]
	[InlineData(typeof(sbyte), 1)]
	[InlineData(typeof(short), 1)]
	[InlineData(typeof(ushort), 1)]
	[InlineData(typeof(int), 1)]
	[InlineData(typeof(uint), 1)]
	[InlineData(typeof(long), 1)]
	[InlineData(typeof(ulong), 1)]
	[InlineData(typeof(CodecTestEnum), 1)]     // 枚举按 Number 处理
	[InlineData(typeof(byte[]), 3)]            // Raw
	[InlineData(typeof(object), 0)]            // Unknown
	[InlineData(typeof(Guid), 0)]
	public void InferFromClrType_maps_clr_types_to_wire_scalars(Type clrType, int expected)
	{
		// internal 枚举不能出现在 public 方法签名中，此处用 int 传入再转换
		Assert.Equal((OracleUdtComplexScalarType)expected, OracleUdtComplexScalarInference.InferFromClrType(clrType));
	}

	[Fact]
	public void InferMemberScalarType_treats_registered_element_arrays_as_nested_udt()
	{
		Assert.Equal(OracleUdtComplexScalarType.Raw, OracleUdtComplexScalarInference.InferMemberScalarType(typeof(byte[])));
		// int 元素已注册数组工厂 → 视作嵌套数组 UDT，退化为 Unknown（区别于未注册元素类型走 InferFromClrType）
		Assert.Equal(OracleUdtComplexScalarType.Unknown, OracleUdtComplexScalarInference.InferMemberScalarType(typeof(int[])));
		Assert.Equal(OracleUdtComplexScalarType.Unknown, OracleUdtComplexScalarInference.InferMemberScalarType(typeof(string[])));
	}

	[Fact]
	public void InferStructAttributeTypes_follows_mapped_member_order()
	{
		var types = OracleUdtComplexScalarInference.InferStructAttributeTypes("UDTCODEC_WIDE", 10);
		Assert.Equal(new[]
		{
			OracleUdtComplexScalarType.Number,
			OracleUdtComplexScalarType.Number,
			OracleUdtComplexScalarType.Varchar2,
			OracleUdtComplexScalarType.Char,
			OracleUdtComplexScalarType.Raw,
			OracleUdtComplexScalarType.Timestamp,
			OracleUdtComplexScalarType.BinaryFloat,
			OracleUdtComplexScalarType.BinaryDouble,
			OracleUdtComplexScalarType.IntervalYM,
			OracleUdtComplexScalarType.IntervalDS
		}, types);
	}

	[Fact]
	public void InferStructAttributeTypes_skips_array_mapping_members_and_pads_unknown()
	{
		// 数组映射成员 B 被跳过：只剩 A(Number)、C(Varchar2)
		var mixed = OracleUdtComplexScalarInference.InferStructAttributeTypes("UDTCODEC_MIXED", 2);
		Assert.Equal(new[] { OracleUdtComplexScalarType.Number, OracleUdtComplexScalarType.Varchar2 }, mixed);

		// 未注册类型 → 全 Unknown；valueCount 只用于补齐，不截断结果（成员多时返回成员数）
		Assert.Equal(new[] { OracleUdtComplexScalarType.Unknown, OracleUdtComplexScalarType.Unknown },
			OracleUdtComplexScalarInference.InferStructAttributeTypes("UDTCODEC_NOT_REGISTERED", 2));
		var padded = OracleUdtComplexScalarInference.InferStructAttributeTypes("UDTCODEC_MIXED", 5);
		Assert.Equal(5, padded.Count);
		Assert.Equal(OracleUdtComplexScalarType.Unknown, padded[2]);
		Assert.Equal(10, OracleUdtComplexScalarInference.InferStructAttributeTypes("UDTCODEC_WIDE", 3).Count);
	}

	// ---------- OracleUdtHexBytes ----------

	[Theory]
	[InlineData("DEADBEEF", "DEADBEEF")]          // 大写偶数位
	[InlineData("deadbeef", "DEADBEEF")]          // 小写
	[InlineData("0xDEADBEEF", "DEADBEEF")]        // 0x 前缀
	[InlineData("0xdeadbeef", "DEADBEEF")]
	[InlineData("  DEADBEEF  ", "DEADBEEF")]      // 首尾空白
	[InlineData("F", "0F")]                       // 奇数位左补 0
	[InlineData("0x", "")]                        // 只有前缀 → 空字节
	[InlineData("", "")]
	[InlineData("GG", null)]                      // 非十六进制字符
	[InlineData("DEADG", null)]                   // 奇数位且含非十六进制字符
	[InlineData("0xZZ", null)]
	public void TryDecodeHex_decodes_and_validates(string input, string? expectedHex)
	{
		var ok = OracleUdtHexBytes.TryDecodeHex(input, out var bytes);
		if (expectedHex == null)
		{
			Assert.False(ok);
		}
		else
		{
			Assert.True(ok);
			Assert.Equal(Convert.FromHexString(expectedHex), bytes);
		}
	}

	[Theory]
	[InlineData("DE-AD", "DEAD")]     // 剥离非十六进制字符后再解码
	[InlineData("de ad", "DEAD")]
	[InlineData("D", "0D")]
	[InlineData("", "")]              // 空串 → 空字节数组，返回 true
	[InlineData("zz", null)]          // 无任何十六进制字符 → false
	public void TryDecodeHexDigitsOnly_strips_punctuation(string input, string? expectedHex)
	{
		var ok = OracleUdtHexBytes.TryDecodeHexDigitsOnly(input, out var bytes);
		if (expectedHex == null)
		{
			Assert.False(ok);
		}
		else
		{
			Assert.True(ok);
			Assert.Equal(Convert.FromHexString(expectedHex), bytes);
		}
	}

	[Theory]
	[InlineData("hextoraw('DEADBEEF')", "DEADBEEF")]   // 函数调用形式（大小写不敏感）
	[InlineData("HEXTORAW('deadbeef')", "DEADBEEF")]
	[InlineData("'DEADBEEF'", "DEADBEEF")]             // 裸引号形式
	[InlineData("DEADBEEF", "DEADBEEF")]               // 裸十六进制
	[InlineData("hextoraw('DE''AD')", null)]           // '' 还原后为 DE'AD，不再是合法十六进制
	[InlineData("hextoraw(DEAD)", null)]               // 函数内必须带引号
	[InlineData("hextoraw('XY')", null)]               // 非十六进制
	public void TryParseHexRawElement_accepts_hextoraw_and_bare_hex(string input, string? expectedHex)
	{
		var ok = OracleUdtHexBytes.TryParseHexRawElement(input, out var bytes);
		if (expectedHex == null)
		{
			Assert.False(ok);
		}
		else
		{
			Assert.True(ok);
			Assert.Equal(Convert.FromHexString(expectedHex), bytes);
		}
	}

	[Theory]
	[InlineData("hextoraw('1')", true)]   // hextoraw 前缀恒判为十六进制
	[InlineData("'AB'", true)]
	[InlineData("AB12", true)]
	[InlineData("123", true)]             // 纯数字串也算（数字是十六进制字符）
	[InlineData("zz1", true)]             // 含任意一个十六进制字符即算
	[InlineData("zz", false)]             // 无十六进制字符
	[InlineData("1", false)]              // 长度 < 2
	[InlineData("", false)]
	public void LooksLikeHexLiteral_is_satisfied_by_any_hex_digit(string element, bool expected)
	{
		Assert.Equal(expected, OracleUdtHexBytes.LooksLikeHexLiteral(element));
	}

	[Fact]
	public void TryCoerceToRawBytes_accepts_byte_array_strings_and_hex()
	{
		var raw = new byte[] { 0x01 };
		Assert.False(OracleUdtHexBytes.TryCoerceToRawBytes(null, out _));
		Assert.False(OracleUdtHexBytes.TryCoerceToRawBytes(DBNull.Value, out _));
		Assert.False(OracleUdtHexBytes.TryCoerceToRawBytes(42, out _));

		Assert.True(OracleUdtHexBytes.TryCoerceToRawBytes(raw, out var same));
		Assert.Same(raw, same);

		Assert.True(OracleUdtHexBytes.TryCoerceToRawBytes("DEADBEEF", out var fromHex));
		Assert.Equal(Convert.FromHexString("DEADBEEF"), fromHex);

		// 带非十六进制字符的串：先按严格 hex 失败，再退化为「只取十六进制字符」
		Assert.True(OracleUdtHexBytes.TryCoerceToRawBytes("DE-AD", out var digitsOnly));
		Assert.Equal(Convert.FromHexString("DEAD"), digitsOnly);

		Assert.False(OracleUdtHexBytes.TryCoerceToRawBytes("zz", out _));
	}

	// ---------- OracleUdtTextScalars ----------

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("NULL")]
	[InlineData("null")]
	[InlineData("hextoraw('DEADBEEF')")]
	[InlineData("2026-01-02 03:04:05")]                 // 含数字的文本命中十六进制启发式
	[InlineData("TIMESTAMP '2026-01-02 03:04:05'")]
	[InlineData("'2026-01-02'")]
	public void TryParseTimestamp_rejects_hex_looking_and_null_text(string element)
	{
		// LooksLikeHex 只要求「含任意十六进制字符」，日期文本必含数字，因此全部被拦截
		Assert.False(OracleUdtTextScalars.TryParseTimestamp(element, out var timestamp));
		Assert.Equal(default(DateTime), timestamp);
	}

	[Theory]
	[InlineData("")]
	[InlineData("NULL")]
	[InlineData("3-5")]                       // 文本形式被十六进制启发式拦截
	[InlineData("'3-5'")]
	[InlineData("INTERVAL '3-5' YEAR(9) TO MONTH")]
	public void TryParseIntervalYm_rejects_plain_text_forms(string element)
	{
		Assert.False(OracleUdtTextScalars.TryParseIntervalYm(element, out var interval));
		Assert.True(interval.IsNull);
	}

	[Fact]
	public void TryParseIntervalYm_parses_wire_hex_bytes()
	{
		// 线格式 7 字节：负号位 + 年(int32 LE) + 月 + 年位数
		Assert.True(OracleUdtTextScalars.TryParseIntervalYm("hextoraw('00030000000501')", out var ym));
		Assert.False(ym.IsNull);
		Assert.Equal(3L, ym.Years);
		Assert.Equal(5, ym.Months);

		// 裸十六进制（7 字节）同样可解析；首字节 01 表示负号
		Assert.True(OracleUdtTextScalars.TryParseIntervalYm("01020000000301", out var negative));
		Assert.Equal("-2-03", negative.ToString());
	}

	[Fact]
	public void TryParseIntervalYm_rejects_wrong_sized_hex()
	{
		// 可强制为字节但长度不是 7 → false
		Assert.False(OracleUdtTextScalars.TryParseIntervalYm("DEADBEEF", out var interval));
		Assert.True(interval.IsNull);
	}

	[Fact]
	public void TryParseIntervalDs_roundtrips_wire_hex_bytes()
	{
		var ids = OracleIntervalDS.FromComponents(isNegative: false, 5, 1, 2, 3, 123456789);
		Assert.True(OracleUdtTextScalars.TryParseIntervalDs(ToHexUpper(ids.ToBytes()), out var parsed));
		Assert.Equal(ids, parsed);
		Assert.Equal("5 01:02:03.123456789", parsed.ToString());
	}

	[Theory]
	[InlineData("")]
	[InlineData("NULL")]
	[InlineData("5 01:02:03.5")]   // 文本形式被十六进制启发式拦截
	public void TryParseIntervalDs_rejects_plain_text_forms(string element)
	{
		Assert.False(OracleUdtTextScalars.TryParseIntervalDs(element, out var interval));
		Assert.True(interval.IsNull);
	}

	[Fact]
	public void CoerceStructScalarValue_coerces_raw_and_timestamp_values()
	{
		var raw = OracleUdtTextScalars.CoerceStructScalarValue(OracleUdtComplexScalarType.Raw, "hextoraw('DEADBEEF')");
		Assert.Equal(Convert.FromHexString("DEADBEEF"), raw);

		// 无法按 RAW 解释的字符串原样返回
		Assert.Equal("zz", OracleUdtTextScalars.CoerceStructScalarValue(OracleUdtComplexScalarType.Raw, "zz"));

		var dt = new DateTime(2026, 1, 2);
		Assert.Equal(dt, (DateTime)OracleUdtTextScalars.CoerceStructScalarValue(OracleUdtComplexScalarType.Timestamp, dt)!);
		// 十六进制样式的字符串 → DBNull；非十六进制且解析失败 → 原样返回
		Assert.Equal(DBNull.Value, OracleUdtTextScalars.CoerceStructScalarValue(OracleUdtComplexScalarType.Timestamp, "abc"));
		Assert.Equal("zulu", OracleUdtTextScalars.CoerceStructScalarValue(OracleUdtComplexScalarType.Timestamp, "zulu"));
	}

	[Fact]
	public void CoerceStructScalarValue_interval_values_pass_through_or_become_null()
	{
		var ym = OracleIntervalYM.FromYearsMonths(3, 5);
		Assert.Equal(ym, (OracleIntervalYM)OracleUdtTextScalars.CoerceStructScalarValue(OracleUdtComplexScalarType.IntervalYM, ym)!);

		var ids = OracleIntervalDS.FromComponents(false, 1, 0, 0, 0, 500000000);
		Assert.Equal(ids, (OracleIntervalDS)OracleUdtTextScalars.CoerceStructScalarValue(OracleUdtComplexScalarType.IntervalDS, ids)!);

		// 注意：十六进制样式的 INTERVAL 字符串被直接置为 Null，
		// 尽管 TryParseIntervalYm 本身能解析同样的十六进制（两处行为不一致，见汇报）
		Assert.True(((OracleIntervalYM)OracleUdtTextScalars.CoerceStructScalarValue(
			OracleUdtComplexScalarType.IntervalYM, "00030000000501")!).IsNull);
		Assert.True(((OracleIntervalDS)OracleUdtTextScalars.CoerceStructScalarValue(
			OracleUdtComplexScalarType.IntervalDS, " ")!).IsNull);

		// null / DBNull 原样透传；其他标量类型不做处理
		Assert.Null(OracleUdtTextScalars.CoerceStructScalarValue(OracleUdtComplexScalarType.Number, null));
		Assert.Equal(DBNull.Value, OracleUdtTextScalars.CoerceStructScalarValue(OracleUdtComplexScalarType.Raw, DBNull.Value));
		Assert.Equal("42", OracleUdtTextScalars.CoerceStructScalarValue(OracleUdtComplexScalarType.Number, "42"));
	}

	// ---------- OracleUdtTdsEncoder：文本字面量 ----------

	[Fact]
	public void EncodeLiteralValue_formats_all_registered_scalar_kinds()
	{
		var ym = OracleIntervalYM.FromYearsMonths(3, 5);
		var ids = OracleIntervalDS.FromComponents(isNegative: false, 5, 1, 2, 3, 123456789);
		var value = Struct("UDTCODEC_WIDE", 42, 1.50m, "hi'", 'Z', Convert.FromHexString("DEADBEEF"),
			new DateTime(2026, 1, 2, 3, 4, 5).AddTicks(1234560), 1.5f, 2.25d, ym, ids);
		Assert.Equal(
			"UDTCODEC_WIDE(42,1.50,'hi''','Z',hextoraw('DEADBEEF')," +
			"TIMESTAMP '2026-01-02 03:04:05.123456',1.5f,2.25," +
			"INTERVAL '3-05' YEAR(9) TO MONTH,INTERVAL '5 01:02:03.123456789' DAY(9) TO SECOND(9))",
			EncodeLiteralText(value));
	}

	[Fact]
	public void EncodeLiteralValue_formats_unregistered_values_with_generic_rules()
	{
		var value = Struct("T", 42, "hi'", null, true, 'Z', 1.5m, new byte[] { 0xDE },
			new DateTime(2026, 1, 2, 3, 4, 5).AddTicks(1234560), 2.5f, 3.5d);
		// 未注册类型 → 标量类型 Unknown：走通用分支（bool/char 加引号、byte[] 用 hextoraw、float 不带 f 后缀）
		Assert.Equal(
			"T(42,'hi''',NULL,'true','Z',1.5,hextoraw('DE')," +
			"TIMESTAMP '2026-01-02 03:04:05.123456',2.5,3.5)",
			EncodeLiteralText(value));
	}

	[Fact]
	public void EncodeLiteralValue_converts_datetimeoffset_to_utc()
	{
		var dto = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(2));
		// DateTimeOffset 分支取 UtcDateTime（+02:00 → 减 2 小时）
		Assert.Equal("T(TIMESTAMP '2026-01-02 01:04:05.000000')", EncodeLiteralText(Struct("T", dto)));
	}

	[Fact]
	public void EncodeLiteralValue_nests_struct_and_array_values_inline()
	{
		var nested = Struct("NESTED_T", 1, "x");
		var array = ArrayValue("ARR_T", 1, 2);
		Assert.Equal("T(NESTED_T(1,'x'),ARR_T(1,2),NULL)", EncodeLiteralText(Struct("T", nested, array, null)));
		// 空结构/空数组输出空参数列表
		Assert.Equal("E()", EncodeLiteralText(Struct("E")));
	}

	[Fact]
	public void EncodeLiteralValue_escapes_quotes_but_never_backslashes()
	{
		// noBackslashEscapes 形参当前不影响输出（Oracle 字面量只做 '' 转义）
		var value = Struct("T", "a\\b'c");
		Assert.Equal("T('a\\b''c')", EncodeLiteralText(value, noBackslashEscapes: false));
		Assert.Equal(EncodeLiteralText(value, noBackslashEscapes: false), EncodeLiteralText(value, noBackslashEscapes: true));
	}

	[Fact]
	public void Literal_encoding_is_stable_under_current_culture()
	{
		// 编码器内部固定 InvariantCulture，切换当前区域不影响输出
		var original = CultureInfo.CurrentCulture;
		CultureInfo.CurrentCulture = new CultureInfo("zh-CN");
		try
		{
			var literal = EncodeLiteralText(Struct("T", 1.5m, 2.5f, 3.5d, new DateTime(2026, 1, 2, 3, 4, 5)));
			Assert.Equal("T(1.5,2.5,3.5,TIMESTAMP '2026-01-02 03:04:05.000000')", literal);
		}
		finally
		{
			CultureInfo.CurrentCulture = original;
		}
	}

	// ---------- OracleUdtTdsEncoder：二进制字节级 ----------

	[Fact]
	public void EncodeBinaryValue_struct_null_bitmap_uses_value_index_bits()
	{
		// 结构：1 字节位图（ceil(3/8)=1），null 位置位 = 属性下标 i（见汇报：与解码端的 i+2 不一致）
		var bytes = OracleUdtTdsEncoder.EncodeBinaryValue(Struct("UDTCODEC_SMALL", 42, null, null));
		// 位图 0x06（bit1|bit2），随后仅写非空属性 N：变长前缀 2 + "42"（0x34 0x32）
		Assert.Equal(new byte[] { 0x06, 0x02, 0x34, 0x32 }, bytes);

		// 未注册结构同样以 i 为位下标：[42, null] → 0x02 位图 + "42"
		Assert.Equal(new byte[] { 0x02, 0x02, 0x34, 0x32 }, OracleUdtTdsEncoder.EncodeBinaryValue(Struct("T", 42, null)));
		Assert.Equal(new byte[] { 0x01 }, OracleUdtTdsEncoder.EncodeBinaryValue(Struct("T", new object?[] { null })));
	}

	[Fact]
	public void EncodeBinaryValue_array_prefixes_count_then_bitmap()
	{
		// 数组：变长整数元素个数 + 位图 + 元素数据；null 元素同样按下标 i 置位
		var bytes = OracleUdtTdsEncoder.EncodeBinaryValue(ArrayValue("ANY_ARR", null, 5));
		// 02=元素个数；01=位图(bit0)；01 35=变长前缀 1 + '5'
		Assert.Equal(new byte[] { 0x02, 0x01, 0x01, 0x35 }, bytes);
	}

	[Fact]
	public void EncodeBinaryValue_generic_float_writes_raw_little_endian_without_prefix()
	{
		// Unknown 标量的 float 直接写 4 字节小端，无长度前缀（见汇报：与解码端变长前缀读取不对称）
		var bytes = OracleUdtTdsEncoder.EncodeBinaryValue(Struct("T", 3.5f));
		Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x60, 0x40 }, bytes);

		// 空结构：0 个属性 → 位图 0 字节，输出为空
		Assert.Empty(OracleUdtTdsEncoder.EncodeBinaryValue(Struct("T")));
	}

	// ---------- 二进制「编码→解码」往返 ----------

	[Fact]
	public void Binary_roundtrip_small_struct_scalars()
	{
		var value = Struct("UDTCODEC_SMALL", 42, "XY", new byte[] { 0xAB });
		var decoded = Decode(OracleUdtTdsEncoder.EncodeBinaryValue(value), "UDTCODEC_SMALL", isArray: false);
		Assert.False(decoded.IsArray);
		Assert.Equal("UDTCODEC_SMALL", decoded.TypeName);
		Assert.Equal(42, decoded.Values[0]);
		Assert.Equal("XY", decoded.Values[1]);
		Assert.Equal(new byte[] { 0xAB }, (byte[])decoded.Values[2]!);
	}

	[Fact]
	public void Binary_roundtrip_honors_session_string_encoding()
	{
		// VARCHAR2 属性按会话编码读写：UTF-16 编码中文往返（NUMBER 恒为 UTF-8 数字文本）
		var value = Struct("UDTCODEC_SMALL", 1, "中文", new byte[] { 0x01 });
		var bytes = OracleUdtTdsEncoder.EncodeBinaryValue(value, Encoding.Unicode);
		var decoded = Decode(bytes, "UDTCODEC_SMALL", isArray: false, Encoding.Unicode);
		Assert.Equal(1, decoded.Values[0]);
		Assert.Equal("中文", decoded.Values[1]);
		Assert.Equal(new byte[] { 0x01 }, (byte[])decoded.Values[2]!);
	}

	[Fact]
	public void Binary_roundtrip_interval_timestamp_and_binary_float_scalars()
	{
		var ym = OracleIntervalYM.FromYearsMonths(3, 5);
		var ids = OracleIntervalDS.FromComponents(isNegative: false, 5, 1, 2, 3, 123456789);
		var dt = new DateTime(2026, 1, 2, 3, 4, 5).AddTicks(1234560);
		var value = Struct("UDTCODEC_INTERVALS", ym, ids, dt, 1.5f, 2.25d, 'Z');
		var decoded = Decode(OracleUdtTdsEncoder.EncodeBinaryValue(value), "UDTCODEC_INTERVALS", isArray: false);
		Assert.Equal(ym, (OracleIntervalYM)decoded.Values[0]!);
		Assert.Equal(ids, (OracleIntervalDS)decoded.Values[1]!);
		Assert.Equal(dt, (DateTime)decoded.Values[2]!);
		Assert.Equal(1.5f, (float)decoded.Values[3]!);
		Assert.Equal(2.25d, (double)decoded.Values[4]!);
		Assert.Equal('Z', (char)decoded.Values[5]!);
	}

	[Fact]
	public void Binary_roundtrip_nested_struct_and_nested_array()
	{
		var inner = Struct("UDTCODEC_SMALL", 7, "in", new byte[] { 0x01 });
		var value = Struct("UDTCODEC_HOLDER", inner, new int[] { 1, 2 });
		var decoded = Decode(OracleUdtTdsEncoder.EncodeBinaryValue(value), "UDTCODEC_HOLDER", isArray: false);

		var decodedInner = Assert.IsType<OracleUdtValue>(decoded.Values[0]);
		Assert.False(decodedInner.IsArray);
		Assert.Equal(7, decodedInner.Values[0]);
		Assert.Equal("in", decodedInner.Values[1]);
		Assert.Equal(new byte[] { 0x01 }, (byte[])decodedInner.Values[2]!);

		var decodedItems = Assert.IsType<OracleUdtValue>(decoded.Values[1]);
		Assert.True(decodedItems.IsArray);
		Assert.Equal("UDTCODEC_INT_ARR", decodedItems.TypeName.ToUpperInvariant());
		Assert.Equal(1, decodedItems.Values[0]);
		Assert.Equal(2, decodedItems.Values[1]);
	}

	[Fact]
	public void Binary_roundtrip_array_with_registered_factory()
	{
		// 顶部数组：注册了数组工厂后即使线格式说 isArray=false 也会按数组解码（ShouldDecodeAsArray）
		var value = ArrayValue("UDTCODEC_INT_ARR", 7, 8);
		var decoded = Decode(OracleUdtTdsEncoder.EncodeBinaryValue(value), "UDTCODEC_INT_ARR", isArray: false);
		Assert.True(decoded.IsArray);
		Assert.Equal(7, decoded.Values[0]);
		Assert.Equal(8, decoded.Values[1]);
	}

	[Fact]
	public void Binary_roundtrip_unregistered_array_parses_numbers_and_strings()
	{
		// 未注册数组：元素按 Unknown 标量处理 → 数字文本还原为数值类型，其余还原为字符串
		var value = ArrayValue("PLAIN_ARR", 1, "ab", 2.5m, -7);
		var decoded = Decode(OracleUdtTdsEncoder.EncodeBinaryValue(value), "PLAIN_ARR", isArray: true);
		Assert.Equal(1, decoded.Values[0]);
		Assert.Equal("ab", decoded.Values[1]);
		Assert.Equal(2.5m, (decimal)decoded.Values[2]!);
		Assert.Equal(-7, decoded.Values[3]);
	}

	[Fact]
	public void Decoder_reads_null_elements_from_bitmap_bits_offset_by_two()
	{
		// 解码端位图以 (i+2) 为下标：手工构造 bit2 置位表示第 0 个属性为 null
		var data = new byte[] { 0x04, 0x02, (byte)'X', (byte)'Y', 0x01, 0xAB };
		var decoded = Decode(data, "UDTCODEC_SMALL", isArray: false);
		Assert.True(decoded.Values[0] is DBNull);
		Assert.Equal("XY", decoded.Values[1]);
		Assert.Equal(new byte[] { 0xAB }, (byte[])decoded.Values[2]!);

		// 数组同理：count=3，位图 0x04 → 第 0 个元素 null，后两个为数字
		var arrayData = new byte[] { 0x03, 0x04, 0x01, (byte)'5', 0x01, (byte)'6' };
		var decodedArray = Decode(arrayData, "UDTCODEC_INT_ARR", isArray: false);
		Assert.True(decodedArray.IsArray);
		Assert.True(decodedArray.Values[0] is DBNull);
		Assert.Equal(5, decodedArray.Values[1]);
		Assert.Equal(6, decodedArray.Values[2]);
	}

	[Fact]
	public void Decoder_returns_dbnull_when_element_data_is_exhausted()
	{
		// 声明 2 个元素但只提供 1 个元素数据：BytesRemaining==0 → DBNull
		var data = new byte[] { 0x02, 0x00, 0x01, (byte)'7' };
		var decoded = Decode(data, "UDTCODEC_INT_ARR", isArray: false);
		Assert.Equal(7, decoded.Values[0]);
		Assert.True(decoded.Values[1] is DBNull);
	}

	// ---------- 解码错误契约 ----------

	[Fact]
	public void Decoder_requires_registered_type_for_struct()
	{
		var ex = Assert.Throws<InvalidOperationException>(() => Decode(Array.Empty<byte>(), "UDTCODEC_NOT_REGISTERED", isArray: false));
		Assert.Contains("Custom type mapping", ex.Message);

		var ex2 = Assert.Throws<InvalidOperationException>(() => Decode(Array.Empty<byte>(), "", isArray: false));
		Assert.Contains("UDT type name is required", ex2.Message);
	}

	[Fact]
	public void Decoder_throws_format_exception_for_short_fixed_size_scalars()
	{
		// TIMESTAMP 属性至少 11 字节
		var ts = Assert.Throws<FormatException>(() => Decode(new byte[] { 0x00, 0x02, 0x14, 0x1A }, "UDTCODEC_TS", isArray: false));
		Assert.Contains("11 bytes for TIMESTAMP", ts.Message);

		// INTERVAL YEAR TO MONTH 恰好 7 字节
		var ym = Assert.Throws<FormatException>(() => Decode(new byte[] { 0x00, 0x03, 0x01, 0x02, 0x03 }, "UDTCODEC_YM", isArray: false));
		Assert.Contains("7 bytes for INTERVAL YEAR TO MONTH", ym.Message);

		// INTERVAL DAY TO SECOND 恰好 14 字节
		var ds = Assert.Throws<FormatException>(() => Decode(new byte[] { 0x00, 0x02, 0x01, 0x02 }, "UDTCODEC_DS", isArray: false));
		Assert.Contains("14 bytes for INTERVAL DAY TO SECOND", ds.Message);
	}

	[Theory]
	[InlineData("UDTCODEC_INT_ARR", false, true)]    // 注册了数组工厂
	[InlineData("UDTCODEC_MIXED", false, true)]      // 含数组映射成员
	[InlineData("UDTCODEC_SMALL", false, false)]     // 纯对象结构
	[InlineData("UDTCODEC_NOT_REGISTERED", false, false)]
	[InlineData("", false, false)]
	[InlineData("UDTCODEC_SMALL", true, true)]       // 线格式已声明数组
	public void ShouldDecodeAsArray_considers_registration_and_wire_flag(string typeName, bool wireSaysArray, bool expected)
	{
		Assert.Equal(expected, OracleUdt.ShouldDecodeAsArray(typeName, wireSaysArray));
	}

	// ---------- OracleUdtTypeRegistry ----------

	[Theory]
	[InlineData("UDTCODEC_SMALL")]            // 原名
	[InlineData("udtcodec_small")]            // 大小写不敏感
	[InlineData("\"UDTCODEC_SMALL\"")]        // 带双引号的标识符
	[InlineData("  UDTCODEC_SMALL  ")]        // 首尾空白
	[InlineData("SYS.UDTCODEC_SMALL")]        // schema 前缀后缀匹配
	public void TryGetCustomType_resolves_registered_names(string requested)
	{
		Assert.True(OracleUdtTypeRegistry.TryGetCustomType(requested, out var type));
		Assert.Equal(typeof(UdtCodecSmall), type);
	}

	[Fact]
	public void TryGetCustomType_returns_false_for_unknown_names()
	{
		Assert.False(OracleUdtTypeRegistry.TryGetCustomType("UDTCODEC_NO_SUCH_TYPE", out var type));
		Assert.Null(type);
	}

	[Fact]
	public void Registry_resolves_factories_by_mapping_name()
	{
		Assert.True(OracleUdtTypeRegistry.TryGetFactoryType("UDTCODEC_SMALL", out var factory));
		Assert.Equal(typeof(UdtCodecSmallFactory), factory);

		Assert.True(OracleUdtTypeRegistry.TryGetArrayFactoryType("udtcodec_int_arr", out var arrayFactory));
		Assert.Equal(typeof(UdtCodecIntArrayFactory), arrayFactory);

		// 数组工厂名不能当对象工厂解析，反之亦然
		Assert.False(OracleUdtTypeRegistry.TryGetFactoryType("UDTCODEC_INT_ARR", out _));
		Assert.False(OracleUdtTypeRegistry.TryGetArrayFactoryType("UDTCODEC_SMALL", out _));
	}

	[Fact]
	public void TryGetArrayTypeNameForElementType_finds_registered_element_type()
	{
		Assert.True(OracleUdtTypeRegistry.TryGetArrayTypeNameForElementType(typeof(int), out var typeName));
		Assert.Equal("UDTCODEC_INT_ARR", typeName);

		Assert.False(OracleUdtTypeRegistry.TryGetArrayTypeNameForElementType(typeof(string), out _));
	}

	// ---------- OracleUdtContext ----------

	[Fact]
	public void Context_named_access_normalizes_attribute_names()
	{
		var ctx = new OracleUdtContext(isArray: false);
		ctx.SetValue("  n ", 42);
		// 属性名做 Trim/去引号/大写归一化，读取端各种写法等价
		Assert.Equal(42, ctx.GetValue("N"));
		Assert.Equal(42, ctx.GetValue("n"));
		Assert.Equal(42, ctx.GetValue("\"N\""));
		Assert.Null(ctx.GetValue("MISSING"));

		// 重复 SetValue 覆盖而非追加
		ctx.SetValue("N", 43);
		Assert.Equal(43, ctx.GetValue("N"));
	}

	[Fact]
	public void Context_indexed_access_validates_and_defaults_to_null()
	{
		var ctx = new OracleUdtContext(isArray: true);
		ctx.SetValue(0, "a");
		Assert.Equal("a", ctx.GetValue(0));
		Assert.Null(ctx.GetValue(7));
		Assert.Throws<ArgumentOutOfRangeException>(() => ctx.SetValue(-1, "x"));
		Assert.Throws<ArgumentOutOfRangeException>(() => ctx.GetValue(-1));
	}

	[Fact]
	public void Context_IsDBNull_and_SetDBNull()
	{
		var ctx = new OracleUdtContext(isArray: false);
		ctx.SetValue("A", DBNull.Value);
		ctx.SetValue("B", 1);
		Assert.True(ctx.IsDBNull("A"));
		Assert.False(ctx.IsDBNull("B"));
		// 不存在的属性名 GetValue 返回 null → IsDBNull 视为真
		Assert.True(ctx.IsDBNull("MISSING"));

		var indexed = new OracleUdtContext(isArray: true);
		indexed.SetValue(0, null);
		Assert.True(indexed.IsDBNull(0));
	}

	[Fact]
	public void Context_set_value_applies_custom_mapping_coercion()
	{
		var ctx = new OracleUdtContext(isArray: false) { CustomMappingType = typeof(UdtCodecSmall) };
		// CustomMappingType 命中 R(byte[]) 成员 → RAW 强制转换
		ctx.SetValue("R", "hextoraw('DEADBEEF')");
		Assert.Equal(Convert.FromHexString("DEADBEEF"), ctx.GetValue("R"));

		// 未命中成员且属性名不像 RAW → 原样保存
		ctx.SetValue("OTHER", "DEADBEEF");
		Assert.Equal("DEADBEEF", ctx.GetValue("OTHER"));
	}

	[Fact]
	public void Context_FillFromMappedMembers_orders_values_by_member_declaration()
	{
		var ctx = new OracleUdtContext(isArray: false);
		ctx.FillFromMappedMembers(new UdtCodecSmall { N = 1, S = "x", R = new byte[] { 9 } });
		var value = ctx.ToValue("UDTCODEC_SMALL");
		Assert.False(value.IsArray);
		Assert.Equal("UDTCODEC_SMALL", value.TypeName);
		Assert.Equal(1, value.Values[0]);
		Assert.Equal("x", value.Values[1]);
		Assert.Equal(new byte[] { 9 }, (byte[])value.Values[2]!);
	}

	[Fact]
	public void Context_FromCustomObject_values_win_over_member_backfill()
	{
		// FromCustomObject 中显式 SetValue 的属性优先；FillFromMappedMembers 不再覆盖
		var obj = new UdtCodecOrder { A = "a", B = "b", BOverride = "ovr" };
		var ctx = new OracleUdtContext(isArray: false);
		obj.FromCustomObject(null!, ctx);
		ctx.FillFromMappedMembers(obj);
		var value = ctx.ToValue("UDTCODEC_ORDER");
		Assert.Equal("a", value.Values[0]);
		Assert.Equal("ovr", value.Values[1]);
	}

	[Fact]
	public void Context_ToValue_for_structs_falls_back_to_named_insertion_order()
	{
		// 未调用 FillFromMappedMembers（无映射顺序可用）→ 按命名值插入顺序输出
		var ctx = new OracleUdtContext(isArray: false);
		ctx.SetValue("Z", 1);
		ctx.SetValue("A", 2);
		var value = ctx.ToValue("T");
		Assert.Equal(1, value.Values[0]);
		Assert.Equal(2, value.Values[1]);
	}

	[Fact]
	public void Context_ToValue_for_arrays_spreads_or_sorts_indexed_values()
	{
		// 唯一索引 0 且为 IEnumerable → 展开为元素
		var spread = new OracleUdtContext(isArray: true);
		spread.SetValue(0, new int[] { 1, 2 });
		var spreadValue = spread.ToValue("ARR");
		Assert.True(spreadValue.IsArray);
		Assert.Equal(1, spreadValue.Values[0]);
		Assert.Equal(2, spreadValue.Values[1]);

		// 多个索引 → 按索引升序（空洞被丢弃）
		var sparse = new OracleUdtContext(isArray: true);
		sparse.SetValue(1, "a");
		sparse.SetValue(3, "c");
		var sparseValue = sparse.ToValue("ARR");
		Assert.Equal(2, sparseValue.Values.Count);
		Assert.Equal("a", sparseValue.Values[0]);
		Assert.Equal("c", sparseValue.Values[1]);

		// 单个非集合值直接作为唯一元素
		var single = new OracleUdtContext(isArray: true);
		single.SetValue(0, "solo");
		var singleValue = single.ToValue("ARR");
		Assert.Equal("solo", singleValue.Values[0]);
	}

	// ---------- OracleUdt 静态 API（离线路径）----------

	[Fact]
	public void OracleUdt_static_accessors_work_against_a_context_without_connection()
	{
		var ctx = new OracleUdtContext(isArray: false);
		OracleUdt.SetValue(null!, ctx, "N", 42);
		Assert.Equal(42, OracleUdt.GetValue(null!, ctx, "N"));

		OracleUdt.SetDBNull(null!, ctx, "N");
		Assert.True(OracleUdt.IsDBNull(null!, ctx, "N"));

		var indexed = new OracleUdtContext(isArray: true);
		OracleUdt.SetValue(null!, indexed, 0, "a");
		Assert.Equal("a", OracleUdt.GetValue(null!, indexed, 0));
		// 未设置的下标取值为 null → IsDBNull 为真
		Assert.True(OracleUdt.IsDBNull(null!, indexed, 1));
		Assert.Null(OracleUdt.GetValue(null!, indexed, 1));
	}

	[Fact]
	public void OracleUdt_GetValue_coerces_likely_raw_attribute_names()
	{
		// 未设置 CustomMappingType 时按属性名启发式：RAW_VAL / *_RAW 后缀 → 十六进制串转字节
		var ctx = new OracleUdtContext(isArray: false);
		ctx.SetValue("RAW_VAL", "DEADBEEF");
		Assert.Equal(Convert.FromHexString("DEADBEEF"), OracleUdt.GetValue(null!, ctx, "RAW_VAL"));

		ctx.SetValue("DATA_RAW", "0x01");
		Assert.Equal(new byte[] { 0x01 }, OracleUdt.GetValue(null!, ctx, "DATA_RAW"));

		ctx.SetValue("NAME", "DEADBEEF");
		Assert.Equal("DEADBEEF", OracleUdt.GetValue(null!, ctx, "NAME"));
	}

	[Fact]
	public void OracleUdt_static_accessors_validate_arguments()
	{
		var ctx = new OracleUdtContext(isArray: false);
		Assert.Throws<ArgumentNullException>(() => OracleUdt.SetValue(null!, ctx, null!, 1));
		Assert.Throws<ArgumentNullException>(() => OracleUdt.GetValue(null!, ctx, null!));
		Assert.Throws<ArgumentNullException>(() => OracleUdt.IsDBNull(null!, ctx, null!));
		Assert.Throws<ArgumentNullException>(() => OracleUdt.SetDBNull(null!, ctx, null!));
		Assert.Throws<ArgumentNullException>(() => OracleUdt.SetValue(null!, null!, "N", 1));
		Assert.Throws<InvalidOperationException>(() => OracleUdt.SetValue(null!, new object(), "N", 1));
	}

	[Fact]
	public void OracleUdt_wrapper_exposes_indexed_access_to_struct_values()
	{
		// OracleUdt 包装 OracleUdtValue：结构按属性下标访问
		var wrapper = new OracleUdt(Struct("T", 1, "two"));
		Assert.Equal(1, OracleUdt.GetValue(null!, wrapper, 0));
		Assert.Equal("two", OracleUdt.GetValue(null!, wrapper, 1));
	}

	[Theory]
	[InlineData(true, null)]
	[InlineData(true, "")]
	[InlineData(true, "DBNull")]
	public void IsNullParameterValue_recognizes_null_sentinels(bool expected, string? kind)
	{
		object? value = kind switch
		{
			null => null,
			"" => DBNull.Value,
			"DBNull" => DBNull.Value,
			_ => throw new ArgumentException()
		};
		Assert.Equal(expected, OracleUdt.IsNullParameterValue(value));

		// IOracleNullable 空哨兵
		Assert.True(OracleUdt.IsNullParameterValue(OracleIntervalYM.Null));
		Assert.True(OracleUdt.IsNullParameterValue(OracleIntervalDS.Null));
		// 非空值
		Assert.False(OracleUdt.IsNullParameterValue(42));
		Assert.False(OracleUdt.IsNullParameterValue("x"));
		Assert.False(OracleUdt.IsNullParameterValue(OracleIntervalYM.FromYearsMonths(1, 2)));
	}

	[Fact]
	public void TryCreateValue_builds_value_from_mapped_custom_object()
	{
		var obj = new UdtCodecSmall { N = 42, S = "x", R = new byte[] { 1 } };
		Assert.True(OracleUdt.TryCreateValue(null, obj, null, treatAsArray: false, out var value));
		Assert.Equal("UDTCODEC_SMALL", value.TypeName);
		Assert.False(value.IsArray);
		Assert.Equal(42, value.Values[0]);
		Assert.Equal("x", value.Values[1]);
		Assert.Equal(new byte[] { 1 }, (byte[])value.Values[2]!);

		// 成员为 null → 值列表中保留 null
		Assert.True(OracleUdt.TryCreateValue(null, new UdtCodecSmall { N = 1 }, null, treatAsArray: false, out var withNull));
		Assert.Null(withNull.Values[1]);
	}

	[Fact]
	public void TryCreateValue_rejects_unmapped_and_non_custom_values()
	{
		Assert.False(OracleUdt.TryCreateValue(null, new UdtCodecUnmapped(), null, treatAsArray: false, out _));
		Assert.False(OracleUdt.TryCreateValue(null, "plain", null, treatAsArray: false, out _));
		Assert.False(OracleUdt.TryCreateValue(null, null!, null, treatAsArray: false, out _));
	}

	[Fact]
	public void TryCreateValue_supports_array_treatment()
	{
		// 显式 udtTypeName + IEnumerable → 数组值
		Assert.True(OracleUdt.TryCreateValue(null, new int[] { 1, 2 }, "MY_ARR", treatAsArray: true, out var named));
		Assert.Equal("MY_ARR", named.TypeName);
		Assert.True(named.IsArray);
		Assert.Equal(2, named.Values.Count);

		// 未提供名称时回退到元素类型的映射特性（UdtCodecSmall[] → 元素的 UDTCODEC_SMALL）
		Assert.True(OracleUdt.TryCreateValue(null, new[] { new UdtCodecSmall { N = 1 } }, null, treatAsArray: true, out var byElement));
		Assert.Equal("UDTCODEC_SMALL", byElement.TypeName);
		Assert.True(byElement.IsArray);
		Assert.IsType<UdtCodecSmall>(byElement.Values[0]);
	}

	[Fact]
	public void TryCreateArrayValue_requires_registered_element_type()
	{
		Assert.True(OracleUdt.TryCreateArrayValue(new int[] { 7, 8 }, out var value));
		Assert.True(value.IsArray);
		Assert.Equal("UDTCODEC_INT_ARR", value.TypeName);
		Assert.Equal(7, value.Values[0]);
		Assert.Equal(8, value.Values[1]);

		// string 元素未注册数组工厂 → false；字符串本身不算集合
		Assert.False(OracleUdt.TryCreateArrayValue(new string[] { "a" }, out _));
		Assert.False(OracleUdt.TryCreateArrayValue("str", out _));
	}

	[Fact]
	public void CreateObject_materializes_custom_object_from_value()
	{
		var value = Struct("UDTCODEC_SMALL", 42, "XY", new byte[] { 0xAB });
		var obj = Assert.IsType<UdtCodecSmall>(OracleUdt.CreateObject(null, value));
		Assert.Equal(42, obj.N);
		Assert.Equal("XY", obj.S);
		Assert.Equal(new byte[] { 0xAB }, obj.R);
	}

	[Fact]
	public void CreateObject_materializes_typed_array_from_factory()
	{
		var value = ArrayValue("UDTCODEC_INT_ARR", 7, 8);
		var array = Assert.IsType<int[]>(OracleUdt.CreateObject(null, value));
		Assert.Equal(new[] { 7, 8 }, array);
	}

	[Fact]
	public void CreateObject_returns_value_itself_when_type_unregistered()
	{
		var value = Struct("UDTCODEC_NOT_REGISTERED", 1);
		Assert.Same(value, OracleUdt.CreateObject(null, value));
	}

	[Fact]
	public void TryGetRegisteredAttributeCount_reports_mapped_members()
	{
		Assert.Equal(3, OracleUdt.TryGetRegisteredAttributeCount("UDTCODEC_SMALL"));
		Assert.Null(OracleUdt.TryGetRegisteredAttributeCount("UDTCODEC_NOT_REGISTERED"));
	}

	// ---------- 映射特性与状态枚举 ----------

	[Fact]
	public void OracleCustomTypeMappingAttribute_stores_type_name()
	{
		var attr = new OracleCustomTypeMappingAttribute("SCHEMA.TYPE");
		Assert.Equal("SCHEMA.TYPE", attr.UdtTypeName);
		Assert.Throws<ArgumentNullException>(() => new OracleCustomTypeMappingAttribute(null!));
	}

	[Fact]
	public void OracleObjectMappingAttribute_stores_attribute_name()
	{
		var attr = new OracleObjectMappingAttribute("ATTR");
		Assert.Equal("ATTR", attr.AttributeName);
		Assert.Throws<ArgumentNullException>(() => new OracleObjectMappingAttribute(null!));
	}

	[Fact]
	public void OracleArrayMappingAttribute_targets_properties_and_fields()
	{
		var usage = typeof(OracleArrayMappingAttribute).GetCustomAttribute<AttributeUsageAttribute>();
		Assert.NotNull(usage);
		Assert.True(usage!.ValidOn.HasFlag(AttributeTargets.Property));
		Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Field));
		Assert.False(usage.AllowMultiple);
		Assert.True(usage.Inherited);
	}

	[Fact]
	public void OracleUdtStatus_has_null_then_notnull()
	{
		Assert.Equal(0, (int)OracleUdtStatus.Null);
		Assert.Equal(1, (int)OracleUdtStatus.NotNull);
	}
}
