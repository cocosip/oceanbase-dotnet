using System.Data;
using System.Data.Common;

namespace OceanBase;

public sealed class OracleRowUpdatedEventArgs : RowUpdatedEventArgs
{
	public new OracleCommand? Command => (OracleCommand)base.Command;

	public OracleRowUpdatedEventArgs(DataRow row, IDbCommand? command, StatementType statementType, DataTableMapping tableMapping)
		: base(row, command, statementType, tableMapping)
	{
	}
}
