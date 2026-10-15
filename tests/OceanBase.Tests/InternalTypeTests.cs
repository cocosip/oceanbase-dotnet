using System;
using System.Reflection;
using System.Text;
using OceanBase.Protocol;
using OceanBase.Utilities;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 内部（internal）与私有（private）实现的单元测试。
///
/// 依赖主程序集 Properties/AssemblyInfo.cs 中的
/// <c>InternalsVisibleTo("OceanBase.Tests")</c> 访问 internal 类型；
/// 对 private 静态解析方法则通过反射调用——这些方法是协议解析的核心，
/// 虽非公开 API，却是重建后最需要回归保护的部分。
/// </summary>
public sealed class InternalTypeTests
{
	/// <summary>
	/// 调用内部类型上的私有静态解析方法（签名 <c>T DoReadValue(ReadOnlySpan&lt;byte&gt;)</c>）。
	/// 注意：<see cref="ReadOnlySpan{T}"/> 是 ref struct，无法装箱成 object，
	/// 因此这里创建强类型委托来调用，而不是用 MethodInfo.Invoke。
	/// </summary>
	private static T DoReadValue<T>(string typeName, ReadOnlySpan<byte> payload)
	{
		var asm = typeof(MySqlConnection).Assembly;
		var type = asm.GetType(typeName, throwOnError: true)!;
		var method = type.GetMethod("DoReadValue",
			BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException($"找不到方法 {typeName}.DoReadValue");

		var d = method.CreateDelegate<ReadValueDelegate<T>>();
		return d(payload);
	}

	private delegate T ReadValueDelegate<T>(ReadOnlySpan<byte> payload);

	/// <summary>调用 internal static 无参方法（如 CRC32 的查表构造、列类型映射表）。</summary>
	private static T InvokeStaticNoArg<T>(Type type, string methodName)
	{
		var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException($"找不到方法 {type.FullName}.{methodName}");
		return (T)method.Invoke(null, null)!;
	}

	private static ColumnType[] CreateMapOf(string methodName)
		=> InvokeStaticNoArg<ColumnType[]>(typeof(ColumnTypeMap), methodName);

	private static ReadOnlySpan<byte> Ascii(string s) => Encoding.ASCII.GetBytes(s);

	// ---------- 二进制列读取器（私有静态解析方法）----------

	[Theory]
	[InlineData("BinarySignedInt8ColumnReader", new byte[] { 0x7F }, (sbyte)127)]
	[InlineData("BinarySignedInt8ColumnReader", new byte[] { 0xFF }, (sbyte)-1)]
	[InlineData("BinarySignedInt8ColumnReader", new byte[] { 0x80 }, (sbyte)-128)]
	public void BinarySignedInt8_reads_twos_complement(string type, byte[] payload, sbyte expected)
	{
		var actual = DoReadValue<sbyte>($"OceanBase.ColumnReaders.{type}", payload);
		Assert.Equal(expected, actual);
	}

	[Theory]
	[InlineData("BinaryUnsignedInt8ColumnReader", new byte[] { 0x00 }, (byte)0)]
	[InlineData("BinaryUnsignedInt8ColumnReader", new byte[] { 0xFF }, (byte)255)]
	public void BinaryUnsignedInt8_reads_all_values(string type, byte[] payload, byte expected)
	{
		var actual = DoReadValue<byte>($"OceanBase.ColumnReaders.{type}", payload);
		Assert.Equal(expected, actual);
	}

	[Fact]
	public void BinarySignedInt16_is_little_endian()
	{
		Assert.Equal((short)0x1234, DoReadValue<short>("OceanBase.ColumnReaders.BinarySignedInt16ColumnReader", new byte[] { 0x34, 0x12 }));
		Assert.Equal((short)-1, DoReadValue<short>("OceanBase.ColumnReaders.BinarySignedInt16ColumnReader", new byte[] { 0xFF, 0xFF }));
	}

	[Fact]
	public void BinaryUnsignedInt16_is_little_endian()
	{
		Assert.Equal((ushort)0x1234, DoReadValue<ushort>("OceanBase.ColumnReaders.BinaryUnsignedInt16ColumnReader", new byte[] { 0x34, 0x12 }));
		Assert.Equal(ushort.MaxValue, DoReadValue<ushort>("OceanBase.ColumnReaders.BinaryUnsignedInt16ColumnReader", new byte[] { 0xFF, 0xFF }));
	}

	[Fact]
	public void BinarySignedInt32_is_little_endian()
	{
		Assert.Equal(0x12345678, DoReadValue<int>("OceanBase.ColumnReaders.BinarySignedInt32ColumnReader", new byte[] { 0x78, 0x56, 0x34, 0x12 }));
		Assert.Equal(-1, DoReadValue<int>("OceanBase.ColumnReaders.BinarySignedInt32ColumnReader", new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }));
	}

	[Fact]
	public void BinaryUnsignedInt32_is_little_endian()
	{
		Assert.Equal(0x12345678u, DoReadValue<uint>("OceanBase.ColumnReaders.BinaryUnsignedInt32ColumnReader", new byte[] { 0x78, 0x56, 0x34, 0x12 }));
	}

	[Fact]
	public void BinarySignedInt64_is_little_endian()
	{
		Assert.Equal(0x0102030405060708L,
			DoReadValue<long>("OceanBase.ColumnReaders.BinarySignedInt64ColumnReader", new byte[] { 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 }));
	}

	[Fact]
	public void BinaryUnsignedInt64_is_little_endian()
	{
		Assert.Equal(0x0102030405060708UL,
			DoReadValue<ulong>("OceanBase.ColumnReaders.BinaryUnsignedInt64ColumnReader", new byte[] { 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 }));
	}

	[Fact]
	public void BinaryBoolean_treats_nonzero_as_true()
	{
		Assert.False((bool)DoReadValue<bool>("OceanBase.ColumnReaders.BinaryBooleanColumnReader", new byte[] { 0x00 })!);
		Assert.True((bool)DoReadValue<bool>("OceanBase.ColumnReaders.BinaryBooleanColumnReader", new byte[] { 0x01 })!);
		Assert.True((bool)DoReadValue<bool>("OceanBase.ColumnReaders.BinaryBooleanColumnReader", new byte[] { 0xFF })!);
	}

	[Fact]
	public void BinaryYear_reads_uint16_little_endian()
	{
		// 实测：YEAR 在二进制协议里是 2 字节小端，值即年份本身（不加 1900）
		Assert.Equal(0, DoReadValue<int>("OceanBase.ColumnReaders.BinaryYearColumnReader", new byte[] { 0x00, 0x00 }));
		Assert.Equal(1, DoReadValue<int>("OceanBase.ColumnReaders.BinaryYearColumnReader", new byte[] { 0x01, 0x00 }));
		Assert.Equal(2024, DoReadValue<int>("OceanBase.ColumnReaders.BinaryYearColumnReader", new byte[] { 0xE8, 0x07 }));
	}

	// ---------- 文本列读取器 ----------

	[Fact]
	public void TextSignedInt8_parses_ascii_digits()
	{
		Assert.Equal((sbyte)42, DoReadValue<sbyte>("OceanBase.ColumnReaders.TextSignedInt8ColumnReader", Ascii("42")));
		Assert.Equal((sbyte)-7, DoReadValue<sbyte>("OceanBase.ColumnReaders.TextSignedInt8ColumnReader", Ascii("-7")));
	}

	[Fact]
	public void TextUnsignedInt8_parses_ascii_digits()
	{
		Assert.Equal((byte)200, DoReadValue<byte>("OceanBase.ColumnReaders.TextUnsignedInt8ColumnReader", Ascii("200")));
	}

	[Fact]
	public void TextSignedInt16_parses_ascii_digits()
	{
		Assert.Equal((short)-1234, DoReadValue<short>("OceanBase.ColumnReaders.TextSignedInt16ColumnReader", Ascii("-1234")));
	}

	[Fact]
	public void TextUnsignedInt16_parses_ascii_digits()
	{
		Assert.Equal((ushort)65535, DoReadValue<ushort>("OceanBase.ColumnReaders.TextUnsignedInt16ColumnReader", Ascii("65535")));
	}

	[Fact]
	public void TextSignedInt32_parses_ascii_digits()
	{
		Assert.Equal(-123456, DoReadValue<int>("OceanBase.ColumnReaders.TextSignedInt32ColumnReader", Ascii("-123456")));
	}

	[Fact]
	public void TextUnsignedInt32_parses_ascii_digits()
	{
		Assert.Equal(4294967295u, DoReadValue<uint>("OceanBase.ColumnReaders.TextUnsignedInt32ColumnReader", Ascii("4294967295")));
	}

	[Fact]
	public void TextSignedInt64_parses_ascii_digits()
	{
		Assert.Equal(-1234567890123L, DoReadValue<long>("OceanBase.ColumnReaders.TextSignedInt64ColumnReader", Ascii("-1234567890123")));
	}

	[Fact]
	public void TextUnsignedInt64_parses_ascii_digits()
	{
		Assert.Equal(18446744073709551615UL, DoReadValue<ulong>("OceanBase.ColumnReaders.TextUnsignedInt64ColumnReader", Ascii("18446744073709551615")));
	}

	[Fact]
	public void TextBoolean_parses_digits()
	{
		Assert.True((bool)DoReadValue<bool>("OceanBase.ColumnReaders.TextBooleanColumnReader", Ascii("1"))!);
		Assert.False((bool)DoReadValue<bool>("OceanBase.ColumnReaders.TextBooleanColumnReader", Ascii("0"))!);
	}

	// ---------- 校验和 ----------

	[Fact]
	public void OceanBaseCRC32_matches_standard_test_vectors()
	{
		Assert.Equal(0x00000000u, OceanBaseCRC32.Calculate(Array.Empty<byte>()));
		Assert.Equal(0x93AD1061u, OceanBaseCRC32.Calculate(Ascii("a")));
		Assert.Equal(0x562F9CCDu, OceanBaseCRC32.Calculate(Ascii("abc")));
		Assert.Equal(0x58E3FA20u, OceanBaseCRC32.Calculate(Ascii("123456789")));
	}

	[Fact]
	public void OceanBaseCRC32_table_has_256_entries()
	{
		Assert.Equal(256, InvokeStaticNoArg<uint[]>(typeof(OceanBaseCRC32), "CreateTable").Length);
	}

	// ---------- Utility ----------

	[Fact]
	public void Utility_FindNextIndex_finds_pattern()
	{
		var data = Ascii("hello world");
		Assert.Equal(6, Utility.FindNextIndex(data, 0, Ascii("world")));
		Assert.Equal(-1, Utility.FindNextIndex(data, 0, Ascii("xyz")));
		Assert.Equal(-1, Utility.FindNextIndex(data, 7, Ascii("hello")));
	}

	[Fact]
	public void Utility_Resize_allocates_collection()
	{
		ResizableArray<byte>? arr = null;
		Utility.Resize(ref arr, 10);
		Assert.NotNull(arr);
		Assert.True(arr!.Count >= 10);
	}

	// ---------- Oracle UDT 十六进制 ----------

	[Theory]
	[InlineData("0A0B0C")]
	[InlineData("0a0b0c")]
	public void OracleUdtHexBytes_decodes_hex(string hex)
	{
		Assert.True(OracleUdtHexBytes.TryDecodeHexDigitsOnly(hex, out var bytes));
		Assert.Equal(new byte[] { 0x0A, 0x0B, 0x0C }, bytes);
	}

		[Fact]
	public void OracleUdtHexBytes_filters_non_hex_characters()
	{
		// 实测行为：非十六进制字符被忽略，剩余数字按字节组装（不是「拒绝」）
		Assert.True(OracleUdtHexBytes.TryDecodeHexDigitsOnly("0A-0B", out var bytes));
		Assert.Equal(new byte[] { 0x0A, 0x0B }, bytes);

		Assert.True(OracleUdtHexBytes.TryDecodeHexDigitsOnly("", out var empty));
		Assert.Empty(empty);
	}

	[Fact]
	public void OracleUdtHexBytes_LooksLikeHexLiteral_is_permissive()
	{
		// 实测行为：只要字符集落在十六进制/常见字母范围内就认为「像」十六进制字面量，
		// 并非严格的十六进制校验（"hello"/"ABC" 都返回 true，"ZZ" 才为 false）。
		Assert.True(OracleUdtHexBytes.LooksLikeHexLiteral("0A0B"));
		Assert.True(OracleUdtHexBytes.LooksLikeHexLiteral("ABC"));
		Assert.False(OracleUdtHexBytes.LooksLikeHexLiteral("ZZ"));
	}

	// ---------- 列类型映射（协议字节 -> 列类型）----------

	// 注：ColumnType 是 internal 枚举，不能作为 public 测试方法的参数（CS0051），
	// 故此处用 internal 方法（xunit.v3 同样会发现 internal 测试）。
	[Theory]
	[InlineData((byte)0x01, ColumnType.Tiny)]
	[InlineData((byte)0x02, ColumnType.Short)]
	[InlineData((byte)0x03, ColumnType.Long)]
	[InlineData((byte)0x08, ColumnType.Longlong)]
	[InlineData((byte)0x04, ColumnType.Float)]
	[InlineData((byte)0x05, ColumnType.Double)]
	[InlineData((byte)0x00, ColumnType.Decimal)]
	[InlineData((byte)0x0D, ColumnType.Year)]
	[InlineData((byte)0x10, ColumnType.Bit)]
	internal void ColumnTypeMap_maps_protocol_type_bytes(byte protocolType, ColumnType expected)
	{
		Assert.Equal(expected, ColumnTypeMap.FromProtocolType(protocolType, false));
	}

	[Fact]
	public void ColumnTypeMap_tables_have_256_entries()
	{
		Assert.Equal(256, CreateMapOf("CreateMySqlMap").Length);
		Assert.Equal(256, CreateMapOf("CreateOracleMap").Length);
		Assert.Equal(256, CreateMapOf("CreateIdentityMap").Length);
	}
}
