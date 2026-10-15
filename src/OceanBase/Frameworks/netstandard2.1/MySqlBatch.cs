using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OceanBase.Core;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;

namespace OceanBase;

/// <summary>
/// <para><see cref="T:OceanBase.MySqlBatch" /> implements the new
/// <a href="https://github.com/dotnet/runtime/issues/28633">ADO.NET batching API</a>.
/// <strong>It is currently experimental</strong> and may change in the future.</para>
/// <para>When using MariaDB (10.2 or later), the commands will be sent in a single batch, reducing network
/// round-trip time. With other MySQL Servers, this may be no more efficient than executing the commands
/// individually.</para>
/// <para>Example usage:</para>
/// <code>
/// await using var connection = new MySqlConnection("...connection string...");
/// await connection.OpenAsync();
///
/// using var batch = new MySqlBatch(connection)
/// {
/// 	BatchCommands =
/// 	{
/// 		new MySqlBatchCommand("INSERT INTO departments(name) VALUES(@name);")
/// 		{
/// 			Parameters =
/// 			{
/// 				new MySqlParameter("@name", "Sales"),
/// 			},
/// 		},
/// 		new MySqlBatchCommand("SET @dept_id = last_insert_id()"),
/// 		new MySqlBatchCommand("INSERT INTO employees(name, department_id) VALUES(@name, @dept_id);")
/// 		{
/// 			Parameters =
/// 			{
/// 				new MySqlParameter("@name", "Jim Halpert"),
/// 			},
/// 		},
/// 	 	new MySqlBatchCommand("INSERT INTO employees(name, department_id) VALUES(@name, @dept_id);")
/// 		{
/// 			Parameters =
/// 			{
/// 				new MySqlParameter("@name", "Dwight Schrute"),
/// 			},
/// 		},
/// 	},
///  };
///  await batch.ExecuteNonQueryAsync();
/// </code>
/// </summary>
/// <remarks>The proposed ADO.NET API that <see cref="T:OceanBase.MySqlBatch" /> is based on is not finalized. This API is experimental and may change in the future.</remarks>
public sealed class MySqlBatch : ICancellableCommand, IDisposable
{
	[CompilerGenerated]
	private int _003CTimeout_003Ek__BackingField;

	private readonly int m_commandId;

	private bool m_isDisposed;

	private Action? m_cancelAction;

	private Action? m_cancelForCommandTimeoutAction;

	private uint m_cancelTimerId;

	private bool m_commandTimedOut;

	public MySqlConnection? Connection { get; set; }

	public MySqlTransaction? Transaction { get; set; }

	/// <summary>
	/// The collection of commands that will be executed in the batch.
	/// </summary>
	public MySqlBatchCommandCollection BatchCommands { get; }

	public int Timeout
	{
		[CompilerGenerated]
		get
		{
			return _003CTimeout_003Ek__BackingField;
		}
		set
		{
			_003CTimeout_003Ek__BackingField = value;
			((ICancellableCommand)this).EffectiveCommandTimeout = null;
		}
	}

	internal CommandBehavior CurrentCommandBehavior { get; set; }

	int ICancellableCommand.CommandId => m_commandId;

	int ICancellableCommand.CommandTimeout => Timeout;

	int? ICancellableCommand.EffectiveCommandTimeout { get; set; }

	int ICancellableCommand.CancelAttemptCount { get; set; }

	bool ICancellableCommand.IsTimedOut => Volatile.Read(ref m_commandTimedOut);

	private bool IsPrepared
	{
		get
		{
			foreach (MySqlBatchCommand batchCommand in BatchCommands)
			{
				if (Connection.Session.TryGetPreparedStatement(batchCommand.CommandText) == null)
				{
					return false;
				}
			}
			return true;
		}
	}

	private IOBehavior AsyncIOBehavior => Connection?.AsyncIOBehavior ?? IOBehavior.Asynchronous;

	ServerSession? ICancellableCommand.Session
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// Initializes a new <see cref="T:OceanBase.MySqlBatch" /> object. The <see cref="P:OceanBase.MySqlBatch.Connection" /> property must be set before this object can be used.
	/// </summary>
	public MySqlBatch()
		: this(null, null)
	{
	}

	/// <summary>
	/// Initializes a new <see cref="T:OceanBase.MySqlBatch" /> object, setting the <see cref="P:OceanBase.MySqlBatch.Connection" /> and <see cref="P:OceanBase.MySqlBatch.Transaction" /> if specified.
	/// </summary>
	/// <param name="connection">(Optional) The <see cref="T:OceanBase.MySqlConnection" /> to use.</param>
	/// <param name="transaction">(Optional) The <see cref="T:OceanBase.MySqlTransaction" /> to use.</param>
	public MySqlBatch(MySqlConnection? connection = null, MySqlTransaction? transaction = null)
	{
		Connection = connection;
		Transaction = transaction;
		BatchCommands = new MySqlBatchCommandCollection();
		m_commandId = ICancellableCommandExtensions.GetNextId();
	}

	/// <summary>
	/// Executes all the commands in the batch, returning a <see cref="T:OceanBase.MySqlDataReader" /> that can iterate
	/// over the result sets. If multiple resultsets are returned, use <see cref="M:OceanBase.MySqlDataReader.NextResult" />
	/// to access them.
	/// </summary>
	public MySqlDataReader ExecuteReader(CommandBehavior commandBehavior = CommandBehavior.Default)
	{
		return (MySqlDataReader)ExecuteDbDataReader(commandBehavior);
	}

	/// <summary>
	/// Executes all the commands in the batch, returning a <see cref="T:OceanBase.MySqlDataReader" /> that can iterate
	/// over the result sets. If multiple resultsets are returned, use <see cref="M:OceanBase.MySqlDataReader.NextResultAsync(System.Threading.CancellationToken)" />
	/// to access them.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> containing the result of the asynchronous operation.</returns>
	public async Task<MySqlDataReader> ExecuteReaderAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return (MySqlDataReader)(await ExecuteDbDataReaderAsync(CommandBehavior.Default, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}

	private DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
	{
		this.ResetCommandTimeout();
		return ExecuteReaderAsync(behavior, IOBehavior.Synchronous, CancellationToken.None).Result;
	}

	private async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
	{
		this.ResetCommandTimeout();
		using (((ICancellableCommand)this).RegisterCancel(cancellationToken))
		{
			return await ExecuteReaderAsync(behavior, AsyncIOBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private ValueTask<MySqlDataReader> ExecuteReaderAsync(CommandBehavior behavior, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (!IsValid(out Exception exception))
		{
			return ValueTaskExtensions.FromException<MySqlDataReader>(exception);
		}
		CurrentCommandBehavior = behavior;
		foreach (MySqlBatchCommand batchCommand in BatchCommands)
		{
			batchCommand.Batch = this;
		}
		ICommandPayloadCreator payloadCreator = (IsPrepared ? SingleCommandPayloadCreator.Instance : ConcatenatedCommandPayloadCreator.Instance);
		return CommandExecutor.ExecuteMysqlReaderAsync(new CommandListPosition(BatchCommands.Commands), payloadCreator, behavior, null, ioBehavior, cancellationToken);
	}

	public int ExecuteNonQuery()
	{
		return ExecuteNonQueryAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
	}

	public object? ExecuteScalar()
	{
		return ExecuteScalarAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
	}

	public Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return ExecuteNonQueryAsync(AsyncIOBehavior, cancellationToken);
	}

	public Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return ExecuteScalarAsync(AsyncIOBehavior, cancellationToken);
	}

	public void Prepare()
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
			DoPrepareAsync(IOBehavior.Synchronous, default(CancellationToken)).GetAwaiter().GetResult();
		}
	}

	public Task PrepareAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return PrepareAsync(AsyncIOBehavior, cancellationToken);
	}

	public void Cancel()
	{
		Connection?.Cancel(this, m_commandId, isCancel: true);
	}

	public void Dispose()
	{
		m_isDisposed = true;
	}

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
		Volatile.Write(ref m_commandTimedOut, value: false);
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
		Cancel();
	}

	private async Task<int> ExecuteNonQueryAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		this.ResetCommandTimeout();
		using (((ICancellableCommand)this).RegisterCancel(cancellationToken))
		{
			using MySqlDataReader reader = await ExecuteReaderAsync(CommandBehavior.Default, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			while (await reader.ReadAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false) || await reader.NextResultAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
			{
			}
			return reader.RecordsAffected;
		}
	}

	private async Task<object?> ExecuteScalarAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		this.ResetCommandTimeout();
		using (((ICancellableCommand)this).RegisterCancel(cancellationToken))
		{
			bool hasSetResult = false;
			object result = null;
			using MySqlDataReader reader = await ExecuteReaderAsync(CommandBehavior.Default, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
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

	private bool IsValid([NotNullWhen(false)] out Exception? exception)
	{
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
				exception = new InvalidOperationException("The transaction associated with this batch is not the connection's active transaction; see https://mysqlconnector.net/trans");
			}
			else if (BatchCommands.Count == 0)
			{
				exception = new InvalidOperationException("BatchCommands must contain a command");
			}
			else
			{
				exception = GetExceptionForInvalidCommands();
			}
		}
		return exception == null;
	}

	private bool NeedsPrepare(out Exception? exception)
	{
		if (m_isDisposed)
		{
			exception = new ObjectDisposedException(GetType().Name);
		}
		else if (Connection == null)
		{
			exception = new InvalidOperationException("Connection property must be non-null.");
		}
		else if (Connection.State != ConnectionState.Open)
		{
			exception = new InvalidOperationException($"Connection must be Open; current state is {Connection.State}");
		}
		else if (BatchCommands.Count == 0)
		{
			exception = new InvalidOperationException("BatchCommands must contain a command");
		}
		else if (Connection.HasActiveReader)
		{
			exception = new InvalidOperationException("Cannot call Prepare when there is an open DataReader for this command; it must be closed first.");
		}
		else
		{
			exception = GetExceptionForInvalidCommands();
		}
		if (exception == null)
		{
			return !Connection.IgnorePrepare;
		}
		return false;
	}

	private InvalidOperationException? GetExceptionForInvalidCommands()
	{
		foreach (MySqlBatchCommand batchCommand in BatchCommands)
		{
			if (batchCommand == null)
			{
				return new InvalidOperationException("BatchCommands must not contain null");
			}
			if (string.IsNullOrWhiteSpace(batchCommand.CommandText))
			{
				return new InvalidOperationException("CommandText must be specified on each batch command");
			}
		}
		return null;
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
		return DoPrepareAsync(ioBehavior, cancellationToken);
	}

	private async Task DoPrepareAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		foreach (MySqlBatchCommand batchCommand in BatchCommands)
		{
			if (((ICommand)batchCommand).CommandType != CommandType.Text)
			{
				throw new NotSupportedException("Only CommandType.Text is currently supported by MySqlBatch.Prepare");
			}
			batchCommand.Batch = this;
			if (Connection.Session.TryGetPreparedStatement(((ICommand)batchCommand).CommandText) == null)
			{
				await Connection.Session.PrepareAsync(new CommandFacade(batchCommand), ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
	}
}
