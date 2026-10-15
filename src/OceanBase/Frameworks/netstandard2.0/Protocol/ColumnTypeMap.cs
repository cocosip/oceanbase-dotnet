namespace OceanBase.Protocol;

internal static class ColumnTypeMap
{
	private static readonly ColumnType[] s_mysql = CreateMySqlMap();

	private static readonly ColumnType[] s_oracle = CreateOracleMap();

	public static ColumnType FromProtocolType(byte protocolType, bool oracleMode)
	{
		if (!oracleMode)
		{
			return s_mysql[protocolType];
		}
		return s_oracle[protocolType];
	}

	private static ColumnType[] CreateMySqlMap()
	{
		ColumnType[] array = CreateIdentityMap();
		array[0] = ColumnType.Decimal;
		array[1] = ColumnType.Tiny;
		array[2] = ColumnType.Short;
		array[3] = ColumnType.Long;
		array[4] = ColumnType.Float;
		array[5] = ColumnType.Double;
		array[6] = ColumnType.Null;
		array[7] = ColumnType.Timestamp;
		array[8] = ColumnType.Longlong;
		array[9] = ColumnType.Int24;
		array[10] = ColumnType.Date;
		array[11] = ColumnType.Time;
		array[12] = ColumnType.DateTime;
		array[13] = ColumnType.Year;
		array[14] = ColumnType.NewDate;
		array[15] = ColumnType.VarChar;
		array[16] = ColumnType.Bit;
		array[245] = ColumnType.Json;
		array[246] = ColumnType.NewDecimal;
		array[247] = ColumnType.Enum;
		array[248] = ColumnType.Set;
		array[249] = ColumnType.TinyBlob;
		array[250] = ColumnType.MediumBlob;
		array[251] = ColumnType.LongBlob;
		array[252] = ColumnType.Blob;
		array[253] = ColumnType.VarString;
		array[254] = ColumnType.String;
		array[255] = ColumnType.Geometry;
		return array;
	}

	private static ColumnType[] CreateOracleMap()
	{
		ColumnType[] array = CreateIdentityMap();
		array[0] = ColumnType.Decimal;
		array[1] = ColumnType.Tiny;
		array[2] = ColumnType.Short;
		array[6] = ColumnType.Null;
		array[8] = ColumnType.Longlong;
		array[9] = ColumnType.Int24;
		array[12] = ColumnType.DateTime;
		array[14] = ColumnType.NewDate;
		array[15] = ColumnType.VarChar;
		array[16] = ColumnType.Bit;
		array[247] = ColumnType.Enum;
		array[248] = ColumnType.Set;
		array[254] = ColumnType.String;
		array[3] = ColumnType.OracleNumber;
		array[4] = ColumnType.OracleBinaryFloat;
		array[5] = ColumnType.OracleBinaryDouble;
		array[160] = ColumnType.OracleComplex;
		array[161] = ColumnType.OracleArray;
		array[162] = ColumnType.OracleStruct;
		array[163] = ColumnType.OracleCursor;
		array[200] = ColumnType.OracleTimeStampTZ;
		array[201] = ColumnType.OracleTimeStampLTZ;
		array[202] = ColumnType.OracleTimeStampNano;
		array[203] = ColumnType.OracleRaw;
		array[204] = ColumnType.OracleIntervalYM;
		array[205] = ColumnType.OracleIntervalDS;
		array[206] = ColumnType.OracleNumberFloat;
		array[207] = ColumnType.OracleNVarChar2;
		array[208] = ColumnType.OracleNChar;
		array[209] = ColumnType.OracleRowId;
		array[210] = ColumnType.OracleBlob;
		array[211] = ColumnType.OracleClob;
		array[246] = ColumnType.NewDecimal;
		array[253] = ColumnType.OracleVarchar2;
		return array;
	}

	private static ColumnType[] CreateIdentityMap()
	{
		ColumnType[] array = new ColumnType[256];
		for (int i = 0; i < 256; i++)
		{
			array[i] = (ColumnType)i;
		}
		return array;
	}
}
