using System;
using System.Data;

namespace OceanBase.Core;

internal static class IOracleCommandExtensions
{
	public static StatementPreparerOptions CreateStatementPreparerOptions(this IOracleCommand command)
	{
		OracleConnection connection = command.Connection;
		StatementPreparerOptions statementPreparerOptions = StatementPreparerOptions.None;
		if (connection.AllowUserVariables || command.CommandType == CommandType.StoredProcedure || command.AllowUserVariables)
		{
			statementPreparerOptions |= StatementPreparerOptions.AllowUserVariables;
		}
		if (connection.DateTimeKind == DateTimeKind.Utc)
		{
			statementPreparerOptions |= StatementPreparerOptions.DateTimeUtc;
		}
		else if (connection.DateTimeKind == DateTimeKind.Local)
		{
			statementPreparerOptions |= StatementPreparerOptions.DateTimeLocal;
		}
		statementPreparerOptions |= StatementPreparerOptions.AllowOutputParameters;
		return statementPreparerOptions | StatementPreparerOptions.NoBackslashEscapes;
	}
}
