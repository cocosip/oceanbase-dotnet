using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OceanBase.Logging;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;

namespace OceanBase;

/// <summary>
/// <see cref="T:OceanBase.OracleTransaction" /> represents an in-progress transaction on a Oracle Server.
/// </summary>
public sealed class OracleTransaction : DbTransaction
{
	private readonly ILogger m_logger;

	private bool m_isDisposed;

	/// <summary>
	/// Gets the <see cref="T:OceanBase.OracleConnection" /> that this transaction is associated with.
	/// </summary>
	public new OracleConnection? Connection { get; private set; }

	/// <summary>
	/// Gets the <see cref="T:OceanBase.OracleConnection" /> that this transaction is associated with.
	/// </summary>
	protected override DbConnection? DbConnection => Connection;

	/// <summary>
	/// Gets the <see cref="P:OceanBase.OracleTransaction.IsolationLevel" /> of this transaction. This value is set from <see cref="M:OceanBase.OracleConnection.BeginTransaction(System.Data.IsolationLevel)" />
	/// or any other overload that specifies an <see cref="P:OceanBase.OracleTransaction.IsolationLevel" />.
	/// </summary>
	public override IsolationLevel IsolationLevel { get; }

	/// <summary>
	/// Commits the database transaction.
	/// </summary>
	public override void Commit()
	{
		CommitAsync(IOBehavior.Synchronous, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Asynchronously commits the database transaction.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task" /> representing the asynchronous operation.</returns>
	public override Task CommitAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return CommitAsync(Connection?.AsyncIOBehavior ?? IOBehavior.Asynchronous, cancellationToken);
	}

	private async Task CommitAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		VerifyValid();
		using Activity activity = Connection.Session.StartActivity("Commit");
		Log.CommittingTransaction(m_logger, Connection.Session.Id);
		try
		{
			using (OracleCommand cmd = new OracleCommand("commit", Connection, this)
			{
				NoActivity = true
			})
			{
				await cmd.ExecuteNonQueryAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			Connection.CurrentTransaction = null;
			Log.CommittedTransaction(m_logger, Connection.Session.Id);
			await Connection.RestoreAutoCommitAfterExplicitTransactionAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			Connection = null;
		}
		catch (Exception exception) when (activity?.IsAllDataRequested ?? false)
		{
			activity.SetException(exception);
			throw;
		}
	}

	/// <summary>
	/// Rolls back the database transaction.
	/// </summary>
	public override void Rollback()
	{
		RollbackAsync(IOBehavior.Synchronous, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Asynchronously rolls back the database transaction.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task" /> representing the asynchronous operation.</returns>
	public override Task RollbackAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return RollbackAsync(Connection?.AsyncIOBehavior ?? IOBehavior.Asynchronous, cancellationToken);
	}

	private async Task RollbackAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		VerifyValid();
		await DoRollback(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		await Connection.RestoreAutoCommitAfterExplicitTransactionAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		Connection.CurrentTransaction = null;
		Connection = null;
	}

	/// <summary>
	/// Removes the named transaction savepoint with the specified <paramref name="savepointName" />. No commit or rollback occurs.
	/// </summary>
	/// <param name="savepointName">The savepoint name.</param>
	/// <remarks>The proposed ADO.NET API that this is based on is not finalized; this API may change in the future.</remarks>
	public override void Release(string savepointName)
	{
		ExecuteSavepointAsync("release ", savepointName, IOBehavior.Synchronous, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Asynchronously removes the named transaction savepoint with the specified <paramref name="savepointName" />. No commit or rollback occurs.
	/// </summary>
	/// <param name="savepointName">The savepoint name.</param>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task" /> representing the asynchronous operation.</returns>
	/// <remarks>The proposed ADO.NET API that this is based on is not finalized; this API may change in the future.</remarks>
	public override Task ReleaseAsync(string savepointName, CancellationToken cancellationToken = default(CancellationToken))
	{
		return ExecuteSavepointAsync("release ", savepointName, Connection?.AsyncIOBehavior ?? IOBehavior.Asynchronous, cancellationToken);
	}

	/// <summary>
	/// Rolls back the current transaction to the savepoint with the specified <paramref name="savepointName" /> without aborting the transaction.
	/// </summary>
	/// <param name="savepointName">The savepoint name.</param>
	/// <remarks><para>The name must have been created with <see cref="M:OceanBase.OracleTransaction.Save(System.String)" />, but not released by calling <see cref="M:OceanBase.OracleTransaction.Release(System.String)" />.</para>
	/// <para>The proposed ADO.NET API that this is based on is not finalized; this API may change in the future.</para></remarks>
	public override void Rollback(string savepointName)
	{
		ExecuteSavepointAsync("rollback to ", savepointName, IOBehavior.Synchronous, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Asynchronously rolls back the current transaction to the savepoint with the specified <paramref name="savepointName" /> without aborting the transaction.
	/// </summary>
	/// <param name="savepointName">The savepoint name.</param>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task" /> representing the asynchronous operation.</returns>
	/// <remarks><para>The name must have been created with <see cref="M:OceanBase.OracleTransaction.SaveAsync(System.String,System.Threading.CancellationToken)" />, but not released by calling <see cref="M:OceanBase.OracleTransaction.ReleaseAsync(System.String,System.Threading.CancellationToken)" />.</para>
	/// <para>The proposed ADO.NET API that this is based on is not finalized; this API may change in the future.</para></remarks>
	public override Task RollbackAsync(string savepointName, CancellationToken cancellationToken = default(CancellationToken))
	{
		return ExecuteSavepointAsync("rollback to ", savepointName, Connection?.AsyncIOBehavior ?? IOBehavior.Asynchronous, cancellationToken);
	}

	/// <summary>
	/// Sets a named transaction savepoint with the specified <paramref name="savepointName" />. If the current transaction
	/// already has a savepoint with the same name, the old savepoint is deleted and a new one is set.
	/// </summary>
	/// <param name="savepointName">The savepoint name.</param>
	/// <remarks>The proposed ADO.NET API that this is based on is not finalized; this API may change in the future.</remarks>
	public override void Save(string savepointName)
	{
		ExecuteSavepointAsync("", savepointName, IOBehavior.Synchronous, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Asynchronously sets a named transaction savepoint with the specified <paramref name="savepointName" />. If the current transaction
	/// already has a savepoint with the same name, the old savepoint is deleted and a new one is set.
	/// </summary>
	/// <param name="savepointName">The savepoint name.</param>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task" /> representing the asynchronous operation.</returns>
	/// <remarks>The proposed ADO.NET API that this is based on is not finalized; this API may change in the future.</remarks>
	public override Task SaveAsync(string savepointName, CancellationToken cancellationToken = default(CancellationToken))
	{
		return ExecuteSavepointAsync("", savepointName, Connection?.AsyncIOBehavior ?? IOBehavior.Asynchronous, cancellationToken);
	}

	private async Task ExecuteSavepointAsync(string command, string savepointName, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		VerifyValid();
		ArgumentNullException.ThrowIfNull(savepointName, "savepointName");
		ArgumentException.ThrowIfNullOrEmpty(savepointName, "savepointName");
		using OracleCommand cmd = new OracleCommand(command + "savepoint " + QuoteIdentifier(savepointName), Connection, this)
		{
			NoActivity = true
		};
		await cmd.ExecuteNonQueryAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	/// <summary>
	/// Releases any resources associated with this transaction. If it was not committed, it will be rolled back.
	/// </summary>
	/// <param name="disposing"><c>true</c> if this method is being called from <c>Dispose</c>; <c>false</c> if being called from a finalizer.</param>
	protected override void Dispose(bool disposing)
	{
		try
		{
			if (disposing)
			{
				DisposeAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
			}
		}
		finally
		{
			base.Dispose(disposing);
		}
	}

	/// <summary>
	/// Asynchronously releases any resources associated with this transaction. If it was not committed, it will be rolled back.
	/// </summary>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task" /> representing the asynchronous operation.</returns>
	public override ValueTask DisposeAsync()
	{
		return DisposeAsync(Connection?.AsyncIOBehavior ?? IOBehavior.Asynchronous, CancellationToken.None);
	}

	internal ValueTask DisposeAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		m_isDisposed = true;
		if (Connection?.CurrentTransaction == this)
		{
			return DoDisposeAsync(ioBehavior, cancellationToken);
		}
		Connection = null;
		return default(ValueTask);
	}

	private async ValueTask DoDisposeAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (Connection?.CurrentTransaction == this)
		{
			if (Connection.State == ConnectionState.Open && Connection.Session.IsConnected)
			{
				try
				{
					await DoRollback(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch (IOException)
				{
				}
				catch (SocketException)
				{
				}
			}
			await Connection.RestoreAutoCommitAfterExplicitTransactionAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			Connection.CurrentTransaction = null;
		}
		Connection = null;
	}

	internal OracleTransaction(OracleConnection connection, IsolationLevel isolationLevel, ILogger logger)
	{
		Connection = connection;
		IsolationLevel = isolationLevel;
		m_logger = logger;
		Log.StartedTransaction(m_logger, Connection.Session.Id);
	}

	private async Task DoRollback(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		using Activity activity = Connection.Session.StartActivity("Rollback");
		Log.RollingBackTransaction(m_logger, Connection.Session.Id);
		try
		{
			using OracleCommand cmd = new OracleCommand("rollback", Connection, this)
			{
				NoActivity = true
			};
			await cmd.ExecuteNonQueryAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			Log.RolledBackTransaction(m_logger, Connection.Session.Id);
		}
		catch (Exception exception) when (activity?.IsAllDataRequested ?? false)
		{
			activity.SetException(exception);
			throw;
		}
	}

	private void VerifyValid()
	{
		ObjectDisposedException.ThrowIf(m_isDisposed, this);
		if (Connection == null)
		{
			throw new InvalidOperationException("Already committed or rolled back.");
		}
		if (Connection.CurrentTransaction == null)
		{
			throw new InvalidOperationException("There is no active transaction.");
		}
		if (Connection.CurrentTransaction != this)
		{
			throw new InvalidOperationException("This is not the active transaction.");
		}
	}

	private static string QuoteIdentifier(string identifier)
	{
		return "`" + identifier.Replace("`", "``") + "`";
	}
}
