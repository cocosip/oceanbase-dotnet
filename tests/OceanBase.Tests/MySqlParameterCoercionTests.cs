using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using OceanBase.Core;
using OceanBase.Protocol.Serialization;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// MySqlParameter / MySqlParameterCollection / MySqlAttribute 的离线单元测试：
/// DbType 与 MySqlDbType 双向映射、Value 赋值时的类型推导、参数名归一化、
/// Clone、SQL 字面量与二进制协议编码、集合增删改查，全部不依赖服务端。
/// </summary>
public sealed class MySqlParameterCoercionTests
{
	// ---------- 辅助方法 ----------

	/// <summary>把值渲染成 SQL 字面量（内部直接调用 AppendSqlString），固定当前区域后断言格式。</summary>
	private static string Sql(object? value, StatementPreparerOptions options = StatementPreparerOptions.None)
	{
		// AppendSqlString 内部全部走 InvariantCulture，仍按要求先固定 CurrentCulture
		var original = CultureInfo.CurrentCulture;
		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
		try
		{
			return Encoding.UTF8.GetString(SqlBytes(value, options));
		}
		finally
		{
			CultureInfo.CurrentCulture = original;
		}
	}

	private static byte[] SqlBytes(object? value, StatementPreparerOptions options = StatementPreparerOptions.None)
	{
		return AppendToBytes(new MySqlParameter("@p", value), options);
	}

	private static byte[] AppendToBytes(MySqlParameter parameter, StatementPreparerOptions options)
	{
		var writer = new ByteBufferWriter(64);
		parameter.AppendSqlString(writer, options);
		return writer.ArraySegment.ToArray();
	}

	/// <summary>把值编码成二进制协议字节（AppendBinary）。</summary>
	private static byte[] Bin(object? value)
	{
		var writer = new ByteBufferWriter(64);
		new MySqlParameter("@p", value).AppendBinary(writer, StatementPreparerOptions.None);
		return writer.ArraySegment.ToArray();
	}

	private sealed class StubStream : Stream
	{
		public override bool CanRead => true;
		public override bool CanSeek => true;
		public override bool CanWrite => true;
		public override long Length => 0;
		public override long Position { get; set; }
		public override void Flush() { }
		public override int Read(byte[] buffer, int offset, int count) => 0;
		public override long Seek(long offset, SeekOrigin origin) => 0;
		public override void SetLength(long value) { }
		public override void Write(byte[] buffer, int offset, int count) { }
	}

	// ---------- DbType ↔ MySqlDbType 双向映射 ----------

	[Theory]
	[InlineData(DbType.Boolean, MySqlDbType.Bool)]
	[InlineData(DbType.SByte, MySqlDbType.Byte)]
	[InlineData(DbType.Byte, MySqlDbType.UByte)]
	[InlineData(DbType.Int16, MySqlDbType.Int16)]
	[InlineData(DbType.UInt16, MySqlDbType.UInt16)]
	[InlineData(DbType.Int32, MySqlDbType.Int32)]
	[InlineData(DbType.UInt32, MySqlDbType.UInt32)]
	[InlineData(DbType.Int64, MySqlDbType.Int64)]
	[InlineData(DbType.UInt64, MySqlDbType.UInt64)]
	[InlineData(DbType.Double, MySqlDbType.Double)]
	[InlineData(DbType.Single, MySqlDbType.Float)]
	[InlineData(DbType.String, MySqlDbType.VarChar)]
	[InlineData(DbType.AnsiString, MySqlDbType.VarChar)]
	[InlineData(DbType.Xml, MySqlDbType.VarChar)]
	[InlineData(DbType.StringFixedLength, MySqlDbType.String)]
	[InlineData(DbType.AnsiStringFixedLength, MySqlDbType.String)]
	[InlineData(DbType.Binary, MySqlDbType.Blob)]
	[InlineData(DbType.Date, MySqlDbType.Date)]
	[InlineData(DbType.DateTime, MySqlDbType.DateTime)]
	[InlineData(DbType.DateTime2, MySqlDbType.DateTime)]
	[InlineData(DbType.Time, MySqlDbType.Time)]
	[InlineData(DbType.Guid, MySqlDbType.Guid)]
	[InlineData(DbType.Decimal, MySqlDbType.NewDecimal)]
	[InlineData(DbType.Currency, MySqlDbType.NewDecimal)]
	[InlineData(DbType.VarNumeric, MySqlDbType.NewDecimal)]
	public void DbType_maps_to_first_registered_MySqlDbType(DbType dbType, MySqlDbType expected)
	{
		// TypeMapper 按注册顺序返回第一个包含该 DbType 的映射：
		// Decimal → NewDecimal（先于 Decimal 注册）、Binary → Blob（先于 Binary/VarBinary 注册）
		var p = new MySqlParameter { DbType = dbType };
		Assert.Equal(expected, p.MySqlDbType);
		Assert.True(p.HasSetDbType);
	}

	[Fact]
	public void DbType_Object_maps_to_Vector()
	{
		// 按注册顺序 VECTOR（DbType.Object）先于 NULL 注册，因此 DbType.Object 推导为 Vector
		var p = new MySqlParameter { DbType = DbType.Object };
		Assert.Equal(MySqlDbType.Vector, p.MySqlDbType);
	}

	[Theory]
	[InlineData(MySqlDbType.Bool, DbType.Boolean)]
	[InlineData(MySqlDbType.Byte, DbType.SByte)]
	[InlineData(MySqlDbType.UByte, DbType.Byte)]
	[InlineData(MySqlDbType.Int16, DbType.Int16)]
	[InlineData(MySqlDbType.UInt16, DbType.UInt16)]
	[InlineData(MySqlDbType.Int24, DbType.Int32)]
	[InlineData(MySqlDbType.UInt24, DbType.UInt32)]
	[InlineData(MySqlDbType.Int32, DbType.Int32)]
	[InlineData(MySqlDbType.UInt32, DbType.UInt32)]
	[InlineData(MySqlDbType.Int64, DbType.Int64)]
	[InlineData(MySqlDbType.UInt64, DbType.UInt64)]
	[InlineData(MySqlDbType.Bit, DbType.UInt64)]
	[InlineData(MySqlDbType.Year, DbType.Int32)]
	[InlineData(MySqlDbType.NewDecimal, DbType.Decimal)]
	[InlineData(MySqlDbType.Decimal, DbType.Decimal)]
	[InlineData(MySqlDbType.Double, DbType.Double)]
	[InlineData(MySqlDbType.Float, DbType.Single)]
	[InlineData(MySqlDbType.VarChar, DbType.String)]
	[InlineData(MySqlDbType.VarString, DbType.String)]
	[InlineData(MySqlDbType.String, DbType.StringFixedLength)]
	[InlineData(MySqlDbType.TinyText, DbType.String)]
	[InlineData(MySqlDbType.Text, DbType.String)]
	[InlineData(MySqlDbType.MediumText, DbType.String)]
	[InlineData(MySqlDbType.LongText, DbType.String)]
	[InlineData(MySqlDbType.Enum, DbType.String)]
	[InlineData(MySqlDbType.Set, DbType.String)]
	[InlineData(MySqlDbType.JSON, DbType.String)]
	[InlineData(MySqlDbType.Blob, DbType.Binary)]
	[InlineData(MySqlDbType.TinyBlob, DbType.Binary)]
	[InlineData(MySqlDbType.Binary, DbType.Binary)]
	[InlineData(MySqlDbType.VarBinary, DbType.Binary)]
	[InlineData(MySqlDbType.MediumBlob, DbType.Binary)]
	[InlineData(MySqlDbType.LongBlob, DbType.Binary)]
	[InlineData(MySqlDbType.Geometry, DbType.Binary)]
	[InlineData(MySqlDbType.DateTime, DbType.DateTime)]
	[InlineData(MySqlDbType.Timestamp, DbType.DateTime)]
	[InlineData(MySqlDbType.Date, DbType.Date)]
	[InlineData(MySqlDbType.Newdate, DbType.Date)]
	[InlineData(MySqlDbType.Time, DbType.Time)]
	[InlineData(MySqlDbType.Guid, DbType.Guid)]
	[InlineData(MySqlDbType.Null, DbType.Object)]
	[InlineData(MySqlDbType.Vector, DbType.Object)]
	public void MySqlDbType_maps_to_first_DbType_of_mapping(MySqlDbType mySqlDbType, DbType expected)
	{
		var p = new MySqlParameter { MySqlDbType = mySqlDbType };
		Assert.Equal(expected, p.DbType);
		Assert.Equal(mySqlDbType, p.MySqlDbType);
		Assert.True(p.HasSetDbType);
	}

	[Fact]
	public void Setting_either_type_property_updates_the_other()
	{
		var p = new MySqlParameter();
		p.DbType = DbType.Int64;
		Assert.Equal(MySqlDbType.Int64, p.MySqlDbType);
		p.MySqlDbType = MySqlDbType.Text;
		Assert.Equal(DbType.String, p.DbType);
	}

	// ---------- Value 赋值时的类型推导 ----------

	[Theory]
	[InlineData(42, DbType.Int32, MySqlDbType.Int32)]
	[InlineData(-1, DbType.Int32, MySqlDbType.Int32)]
	[InlineData(100L, DbType.Int64, MySqlDbType.Int64)]
	[InlineData(100UL, DbType.UInt64, MySqlDbType.UInt64)]
	[InlineData(2.5, DbType.Double, MySqlDbType.Double)]
	[InlineData(2.5f, DbType.Single, MySqlDbType.Float)]
	[InlineData(true, DbType.Boolean, MySqlDbType.Bool)]
	[InlineData("s", DbType.String, MySqlDbType.VarChar)]
	public void Value_inference_sets_both_types(object value, DbType expectedDb, MySqlDbType expectedMySql)
	{
		// 未显式设置 DbType 时，从 CLR 类型推导；推导不置 HasSetDbType
		var p = new MySqlParameter { Value = value };
		Assert.Equal(expectedDb, p.DbType);
		Assert.Equal(expectedMySql, p.MySqlDbType);
		Assert.False(p.HasSetDbType);
	}

	[Fact]
	public void Value_inference_covers_all_simple_clr_types()
	{
		Assert.Equal(DbType.Binary, new MySqlParameter { Value = new byte[] { 1 } }.DbType);
		Assert.Equal(MySqlDbType.Blob, new MySqlParameter { Value = new byte[] { 1 } }.MySqlDbType);
		Assert.Equal(DbType.Byte, new MySqlParameter { Value = (byte)1 }.DbType);
		Assert.Equal(MySqlDbType.UByte, new MySqlParameter { Value = (byte)1 }.MySqlDbType);
		Assert.Equal(DbType.SByte, new MySqlParameter { Value = (sbyte)1 }.DbType);
		Assert.Equal(MySqlDbType.Byte, new MySqlParameter { Value = (sbyte)1 }.MySqlDbType);
		Assert.Equal(DbType.Int16, new MySqlParameter { Value = (short)1 }.DbType);
		Assert.Equal(DbType.UInt16, new MySqlParameter { Value = (ushort)1 }.DbType);
		Assert.Equal(DbType.UInt32, new MySqlParameter { Value = (uint)1 }.DbType);
		Assert.Equal(DbType.Guid, new MySqlParameter { Value = Guid.NewGuid() }.DbType);
		Assert.Equal(DbType.Date, new MySqlParameter { Value = new DateOnly(2024, 1, 2) }.DbType);
		Assert.Equal(MySqlDbType.Date, new MySqlParameter { Value = new DateOnly(2024, 1, 2) }.MySqlDbType);
		Assert.Equal(DbType.DateTime, new MySqlParameter { Value = new DateTime(2024, 1, 2) }.DbType);
		Assert.Equal(MySqlDbType.DateTime, new MySqlParameter { Value = new DateTime(2024, 1, 2) }.MySqlDbType);
		Assert.Equal(DbType.DateTimeOffset, new MySqlParameter { Value = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero) }.DbType);
		// DateTimeOffset 命中的是 DateTime 列（DateTime 的映射表包含 DateTimeOffset 且先于 Timestamp 注册）
		Assert.Equal(MySqlDbType.DateTime, new MySqlParameter { Value = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero) }.MySqlDbType);
		Assert.Equal(DbType.Time, new MySqlParameter { Value = new TimeOnly(1, 2, 3) }.DbType);
		Assert.Equal(DbType.Time, new MySqlParameter { Value = TimeSpan.FromSeconds(1) }.DbType);
		Assert.Equal(MySqlDbType.Time, new MySqlParameter { Value = TimeSpan.FromSeconds(1) }.MySqlDbType);
		Assert.Equal(DbType.Decimal, new MySqlParameter { Value = 1.5m }.DbType);
		Assert.Equal(MySqlDbType.NewDecimal, new MySqlParameter { Value = 1.5m }.MySqlDbType);
	}

	[Fact]
	public void Value_inference_uses_enum_underlying_type()
	{
		// TypeMapper.GetDbTypeMapping 对枚举取其基础类型
		Assert.Equal(DbType.Int32, new MySqlParameter { Value = DayOfWeek.Friday }.DbType);
		Assert.Equal(MySqlDbType.Int32, new MySqlParameter { Value = DayOfWeek.Friday }.MySqlDbType);
	}

	[Fact]
	public void Value_null_and_DBNull_keep_default_types()
	{
		// null 直接跳过推导；DBNull 无 CLR 映射（dbTypeMapping == null 分支），两者都保持默认 String/VarChar
		var p = new MySqlParameter { Value = null };
		Assert.Equal(DbType.String, p.DbType);
		Assert.Equal(MySqlDbType.VarChar, p.MySqlDbType);
		Assert.Null(p.Value);

		var p2 = new MySqlParameter { Value = DBNull.Value };
		Assert.Equal(DbType.String, p2.DbType);
		Assert.Equal(MySqlDbType.VarChar, p2.MySqlDbType);
		Assert.Equal(DBNull.Value, p2.Value);
	}

	[Fact]
	public void Explicit_DbType_prevents_value_inference()
	{
		var p = new MySqlParameter { DbType = DbType.Decimal };
		p.Value = 123;
		Assert.Equal(DbType.Decimal, p.DbType);
		Assert.Equal(MySqlDbType.NewDecimal, p.MySqlDbType);
	}

	[Fact]
	public void ResetDbType_restores_defaults_and_reenables_inference()
	{
		var p = new MySqlParameter { DbType = DbType.Int32, Value = 1 };
		p.ResetDbType();
		Assert.Equal(DbType.String, p.DbType);
		Assert.Equal(MySqlDbType.VarChar, p.MySqlDbType);
		Assert.False(p.HasSetDbType);

		// 重置后再次赋值会重新推导
		p.Value = "abc";
		Assert.Equal(DbType.String, p.DbType);
		Assert.Equal(MySqlDbType.VarChar, p.MySqlDbType);
		p.Value = 7;
		Assert.Equal(DbType.Int32, p.DbType);
	}

	// ---------- Direction 检查 ----------

	[Fact]
	public void Direction_defaults_to_input()
	{
		var p = new MySqlParameter();
		Assert.Equal(ParameterDirection.Input, p.Direction);
		Assert.False(p.HasSetDirection);
	}

	[Theory]
	[InlineData(ParameterDirection.Input)]
	[InlineData(ParameterDirection.Output)]
	[InlineData(ParameterDirection.InputOutput)]
	[InlineData(ParameterDirection.ReturnValue)]
	public void Direction_accepts_supported_values(ParameterDirection direction)
	{
		var p = new MySqlParameter { Direction = direction };
		Assert.Equal(direction, p.Direction);
		Assert.True(p.HasSetDirection);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(4)]
	[InlineData(5)]
	[InlineData(7)]
	[InlineData(100)]
	public void Direction_rejects_unsupported_values(int raw)
	{
		// 只接受 Input/Output/InputOutput/ReturnValue（1,2,3,6），其余抛 ArgumentOutOfRange
		var p = new MySqlParameter();
		Assert.Throws<ArgumentOutOfRangeException>(() => p.Direction = (ParameterDirection)raw);
	}

	// ---------- 参数名归一化 ----------

	[Fact]
	public void ParameterName_null_becomes_empty()
	{
		var p = new MySqlParameter(null, 1);
		Assert.Equal("", p.ParameterName);
		Assert.Equal("", p.NormalizedParameterName);

		p.ParameterName = null;
		Assert.Equal("", p.ParameterName);
		Assert.Equal("", p.NormalizedParameterName);
	}

	[Fact]
	public void SourceColumn_null_becomes_empty()
	{
		var p = new MySqlParameter { SourceColumn = null };
		Assert.Equal("", p.SourceColumn);
	}

	[Fact]
	public void ParameterName_preserves_raw_name_but_normalizes()
	{
		var p = new MySqlParameter(" @p ", 1);
		// 原名保留（含空白与前缀），归一化名去掉空白和 @/? 前缀
		Assert.Equal(" @p ", p.ParameterName);
		Assert.Equal("p", p.NormalizedParameterName);
	}

	[Theory]
	[InlineData("name", "name")]
	[InlineData("@name", "name")]
	[InlineData("?name", "name")]
	[InlineData(" @name\t", "name")]
	[InlineData("@", "")]
	[InlineData("?", "")]
	[InlineData("@a", "a")]
	[InlineData("@@x", "@x")]                 // 第二个字符不是引号，只剥掉第一个 @
	[InlineData("@`abc`", "abc")]
	[InlineData("@`a``b`", "a`b")]            // `` 是反引号的转义
	[InlineData("@'o''brien'", "o'brien")]
	[InlineData("@\"q\"\"q\"", "q\"q")]
	[InlineData("@`unterminated", "`unterminated")] // 结尾不是 `，退化为只剥前缀
	[InlineData("name@", "name@")]
	[InlineData("", "")]
	[InlineData("x", "x")]
	public void NormalizeParameterName_strips_prefix_and_quotes(string raw, string expected)
	{
		Assert.Equal(expected, MySqlParameter.NormalizeParameterName(raw));
	}

	// ---------- 构造函数 / Size / Precision / Scale ----------

	[Fact]
	public void MySqlDbType_ctor_chain_sets_defaults()
	{
		var p = new MySqlParameter("@p", MySqlDbType.Int32);
		Assert.Equal("@p", p.ParameterName);
		Assert.Equal("p", p.NormalizedParameterName);
		Assert.Equal(MySqlDbType.Int32, p.MySqlDbType);
		Assert.Equal(DbType.Int32, p.DbType);
		Assert.Equal(0, p.Size);
		Assert.Equal("", p.SourceColumn);
		Assert.Equal(DataRowVersion.Current, p.SourceVersion);
		Assert.Null(p.Value);

		var p2 = new MySqlParameter("@p", MySqlDbType.VarBinary, 55);
		Assert.Equal(55, p2.Size);
		Assert.Equal(DbType.Binary, p2.DbType);

		var p3 = new MySqlParameter("@p", MySqlDbType.VarChar, 10, "src");
		Assert.Equal("src", p3.SourceColumn);
	}

	[Fact]
	public void Full_constructor_sets_every_property()
	{
		var p = new MySqlParameter("@p", MySqlDbType.Decimal, 20, ParameterDirection.InputOutput, isNullable: true, precision: (byte)10, scale: (byte)3, sourceColumn: "col", sourceVersion: DataRowVersion.Original, value: 1.5m);
		Assert.Equal("@p", p.ParameterName);
		Assert.Equal(MySqlDbType.Decimal, p.MySqlDbType);
		Assert.Equal(20, p.Size);
		Assert.Equal(ParameterDirection.InputOutput, p.Direction);
		Assert.True(p.IsNullable);
		Assert.Equal((byte)10, p.Precision);
		Assert.Equal((byte)3, p.Scale);
		Assert.Equal("col", p.SourceColumn);
		Assert.Equal(DataRowVersion.Original, p.SourceVersion);
		Assert.Equal(1.5m, p.Value);
		Assert.True(p.HasSetDbType);
		Assert.True(p.HasSetDirection);
	}

	[Fact]
	public void Size_Precision_Scale_are_settable()
	{
		var p = new MySqlParameter();
		p.Size = 100;
		p.Precision = 18;
		p.Scale = 6;
		Assert.Equal(100, p.Size);
		Assert.Equal((byte)18, p.Precision);
		Assert.Equal((byte)6, p.Scale);
	}

	// ---------- Clone / WithParameterName ----------

	[Fact]
	public void Clone_copies_all_state()
	{
		var payload = new byte[] { 1, 2, 3 };
		var p = new MySqlParameter("@p", MySqlDbType.Decimal, 20, ParameterDirection.InputOutput, isNullable: true, precision: (byte)10, scale: (byte)3, sourceColumn: "col", sourceVersion: DataRowVersion.Original, value: payload);
		p.SourceColumnNullMapping = true;

		var c = p.Clone();
		Assert.NotSame(p, c);
		Assert.Equal(p.ParameterName, c.ParameterName);
		Assert.Equal(p.NormalizedParameterName, c.NormalizedParameterName);
		Assert.Equal(p.DbType, c.DbType);
		Assert.Equal(p.MySqlDbType, c.MySqlDbType);
		Assert.Equal(p.Direction, c.Direction);
		Assert.Equal(p.Size, c.Size);
		Assert.Equal(p.IsNullable, c.IsNullable);
		Assert.Equal(p.Precision, c.Precision);
		Assert.Equal(p.Scale, c.Scale);
		Assert.Equal(p.SourceColumn, c.SourceColumn);
		Assert.Equal(p.SourceColumnNullMapping, c.SourceColumnNullMapping);
		Assert.Equal(p.SourceVersion, c.SourceVersion);
		Assert.Same(payload, c.Value);        // 浅拷贝：Value 引用同一对象
		Assert.True(c.HasSetDbType);          // 内部状态也被复制
		Assert.True(c.HasSetDirection);
	}

	[Fact]
	public void ICloneable_Clone_returns_MySqlParameter()
	{
		ICloneable p = new MySqlParameter("@p", 1);
		var c = Assert.IsType<MySqlParameter>(p.Clone());
		Assert.Equal("@p", c.ParameterName);
	}

	[Fact]
	public void Clone_is_detached_from_collection()
	{
		var coll = new MySqlParameterCollection();
		var p = coll.AddWithValue("@p", 1);
		Assert.Same(coll, p.ParameterCollection);
		var c = p.Clone();
		Assert.Null(c.ParameterCollection);
	}

	[Fact]
	public void WithParameterName_renames_the_copy()
	{
		var p = new MySqlParameter("@old", MySqlDbType.Int32, 11);
		var renamed = p.WithParameterName("@new");
		Assert.Equal("@new", renamed.ParameterName);
		Assert.Equal("new", renamed.NormalizedParameterName);
		Assert.Equal(p.DbType, renamed.DbType);
		Assert.Equal(p.Size, renamed.Size);
		Assert.Null(renamed.Value);

		Assert.Throws<ArgumentNullException>(() => p.WithParameterName(null!));
	}

	// ---------- AppendSqlString：值 → SQL 字面量 ----------

	[Theory]
	[InlineData(true, "true")]
	[InlineData(false, "false")]
	public void Bool_renders_as_true_false(bool value, string expected)
	{
		Assert.Equal(expected, Sql(value));
	}

	[Theory]
	[InlineData(0, "0")]
	[InlineData(-1, "-1")]
	[InlineData(1000000, "1000000")]
	public void Int_renders_invariantly(int value, string expected)
	{
		Assert.Equal(expected, Sql(value));
	}

	[Fact]
	public void Integer_extremes_render_invariantly()
	{
		Assert.Equal("255", Sql((byte)255));
		Assert.Equal("-128", Sql((sbyte)-128));
		Assert.Equal("-32768", Sql((short)-32768));
		Assert.Equal("65535", Sql((ushort)65535));
		Assert.Equal("-2147483648", Sql(int.MinValue));
		Assert.Equal("4294967295", Sql(uint.MaxValue));
		Assert.Equal("-9223372036854775808", Sql(long.MinValue));
		Assert.Equal("18446744073709551615", Sql(ulong.MaxValue));
	}

	[Fact]
	public void Decimal_renders_with_scale_preserved()
	{
		Assert.Equal("12.345", Sql(12.345m));
		Assert.Equal("0", Sql(0m));
		Assert.Equal("-2.25", Sql(-2.25m));
		// decimal 保留标度：1.50m 格式化后仍是两位小数
		Assert.Equal("1.50", Sql(1.50m));
	}

	[Fact]
	public void Float_and_Double_render_invariantly()
	{
		Assert.Equal("1.5", Sql(1.5f));
		Assert.Equal("-0.5", Sql(-0.5f));
		Assert.Equal("2.25", Sql(2.25d));
		Assert.Equal("1E+300", Sql(1e300d));
	}

	[Fact]
	public void Char_renders_quoted_and_escaped()
	{
		Assert.Equal("'A'", Sql('A'));
		Assert.Equal("''''", Sql('\''));    // 单引号加倍
		Assert.Equal("'\\\\'", Sql('\\'));  // 反斜杠翻倍
		Assert.Equal("'\\0'", Sql('\0'));   // NUL 写成 \0
	}

	[Fact]
	public void Char_escaping_changes_with_NoBackslashEscapes()
	{
		Assert.Equal("'\\'", Sql('\\', StatementPreparerOptions.NoBackslashEscapes));
		Assert.Equal("'\0'", Sql('\0', StatementPreparerOptions.NoBackslashEscapes));
	}

	[Fact]
	public void ReadOnlyMemory_of_char_renders_as_string()
	{
		Assert.Equal("'ab'", Sql("ab".AsMemory()));
	}

	[Fact]
	public void ByteArray_renders_as_binary_literal_with_escapes()
	{
		var prefix = Encoding.UTF8.GetBytes("_binary'");
		// 默认：' 加倍、NUL → \0、\ → \\
		var expected = prefix.Concat(new byte[] { 0x27, 0x27, 0x5C, 0x30, 0x5C, 0x5C, 0x27 }).ToArray();
		Assert.Equal(expected, SqlBytes(new byte[] { 0x27, 0x00, 0x5C }));

		// NoBackslashEscapes：NUL 与 \ 原样输出，' 仍加倍
		var expectedNoBackslash = prefix.Concat(new byte[] { 0x27, 0x27, 0x00, 0x5C, 0x27 }).ToArray();
		Assert.Equal(expectedNoBackslash, SqlBytes(new byte[] { 0x27, 0x00, 0x5C }, StatementPreparerOptions.NoBackslashEscapes));
	}

	[Fact]
	public void MemoryStream_renders_as_binary_literal()
	{
		// MemoryStream(byte[]) 的 TryGetBuffer 不可见，走 ToArray 分支
		Assert.Equal("_binary'A'", Sql(new MemoryStream(new byte[] { (byte)'A' })));
	}

	[Fact]
	public void FloatArray_renders_as_little_endian_bytes()
	{
		// 0.8f 的 LE 字节（CD CC 4C 3F）不含 0x00/0x27/0x5C，不触发 _binary'...' 转义
		var expected = Encoding.UTF8.GetBytes("_binary'")
			.Concat(BitConverter.GetBytes(0.8f))
			.Concat(new byte[] { 0x27 })
			.ToArray();
		Assert.Equal(expected, SqlBytes(new float[] { 0.8f }));
	}

	[Fact]
	public void Guid_renders_as_char36_by_default()
	{
		var guid = new Guid("11223344-5566-7788-99aa-bbccddeeff01");
		Assert.Equal("'11223344-5566-7788-99aa-bbccddeeff01'", Sql(guid));
		Assert.Equal("'112233445566778899aabbccddeeff01'", Sql(guid, StatementPreparerOptions.GuidFormatChar32));
	}

	[Fact]
	public void Guid_binary16_renders_escaped_raw_bytes()
	{
		// 所选 Guid 的 16 字节不含 0x00/0x27/0x5C，不触发 _binary'...' 内的转义
		var guid = new Guid("11223344-5566-7788-99aa-bbccddeeff01");
		var bigEndian = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x01 };
		var expectedBe = Encoding.UTF8.GetBytes("_binary'").Concat(bigEndian).Append((byte)0x27).ToArray();
		Assert.Equal(expectedBe, SqlBytes(guid, StatementPreparerOptions.GuidFormatBinary16));

		// LittleEndianBinary16 按 mixed-endian（Guid.ToByteArray 顺序）写出
		var mixedEndian = new byte[] { 0x44, 0x33, 0x22, 0x11, 0x66, 0x55, 0x88, 0x77, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x01 };
		var expectedLe = Encoding.UTF8.GetBytes("_binary'").Concat(mixedEndian).Append((byte)0x27).ToArray();
		Assert.Equal(expectedLe, SqlBytes(guid, StatementPreparerOptions.GuidFormatLittleEndianBinary16));
	}

	[Fact]
	public void DateTime_renders_as_timestamp_literal()
	{
		var dt = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Unspecified).AddTicks(1234560);
		Assert.Equal("timestamp('2024-01-02 03:04:05.123456')", Sql(dt));
	}

	[Fact]
	public void DateTime_kind_is_validated_against_options()
	{
		var local = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local);
		var ex = Assert.Throws<MySqlException>(() => Sql(local, StatementPreparerOptions.DateTimeUtc));
		Assert.Contains("must not be Local", ex.Message);

		var utc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
		Assert.Throws<MySqlException>(() => Sql(utc, StatementPreparerOptions.DateTimeLocal));
	}

	[Fact]
	public void DateOnly_DateTimeOffset_render_as_timestamp()
	{
		Assert.Equal("timestamp('2024-01-02')", Sql(new DateOnly(2024, 1, 2)));
		// DateTimeOffset 取其 UtcDateTime（+8h 偏移换算成 UTC）
		Assert.Equal("timestamp('2024-01-01 19:04:05.000000')", Sql(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(8))));
	}

	[Fact]
	public void TimeOnly_and_TimeSpan_render_as_time_literal()
	{
		Assert.Equal("time '03:04:05.123000'", Sql(new TimeOnly(3, 4, 5, 123)));
		// 天+小时合并为总小时数
		Assert.Equal("time '26:03:04.000000'", Sql(new TimeSpan(1, 2, 3, 4)));
		// 负值先输出 '-' 再输出绝对值
		Assert.Equal("time '-0:00:05.000000'", Sql(new TimeSpan(0, 0, 0, -5)));
	}

	[Fact]
	public void MySqlDateTime_renders_valid_and_zero_date()
	{
		Assert.Equal("timestamp('2024-01-02 03:04:05.123456')", Sql(new MySqlDateTime(2024, 1, 2, 3, 4, 5, 123456)));
		Assert.Equal("timestamp('0000-00-00')", Sql(new MySqlDateTime(0, 0, 0, 0, 0, 0, 0)));
	}

	[Fact]
	public void MySqlDecimal_and_BigInteger_render_as_digits()
	{
		Assert.Equal("1.5", Sql(new MySqlDecimal("1.5")));
		Assert.Equal("9223372036854775807", Sql((BigInteger)long.MaxValue));
	}

	[Fact]
	public void StringBuilder_renders_as_escaped_string()
	{
		Assert.Equal("'a''b'", Sql(new StringBuilder("a'b")));
	}

	[Fact]
	public void Enum_renders_as_number_by_default()
	{
		// 推导类型（未显式设置 DbType）时枚举按数值输出
		Assert.Equal("3", Sql(DayOfWeek.Wednesday));
	}

	[Fact]
	public void Enum_renders_as_name_for_string_types()
	{
		// 显式设置为 VarChar/String（253/254）且 HasSetDbType 时按名称输出
		var p = new MySqlParameter { ParameterName = "@p", MySqlDbType = MySqlDbType.VarChar, Value = DayOfWeek.Wednesday };
		Assert.Equal("'Wednesday'", Encoding.UTF8.GetString(AppendToBytes(p, StatementPreparerOptions.None)));
	}

	[Fact]
	public void Unsupported_value_type_throws_NotSupported()
	{
		var ex = Assert.Throws<NotSupportedException>(() => Sql(new object()));
		Assert.Contains("not supported", ex.Message);
	}

	[Fact]
	public void Stream_value_requires_Prepare()
	{
		var ex = Assert.Throws<NotSupportedException>(() => Sql(new StubStream()));
		Assert.Contains("Prepare", ex.Message);
	}

	// ---------- AppendBinary：值 → 二进制协议字节 ----------

	[Fact]
	public void AppendBinary_skips_null_and_DBNull()
	{
		Assert.Empty(Bin(null));
		Assert.Empty(Bin(DBNull.Value));
	}

	[Fact]
	public void AppendBinary_writes_numeric_primitives()
	{
		Assert.Equal(new byte[] { 42, 0, 0, 0 }, Bin(42));
		Assert.Equal(new byte[] { 5 }, Bin((byte)5));
		Assert.Equal(new byte[] { 1 }, Bin(true));
		Assert.Equal(new byte[] { 0 }, Bin(false));
		Assert.Equal(new byte[] { 2, 1 }, Bin((short)0x0102));
		Assert.Equal(new byte[] { 4, 3, 2, 1 }, Bin((uint)0x01020304));
		Assert.Equal(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }, Bin(1L));
	}

	[Fact]
	public void AppendBinary_writes_length_prefixed_strings()
	{
		Assert.Equal(new byte[] { 2, 0x61, 0x62 }, Bin("ab"));
		Assert.Equal(new byte[] { 1, 0x41 }, Bin('A'));
	}

	[Fact]
	public void AppendBinary_writes_byte_array_with_length()
	{
		Assert.Equal(new byte[] { 2, 1, 2 }, Bin(new byte[] { 1, 2 }));
		Assert.Equal(new byte[] { 1, 9 }, Bin(new MemoryStream(new byte[] { 9 })));
	}

	[Fact]
	public void AppendBinary_writes_float_and_double_little_endian()
	{
		Assert.Equal(BitConverter.GetBytes(1f), Bin(1f));
		Assert.Equal(BitConverter.GetBytes(1d), Bin(1d));
	}

	[Fact]
	public void AppendBinary_writes_decimal_as_length_prefixed_ascii()
	{
		Assert.Equal(new byte[] { 5 }.Concat(Encoding.ASCII.GetBytes("12.34")).ToArray(), Bin(12.34m));
	}

	[Fact]
	public void AppendBinary_writes_DateTime_with_variable_length()
	{
		// 有微秒 → 长度 11；仅有时间 → 7；零点整 → 4
		var withMicro = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Unspecified).AddTicks(1234560);
		Assert.Equal(new byte[] { 11, 0xE8, 0x07, 1, 2, 3, 4, 5, 0x40, 0xE2, 0x01, 0x00 }, Bin(withMicro));
		Assert.Equal(new byte[] { 7, 0xE8, 0x07, 1, 2, 3, 4, 5 }, Bin(new DateTime(2024, 1, 2, 3, 4, 5)));
		Assert.Equal(new byte[] { 4, 0xE8, 0x07, 1, 2 }, Bin(new DateTime(2024, 1, 2)));
	}

	[Fact]
	public void AppendBinary_writes_DateOnly()
	{
		Assert.Equal(new byte[] { 4, 0xE8, 0x07, 1, 2 }, Bin(new DateOnly(2024, 1, 2)));
	}

	[Fact]
	public void AppendBinary_writes_Time_with_sign_and_optional_micros()
	{
		Assert.Equal(new byte[] { 0 }, Bin(TimeSpan.Zero));
		Assert.Equal(new byte[] { 8, 0, 1, 0, 0, 0, 2, 3, 4 }, Bin(new TimeSpan(1, 2, 3, 4)));
		// 负值：长度 8 + 负号标记 1
		Assert.Equal(new byte[] { 8, 1, 0, 0, 0, 0, 0, 0, 5 }, Bin(new TimeSpan(0, 0, 0, -5)));
		Assert.Equal(new byte[] { 8, 0, 0, 0, 0, 0, 3, 4, 5 }, Bin(new TimeOnly(3, 4, 5)));
	}

	[Fact]
	public void AppendBinary_writes_guid_binary16_big_endian()
	{
		var guid = new Guid("11223344-5566-7788-99aa-bbccddeeff01");
		var expected = new byte[] { 16, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x01 };
		var writer = new ByteBufferWriter(32);
		new MySqlParameter("@p", guid).AppendBinary(writer, StatementPreparerOptions.GuidFormatBinary16);
		Assert.Equal(expected, writer.ArraySegment.ToArray());
	}

	[Fact]
	public void AppendBinary_writes_enum_as_underlying_number_or_name()
	{
		Assert.Equal(new byte[] { 3, 0, 0, 0 }, Bin(DayOfWeek.Wednesday));
		var p = new MySqlParameter { ParameterName = "@p", MySqlDbType = MySqlDbType.VarChar, Value = DayOfWeek.Wednesday };
		var writer = new ByteBufferWriter(32);
		p.AppendBinary(writer, StatementPreparerOptions.None);
		Assert.Equal(new byte[] { 9 }.Concat(Encoding.ASCII.GetBytes("Wednesday")).ToArray(), writer.ArraySegment.ToArray());
	}

	[Fact]
	public void AppendBinary_writes_MySqlDateTime()
	{
		Assert.Equal(new byte[] { 0 }, Bin(new MySqlDateTime(0, 0, 0, 0, 0, 0, 0)));
		Assert.Equal(new byte[] { 7, 0xE8, 0x07, 1, 2, 3, 4, 5 }, Bin(new MySqlDateTime(2024, 1, 2, 3, 4, 5, 0)));
	}

	[Fact]
	public void ConvertFloatsToBytes_is_little_endian()
	{
		// 在小端平台上直接内存重解释；期望值用 BitConverter 生成（本机为小端）
		var expected = BitConverter.GetBytes(1f).Concat(BitConverter.GetBytes(-2.5f)).ToArray();
		Assert.True(MySqlParameter.ConvertFloatsToBytes(new float[] { 1f, -2.5f }).SequenceEqual(expected));
	}

	// ---------- MySqlParameterCollection ----------

	[Fact]
	public void Collection_flags_and_SyncRoot()
	{
		var coll = new MySqlParameterCollection();
		Assert.False(coll.IsFixedSize);
		Assert.False(coll.IsReadOnly);
		Assert.False(coll.IsSynchronized);
		Assert.Throws<NotSupportedException>(() => coll.SyncRoot);
	}

	[Fact]
	public void Add_with_name_and_DbType_returns_parameter()
	{
		var coll = new MySqlParameterCollection();
		var p = coll.Add("@p", DbType.Int64);
		Assert.Equal("@p", p.ParameterName);
		Assert.Equal(DbType.Int64, p.DbType);
		Assert.True(p.HasSetDbType);
		Assert.Same(p, coll[0]);
		Assert.Equal(1, coll.Count);
	}

	[Fact]
	public void Add_with_MySqlDbType_overloads()
	{
		var coll = new MySqlParameterCollection();
		var p1 = coll.Add("@a", MySqlDbType.Int32);
		Assert.Equal(DbType.Int32, p1.DbType);
		var p2 = coll.Add("@b", MySqlDbType.VarBinary, 64);
		Assert.Equal(64, p2.Size);
		Assert.Equal(DbType.Binary, p2.DbType);
		Assert.Equal(2, coll.Count);
	}

	[Fact]
	public void AddWithValue_infers_type_from_value()
	{
		var coll = new MySqlParameterCollection();
		var p = coll.AddWithValue("@p", 42);
		Assert.Equal(DbType.Int32, p.DbType);
		Assert.Equal(MySqlDbType.Int32, p.MySqlDbType);
		Assert.Same(p, coll["p"]);
	}

	[Fact]
	public void Add_rejects_null_and_non_MySqlParameter()
	{
		var coll = new MySqlParameterCollection();
		Assert.Throws<ArgumentNullException>(() => coll.Add((MySqlParameter)null!));
		Assert.Throws<ArgumentNullException>(() => coll.Add((object)null!));
		Assert.Throws<InvalidCastException>(() => coll.Add((object)"oops"));
	}

	[Fact]
	public void Add_duplicate_normalized_name_throws()
	{
		var coll = new MySqlParameterCollection();
		coll.AddWithValue("@p", 1);
		// @p 与 ?p 归一化后同名
		Assert.Throws<MySqlException>(() => coll.Add(new MySqlParameter("?p", 2)));
	}

	[Fact]
	public void Add_duplicate_name_is_case_insensitive()
	{
		var coll = new MySqlParameterCollection();
		coll.AddWithValue("@p", 1);
		var ex = Assert.Throws<MySqlException>(() => coll.Add(new MySqlParameter("@P", 2)));
		Assert.Contains("already been defined", ex.Message);
	}

	[Fact]
	public void Multiple_unnamed_parameters_are_allowed()
	{
		// 归一化名为空时不进名字字典，也不做重名检查
		var coll = new MySqlParameterCollection();
		coll.Add(new MySqlParameter());
		coll.Add(new MySqlParameter());
		Assert.Equal(2, coll.Count);
	}

	[Fact]
	public void IndexOf_matches_all_prefix_and_case_variants()
	{
		var coll = new MySqlParameterCollection();
		coll.AddWithValue("@p", 1);
		Assert.Equal(0, coll.IndexOf("p"));
		Assert.Equal(0, coll.IndexOf("@p"));
		Assert.Equal(0, coll.IndexOf("?p"));
		Assert.Equal(0, coll.IndexOf("P"));
		Assert.Equal(-1, coll.IndexOf("missing"));
		// null 名归一化为空串，空串不会注册进字典
		Assert.Equal(-1, coll.IndexOf((string?)null));
	}

	[Fact]
	public void Contains_supports_parameter_object_and_name()
	{
		var coll = new MySqlParameterCollection();
		var p = coll.AddWithValue("@p", 1);
		Assert.True(coll.Contains(p));
		Assert.True(coll.Contains("p"));
		Assert.True(coll.Contains("@p"));
		Assert.False(coll.Contains("missing"));
		Assert.False(coll.Contains((object)new MySqlParameter()));
		Assert.False(coll.Contains((object)"p"));   // 非 MySqlParameter 对象永远 false
		Assert.False(coll.Contains((object)null!));
	}

	[Fact]
	public void IndexOf_object_returns_minus_one_for_non_parameters()
	{
		var coll = new MySqlParameterCollection();
		coll.AddWithValue("@p", 1);
		Assert.Equal(0, coll.IndexOf((object)coll[0]));
		Assert.Equal(-1, coll.IndexOf((object)"str"));
		Assert.Equal(-1, coll.IndexOf((object)null!));
	}

	[Fact]
	public void Indexer_by_name_throws_when_not_found()
	{
		var coll = new MySqlParameterCollection();
		var ex = Assert.Throws<ArgumentException>(() => coll["missing"]);
		Assert.Contains("not found", ex.Message);
	}

	[Fact]
	public void Indexer_by_index_validates_range()
	{
		var coll = new MySqlParameterCollection();
		Assert.Throws<ArgumentOutOfRangeException>(() => coll[0]);
		coll.AddWithValue("@p", 1);
		Assert.Throws<ArgumentOutOfRangeException>(() => coll[1]);
		Assert.Throws<ArgumentOutOfRangeException>(() => coll[-1]);
	}

	[Fact]
	public void Indexer_set_replaces_and_detaches_old_parameter()
	{
		var coll = new MySqlParameterCollection();
		var old = coll.AddWithValue("@old", 1);
		var replacement = new MySqlParameter("@new", 2);
		coll[0] = replacement;
		Assert.Same(replacement, coll[0]);
		Assert.Null(old.ParameterCollection);
		Assert.Same(coll, replacement.ParameterCollection);
		Assert.Equal(0, coll.IndexOf("new"));
		Assert.Equal(-1, coll.IndexOf("old"));
	}

	[Fact]
	public void Indexer_set_rejects_null()
	{
		var coll = new MySqlParameterCollection();
		coll.AddWithValue("@p", 1);
		Assert.Throws<ArgumentNullException>(() => coll[0] = null!);
	}

	[Fact]
	public void Indexer_set_with_duplicate_name_throws()
	{
		var coll = new MySqlParameterCollection();
		coll.AddWithValue("@a", 1);
		coll.AddWithValue("@b", 2);
		// 字典 Add 发现 "b" 已存在 → ArgumentException（Dictionary 重复键）
		Assert.Throws<ArgumentException>(() => coll[0] = new MySqlParameter("@b", 3));
	}

	[Fact]
	public void Indexer_by_name_set_replaces_and_throws_when_missing()
	{
		var coll = new MySqlParameterCollection();
		coll.AddWithValue("@a", 1);
		var replacement = new MySqlParameter("@c", 2);
		coll["a"] = replacement;
		Assert.Same(replacement, coll[0]);
		// 按名字替换时先 IndexOf，找不到返回 -1 → 访问 m_parameters[-1] 抛下标越界
		Assert.Throws<ArgumentOutOfRangeException>(() => coll["missing"] = new MySqlParameter("@x", 1));
	}

	[Fact]
	public void Insert_shifts_existing_name_indexes()
	{
		var coll = new MySqlParameterCollection();
		coll.AddWithValue("@a", 1);
		coll.AddWithValue("@b", 2);
		coll.Insert(0, new MySqlParameter("@c", 3));
		Assert.Equal(0, coll.IndexOf("c"));
		Assert.Equal(1, coll.IndexOf("a"));
		Assert.Equal(2, coll.IndexOf("b"));

		// 插到末尾（index == Count）不触发索引搬移分支
		coll.Insert(coll.Count, new MySqlParameter("@d", 4));
		Assert.Equal(3, coll.IndexOf("d"));
	}

	[Fact]
	public void Insert_rejects_null_and_duplicate()
	{
		var coll = new MySqlParameterCollection();
		coll.AddWithValue("@a", 1);
		Assert.Throws<ArgumentNullException>(() => coll.Insert(0, null!));
		Assert.Throws<MySqlException>(() => coll.Insert(0, (object)new MySqlParameter("@a", 2)));
	}

	[Fact]
	public void RemoveAt_reindexes_following_entries()
	{
		var coll = new MySqlParameterCollection();
		var a = coll.AddWithValue("@a", 1);
		var b = coll.AddWithValue("@b", 2);
		var c = coll.AddWithValue("@c", 3);
		coll.RemoveAt(1);
		Assert.Equal(2, coll.Count);
		Assert.Equal(0, coll.IndexOf("a"));
		Assert.Equal(-1, coll.IndexOf("b"));
		Assert.Equal(1, coll.IndexOf("c"));
		Assert.Null(b.ParameterCollection);      // 被移除的参数脱离集合
		Assert.Same(coll, a.ParameterCollection);
		Assert.Same(coll, c.ParameterCollection);
	}

	[Fact]
	public void RemoveAt_by_name_removes_parameter()
	{
		var coll = new MySqlParameterCollection();
		coll.AddWithValue("@a", 1);
		coll.AddWithValue("@b", 2);
		coll.RemoveAt("?b");
		Assert.Equal(1, coll.Count);
		Assert.Same(coll["@a"], coll[0]);
	}

	[Fact]
	public void Remove_absent_parameter_throws_out_of_range()
	{
		var coll = new MySqlParameterCollection();
		// IndexOf 返回 -1 后直接访问 m_parameters[-1]
		Assert.Throws<ArgumentOutOfRangeException>(() => coll.Remove((object)new MySqlParameter("@x", 1)));
		Assert.Throws<ArgumentOutOfRangeException>(() => coll.RemoveAt("missing"));
		Assert.Throws<ArgumentOutOfRangeException>(() => coll.RemoveAt(0));
		Assert.Throws<ArgumentNullException>(() => coll.Remove((object)null!));
	}

	[Fact]
	public void Clear_detaches_all_parameters()
	{
		var coll = new MySqlParameterCollection();
		var a = coll.AddWithValue("@a", 1);
		var b = coll.AddWithValue("@b", 2);
		coll.Clear();
		Assert.Equal(0, coll.Count);
		Assert.Null(a.ParameterCollection);
		Assert.Null(b.ParameterCollection);
		Assert.Equal(-1, coll.IndexOf("a"));
	}

	[Fact]
	public void Rename_through_ParameterName_updates_collection()
	{
		var coll = new MySqlParameterCollection();
		var p = coll.AddWithValue("@old", 1);
		p.ParameterName = "@new";
		Assert.Equal(0, coll.IndexOf("new"));
		Assert.Equal(-1, coll.IndexOf("old"));
		Assert.Same(p, coll["new"]);
	}

	[Fact]
	public void Rename_to_conflicting_name_throws()
	{
		var coll = new MySqlParameterCollection();
		var a = coll.AddWithValue("@a", 1);
		coll.AddWithValue("@b", 2);
		Assert.Throws<MySqlException>(() => a.ParameterName = "@b");
	}

	[Fact]
	public void Renaming_unnamed_parameter_registers_it()
	{
		var coll = new MySqlParameterCollection();
		coll.AddWithValue("@a", 1);
		var unnamed = coll.Add(new MySqlParameter());
		unnamed.ParameterName = "@z";
		Assert.Equal(1, coll.IndexOf("z"));
	}

	[Fact]
	public void Renaming_to_empty_unregisters_parameter()
	{
		var coll = new MySqlParameterCollection();
		var p = coll.AddWithValue("@a", 1);
		p.ParameterName = "";
		Assert.Equal(-1, coll.IndexOf("a"));
		// 名字腾出后可再次添加
		coll.AddWithValue("@a", 2);
		Assert.Equal(1, coll.IndexOf("a"));
	}

	[Fact]
	public void AddRange_appends_and_rejects_null_elements()
	{
		var coll = new MySqlParameterCollection();
		coll.AddRange(new object[] { new MySqlParameter("@a", 1), new MySqlParameter("@b", 2) });
		Assert.Equal(2, coll.Count);
		Assert.Throws<ArgumentNullException>(() => coll.AddRange(new object?[] { null }));
	}

	[Fact]
	public void CopyTo_copies_into_array_at_index()
	{
		var coll = new MySqlParameterCollection();
		var a = coll.AddWithValue("@a", 1);
		var b = coll.AddWithValue("@b", 2);
		var array = new MySqlParameter[3];
		coll.CopyTo(array, 1);
		Assert.Null(array[0]);
		Assert.Same(a, array[1]);
		Assert.Same(b, array[2]);
	}

	[Fact]
	public void Enumeration_preserves_insertion_order()
	{
		var coll = new MySqlParameterCollection();
		var a = coll.AddWithValue("@a", 1);
		var b = coll.AddWithValue("@b", 2);
		Assert.Collection(coll, x => Assert.Same(a, x), x => Assert.Same(b, x));

		var plain = new List<object>();
		foreach (object o in (IEnumerable)coll)
		{
			plain.Add(o);
		}
		Assert.Equal(2, plain.Count);
	}

	// ---------- MySqlAttribute / MySqlAttributeCollection ----------

	[Fact]
	public void MySqlAttribute_null_and_default_names_become_empty()
	{
		Assert.Equal("", new MySqlAttribute().AttributeName);
		Assert.Null(new MySqlAttribute().Value);
		Assert.Equal("", new MySqlAttribute(null, 1).AttributeName);
	}

	[Fact]
	public void MySqlAttribute_Clone_shares_value_reference()
	{
		var payload = new object();
		var c = new MySqlAttribute("k", payload).Clone();
		Assert.Equal("k", c.AttributeName);
		Assert.Same(payload, c.Value);
		Assert.Equal("k", ((MySqlAttribute)((ICloneable)new MySqlAttribute("k", 2)).Clone()).AttributeName);
	}

	[Fact]
	public void MySqlAttributeCollection_Add_validates_input()
	{
		var col = new MySqlAttributeCollection();
		Assert.Throws<ArgumentNullException>(() => col.Add(null!));
		Assert.Throws<ArgumentException>(() => col.Add(new MySqlAttribute()));
		var added = new MySqlAttribute("k", "v");
		col.Add(added);
		Assert.Equal(1, col.Count);
		Assert.Same(added, col[0]);

		var ex = Assert.Throws<ArgumentException>(() => col.Add(new MySqlAttribute("k", "v2")));
		Assert.Contains("already exists", ex.Message);
	}

	[Fact]
	public void MySqlAttributeCollection_SetAttribute_validates_name()
	{
		var col = new MySqlAttributeCollection();
		Assert.Throws<ArgumentException>(() => col.SetAttribute("", 1));
	}

	[Fact]
	public void MySqlAttributeCollection_SetAttribute_replaces_whole_instance()
	{
		var col = new MySqlAttributeCollection();
		col.SetAttribute("k", "v1");
		var original = col[0];
		col.SetAttribute("k", "v2");
		Assert.Equal(1, col.Count);
		// 覆盖是整体替换为新实例，而非改写原实例
		Assert.NotSame(original, col[0]);
		Assert.Equal("v2", col[0].Value);
	}

	[Fact]
	public void MySqlAttributeCollection_Remove_by_reference()
	{
		var col = new MySqlAttributeCollection();
		var a = new MySqlAttribute("a", 1);
		var b = new MySqlAttribute("b", 2);
		col.Add(a);
		col.Add(b);
		Assert.True(col.Remove(a));
		Assert.Equal(1, col.Count);
		// List.Remove 按引用比较：同名新实例不会命中
		Assert.False(col.Remove(new MySqlAttribute("b", 2)));
		Assert.False(col.Remove(null!));
	}

	[Fact]
	public void MySqlAttributeCollection_enumerates_in_order()
	{
		var col = new MySqlAttributeCollection();
		var a = new MySqlAttribute("a", 1);
		var b = new MySqlAttribute("b", 2);
		col.Add(a);
		col.Add(b);
		Assert.Collection(col, x => Assert.Same(a, x), x => Assert.Same(b, x));
		Assert.Throws<ArgumentOutOfRangeException>(() => col[2]);
	}

	[Fact]
	public void MySqlAttribute_ToParameter_maps_name_and_value()
	{
		var p = new MySqlAttribute("k", 42).ToParameter();
		Assert.Equal("k", p.ParameterName);
		Assert.Equal(42, p.Value);
		Assert.Equal(DbType.Int32, p.DbType);    // 类型由值推导
		Assert.Throws<InvalidOperationException>(() => new MySqlAttribute().ToParameter());
	}

	[Fact]
	public void MySqlAttribute_ToOracleParameter_maps_name_and_value()
	{
		var p = new MySqlAttribute("k", 42).ToOracleParameter();
		Assert.Equal("k", p.ParameterName);
		Assert.Equal(42, p.Value);
		Assert.Throws<InvalidOperationException>(() => new MySqlAttribute().ToOracleParameter());
	}
}
