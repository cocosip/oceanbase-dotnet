using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Core;

/// <summary>
/// <see cref="T:OceanBase.Core.CommandFacade" /> is an internal adapter that exposes a common view over <see cref="T:OceanBase.Core.IMySqlCommand" /> and
/// <see cref="T:OceanBase.Core.IOracleCommand" /> for shared execution/payload/datareader logic.
/// </summary>
internal readonly struct CommandFacade
{
	private readonly ICommand? m_command;

	private readonly IMySqlCommand? m_mySqlCommand;

	private readonly IOracleCommand? m_oracleCommand;

	public bool IsMySqlCommand => m_mySqlCommand != null;

	public bool IsOracleCommand => m_oracleCommand != null;

	public int OracleArrayBindCount
	{
		get
		{
			if (!(m_oracleCommand is OracleCommand oracleCommand))
			{
				return 0;
			}
			return oracleCommand.ArrayBindCount;
		}
	}

	public string? CommandText => m_command?.CommandText;

	public CommandType CommandType => m_command?.CommandType ?? ((CommandType)0);

	public bool AllowUserVariables => m_command?.AllowUserVariables ?? false;

	public CommandBehavior CommandBehavior => m_command?.CommandBehavior ?? CommandBehavior.Default;

	public DbParameterCollection? RawParameters => m_command?.RawParameters;

	public MySqlAttributeCollection? RawAttributes => m_command?.RawAttributes;

	public DbParameterCollection? OutParameters
	{
		get
		{
			return m_command?.OutParameters;
		}
		set
		{
			IMySqlCommand mySqlCommand = m_mySqlCommand;
			if (mySqlCommand != null)
			{
				mySqlCommand.OutParameters = (MySqlParameterCollection)value;
				return;
			}
			IOracleCommand oracleCommand = m_oracleCommand;
			if (oracleCommand != null)
			{
				oracleCommand.OutParameters = (OracleParameterCollection)value;
				return;
			}
			throw CreateUninitializedException();
		}
	}

	public DbParameter? ReturnParameter
	{
		get
		{
			return m_command?.ReturnParameter;
		}
		set
		{
			IMySqlCommand mySqlCommand = m_mySqlCommand;
			if (mySqlCommand != null)
			{
				mySqlCommand.ReturnParameter = (MySqlParameter)value;
				return;
			}
			IOracleCommand oracleCommand = m_oracleCommand;
			if (oracleCommand != null)
			{
				oracleCommand.ReturnParameter = (OracleParameter)value;
				return;
			}
			throw CreateUninitializedException();
		}
	}

	public ICancellableCommand CancellableCommand => m_command?.CancellableCommand ?? throw CreateUninitializedException();

	public ILogger Logger => m_command?.Logger ?? throw CreateUninitializedException();

	public ConnectionFacade? Connection
	{
		get
		{
			IMySqlCommand mySqlCommand = m_mySqlCommand;
			if (mySqlCommand != null)
			{
				MySqlConnection connection = mySqlCommand.Connection;
				if (connection == null)
				{
					return null;
				}
				return new ConnectionFacade(connection);
			}
			IOracleCommand oracleCommand = m_oracleCommand;
			if (oracleCommand != null)
			{
				OracleConnection connection2 = oracleCommand.Connection;
				if (connection2 == null)
				{
					return null;
				}
				return new ConnectionFacade(connection2);
			}
			throw CreateUninitializedException();
		}
	}

	public ServerSession? Session
	{
		get
		{
			IMySqlCommand mySqlCommand = m_mySqlCommand;
			if (mySqlCommand != null)
			{
				return mySqlCommand.Connection?.Session;
			}
			IOracleCommand oracleCommand = m_oracleCommand;
			if (oracleCommand != null)
			{
				return oracleCommand.Connection?.Session;
			}
			throw CreateUninitializedException();
		}
	}

	public string SessionId => Session?.Id ?? "";

	public object InnerCommand => ((object)m_mySqlCommand) ?? ((object)(m_oracleCommand ?? throw CreateUninitializedException()));

	public MySqlParameterCollection? MySqlRawParameters => m_mySqlCommand?.RawParameters;

	public OracleParameterCollection? OracleRawParameters => m_oracleCommand?.RawParameters;

	public CommandFacade(IMySqlCommand command)
	{
		if (command == null)
		{
			throw new ArgumentNullException("command");
		}
		m_command = command;
		m_mySqlCommand = command;
		m_oracleCommand = null;
	}

	public CommandFacade(IOracleCommand command)
	{
		if (command == null)
		{
			throw new ArgumentNullException("command");
		}
		m_command = command;
		m_mySqlCommand = null;
		m_oracleCommand = command;
	}

	public PreparedStatements? TryGetPreparedStatements()
	{
		return m_command?.TryGetPreparedStatements();
	}

	public void SetLastInsertedId(long lastInsertedId)
	{
		m_command?.SetLastInsertedId(lastInsertedId);
	}

	public void SetActiveReader(DbDataReader dataReader)
	{
		IMySqlCommand mySqlCommand = m_mySqlCommand;
		if (mySqlCommand != null)
		{
			if (!(dataReader is MySqlDataReader activeReader))
			{
				throw new ArgumentException("Expected a MySqlDataReader for IMySqlCommand.", "dataReader");
			}
			mySqlCommand.Connection.SetActiveReader(activeReader);
			return;
		}
		IOracleCommand oracleCommand = m_oracleCommand;
		if (oracleCommand != null)
		{
			if (!(dataReader is OracleDataReader activeReader2))
			{
				throw new ArgumentException("Expected an OracleDataReader for IOracleCommand.", "dataReader");
			}
			oracleCommand.Connection.SetActiveReader(activeReader2);
			return;
		}
		throw CreateUninitializedException();
	}

	public void FinishQuerying(bool hasWarnings)
	{
		IMySqlCommand mySqlCommand = m_mySqlCommand;
		if (mySqlCommand != null)
		{
			mySqlCommand.Connection?.FinishQuerying(hasWarnings);
			return;
		}
		IOracleCommand oracleCommand = m_oracleCommand;
		if (oracleCommand != null)
		{
			oracleCommand.Connection?.FinishQuerying(hasWarnings);
			return;
		}
		throw CreateUninitializedException();
	}

	public ValueTask<CachedProcedure?> GetCachedProcedureAsync(string name, bool revalidateMissing, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		IMySqlCommand mySqlCommand = m_mySqlCommand;
		if (mySqlCommand == null)
		{
			IOracleCommand oracleCommand = m_oracleCommand;
			if (oracleCommand == null)
			{
				throw CreateUninitializedException();
			}
			return oracleCommand.Connection.GetCachedProcedure(name, revalidateMissing, ioBehavior, cancellationToken).AsValueTask();
		}
		return mySqlCommand.Connection.GetCachedProcedure(name, revalidateMissing, ioBehavior, cancellationToken).AsValueTask();
	}

	public StatementPreparerOptions CreateStatementPreparerOptions()
	{
		IMySqlCommand mySqlCommand = m_mySqlCommand;
		if (mySqlCommand != null)
		{
			return mySqlCommand.CreateStatementPreparerOptions();
		}
		IOracleCommand oracleCommand = m_oracleCommand;
		if (oracleCommand != null)
		{
			return oracleCommand.CreateStatementPreparerOptions();
		}
		throw CreateUninitializedException();
	}

	public void SetOracleArrayBindRowsAffected(long[]? rowsAffected)
	{
		if (m_oracleCommand is OracleCommand oracleCommand)
		{
			oracleCommand.ArrayBindRowsAffected = rowsAffected;
		}
	}

	private static InvalidOperationException CreateUninitializedException()
	{
		return new InvalidOperationException("CommandFacade must be constructed with an IMySqlCommand or IOracleCommand.");
	}
}
