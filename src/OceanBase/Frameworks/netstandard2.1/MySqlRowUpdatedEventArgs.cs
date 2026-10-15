using System.Data;
using System.Data.Common;

namespace OceanBase;

public sealed class MySqlRowUpdatedEventArgs : RowUpdatedEventArgs
{
	public new MySqlCommand? Command => (MySqlCommand)base.Command;

	public MySqlRowUpdatedEventArgs(DataRow row, IDbCommand? command, StatementType statementType, DataTableMapping tableMapping)
		: base(row, command, statementType, tableMapping)
	{
	}
}
