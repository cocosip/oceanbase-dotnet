using System;
using System.Data;
using OceanBase.Core;
using OceanBase.Protocol;
using OceanBase.Protocol.Payloads;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 类型映射测试（internal 类型，无需服务端）。
///
/// <see cref="TypeMapper"/> 维护 列类型名 ⇄ MySqlDbType/OracleDbType ⇄ DbType ⇄ CLR 类型
/// 的全部映射表，并做二进制列定义 → 提供程序类型的推导。
/// </summary>
public sealed class TypeMapperTests
{
	// ---------- 列类型名 → 提供程序类型 ----------

	[Theory]
	[InlineData("TINYINT", false, MySqlDbType.Byte)]
	[InlineData("TINYINT", true, MySqlDbType.UByte)]
	[InlineData("SMALLINT", false, MySqlDbType.Int16)]
	[InlineData("SMALLINT", true, MySqlDbType.UInt16)]
	[InlineData("MEDIUMINT", false, MySqlDbType.Int24)]
	[InlineData("INT", true, MySqlDbType.UInt32)]
	[InlineData("BIGINT", false, MySqlDbType.Int64)]
	[InlineData("BIGINT", true, MySqlDbType.UInt64)]
	[InlineData("DECIMAL", false, MySqlDbType.NewDecimal)]
	[InlineData("DOUBLE", false, MySqlDbType.Double)]
	[InlineData("FLOAT", false, MySqlDbType.Float)]
	[InlineData("VARCHAR", false, MySqlDbType.VarChar)]
	[InlineData("CHAR", false, MySqlDbType.String)]
	[InlineData("TEXT", false, MySqlDbType.Text)]
	[InlineData("LONGTEXT", false, MySqlDbType.LongText)]
	[InlineData("BLOB", false, MySqlDbType.Blob)]
	[InlineData("VARBINARY", false, MySqlDbType.VarBinary)]
	[InlineData("JSON", false, MySqlDbType.JSON)]
	[InlineData("DATETIME", false, MySqlDbType.DateTime)]
	[InlineData("DATE", false, MySqlDbType.Date)]
	[InlineData("TIME", false, MySqlDbType.Time)]
	[InlineData("TIMESTAMP", false, MySqlDbType.Timestamp)]
	[InlineData("YEAR", false, MySqlDbType.Year)]
	[InlineData("VECTOR", false, MySqlDbType.Vector)]
	public void GetMySqlDbType_maps_column_type_names(string typeName, bool unsigned, MySqlDbType expected)
	{
		Assert.Equal(expected, TypeMapper.Instance.GetMySqlDbType(typeName, unsigned, 0, MySqlGuidFormat.Default));
	}

	[Theory]
	[InlineData("TINYINT", OracleDbType.Byte)]
	[InlineData("SMALLINT", OracleDbType.Int16)]
	[InlineData("NUMBER", OracleDbType.Int32)]
	[InlineData("BIGINT", OracleDbType.Int64)]
	[InlineData("VARCHAR2", OracleDbType.Varchar2)]
	[InlineData("NVARCHAR2", OracleDbType.NVarchar2)]
	[InlineData("CHAR", OracleDbType.Char)]
	[InlineData("NCHAR", OracleDbType.NChar)]
	[InlineData("CLOB", OracleDbType.Clob)]
	[InlineData("BLOB", OracleDbType.Blob)]
	[InlineData("RAW", OracleDbType.Raw)]
	[InlineData("TIMESTAMP", OracleDbType.TimeStamp)]
	[InlineData("TIMESTAMP WITH TIME ZONE", OracleDbType.TimeStampTZ)]
	[InlineData("INTERVAL DAY TO SECOND", OracleDbType.IntervalDS)]
	[InlineData("REF CURSOR", OracleDbType.RefCursor)]
	public void GetOracleDbType_maps_column_type_names(string typeName, OracleDbType expected)
	{
		Assert.Equal(expected, TypeMapper.Instance.GetOracleDbType(typeName, unsigned: false, 0));
		Assert.True(TypeMapper.Instance.TryGetOracleDbType(typeName, unsigned: false, 0, out var actual));
		Assert.Equal(expected, actual);
	}

	[Fact]
	public void GetMySqlDbType_char36_guid_format_infers_guid()
	{
		Assert.Equal(MySqlDbType.Guid, TypeMapper.Instance.GetMySqlDbType("CHAR", false, 36, MySqlGuidFormat.Char36));
		Assert.Equal(MySqlDbType.Guid, TypeMapper.Instance.GetMySqlDbType("CHAR", false, 32, MySqlGuidFormat.Char32));
		// 注册表里带有 CHAR(36) 的默认条目，默认格式同样推断为 Guid
		Assert.Equal(MySqlDbType.Guid, TypeMapper.Instance.GetMySqlDbType("CHAR", false, 36, MySqlGuidFormat.Default));
		// 长度既非 32 也非 36 时退回普通 CHAR
		Assert.Equal(MySqlDbType.String, TypeMapper.Instance.GetMySqlDbType("CHAR", false, 33, MySqlGuidFormat.Default));
	}

	// ---------- DbType ⇄ 提供程序类型 ----------

	[Theory]
	[InlineData(MySqlDbType.Bool, DbType.Boolean)]
	[InlineData(MySqlDbType.Byte, DbType.SByte)]
	[InlineData(MySqlDbType.UByte, DbType.Byte)]
	[InlineData(MySqlDbType.Int16, DbType.Int16)]
	[InlineData(MySqlDbType.Int32, DbType.Int32)]
	[InlineData(MySqlDbType.Int64, DbType.Int64)]
	[InlineData(MySqlDbType.UInt64, DbType.UInt64)]
	[InlineData(MySqlDbType.Double, DbType.Double)]
	[InlineData(MySqlDbType.NewDecimal, DbType.Decimal)]
	[InlineData(MySqlDbType.VarChar, DbType.String)]
	[InlineData(MySqlDbType.Date, DbType.Date)]
	[InlineData(MySqlDbType.Time, DbType.Time)]
	[InlineData(MySqlDbType.DateTime, DbType.DateTime)]
	[InlineData(MySqlDbType.Text, DbType.String)]
	public void GetDbTypeForMySqlDbType(MySqlDbType mySqlDbType, DbType expected)
	{
		Assert.Equal(expected, TypeMapper.Instance.GetDbTypeForMySqlDbType(mySqlDbType));
	}

	[Theory]
	[InlineData(DbType.Int32, MySqlDbType.Int32)]
	[InlineData(DbType.UInt32, MySqlDbType.UInt32)]
	[InlineData(DbType.String, MySqlDbType.VarChar)]  // String/AnsiString/Xml 共用映射，VarChar 最先注册
	[InlineData(DbType.Binary, MySqlDbType.Blob)]     // Binary 系列映射最先注册的是 BLOB
	[InlineData(DbType.Time, MySqlDbType.Time)]
	[InlineData(DbType.Boolean, MySqlDbType.Bool)]
	public void GetMySqlDbTypeForDbType(DbType dbType, MySqlDbType expected)
	{
		Assert.Equal(expected, TypeMapper.Instance.GetMySqlDbTypeForDbType(dbType));
	}

	[Fact]
	public void GetMySqlDbTypeForDbType_xml_maps_to_varchar()
	{
		Assert.Equal(MySqlDbType.VarChar, TypeMapper.Instance.GetMySqlDbTypeForDbType(DbType.Xml));
	}

	[Theory]
	[InlineData(OracleDbType.Blob, DbType.Object)]
	[InlineData(OracleDbType.Byte, DbType.Byte)]
	[InlineData(OracleDbType.Int16, DbType.Int16)]
	[InlineData(OracleDbType.Decimal, DbType.Decimal)]
	[InlineData(OracleDbType.Date, DbType.Date)]
	[InlineData(OracleDbType.TimeStamp, DbType.DateTime)]
	[InlineData(OracleDbType.TimeStampTZ, DbType.DateTimeOffset)]
	[InlineData(OracleDbType.Varchar2, DbType.String)]
	[InlineData(OracleDbType.Raw, DbType.Binary)]
	[InlineData(OracleDbType.Boolean, DbType.Boolean)]
	[InlineData(OracleDbType.Vector, DbType.Object)]
	public void GetDbTypeForOracleDbType(OracleDbType oracleDbType, DbType expected)
	{
		Assert.Equal(expected, TypeMapper.GetDbTypeForOracleDbType(oracleDbType));
	}

	[Theory]
	[InlineData(DbType.Binary, OracleDbType.Raw)]
	[InlineData(DbType.Byte, OracleDbType.Byte)]
	[InlineData(DbType.Date, OracleDbType.Date)]
	[InlineData(DbType.DateTime, OracleDbType.TimeStamp)]
	[InlineData(DbType.DateTimeOffset, OracleDbType.TimeStampTZ)]
	[InlineData(DbType.Decimal, OracleDbType.Decimal)]
	[InlineData(DbType.Double, OracleDbType.Double)]
	[InlineData(DbType.Int64, OracleDbType.Int64)]
	[InlineData(DbType.Object, OracleDbType.Object)]
	[InlineData(DbType.Single, OracleDbType.Single)]
	[InlineData(DbType.String, OracleDbType.Varchar2)]
	[InlineData(DbType.AnsiString, OracleDbType.Varchar2)]
	[InlineData(DbType.StringFixedLength, OracleDbType.Char)]
	[InlineData(DbType.Time, OracleDbType.TimeStamp)]
	public void GetOracleDbTypeForDbType(DbType dbType, OracleDbType expected)
	{
		Assert.Equal(expected, TypeMapper.GetOracleDbTypeForDbType(dbType));
	}

	[Fact]
	public void GetOracleDbTypeForDbType_unsupported_throws()
	{
		Assert.Throws<NotSupportedException>(() => TypeMapper.GetOracleDbTypeForDbType(DbType.VarNumeric));
	}

	// ---------- CLR 类型 → DbTypeMapping ----------

	[Theory]
	[InlineData(typeof(bool), DbType.Boolean)]
	[InlineData(typeof(sbyte), DbType.SByte)]
	[InlineData(typeof(byte), DbType.Byte)]
	[InlineData(typeof(short), DbType.Int16)]
	[InlineData(typeof(ushort), DbType.UInt16)]
	[InlineData(typeof(int), DbType.Int32)]
	[InlineData(typeof(uint), DbType.UInt32)]
	[InlineData(typeof(long), DbType.Int64)]
	[InlineData(typeof(ulong), DbType.UInt64)]
	[InlineData(typeof(decimal), DbType.Decimal)]
	[InlineData(typeof(double), DbType.Double)]
	[InlineData(typeof(float), DbType.Single)]
	[InlineData(typeof(string), DbType.String)]
	[InlineData(typeof(byte[]), DbType.Binary)]
	[InlineData(typeof(DateTime), DbType.DateTime)]  // 同一 CLR 类型后注册的映射胜出
	[InlineData(typeof(Guid), DbType.Guid)]
	public void GetDbTypeMapping_by_clr_type(Type clrType, DbType expectedPrimary)
	{
		var mapping = TypeMapper.Instance.GetDbTypeMapping(clrType);
		Assert.NotNull(mapping);
		Assert.Equal(clrType, mapping!.ClrType);
		Assert.Equal(expectedPrimary, mapping.DbTypes[0]);
	}

	[Fact]
	public void GetDbTypeMapping_uses_enum_underlying_type()
	{
		var mapping = TypeMapper.Instance.GetDbTypeMapping(typeof(MyDayOfWeek));
		Assert.NotNull(mapping);
		Assert.Equal(DbType.Int32, mapping!.DbTypes[0]);
	}

	private enum MyDayOfWeek
	{
		Monday,
		Tuesday,
	}

	[Fact]
	public void GetDbTypeMapping_by_db_type_converts_values()
	{
		var mapping = TypeMapper.Instance.GetDbTypeMapping(DbType.Int32);
		Assert.NotNull(mapping);
		Assert.Equal(42, mapping!.DoConversion("42"));
	}

	[Fact]
	public void GetDbTypeMapping_by_column_type_name()
	{
		Assert.NotNull(TypeMapper.Instance.GetDbTypeMapping("INT", unsigned: false, 0));
		Assert.NotNull(TypeMapper.Instance.GetDbTypeMapping("VARCHAR", unsigned: false, 20));
	}

	// ---------- 二进制列定义 → 提供程序类型 ----------

	private static byte[] CreateColumnDefinitionBytes(
		CharacterSet characterSet,
		uint columnLength,
		byte protocolType,
		ColumnFlags flags)
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

	private static ColumnDefinitionPayload CreateColumnDefinition(CharacterSet characterSet, uint columnLength, byte protocolType, ColumnFlags flags = 0)
	{
		var bytes = CreateColumnDefinitionBytes(characterSet, columnLength, protocolType, flags);
		var resizable = new ResizableArray<byte>();
		Utility.Resize(ref resizable, bytes.Length);
		bytes.CopyTo(resizable.Array!.AsSpan(0));
		ColumnDefinitionPayload payload = null!;
		ColumnDefinitionPayload.Initialize(ref payload, new ResizableArraySegment<byte>(resizable, 0, bytes.Length), isOracleMode: false);
		return payload;
	}

	[Theory]
	[InlineData(1, 1, false, MySqlDbType.Byte)]
	[InlineData(1, 1, true, MySqlDbType.UByte)]
	[InlineData(3, 0, false, MySqlDbType.Int32)]
	[InlineData(3, 0, true, MySqlDbType.UInt32)]
	[InlineData(8, 0, false, MySqlDbType.Int64)]
	[InlineData(2, 0, true, MySqlDbType.UInt16)]
	[InlineData(16, 0, false, MySqlDbType.Bit)]
	[InlineData(245, 0, false, MySqlDbType.JSON)]
	[InlineData(10, 0, false, MySqlDbType.Date)]
	[InlineData(12, 0, false, MySqlDbType.DateTime)]
	[InlineData(7, 0, false, MySqlDbType.Timestamp)]
	[InlineData(11, 0, false, MySqlDbType.Time)]
	[InlineData(13, 0, false, MySqlDbType.Year)]
	[InlineData(4, 0, false, MySqlDbType.Float)]
	[InlineData(5, 0, false, MySqlDbType.Double)]
	[InlineData(246, 0, false, MySqlDbType.NewDecimal)]
	[InlineData(255, 0, false, MySqlDbType.Geometry)]
	[InlineData(242, 0, false, MySqlDbType.Vector)]
	public void ConvertToMySqlDbType_from_binary_column_definition(byte protocolType, uint columnLength, bool unsigned, MySqlDbType expected)
	{
		var flags = unsigned ? ColumnFlags.Unsigned : (ColumnFlags)0;
		var column = CreateColumnDefinition(CharacterSet.Utf8Mb4GeneralCaseInsensitive, columnLength, protocolType, flags);
		Assert.Equal(expected, TypeMapper.ConvertToMySqlDbType(column, treatTinyAsBoolean: false, MySqlGuidFormat.Default));
	}

	[Fact]
	public void ConvertToMySqlDbType_tinyint_as_boolean()
	{
		// 列长为 1 的带符号 TINYINT 在 TreatTinyAsBoolean 下推断为 Bool
		var column = CreateColumnDefinition(CharacterSet.Utf8Mb4GeneralCaseInsensitive, 1, 1);
		Assert.Equal(MySqlDbType.Bool, TypeMapper.ConvertToMySqlDbType(column, treatTinyAsBoolean: true, MySqlGuidFormat.Default));
		Assert.Equal(MySqlDbType.Byte, TypeMapper.ConvertToMySqlDbType(column, treatTinyAsBoolean: false, MySqlGuidFormat.Default));

		// 列长不为 1 时不按布尔处理
		var wide = CreateColumnDefinition(CharacterSet.Utf8Mb4GeneralCaseInsensitive, 4, 1);
		Assert.Equal(MySqlDbType.Byte, TypeMapper.ConvertToMySqlDbType(wide, treatTinyAsBoolean: true, MySqlGuidFormat.Default));
	}

	[Fact]
	public void ConvertToMySqlDbType_string_columns_by_charset()
	{
		// 文本字符集 → String/VarChar/Text 系列
		var varString = CreateColumnDefinition(CharacterSet.Utf8Mb4GeneralCaseInsensitive, 100, 253);
		Assert.Equal(MySqlDbType.VarChar, TypeMapper.ConvertToMySqlDbType(varString, false, MySqlGuidFormat.Default));

		var blob = CreateColumnDefinition(CharacterSet.Utf8Mb4GeneralCaseInsensitive, 100, 252);
		Assert.Equal(MySqlDbType.Text, TypeMapper.ConvertToMySqlDbType(blob, false, MySqlGuidFormat.Default));

		// 二进制字符集 → Binary/VarBinary/Blob 系列；长度 16 + Binary16 格式 → Guid
		var binary = CreateColumnDefinition(CharacterSet.Binary, 100, 254);
		Assert.Equal(MySqlDbType.Binary, TypeMapper.ConvertToMySqlDbType(binary, false, MySqlGuidFormat.Default));

		var varBinary = CreateColumnDefinition(CharacterSet.Binary, 100, 253);
		Assert.Equal(MySqlDbType.VarBinary, TypeMapper.ConvertToMySqlDbType(varBinary, false, MySqlGuidFormat.Default));

		var guid = CreateColumnDefinition(CharacterSet.Binary, 16, 253);
		Assert.Equal(MySqlDbType.Guid, TypeMapper.ConvertToMySqlDbType(guid, false, MySqlGuidFormat.Binary16));
	}

	[Fact]
	public void ConvertToMySqlDbType_char36_guid_by_bytes_per_character()
	{
		// utf8mb4 每字符 4 字节：36 字符 GUID 的列长是 144
		var column = CreateColumnDefinition(CharacterSet.Utf8Mb4GeneralCaseInsensitive, 144, 254);
		Assert.Equal(MySqlDbType.Guid, TypeMapper.ConvertToMySqlDbType(column, false, MySqlGuidFormat.Char36));
		Assert.Equal(MySqlDbType.String, TypeMapper.ConvertToMySqlDbType(column, false, MySqlGuidFormat.Default));
	}

	[Fact]
	public void ConvertToMySqlDbType_enum_and_set_flags()
	{
		var enumColumn = CreateColumnDefinition(CharacterSet.Utf8Mb4GeneralCaseInsensitive, 10, 254, ColumnFlags.Enum);
		Assert.Equal(MySqlDbType.Enum, TypeMapper.ConvertToMySqlDbType(enumColumn, false, MySqlGuidFormat.Default));

		var setColumn = CreateColumnDefinition(CharacterSet.Utf8Mb4GeneralCaseInsensitive, 10, 254, ColumnFlags.Set);
		Assert.Equal(MySqlDbType.Set, TypeMapper.ConvertToMySqlDbType(setColumn, false, MySqlGuidFormat.Default));
	}

	// ---------- 提供程序类型 → 协议列类型 ----------

	[Theory]
	[InlineData(MySqlDbType.Bool, 1)]        // Tiny
	[InlineData(MySqlDbType.Byte, 1)]
	[InlineData(MySqlDbType.UByte, 1)]
	[InlineData(MySqlDbType.Int16, 2)]       // Short
	[InlineData(MySqlDbType.Int24, 9)]       // Int24
	[InlineData(MySqlDbType.Int32, 3)]       // Long
	[InlineData(MySqlDbType.Int64, 8)]       // Longlong
	[InlineData(MySqlDbType.Bit, 16)]
	[InlineData(MySqlDbType.String, 254)]    // String
	[InlineData(MySqlDbType.VarChar, 253)]   // VarString
	[InlineData(MySqlDbType.VarBinary, 253)]
	[InlineData(MySqlDbType.TinyText, 249)]  // TinyBlob
	[InlineData(MySqlDbType.Text, 252)]      // Blob
	[InlineData(MySqlDbType.MediumText, 250)]
	[InlineData(MySqlDbType.LongText, 251)]
	[InlineData(MySqlDbType.JSON, 245)]
	[InlineData(MySqlDbType.Date, 10)]
	[InlineData(MySqlDbType.DateTime, 12)]
	[InlineData(MySqlDbType.Timestamp, 7)]
	[InlineData(MySqlDbType.Time, 11)]
	[InlineData(MySqlDbType.Year, 13)]
	[InlineData(MySqlDbType.Float, 4)]
	[InlineData(MySqlDbType.Double, 5)]
	[InlineData(MySqlDbType.NewDecimal, 246)]
	[InlineData(MySqlDbType.Geometry, 255)]
	[InlineData(MySqlDbType.Null, 6)]
	[InlineData(MySqlDbType.Vector, 242)]
	public void ConvertToColumnTypeAndFlags(MySqlDbType dbType, int expectedProtocolType)
	{
		var value = TypeMapper.ConvertToColumnTypeAndFlags(dbType, MySqlGuidFormat.Default);
		Assert.Equal(expectedProtocolType, value & 0xFF);
	}

	[Fact]
	public void ConvertToColumnTypeAndFlags_marks_unsigned_bit()
	{
		var signed = TypeMapper.ConvertToColumnTypeAndFlags(MySqlDbType.Int32, MySqlGuidFormat.Default);
		var unsigned = TypeMapper.ConvertToColumnTypeAndFlags(MySqlDbType.UInt32, MySqlGuidFormat.Default);
		Assert.Equal(0, signed & 0x8000);
		Assert.Equal(0x8000, unsigned & 0x8000);
	}

	[Fact]
	public void ConvertToColumnTypeAndFlags_guid_uses_string_for_char_formats()
	{
		var char36 = TypeMapper.ConvertToColumnTypeAndFlags(MySqlDbType.Guid, MySqlGuidFormat.Char36);
		var binary16 = TypeMapper.ConvertToColumnTypeAndFlags(MySqlDbType.Guid, MySqlGuidFormat.Binary16);
		Assert.Equal((ushort)ColumnType.String, (ushort)(char36 & 0xFF));
		Assert.Equal((ushort)ColumnType.Blob, (ushort)(binary16 & 0xFF));
	}

	[Theory]
	[InlineData(OracleDbType.Byte, 1)]            // Tiny
	[InlineData(OracleDbType.Int16, 2)]           // Short
	[InlineData(OracleDbType.Int32, 3)]           // Long
	[InlineData(OracleDbType.Int64, 8)]           // Longlong
	[InlineData(OracleDbType.Decimal, 246)]       // NewDecimal
	[InlineData(OracleDbType.Double, 5)]
	[InlineData(OracleDbType.Date, 12)]           // DateTime
	[InlineData(OracleDbType.TimeStamp, 202)]     // OracleTimeStampNano
	[InlineData(OracleDbType.TimeStampTZ, 200)]
	[InlineData(OracleDbType.TimeStampLTZ, 201)]
	[InlineData(OracleDbType.Varchar2, 253)]      // VarString
	[InlineData(OracleDbType.Char, 254)]          // String
	[InlineData(OracleDbType.Raw, 203)]
	[InlineData(OracleDbType.Blob, 210)]
	[InlineData(OracleDbType.Clob, 211)]
	[InlineData(OracleDbType.IntervalDS, 205)]
	[InlineData(OracleDbType.IntervalYM, 204)]
	[InlineData(OracleDbType.RefCursor, 6)]       // Null
	[InlineData(OracleDbType.Object, 160)]        // OracleComplex
	[InlineData(OracleDbType.Json, 245)]
	public void ConvertToOracleColumnTypeAndFlags(OracleDbType dbType, int expectedProtocolType)
	{
		Assert.Equal(expectedProtocolType, TypeMapper.ConvertToOracleColumnTypeAndFlags(dbType) & 0xFF);
	}

	[Fact]
	public void GetColumnTypeMetadata_by_provider_type_is_populated()
	{
		var metadata = TypeMapper.Instance.GetColumnTypeMetadata(MySqlDbType.Int32);
		Assert.Equal(DbType.Int32, metadata.DbTypeMapping.DbTypes[0]);

		var oracleMetadata = TypeMapper.Instance.GetColumnTypeMetadata(OracleDbType.Varchar2);
		Assert.Equal(DbType.String, oracleMetadata.DbTypeMapping.DbTypes[0]);

		Assert.NotEmpty(TypeMapper.Instance.GetColumnTypeMetadata());
		Assert.NotEmpty(TypeMapper.Instance.GetColumnMappings());
	}
}
