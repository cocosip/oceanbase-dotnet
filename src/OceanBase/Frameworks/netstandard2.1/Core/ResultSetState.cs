namespace OceanBase.Core;

internal enum ResultSetState
{
	None,
	ReadResultSetHeader,
	ReadingRows,
	HasMoreData,
	NoMoreData
}
