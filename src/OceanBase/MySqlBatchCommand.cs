using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using OceanBase.Core;

namespace OceanBase;

public sealed class MySqlBatchCommand : DbBatchCommand, IMySqlCommand, ICommand
{
	private MySqlParameterCollection? m_parameterCollection;

	private long m_lastInsertedId;

	public override string CommandText { get; set; }

	public override CommandType CommandType { get; set; }

	public override int RecordsAffected => 0;

	public new MySqlParameterCollection Parameters => m_parameterCollection ?? (m_parameterCollection = new MySqlParameterCollection());

	public override bool CanCreateParameter => true;

	protected override DbParameterCollection DbParameterCollection => Parameters;

	bool ICommand.AllowUserVariables => false;

	CommandBehavior ICommand.CommandBehavior => Batch.CurrentCommandBehavior;

	MySqlParameterCollection? IMySqlCommand.RawParameters => m_parameterCollection;

	DbParameterCollection? ICommand.RawParameters => ((IMySqlCommand)this).RawParameters;

	MySqlAttributeCollection? ICommand.RawAttributes => null;

	MySqlConnection? IMySqlCommand.Connection => Batch?.Connection;

	DbConnection? ICommand.Connection => ((IMySqlCommand)this).Connection;

	long ICommand.LastInsertedId => m_lastInsertedId;

	MySqlParameterCollection? IMySqlCommand.OutParameters { get; set; }

	DbParameterCollection? ICommand.OutParameters
	{
		get
		{
			return ((IMySqlCommand)this).OutParameters;
		}
		set
		{
			((IMySqlCommand)this).OutParameters = (MySqlParameterCollection)value;
		}
	}

	MySqlParameter? IMySqlCommand.ReturnParameter { get; set; }

	DbParameter? ICommand.ReturnParameter
	{
		get
		{
			return ((IMySqlCommand)this).ReturnParameter;
		}
		set
		{
			((IMySqlCommand)this).ReturnParameter = (MySqlParameter)value;
		}
	}

	ICancellableCommand ICommand.CancellableCommand => Batch;

	ILogger ICommand.Logger => Batch.Connection.LoggingConfiguration.CommandLogger;

	internal MySqlBatch? Batch { get; set; }

	public MySqlBatchCommand()
		: this(null)
	{
	}

	public MySqlBatchCommand(string? commandText)
	{
		CommandText = commandText ?? "";
		CommandType = CommandType.Text;
	}

	public override DbParameter CreateParameter()
	{
		return new MySqlParameter();
	}

	PreparedStatements? ICommand.TryGetPreparedStatements()
	{
		return null;
	}

	void ICommand.SetLastInsertedId(long lastInsertedId)
	{
		m_lastInsertedId = lastInsertedId;
	}
}
