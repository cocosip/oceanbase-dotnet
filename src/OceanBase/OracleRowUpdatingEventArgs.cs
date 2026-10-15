using System.Data;
using System.Data.Common;

namespace OceanBase;

public sealed class OracleRowUpdatingEventArgs : RowUpdatingEventArgs
{
	public new OracleCommand? Command => (OracleCommand)base.Command;

	public OracleRowUpdatingEventArgs(DataRow row, IDbCommand? command, StatementType statementType, DataTableMapping tableMapping)
		: base(row, command, statementType, tableMapping)
	{
	}
}
