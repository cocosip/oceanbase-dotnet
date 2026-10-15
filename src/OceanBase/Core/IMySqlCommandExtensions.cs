using System;
using System.Data;

namespace OceanBase.Core;

internal static class IMySqlCommandExtensions
{
	public static StatementPreparerOptions CreateStatementPreparerOptions(this IMySqlCommand command)
	{
		MySqlConnection connection = command.Connection;
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
		if (command.CommandType == CommandType.StoredProcedure)
		{
			statementPreparerOptions |= StatementPreparerOptions.AllowOutputParameters;
		}
		if (connection.NoBackslashEscapes)
		{
			statementPreparerOptions |= StatementPreparerOptions.NoBackslashEscapes;
		}
		StatementPreparerOptions statementPreparerOptions2 = statementPreparerOptions;
		return (StatementPreparerOptions)((int)statementPreparerOptions2 | (connection.GuidFormat switch
		{
			MySqlGuidFormat.Char36 => 32, 
			MySqlGuidFormat.Char32 => 64, 
			MySqlGuidFormat.Binary16 => 96, 
			MySqlGuidFormat.TimeSwapBinary16 => 128, 
			MySqlGuidFormat.LittleEndianBinary16 => 160, 
			_ => 0, 
		}));
	}
}
