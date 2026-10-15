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
/// <see cref="MySqlDbColumn"/> 与 <see cref="OracleDbColumn"/> 的元数据映射测试（internal 构造，无需服务端）。
///
/// 这两个类型把协议层的 <see cref="ColumnDefinitionPayload"/> 翻译成 ADO.NET 的
/// <see cref="System.Data.Common.DbColumn"/> 契约（GetSchemaTable / GetColumnSchema 的数据源），
/// 上层 ORM 依赖其中每一项；这里用手工构造的列定义逐项校验。
/// </summary>
public sealed class DbColumnTests
{
	private static byte[] CreateColumnDefinitionBytes(
		string name = "col",
		string physicalName = "col",
		string table = "t",
		string physicalTable = "t",
		CharacterSet characterSet = CharacterSet.Utf8Mb4GeneralCaseInsensitive,
		uint columnLength = 100,
		byte protocolType = 3,
		ColumnFlags flags = (ColumnFlags)0,
		byte decimals = 0,
		byte precision = 0)
	{
		var w = new ByteBufferWriter(128);
		w.WriteLengthEncodedString("def");
		w.WriteLengthEncodedString("db");
		w.WriteLengthEncodedString(table);
		w.WriteLengthEncodedString(physicalTable);
		w.WriteLengthEncodedString(name);
		w.WriteLengthEncodedString(physicalName);
		w.Write((byte)12);
		w.Write((ushort)characterSet);
		w.Write(columnLength);
		w.Write(protocolType);
		w.Write((ushort)flags);
		w.Write(decimals);
		w.Write(precision);
		w.Write((byte)0);
		return w.ToPayloadData().Span.ToArray();
	}

	private static ColumnDefinitionPayload CreateColumnDefinition(byte[] bytes, bool isOracleMode = false)
	{
		var resizable = new ResizableArray<byte>();
		Utility.Resize(ref resizable, bytes.Length);
		bytes.CopyTo(resizable.Array!.AsSpan(0));
		ColumnDefinitionPayload payload = null!;
		ColumnDefinitionPayload.Initialize(ref payload, new ResizableArraySegment<byte>(resizable, 0, bytes.Length), isOracleMode);
		return payload;
	}

	private static MySqlDbColumn CreateMySqlColumn(
		MySqlDbType mySqlDbType,
		ColumnFlags flags = (ColumnFlags)0,
		uint columnLength = 100,
		byte protocolType = 3,
		CharacterSet characterSet = CharacterSet.Utf8Mb4GeneralCaseInsensitive,
		byte decimals = 0,
		string name = "col",
		string physicalName = "col",
		bool allowZeroDateTime = false,
		int ordinal = 0)
	{
		var payload = CreateColumnDefinition(CreateColumnDefinitionBytes(
			name: name, physicalName: physicalName, characterSet: characterSet,
			columnLength: columnLength, protocolType: protocolType, flags: flags, decimals: decimals));
		return new MySqlDbColumn(ordinal, payload, allowZeroDateTime, mySqlDbType);
	}

	// ---------- MySqlDbColumn 基础契约 ----------

	[Fact]
	public void MySqlDbColumn_maps_names_and_ordinal()
	{
		var column = CreateMySqlColumn(MySqlDbType.Int32, name: "amount", physicalName: "amount", ordinal: 3);
		Assert.Equal("amount", column.ColumnName);
		Assert.Equal("amount", column.BaseColumnName);
		Assert.Equal(3, column.ColumnOrdinal);
		// 实测：BaseCatalogName 被显式置为 null（协议里的 "def" 不向外暴露）
		Assert.Null(column.BaseCatalogName);
		Assert.Equal("db", column.BaseSchemaName);
		Assert.Equal("t", column.BaseTableName);
		Assert.Equal("t", column.TableName);
	}

	[Fact]
	public void MySqlDbColumn_nullable_defaults_to_true()
	{
		// 未置 NotNull 标志 → 允许 NULL
		Assert.True(CreateMySqlColumn(MySqlDbType.Int32).AllowDBNull);

		var notNull = CreateMySqlColumn(MySqlDbType.Int32, ColumnFlags.NotNull);
		Assert.False(notNull.AllowDBNull);
	}

	[Fact]
	public void MySqlDbColumn_maps_key_and_increment_flags()
	{
		var plain = CreateMySqlColumn(MySqlDbType.Int32);
		Assert.False(plain.IsKey);
		Assert.False(plain.IsUnique);
		Assert.False(plain.IsAutoIncrement);
		Assert.False(plain.IsLong);
		Assert.False(plain.IsReadOnly);
		Assert.False(plain.IsAliased);

		var full = CreateMySqlColumn(
			MySqlDbType.Int32,
			ColumnFlags.PrimaryKey | ColumnFlags.UniqueKey | ColumnFlags.AutoIncrement);
		Assert.True(full.IsKey);
		Assert.True(full.IsUnique);
		Assert.True(full.IsAutoIncrement);
	}

	[Fact]
	public void MySqlDbColumn_detects_alias_by_name_mismatch()
	{
		var aliased = CreateMySqlColumn(MySqlDbType.Int32, name: "alias", physicalName: "real");
		Assert.True(aliased.IsAliased);
		Assert.Equal("alias", aliased.ColumnName);
		Assert.Equal("real", aliased.BaseColumnName);
	}

	[Fact]
	public void MySqlDbColumn_provider_type_and_data_type_name()
	{
		var column = CreateMySqlColumn(MySqlDbType.Int32);
		Assert.Equal(MySqlDbType.Int32, column.ProviderType);
		Assert.Equal(typeof(int), column.DataType);
		Assert.Equal("INT", column.DataTypeName);
	}

	[Fact]
	public void MySqlDbColumn_allowZeroDateTime_swaps_DateTime_for_MySqlDateTime()
	{
		// 仅当 CLR 类型是 DateTime 时替换；其它类型不受影响
		var dateTime = CreateMySqlColumn(MySqlDbType.DateTime, protocolType: 12);
		Assert.Equal(typeof(DateTime), dateTime.DataType);

		var zeroable = CreateMySqlColumn(MySqlDbType.DateTime, protocolType: 12, allowZeroDateTime: true);
		Assert.Equal(typeof(MySqlDateTime), zeroable.DataType);

		var intColumn = CreateMySqlColumn(MySqlDbType.Int32, allowZeroDateTime: true);
		Assert.Equal(typeof(int), intColumn.DataType);
	}

	// ---------- 列长换算 ----------

	[Fact]
	public void MySqlDbColumn_string_size_divides_by_bytes_per_character()
	{
		// utf8mb4 每字符 4 字节：列长 400 → 100 字符
		var column = CreateMySqlColumn(
			MySqlDbType.VarChar, columnLength: 400, protocolType: 253,
			characterSet: CharacterSet.Utf8Mb4GeneralCaseInsensitive);
		Assert.Equal(100, column.ColumnSize);
	}

	[Fact]
	public void MySqlDbColumn_binary_size_is_not_divided()
	{
		// 二进制字符集每字符 1 字节，长度原样保留
		var column = CreateMySqlColumn(
			MySqlDbType.VarBinary, columnLength: 400, protocolType: 253,
			characterSet: CharacterSet.Binary);
		Assert.Equal(400, column.ColumnSize);
	}

	[Fact]
	public void MySqlDbColumn_vector_size_divides_by_four()
	{
		// VECTOR 每元素 4 字节（float32）
		var column = CreateMySqlColumn(MySqlDbType.Vector, columnLength: 400, protocolType: 242);
		Assert.Equal(100, column.ColumnSize);
	}

	[Fact]
	public void MySqlDbColumn_numeric_size_is_raw_column_length()
	{
		var column = CreateMySqlColumn(MySqlDbType.Int32, columnLength: 11);
		Assert.Equal(11, column.ColumnSize);
	}

	// ---------- DECIMAL 精度/小数位 ----------

	[Fact]
	public void MySqlDbColumn_decimal_precision_and_scale()
	{
		// 有符号 DECIMAL(10,2)：ColumnLength=10，Precision 减去符号位与小数点各 1 → 8
		var signed = CreateMySqlColumn(
			MySqlDbType.NewDecimal, protocolType: 246, columnLength: 10, decimals: 2);
		Assert.Equal(8, signed.NumericPrecision);
		Assert.Equal(2, signed.NumericScale);
	}

	[Fact]
	public void MySqlDbColumn_unsigned_decimal_keeps_sign_position()
	{
		// 无符号时不扣符号位 → 10-1 = 9
		var unsigned = CreateMySqlColumn(
			MySqlDbType.NewDecimal, protocolType: 246, columnLength: 10, decimals: 2,
			flags: ColumnFlags.Unsigned);
		Assert.Equal(9, unsigned.NumericPrecision);
		Assert.Equal(2, unsigned.NumericScale);
	}

	[Fact]
	public void MySqlDbColumn_decimal_without_scale_keeps_precision_minus_sign()
	{
		// decimals == 0 时不扣小数点 → 10-1 = 9
		var column = CreateMySqlColumn(
			MySqlDbType.NewDecimal, protocolType: 246, columnLength: 10, decimals: 0);
		Assert.Equal(9, column.NumericPrecision);
		Assert.Equal(0, column.NumericScale);
	}

	[Fact]
	public void MySqlDbColumn_non_decimal_has_no_numeric_precision()
	{
		var column = CreateMySqlColumn(MySqlDbType.Int32);
		Assert.Null(column.NumericPrecision);
	}

	// ---------- IsLong 判定 ----------

	[Fact]
	public void MySqlDbColumn_short_column_is_not_long()
	{
		// 列长 ≤ 255 时直接判为不长
		var column = CreateMySqlColumn(MySqlDbType.VarChar, columnLength: 255, protocolType: 253);
		Assert.False(column.IsLong);
	}

	[Fact]
	public void MySqlDbColumn_long_blob_flag_marks_long()
	{
		// 列长 > 255 且带 Blob 标志 → IsLong
		var column = CreateMySqlColumn(
			MySqlDbType.Blob, columnLength: 1000, protocolType: 252, flags: ColumnFlags.Blob);
		Assert.True(column.IsLong);
	}

	[Fact]
	public void MySqlDbColumn_long_text_type_marks_long_without_blob_flag()
	{
		// 协议类型落在 249..252（TINY/MEDIUM/LONG/BLOB）时同样判为长列
		foreach (var protocolType in new byte[] { 249, 250, 251, 252 })
		{
			var column = CreateMySqlColumn(
				MySqlDbType.LongText, columnLength: 1000, protocolType: protocolType);
			Assert.True(column.IsLong);
		}
	}

	[Fact]
	public void MySqlDbColumn_wide_but_not_lob_type_is_not_long()
	{
		// 列长 > 255 但既非 Blob 标志、协议类型也不在 249..252 → 不判为长
		var column = CreateMySqlColumn(MySqlDbType.VarChar, columnLength: 1000, protocolType: 253);
		Assert.False(column.IsLong);
	}

	[Fact]
	public void MySqlDbColumn_string_data_type_name_includes_length()
	{
		// MySqlDbType.String 时 DataTypeName 追加 (长度)
		var column = CreateMySqlColumn(
			MySqlDbType.String, columnLength: 40, protocolType: 254,
			characterSet: CharacterSet.Utf8Mb4GeneralCaseInsensitive);
		Assert.Equal("CHAR(10)", column.DataTypeName);
	}

	[Fact]
	public void MySqlDbColumn_expression_and_hidden_are_always_false()
	{
		var column = CreateMySqlColumn(MySqlDbType.Int32);
		Assert.False(column.IsExpression);
		Assert.False(column.IsHidden);
	}

	// ---------- OracleDbColumn ----------

	private static OracleDbColumn CreateOracleColumn(
		OracleDbType oracleDbType,
		ColumnFlags flags = (ColumnFlags)0,
		uint columnLength = 100,
		byte protocolType = 3,
		CharacterSet characterSet = CharacterSet.Utf8Mb4GeneralCaseInsensitive,
		byte decimals = 0,
		byte precision = 0,
		string name = "col",
		string physicalName = "col",
		bool allowZeroDateTime = false,
		int ordinal = 0)
	{
		var payload = CreateColumnDefinition(CreateColumnDefinitionBytes(
			name: name, physicalName: physicalName, characterSet: characterSet,
			columnLength: columnLength, protocolType: protocolType, flags: flags,
			decimals: decimals, precision: precision), isOracleMode: true);
		return new OracleDbColumn(ordinal, payload, allowZeroDateTime, oracleDbType);
	}

	[Fact]
	public void OracleDbColumn_maps_names_and_ordinal()
	{
		var column = CreateOracleColumn(OracleDbType.Varchar2, name: "c1", physicalName: "c1", ordinal: 2);
		Assert.Equal("c1", column.ColumnName);
		Assert.Equal("c1", column.BaseColumnName);
		Assert.Equal(2, column.ColumnOrdinal);
		Assert.Equal("t", column.TableName);
		Assert.Equal(OracleDbType.Varchar2, column.ProviderType);
	}

	[Fact]
	public void OracleDbColumn_nullable_and_flags()
	{
		Assert.True(CreateOracleColumn(OracleDbType.Varchar2).AllowDBNull);

		var full = CreateOracleColumn(
			OracleDbType.Varchar2,
			ColumnFlags.NotNull | ColumnFlags.PrimaryKey | ColumnFlags.UniqueKey | ColumnFlags.AutoIncrement);
		Assert.False(full.AllowDBNull);
		Assert.True(full.IsKey);
		Assert.True(full.IsUnique);
		Assert.True(full.IsAutoIncrement);
	}

	[Fact]
	public void OracleDbColumn_varchar2_data_type_name_uses_raw_column_length()
	{
		// 注意：Oracle 侧的 DataTypeName 用的是**原始列长**，不像 MySqlDbColumn 那样
		// 按字符集宽度换算成字符数（ColumnSize 同样保留原始列长）。
		var column = CreateOracleColumn(
			OracleDbType.Varchar2, columnLength: 40, protocolType: 253,
			characterSet: CharacterSet.Utf8Mb4GeneralCaseInsensitive);
		Assert.Equal("VARCHAR2(40)", column.DataTypeName);
		Assert.Equal(40, column.ColumnSize);
	}

	[Fact]
	public void OracleDbColumn_allowZeroDateTime_swaps_DateTime()
	{
		var plain = CreateOracleColumn(OracleDbType.Date, protocolType: 12);
		Assert.Equal(typeof(DateTime), plain.DataType);

		var zeroable = CreateOracleColumn(OracleDbType.Date, protocolType: 12, allowZeroDateTime: true);
		Assert.Equal(typeof(MySqlDateTime), zeroable.DataType);
	}

	[Fact]
	public void OracleDbColumn_decimal_uses_precision_when_provided()
	{
		// 有符号 NUMBER 且 Precision 明确：NumericPrecision = Precision，Scale = Decimals
		var column = CreateOracleColumn(
			OracleDbType.Decimal, protocolType: 246, precision: 10, decimals: 2);
		Assert.Equal(10, column.NumericPrecision);
		Assert.Equal(2, column.NumericScale);
	}

	[Fact]
	public void OracleDbColumn_decimal_without_precision_defaults_to_38()
	{
		// Precision == 0 → 38（Oracle NUMBER 的最大精度）
		var column = CreateOracleColumn(
			OracleDbType.Decimal, protocolType: 246, precision: 0, decimals: 0);
		Assert.Equal(38, column.NumericPrecision);
		Assert.Equal(0, column.NumericScale);
	}

	[Fact]
	public void OracleDbColumn_decimal_float_mode_derives_precision_from_binary_digits()
	{
		// 判定条件是 (sbyte)Decimals == -127，即字节值 129；此时按二进制位数换算十进制精度。
		// precision=53 → round(53 / 3.32193) = 16，且 NumericScale 固定为 1。
		var column = CreateOracleColumn(
			OracleDbType.Decimal, protocolType: 246, precision: 53, decimals: 129);
		Assert.Equal(16, column.NumericPrecision);
		Assert.Equal(1, column.NumericScale);
		// ColumnSize = 16 + (decimals != 0 ? 1 : 0) + 1 = 18
		Assert.Equal(18, column.ColumnSize);
	}

	[Fact]
	public void OracleDbColumn_decimal_decimals_171_is_normalized_to_zero()
	{
		// 字节值 171 是 Oracle NUMBER 的「未指定小数位」哨兵，Initialize 阶段会被归零，
		// 因此不会进入上面的浮点分支。
		var column = CreateOracleColumn(
			OracleDbType.Decimal, protocolType: 246, precision: 10, decimals: 171);
		Assert.Equal(10, column.NumericPrecision);
		Assert.Equal(0, column.NumericScale);
	}

	[Fact]
	public void OracleDbColumn_decimal_column_size_adds_sign_and_point()
	{
		// num2 = precision + (decimals != 0 ? 1 : 0) + 1 → 10 + 1 + 1 = 12
		var column = CreateOracleColumn(
			OracleDbType.Decimal, protocolType: 246, precision: 10, decimals: 2);
		Assert.Equal(12, column.ColumnSize);
	}

	[Fact]
	public void OracleDbColumn_non_decimal_column_size_is_raw_length()
	{
		var column = CreateOracleColumn(OracleDbType.Varchar2, columnLength: 123, protocolType: 253);
		Assert.Equal(123, column.ColumnSize);
	}

	[Fact]
	public void OracleDbColumn_nchar_precision_divides_by_bytes_per_character()
	{
		// OracleNChar(208) 落在 207..208 区间 → 精度按字符数算
		var column = CreateOracleColumn(
			OracleDbType.NChar, columnLength: 40, protocolType: 208, decimals: 0,
			characterSet: CharacterSet.Utf8Mb4GeneralCaseInsensitive);
		Assert.Equal(10, column.NumericPrecision);
	}

	[Fact]
	public void OracleDbColumn_numeric_types_get_precision_from_column_length()
	{
		// Tiny/Short/Longlong/Int24/Bit 等走「长度 + PrecisionAdjustFactor」分支
		var column = CreateOracleColumn(OracleDbType.Int16, columnLength: 5, protocolType: 2, decimals: 0);
		Assert.Equal(5, column.NumericPrecision);
		Assert.Equal(0, column.NumericScale);
	}

	[Fact]
	public void OracleDbColumn_numeric_precision_reduced_when_scaled()
	{
		// Decimals > 0 时精度再减 1（小数点占位）
		var column = CreateOracleColumn(OracleDbType.Int16, columnLength: 5, protocolType: 2, decimals: 3);
		Assert.Equal(4, column.NumericPrecision);
		Assert.Equal(3, column.NumericScale);
	}

	[Fact]
	public void OracleDbColumn_plain_string_type_gets_length_precision()
	{
		// 非 207..208 的字符类型：精度取原始列长
		var column = CreateOracleColumn(
			OracleDbType.Char, columnLength: 7, protocolType: 254, decimals: 0,
			characterSet: CharacterSet.Utf8Mb4GeneralCaseInsensitive);
		Assert.Equal(7, column.NumericPrecision);
	}

	[Fact]
	public void OracleDbColumn_long_flag_follows_same_rules_as_my_sql()
	{
		Assert.False(CreateOracleColumn(OracleDbType.Varchar2, columnLength: 255, protocolType: 253).IsLong);
		Assert.True(CreateOracleColumn(
			OracleDbType.Blob, columnLength: 1000, protocolType: 210, flags: ColumnFlags.Blob).IsLong);
	}
}
