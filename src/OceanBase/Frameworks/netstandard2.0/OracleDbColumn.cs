using System;
using System.Data.Common;
using System.Globalization;
using OceanBase.Core;
using OceanBase.Protocol;
using OceanBase.Protocol.Payloads;
using OceanBase.Protocol.Serialization;

namespace OceanBase;

public sealed class OracleDbColumn : DbColumn
{
	public OracleDbType ProviderType { get; }

	/// <summary>
	/// Gets the name of the table that the column belongs to. This will be the alias if the table is aliased in the query.
	/// </summary>
	public string TableName { get; }

	internal OracleDbColumn(int ordinal, ColumnDefinitionPayload column, bool allowZeroDateTime, OracleDbType oracleDbType)
	{
		OracleColumnTypeMetadata columnTypeMetadata = TypeMapper.Instance.GetColumnTypeMetadata(oracleDbType);
		Type clrType = columnTypeMetadata.DbTypeMapping.ClrType;
		int? num = null;
		int? numericScale = null;
		int num2 = 0;
		ColumnType columnType = column.ColumnType;
		if ((columnType == ColumnType.Decimal || columnType == ColumnType.NewDecimal) ? true : false)
		{
			int precision = column.Precision;
			sbyte b = (sbyte)column.Decimals;
			if (precision != 0 && b == -127)
			{
				num = Math.Max(1, (int)Math.Round((double)precision / 3.32193, MidpointRounding.AwayFromZero));
				numericScale = 1;
			}
			else
			{
				num = ((precision == 0) ? 38 : precision);
				numericScale = ((b != -127) ? Math.Max(0, (int)b) : 0);
			}
			num2 = (num + ((b != 0) ? 1 : 0) + 1).Value;
		}
		else
		{
			num2 = (int)column.ColumnLength;
		}
		base.AllowDBNull = (column.ColumnFlags & ColumnFlags.NotNull) == 0;
		base.BaseCatalogName = null;
		base.BaseColumnName = column.PhysicalName;
		base.BaseSchemaName = column.SchemaName;
		base.BaseTableName = column.PhysicalTable;
		base.ColumnName = column.Name;
		base.ColumnOrdinal = ordinal;
		base.ColumnSize = ((num2 > int.MaxValue) ? int.MaxValue : num2);
		base.DataType = ((allowZeroDateTime && clrType == typeof(DateTime)) ? typeof(MySqlDateTime) : clrType);
		base.DataTypeName = columnTypeMetadata.SimpleDataTypeName;
		if (oracleDbType == OracleDbType.Varchar2)
		{
			base.DataTypeName += string.Format(CultureInfo.InvariantCulture, "({0})", num2);
		}
		base.IsAliased = column.PhysicalName != column.Name;
		base.IsAutoIncrement = (column.ColumnFlags & ColumnFlags.AutoIncrement) != 0;
		base.IsExpression = false;
		base.IsHidden = false;
		base.IsKey = (column.ColumnFlags & ColumnFlags.PrimaryKey) != 0;
		bool flag = column.ColumnLength > 255;
		if (flag)
		{
			bool flag2 = (column.ColumnFlags & ColumnFlags.Blob) != 0;
			if (!flag2)
			{
				columnType = column.ColumnType;
				flag2 = (uint)(columnType - 249) <= 3u;
			}
			flag = flag2;
		}
		base.IsLong = flag;
		base.IsReadOnly = false;
		base.IsUnique = (column.ColumnFlags & ColumnFlags.UniqueKey) != 0;
		if (num.HasValue || numericScale.HasValue)
		{
			base.NumericPrecision = num;
			base.NumericScale = numericScale;
		}
		else
		{
			switch (column.ColumnType)
			{
			case ColumnType.Tiny:
			case ColumnType.Short:
			case ColumnType.Longlong:
			case ColumnType.Int24:
			case ColumnType.Bit:
			case ColumnType.OracleNumberFloat:
			case ColumnType.OracleBinaryFloat:
			case ColumnType.OracleBinaryDouble:
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				base.NumericPrecision = (int)column.ColumnLength + column.PrecisionAdjustFactor;
				if (column.Decimals > 0)
				{
					base.NumericPrecision--;
				}
				base.NumericScale = column.Decimals;
			}
			else
			{
				columnType = column.ColumnType;
				if ((uint)(columnType - 207) <= 1u)
				{
					base.NumericPrecision = (int)column.ColumnLength / ProtocolUtility.GetBytesPerCharacter(column.CharacterSet);
					base.NumericScale = column.Decimals;
				}
				else
				{
					base.NumericPrecision = (int)column.ColumnLength;
					base.NumericScale = column.Decimals;
				}
			}
		}
		ProviderType = oracleDbType;
		TableName = column.Table;
	}
}
