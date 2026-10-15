using System.Runtime.CompilerServices;

namespace OceanBase.Core;

internal sealed class ColumnTypeMetadata(string dataTypeName, DbTypeMapping dbTypeMapping, MySqlDbType mySqlDbType, bool isUnsigned = false, bool binary = false, int length = 0, string? simpleDataTypeName = null, string? createFormat = null, long columnSize = 0L, MySqlGuidFormat guidFormat = MySqlGuidFormat.Default)
{
	public string DataTypeName { get; } = dataTypeName;

	public string SimpleDataTypeName { get; } = simpleDataTypeName ?? dataTypeName;

	public string CreateFormat { get; } = createFormat ?? (dataTypeName + (isUnsigned ? " UNSIGNED" : ""));

	public DbTypeMapping DbTypeMapping { get; } = dbTypeMapping;

	public MySqlDbType MySqlDbType { get; } = mySqlDbType;

	public bool Binary { get; } = binary;

	public long ColumnSize { get; } = columnSize;

	public bool IsUnsigned { get; } = isUnsigned;

	public int Length { get; } = length;

	public MySqlGuidFormat GuidFormat { get; } = guidFormat;

	public static string CreateLookupKey(string columnTypeName, bool isUnsigned, int length, MySqlGuidFormat guidFormat)
	{
		return $"{columnTypeName}|{(isUnsigned ? "u" : "s")}|{length}|{GetGuidFormatLookupKey(guidFormat)}";
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string GetGuidFormatLookupKey(MySqlGuidFormat guidFormat)
	{
		switch (guidFormat)
		{
		case MySqlGuidFormat.Char36:
			return "c36";
		case MySqlGuidFormat.Char32:
			return "c32";
		case MySqlGuidFormat.Binary16:
		case MySqlGuidFormat.TimeSwapBinary16:
		case MySqlGuidFormat.LittleEndianBinary16:
			return "b16";
		default:
			return "def";
		}
	}

	public string CreateLookupKey()
	{
		return CreateLookupKey(DataTypeName, IsUnsigned, Length, GuidFormat);
	}
}
