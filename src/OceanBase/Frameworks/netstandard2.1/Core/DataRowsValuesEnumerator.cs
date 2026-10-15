using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace OceanBase.Core;

internal sealed class DataRowsValuesEnumerator(IEnumerable<DataRow> dataRows, int columnCount) : IValuesEnumerator
{
	private readonly IEnumerator<DataRow> m_dataRows = dataRows.GetEnumerator();

	public int FieldCount { get; } = columnCount;

	public static IValuesEnumerator Create(DataTable dataTable)
	{
		return new DataRowsValuesEnumerator(from DataRow x in dataTable.Rows
			where x != null
			select (x), dataTable.Columns.Count);
	}

	public ValueTask<bool> MoveNextAsync()
	{
		return new ValueTask<bool>(MoveNext());
	}

	public bool MoveNext()
	{
		if (m_dataRows.MoveNext())
		{
			return true;
		}
		m_dataRows.Dispose();
		return false;
	}

	public void GetValues(object[] values)
	{
		DataRow current = m_dataRows.Current;
		for (int i = 0; i < FieldCount; i++)
		{
			values[i] = current[i];
		}
	}
}
