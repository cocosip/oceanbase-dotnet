using System;
using System.Data.Common;
using System.Globalization;
using OceanBase.Core;
using OceanBase.Protocol;
using OceanBase.Protocol.Payloads;
using OceanBase.Protocol.Serialization;

namespace OceanBase;

public sealed class MySqlDbColumn : DbColumn
{
	public MySqlDbType ProviderType { get; }

	/// <summary>
	/// Gets the name of the table that the column belongs to. This will be the alias if the table is aliased in the query.
	/// </summary>
	public string TableName { get; }

	internal MySqlDbColumn(int ordinal, ColumnDefinitionPayload column, bool allowZeroDateTime, MySqlDbType mySqlDbType)
	{
		ColumnTypeMetadata columnTypeMetadata = TypeMapper.Instance.GetColumnTypeMetadata(mySqlDbType);
		Type clrType = columnTypeMetadata.DbTypeMapping.ClrType;
		long num = ((clrType == typeof(string) || clrType == typeof(Guid)) ? (column.ColumnLength / ProtocolUtility.GetBytesPerCharacter(column.CharacterSet)) : ((column.ColumnType == ColumnType.Vector) ? (column.ColumnLength / 4) : column.ColumnLength));
		base.AllowDBNull = (column.ColumnFlags & ColumnFlags.NotNull) == 0;
		base.BaseCatalogName = null;
		base.BaseColumnName = column.PhysicalName;
		base.BaseSchemaName = column.SchemaName;
		base.BaseTableName = column.PhysicalTable;
		base.ColumnName = column.Name;
		base.ColumnOrdinal = ordinal;
		base.ColumnSize = (int)((num > int.MaxValue) ? int.MaxValue : num);
		base.DataType = ((allowZeroDateTime && clrType == typeof(DateTime)) ? typeof(MySqlDateTime) : clrType);
		base.DataTypeName = columnTypeMetadata.SimpleDataTypeName;
		if (mySqlDbType == MySqlDbType.String)
		{
			base.DataTypeName += string.Format(CultureInfo.InvariantCulture, "({0})", num);
		}
		base.IsAliased = column.PhysicalName != column.Name;
		base.IsAutoIncrement = (column.ColumnFlags & ColumnFlags.AutoIncrement) != 0;
		base.IsExpression = false;
		base.IsHidden = false;
		base.IsKey = (column.ColumnFlags & ColumnFlags.PrimaryKey) != 0;
		bool flag = column.ColumnLength > 255;
		ColumnType columnType;
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
		columnType = column.ColumnType;
		if ((columnType == ColumnType.Decimal || columnType == ColumnType.NewDecimal) ? true : false)
		{
			base.NumericPrecision = (int)column.ColumnLength;
			if ((column.ColumnFlags & ColumnFlags.Unsigned) == 0)
			{
				base.NumericPrecision--;
			}
			if (column.Decimals > 0)
			{
				base.NumericPrecision--;
			}
		}
		base.NumericScale = column.Decimals;
		ProviderType = mySqlDbType;
		TableName = column.Table;
	}
}
