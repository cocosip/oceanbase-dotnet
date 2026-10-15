using System.Data.Common;
using System.Threading.Tasks;

namespace OceanBase.Core;

internal sealed class DbDataReaderValuesEnumerator(DbDataReader dataReader) : IValuesEnumerator
{
	public int FieldCount => dataReader.FieldCount;

	public ValueTask<bool> MoveNextAsync()
	{
		return new ValueTask<bool>(dataReader.ReadAsync());
	}

	public bool MoveNext()
	{
		return dataReader.Read();
	}

	public void GetValues(object[] values)
	{
		dataReader.GetValues(values);
	}
}
