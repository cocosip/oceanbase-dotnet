using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using OceanBase.Protocol;
using OceanBase.Protocol.Payloads;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;

namespace OceanBase.Core;

internal sealed class TypeMapper
{
	private readonly List<ColumnTypeMetadata> m_columnTypeMetadata;

	private readonly List<OracleColumnTypeMetadata> m_oracleColumnTypeMetadata;

	private readonly Dictionary<Type, DbTypeMapping> m_dbTypeMappingsByClrType;

	private readonly Dictionary<DbType, DbTypeMapping> m_dbTypeMappingsByDbType;

	private readonly Dictionary<string, ColumnTypeMetadata> m_columnTypeMetadataLookup;

	private readonly Dictionary<string, OracleColumnTypeMetadata> m_oracleColumnTypeMetadataLookup;

	private readonly Dictionary<MySqlDbType, ColumnTypeMetadata> m_mySqlDbTypeToColumnTypeMetadata;

	private readonly Dictionary<OracleDbType, OracleColumnTypeMetadata> m_OracleDbTypeToColumnTypeMetadata;

	public static TypeMapper Instance { get; } = new TypeMapper();

	private TypeMapper()
	{
		m_columnTypeMetadata = new List<ColumnTypeMetadata>();
		m_oracleColumnTypeMetadata = new List<OracleColumnTypeMetadata>();
		m_dbTypeMappingsByClrType = new Dictionary<Type, DbTypeMapping>();
		m_dbTypeMappingsByDbType = new Dictionary<DbType, DbTypeMapping>();
		m_columnTypeMetadataLookup = new Dictionary<string, ColumnTypeMetadata>(StringComparer.OrdinalIgnoreCase);
		m_oracleColumnTypeMetadataLookup = new Dictionary<string, OracleColumnTypeMetadata>(StringComparer.OrdinalIgnoreCase);
		m_mySqlDbTypeToColumnTypeMetadata = new Dictionary<MySqlDbType, ColumnTypeMetadata>();
		m_OracleDbTypeToColumnTypeMetadata = new Dictionary<OracleDbType, OracleColumnTypeMetadata>();
		DbTypeMapping dbTypeMapping = AddDbTypeMapping(new DbTypeMapping(typeof(bool), new DbType[1] { DbType.Boolean }, (object o) => Convert.ToBoolean(o, CultureInfo.InvariantCulture)));
		AddColumnTypeMetadata(new ColumnTypeMetadata("TINYINT", dbTypeMapping, MySqlDbType.Bool, isUnsigned: false, binary: false, 1, "BOOL", "BOOL", 1L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("TINYINT", dbTypeMapping, OracleDbType.Boolean, isUnsigned: false, binary: false, 1, "BOOLEAN", "BOOLEAN", 1L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("PL/SQL BOOLEAN", dbTypeMapping, OracleDbType.Boolean, isUnsigned: false, binary: false, 0, "PL/SQL BOOLEAN", "PL/SQL BOOLEAN", 1L));
		DbTypeMapping dbTypeMapping2 = AddDbTypeMapping(new DbTypeMapping(typeof(sbyte), new DbType[1] { DbType.SByte }, (object o) => Convert.ToSByte(o, CultureInfo.InvariantCulture)));
		DbTypeMapping dbTypeMapping3 = AddDbTypeMapping(new DbTypeMapping(typeof(byte), new DbType[1] { DbType.Byte }, (object o) => Convert.ToByte(o, CultureInfo.InvariantCulture)));
		DbTypeMapping dbTypeMapping4 = AddDbTypeMapping(new DbTypeMapping(typeof(short), new DbType[1] { DbType.Int16 }, (object o) => Convert.ToInt16(o, CultureInfo.InvariantCulture)));
		DbTypeMapping dbTypeMapping5 = AddDbTypeMapping(new DbTypeMapping(typeof(ushort), new DbType[1] { DbType.UInt16 }, (object o) => Convert.ToUInt16(o, CultureInfo.InvariantCulture)));
		DbTypeMapping dbTypeMapping6 = AddDbTypeMapping(new DbTypeMapping(typeof(int), new DbType[1] { DbType.Int32 }, (object o) => Convert.ToInt32(o, CultureInfo.InvariantCulture)));
		DbTypeMapping dbTypeMapping7 = AddDbTypeMapping(new DbTypeMapping(typeof(uint), new DbType[1] { DbType.UInt32 }, (object o) => Convert.ToUInt32(o, CultureInfo.InvariantCulture)));
		DbTypeMapping dbTypeMapping8 = AddDbTypeMapping(new DbTypeMapping(typeof(long), new DbType[1] { DbType.Int64 }, (object o) => Convert.ToInt64(o, CultureInfo.InvariantCulture)));
		DbTypeMapping dbTypeMapping9 = AddDbTypeMapping(new DbTypeMapping(typeof(ulong), new DbType[1] { DbType.UInt64 }, (object o) => Convert.ToUInt64(o, CultureInfo.InvariantCulture)));
		AddColumnTypeMetadata(new ColumnTypeMetadata("TINYINT", dbTypeMapping2, MySqlDbType.Byte, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("TINYINT", dbTypeMapping3, MySqlDbType.UByte, isUnsigned: true, binary: false, 1, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("TINYINT", dbTypeMapping3, MySqlDbType.UByte, isUnsigned: true, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("SMALLINT", dbTypeMapping4, MySqlDbType.Int16, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("SMALLINT", dbTypeMapping5, MySqlDbType.UInt16, isUnsigned: true, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("INT", dbTypeMapping6, MySqlDbType.Int32, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("INT", dbTypeMapping7, MySqlDbType.UInt32, isUnsigned: true, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("MEDIUMINT", dbTypeMapping6, MySqlDbType.Int24, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("MEDIUMINT", dbTypeMapping7, MySqlDbType.UInt24, isUnsigned: true, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("BIGINT", dbTypeMapping8, MySqlDbType.Int64, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("BIGINT", dbTypeMapping9, MySqlDbType.UInt64, isUnsigned: true, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("BIT", dbTypeMapping9, MySqlDbType.Bit, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("TINYINT", dbTypeMapping3, OracleDbType.Byte, isUnsigned: false, binary: false, 0, null, "NUMBER(3,0)", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("SMALLINT", dbTypeMapping4, OracleDbType.Int16, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("NUMBER", dbTypeMapping6, OracleDbType.Int32, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("BIGINT", dbTypeMapping8, OracleDbType.Int64, isUnsigned: false, binary: false, 0, null, null, 0L));
		DbTypeMapping dbTypeMapping10 = AddDbTypeMapping(new DbTypeMapping(typeof(decimal), new DbType[3]
		{
			DbType.Decimal,
			DbType.Currency,
			DbType.VarNumeric
		}, (object o) => Convert.ToDecimal(o, CultureInfo.InvariantCulture)));
		DbTypeMapping dbTypeMapping11 = AddDbTypeMapping(new DbTypeMapping(typeof(double), new DbType[1] { DbType.Double }, (object o) => Convert.ToDouble(o, CultureInfo.InvariantCulture)));
		DbTypeMapping dbTypeMapping12 = AddDbTypeMapping(new DbTypeMapping(typeof(float), new DbType[1] { DbType.Single }, (object o) => Convert.ToSingle(o, CultureInfo.InvariantCulture)));
		AddColumnTypeMetadata(new ColumnTypeMetadata("DECIMAL", dbTypeMapping10, MySqlDbType.NewDecimal, isUnsigned: false, binary: false, 0, null, "DECIMAL({0},{1});precision,scale", 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("DECIMAL", dbTypeMapping10, MySqlDbType.NewDecimal, isUnsigned: true, binary: false, 0, null, "DECIMAL({0},{1}) UNSIGNED;precision,scale", 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("DECIMAL", dbTypeMapping10, MySqlDbType.Decimal, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("DOUBLE", dbTypeMapping11, MySqlDbType.Double, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("FLOAT", dbTypeMapping12, MySqlDbType.Float, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("DECIMAL", dbTypeMapping10, OracleDbType.Decimal, isUnsigned: false, binary: false, 0, null, "DECIMAL({0},{1});precision,scale", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("DECIMAL", dbTypeMapping10, OracleDbType.Decimal, isUnsigned: true, binary: false, 0, null, "DECIMAL({0},{1}) UNSIGNED;precision,scale", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("DOUBLE", dbTypeMapping11, OracleDbType.Double, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("BINARY_DOUBLE", dbTypeMapping11, OracleDbType.BinaryDouble, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("FLOAT", dbTypeMapping12, OracleDbType.Single, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("BINARY_FLOAT", dbTypeMapping12, OracleDbType.BinaryFloat, isUnsigned: false, binary: false, 0, null, null, 0L));
		DbTypeMapping dbTypeMapping13 = AddDbTypeMapping(new DbTypeMapping(typeof(ReadOnlyMemory<float>), new DbType[1] { DbType.Object }));
		AddColumnTypeMetadata(new ColumnTypeMetadata("VECTOR", dbTypeMapping13, MySqlDbType.Vector, isUnsigned: false, binary: true, 0, "VECTOR", "VECTOR({0})", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("VECTOR", dbTypeMapping13, OracleDbType.Vector, isUnsigned: false, binary: true, 0, "VECTOR", "VECTOR({0})", 0L));
		DbTypeMapping dbTypeMapping14 = AddDbTypeMapping(new DbTypeMapping(typeof(string), new DbType[2]
		{
			DbType.StringFixedLength,
			DbType.AnsiStringFixedLength
		}, Convert.ToString));
		DbTypeMapping dbTypeMapping15 = AddDbTypeMapping(new DbTypeMapping(typeof(string), new DbType[3]
		{
			DbType.String,
			DbType.AnsiString,
			DbType.Xml
		}, Convert.ToString));
		AddColumnTypeMetadata(new ColumnTypeMetadata("VARCHAR", dbTypeMapping15, MySqlDbType.VarChar, isUnsigned: false, binary: false, 0, null, "VARCHAR({0});size", 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("VARCHAR", dbTypeMapping15, MySqlDbType.VarString, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("CHAR", dbTypeMapping14, MySqlDbType.String, isUnsigned: false, binary: false, 0, null, "CHAR({0});size", 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("TINYTEXT", dbTypeMapping15, MySqlDbType.TinyText, isUnsigned: false, binary: false, 0, "VARCHAR", null, 255L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("TEXT", dbTypeMapping15, MySqlDbType.Text, isUnsigned: false, binary: false, 0, "VARCHAR", null, 65535L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("MEDIUMTEXT", dbTypeMapping15, MySqlDbType.MediumText, isUnsigned: false, binary: false, 0, "VARCHAR", null, 16777215L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("LONGTEXT", dbTypeMapping15, MySqlDbType.LongText, isUnsigned: false, binary: false, 0, "VARCHAR", null, 4294967295L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("ENUM", dbTypeMapping15, MySqlDbType.Enum, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("SET", dbTypeMapping15, MySqlDbType.Set, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("JSON", dbTypeMapping15, MySqlDbType.JSON, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("VARCHAR2", dbTypeMapping15, OracleDbType.Varchar2, isUnsigned: false, binary: false, 0, "VARCHAR2", "VARCHAR2({0});size", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("NVARCHAR2", dbTypeMapping15, OracleDbType.NVarchar2, isUnsigned: false, binary: false, 0, "NVARCHAR2", "NVARCHAR2({0});size", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("CHAR", dbTypeMapping14, OracleDbType.Char, isUnsigned: false, binary: false, 0, "CHAR", "CHAR({0});size", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("NCHAR", dbTypeMapping14, OracleDbType.NChar, isUnsigned: false, binary: false, 0, "NCHAR", "NCHAR({0});size", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("LONG", dbTypeMapping15, OracleDbType.Long, isUnsigned: false, binary: false, 0, "LONG", "LONG", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("JSON", dbTypeMapping15, OracleDbType.Json, isUnsigned: false, binary: false, 0, "JSON", "JSON", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("XMLTYPE", dbTypeMapping15, OracleDbType.XmlType, isUnsigned: false, binary: false, 0, "XMLTYPE", "XMLTYPE", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("CLOB", dbTypeMapping15, OracleDbType.Clob, isUnsigned: false, binary: false, 0, "CLOB", "CLOB", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("NCLOB", dbTypeMapping15, OracleDbType.NClob, isUnsigned: false, binary: false, 0, "NCLOB", "NCLOB", 0L));
		DbTypeMapping dbTypeMapping16 = AddDbTypeMapping(new DbTypeMapping(typeof(byte[]), new DbType[1] { DbType.Binary }));
		AddColumnTypeMetadata(new ColumnTypeMetadata("BLOB", dbTypeMapping16, MySqlDbType.Blob, isUnsigned: false, binary: true, 0, "BLOB", null, 65535L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("BINARY", dbTypeMapping16, MySqlDbType.Binary, isUnsigned: false, binary: true, 0, "BLOB", "BINARY({0});length", 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("VARBINARY", dbTypeMapping16, MySqlDbType.VarBinary, isUnsigned: false, binary: true, 0, "BLOB", "VARBINARY({0});length", 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("TINYBLOB", dbTypeMapping16, MySqlDbType.TinyBlob, isUnsigned: false, binary: true, 0, "BLOB", null, 255L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("MEDIUMBLOB", dbTypeMapping16, MySqlDbType.MediumBlob, isUnsigned: false, binary: true, 0, "BLOB", null, 16777215L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("LONGBLOB", dbTypeMapping16, MySqlDbType.LongBlob, isUnsigned: false, binary: true, 0, "BLOB", null, 4294967295L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("RAW", dbTypeMapping16, OracleDbType.Raw, isUnsigned: false, binary: true, 0, "RAW", "RAW({0});length", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("LONG RAW", dbTypeMapping16, OracleDbType.LongRaw, isUnsigned: false, binary: true, 0, "LONG RAW", "LONG RAW", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("BLOB", dbTypeMapping16, OracleDbType.Blob, isUnsigned: false, binary: true, 0, "BLOB", "BLOB", 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("GEOMETRY", dbTypeMapping16, MySqlDbType.Geometry, isUnsigned: false, binary: true, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("POINT", dbTypeMapping16, MySqlDbType.Geometry, isUnsigned: false, binary: true, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("LINESTRING", dbTypeMapping16, MySqlDbType.Geometry, isUnsigned: false, binary: true, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("POLYGON", dbTypeMapping16, MySqlDbType.Geometry, isUnsigned: false, binary: true, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("MULTIPOINT", dbTypeMapping16, MySqlDbType.Geometry, isUnsigned: false, binary: true, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("MULTILINESTRING", dbTypeMapping16, MySqlDbType.Geometry, isUnsigned: false, binary: true, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("MULTIPOLYGON", dbTypeMapping16, MySqlDbType.Geometry, isUnsigned: false, binary: true, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("GEOMETRYCOLLECTION", dbTypeMapping16, MySqlDbType.Geometry, isUnsigned: false, binary: true, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("GEOMCOLLECTION", dbTypeMapping16, MySqlDbType.Geometry, isUnsigned: false, binary: true, 0, null, null, 0L));
		AddDbTypeMapping(new DbTypeMapping(typeof(DateOnly), new DbType[1] { DbType.Date }));
		DbTypeMapping dbTypeMapping17 = AddDbTypeMapping(new DbTypeMapping(typeof(DateTime), new DbType[1] { DbType.Date }));
		DbTypeMapping dbTypeMapping18 = AddDbTypeMapping(new DbTypeMapping(typeof(DateTime), new DbType[3]
		{
			DbType.DateTime,
			DbType.DateTime2,
			DbType.DateTimeOffset
		}));
		DbTypeMapping dbTypeMapping19 = AddDbTypeMapping(new DbTypeMapping(typeof(DateTimeOffset), new DbType[1] { DbType.DateTimeOffset }));
		AddDbTypeMapping(new DbTypeMapping(typeof(TimeOnly), new DbType[1] { DbType.Time }));
		DbTypeMapping dbTypeMapping20 = AddDbTypeMapping(new DbTypeMapping(typeof(TimeSpan), new DbType[1] { DbType.Time }, (object o) => (!(o is string s)) ? Convert.ChangeType(o, typeof(TimeSpan), CultureInfo.InvariantCulture) : ((object)Utility.ParseTimeSpan(Encoding.UTF8.GetBytes(s)))));
		AddColumnTypeMetadata(new ColumnTypeMetadata("DATETIME", dbTypeMapping18, MySqlDbType.DateTime, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("DATE", dbTypeMapping17, MySqlDbType.Date, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("DATE", dbTypeMapping17, MySqlDbType.Newdate, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("TIME", dbTypeMapping20, MySqlDbType.Time, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("TIMESTAMP", dbTypeMapping18, MySqlDbType.Timestamp, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("YEAR", dbTypeMapping6, MySqlDbType.Year, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("DATE", dbTypeMapping17, OracleDbType.Date, isUnsigned: false, binary: false, 0, "DATE", "DATE", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("TIMESTAMP", dbTypeMapping18, OracleDbType.TimeStamp, isUnsigned: false, binary: false, 0, "TIMESTAMP", "TIMESTAMP{{0}}", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("TIMESTAMP WITH LOCAL TIME ZONE", dbTypeMapping18, OracleDbType.TimeStampLTZ, isUnsigned: false, binary: false, 0, "TIMESTAMP WITH LOCAL TIME ZONE", "TIMESTAMP{{0}} WITH LOCAL TIME ZONE", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("TIMESTAMP WITH TIME ZONE", dbTypeMapping19, OracleDbType.TimeStampTZ, isUnsigned: false, binary: false, 0, "TIMESTAMP WITH TIME ZONE", "TIMESTAMP{{0}} WITH TIME ZONE", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("INTERVAL DAY TO SECOND", dbTypeMapping20, OracleDbType.IntervalDS, isUnsigned: false, binary: false, 0, "INTERVAL DAY TO SECOND", "INTERVAL DAY{{0}} TO SECOND{{1}}", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("INTERVAL YEAR TO MONTH", dbTypeMapping8, OracleDbType.IntervalYM, isUnsigned: false, binary: false, 0, "INTERVAL YEAR TO MONTH", "INTERVAL YEAR{{0}} TO MONTH", 0L));
		Func<object, object> convert = (object o) => Guid.Parse(Convert.ToString(o, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
		DbTypeMapping dbTypeMapping21 = AddDbTypeMapping(new DbTypeMapping(typeof(Guid), new DbType[1] { DbType.Guid }, convert));
		AddColumnTypeMetadata(new ColumnTypeMetadata("CHAR", dbTypeMapping21, MySqlDbType.Guid, isUnsigned: false, binary: false, 36, "CHAR(36)", "CHAR(36)", 0L));
		AddColumnTypeMetadata(new ColumnTypeMetadata("CHAR", dbTypeMapping21, MySqlDbType.Guid, isUnsigned: false, binary: false, 32, null, null, 0L, MySqlGuidFormat.Char32));
		AddColumnTypeMetadata(new ColumnTypeMetadata("CHAR", dbTypeMapping21, MySqlDbType.Guid, isUnsigned: false, binary: false, 36, null, null, 0L, MySqlGuidFormat.Char36));
		AddColumnTypeMetadata(new ColumnTypeMetadata("BINARY", dbTypeMapping21, MySqlDbType.Guid, isUnsigned: false, binary: true, 16, null, null, 0L, MySqlGuidFormat.Binary16));
		DbTypeMapping dbTypeMapping22 = AddDbTypeMapping(new DbTypeMapping(typeof(object), new DbType[1] { DbType.Object }));
		AddColumnTypeMetadata(new ColumnTypeMetadata("NULL", dbTypeMapping22, MySqlDbType.Null, isUnsigned: false, binary: false, 0, null, null, 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("OBJECT", dbTypeMapping22, OracleDbType.Object, isUnsigned: false, binary: false, 0, "OBJECT", "OBJECT", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("ARRAY", dbTypeMapping22, OracleDbType.Array, isUnsigned: false, binary: false, 0, "ARRAY", "ARRAY", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("VARRAY", dbTypeMapping22, OracleDbType.Array, isUnsigned: false, binary: false, 0, "VARRAY", "VARRAY", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("COLLECTION", dbTypeMapping22, OracleDbType.Array, isUnsigned: false, binary: false, 0, "COLLECTION", "COLLECTION", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("EXT", dbTypeMapping22, OracleDbType.Object, isUnsigned: false, binary: false, 0, "EXT", "EXT", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("REF", dbTypeMapping22, OracleDbType.Ref, isUnsigned: false, binary: false, 0, "REF", "REF", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("REF CURSOR", dbTypeMapping22, OracleDbType.RefCursor, isUnsigned: false, binary: false, 0, "REF CURSOR", "REF CURSOR", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("BFILE", dbTypeMapping22, OracleDbType.BFile, isUnsigned: false, binary: false, 0, "BFILE", "BFILE", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("ARRAY_AS_JSON", dbTypeMapping22, OracleDbType.ArrayAsJson, isUnsigned: false, binary: false, 0, "ARRAY_AS_JSON", "ARRAY_AS_JSON", 0L));
		AddColumnTypeMetadataForOracle(new OracleColumnTypeMetadata("OBJECT_AS_JSON", dbTypeMapping22, OracleDbType.ObjectAsJson, isUnsigned: false, binary: false, 0, "OBJECT_AS_JSON", "OBJECT_AS_JSON", 0L));
	}

	public IReadOnlyList<ColumnTypeMetadata> GetColumnTypeMetadata()
	{
		return m_columnTypeMetadata.AsReadOnly();
	}

	public ColumnTypeMetadata GetColumnTypeMetadata(MySqlDbType mySqlDbType)
	{
		return m_mySqlDbTypeToColumnTypeMetadata[mySqlDbType];
	}

	public DbType GetDbTypeForMySqlDbType(MySqlDbType mySqlDbType)
	{
		return m_mySqlDbTypeToColumnTypeMetadata[mySqlDbType].DbTypeMapping.DbTypes[0];
	}

	public OracleColumnTypeMetadata GetColumnTypeMetadata(OracleDbType oracleDbType)
	{
		return m_OracleDbTypeToColumnTypeMetadata[oracleDbType];
	}

	/// <summary>
	/// Oracle-mode: infer <see cref="T:System.Data.DbType" /> from <see cref="T:OceanBase.OracleDbType" /> (ODP.NET compatible).
	/// See "Table 3-16 Inference of System.Data.DbType from OracleDbType".
	/// </summary>
	public static DbType GetDbTypeForOracleDbType(OracleDbType oracleDbType)
	{
		switch (oracleDbType)
		{
		case OracleDbType.BFile:
		case OracleDbType.Blob:
		case OracleDbType.Clob:
		case OracleDbType.IntervalDS:
		case OracleDbType.NClob:
		case OracleDbType.RefCursor:
		case OracleDbType.Array:
		case OracleDbType.Object:
		case OracleDbType.Ref:
		case OracleDbType.ArrayAsJson:
		case OracleDbType.ObjectAsJson:
		case OracleDbType.Vector:
		case OracleDbType.VectorInt8:
		case OracleDbType.VectorFloat32:
		case OracleDbType.VectorFloat64:
		case OracleDbType.VectorBinary:
			return DbType.Object;
		case OracleDbType.Single:
		case OracleDbType.BinaryFloat:
			return DbType.Single;
		case OracleDbType.Double:
		case OracleDbType.BinaryDouble:
			return DbType.Double;
		case OracleDbType.Boolean:
			return DbType.Boolean;
		case OracleDbType.Byte:
			return DbType.Byte;
		case OracleDbType.Char:
		case OracleDbType.NChar:
			return DbType.StringFixedLength;
		case OracleDbType.Date:
			return DbType.Date;
		case OracleDbType.Decimal:
			return DbType.Decimal;
		case OracleDbType.Int16:
			return DbType.Int16;
		case OracleDbType.Int32:
			return DbType.Int32;
		case OracleDbType.Int64:
			return DbType.Int64;
		case OracleDbType.IntervalYM:
			return DbType.Int64;
		case OracleDbType.XmlType:
		case OracleDbType.Json:
			return DbType.String;
		case OracleDbType.Long:
		case OracleDbType.NVarchar2:
		case OracleDbType.Varchar2:
			return DbType.String;
		case OracleDbType.LongRaw:
		case OracleDbType.Raw:
			return DbType.Binary;
		case OracleDbType.TimeStamp:
		case OracleDbType.TimeStampLTZ:
			return DbType.DateTime;
		case OracleDbType.TimeStampTZ:
			return DbType.DateTimeOffset;
		default:
			return DbType.Object;
		}
	}

	public MySqlDbType GetMySqlDbTypeForDbType(DbType dbType)
	{
		foreach (KeyValuePair<MySqlDbType, ColumnTypeMetadata> mySqlDbTypeToColumnTypeMetadatum in m_mySqlDbTypeToColumnTypeMetadata)
		{
			if (mySqlDbTypeToColumnTypeMetadatum.Value.DbTypeMapping.DbTypes.Contains(dbType, null))
			{
				return mySqlDbTypeToColumnTypeMetadatum.Key;
			}
		}
		return MySqlDbType.VarChar;
	}

	/// <summary>
	/// Oracle-mode: infer <see cref="T:OceanBase.OracleDbType" /> from <see cref="T:System.Data.DbType" /> (ODP.NET compatible).
	/// See "Table 3-17 Inference of OracleDbType from DbType".
	/// </summary>
	public static OracleDbType GetOracleDbTypeForDbType(DbType dbType)
	{
		return dbType switch
		{
			DbType.Binary => OracleDbType.Raw, 
			DbType.Boolean => OracleDbType.Boolean, 
			DbType.Byte => OracleDbType.Byte, 
			DbType.Date => OracleDbType.Date, 
			DbType.DateTime => OracleDbType.TimeStamp, 
			DbType.DateTimeOffset => OracleDbType.TimeStampTZ, 
			DbType.Decimal => OracleDbType.Decimal, 
			DbType.Double => OracleDbType.Double, 
			DbType.Guid => OracleDbType.Raw, 
			DbType.Int16 => OracleDbType.Int16, 
			DbType.Int32 => OracleDbType.Int32, 
			DbType.Int64 => OracleDbType.Int64, 
			DbType.Object => OracleDbType.Object, 
			DbType.Single => OracleDbType.Single, 
			DbType.String => OracleDbType.Varchar2, 
			DbType.StringFixedLength => OracleDbType.Char, 
			DbType.AnsiString => OracleDbType.Varchar2, 
			DbType.AnsiStringFixedLength => OracleDbType.Char, 
			DbType.Time => OracleDbType.TimeStamp, 
			_ => throw new NotSupportedException($"DbType.{dbType} is not supported for OracleDbType inference."), 
		};
	}

	private DbTypeMapping AddDbTypeMapping(DbTypeMapping dbTypeMapping)
	{
		m_dbTypeMappingsByClrType[dbTypeMapping.ClrType] = dbTypeMapping;
		if (dbTypeMapping.DbTypes != null)
		{
			DbType[] dbTypes = dbTypeMapping.DbTypes;
			foreach (DbType key in dbTypes)
			{
				m_dbTypeMappingsByDbType[key] = dbTypeMapping;
			}
		}
		return dbTypeMapping;
	}

	private void AddColumnTypeMetadata(ColumnTypeMetadata columnTypeMetadata)
	{
		m_columnTypeMetadata.Add(columnTypeMetadata);
		string key = columnTypeMetadata.CreateLookupKey();
		m_columnTypeMetadataLookup.TryAdd(key, columnTypeMetadata);
		m_mySqlDbTypeToColumnTypeMetadata.TryAdd(columnTypeMetadata.MySqlDbType, columnTypeMetadata);
	}

	private void AddColumnTypeMetadataForOracle(OracleColumnTypeMetadata oracleColumnTypeMetadata)
	{
		m_oracleColumnTypeMetadata.Add(oracleColumnTypeMetadata);
		string key = oracleColumnTypeMetadata.CreateLookupKey();
		m_oracleColumnTypeMetadataLookup.TryAdd(key, oracleColumnTypeMetadata);
		m_OracleDbTypeToColumnTypeMetadata.TryAdd(oracleColumnTypeMetadata.OracleDbType, oracleColumnTypeMetadata);
	}

	internal DbTypeMapping? GetDbTypeMapping(Type clrType)
	{
		if (clrType.IsEnum)
		{
			clrType = Enum.GetUnderlyingType(clrType);
		}
		m_dbTypeMappingsByClrType.TryGetValue(clrType, out DbTypeMapping value);
		return value;
	}

	internal DbTypeMapping? GetDbTypeMapping(DbType dbType)
	{
		m_dbTypeMappingsByDbType.TryGetValue(dbType, out DbTypeMapping value);
		return value;
	}

	public DbTypeMapping? GetDbTypeMapping(string columnTypeName, bool unsigned = false, int length = 0)
	{
		return GetColumnTypeMetadata(columnTypeName, unsigned, length, MySqlGuidFormat.Default)?.DbTypeMapping;
	}

	public MySqlDbType GetMySqlDbType(string typeName, bool unsigned, int length, MySqlGuidFormat guidFormat)
	{
		return GetColumnTypeMetadata(typeName, unsigned, length, guidFormat).MySqlDbType;
	}

	public OracleDbType GetOracleDbType(string typeName, bool unsigned, int length)
	{
		return GetOracleColumnTypeMetadata(typeName, unsigned, length).OracleDbType;
	}

	public bool TryGetOracleDbType(string typeName, bool unsigned, int length, out OracleDbType oracleDbType)
	{
		OracleColumnTypeMetadata oracleColumnTypeMetadata = GetOracleColumnTypeMetadata(typeName, unsigned, length);
		if (oracleColumnTypeMetadata == null)
		{
			oracleDbType = (OracleDbType)0;
			return false;
		}
		oracleDbType = oracleColumnTypeMetadata.OracleDbType;
		return true;
	}

	private ColumnTypeMetadata? GetColumnTypeMetadata(string columnTypeName, bool unsigned, int length, MySqlGuidFormat guidFormat)
	{
		if (m_columnTypeMetadataLookup.TryGetValue(ColumnTypeMetadata.CreateLookupKey(columnTypeName, unsigned, length, guidFormat), out ColumnTypeMetadata value))
		{
			return value;
		}
		if (guidFormat != MySqlGuidFormat.Default && m_columnTypeMetadataLookup.TryGetValue(ColumnTypeMetadata.CreateLookupKey(columnTypeName, unsigned, length, MySqlGuidFormat.Default), out value))
		{
			return value;
		}
		if (length != 0)
		{
			m_columnTypeMetadataLookup.TryGetValue(ColumnTypeMetadata.CreateLookupKey(columnTypeName, unsigned, 0, MySqlGuidFormat.Default), out value);
		}
		return value;
	}

	private OracleColumnTypeMetadata? GetOracleColumnTypeMetadata(string columnTypeName, bool unsigned, int length)
	{
		if (m_oracleColumnTypeMetadataLookup.TryGetValue(OracleColumnTypeMetadata.CreateLookupKey(columnTypeName, unsigned, length), out OracleColumnTypeMetadata value))
		{
			return value;
		}
		if (length != 0)
		{
			m_oracleColumnTypeMetadataLookup.TryGetValue(OracleColumnTypeMetadata.CreateLookupKey(columnTypeName, unsigned, 0), out value);
		}
		return value;
	}

	public static MySqlDbType ConvertToMySqlDbType(ColumnDefinitionPayload columnDefinition, bool treatTinyAsBoolean, MySqlGuidFormat guidFormat)
	{
		bool flag = (columnDefinition.ColumnFlags & ColumnFlags.Unsigned) != 0;
		switch (columnDefinition.ColumnType)
		{
		case ColumnType.Tiny:
			if (!treatTinyAsBoolean || columnDefinition.ColumnLength != 1 || flag)
			{
				if (!flag)
				{
					return MySqlDbType.Byte;
				}
				return MySqlDbType.UByte;
			}
			return MySqlDbType.Bool;
		case ColumnType.Int24:
			if (!flag)
			{
				return MySqlDbType.Int24;
			}
			return MySqlDbType.UInt24;
		case ColumnType.Long:
			if (!flag)
			{
				return MySqlDbType.Int32;
			}
			return MySqlDbType.UInt32;
		case ColumnType.Longlong:
			if (!flag)
			{
				return MySqlDbType.Int64;
			}
			return MySqlDbType.UInt64;
		case ColumnType.Bit:
			return MySqlDbType.Bit;
		case ColumnType.String:
			if (guidFormat == MySqlGuidFormat.Char36 && columnDefinition.ColumnLength / ProtocolUtility.GetBytesPerCharacter(columnDefinition.CharacterSet) == 36)
			{
				return MySqlDbType.Guid;
			}
			if (guidFormat == MySqlGuidFormat.Char32 && columnDefinition.ColumnLength / ProtocolUtility.GetBytesPerCharacter(columnDefinition.CharacterSet) == 32)
			{
				return MySqlDbType.Guid;
			}
			if ((columnDefinition.ColumnFlags & ColumnFlags.Enum) != 0)
			{
				return MySqlDbType.Enum;
			}
			if ((columnDefinition.ColumnFlags & ColumnFlags.Set) != 0)
			{
				return MySqlDbType.Set;
			}
			goto case ColumnType.VarChar;
		case ColumnType.VarChar:
		case ColumnType.TinyBlob:
		case ColumnType.MediumBlob:
		case ColumnType.LongBlob:
		case ColumnType.Blob:
		case ColumnType.VarString:
		{
			ColumnType columnType = columnDefinition.ColumnType;
			if (columnDefinition.CharacterSet == CharacterSet.Binary)
			{
				bool flag2 = (uint)(guidFormat - 4) <= 2u;
				if (flag2 && columnDefinition.ColumnLength == 16)
				{
					return MySqlDbType.Guid;
				}
				return columnType switch
				{
					ColumnType.String => MySqlDbType.Binary, 
					ColumnType.VarString => MySqlDbType.VarBinary, 
					ColumnType.TinyBlob => MySqlDbType.TinyBlob, 
					ColumnType.Blob => MySqlDbType.Blob, 
					ColumnType.MediumBlob => MySqlDbType.MediumBlob, 
					_ => MySqlDbType.LongBlob, 
				};
			}
			return columnType switch
			{
				ColumnType.String => MySqlDbType.String, 
				ColumnType.VarString => MySqlDbType.VarChar, 
				ColumnType.TinyBlob => MySqlDbType.TinyText, 
				ColumnType.Blob => MySqlDbType.Text, 
				ColumnType.MediumBlob => MySqlDbType.MediumText, 
				_ => MySqlDbType.LongText, 
			};
		}
		case ColumnType.Json:
			return MySqlDbType.JSON;
		case ColumnType.Short:
			if (!flag)
			{
				return MySqlDbType.Int16;
			}
			return MySqlDbType.UInt16;
		case ColumnType.Date:
		case ColumnType.NewDate:
			return MySqlDbType.Date;
		case ColumnType.DateTime:
			return MySqlDbType.DateTime;
		case ColumnType.Timestamp:
			return MySqlDbType.Timestamp;
		case ColumnType.Time:
			return MySqlDbType.Time;
		case ColumnType.Year:
			return MySqlDbType.Year;
		case ColumnType.Float:
			return MySqlDbType.Float;
		case ColumnType.Double:
			return MySqlDbType.Double;
		case ColumnType.Decimal:
			return MySqlDbType.Decimal;
		case ColumnType.NewDecimal:
			return MySqlDbType.NewDecimal;
		case ColumnType.Geometry:
			return MySqlDbType.Geometry;
		case ColumnType.Null:
			return MySqlDbType.Null;
		case ColumnType.Enum:
			return MySqlDbType.Enum;
		case ColumnType.Set:
			return MySqlDbType.Set;
		case ColumnType.Vector:
			return MySqlDbType.Vector;
		default:
			throw new NotImplementedException($"ConvertToMySqlDbType for {columnDefinition.ColumnType} is not implemented");
		}
	}

	public static OracleDbType ConvertToOracleDbType(ColumnDefinitionPayload columnDefinition, bool treatTinyAsBoolean)
	{
		bool flag = (columnDefinition.ColumnFlags & ColumnFlags.Unsigned) != 0;
		switch (columnDefinition.ColumnType)
		{
		case ColumnType.Tiny:
			if (!treatTinyAsBoolean || columnDefinition.ColumnLength != 1 || flag)
			{
				return OracleDbType.Byte;
			}
			return OracleDbType.Boolean;
		case ColumnType.Short:
			return OracleDbType.Int16;
		case ColumnType.Long:
		case ColumnType.Int24:
			return OracleDbType.Int32;
		case ColumnType.Longlong:
			return OracleDbType.Int64;
		case ColumnType.Bit:
			return OracleDbType.Raw;
		case ColumnType.Decimal:
		case ColumnType.NewDecimal:
		case ColumnType.OracleNumber:
			return OracleDbType.Decimal;
		case ColumnType.Double:
			return OracleDbType.Double;
		case ColumnType.OracleBinaryFloat:
			return OracleDbType.BinaryFloat;
		case ColumnType.OracleBinaryDouble:
			return OracleDbType.BinaryDouble;
		case ColumnType.OracleNumberFloat:
			return OracleDbType.Double;
		case ColumnType.NewDate:
			return OracleDbType.Date;
		case ColumnType.Timestamp:
		case ColumnType.DateTime:
		case ColumnType.Timestamp2:
		case ColumnType.DateTime2:
			return OracleDbType.TimeStamp;
		case ColumnType.OracleTimeStampTZ:
			return OracleDbType.TimeStampTZ;
		case ColumnType.OracleTimeStampLTZ:
			return OracleDbType.TimeStampLTZ;
		case ColumnType.OracleTimeStampNano:
			return OracleDbType.TimeStamp;
		case ColumnType.VarChar:
		case ColumnType.Enum:
		case ColumnType.Set:
		case ColumnType.VarString:
		case ColumnType.String:
		case ColumnType.OracleVarchar2:
			return OracleDbType.Varchar2;
		case ColumnType.OracleNVarChar2:
			return OracleDbType.NVarchar2;
		case ColumnType.OracleNChar:
			return OracleDbType.NChar;
		case ColumnType.OracleRaw:
			return OracleDbType.Raw;
		case ColumnType.OracleBlob:
			return OracleDbType.Blob;
		case ColumnType.OracleClob:
			return OracleDbType.Clob;
		case ColumnType.OracleIntervalDS:
			return OracleDbType.IntervalDS;
		case ColumnType.OracleIntervalYM:
			return OracleDbType.IntervalYM;
		case ColumnType.OracleCursor:
			return OracleDbType.RefCursor;
		case ColumnType.OracleArray:
			return OracleDbType.Array;
		case ColumnType.OracleComplex:
		case ColumnType.OracleStruct:
			return OracleDbType.Object;
		case ColumnType.Json:
			return OracleDbType.Json;
		case ColumnType.Vector:
			return OracleDbType.Vector;
		case ColumnType.Geometry:
			return OracleDbType.Object;
		case ColumnType.Null:
			return OracleDbType.Object;
		case ColumnType.OracleRowId:
			return OracleDbType.Varchar2;
		default:
			return OracleDbType.Object;
		}
	}

	public static ushort ConvertToColumnTypeAndFlags(MySqlDbType dbType, MySqlGuidFormat guidFormat)
	{
		bool flag = (((uint)(dbType - 501) <= 2u || (uint)(dbType - 508) <= 1u) ? true : false);
		bool flag2 = flag;
		ColumnType columnType;
		switch (dbType)
		{
		case MySqlDbType.Bool:
		case MySqlDbType.Byte:
		case MySqlDbType.UByte:
			columnType = ColumnType.Tiny;
			break;
		case MySqlDbType.Int16:
		case MySqlDbType.UInt16:
			columnType = ColumnType.Short;
			break;
		case MySqlDbType.Int24:
		case MySqlDbType.UInt24:
			columnType = ColumnType.Int24;
			break;
		case MySqlDbType.Int32:
		case MySqlDbType.UInt32:
			columnType = ColumnType.Long;
			break;
		case MySqlDbType.Int64:
		case MySqlDbType.UInt64:
			columnType = ColumnType.Longlong;
			break;
		case MySqlDbType.Bit:
			columnType = ColumnType.Bit;
			break;
		case MySqlDbType.Guid:
			flag = (uint)(guidFormat - 2) <= 1u;
			columnType = (flag ? ColumnType.String : ColumnType.Blob);
			break;
		case MySqlDbType.Enum:
		case MySqlDbType.Set:
			columnType = ColumnType.String;
			break;
		case MySqlDbType.String:
		case MySqlDbType.Binary:
			columnType = ColumnType.String;
			break;
		case MySqlDbType.VarString:
		case MySqlDbType.VarChar:
		case MySqlDbType.VarBinary:
			columnType = ColumnType.VarString;
			break;
		case MySqlDbType.TinyBlob:
		case MySqlDbType.TinyText:
			columnType = ColumnType.TinyBlob;
			break;
		case MySqlDbType.Blob:
		case MySqlDbType.Text:
			columnType = ColumnType.Blob;
			break;
		case MySqlDbType.MediumBlob:
		case MySqlDbType.MediumText:
			columnType = ColumnType.MediumBlob;
			break;
		case MySqlDbType.LongBlob:
		case MySqlDbType.LongText:
			columnType = ColumnType.LongBlob;
			break;
		case MySqlDbType.JSON:
			columnType = ColumnType.Json;
			break;
		case MySqlDbType.Date:
		case MySqlDbType.Newdate:
			columnType = ColumnType.Date;
			break;
		case MySqlDbType.DateTime:
			columnType = ColumnType.DateTime;
			break;
		case MySqlDbType.Timestamp:
			columnType = ColumnType.Timestamp;
			break;
		case MySqlDbType.Time:
			columnType = ColumnType.Time;
			break;
		case MySqlDbType.Year:
			columnType = ColumnType.Year;
			break;
		case MySqlDbType.Float:
			columnType = ColumnType.Float;
			break;
		case MySqlDbType.Double:
			columnType = ColumnType.Double;
			break;
		case MySqlDbType.Decimal:
			columnType = ColumnType.Decimal;
			break;
		case MySqlDbType.NewDecimal:
			columnType = ColumnType.NewDecimal;
			break;
		case MySqlDbType.Geometry:
			columnType = ColumnType.Geometry;
			break;
		case MySqlDbType.Null:
			columnType = ColumnType.Null;
			break;
		case MySqlDbType.Vector:
			columnType = ColumnType.Vector;
			break;
		default:
			throw new NotImplementedException($"ConvertToColumnTypeAndFlags for {dbType} is not implemented");
		}
		return (ushort)((byte)columnType | (flag2 ? 32768 : 0));
	}

	public static ushort ConvertToOracleColumnTypeAndFlags(OracleDbType dbType)
	{
		ColumnType columnType;
		switch (dbType)
		{
		case OracleDbType.Byte:
		case OracleDbType.Boolean:
			columnType = ColumnType.Tiny;
			break;
		case OracleDbType.Int64:
			columnType = ColumnType.Longlong;
			break;
		case OracleDbType.Int32:
			columnType = ColumnType.Long;
			break;
		case OracleDbType.Int16:
			columnType = ColumnType.Short;
			break;
		case OracleDbType.Single:
			columnType = ColumnType.Float;
			break;
		case OracleDbType.Decimal:
			columnType = ColumnType.NewDecimal;
			break;
		case OracleDbType.Double:
			columnType = ColumnType.Double;
			break;
		case OracleDbType.Date:
			columnType = ColumnType.DateTime;
			break;
		case OracleDbType.TimeStamp:
			columnType = ColumnType.OracleTimeStampNano;
			break;
		case OracleDbType.TimeStampTZ:
			columnType = ColumnType.OracleTimeStampTZ;
			break;
		case OracleDbType.TimeStampLTZ:
			columnType = ColumnType.OracleTimeStampLTZ;
			break;
		case OracleDbType.Varchar2:
			columnType = ColumnType.VarString;
			break;
		case OracleDbType.Char:
			columnType = ColumnType.String;
			break;
		case OracleDbType.NVarchar2:
			columnType = ColumnType.OracleNVarChar2;
			break;
		case OracleDbType.NChar:
			columnType = ColumnType.OracleNChar;
			break;
		case OracleDbType.LongRaw:
		case OracleDbType.Raw:
			columnType = ColumnType.OracleRaw;
			break;
		case OracleDbType.Blob:
			columnType = ColumnType.OracleBlob;
			break;
		case OracleDbType.Clob:
			columnType = ColumnType.OracleClob;
			break;
		case OracleDbType.IntervalDS:
			columnType = ColumnType.OracleIntervalDS;
			break;
		case OracleDbType.IntervalYM:
			columnType = ColumnType.OracleIntervalYM;
			break;
		case OracleDbType.RefCursor:
			columnType = ColumnType.Null;
			break;
		case OracleDbType.Array:
		case OracleDbType.Object:
			columnType = ColumnType.OracleComplex;
			break;
		case OracleDbType.BinaryDouble:
			columnType = ColumnType.Double;
			break;
		case OracleDbType.BinaryFloat:
			columnType = ColumnType.Float;
			break;
		case OracleDbType.Json:
			columnType = ColumnType.Json;
			break;
		default:
			throw new NotImplementedException($"ConvertToOracleColumnTypeAndFlags for {dbType} is not implemented");
		}
		return (byte)columnType;
	}

	internal IEnumerable<ColumnTypeMetadata> GetColumnMappings()
	{
		return m_columnTypeMetadataLookup.Values.AsEnumerable();
	}
}
