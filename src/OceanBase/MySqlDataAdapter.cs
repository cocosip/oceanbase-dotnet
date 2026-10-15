using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OceanBase.Core;

namespace OceanBase;

public sealed class MySqlDataAdapter : DbDataAdapter
{
	internal sealed class InsertSqlParser : SqlParser
	{
		private readonly MySqlParameterCollection? m_parameters;

		public List<int> ParameterIndexes { get; }

		public string CommandText { get; private set; }

		public InsertSqlParser(IMySqlCommand command)
			: base(new StatementPreparer(command.CommandText, null, command.CreateStatementPreparerOptions()))
		{
			CommandText = command.CommandText;
			m_parameters = command.RawParameters;
			ParameterIndexes = new List<int>();
		}

		protected override void OnNamedParameter(int index, int length)
		{
			string parameterName = CommandText.Substring(index, length);
			int item = m_parameters?.NormalizedIndexOf(parameterName) ?? (-1);
			ParameterIndexes.Add(item);
			CommandText = string.Concat(CommandText.AsSpan(0, index), new string(' ', length).AsSpan(), CommandText.AsSpan(index + length));
		}

		protected override void OnPositionalParameter(int index)
		{
			ParameterIndexes.Add(ParameterIndexes.Count);
			CommandText = string.Concat(CommandText.AsSpan(0, index), " ".AsSpan(), CommandText.AsSpan(index + 1));
		}
	}

	private MySqlBatch? m_batch;

	public new MySqlCommand? DeleteCommand
	{
		get
		{
			return (MySqlCommand)base.DeleteCommand;
		}
		set
		{
			base.DeleteCommand = value;
		}
	}

	public new MySqlCommand? InsertCommand
	{
		get
		{
			return (MySqlCommand)base.InsertCommand;
		}
		set
		{
			base.InsertCommand = value;
		}
	}

	public new MySqlCommand? SelectCommand
	{
		get
		{
			return (MySqlCommand)base.SelectCommand;
		}
		set
		{
			base.SelectCommand = value;
		}
	}

	public new MySqlCommand? UpdateCommand
	{
		get
		{
			return (MySqlCommand)base.UpdateCommand;
		}
		set
		{
			base.UpdateCommand = value;
		}
	}

	public override int UpdateBatchSize { get; set; }

	public event MySqlRowUpdatingEventHandler? RowUpdating;

	public event MySqlRowUpdatedEventHandler? RowUpdated;

	public MySqlDataAdapter()
	{
		GC.SuppressFinalize(this);
	}

	public MySqlDataAdapter(MySqlCommand selectCommand)
		: this()
	{
		SelectCommand = selectCommand;
	}

	public MySqlDataAdapter(string selectCommandText, MySqlConnection connection)
		: this(new MySqlCommand(selectCommandText, connection))
	{
	}

	public MySqlDataAdapter(string selectCommandText, string connectionString)
		: this(new MySqlCommand(selectCommandText, new MySqlConnection(connectionString)))
	{
	}

	protected override void OnRowUpdating(RowUpdatingEventArgs value)
	{
		this.RowUpdating?.Invoke(this, (MySqlRowUpdatingEventArgs)value);
	}

	protected override void OnRowUpdated(RowUpdatedEventArgs value)
	{
		this.RowUpdated?.Invoke(this, (MySqlRowUpdatedEventArgs)value);
	}

	protected override RowUpdatingEventArgs CreateRowUpdatingEvent(DataRow dataRow, IDbCommand? command, StatementType statementType, DataTableMapping tableMapping)
	{
		return new MySqlRowUpdatingEventArgs(dataRow, command, statementType, tableMapping);
	}

	protected override RowUpdatedEventArgs CreateRowUpdatedEvent(DataRow dataRow, IDbCommand? command, StatementType statementType, DataTableMapping tableMapping)
	{
		return new MySqlRowUpdatedEventArgs(dataRow, command, statementType, tableMapping);
	}

	protected override void InitializeBatching()
	{
		m_batch = new MySqlBatch();
	}

	protected override void TerminateBatching()
	{
		m_batch?.Dispose();
		m_batch = null;
	}

	protected override int AddToBatch(IDbCommand command)
	{
		MySqlCommand mySqlCommand = (MySqlCommand)command;
		if (m_batch.Connection == null)
		{
			m_batch.Connection = mySqlCommand.Connection;
			m_batch.Transaction = mySqlCommand.Transaction;
		}
		int count = m_batch.BatchCommands.Count;
		MySqlBatchCommand mySqlBatchCommand = new MySqlBatchCommand
		{
			CommandText = command.CommandText,
			CommandType = command.CommandType
		};
		MySqlParameterCollection mySqlParameterCollection = mySqlCommand.CloneRawParameters();
		if (mySqlParameterCollection != null)
		{
			foreach (object item in mySqlParameterCollection)
			{
				mySqlBatchCommand.Parameters.Add(item);
			}
		}
		m_batch.BatchCommands.Add(mySqlBatchCommand);
		return count;
	}

	protected override void ClearBatch()
	{
		m_batch.BatchCommands.Clear();
	}

	protected override int ExecuteBatch()
	{
		MySqlCommand mySqlCommand = TryConvertToCommand(m_batch);
		if (mySqlCommand != null)
		{
			mySqlCommand.Connection = m_batch.Connection;
			mySqlCommand.Transaction = m_batch.Transaction;
			return mySqlCommand.ExecuteNonQuery();
		}
		return m_batch.ExecuteNonQuery();
	}

	internal static MySqlCommand? TryConvertToCommand(MySqlBatch batch)
	{
		if (batch.BatchCommands.Count < 1)
		{
			return null;
		}
		MySqlBatchCommand mySqlBatchCommand = batch.BatchCommands[0];
		if (mySqlBatchCommand.Parameters.Count == 0)
		{
			return null;
		}
		mySqlBatchCommand.Batch = batch;
		string commandText = mySqlBatchCommand.CommandText;
		for (int i = 1; i < batch.BatchCommands.Count; i++)
		{
			if (batch.BatchCommands[i].CommandText != commandText)
			{
				return null;
			}
		}
		if (!commandText.StartsWith("INSERT INTO ", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		Match match = Regex.Match(commandText, "\\bVALUES\\s*\\([^)]+\\)\\s*;?\\s*$", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
		if (!match.Success)
		{
			return null;
		}
		InsertSqlParser insertSqlParser = new InsertSqlParser(mySqlBatchCommand);
		insertSqlParser.Parse(commandText);
		foreach (int parameterIndex in insertSqlParser.ParameterIndexes)
		{
			if (parameterIndex < 0 || parameterIndex >= mySqlBatchCommand.Parameters.Count)
			{
				return null;
			}
		}
		string commandText2 = insertSqlParser.CommandText;
		int num = match.Index + 6;
		if (!string.IsNullOrWhiteSpace(commandText2.Substring(num, commandText2.Length - num).Trim().TrimEnd(';')
			.Trim()
			.TrimStart('(')
			.TrimEnd(')')
			.Replace(",", "")))
		{
			return null;
		}
		MySqlCommand mySqlCommand = new MySqlCommand();
		StringBuilder stringBuilder = new StringBuilder(commandText.Substring(0, match.Index + 6));
		int num2 = 0;
		for (int j = 0; j < batch.BatchCommands.Count; j++)
		{
			MySqlBatchCommand mySqlBatchCommand2 = batch.BatchCommands[j];
			if (j != 0)
			{
				stringBuilder.Append(',');
			}
			stringBuilder.Append('(');
			for (int k = 0; k < insertSqlParser.ParameterIndexes.Count; k++)
			{
				if (k != 0)
				{
					stringBuilder.Append(',');
				}
				string text = "@p" + num2.ToString(CultureInfo.InvariantCulture);
				stringBuilder.Append(text);
				num2++;
				MySqlParameter mySqlParameter = mySqlBatchCommand2.Parameters[insertSqlParser.ParameterIndexes[k]].Clone();
				mySqlParameter.ParameterName = text;
				mySqlCommand.Parameters.Add(mySqlParameter);
			}
			stringBuilder.Append(')');
		}
		stringBuilder.Append(';');
		mySqlCommand.CommandText = stringBuilder.ToString();
		return mySqlCommand;
	}
}
