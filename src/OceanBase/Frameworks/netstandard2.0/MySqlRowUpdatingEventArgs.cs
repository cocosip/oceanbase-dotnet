using System.Data;
using System.Data.Common;

namespace OceanBase;

public sealed class MySqlRowUpdatingEventArgs : RowUpdatingEventArgs
{
	public new MySqlCommand? Command => (MySqlCommand)base.Command;

	public MySqlRowUpdatingEventArgs(DataRow row, IDbCommand? command, StatementType statementType, DataTableMapping tableMapping)
		: base(row, command, statementType, tableMapping)
	{
	}
}
