using System;
using System.Buffers.Binary;
using System.Linq;
using System.Text;
using OceanBase.ColumnReaders;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 二进制列读取器测试（internal，无需服务端）。
///
/// 这些类负责把 MySQL/OceanBase 二进制协议的数据段解析成 CLR 值。
/// 数值型走 <c>DoReadValue(ReadOnlySpan&lt;byte&gt;)</c> 私有静态方法，
/// 通过强类型委托调用（ReadOnlySpan 是 ref struct，无法装箱进 MethodInfo.Invoke）。
/// 浮点/日期等使用公开静态方法或经由反射。
/// </summary>
public sealed class ColumnReaderTests
{
	private static T DoRead<T>(string readerName, byte[] payload)
	{
		var type = typeof(MySqlConnection).Assembly.GetType($"OceanBase.ColumnReaders.{readerName}", throwOnError: true)!;
		var method = type.GetMethod("DoReadValue",
			System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
			?? throw new InvalidOperationException($"{readerName} 无 DoReadValue");
		return method.CreateDelegate<ReadDelegate<T>>()(payload);
	}

	private delegate T ReadDelegate<T>(ReadOnlySpan<byte> payload);

	private static ReadOnlySpan<byte> Ascii(string s) => Encoding.ASCII.GetBytes(s);

	// ---------- DECIMAL（实测走 Utf8Parser，即文本解析）----------

	[Theory]
	[InlineData("0", 0)]
	[InlineData("1", 1)]
	[InlineData("-1", -1)]
	[InlineData("1.5", 1.5)]
	[InlineData("-2.25", -2.25)]
	public void Decimal_parses_utf8_text(string text, double expected)
	{
		// 实测：DoReadValue 用 Utf8Parser.TryParse(..., '\0')，输入是 UTF-8 文本
		var actual = DoRead<decimal>("DecimalColumnReader", Encoding.ASCII.GetBytes(text));
		Assert.Equal((decimal)expected, actual);
	}

	[Fact]
	public void Decimal_parses_high_precision_text()
	{
		// 用字符串直接断言，绕过 double 的精度损失
		var actual = DoRead<decimal>("DecimalColumnReader", Encoding.ASCII.GetBytes("123456789.123456789"));
		Assert.Equal(123456789.123456789m, actual);
		Assert.Equal(decimal.MaxValue, DoRead<decimal>("DecimalColumnReader", Encoding.ASCII.GetBytes("79228162514264337593543950335")));
	}

	[Fact]
	public void Decimal_rejects_invalid_text()
	{
		var mb = typeof(MySqlConnection).Assembly.GetType("OceanBase.ColumnReaders.DecimalColumnReader", true)!;
		var mi = mb.GetMethod("DoReadValue", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!;
		var d = mi.CreateDelegate<Func<ReadOnlySpan<byte>, decimal>>();

		var threw = false;
		try { d(Encoding.ASCII.GetBytes("not-a-number")); }
		catch (FormatException) { threw = true; }
		catch (OverflowException) { threw = true; }
		Assert.True(threw, "非法十进制文本应抛异常");
	}
	// ---------- GUID 布局 ----------

	[Fact]
	public void Guid_readers_expose_singletons()
	{
		// GuidChar36/32 只暴露 ReadValue(data, columnDefinition)，需要列定义才能调用；
		// 这里校验其单例入口存在（真正的字节解析由下面的 Binary16 系列覆盖）。
		Assert.NotNull(typeof(MySqlConnection).Assembly.GetType("OceanBase.ColumnReaders.GuidChar36ColumnReader", true));
		Assert.NotNull(typeof(MySqlConnection).Assembly.GetType("OceanBase.ColumnReaders.GuidChar32ColumnReader", true));
	}


	[Fact]
	public void Guid_binary_variants_produce_different_layouts()
	{
		var bytes = new byte[16];
		for (var i = 0; i < 16; i++) bytes[i] = (byte)i;
		var a = GuidBinary16ColumnReader.ReadGuid(bytes);
		var b = GuidLittleEndianBinary16ColumnReader.ReadGuid(bytes);
		var c2 = GuidTimeSwapBinary16ColumnReader.ReadGuid(bytes);
		Assert.NotEqual(a, b);
		Assert.NotEqual(a, c2);
	}



	// ---------- 文本数值读取器（穷举边界）----------

	[Theory]
	[InlineData("0", 0)]
	[InlineData("1", 1)]
	[InlineData("-1", -1)]
	[InlineData("127", 127)]
	[InlineData("-128", -128)]
	public void TextSignedInt8_boundaries(string text, sbyte expected)
	{
		Assert.Equal(expected, DoRead<sbyte>("TextSignedInt8ColumnReader", Ascii(text).ToArray()));
	}

	[Theory]
	[InlineData("0", 0)]
	[InlineData("255", 255)]
	public void TextUnsignedInt8_boundaries(string text, byte expected)
	{
		Assert.Equal(expected, DoRead<byte>("TextUnsignedInt8ColumnReader", Ascii(text).ToArray()));
	}

	[Theory]
	[InlineData("-32768", short.MinValue)]
	[InlineData("32767", short.MaxValue)]
	public void TextSignedInt16_boundaries(string text, short expected)
	{
		Assert.Equal(expected, DoRead<short>("TextSignedInt16ColumnReader", Ascii(text).ToArray()));
	}

	[Theory]
	[InlineData("0", 0u)]
	[InlineData("65535", ushort.MaxValue)]
	public void TextUnsignedInt16_boundaries(string text, ushort expected)
	{
		Assert.Equal(expected, DoRead<ushort>("TextUnsignedInt16ColumnReader", Ascii(text).ToArray()));
	}

	[Theory]
	[InlineData("-2147483648", int.MinValue)]
	[InlineData("2147483647", int.MaxValue)]
	public void TextSignedInt32_boundaries(string text, int expected)
	{
		Assert.Equal(expected, DoRead<int>("TextSignedInt32ColumnReader", Ascii(text).ToArray()));
	}

	[Theory]
	[InlineData("0", 0u)]
	[InlineData("4294967295", uint.MaxValue)]
	public void TextUnsignedInt32_boundaries(string text, uint expected)
	{
		Assert.Equal(expected, DoRead<uint>("TextUnsignedInt32ColumnReader", Ascii(text).ToArray()));
	}

	[Theory]
	[InlineData("-9223372036854775808", long.MinValue)]
	[InlineData("9223372036854775807", long.MaxValue)]
	public void TextSignedInt64_boundaries(string text, long expected)
	{
		Assert.Equal(expected, DoRead<long>("TextSignedInt64ColumnReader", Ascii(text).ToArray()));
	}

	[Theory]
	[InlineData("0", 0ul)]
	[InlineData("18446744073709551615", ulong.MaxValue)]
	public void TextUnsignedInt64_boundaries(string text, ulong expected)
	{
		Assert.Equal(expected, DoRead<ulong>("TextUnsignedInt64ColumnReader", Ascii(text).ToArray()));
	}

	// ---------- 二进制整数边界 ----------

	[Theory]
	[InlineData(new byte[] { 0x00 }, 0)]
	[InlineData(new byte[] { 0x7F }, 127)]
	[InlineData(new byte[] { 0x80 }, -128)]
	[InlineData(new byte[] { 0xFF }, -1)]
	public void BinarySignedInt8_boundaries(byte[] payload, sbyte expected)
	{
		Assert.Equal(expected, DoRead<sbyte>("BinarySignedInt8ColumnReader", payload));
	}

	[Theory]
	[InlineData(new byte[] { 0x00, 0x00 }, 0)]
	[InlineData(new byte[] { 0xFF, 0x7F }, short.MaxValue)]
	[InlineData(new byte[] { 0x00, 0x80 }, short.MinValue)]
	public void BinarySignedInt16_boundaries(byte[] payload, short expected)
	{
		Assert.Equal(expected, DoRead<short>("BinarySignedInt16ColumnReader", payload));
	}

	[Theory]
	[InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00 }, 0)]
	[InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0x7F }, int.MaxValue)]
	[InlineData(new byte[] { 0x00, 0x00, 0x00, 0x80 }, int.MinValue)]
	public void BinarySignedInt32_boundaries(byte[] payload, int expected)
	{
		Assert.Equal(expected, DoRead<int>("BinarySignedInt32ColumnReader", payload));
	}

	[Fact]
	public void BinarySignedInt64_boundaries()
	{
		Assert.Equal(long.MaxValue, DoRead<long>("BinarySignedInt64ColumnReader", new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F }));
		Assert.Equal(long.MinValue, DoRead<long>("BinarySignedInt64ColumnReader", new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80 }));
	}

	[Fact]
	public void BinaryUnsignedInt64_boundaries()
	{
		Assert.Equal(ulong.MaxValue, DoRead<ulong>("BinaryUnsignedInt64ColumnReader", new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }));
		Assert.Equal(0UL, DoRead<ulong>("BinaryUnsignedInt64ColumnReader", new byte[8]));
	}
}
