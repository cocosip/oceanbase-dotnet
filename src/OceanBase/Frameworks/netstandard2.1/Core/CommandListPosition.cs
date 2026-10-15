using System;
using System.Collections.Generic;

namespace OceanBase.Core;

/// <summary>
/// <see cref="T:OceanBase.Core.CommandListPosition" /> encapsulates a list of commands and the current position within that list.
/// </summary>
internal struct CommandListPosition
{
	/// <summary>
	/// The commands in this list; either a singular command (<see cref="T:OceanBase.MySqlCommand" /> or <see cref="T:OceanBase.OracleCommand" />)
	/// or a <see cref="T:System.Collections.Generic.IReadOnlyList`1" />.
	/// </summary>
	private readonly object m_commands;

	/// <summary>
	/// The number of commands in the list.
	/// </summary>
	public readonly int CommandCount;

	/// <summary>
	/// Associated prepared statements of commands
	/// </summary>
	public PreparedStatements? PreparedStatements;

	/// <summary>
	/// The index of the current command.
	/// </summary>
	public int CommandIndex;

	/// <summary>
	/// If the current command is a prepared statement, the index of the current prepared statement for that command.
	/// </summary>
	public int PreparedStatementIndex;

	/// <summary>
	/// Retrieve the last used prepared statement
	/// </summary>
	public PreparedStatement? LastUsedPreparedStatement;

	/// <summary>
	/// True when the outgoing packet was COM_STMT_PREPARE_EXECUTE (command kind 0xA1).
	/// The server response starts with OceanBase prepare/execute header payload (first byte 0x00, non-standard OK layout),
	/// not a standard MySQL OK packet, even when reusing a non-zero statement id.
	/// </summary>
	public bool ExpectOraclePrepareExecuteResponse;

	public CommandListPosition(object commands)
	{
		LastUsedPreparedStatement = null;
		ExpectOraclePrepareExecuteResponse = false;
		m_commands = commands;
		int commandCount = ((commands is MySqlCommand) ? 1 : ((commands is OracleCommand) ? 1 : ((commands is IReadOnlyList<MySqlBatchCommand> readOnlyList) ? readOnlyList.Count : 0)));
		CommandCount = commandCount;
		PreparedStatements = null;
		CommandIndex = 0;
		PreparedStatementIndex = 0;
	}

	public readonly CommandFacade CommandAt(int index)
	{
		object commands = m_commands;
		if (!(commands is MySqlCommand command))
		{
			if (!(commands is OracleCommand command2))
			{
				if (commands is IReadOnlyList<MySqlBatchCommand> readOnlyList)
				{
					return new CommandFacade(readOnlyList[index]);
				}
			}
			else if (index == 0)
			{
				return new CommandFacade(command2);
			}
		}
		else if (index == 0)
		{
			return new CommandFacade(command);
		}
		throw new ArgumentOutOfRangeException("index");
	}
}
