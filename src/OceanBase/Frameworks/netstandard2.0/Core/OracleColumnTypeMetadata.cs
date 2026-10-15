namespace OceanBase.Core;

internal sealed class OracleColumnTypeMetadata(string dataTypeName, DbTypeMapping dbTypeMapping, OracleDbType oracleDbType, bool isUnsigned = false, bool binary = false, int length = 0, string? simpleDataTypeName = null, string? createFormat = null, long columnSize = 0L)
{
	public string DataTypeName { get; } = dataTypeName;

	public string SimpleDataTypeName { get; } = simpleDataTypeName ?? dataTypeName;

	public string CreateFormat { get; } = createFormat ?? (dataTypeName + (isUnsigned ? " UNSIGNED" : ""));

	public DbTypeMapping DbTypeMapping { get; } = dbTypeMapping;

	public OracleDbType OracleDbType { get; } = oracleDbType;

	public bool Binary { get; } = binary;

	public long ColumnSize { get; } = columnSize;

	public bool IsUnsigned { get; } = isUnsigned;

	public int Length { get; } = length;

	public static string CreateLookupKey(string columnTypeName, bool isUnsigned, int length)
	{
		return string.Format("{0}|{1}|{2}|", columnTypeName, isUnsigned ? "u" : "s", length);
	}

	public string CreateLookupKey()
	{
		return CreateLookupKey(DataTypeName, IsUnsigned, Length);
	}
}
