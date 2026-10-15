using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace OceanBase.Core;

internal sealed class DataReaderValuesEnumerator(IDataReader dataReader) : IValuesEnumerator
{
	public int FieldCount => dataReader.FieldCount;

	public static IValuesEnumerator Create(IDataReader dataReader)
	{
		if (!(dataReader is DbDataReader dbDataReader))
		{
			return new DataReaderValuesEnumerator(dataReader);
		}
		return new DbDataReaderValuesEnumerator(dbDataReader);
	}

	public ValueTask<bool> MoveNextAsync()
	{
		return new ValueTask<bool>(MoveNext());
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
