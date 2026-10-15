using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OceanBase.Core;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;

namespace OceanBase;

/// <summary>
/// <see cref="T:OceanBase.MySqlCommand" /> represents a SQL statement or stored procedure name
/// to execute against a MySQL database.
/// </summary>
public sealed class MySqlCommand : DbCommand, IMySqlCommand, ICommand, ICancellableCommand, ICloneable
{
	private readonly int m_commandId;

	private string m_commandText = "";

	private MySqlConnection? m_connection;

	private CommandType m_commandType = CommandType.Text;

	private bool m_isDisposed;

	private MySqlParameterCollection? m_parameterCollection;

	private MySqlAttributeCollection? m_attributeCollection;

	private int? m_commandTimeout;

	private CommandBehavior m_commandBehavior;

	private Action? m_cancelAction;

	private Action? m_cancelForCommandTimeoutAction;

	private uint m_cancelTimerId;

	private bool m_commandTimedOut;

	/// <summary>
	/// The collection of <see cref="T:OceanBase.MySqlParameter" /> objects for this command.
	/// </summary>
	public new MySqlParameterCollection Parameters => m_parameterCollection ?? (m_parameterCollection = new MySqlParameterCollection());

	MySqlParameterCollection? IMySqlCommand.RawParameters => m_parameterCollection;

	DbParameterCollection? ICommand.RawParameters => ((IMySqlCommand)this).RawParameters;

	/// <summary>
	/// The collection of <see cref="T:OceanBase.MySqlAttribute" /> objects for this command.
	/// </summary>
	public MySqlAttributeCollection Attributes => m_attributeCollection ?? (m_attributeCollection = new MySqlAttributeCollection());

	MySqlAttributeCollection? ICommand.RawAttributes => m_attributeCollection;

	bool ICommand.AllowUserVariables => AllowUserVariables;

	internal bool AllowUserVariables { get; set; }

	internal bool NoActivity { get; set; }

	/// <summary>
	/// Gets or sets the command text to execute.
	/// </summary>
	/// <remarks>If <see cref="P:OceanBase.MySqlCommand.CommandType" /> is <see cref="F:System.Data.CommandType.Text" />, this is one or more SQL statements to execute.
	/// If <see cref="P:OceanBase.MySqlCommand.CommandType" /> is <see cref="F:System.Data.CommandType.StoredProcedure" />, this is the name of the stored procedure; any
	/// special characters in the stored procedure name must be quoted or escaped.</remarks>
	public override string CommandText
	{
		get
		{
			return m_commandText;
		}
		[param: AllowNull]
		set
		{
			if (Connection?.ActiveCommandId == m_commandId)
			{
				throw new InvalidOperationException("Cannot set MySqlCommand.CommandText when there is an open DataReader for this command; it must be closed first.");
			}
			m_commandText = value ?? "";
		}
	}

	public bool IsPrepared => ((ICommand)this).TryGetPreparedStatements() != null;

	public new MySqlTransaction? Transaction { get; set; }

	public new MySqlConnection? Connection
	{
		get
		{
			return m_connection;
		}
		set
		{
			if (m_connection?.ActiveCommandId == m_commandId)
			{
				throw new InvalidOperationException("Cannot set MySqlCommand.Connection when there is an open DataReader for this command; it must be closed first.");
			}
			m_connection = value;
		}
	}

	MySqlConnection? IMySqlCommand.Connection => Connection;

	DbConnection? ICommand.Connection => ((IMySqlCommand)this).Connection;

	/// <inheritdoc />
	public override int CommandTimeout
	{
		get
		{
			return Math.Min(m_commandTimeout ?? Connection?.DefaultCommandTimeout ?? 0, 2147483);
		}
		set
		{
			ExceptionExtensions.ThrowIfNegative(value, "value");
			m_commandTimeout = value;
			((ICancellableCommand)this).EffectiveCommandTimeout = null;
		}
	}

	/// <inheritdoc />
	public override CommandType CommandType
	{
		get
		{
			return m_commandType;
		}
		set
		{
			if (value != CommandType.Text && value != CommandType.StoredProcedure)
			{
				throw new ArgumentException("CommandType must be Text or StoredProcedure.", "value");
			}
			m_commandType = value;
		}
	}

	/// <inheritdoc />
	public override bool DesignTimeVisible { get; set; }

	/// <inheritdoc />
	public override UpdateRowSource UpdatedRowSource { get; set; }

	/// <summary>
	/// Holds the first automatically-generated ID for a value inserted in an <c>AUTO_INCREMENT</c> column in the last statement.
	/// </summary>
	/// <remarks>
	/// See <a href="https://dev.mysql.com/doc/refman/8.0/en/information-functions.html#function_last-insert-id"><c>LAST_INSERT_ID()</c></a> for more information.
	/// </remarks>
	public long LastInsertedId { get; private set; }

	long ICommand.LastInsertedId => LastInsertedId;

	protected override DbConnection? DbConnection
	{
		get
		{
			return Connection;
		}
		set
		{
			Connection = (MySqlConnection)value;
		}
	}

	protected override DbParameterCollection DbParameterCollection => Parameters;

	protected override DbTransaction? DbTransaction
	{
		get
		{
			return Transaction;
		}
		set
		{
			Transaction = (MySqlTransaction)value;
		}
	}

	bool ICancellableCommand.IsTimedOut => Volatile.Read(ref m_commandTimedOut);

	int ICancellableCommand.CommandId => m_commandId;

	int? ICancellableCommand.EffectiveCommandTimeout { get; set; }

	int ICancellableCommand.CancelAttemptCount { get; set; }

	ICancellableCommand ICommand.CancellableCommand => this;

	private IOBehavior AsyncIOBehavior => Connection?.AsyncIOBehavior ?? IOBehavior.Asynchronous;

	CommandBehavior ICommand.CommandBehavior => m_commandBehavior;

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

	ILogger ICommand.Logger => Connection.LoggingConfiguration.CommandLogger;

	ServerSession? ICancellableCommand.Session => ((ICommand)this).CancellableCommand.Session;

	/// <summary>
	/// Initializes a new instance of the <see cref="T:OceanBase.MySqlCommand" /> class.
	/// </summary>
	public MySqlCommand()
		: this(null, null, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="T:OceanBase.MySqlCommand" /> class, setting <see cref="P:OceanBase.MySqlCommand.CommandText" /> to <paramref name="commandText" />.
	/// </summary>
	/// <param name="commandText">The text to assign to <see cref="P:OceanBase.MySqlCommand.CommandText" />.</param>
	public MySqlCommand(string? commandText)
		: this(commandText, null, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="T:OceanBase.MySqlCommand" /> class with the specified <see cref="T:OceanBase.MySqlConnection" /> and <see cref="T:OceanBase.MySqlTransaction" />.
	/// </summary>
	/// <param name="connection">The <see cref="T:OceanBase.MySqlConnection" /> to use.</param>
	/// <param name="transaction">The active <see cref="T:OceanBase.MySqlTransaction" />, if any.</param>
	public MySqlCommand(MySqlConnection? connection, MySqlTransaction? transaction)
		: this(null, connection, transaction)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="T:OceanBase.MySqlCommand" /> class with the specified command text and <see cref="T:OceanBase.MySqlConnection" />.
	/// </summary>
	/// <param name="commandText">The text to assign to <see cref="P:OceanBase.MySqlCommand.CommandText" />.</param>
	/// <param name="connection">The <see cref="T:OceanBase.MySqlConnection" /> to use.</param>
	public MySqlCommand(string? commandText, MySqlConnection? connection)
		: this(commandText, connection, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="T:OceanBase.MySqlCommand" /> class with the specified command text,<see cref="T:OceanBase.MySqlConnection" />, and <see cref="T:OceanBase.MySqlTransaction" />.
	/// </summary>
	/// <param name="commandText">The text to assign to <see cref="P:OceanBase.MySqlCommand.CommandText" />.</param>
	/// <param name="connection">The <see cref="T:OceanBase.MySqlConnection" /> to use.</param>
	/// <param name="transaction">The active <see cref="T:OceanBase.MySqlTransaction" />, if any.</param>
	public MySqlCommand(string? commandText, MySqlConnection? connection, MySqlTransaction? transaction)
	{
		GC.SuppressFinalize(this);
		m_commandId = ICancellableCommandExtensions.GetNextId();
		CommandText = commandText ?? "";
		Connection = connection;
		Transaction = transaction;
		CommandType = CommandType.Text;
	}

	private MySqlCommand(MySqlCommand other)
		: this(other.CommandText, other.Connection, other.Transaction)
	{
		GC.SuppressFinalize(this);
		m_commandTimeout = other.m_commandTimeout;
		((ICancellableCommand)this).EffectiveCommandTimeout = null;
		CommandType = other.CommandType;
		DesignTimeVisible = other.DesignTimeVisible;
		UpdatedRowSource = other.UpdatedRowSource;
		m_parameterCollection = other.CloneRawParameters();
		m_attributeCollection = other.CloneRawAttributes();
	}

	public new MySqlParameter CreateParameter()
	{
		return (MySqlParameter)base.CreateParameter();
	}

	/// <inheritdoc />
	public override void Cancel()
	{
		Connection?.Cancel(this, m_commandId, isCancel: true);
	}

	/// <summary>
	/// Executes this command on the associated <see cref="T:OceanBase.MySqlConnection" />.
	/// </summary>
	/// <returns>The number of rows affected.</returns>
	/// <remarks>For UPDATE, INSERT, and DELETE statements, the return value is the number of rows affected by the command.
	/// For stored procedures, the return value is the number of rows affected by the last statement in the stored procedure,
	/// or zero if the last statement is a SELECT. For all other types of statements, the return value is -1.</remarks>
	public override int ExecuteNonQuery()
	{
		return ExecuteNonQueryAsync(IOBehavior.Synchronous, CancellationToken.None).Result;
	}

	public override object? ExecuteScalar()
	{
		return ExecuteScalarAsync(IOBehavior.Synchronous, CancellationToken.None).Result;
	}

	public new MySqlDataReader ExecuteReader()
	{
		return ExecuteReaderAsync(CommandBehavior.Default, IOBehavior.Synchronous, default(CancellationToken)).Result;
	}

	public new MySqlDataReader ExecuteReader(CommandBehavior commandBehavior)
	{
		return ExecuteReaderAsync(commandBehavior, IOBehavior.Synchronous, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <inheritdoc />
	public override void Prepare()
	{
		if (!NeedsPrepare(out Exception exception))
		{
			if (exception != null)
			{
				throw exception;
			}
		}
		else
		{
			Connection.Session.PrepareAsync(this, IOBehavior.Synchronous, default(CancellationToken)).GetAwaiter().GetResult();
		}
	}

	public override Task PrepareAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return PrepareAsync(AsyncIOBehavior, cancellationToken);
	}

	internal MySqlParameterCollection? CloneRawParameters()
	{
		if (m_parameterCollection == null)
		{
			return null;
		}
		MySqlParameterCollection mySqlParameterCollection = new MySqlParameterCollection();
		foreach (MySqlParameter item in (IEnumerable<MySqlParameter>)m_parameterCollection)
		{
			mySqlParameterCollection.Add(item.Clone());
		}
		return mySqlParameterCollection;
	}

	private MySqlAttributeCollection? CloneRawAttributes()
	{
		if (m_attributeCollection == null)
		{
			return null;
		}
		MySqlAttributeCollection mySqlAttributeCollection = new MySqlAttributeCollection();
		foreach (MySqlAttribute item in m_attributeCollection)
		{
			mySqlAttributeCollection.Add(new MySqlAttribute(item.AttributeName, item.Value));
		}
		return mySqlAttributeCollection;
	}

	private Task PrepareAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (!NeedsPrepare(out Exception exception))
		{
			if (exception != null)
			{
				return Task.FromException(exception);
			}
			return Task.CompletedTask;
		}
		return Connection.Session.PrepareAsync(this, ioBehavior, cancellationToken);
	}

	private bool NeedsPrepare(out Exception? exception)
	{
		exception = null;
		if (Connection == null)
		{
			exception = new InvalidOperationException("Connection property must be non-null.");
		}
		else if (Connection.State != ConnectionState.Open)
		{
			exception = new InvalidOperationException($"Connection must be Open; current state is {Connection.State}");
		}
		else if (string.IsNullOrWhiteSpace(CommandText))
		{
			exception = new InvalidOperationException("CommandText must be specified");
		}
		else if (Connection?.HasActiveReader ?? false)
		{
			exception = new InvalidOperationException("Cannot call Prepare when there is an open DataReader for this command's connection; it must be closed first.");
		}
		if (exception != null || Connection.IgnorePrepare)
		{
			return false;
		}
		CommandType commandType = CommandType;
		if (commandType != CommandType.StoredProcedure && commandType != CommandType.Text)
		{
			exception = new NotSupportedException("Only CommandType.Text and CommandType.StoredProcedure are currently supported by MySqlCommand.Prepare.");
			return false;
		}
		return Connection.Session.TryGetPreparedStatement(CommandText) == null;
	}

	void ICommand.SetLastInsertedId(long lastInsertedId)
	{
		LastInsertedId = lastInsertedId;
	}

	protected override DbParameter CreateDbParameter()
	{
		return new MySqlParameter();
	}

	protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
	{
		return ExecuteReaderAsync(behavior, IOBehavior.Synchronous, CancellationToken.None).Result;
	}

	/// <summary>
	/// Executes this command asynchronously on the associated <see cref="T:OceanBase.MySqlConnection" />.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>For UPDATE, INSERT, and DELETE statements, the return value is the number of rows affected by the command.
	/// For stored procedures, the return value is the number of rows affected by the last statement in the stored procedure,
	/// or zero if the last statement is a SELECT. For all other types of statements, the return value is -1.</remarks>
	public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
	{
		return ExecuteNonQueryAsync(AsyncIOBehavior, cancellationToken).AsTask();
	}

	internal async ValueTask<int> ExecuteNonQueryAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		Volatile.Write(ref m_commandTimedOut, value: false);
		this.ResetCommandTimeout();
		using (((ICancellableCommand)this).RegisterCancel(cancellationToken))
		{
			using MySqlDataReader reader = await ExecuteReaderNoResetTimeoutAsync(CommandBehavior.Default, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			while (await reader.ReadAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false) || await reader.NextResultAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
			{
			}
			return reader.RecordsAffected;
		}
	}

	public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
	{
		return ExecuteScalarAsync(AsyncIOBehavior, cancellationToken).AsTask();
	}

	internal async ValueTask<object?> ExecuteScalarAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		Volatile.Write(ref m_commandTimedOut, value: false);
		this.ResetCommandTimeout();
		using (((ICancellableCommand)this).RegisterCancel(cancellationToken))
		{
			bool hasSetResult = false;
			object result = null;
			using MySqlDataReader reader = await ExecuteReaderNoResetTimeoutAsync(CommandBehavior.Default, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			do
			{
				bool flag = await reader.ReadAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (!hasSetResult)
				{
					if (flag)
					{
						result = reader.GetValue(0);
					}
					hasSetResult = true;
				}
			}
			while (await reader.NextResultAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
			return result;
		}
	}

	public new Task<MySqlDataReader> ExecuteReaderAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return ExecuteReaderAsync(CommandBehavior.Default, AsyncIOBehavior, cancellationToken).AsTask();
	}

	public new Task<MySqlDataReader> ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken = default(CancellationToken))
	{
		return ExecuteReaderAsync(behavior, AsyncIOBehavior, cancellationToken).AsTask();
	}

	protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
	{
		return await ExecuteReaderAsync(behavior, AsyncIOBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	internal async ValueTask<MySqlDataReader> ExecuteReaderAsync(CommandBehavior behavior, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		Volatile.Write(ref m_commandTimedOut, value: false);
		this.ResetCommandTimeout();
		using (((ICancellableCommand)this).RegisterCancel(cancellationToken))
		{
			return await ExecuteReaderNoResetTimeoutAsync(behavior, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	internal ValueTask<MySqlDataReader> ExecuteReaderNoResetTimeoutAsync(CommandBehavior behavior, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (!IsValid(out Exception exception))
		{
			return ValueTaskExtensions.FromException<MySqlDataReader>(exception);
		}
		Activity activity = (NoActivity ? null : Connection.Session.StartActivity("Execute", "db.statement", CommandText));
		m_commandBehavior = behavior;
		return CommandExecutor.ExecuteMysqlReaderAsync(new CommandListPosition(this), SingleCommandPayloadCreator.Instance, behavior, activity, ioBehavior, cancellationToken);
	}

	public MySqlCommand Clone()
	{
		return new MySqlCommand(this);
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	protected override void Dispose(bool disposing)
	{
		m_isDisposed = true;
		base.Dispose(disposing);
	}

	public override ValueTask DisposeAsync()
	{
		Dispose();
		return default(ValueTask);
	}

	/// <summary>
	/// Registers <see cref="M:OceanBase.MySqlCommand.Cancel" /> as a callback with <paramref name="cancellationToken" /> if cancellation is supported.
	/// </summary>
	/// <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" />.</param>
	/// <returns>An object that must be disposed to revoke the cancellation registration.</returns>
	/// <remarks>This method is more efficient than calling <code>token.Register(Command.Cancel)</code> because it avoids
	/// unnecessary allocations.</remarks>
	CancellationTokenRegistration ICancellableCommand.RegisterCancel(CancellationToken cancellationToken)
	{
		if (!cancellationToken.CanBeCanceled)
		{
			return default(CancellationTokenRegistration);
		}
		if (m_cancelAction == null)
		{
			m_cancelAction = Cancel;
		}
		return cancellationToken.Register(m_cancelAction);
	}

	void ICancellableCommand.SetTimeout(int milliseconds)
	{
		if (m_cancelTimerId != 0)
		{
			TimerQueue.Instance.Remove(m_cancelTimerId);
		}
		if (milliseconds != int.MaxValue)
		{
			if (m_cancelForCommandTimeoutAction == null)
			{
				m_cancelForCommandTimeoutAction = CancelCommandForTimeout;
			}
			m_cancelTimerId = TimerQueue.Instance.Add(milliseconds, m_cancelForCommandTimeoutAction);
		}
	}

	private void CancelCommandForTimeout()
	{
		Volatile.Write(ref m_commandTimedOut, value: true);
		Connection?.Cancel(this, m_commandId, isCancel: false);
	}

	private bool IsValid([NotNullWhen(false)] out Exception? exception)
	{
		exception = null;
		if (m_isDisposed)
		{
			exception = new ObjectDisposedException(GetType().Name);
		}
		else if (Connection == null)
		{
			exception = new InvalidOperationException("Connection property must be non-null.");
		}
		else
		{
			ConnectionState state = Connection.State;
			if (state != ConnectionState.Open && state != ConnectionState.Connecting)
			{
				exception = new InvalidOperationException($"Connection must be Open; current state is {Connection.State}");
			}
			else if (!Connection.IgnoreCommandTransaction && Transaction != Connection.CurrentTransaction)
			{
				exception = new InvalidOperationException("The transaction associated with this command is not the connection's active transaction; see https://mysqlconnector.net/trans");
			}
			else if (string.IsNullOrWhiteSpace(CommandText))
			{
				exception = new InvalidOperationException("CommandText must be specified");
			}
		}
		return exception == null;
	}

	PreparedStatements? ICommand.TryGetPreparedStatements()
	{
		if (CommandType != CommandType.Text || string.IsNullOrWhiteSpace(CommandText) || Connection == null || Connection.State != ConnectionState.Open)
		{
			return null;
		}
		return Connection.Session.TryGetPreparedStatement(CommandText);
	}
}
