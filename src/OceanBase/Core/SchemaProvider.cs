using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Core;

internal sealed class SchemaProvider(MySqlConnection connection)
{
	private void DoFillDataSourceInformation(DataTable dataTable)
	{
		DataRow dataRow = dataTable.NewRow();
		dataRow["CompositeIdentifierSeparatorPattern"] = "\\.";
		dataRow["DataSourceProductName"] = "MySQL";
		dataRow["DataSourceProductVersion"] = connection.ServerVersion;
		dataRow["DataSourceProductVersionNormalized"] = GetVersion(connection.Session.ServerVersion.Version);
		dataRow["GroupByBehavior"] = GroupByBehavior.Unrelated;
		dataRow["IdentifierPattern"] = "(^\\[\\p{Lo}\\p{Lu}\\p{Ll}_@#][\\p{Lo}\\p{Lu}\\p{Ll}\\p{Nd}@$#_]*$)|(^\\[[^\\]\\0]|\\]\\]+\\]$)|(^\\\"[^\\\"\\0]|\\\"\\\"+\\\"$)";
		dataRow["IdentifierCase"] = IdentifierCase.Insensitive;
		dataRow["OrderByColumnsInSelect"] = false;
		dataRow["ParameterMarkerFormat"] = "{0}";
		dataRow["ParameterMarkerPattern"] = "(@[A-Za-z0-9_$#]*)";
		dataRow["ParameterNameMaxLength"] = 128;
		dataRow["QuotedIdentifierPattern"] = "(([^\\`]|\\`\\`)*)";
		dataRow["QuotedIdentifierCase"] = IdentifierCase.Sensitive;
		dataRow["ParameterNamePattern"] = "^[\\p{Lo}\\p{Lu}\\p{Ll}\\p{Lm}_@#][\\p{Lo}\\p{Lu}\\p{Ll}\\p{Lm}\\p{Nd}\\uff3f_@#\\$]*(?=\\s+|$)";
		dataRow["StatementSeparatorPattern"] = ";";
		dataRow["StringLiteralPattern"] = "'(([^']|'')*)'";
		dataRow["SupportedJoinOperators"] = SupportedJoinOperators.Inner | SupportedJoinOperators.LeftOuter | SupportedJoinOperators.RightOuter | SupportedJoinOperators.FullOuter;
		dataTable.Rows.Add(dataRow);
		static string GetVersion(Version v)
		{
			IFormatProvider invariantCulture = CultureInfo.InvariantCulture;
			IFormatProvider provider = invariantCulture;
			Span<char> initialBuffer = stackalloc char[10];
			DefaultInterpolatedStringHandler handler = new DefaultInterpolatedStringHandler(2, 3, invariantCulture, initialBuffer);
			handler.AppendFormatted(v.Major, "00");
			handler.AppendLiteral(".");
			handler.AppendFormatted(v.Minor, "00");
			handler.AppendLiteral(".");
			handler.AppendFormatted(v.Build, "0000");
			return string.Create(provider, initialBuffer, ref handler);
		}
	}

	private static void DoFillDataTypes(DataTable dataTable)
	{
		HashSet<string> hashSet = new HashSet<string>();
		foreach (ColumnTypeMetadata columnTypeMetadatum in TypeMapper.Instance.GetColumnTypeMetadata())
		{
			MySqlDbType mySqlDbType = columnTypeMetadatum.MySqlDbType;
			bool flag = ((mySqlDbType == MySqlDbType.Decimal || mySqlDbType == MySqlDbType.Null || (uint)(mySqlDbType - 14) <= 1u) ? true : false);
			if (flag || (columnTypeMetadatum != null && columnTypeMetadatum.MySqlDbType == MySqlDbType.Bool && columnTypeMetadatum.IsUnsigned))
			{
				continue;
			}
			Type clrType = columnTypeMetadatum.DbTypeMapping.ClrType;
			string text = clrType.ToString();
			MySqlDbType mySqlDbType2 = columnTypeMetadatum.MySqlDbType;
			string text2 = mySqlDbType2 switch
			{
				MySqlDbType.Bool => "BOOL", 
				MySqlDbType.Guid => "GUID", 
				_ => columnTypeMetadatum.DataTypeName, 
			};
			switch (mySqlDbType2)
			{
			case MySqlDbType.Byte:
			case MySqlDbType.Int16:
			case MySqlDbType.Int32:
			case MySqlDbType.Int64:
			case MySqlDbType.Int24:
			case MySqlDbType.UByte:
			case MySqlDbType.UInt16:
			case MySqlDbType.UInt32:
			case MySqlDbType.UInt64:
			case MySqlDbType.UInt24:
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			bool flag2 = flag;
			bool flag3 = hashSet.Add(text);
			flag = flag2;
			if (!flag)
			{
				bool flag4;
				switch (mySqlDbType2)
				{
				case MySqlDbType.Bool:
				case MySqlDbType.Float:
				case MySqlDbType.Double:
				case MySqlDbType.Timestamp:
				case MySqlDbType.Date:
				case MySqlDbType.Time:
				case MySqlDbType.DateTime:
				case MySqlDbType.Year:
				case MySqlDbType.Guid:
					flag4 = true;
					break;
				default:
					flag4 = false;
					break;
				}
				flag = flag4;
			}
			bool flag5 = flag;
			flag = flag5;
			if (!flag)
			{
				bool flag4 = ((mySqlDbType2 == MySqlDbType.Bit || mySqlDbType2 == MySqlDbType.NewDecimal) ? true : false);
				flag = flag4;
			}
			bool flag6 = flag;
			flag = (uint)(mySqlDbType2 - 250) <= 2u;
			bool flag7 = flag;
			string[] array = columnTypeMetadatum.CreateFormat.Split(';');
			dataTable.Rows.Add(text2, (int)mySqlDbType2, columnTypeMetadatum.ColumnSize, array[0], (array.Length == 1) ? null : array[1], text, flag2, flag3, false, flag5, flag6, flag7, true, clrType != typeof(byte[]), clrType == typeof(string), columnTypeMetadatum.IsUnsigned, DBNull.Value, DBNull.Value, DBNull.Value, true, DBNull.Value, DBNull.Value, null);
		}
	}

	private static void DoFillReservedWords(DataTable dataTable)
	{
		string[] array = new string[262]
		{
			"ACCESSIBLE", "ADD", "ALL", "ALTER", "ANALYZE", "AND", "AS", "ASC", "ASENSITIVE", "BEFORE",
			"BETWEEN", "BIGINT", "BINARY", "BLOB", "BOTH", "BY", "CALL", "CASCADE", "CASE", "CHANGE",
			"CHAR", "CHARACTER", "CHECK", "COLLATE", "COLUMN", "CONDITION", "CONSTRAINT", "CONTINUE", "CONVERT", "CREATE",
			"CROSS", "CUBE", "CUME_DIST", "CURRENT_DATE", "CURRENT_TIME", "CURRENT_TIMESTAMP", "CURRENT_USER", "CURSOR", "DATABASE", "DATABASES",
			"DAY_HOUR", "DAY_MICROSECOND", "DAY_MINUTE", "DAY_SECOND", "DEC", "DECIMAL", "DECLARE", "DEFAULT", "DELAYED", "DELETE",
			"DENSE_RANK", "DESC", "DESCRIBE", "DETERMINISTIC", "DISTINCT", "DISTINCTROW", "DIV", "DOUBLE", "DROP", "DUAL",
			"EACH", "ELSE", "ELSEIF", "EMPTY", "ENCLOSED", "ESCAPED", "EXCEPT", "EXISTS", "EXIT", "EXPLAIN",
			"FALSE", "FETCH", "FIRST_VALUE", "FLOAT", "FLOAT4", "FLOAT8", "FOR", "FORCE", "FOREIGN", "FROM",
			"FULLTEXT", "FUNCTION", "GENERATED", "GET", "GRANT", "GROUP", "GROUPING", "GROUPS", "HAVING", "HIGH_PRIORITY",
			"HOUR_MICROSECOND", "HOUR_MINUTE", "HOUR_SECOND", "IF", "IGNORE", "IN", "INDEX", "INFILE", "INNER", "INOUT",
			"INSENSITIVE", "INSERT", "INT", "INT1", "INT2", "INT3", "INT4", "INT8", "INTEGER", "INTERVAL",
			"INTO", "IO_AFTER_GTIDS", "IO_BEFORE_GTIDS", "IS", "ITERATE", "JOIN", "JSON_TABLE", "KEY", "KEYS", "KILL",
			"LAG", "LAST_VALUE", "LATERAL", "LEAD", "LEADING", "LEAVE", "LEFT", "LIKE", "LIMIT", "LINEAR",
			"LINES", "LOAD", "LOCALTIME", "LOCALTIMESTAMP", "LOCK", "LONG", "LONGBLOB", "LONGTEXT", "LOOP", "LOW_PRIORITY",
			"MASTER_BIND", "MASTER_SSL_VERIFY_SERVER_CERT", "MATCH", "MAXVALUE", "MEDIUMBLOB", "MEDIUMINT", "MEDIUMTEXT", "MEMBER", "MIDDLEINT", "MINUTE_MICROSECOND",
			"MINUTE_SECOND", "MOD", "MODIFIES", "NATURAL", "NOT", "NO_WRITE_TO_BINLOG", "NTH_VALUE", "NTILE", "NULL", "NUMERIC",
			"OF", "ON", "OPTIMIZE", "OPTIMIZER_COSTS", "OPTION", "OPTIONALLY", "OR", "ORDER", "OUT", "OUTER",
			"OUTFILE", "OVER", "PARTITION", "PERCENT_RANK", "PRECISION", "PRIMARY", "PROCEDURE", "PURGE", "RANGE", "RANK",
			"READ", "READS", "READ_WRITE", "REAL", "RECURSIVE", "REFERENCES", "REGEXP", "RELEASE", "RENAME", "REPEAT",
			"REPLACE", "REQUIRE", "RESIGNAL", "RESTRICT", "RETURN", "REVOKE", "RIGHT", "RLIKE", "ROW", "ROWS",
			"ROW_NUMBER", "SCHEMA", "SCHEMAS", "SECOND_MICROSECOND", "SELECT", "SENSITIVE", "SEPARATOR", "SET", "SHOW", "SIGNAL",
			"SMALLINT", "SPATIAL", "SPECIFIC", "SQL", "SQLEXCEPTION", "SQLSTATE", "SQLWARNING", "SQL_BIG_RESULT", "SQL_CALC_FOUND_ROWS", "SQL_SMALL_RESULT",
			"SSL", "STARTING", "STORED", "STRAIGHT_JOIN", "SYSTEM", "TABLE", "TERMINATED", "THEN", "TINYBLOB", "TINYINT",
			"TINYTEXT", "TO", "TRAILING", "TRIGGER", "TRUE", "UNDO", "UNION", "UNIQUE", "UNLOCK", "UNSIGNED",
			"UPDATE", "USAGE", "USE", "USING", "UTC_DATE", "UTC_TIME", "UTC_TIMESTAMP", "VALUES", "VARBINARY", "VARCHAR",
			"VARCHARACTER", "VARYING", "VIRTUAL", "WHEN", "WHERE", "WHILE", "WINDOW", "WITH", "WRITE", "XOR",
			"YEAR_MONTH", "ZEROFILL"
		};
		foreach (string text in array)
		{
			dataTable.Rows.Add(text);
		}
	}

	private async Task FillDataTableAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, List<KeyValuePair<string, string>>? columns, CancellationToken cancellationToken)
	{
		await FillDataTableAsync(ioBehavior, dataTable, delegate(MySqlCommand command)
		{
			command.CommandText = "SELECT " + string.Join(", ", from DataColumn x in dataTable.Columns
				select x.ColumnName) + " FROM INFORMATION_SCHEMA." + tableName;
			if (columns != null && columns.Count > 0)
			{
				command.CommandText = command.CommandText + " WHERE " + string.Join(" AND ", columns.Select<KeyValuePair<string, string>, string>((KeyValuePair<string, string> x) => x.Key + " = @" + x.Key));
				foreach (KeyValuePair<string, string> column in columns)
				{
					command.Parameters.AddWithValue("@" + column.Key, column.Value);
				}
			}
		}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillDataTableAsync(IOBehavior ioBehavior, DataTable dataTable, Action<MySqlCommand> configureCommand, CancellationToken cancellationToken)
	{
		Action close = null;
		if (connection.State != ConnectionState.Open)
		{
			await connection.OpenAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			close = connection.Close;
		}
		if (dataTable.TableName == "Columns")
		{
			using (MySqlCommand generationExpressionCommand = new MySqlCommand("SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE table_schema = 'information_schema' AND table_name = 'COLUMNS' AND column_name = 'GENERATION_EXPRESSION';", connection))
			{
				if (await generationExpressionCommand.ExecuteScalarAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false) == null)
				{
					dataTable.Columns.Remove("GENERATION_EXPRESSION");
				}
			}
			using MySqlCommand srsIdCommand = new MySqlCommand("SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE table_schema = 'information_schema' AND table_name = 'COLUMNS' AND column_name = 'SRS_ID';", connection);
			if (await srsIdCommand.ExecuteScalarAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false) == null)
			{
				dataTable.Columns.Remove("SRS_ID");
			}
		}
		using (MySqlCommand command = connection.CreateCommand())
		{
			configureCommand(command);
			using MySqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.Default, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			while (await reader.ReadAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
			{
				object[] values = new object[dataTable.Columns.Count];
				reader.GetValues(values);
				dataTable.Rows.Add(values);
			}
		}
		close?.Invoke();
	}

	private Task DoFillForeignKeysAsync(IOBehavior ioBehavior, DataTable dataTable, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		return FillDataTableAsync(ioBehavior, dataTable, delegate(MySqlCommand command)
		{
			command.CommandText = "SELECT rc.constraint_catalog, rc.constraint_schema, rc.constraint_name,\n\tkcu.table_catalog, kcu.table_schema,\n\trc.table_name, rc.match_option, rc.update_rule, rc.delete_rule,\n\tNULL as referenced_table_catalog, kcu.referenced_table_schema, rc.referenced_table_name\nFROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc\nLEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu\n\tON kcu.constraint_catalog = rc.constraint_catalog\n\tAND kcu.constraint_schema = rc.constraint_schema\n\tAND kcu.table_name = rc.table_name\n\tAND kcu.constraint_name = rc.constraint_name\nWHERE kcu.ORDINAL_POSITION = 1";
			if (restrictionValues != null && restrictionValues.Length >= 2)
			{
				string text = restrictionValues[1];
				if (text != null && text.Length > 0)
				{
					command.CommandText += " AND rc.constraint_schema LIKE @schema";
					command.Parameters.AddWithValue("@schema", text);
				}
			}
			if (restrictionValues != null && restrictionValues.Length >= 3)
			{
				string text2 = restrictionValues[2];
				if (text2 != null && text2.Length > 0)
				{
					command.CommandText += " AND rc.table_name LIKE @table";
					command.Parameters.AddWithValue("@table", text2);
				}
			}
			if (restrictionValues != null && restrictionValues.Length >= 4)
			{
				string text3 = restrictionValues[3];
				if (text3 != null && text3.Length > 0)
				{
					command.CommandText += " AND rc.constraint_name LIKE @constraint";
					command.Parameters.AddWithValue("@constraint", text3);
				}
			}
		}, cancellationToken);
	}

	private Task DoFillIndexesAsync(IOBehavior ioBehavior, DataTable dataTable, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		return FillDataTableAsync(ioBehavior, dataTable, delegate(MySqlCommand command)
		{
			command.CommandText = "SELECT null AS INDEX_CATALOG, INDEX_SCHEMA,\n\tINDEX_NAME, TABLE_NAME,\n\t!NON_UNIQUE as `UNIQUE`,\n\tINDEX_NAME='PRIMARY' as `PRIMARY`,\n\tINDEX_TYPE as TYPE, COMMENT\nFROM INFORMATION_SCHEMA.STATISTICS\nWHERE SEQ_IN_INDEX=1";
			if (restrictionValues != null && restrictionValues.Length >= 2)
			{
				string text = restrictionValues[1];
				if (text != null && text.Length > 0)
				{
					command.CommandText += " AND INDEX_SCHEMA LIKE @schema";
					command.Parameters.AddWithValue("@schema", text);
				}
			}
			if (restrictionValues != null && restrictionValues.Length >= 3)
			{
				string text2 = restrictionValues[2];
				if (text2 != null && text2.Length > 0)
				{
					command.CommandText += " AND TABLE_NAME LIKE @table";
					command.Parameters.AddWithValue("@table", text2);
				}
			}
			if (restrictionValues != null && restrictionValues.Length >= 4)
			{
				string text3 = restrictionValues[3];
				if (text3 != null && text3.Length > 0)
				{
					command.CommandText += " AND INDEX_NAME LIKE @index";
					command.Parameters.AddWithValue("@index", text3);
				}
			}
		}, cancellationToken);
	}

	private Task DoFillIndexColumnsAsync(IOBehavior ioBehavior, DataTable dataTable, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		return FillDataTableAsync(ioBehavior, dataTable, delegate(MySqlCommand command)
		{
			command.CommandText = "SELECT null AS INDEX_CATALOG, INDEX_SCHEMA,\n\tINDEX_NAME, TABLE_NAME,\n\tCOLUMN_NAME,\n\tSEQ_IN_INDEX as `ORDINAL_POSITION`,\n\tCOLLATION as SORT_ORDER\nFROM INFORMATION_SCHEMA.STATISTICS\nWHERE 1=1";
			if (restrictionValues != null && restrictionValues.Length >= 2)
			{
				string text = restrictionValues[1];
				if (text != null && text.Length > 0)
				{
					command.CommandText += " AND INDEX_SCHEMA LIKE @schema";
					command.Parameters.AddWithValue("@schema", text);
				}
			}
			if (restrictionValues != null && restrictionValues.Length >= 3)
			{
				string text2 = restrictionValues[2];
				if (text2 != null && text2.Length > 0)
				{
					command.CommandText += " AND TABLE_NAME LIKE @table";
					command.Parameters.AddWithValue("@table", text2);
				}
			}
			if (restrictionValues != null && restrictionValues.Length >= 4)
			{
				string text3 = restrictionValues[3];
				if (text3 != null && text3.Length > 0)
				{
					command.CommandText += " AND INDEX_NAME LIKE @index";
					command.Parameters.AddWithValue("@index", text3);
				}
			}
			if (restrictionValues != null && restrictionValues.Length >= 5)
			{
				string text4 = restrictionValues[4];
				if (text4 != null && text4.Length > 0)
				{
					command.CommandText += " AND COLUMN_NAME LIKE @column";
					command.Parameters.AddWithValue("@column", text4);
				}
			}
		}, cancellationToken);
	}

	public async ValueTask<DataTable> GetSchemaAsync(IOBehavior ioBehavior, string collectionName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(collectionName, "collectionName");
		DataTable dataTable = new DataTable();
		if (string.Equals(collectionName, "MetaDataCollections", StringComparison.OrdinalIgnoreCase))
		{
			await FillMetaDataCollectionsAsync(ioBehavior, dataTable, "MetaDataCollections", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "CharacterSets", StringComparison.OrdinalIgnoreCase))
		{
			await FillCharacterSetsAsync(ioBehavior, dataTable, "CharacterSets", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "Collations", StringComparison.OrdinalIgnoreCase))
		{
			await FillCollationsAsync(ioBehavior, dataTable, "Collations", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "CollationCharacterSetApplicability", StringComparison.OrdinalIgnoreCase))
		{
			await FillCollationCharacterSetApplicabilityAsync(ioBehavior, dataTable, "CollationCharacterSetApplicability", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "Columns", StringComparison.OrdinalIgnoreCase))
		{
			await FillColumnsAsync(ioBehavior, dataTable, "Columns", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "Databases", StringComparison.OrdinalIgnoreCase))
		{
			await FillDatabasesAsync(ioBehavior, dataTable, "Databases", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "DataSourceInformation", StringComparison.OrdinalIgnoreCase))
		{
			await FillDataSourceInformationAsync(ioBehavior, dataTable, "DataSourceInformation", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "DataTypes", StringComparison.OrdinalIgnoreCase))
		{
			await FillDataTypesAsync(ioBehavior, dataTable, "DataTypes", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "Engines", StringComparison.OrdinalIgnoreCase))
		{
			await FillEnginesAsync(ioBehavior, dataTable, "Engines", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "KeyColumnUsage", StringComparison.OrdinalIgnoreCase))
		{
			await FillKeyColumnUsageAsync(ioBehavior, dataTable, "KeyColumnUsage", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "KeyWords", StringComparison.OrdinalIgnoreCase))
		{
			await FillKeyWordsAsync(ioBehavior, dataTable, "KeyWords", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "Parameters", StringComparison.OrdinalIgnoreCase))
		{
			await FillParametersAsync(ioBehavior, dataTable, "Parameters", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "Partitions", StringComparison.OrdinalIgnoreCase))
		{
			await FillPartitionsAsync(ioBehavior, dataTable, "Partitions", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "Plugins", StringComparison.OrdinalIgnoreCase))
		{
			await FillPluginsAsync(ioBehavior, dataTable, "Plugins", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "Procedures", StringComparison.OrdinalIgnoreCase))
		{
			await FillProceduresAsync(ioBehavior, dataTable, "Procedures", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "ProcessList", StringComparison.OrdinalIgnoreCase))
		{
			await FillProcessListAsync(ioBehavior, dataTable, "ProcessList", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "Profiling", StringComparison.OrdinalIgnoreCase))
		{
			await FillProfilingAsync(ioBehavior, dataTable, "Profiling", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "ReferentialConstraints", StringComparison.OrdinalIgnoreCase))
		{
			await FillReferentialConstraintsAsync(ioBehavior, dataTable, "ReferentialConstraints", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "ReservedWords", StringComparison.OrdinalIgnoreCase))
		{
			await FillReservedWordsAsync(ioBehavior, dataTable, "ReservedWords", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "ResourceGroups", StringComparison.OrdinalIgnoreCase))
		{
			await FillResourceGroupsAsync(ioBehavior, dataTable, "ResourceGroups", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "Restrictions", StringComparison.OrdinalIgnoreCase))
		{
			await FillRestrictionsAsync(ioBehavior, dataTable, "Restrictions", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "SchemaPrivileges", StringComparison.OrdinalIgnoreCase))
		{
			await FillSchemaPrivilegesAsync(ioBehavior, dataTable, "SchemaPrivileges", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "Tables", StringComparison.OrdinalIgnoreCase))
		{
			await FillTablesAsync(ioBehavior, dataTable, "Tables", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "TableConstraints", StringComparison.OrdinalIgnoreCase))
		{
			await FillTableConstraintsAsync(ioBehavior, dataTable, "TableConstraints", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "TablePrivileges", StringComparison.OrdinalIgnoreCase))
		{
			await FillTablePrivilegesAsync(ioBehavior, dataTable, "TablePrivileges", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "Triggers", StringComparison.OrdinalIgnoreCase))
		{
			await FillTriggersAsync(ioBehavior, dataTable, "Triggers", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "UserPrivileges", StringComparison.OrdinalIgnoreCase))
		{
			await FillUserPrivilegesAsync(ioBehavior, dataTable, "UserPrivileges", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "Views", StringComparison.OrdinalIgnoreCase))
		{
			await FillViewsAsync(ioBehavior, dataTable, "Views", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "Foreign Keys", StringComparison.OrdinalIgnoreCase))
		{
			await FillForeignKeysAsync(ioBehavior, dataTable, "Foreign Keys", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (string.Equals(collectionName, "Indexes", StringComparison.OrdinalIgnoreCase))
		{
			await FillIndexesAsync(ioBehavior, dataTable, "Indexes", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else
		{
			if (!string.Equals(collectionName, "IndexColumns", StringComparison.OrdinalIgnoreCase))
			{
				throw new ArgumentException("Invalid collection name: '" + collectionName + "'.", "collectionName");
			}
			await FillIndexColumnsAsync(ioBehavior, dataTable, "IndexColumns", restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		return dataTable;
	}

	private Task FillMetaDataCollectionsAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'MetaDataCollections'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[3]
		{
			new DataColumn("CollectionName", typeof(string)),
			new DataColumn("NumberOfRestrictions", typeof(int)),
			new DataColumn("NumberOfIdentifierParts", typeof(int))
		});
		dataTable.Rows.Add("MetaDataCollections", 0, 0);
		dataTable.Rows.Add("CharacterSets", 0, 0);
		dataTable.Rows.Add("Collations", 0, 0);
		dataTable.Rows.Add("CollationCharacterSetApplicability", 0, 0);
		dataTable.Rows.Add("Columns", 4, 4);
		dataTable.Rows.Add("Databases", 0, 2);
		dataTable.Rows.Add("DataSourceInformation", 0, 0);
		dataTable.Rows.Add("DataTypes", 0, 0);
		dataTable.Rows.Add("Engines", 0, 0);
		dataTable.Rows.Add("KeyColumnUsage", 0, 0);
		dataTable.Rows.Add("KeyWords", 0, 0);
		dataTable.Rows.Add("Parameters", 0, 0);
		dataTable.Rows.Add("Partitions", 0, 0);
		dataTable.Rows.Add("Plugins", 0, 0);
		dataTable.Rows.Add("Procedures", 0, 3);
		dataTable.Rows.Add("ProcessList", 0, 0);
		dataTable.Rows.Add("Profiling", 0, 0);
		dataTable.Rows.Add("ReferentialConstraints", 0, 3);
		dataTable.Rows.Add("ReservedWords", 0, 0);
		dataTable.Rows.Add("ResourceGroups", 0, 0);
		dataTable.Rows.Add("Restrictions", 0, 0);
		dataTable.Rows.Add("SchemaPrivileges", 0, 0);
		dataTable.Rows.Add("Tables", 4, 3);
		dataTable.Rows.Add("TableConstraints", 0, 3);
		dataTable.Rows.Add("TablePrivileges", 0, 0);
		dataTable.Rows.Add("Triggers", 0, 3);
		dataTable.Rows.Add("UserPrivileges", 0, 0);
		dataTable.Rows.Add("Views", 0, 3);
		dataTable.Rows.Add("Foreign Keys", 4, 0);
		dataTable.Rows.Add("Indexes", 4, 0);
		dataTable.Rows.Add("IndexColumns", 5, 0);
		return Task.CompletedTask;
	}

	private async Task FillCharacterSetsAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'CharacterSets'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[4]
		{
			new DataColumn("CHARACTER_SET_NAME", typeof(string)),
			new DataColumn("DEFAULT_COLLATE_NAME", typeof(string)),
			new DataColumn("DESCRIPTION", typeof(string)),
			new DataColumn("MAXLEN", typeof(int))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "CHARACTER_SETS", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillCollationsAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'Collations'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[6]
		{
			new DataColumn("COLLATION_NAME", typeof(string)),
			new DataColumn("CHARACTER_SET_NAME", typeof(string)),
			new DataColumn("ID", typeof(int)),
			new DataColumn("IS_DEFAULT", typeof(string)),
			new DataColumn("IS_COMPILED", typeof(string)),
			new DataColumn("SORTLEN", typeof(int))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "COLLATIONS", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillCollationCharacterSetApplicabilityAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'CollationCharacterSetApplicability'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[2]
		{
			new DataColumn("COLLATION_NAME", typeof(string)),
			new DataColumn("CHARACTER_SET_NAME", typeof(string))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "COLLATION_CHARACTER_SET_APPLICABILITY", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillColumnsAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length > 4)
		{
			throw new ArgumentException("More than 4 restrictionValues are not supported for schema 'Columns'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[21]
		{
			new DataColumn("TABLE_CATALOG", typeof(string)),
			new DataColumn("TABLE_SCHEMA", typeof(string)),
			new DataColumn("TABLE_NAME", typeof(string)),
			new DataColumn("COLUMN_NAME", typeof(string)),
			new DataColumn("ORDINAL_POSITION", typeof(uint)),
			new DataColumn("COLUMN_DEFAULT", typeof(string)),
			new DataColumn("IS_NULLABLE", typeof(string)),
			new DataColumn("DATA_TYPE", typeof(string)),
			new DataColumn("CHARACTER_MAXIMUM_LENGTH", typeof(long)),
			new DataColumn("NUMERIC_PRECISION", typeof(ulong)),
			new DataColumn("NUMERIC_SCALE", typeof(ulong)),
			new DataColumn("DATETIME_PRECISION", typeof(uint)),
			new DataColumn("CHARACTER_SET_NAME", typeof(string)),
			new DataColumn("COLLATION_NAME", typeof(string)),
			new DataColumn("COLUMN_TYPE", typeof(string)),
			new DataColumn("COLUMN_KEY", typeof(string)),
			new DataColumn("EXTRA", typeof(string)),
			new DataColumn("PRIVILEGES", typeof(string)),
			new DataColumn("COLUMN_COMMENT", typeof(string)),
			new DataColumn("GENERATION_EXPRESSION", typeof(string)),
			new DataColumn("SRS_ID", typeof(string))
		});
		List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
		if (restrictionValues != null)
		{
			if (restrictionValues.Length != 0 && !string.IsNullOrEmpty(restrictionValues[0]))
			{
				list.Add(new KeyValuePair<string, string>("TABLE_CATALOG", restrictionValues[0]));
			}
			if (restrictionValues.Length > 1 && !string.IsNullOrEmpty(restrictionValues[1]))
			{
				list.Add(new KeyValuePair<string, string>("TABLE_SCHEMA", restrictionValues[1]));
			}
			if (restrictionValues.Length > 2 && !string.IsNullOrEmpty(restrictionValues[2]))
			{
				list.Add(new KeyValuePair<string, string>("TABLE_NAME", restrictionValues[2]));
			}
			if (restrictionValues.Length > 3 && !string.IsNullOrEmpty(restrictionValues[3]))
			{
				list.Add(new KeyValuePair<string, string>("COLUMN_NAME", restrictionValues[3]));
			}
		}
		await FillDataTableAsync(ioBehavior, dataTable, "COLUMNS", list, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillDatabasesAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'Databases'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[5]
		{
			new DataColumn("CATALOG_NAME", typeof(string)),
			new DataColumn("SCHEMA_NAME", typeof(string)),
			new DataColumn("DEFAULT_CHARACTER_SET_NAME", typeof(string)),
			new DataColumn("DEFAULT_COLLATION_NAME", typeof(string)),
			new DataColumn("SQL_PATH", typeof(string))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "SCHEMATA", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private Task FillDataSourceInformationAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'DataSourceInformation'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[17]
		{
			new DataColumn("CompositeIdentifierSeparatorPattern", typeof(string)),
			new DataColumn("DataSourceProductName", typeof(string)),
			new DataColumn("DataSourceProductVersion", typeof(string)),
			new DataColumn("DataSourceProductVersionNormalized", typeof(string)),
			new DataColumn("GroupByBehavior", typeof(GroupByBehavior)),
			new DataColumn("IdentifierPattern", typeof(string)),
			new DataColumn("IdentifierCase", typeof(IdentifierCase)),
			new DataColumn("OrderByColumnsInSelect", typeof(bool)),
			new DataColumn("ParameterMarkerFormat", typeof(string)),
			new DataColumn("ParameterMarkerPattern", typeof(string)),
			new DataColumn("ParameterNameMaxLength", typeof(int)),
			new DataColumn("QuotedIdentifierPattern", typeof(string)),
			new DataColumn("QuotedIdentifierCase", typeof(IdentifierCase)),
			new DataColumn("ParameterNamePattern", typeof(string)),
			new DataColumn("StatementSeparatorPattern", typeof(string)),
			new DataColumn("StringLiteralPattern", typeof(string)),
			new DataColumn("SupportedJoinOperators", typeof(SupportedJoinOperators))
		});
		DoFillDataSourceInformation(dataTable);
		return Task.CompletedTask;
	}

	private Task FillDataTypesAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'DataTypes'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[23]
		{
			new DataColumn("TypeName", typeof(string)),
			new DataColumn("ProviderDbType", typeof(int)),
			new DataColumn("ColumnSize", typeof(long)),
			new DataColumn("CreateFormat", typeof(string)),
			new DataColumn("CreateParameters", typeof(string)),
			new DataColumn("DataType", typeof(string)),
			new DataColumn("IsAutoIncrementable", typeof(bool)),
			new DataColumn("IsBestMatch", typeof(bool)),
			new DataColumn("IsCaseSensitive", typeof(bool)),
			new DataColumn("IsFixedLength", typeof(bool)),
			new DataColumn("IsFixedPrecisionScale", typeof(bool)),
			new DataColumn("IsLong", typeof(bool)),
			new DataColumn("IsNullable", typeof(bool)),
			new DataColumn("IsSearchable", typeof(bool)),
			new DataColumn("IsSearchableWithLike", typeof(bool)),
			new DataColumn("IsUnsigned", typeof(bool)),
			new DataColumn("MaximumScale", typeof(short)),
			new DataColumn("MinimumScale", typeof(short)),
			new DataColumn("IsConcurrencyType", typeof(bool)),
			new DataColumn("IsLiteralSupported", typeof(bool)),
			new DataColumn("LiteralPrefix", typeof(string)),
			new DataColumn("LiteralSuffix", typeof(string)),
			new DataColumn("NativeDataType", typeof(string))
		});
		DoFillDataTypes(dataTable);
		return Task.CompletedTask;
	}

	private async Task FillEnginesAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'Engines'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[6]
		{
			new DataColumn("ENGINE", typeof(string)),
			new DataColumn("SUPPORT", typeof(string)),
			new DataColumn("COMMENT", typeof(string)),
			new DataColumn("TRANSACTIONS", typeof(string)),
			new DataColumn("XA", typeof(string)),
			new DataColumn("SAVEPOINTS", typeof(string))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "ENGINES", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillKeyColumnUsageAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'KeyColumnUsage'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[12]
		{
			new DataColumn("CONSTRAINT_CATALOG", typeof(string)),
			new DataColumn("CONSTRAINT_SCHEMA", typeof(string)),
			new DataColumn("CONSTRAINT_NAME", typeof(string)),
			new DataColumn("TABLE_CATALOG", typeof(string)),
			new DataColumn("TABLE_SCHEMA", typeof(string)),
			new DataColumn("TABLE_NAME", typeof(string)),
			new DataColumn("COLUMN_NAME", typeof(string)),
			new DataColumn("ORDINAL_POSITION", typeof(int)),
			new DataColumn("POSITION_IN_UNIQUE_CONSTRAINT", typeof(string)),
			new DataColumn("REFERENCED_TABLE_SCHEMA", typeof(string)),
			new DataColumn("REFERENCED_TABLE_NAME", typeof(string)),
			new DataColumn("REFERENCED_COLUMN_NAME", typeof(string))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "KEY_COLUMN_USAGE", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillKeyWordsAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'KeyWords'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[2]
		{
			new DataColumn("WORD", typeof(string)),
			new DataColumn("RESERVED", typeof(int))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "KEYWORDS", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillParametersAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'Parameters'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[16]
		{
			new DataColumn("SPECIFIC_CATALOG", typeof(string)),
			new DataColumn("SPECIFIC_SCHEMA", typeof(string)),
			new DataColumn("SPECIFIC_NAME", typeof(string)),
			new DataColumn("ORDINAL_POSITION", typeof(int)),
			new DataColumn("PARAMETER_MODE", typeof(string)),
			new DataColumn("PARAMETER_NAME", typeof(string)),
			new DataColumn("DATA_TYPE", typeof(string)),
			new DataColumn("CHARACTER_MAXIMUM_LENGTH", typeof(long)),
			new DataColumn("CHARACTER_OCTET_LENGTH", typeof(long)),
			new DataColumn("NUMERIC_PRECISION", typeof(int)),
			new DataColumn("NUMERIC_SCALE", typeof(int)),
			new DataColumn("DATETIME_PRECISION", typeof(int)),
			new DataColumn("CHARACTER_SET_NAME", typeof(string)),
			new DataColumn("COLLATION_NAME", typeof(string)),
			new DataColumn("DTD_IDENTIFIER", typeof(string)),
			new DataColumn("ROUTINE_TYPE", typeof(string))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "PARAMETERS", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillPartitionsAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'Partitions'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[25]
		{
			new DataColumn("TABLE_CATALOG", typeof(string)),
			new DataColumn("TABLE_SCHEMA", typeof(string)),
			new DataColumn("TABLE_NAME", typeof(string)),
			new DataColumn("PARTITION_NAME", typeof(string)),
			new DataColumn("SUBPARTITION_NAME", typeof(string)),
			new DataColumn("PARTITION_ORDINAL_POSITION", typeof(int)),
			new DataColumn("SUBPARTITION_ORDINAL_POSITION", typeof(int)),
			new DataColumn("PARTITION_METHOD", typeof(string)),
			new DataColumn("SUBPARTITION_METHOD", typeof(string)),
			new DataColumn("PARTITION_EXPRESSION", typeof(string)),
			new DataColumn("SUBPARTITION_EXPRESSION", typeof(string)),
			new DataColumn("PARTITION_DESCRIPTION", typeof(string)),
			new DataColumn("TABLE_ROWS", typeof(long)),
			new DataColumn("AVG_ROW_LENGTH", typeof(long)),
			new DataColumn("DATA_LENGTH", typeof(long)),
			new DataColumn("MAX_DATA_LENGTH", typeof(long)),
			new DataColumn("INDEX_LENGTH", typeof(long)),
			new DataColumn("DATA_FREE", typeof(long)),
			new DataColumn("CREATE_TIME", typeof(DateTime)),
			new DataColumn("UPDATE_TIME", typeof(DateTime)),
			new DataColumn("CHECK_TIME", typeof(DateTime)),
			new DataColumn("CHECKSUM", typeof(long)),
			new DataColumn("PARTITION_COMMENT", typeof(string)),
			new DataColumn("NODEGROUP", typeof(string)),
			new DataColumn("TABLESPACE_NAME", typeof(string))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "PARTITIONS", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillPluginsAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'Plugins'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[11]
		{
			new DataColumn("PLUGIN_NAME", typeof(string)),
			new DataColumn("PLUGIN_VERSION", typeof(string)),
			new DataColumn("PLUGIN_STATUS", typeof(string)),
			new DataColumn("PLUGIN_TYPE", typeof(string)),
			new DataColumn("PLUGIN_TYPE_VERSION", typeof(string)),
			new DataColumn("PLUGIN_LIBRARY", typeof(string)),
			new DataColumn("PLUGIN_LIBRARY_VERSION", typeof(string)),
			new DataColumn("PLUGIN_AUTHOR", typeof(string)),
			new DataColumn("PLUGIN_DESCRIPTION", typeof(string)),
			new DataColumn("PLUGIN_LICENSE", typeof(string)),
			new DataColumn("LOAD_OPTION", typeof(string))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "PLUGINS", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillProceduresAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'Procedures'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[20]
		{
			new DataColumn("SPECIFIC_NAME", typeof(string)),
			new DataColumn("ROUTINE_CATALOG", typeof(string)),
			new DataColumn("ROUTINE_SCHEMA", typeof(string)),
			new DataColumn("ROUTINE_NAME", typeof(string)),
			new DataColumn("ROUTINE_TYPE", typeof(string)),
			new DataColumn("DTD_IDENTIFIER", typeof(string)),
			new DataColumn("ROUTINE_BODY", typeof(string)),
			new DataColumn("ROUTINE_DEFINITION", typeof(string)),
			new DataColumn("EXTERNAL_NAME", typeof(string)),
			new DataColumn("EXTERNAL_LANGUAGE", typeof(string)),
			new DataColumn("PARAMETER_STYLE", typeof(string)),
			new DataColumn("IS_DETERMINISTIC", typeof(string)),
			new DataColumn("SQL_DATA_ACCESS", typeof(string)),
			new DataColumn("SQL_PATH", typeof(string)),
			new DataColumn("SECURITY_TYPE", typeof(string)),
			new DataColumn("CREATED", typeof(DateTime)),
			new DataColumn("LAST_ALTERED", typeof(DateTime)),
			new DataColumn("SQL_MODE", typeof(string)),
			new DataColumn("ROUTINE_COMMENT", typeof(string)),
			new DataColumn("DEFINER", typeof(string))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "ROUTINES", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillProcessListAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'ProcessList'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[8]
		{
			new DataColumn("ID", typeof(long)),
			new DataColumn("USER", typeof(string)),
			new DataColumn("HOST", typeof(string)),
			new DataColumn("DB", typeof(string)),
			new DataColumn("COMMAND", typeof(string)),
			new DataColumn("TIME", typeof(int)),
			new DataColumn("STATE", typeof(string)),
			new DataColumn("INFO", typeof(string))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "PROCESSLIST", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillProfilingAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'Profiling'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[18]
		{
			new DataColumn("QUERY_ID", typeof(int)),
			new DataColumn("SEQ", typeof(int)),
			new DataColumn("STATE", typeof(string)),
			new DataColumn("DURATION", typeof(decimal)),
			new DataColumn("CPU_USER", typeof(decimal)),
			new DataColumn("CPU_SYSTEM", typeof(decimal)),
			new DataColumn("CONTEXT_VOLUNTARY", typeof(int)),
			new DataColumn("CONTEXT_INVOLUNTARY", typeof(int)),
			new DataColumn("BLOCK_OPS_IN", typeof(int)),
			new DataColumn("BLOCK_OPS_OUT", typeof(int)),
			new DataColumn("MESSAGES_SENT", typeof(int)),
			new DataColumn("MESSAGES_RECEIVED", typeof(int)),
			new DataColumn("PAGE_FAULTS_MAJOR", typeof(int)),
			new DataColumn("PAGE_FAULTS_MINOR", typeof(int)),
			new DataColumn("SWAPS", typeof(int)),
			new DataColumn("SOURCE_FUNCTION", typeof(string)),
			new DataColumn("SOURCE_FILE", typeof(string)),
			new DataColumn("SOURCE_LINE", typeof(int))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "PROFILING", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillReferentialConstraintsAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'ReferentialConstraints'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[11]
		{
			new DataColumn("CONSTRAINT_CATALOG", typeof(string)),
			new DataColumn("CONSTRAINT_SCHEMA", typeof(string)),
			new DataColumn("CONSTRAINT_NAME", typeof(string)),
			new DataColumn("UNIQUE_CONSTRAINT_CATALOG", typeof(string)),
			new DataColumn("UNIQUE_CONSTRAINT_SCHEMA", typeof(string)),
			new DataColumn("UNIQUE_CONSTRAINT_NAME", typeof(string)),
			new DataColumn("MATCH_OPTION", typeof(string)),
			new DataColumn("UPDATE_RULE", typeof(string)),
			new DataColumn("DELETE_RULE", typeof(string)),
			new DataColumn("TABLE_NAME", typeof(string)),
			new DataColumn("REFERENCED_TABLE_NAME", typeof(string))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "REFERENTIAL_CONSTRAINTS", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private Task FillReservedWordsAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'ReservedWords'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[1]
		{
			new DataColumn("ReservedWord", typeof(string))
		});
		DoFillReservedWords(dataTable);
		return Task.CompletedTask;
	}

	private async Task FillResourceGroupsAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'ResourceGroups'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[5]
		{
			new DataColumn("RESOURCE_GROUP_NAME", typeof(string)),
			new DataColumn("RESOURCE_GROUP_TYPE", typeof(string)),
			new DataColumn("RESOURCE_GROUP_ENABLED", typeof(int)),
			new DataColumn("VCPU_IDS", typeof(string)),
			new DataColumn("THREAD_PRIORITY", typeof(int))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "RESOURCE_GROUPS", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private Task FillRestrictionsAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'Restrictions'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[4]
		{
			new DataColumn("CollectionName", typeof(string)),
			new DataColumn("RestrictionName", typeof(string)),
			new DataColumn("RestrictionDefault", typeof(string)),
			new DataColumn("RestrictionNumber", typeof(int))
		});
		dataTable.Rows.Add("Columns", "Catalog", "TABLE_CATALOG", 1);
		dataTable.Rows.Add("Columns", "Schema", "TABLE_SCHEMA", 2);
		dataTable.Rows.Add("Columns", "Table", "TABLE_NAME", 3);
		dataTable.Rows.Add("Columns", "Column", "COLUMN_NAME", 4);
		dataTable.Rows.Add("Tables", "Catalog", "TABLE_CATALOG", 1);
		dataTable.Rows.Add("Tables", "Schema", "TABLE_SCHEMA", 2);
		dataTable.Rows.Add("Tables", "Table", "TABLE_NAME", 3);
		dataTable.Rows.Add("Tables", "TableType", "TABLE_TYPE", 4);
		dataTable.Rows.Add("Foreign Keys", "Catalog", "TABLE_CATALOG", 1);
		dataTable.Rows.Add("Foreign Keys", "Schema", "TABLE_SCHEMA", 2);
		dataTable.Rows.Add("Foreign Keys", "Table", "TABLE_NAME", 3);
		dataTable.Rows.Add("Foreign Keys", "Constraint Name", "CONSTRAINT_NAME", 4);
		dataTable.Rows.Add("Indexes", "Catalog", "TABLE_CATALOG", 1);
		dataTable.Rows.Add("Indexes", "Schema", "TABLE_SCHEMA", 2);
		dataTable.Rows.Add("Indexes", "Table", "TABLE_NAME", 3);
		dataTable.Rows.Add("Indexes", "Name", "INDEX_NAME", 4);
		dataTable.Rows.Add("IndexColumns", "Catalog", "TABLE_CATALOG", 1);
		dataTable.Rows.Add("IndexColumns", "Schema", "TABLE_SCHEMA", 2);
		dataTable.Rows.Add("IndexColumns", "Table", "TABLE_NAME", 3);
		dataTable.Rows.Add("IndexColumns", "Name", "INDEX_NAME", 4);
		dataTable.Rows.Add("IndexColumns", "Column", "COLUMN_NAME", 5);
		return Task.CompletedTask;
	}

	private async Task FillSchemaPrivilegesAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'SchemaPrivileges'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[5]
		{
			new DataColumn("GRANTEE", typeof(string)),
			new DataColumn("TABLE_CATALOG", typeof(string)),
			new DataColumn("TABLE_SCHEMA", typeof(string)),
			new DataColumn("PRIVILEGE_TYPE", typeof(string)),
			new DataColumn("IS_GRANTABLE", typeof(string))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "SCHEMA_PRIVILEGES", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillTablesAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length > 4)
		{
			throw new ArgumentException("More than 4 restrictionValues are not supported for schema 'Tables'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[21]
		{
			new DataColumn("TABLE_CATALOG", typeof(string)),
			new DataColumn("TABLE_SCHEMA", typeof(string)),
			new DataColumn("TABLE_NAME", typeof(string)),
			new DataColumn("TABLE_TYPE", typeof(string)),
			new DataColumn("ENGINE", typeof(string)),
			new DataColumn("VERSION", typeof(string)),
			new DataColumn("ROW_FORMAT", typeof(string)),
			new DataColumn("TABLE_ROWS", typeof(long)),
			new DataColumn("AVG_ROW_LENGTH", typeof(long)),
			new DataColumn("DATA_LENGTH", typeof(long)),
			new DataColumn("MAX_DATA_LENGTH", typeof(long)),
			new DataColumn("INDEX_LENGTH", typeof(long)),
			new DataColumn("DATA_FREE", typeof(long)),
			new DataColumn("AUTO_INCREMENT", typeof(long)),
			new DataColumn("CREATE_TIME", typeof(DateTime)),
			new DataColumn("UPDATE_TIME", typeof(DateTime)),
			new DataColumn("CHECK_TIME", typeof(DateTime)),
			new DataColumn("TABLE_COLLATION", typeof(string)),
			new DataColumn("CHECKSUM", typeof(string)),
			new DataColumn("CREATE_OPTIONS", typeof(string)),
			new DataColumn("TABLE_COMMENT", typeof(string))
		});
		List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
		if (restrictionValues != null)
		{
			if (restrictionValues.Length != 0 && !string.IsNullOrEmpty(restrictionValues[0]))
			{
				list.Add(new KeyValuePair<string, string>("TABLE_CATALOG", restrictionValues[0]));
			}
			if (restrictionValues.Length > 1 && !string.IsNullOrEmpty(restrictionValues[1]))
			{
				list.Add(new KeyValuePair<string, string>("TABLE_SCHEMA", restrictionValues[1]));
			}
			if (restrictionValues.Length > 2 && !string.IsNullOrEmpty(restrictionValues[2]))
			{
				list.Add(new KeyValuePair<string, string>("TABLE_NAME", restrictionValues[2]));
			}
			if (restrictionValues.Length > 3 && !string.IsNullOrEmpty(restrictionValues[3]))
			{
				list.Add(new KeyValuePair<string, string>("TABLE_TYPE", restrictionValues[3]));
			}
		}
		await FillDataTableAsync(ioBehavior, dataTable, "TABLES", list, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillTableConstraintsAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'TableConstraints'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[6]
		{
			new DataColumn("CONSTRAINT_CATALOG", typeof(string)),
			new DataColumn("CONSTRAINT_SCHEMA", typeof(string)),
			new DataColumn("CONSTRAINT_NAME", typeof(string)),
			new DataColumn("TABLE_SCHEMA", typeof(string)),
			new DataColumn("TABLE_NAME", typeof(string)),
			new DataColumn("CONSTRAINT_TYPE", typeof(string))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "TABLE_CONSTRAINTS", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillTablePrivilegesAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'TablePrivileges'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[6]
		{
			new DataColumn("GRANTEE", typeof(string)),
			new DataColumn("TABLE_CATALOG", typeof(string)),
			new DataColumn("TABLE_SCHEMA", typeof(string)),
			new DataColumn("TABLE_NAME", typeof(string)),
			new DataColumn("PRIVILEGE_TYPE", typeof(string)),
			new DataColumn("IS_GRANTABLE", typeof(string))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "TABLE_PRIVILEGES", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillTriggersAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'Triggers'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[22]
		{
			new DataColumn("TRIGGER_CATALOG", typeof(string)),
			new DataColumn("TRIGGER_SCHEMA", typeof(string)),
			new DataColumn("TRIGGER_NAME", typeof(string)),
			new DataColumn("EVENT_MANIPULATION", typeof(string)),
			new DataColumn("EVENT_OBJECT_CATALOG", typeof(string)),
			new DataColumn("EVENT_OBJECT_SCHEMA", typeof(string)),
			new DataColumn("EVENT_OBJECT_TABLE", typeof(string)),
			new DataColumn("ACTION_ORDER", typeof(long)),
			new DataColumn("ACTION_CONDITION", typeof(string)),
			new DataColumn("ACTION_STATEMENT", typeof(string)),
			new DataColumn("ACTION_ORIENTATION", typeof(string)),
			new DataColumn("ACTION_TIMING", typeof(string)),
			new DataColumn("ACTION_REFERENCE_OLD_TABLE", typeof(string)),
			new DataColumn("ACTION_REFERENCE_NEW_TABLE", typeof(string)),
			new DataColumn("ACTION_REFERENCE_OLD_ROW", typeof(string)),
			new DataColumn("ACTION_REFERENCE_NEW_ROW", typeof(string)),
			new DataColumn("CREATED", typeof(DateTime)),
			new DataColumn("SQL_MODE", typeof(string)),
			new DataColumn("DEFINER", typeof(string)),
			new DataColumn("CHARACTER_SET_CLIENT", typeof(string)),
			new DataColumn("COLLATION_CONNECTION", typeof(string)),
			new DataColumn("DATABASE_COLLATION", typeof(string))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "TRIGGERS", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillUserPrivilegesAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'UserPrivileges'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[4]
		{
			new DataColumn("GRANTEE", typeof(string)),
			new DataColumn("TABLE_CATALOG", typeof(string)),
			new DataColumn("PRIVILEGE_TYPE", typeof(string)),
			new DataColumn("IS_GRANTABLE", typeof(string))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "USER_PRIVILEGES", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillViewsAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length != 0)
		{
			throw new ArgumentException("restrictionValues is not supported for schema 'Views'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[10]
		{
			new DataColumn("TABLE_CATALOG", typeof(string)),
			new DataColumn("TABLE_SCHEMA", typeof(string)),
			new DataColumn("TABLE_NAME", typeof(string)),
			new DataColumn("VIEW_DEFINITION", typeof(string)),
			new DataColumn("CHECK_OPTION", typeof(string)),
			new DataColumn("IS_UPDATABLE", typeof(string)),
			new DataColumn("DEFINER", typeof(string)),
			new DataColumn("SECURITY_TYPE", typeof(string)),
			new DataColumn("CHARACTER_SET_CLIENT", typeof(string)),
			new DataColumn("COLLATION_CONNECTION", typeof(string))
		});
		await FillDataTableAsync(ioBehavior, dataTable, "VIEWS", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillForeignKeysAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length > 4)
		{
			throw new ArgumentException("More than 4 restrictionValues are not supported for schema 'Foreign Keys'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[12]
		{
			new DataColumn("CONSTRAINT_CATALOG", typeof(string)),
			new DataColumn("CONSTRAINT_SCHEMA", typeof(string)),
			new DataColumn("CONSTRAINT_NAME", typeof(string)),
			new DataColumn("TABLE_CATALOG", typeof(string)),
			new DataColumn("TABLE_SCHEMA", typeof(string)),
			new DataColumn("TABLE_NAME", typeof(string)),
			new DataColumn("MATCH_OPTION", typeof(string)),
			new DataColumn("UPDATE_RULE", typeof(string)),
			new DataColumn("DELETE_RULE", typeof(string)),
			new DataColumn("REFERENCED_TABLE_CATALOG", typeof(string)),
			new DataColumn("REFERENCED_TABLE_SCHEMA", typeof(string)),
			new DataColumn("REFERENCED_TABLE_NAME", typeof(string))
		});
		await DoFillForeignKeysAsync(ioBehavior, dataTable, restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillIndexesAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length > 4)
		{
			throw new ArgumentException("More than 4 restrictionValues are not supported for schema 'Indexes'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[8]
		{
			new DataColumn("INDEX_CATALOG", typeof(string)),
			new DataColumn("INDEX_SCHEMA", typeof(string)),
			new DataColumn("INDEX_NAME", typeof(string)),
			new DataColumn("TABLE_NAME", typeof(string)),
			new DataColumn("UNIQUE", typeof(bool)),
			new DataColumn("PRIMARY", typeof(bool)),
			new DataColumn("TYPE", typeof(string)),
			new DataColumn("COMMENT", typeof(string))
		});
		await DoFillIndexesAsync(ioBehavior, dataTable, restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task FillIndexColumnsAsync(IOBehavior ioBehavior, DataTable dataTable, string tableName, string?[]? restrictionValues, CancellationToken cancellationToken)
	{
		if (restrictionValues != null && restrictionValues.Length > 5)
		{
			throw new ArgumentException("More than 5 restrictionValues are not supported for schema 'IndexColumns'.", "restrictionValues");
		}
		dataTable.TableName = tableName;
		dataTable.Columns.AddRange(new DataColumn[7]
		{
			new DataColumn("INDEX_CATALOG", typeof(string)),
			new DataColumn("INDEX_SCHEMA", typeof(string)),
			new DataColumn("INDEX_NAME", typeof(string)),
			new DataColumn("TABLE_NAME", typeof(string)),
			new DataColumn("COLUMN_NAME", typeof(string)),
			new DataColumn("ORDINAL_POSITION", typeof(int)),
			new DataColumn("SORT_ORDER", typeof(string))
		});
		await DoFillIndexColumnsAsync(ioBehavior, dataTable, restrictionValues, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}
}
