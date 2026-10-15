using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Logging;
using OceanBase.Core;
using OceanBase.Logging;
using OceanBase.Plugins;
using OceanBase.Protocol;
using OceanBase.Protocol.Payloads;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;

namespace OceanBase;

/// <summary>
/// <see cref="T:OceanBase.MySqlConnection" /> represents a connection to a MySQL database.
/// </summary>
public sealed class MySqlConnection : DbConnection, ICloneable, IServerSessionOwner
{
	private static readonly StateChangeEventArgs s_stateChangeClosedConnecting = new StateChangeEventArgs(ConnectionState.Closed, ConnectionState.Connecting);

	private static readonly StateChangeEventArgs s_stateChangeConnectingOpen = new StateChangeEventArgs(ConnectionState.Connecting, ConnectionState.Open);

	private static readonly StateChangeEventArgs s_stateChangeOpenClosed = new StateChangeEventArgs(ConnectionState.Open, ConnectionState.Closed);

	#if NET9_0_OR_GREATER

	#if NET9_0_OR_GREATER

	private static readonly Lock s_lock = new Lock();

	#else

	private static readonly object s_lock = new object();

	#endif

	#else

	private static readonly object s_lock = new object();

	#endif

	private static readonly Dictionary<Transaction, List<EnlistedTransactionBase>> s_transactionConnections = new Dictionary<Transaction, List<EnlistedTransactionBase>>();

	private static readonly ReadOnlyMemory<byte>[] s_startTransactionPayloads = new ReadOnlyMemory<byte>[30];

	private readonly MySqlDataSource? m_dataSource;

	private readonly ILogger m_logger;

	private readonly ILogger m_transactionLogger;

	private string m_connectionString;

	private ConnectionSettings? m_connectionSettings;

	private ServerSession? m_session;

	private ConnectionState m_connectionState;

	private bool m_hasBeenOpened;

	private bool m_isDisposed;

	private Dictionary<string, CachedProcedure?>? m_cachedProcedures;

	private SchemaProvider? m_schemaProvider;

	private MySqlDataReader? m_activeReader;

	private EnlistedTransactionBase? m_enlistedTransaction;

	public override string ConnectionString
	{
		get
		{
			if (!m_hasBeenOpened)
			{
				return m_connectionString;
			}
			MySqlConnectionStringBuilder connectionStringBuilder = GetConnectionSettings().ConnectionStringBuilder;
			return connectionStringBuilder.GetConnectionString(connectionStringBuilder.PersistSecurityInfo);
		}
		[param: AllowNull]
		set
		{
			if (m_connectionState == ConnectionState.Open)
			{
				throw new InvalidOperationException("Cannot change the connection string on an open connection.");
			}
			m_hasBeenOpened = false;
			m_connectionString = value ?? "";
			m_connectionSettings = null;
		}
	}

	public override string Database => m_session?.DatabaseOverride ?? GetConnectionSettings().Database;

	public override ConnectionState State => m_connectionState;

	public override string DataSource => GetConnectionSettings().ConnectionStringBuilder.Server;

	public override string ServerVersion => Session.ServerVersion.OriginalString;

	/// <summary>
	/// The connection ID from MySQL Server.
	/// </summary>
	public int ServerThread => Session.ConnectionId;

	/// <summary>
	/// Gets or sets the delegate used to provide client certificates for connecting to a server.
	/// </summary>
	/// <remarks>The provided <see cref="T:System.Security.Cryptography.X509Certificates.X509CertificateCollection" /> should be filled with the client certificate(s) needed to connect to the server.</remarks>
	public Func<X509CertificateCollection, ValueTask>? ProvideClientCertificatesCallback { get; set; }

	/// <summary>
	/// Gets or sets the delegate used to generate a password for new database connections.
	/// </summary>
	/// <remarks>
	/// <para>This delegate is executed when a new database connection is opened that requires a password. Due to
	/// connection pooling, this delegate is only executed when a new physical connection is established with a database
	/// server, not when a connection is retrieved from the pool.</para>
	/// <para>The <see cref="P:OceanBase.MySqlConnectionStringBuilder.Password" /> option takes precedence over this
	/// delegate if it is specified.</para>
	/// <para>Using this delegate can make more efficient use of connection pooling for servers that require
	/// frequently-changing passwords or authentication tokens. Changing the password in the connection string
	/// will create unique connection pools; this delegate allows a single connection pool to use multiple passwords.</para>
	/// </remarks>
	public Func<MySqlProvidePasswordContext, string>? ProvidePasswordCallback { get; set; }

	/// <summary>
	/// Gets or sets the delegate used to verify that the server's certificate is valid.
	/// </summary>
	/// <remarks><see cref="P:OceanBase.MySqlConnectionStringBuilder.SslMode" /> must be set to <see cref="F:OceanBase.MySqlSslMode.Preferred" />
	/// or <see cref="F:OceanBase.MySqlSslMode.Required" /> in order for this delegate to be invoked. See the documentation for
	/// <see cref="P:OceanBase.MySqlConnection.RemoteCertificateValidationCallback" /> for more information on the values passed to this delegate.</remarks>
	public RemoteCertificateValidationCallback? RemoteCertificateValidationCallback { get; set; }

	protected override DbProviderFactory DbProviderFactory => MySqlConnectorFactory.Instance;

	/// <summary>
	/// Gets the time (in seconds) to wait while trying to establish a connection
	/// before terminating the attempt and generating an error. This value
	/// is controlled by <see cref="P:OceanBase.MySqlConnectionStringBuilder.ConnectionTimeout" />,
	/// which defaults to 15 seconds.
	/// </summary>
	public override int ConnectionTimeout => GetConnectionSettings().ConnectionTimeout;

	public override bool CanCreateBatch => true;

	internal ServerSession Session
	{
		get
		{
			VerifyNotDisposed();
			if (m_session == null || State != ConnectionState.Open)
			{
				throw new InvalidOperationException($"Connection must be Open; current state is {State}");
			}
			return m_session;
		}
	}

	internal MySqlTransaction? CurrentTransaction { get; set; }

	internal MySqlConnectorLoggingConfiguration LoggingConfiguration { get; }

	internal ZstandardPlugin? ZstandardPlugin { get; set; }

	internal ConnectionOpenedCallback? ConnectionOpenedCallback { get; set; }

	internal bool AllowLoadLocalInfile => GetInitializedConnectionSettings().AllowLoadLocalInfile;

	internal bool AllowUserVariables => GetInitializedConnectionSettings().AllowUserVariables;

	internal bool AllowZeroDateTime => GetInitializedConnectionSettings().AllowZeroDateTime;

	internal bool ConvertZeroDateTime => GetInitializedConnectionSettings().ConvertZeroDateTime;

	internal DateTimeKind DateTimeKind => GetInitializedConnectionSettings().DateTimeKind;

	internal int DefaultCommandTimeout => GetConnectionSettings().DefaultCommandTimeout;

	internal MySqlGuidFormat GuidFormat => GetInitializedConnectionSettings().GuidFormat;

	internal bool IgnoreCommandTransaction
	{
		get
		{
			if (!GetInitializedConnectionSettings().IgnoreCommandTransaction)
			{
				return m_enlistedTransaction is StandardEnlistedTransaction;
			}
			return true;
		}
	}

	internal bool IgnorePrepare => GetInitializedConnectionSettings().IgnorePrepare;

	internal bool AutoClosePreparedStatements => GetInitializedConnectionSettings().AutoClosePreparedStatements;

	internal bool NoBackslashEscapes => GetInitializedConnectionSettings().NoBackslashEscapes;

	internal bool TreatTinyAsBoolean => GetInitializedConnectionSettings().TreatTinyAsBoolean;

	internal IOBehavior AsyncIOBehavior
	{
		get
		{
			if (!GetConnectionSettings().ForceSynchronous)
			{
				return IOBehavior.Asynchronous;
			}
			return IOBehavior.Synchronous;
		}
	}

	internal IOBehavior SimpleAsyncIOBehavior
	{
		get
		{
			if ((!(m_connectionSettings?.ForceSynchronous)) ?? true)
			{
				return IOBehavior.Asynchronous;
			}
			return IOBehavior.Synchronous;
		}
	}

	internal MySqlSslMode SslMode => GetInitializedConnectionSettings().SslMode;

	internal int? ActiveCommandId => m_session?.ActiveCommandId;

	internal bool SupportsPerQueryVariables => m_session?.SupportsPerQueryVariables ?? false;

	internal bool HasActiveReader => m_activeReader != null;

	internal bool SslIsEncrypted => m_session.SslIsEncrypted;

	internal bool SslIsSigned => m_session.SslIsSigned;

	internal bool SslIsAuthenticated => m_session.SslIsAuthenticated;

	internal bool SslIsMutuallyAuthenticated => m_session.SslIsMutuallyAuthenticated;

	internal SslProtocols SslProtocol => m_session.SslProtocol;

	internal IPEndPoint? SessionEndPoint => m_session.IPEndPoint;

	internal MySqlDataSource? MySqlDataSource => m_dataSource;

	ZstandardPlugin? IServerSessionOwner.ZstandardPlugin => ZstandardPlugin;

	public event MySqlInfoMessageEventHandler? InfoMessage;

	public MySqlConnection()
		: this("")
	{
	}

	public MySqlConnection(string? connectionString)
		: this(connectionString ?? "", MySqlConnectorLoggingConfiguration.GlobalConfiguration)
	{
	}

	internal MySqlConnection(MySqlDataSource dataSource)
		: this(dataSource.ConnectionString, dataSource.LoggingConfiguration)
	{
		m_dataSource = dataSource;
	}

	private MySqlConnection(string connectionString, MySqlConnectorLoggingConfiguration loggingConfiguration)
	{
		GC.SuppressFinalize(this);
		m_connectionString = connectionString;
		LoggingConfiguration = loggingConfiguration;
		m_logger = loggingConfiguration.ConnectionLogger;
		m_transactionLogger = loggingConfiguration.TransactionLogger;
	}

	/// <summary>
	/// Begins a database transaction.
	/// </summary>
	/// <returns>A <see cref="T:OceanBase.MySqlTransaction" /> representing the new database transaction.</returns>
	/// <remarks>Transactions may not be nested.</remarks>
	public new MySqlTransaction BeginTransaction()
	{
		return BeginTransactionAsync(System.Data.IsolationLevel.Unspecified, null, IOBehavior.Synchronous, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Begins a database transaction.
	/// </summary>
	/// <param name="isolationLevel">The <see cref="T:System.Data.IsolationLevel" /> for the transaction.</param>
	/// <returns>A <see cref="T:OceanBase.MySqlTransaction" /> representing the new database transaction.</returns>
	/// <remarks>Transactions may not be nested.</remarks>
	public new MySqlTransaction BeginTransaction(System.Data.IsolationLevel isolationLevel)
	{
		return BeginTransactionAsync(isolationLevel, null, IOBehavior.Synchronous, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Begins a database transaction.
	/// </summary>
	/// <param name="isolationLevel">The <see cref="T:System.Data.IsolationLevel" /> for the transaction.</param>
	/// <param name="isReadOnly">If <c>true</c>, changes to tables used in the transaction are prohibited; otherwise, they are permitted.</param>
	/// <returns>A <see cref="T:OceanBase.MySqlTransaction" /> representing the new database transaction.</returns>
	/// <remarks>Transactions may not be nested.</remarks>
	public MySqlTransaction BeginTransaction(System.Data.IsolationLevel isolationLevel, bool isReadOnly)
	{
		return BeginTransactionAsync(isolationLevel, isReadOnly, IOBehavior.Synchronous, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Begins a database transaction.
	/// </summary>
	/// <param name="isolationLevel">The <see cref="T:System.Data.IsolationLevel" /> for the transaction.</param>
	/// <returns>A <see cref="T:OceanBase.MySqlTransaction" /> representing the new database transaction.</returns>
	protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel)
	{
		return BeginTransactionAsync(isolationLevel, null, IOBehavior.Synchronous, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Begins a database transaction asynchronously.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> representing the new database transaction.</returns>
	/// <remarks>Transactions may not be nested.</remarks>
	public new ValueTask<MySqlTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return BeginTransactionAsync(System.Data.IsolationLevel.Unspecified, null, AsyncIOBehavior, cancellationToken);
	}

	/// <summary>
	/// Begins a database transaction asynchronously.
	/// </summary>
	/// <param name="isolationLevel">The <see cref="T:System.Data.IsolationLevel" /> for the transaction.</param>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> representing the new database transaction.</returns>
	/// <remarks>Transactions may not be nested.</remarks>
	public new ValueTask<MySqlTransaction> BeginTransactionAsync(System.Data.IsolationLevel isolationLevel, CancellationToken cancellationToken = default(CancellationToken))
	{
		return BeginTransactionAsync(isolationLevel, null, AsyncIOBehavior, cancellationToken);
	}

	/// <summary>
	/// Begins a database transaction asynchronously.
	/// </summary>
	/// <param name="isolationLevel">The <see cref="T:System.Data.IsolationLevel" /> for the transaction.</param>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.ValueTask`1" /> representing the new database transaction.</returns>
	protected override async ValueTask<DbTransaction> BeginDbTransactionAsync(System.Data.IsolationLevel isolationLevel, CancellationToken cancellationToken)
	{
		return await BeginTransactionAsync(isolationLevel, null, AsyncIOBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	/// <summary>
	/// Begins a database transaction asynchronously.
	/// </summary>
	/// <param name="isolationLevel">The <see cref="T:System.Data.IsolationLevel" /> for the transaction.</param>
	/// <param name="isReadOnly">If <c>true</c>, changes to tables used in the transaction are prohibited; otherwise, they are permitted.</param>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> representing the new database transaction.</returns>
	/// <remarks>Transactions may not be nested.</remarks>
	public ValueTask<MySqlTransaction> BeginTransactionAsync(System.Data.IsolationLevel isolationLevel, bool isReadOnly, CancellationToken cancellationToken = default(CancellationToken))
	{
		return BeginTransactionAsync(isolationLevel, isReadOnly, AsyncIOBehavior, cancellationToken);
	}

	private async ValueTask<MySqlTransaction> BeginTransactionAsync(System.Data.IsolationLevel isolationLevel, bool? isReadOnly, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (State != ConnectionState.Open)
		{
			throw new InvalidOperationException("Connection is not open.");
		}
		if (CurrentTransaction != null)
		{
			throw new InvalidOperationException("Transactions may not be nested.");
		}
		if (m_enlistedTransaction != null)
		{
			throw new InvalidOperationException("Cannot begin a transaction when already enlisted in a transaction.");
		}
		Log.StartingTransaction(m_transactionLogger, m_session.Id);
		ReadOnlyMemory<byte> startTransactionPayload = GetStartTransactionPayload(isolationLevel, isReadOnly, m_session.SupportsQueryAttributes);
		ConnectionSettings initializedConnectionSettings = GetInitializedConnectionSettings();
		if ((initializedConnectionSettings != null && !initializedConnectionSettings.UseCompression && !((!initializedConnectionSettings.Pipelining) ?? false)) ? true : false)
		{
			await m_session.SendRawAsync(startTransactionPayload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			OkPayload.Verify((await m_session.ReceiveReplyAsync(1, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Span, m_session);
			OkPayload.Verify((await m_session.ReceiveReplyAsync(1, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Span, m_session);
		}
		else
		{
			await m_session.SendAsync(new PayloadData(startTransactionPayload.Slice(4, startTransactionPayload.Span[0])), ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			OkPayload.Verify((await m_session.ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Span, m_session);
			await m_session.SendAsync(new PayloadData(startTransactionPayload.Slice(8 + startTransactionPayload.Span[0], startTransactionPayload.Span[startTransactionPayload.Span[0] + 4])), ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			OkPayload.Verify((await m_session.ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Span, m_session);
		}
		return CurrentTransaction = new MySqlTransaction(this, isolationLevel, m_transactionLogger);
	}

	/// <summary>
	/// Returns a <see cref="T:System.ReadOnlyMemory`1" /> containing two payloads to set the isolation level and start a transaction.
	/// </summary>
	internal static ReadOnlyMemory<byte> GetStartTransactionPayload(System.Data.IsolationLevel isolationLevel, bool? isReadOnly, bool supportsQueryAttributes)
	{
		int num = isolationLevel switch
		{
			System.Data.IsolationLevel.ReadUncommitted => 0, 
			System.Data.IsolationLevel.ReadCommitted => 1, 
			System.Data.IsolationLevel.Serializable => 2, 
			System.Data.IsolationLevel.RepeatableRead => 3, 
			System.Data.IsolationLevel.Snapshot => 3, 
			System.Data.IsolationLevel.Unspecified => 3, 
			_ => throw new NotSupportedException($"IsolationLevel.{isolationLevel} is not supported."), 
		};
		int num2 = ((isolationLevel == System.Data.IsolationLevel.Snapshot) ? 1 : 0);
		int num3 = (isReadOnly.HasValue ? ((isReadOnly != true) ? 1 : 2) : 0);
		int num4 = num3;
		int num5 = ((supportsQueryAttributes ? 1 : 0) * 5 + num + num2) * 3 + num4;
		if (s_startTransactionPayloads[num5].IsEmpty)
		{
			byte[] array = new byte[125];
			int num6 = 4;
			int num7 = 0;
			array[num6] = 3;
			num6++;
			num7++;
			if (supportsQueryAttributes)
			{
				array[num6 + 1] = 1;
				num6 += 2;
				num7 += 2;
			}
			ReadOnlySpan<byte> readOnlySpan = "set session transaction isolation level "u8;
			readOnlySpan.CopyTo(array.AsSpan(num6));
			num7 += readOnlySpan.Length;
			num6 += readOnlySpan.Length;
			ReadOnlySpan<byte> readOnlySpan2 = num switch
			{
				0 => "read uncommitted"u8, 
				1 => "read committed"u8, 
				2 => "serializable"u8, 
				_ => "repeatable read"u8, 
			};
			readOnlySpan2.CopyTo(array.AsSpan(num6));
			num6 += readOnlySpan2.Length;
			num7 += readOnlySpan2.Length;
			array[num6] = 59;
			num6++;
			num7++;
			array[0] = (byte)num7;
			num6 += 4;
			array[num6] = 3;
			num6++;
			num7 = 1;
			if (supportsQueryAttributes)
			{
				array[num6 + 1] = 1;
				num6 += 2;
				num7 += 2;
			}
			ReadOnlySpan<byte> readOnlySpan3 = "start transaction"u8;
			readOnlySpan3.CopyTo(array.AsSpan(num6));
			num7 += readOnlySpan3.Length;
			num6 += readOnlySpan3.Length;
			if (num2 == 1)
			{
				ReadOnlySpan<byte> readOnlySpan4 = " with consistent snapshot"u8;
				readOnlySpan4.CopyTo(array.AsSpan(num6));
				num7 += readOnlySpan4.Length;
				num6 += readOnlySpan4.Length;
			}
			if (num2 > 0 && num4 > 0)
			{
				array[num6] = 44;
				num6++;
				num7++;
			}
			ReadOnlySpan<byte> readOnlySpan5 = num4 switch
			{
				1 => " read write"u8, 
				2 => " read only"u8, 
				_ => ""u8, 
			};
			readOnlySpan5.CopyTo(array.AsSpan(num6));
			num6 += readOnlySpan5.Length;
			num7 += readOnlySpan5.Length;
			array[num6] = 59;
			num6++;
			num7++;
			array[array[0] + 4] = (byte)num7;
			s_startTransactionPayloads[num5] = new ReadOnlyMemory<byte>(array, 0, array[0] + array[array[0] + 4] + 8);
		}
		return s_startTransactionPayloads[num5];
	}

	public override void EnlistTransaction(Transaction? transaction)
	{
		if (State != ConnectionState.Open)
		{
			throw new InvalidOperationException("Connection is not open.");
		}
		if (!((!(m_enlistedTransaction?.Transaction.Equals(transaction))) ?? true))
		{
			return;
		}
		if (m_enlistedTransaction != null)
		{
			throw new MySqlException("Already enlisted in a Transaction.");
		}
		if (CurrentTransaction != null)
		{
			throw new InvalidOperationException("Can't enlist in a Transaction when there is an active MySqlTransaction.");
		}
		if ((object)transaction == null)
		{
			return;
		}
		MySqlConnection mySqlConnection = FindExistingEnlistedSession(transaction);
		if (mySqlConnection != null)
		{
			CloseAsync(changeState: false, IOBehavior.Synchronous).GetAwaiter().GetResult();
			TakeSessionFrom(mySqlConnection);
			return;
		}
		bool useXaTransactions = GetInitializedConnectionSettings().UseXaTransactions;
		EnlistedTransactionBase enlistedTransactionBase = (useXaTransactions ? ((EnlistedTransactionBase)new XaEnlistedTransaction(transaction, this, UnenlistTransaction, useXaTransactions)) : ((EnlistedTransactionBase)new StandardEnlistedTransaction(transaction, this, UnenlistTransaction, useXaTransactions)));
		enlistedTransactionBase.Start();
		lock (s_lock)
		{
			if (!s_transactionConnections.TryGetValue(transaction, out List<EnlistedTransactionBase> value))
			{
				value = (s_transactionConnections[transaction] = new List<EnlistedTransactionBase>());
			}
			value.Add(enlistedTransactionBase);
		}
		m_enlistedTransaction = enlistedTransactionBase;
	}

	internal void UnenlistTransaction()
	{
		Transaction transaction = m_enlistedTransaction.Transaction;
		m_enlistedTransaction = null;
		bool? flag = null;
		lock (s_lock)
		{
			List<EnlistedTransactionBase> list = s_transactionConnections[transaction];
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].Connection == this)
				{
					flag = list[i].IsIdle;
					list.RemoveAt(i);
					break;
				}
			}
			if (list.Count == 0)
			{
				s_transactionConnections.Remove(transaction);
			}
		}
		if (!flag.HasValue)
		{
			throw new InvalidOperationException("Didn't find transaction");
		}
		if (flag.Value)
		{
			Close();
		}
	}

	private MySqlConnection? FindExistingEnlistedSession(Transaction transaction)
	{
		bool flag = false;
		bool flag2 = false;
		lock (s_lock)
		{
			if (s_transactionConnections.TryGetValue(transaction, out List<EnlistedTransactionBase> value))
			{
				flag = true;
				foreach (EnlistedTransactionBase item in value)
				{
					flag2 = item.UsesXaTransactions;
					if (item.IsIdle && item.ConnectionString == m_connectionString && item.Connection is MySqlConnection result)
					{
						item.Connection = this;
						item.IsIdle = false;
						return result;
					}
				}
			}
		}
		if (GetInitializedConnectionSettings().UseXaTransactions)
		{
			if (flag && !flag2)
			{
				throw new NotSupportedException("Cannot start an XA transaction when there is an existing non-XA transaction.");
			}
		}
		else if (flag)
		{
			throw new NotSupportedException("Multiple simultaneous connections or connections with different connection strings inside the same transaction are not supported when UseXaTransactions=False.");
		}
		return null;
	}

	private void TakeSessionFrom(MySqlConnection other)
	{
		m_session = other.m_session;
		m_session.OwningConnection = new WeakReference<IServerSessionOwner>(this);
		other.m_session = null;
		m_cachedProcedures = other.m_cachedProcedures;
		other.m_cachedProcedures = null;
		m_enlistedTransaction = other.m_enlistedTransaction;
		other.m_enlistedTransaction = null;
	}

	public override void Close()
	{
		CloseAsync(changeState: true, IOBehavior.Synchronous).GetAwaiter().GetResult();
	}

	public override Task CloseAsync()
	{
		return CloseAsync(changeState: true, SimpleAsyncIOBehavior);
	}

	internal Task CloseAsync(IOBehavior ioBehavior)
	{
		return CloseAsync(changeState: true, ioBehavior);
	}

	public override void ChangeDatabase(string databaseName)
	{
		ChangeDatabaseAsync(IOBehavior.Synchronous, databaseName, CancellationToken.None).GetAwaiter().GetResult();
	}

	public override Task ChangeDatabaseAsync(string databaseName, CancellationToken cancellationToken = default(CancellationToken))
	{
		return ChangeDatabaseAsync(AsyncIOBehavior, databaseName, cancellationToken);
	}

	private async Task ChangeDatabaseAsync(IOBehavior ioBehavior, string databaseName, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(databaseName))
		{
			throw new ArgumentException("Database name is not valid.", "databaseName");
		}
		if (State != ConnectionState.Open)
		{
			throw new InvalidOperationException("Connection is not open.");
		}
		using (PayloadData initDatabasePayload = InitDatabasePayload.Create(databaseName))
		{
			await m_session.SendAsync(initDatabasePayload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		OkPayload.Verify((await m_session.ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Span, m_session);
		m_session.DatabaseOverride = databaseName;
	}

	public new MySqlCommand CreateCommand()
	{
		return (MySqlCommand)base.CreateCommand();
	}

	public bool Ping()
	{
		return PingAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
	}

	public Task<bool> PingAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return PingAsync(SimpleAsyncIOBehavior, cancellationToken).AsTask();
	}

	private async ValueTask<bool> PingAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (m_session == null)
		{
			return false;
		}
		try
		{
			if (await m_session.TryPingAsync(logInfo: true, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
			{
				return true;
			}
		}
		catch (InvalidOperationException)
		{
		}
		SetState(ConnectionState.Broken);
		return false;
	}

	public override void Open()
	{
		OpenAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
	}

	public override Task OpenAsync(CancellationToken cancellationToken)
	{
		return OpenAsync(null, cancellationToken);
	}

	internal async Task OpenAsync(IOBehavior? ioBehavior, CancellationToken cancellationToken)
	{
		long timestamp = Stopwatch.GetTimestamp();
		VerifyNotDisposed();
		cancellationToken.ThrowIfCancellationRequested();
		if (State != ConnectionState.Closed)
		{
			throw new InvalidOperationException($"Cannot Open when State is {State}.");
		}
		using Activity activity = ActivitySourceHelper.StartActivity("Open");
		_ = 2;
		try
		{
			SetState(ConnectionState.Connecting);
			ConnectionPool connectionPool = m_dataSource?.Pool ?? ConnectionPool.GetPool(m_connectionString, LoggingConfiguration);
			if (m_connectionSettings == null)
			{
				m_connectionSettings = connectionPool?.ConnectionSettings ?? new ConnectionSettings(new MySqlConnectionStringBuilder(m_connectionString));
			}
			if (m_connectionSettings.AutoEnlist && (object)Transaction.Current != null)
			{
				MySqlConnection mySqlConnection = FindExistingEnlistedSession(Transaction.Current);
				if (mySqlConnection != null)
				{
					TakeSessionFrom(mySqlConnection);
					ActivitySourceHelper.CopyTags(m_session.ActivityTags, activity);
					m_hasBeenOpened = true;
					SetState(ConnectionState.Open);
					ConnectionOpenedCallback connectionOpenedCallback = ConnectionOpenedCallback;
					if (connectionOpenedCallback != null)
					{
						cancellationToken.ThrowIfCancellationRequested();
						await connectionOpenedCallback(new ConnectionOpenedContext(this, ConnectionOpenedConditions.None), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					}
					return;
				}
			}
			try
			{
				m_session = await CreateSessionAsync(connectionPool, timestamp, activity, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				m_hasBeenOpened = true;
				SetState(ConnectionState.Open);
			}
			catch (OperationCanceledException ex)
			{
				SetState(ConnectionState.Closed);
				if (!cancellationToken.Equals(ex.CancellationToken))
				{
					cancellationToken.ThrowIfCancellationRequested();
				}
				throw;
			}
			catch (MySqlException)
			{
				SetState(ConnectionState.Closed);
				cancellationToken.ThrowIfCancellationRequested();
				throw;
			}
			catch (SocketException)
			{
				SetState(ConnectionState.Closed);
				throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "Unable to connect to any of the specified MySQL hosts.");
			}
			if (m_connectionSettings.AutoEnlist && (object)Transaction.Current != null)
			{
				EnlistTransaction(Transaction.Current);
			}
			ConnectionOpenedCallback connectionOpenedCallback2 = ConnectionOpenedCallback;
			if (connectionOpenedCallback2 != null)
			{
				cancellationToken.ThrowIfCancellationRequested();
				await connectionOpenedCallback2(new ConnectionOpenedContext(this, m_session.Conditions), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		catch (Exception exception) when (activity?.IsAllDataRequested ?? false)
		{
			MySqlConnectionStringBuilder mySqlConnectionStringBuilder = m_connectionSettings?.ConnectionStringBuilder;
			if (mySqlConnectionStringBuilder != null)
			{
				activity.SetTag("db.connection_string", mySqlConnectionStringBuilder.GetConnectionString(mySqlConnectionStringBuilder.PersistSecurityInfo));
			}
			activity.SetException(exception);
			throw;
		}
	}

	/// <summary>
	/// Resets the session state of the current open connection; this clears temporary tables and user-defined variables.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <c>ValueTask</c> representing the asynchronous operation.</returns>
	/// <remarks>This is an optional feature of the MySQL protocol and may not be supported by all servers.
	/// It's known to be supported by MySQL Server 5.7.3 (and later) and MariaDB 10.2.4 (and later).
	/// Other MySQL-compatible servers or proxies may not support this command.</remarks>
	public async ValueTask ResetConnectionAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		ServerSession session = Session;
		Log.ResettingConnection(m_logger, session.Id);
		try
		{
			await session.SendAsync(ResetConnectionPayload.Instance, AsyncIOBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			OkPayload.Verify((await session.ReceiveReplyAsync(AsyncIOBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Span, session);
			Log.ResetConnection(m_logger, session.Id);
		}
		catch (Exception ex)
		{
			Log.ResettingConnectionFailed(m_logger, session.Id, ex.Message);
			if (ex is MySqlException)
			{
				throw;
			}
			throw new MySqlException("Failed to reset connection", ex);
		}
	}

	/// <summary>
	/// Clears the connection pool that <paramref name="connection" /> belongs to.
	/// </summary>
	/// <param name="connection">The <see cref="T:OceanBase.MySqlConnection" /> whose connection pool will be cleared.</param>
	public static void ClearPool(MySqlConnection connection)
	{
		ClearPoolAsync(connection, IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Asynchronously clears the connection pool that <paramref name="connection" /> belongs to.
	/// </summary>
	/// <param name="connection">The <see cref="T:OceanBase.MySqlConnection" /> whose connection pool will be cleared.</param>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task" /> representing the asynchronous operation.</returns>
	public static Task ClearPoolAsync(MySqlConnection connection, CancellationToken cancellationToken = default(CancellationToken))
	{
		return ClearPoolAsync(connection, connection.AsyncIOBehavior, cancellationToken);
	}

	/// <summary>
	/// Clears all connection pools.
	/// </summary>
	public static void ClearAllPools()
	{
		ConnectionPool.ClearPoolsAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Asynchronously clears all connection pools.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task" /> representing the asynchronous operation.</returns>
	public static Task ClearAllPoolsAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return ConnectionPool.ClearPoolsAsync(IOBehavior.Asynchronous, cancellationToken);
	}

	private static async Task ClearPoolAsync(MySqlConnection connection, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(connection, "connection");
		ConnectionPool pool = ConnectionPool.GetPool(connection.m_connectionString, null, createIfNotFound: false);
		if (pool != null)
		{
			await pool.ClearAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	protected override DbCommand CreateDbCommand()
	{
		return new MySqlCommand(this, null);
	}

	/// <summary>
	/// Returns schema information for the data source of this <see cref="T:OceanBase.MySqlConnection" />.
	/// </summary>
	/// <returns>A <see cref="T:System.Data.DataTable" /> containing schema information.</returns>
	public override DataTable GetSchema()
	{
		return GetSchemaProvider().GetSchemaAsync(IOBehavior.Synchronous, "MetaDataCollections", null, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Returns schema information for the data source of this <see cref="T:OceanBase.MySqlConnection" />.
	/// </summary>
	/// <param name="collectionName">The name of the schema to return. See <a href="https://mysqlconnector.net/overview/schema-collections/">Supported Schema Collections</a> for the list of supported schema names.</param>
	/// <returns>A <see cref="T:System.Data.DataTable" /> containing schema information.</returns>
	public override DataTable GetSchema(string collectionName)
	{
		return GetSchemaProvider().GetSchemaAsync(IOBehavior.Synchronous, collectionName, null, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Returns schema information for the data source of this <see cref="T:OceanBase.MySqlConnection" />.
	/// </summary>
	/// <param name="collectionName">The name of the schema to return. See <a href="https://mysqlconnector.net/overview/schema-collections/">Supported Schema Collections</a> for the list of supported schema names.</param>
	/// <param name="restrictionValues">The restrictions to apply to the schema.</param>
	/// <returns>A <see cref="T:System.Data.DataTable" /> containing schema information.</returns>
	public override DataTable GetSchema(string collectionName, string?[] restrictionValues)
	{
		return GetSchemaProvider().GetSchemaAsync(IOBehavior.Synchronous, collectionName, restrictionValues, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Asynchronously returns schema information for the data source of this <see cref="T:OceanBase.MySqlConnection" />.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> containing schema information.</returns>
	/// <remarks>The proposed ADO.NET API that this is based on is not finalized; this API may change in the future.</remarks>
	public override Task<DataTable> GetSchemaAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return GetSchemaProvider().GetSchemaAsync(AsyncIOBehavior, "MetaDataCollections", null, cancellationToken).AsTask();
	}

	/// <summary>
	/// Asynchronously returns schema information for the data source of this <see cref="T:OceanBase.MySqlConnection" />.
	/// </summary>
	/// <param name="collectionName">The name of the schema to return.</param>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> containing schema information.</returns>
	/// <remarks>The proposed ADO.NET API that this is based on is not finalized; this API may change in the future.</remarks>
	public override Task<DataTable> GetSchemaAsync(string collectionName, CancellationToken cancellationToken = default(CancellationToken))
	{
		return GetSchemaProvider().GetSchemaAsync(AsyncIOBehavior, collectionName, null, cancellationToken).AsTask();
	}

	/// <summary>
	/// Asynchronously returns schema information for the data source of this <see cref="T:OceanBase.MySqlConnection" />.
	/// </summary>
	/// <param name="collectionName">The name of the schema to return.</param>
	/// <param name="restrictionValues">The restrictions to apply to the schema.</param>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> containing schema information.</returns>
	/// <remarks>The proposed ADO.NET API that this is based on is not finalized; this API may change in the future.</remarks>
	public override Task<DataTable> GetSchemaAsync(string collectionName, string?[] restrictionValues, CancellationToken cancellationToken = default(CancellationToken))
	{
		return GetSchemaProvider().GetSchemaAsync(AsyncIOBehavior, collectionName, restrictionValues, cancellationToken).AsTask();
	}

	private SchemaProvider GetSchemaProvider()
	{
		return m_schemaProvider ?? (m_schemaProvider = new SchemaProvider(this));
	}

	/// <summary>
	/// Creates a <see cref="T:OceanBase.MySqlBatch" /> object for executing batched commands.
	/// </summary>
	public new MySqlBatch CreateBatch()
	{
		return new MySqlBatch(this);
	}

	protected override DbBatch CreateDbBatch()
	{
		return CreateBatch();
	}

	protected override void Dispose(bool disposing)
	{
		try
		{
			if (disposing)
			{
				CloseAsync(changeState: true, IOBehavior.Synchronous).GetAwaiter().GetResult();
			}
		}
		finally
		{
			m_isDisposed = true;
			base.Dispose(disposing);
		}
	}

	public override async ValueTask DisposeAsync()
	{
		try
		{
			await CloseAsync(changeState: true, SimpleAsyncIOBehavior).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			m_isDisposed = true;
			base.Dispose(disposing: true);
		}
	}

	public MySqlConnection Clone()
	{
		return new MySqlConnection(this, m_dataSource, m_connectionString, m_hasBeenOpened);
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <summary>
	/// Returns an unopened copy of this connection with a new connection string. If the <c>Password</c>
	/// in <paramref name="connectionString" /> is not set, the password from this connection will be used.
	/// This allows creating a new connection with the same security information while changing other options,
	/// such as database or pooling.
	/// </summary>
	/// <param name="connectionString">The new connection string to be used.</param>
	/// <returns>A new <see cref="T:OceanBase.MySqlConnection" /> with different connection string options but
	/// the same password as this connection (unless overridden by <paramref name="connectionString" />).</returns>
	public MySqlConnection CloneWith(string connectionString)
	{
		MySqlConnectionStringBuilder mySqlConnectionStringBuilder = new MySqlConnectionStringBuilder(connectionString ?? throw new ArgumentNullException("connectionString"));
		MySqlConnectionStringBuilder connectionStringBuilder = GetConnectionSettings().ConnectionStringBuilder;
		bool flag = mySqlConnectionStringBuilder.Password.Length == 0 && (!mySqlConnectionStringBuilder.PersistSecurityInfo || connectionStringBuilder.PersistSecurityInfo);
		if (flag)
		{
			mySqlConnectionStringBuilder.Password = connectionStringBuilder.Password;
		}
		string connectionString2 = mySqlConnectionStringBuilder.ConnectionString;
		MySqlDataSource dataSource = ((connectionString2 == connectionStringBuilder.ConnectionString) ? m_dataSource : null);
		return new MySqlConnection(this, dataSource, connectionString2, m_hasBeenOpened && flag && !connectionStringBuilder.PersistSecurityInfo);
	}

	internal void SetSessionFailed(Exception exception)
	{
		m_session.SetFailed(exception);
	}

	internal void Cancel(ICancellableCommand command, int commandId, bool isCancel)
	{
		ServerSession session = m_session;
		if (session == null || State != ConnectionState.Open || !session.TryStartCancel(command))
		{
			Log.IgnoringCancellationForCommand(m_logger, commandId);
			return;
		}
		Log.CommandHasBeenCanceled(m_logger, commandId, session.Id, isCancel ? "Cancel()" : "command timeout");
		try
		{
			MySqlConnectionStringBuilder mySqlConnectionStringBuilder = new MySqlConnectionStringBuilder(m_connectionString)
			{
				AutoEnlist = false,
				Pooling = false
			};
			IPAddress iPAddress = null;
			IPEndPoint iPEndPoint = session.IPEndPoint;
			if (iPEndPoint != null)
			{
				IPAddress address = iPEndPoint.Address;
				if (address != null)
				{
					int port = iPEndPoint.Port;
					mySqlConnectionStringBuilder.Server = session.HostName;
					mySqlConnectionStringBuilder.Port = (uint)port;
					iPAddress = address;
				}
			}
			mySqlConnectionStringBuilder.UserID = session.UserID;
			int cancellationTimeout = GetConnectionSettings().CancellationTimeout;
			mySqlConnectionStringBuilder.ConnectionTimeout = ((cancellationTimeout < 1) ? 3u : ((uint)cancellationTimeout));
			ConnectionSettings connectionSettings = new ConnectionSettings(mySqlConnectionStringBuilder);
			if (iPAddress != null)
			{
				connectionSettings = connectionSettings.CloneWith(iPAddress);
			}
			using MySqlConnection mySqlConnection = CloneWith(mySqlConnectionStringBuilder.ConnectionString);
			mySqlConnection.m_connectionSettings = connectionSettings;
			mySqlConnection.ConnectionOpenedCallback = null;
			mySqlConnection.Open();
			int value = command.Session?.ConnectionId ?? session.ConnectionId;
			IFormatProvider invariantCulture = CultureInfo.InvariantCulture;
			DefaultInterpolatedStringHandler handler = new DefaultInterpolatedStringHandler(11, 1, invariantCulture);
			handler.AppendLiteral("KILL QUERY ");
			handler.AppendFormatted(value);
			using MySqlCommand mySqlCommand = new MySqlCommand(string.Create(invariantCulture, ref handler), mySqlConnection);
			mySqlCommand.CommandTimeout = ((cancellationTimeout < 1) ? 3 : cancellationTimeout);
			session.DoCancel(command, mySqlCommand);
		}
		catch (InvalidOperationException exception)
		{
			Log.IgnoringCancellationForClosedConnection(m_logger, exception, session.Id);
			session.AbortCancel(command);
		}
		catch (MySqlException exception2)
		{
			Log.CancelingCommandFailed(m_logger, exception2, session.Id, command.CommandId);
			session.AbortCancel(command);
		}
	}

	internal async Task<CachedProcedure?> GetCachedProcedure(string name, bool revalidateMissing, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		Log.GettingCachedProcedure(m_logger, m_session.Id, name);
		if (State != ConnectionState.Open)
		{
			throw new InvalidOperationException("Connection is not open.");
		}
		Dictionary<string, CachedProcedure?> cachedProcedures = m_session.Pool?.GetProcedureCache() ?? m_cachedProcedures;
		if (cachedProcedures == null)
		{
			Log.PoolDoesNotHaveSharedProcedureCache(m_logger, m_session.Id, m_session.Pool?.Id);
			cachedProcedures = (m_cachedProcedures = new Dictionary<string, CachedProcedure>());
		}
		NormalizedSchema normalized = NormalizedSchema.MustNormalize(name, Database);
		if (string.IsNullOrEmpty(normalized.Schema))
		{
			Log.CouldNotNormalizeDatabaseAndName(m_logger, m_session.Id, name, Database);
			return null;
		}
		bool flag;
		CachedProcedure value;
		lock (cachedProcedures)
		{
			flag = cachedProcedures.TryGetValue(normalized.FullyQualified, out value);
		}
		if (!flag || (value == null && revalidateMissing))
		{
			value = await CachedProcedure.FillAsync(ioBehavior, this, normalized.Schema, normalized.Component, m_logger, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (value == null)
			{
				Log.FailedToCacheProcedure(m_logger, m_session.Id, normalized.Schema, normalized.Component);
			}
			else
			{
				Log.CachingProcedure(m_logger, m_session.Id, normalized.Schema, normalized.Component);
			}
			int count;
			lock (cachedProcedures)
			{
				cachedProcedures[normalized.FullyQualified] = value;
				count = cachedProcedures.Count;
			}
			Log.ProcedureCacheCount(m_logger, m_session.Id, count);
		}
		if (value == null)
		{
			Log.DidNotFindCachedProcedure(m_logger, m_session.Id, normalized.Schema, normalized.Component);
		}
		else
		{
			Log.ReturningCachedProcedure(m_logger, m_session.Id, normalized.Schema, normalized.Component);
		}
		return value;
	}

	internal void SetActiveReader(MySqlDataReader dataReader)
	{
		ArgumentNullException.ThrowIfNull(dataReader, "dataReader");
		if (m_activeReader != null)
		{
			throw new InvalidOperationException("Can't replace active reader.");
		}
		m_activeReader = dataReader;
	}

	internal void FinishQuerying(bool hasWarnings)
	{
		m_session.FinishQuerying();
		m_activeReader = null;
		if (!hasWarnings || this.InfoMessage == null)
		{
			return;
		}
		List<MySqlError> list = new List<MySqlError>();
		using (MySqlCommand mySqlCommand = new MySqlCommand("SHOW WARNINGS;", this))
		{
			mySqlCommand.Transaction = CurrentTransaction;
			using MySqlDataReader mySqlDataReader = mySqlCommand.ExecuteReader();
			while (mySqlDataReader.Read())
			{
				list.Add(new MySqlError(mySqlDataReader.GetString(0), mySqlDataReader.GetInt32(1), mySqlDataReader.GetString(2)));
			}
		}
		this.InfoMessage(this, new MySqlInfoMessageEventArgs(list));
	}

	private async ValueTask<ServerSession> CreateSessionAsync(ConnectionPool? pool, long startingTimestamp, Activity? activity, IOBehavior? ioBehavior, CancellationToken cancellationToken)
	{
		MetricsReporter.AddPendingRequest(pool);
		ConnectionSettings connectionSettings = GetInitializedConnectionSettings();
		IOBehavior ioBehavior2 = (IOBehavior)(((int?)ioBehavior) ?? ((!connectionSettings.ForceSynchronous) ? 1 : 0));
		CancellationTokenSource timeoutSource = null;
		CancellationTokenSource linkedSource = null;
		try
		{
			if (connectionSettings.ConnectionTimeout != 0)
			{
				timeoutSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(Math.Max(1, connectionSettings.ConnectionTimeoutMilliseconds - Utility.GetElapsedMilliseconds(startingTimestamp))));
			}
			if (cancellationToken.CanBeCanceled && timeoutSource != null)
			{
				linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
			}
			CancellationToken cancellationToken2 = linkedSource?.Token ?? timeoutSource?.Token ?? cancellationToken;
			if (pool != null)
			{
				return await pool.GetSessionAsync(this, startingTimestamp, connectionSettings.ConnectionTimeoutMilliseconds, activity, ioBehavior2, cancellationToken2).ConfigureAwait(continueOnCapturedContext: false);
			}
			ILoadBalancer loadBalancer = ((connectionSettings.LoadBalance == MySqlLoadBalance.Random && connectionSettings.HostNames.Count > 1) ? RandomLoadBalancer.Instance : FailOverLoadBalancer.Instance);
			ServerSession serverSession = await ServerSession.ConnectAndRedirectAsync(m_logger, m_logger, NonPooledConnectionPoolMetadata.Instance, connectionSettings, loadBalancer, this, null, startingTimestamp, null, ioBehavior2, cancellationToken2).ConfigureAwait(continueOnCapturedContext: false);
			serverSession.OwningConnection = new WeakReference<IServerSessionOwner>(this);
			Log.CreatedNonPooledSession(m_logger, serverSession.Id);
			return serverSession;
		}
		catch (OperationCanceledException) when (timeoutSource?.IsCancellationRequested ?? false)
		{
			MetricsReporter.AddTimeout(pool, connectionSettings);
			string text = ((pool?.IsEmpty ?? false) ? " All pooled connections are in use." : "");
			throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "Connect Timeout expired." + text);
		}
		catch (MySqlException ex2) when ((timeoutSource?.IsCancellationRequested ?? false) || ex2.ErrorCode == MySqlErrorCode.CommandTimeoutExpired)
		{
			MetricsReporter.AddTimeout(pool, connectionSettings);
			throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "Connect Timeout expired.", ex2);
		}
		catch (MySqlException ex3) when (ex3.ErrorCode == MySqlErrorCode.UnableToConnectToHost && ex3.Message == "Connect Timeout expired.")
		{
			MetricsReporter.AddTimeout(pool, connectionSettings);
			throw;
		}
		finally
		{
			MetricsReporter.RemovePendingRequest(pool);
			linkedSource?.Dispose();
			timeoutSource?.Dispose();
		}
	}

	internal void SetState(ConnectionState newState)
	{
		if (m_connectionState != newState)
		{
			ConnectionState connectionState = m_connectionState;
			m_connectionState = newState;
			StateChangeEventArgs stateChange = ((connectionState == ConnectionState.Closed && newState == ConnectionState.Connecting) ? s_stateChangeClosedConnecting : ((connectionState == ConnectionState.Connecting && newState == ConnectionState.Open) ? s_stateChangeConnectingOpen : ((connectionState == ConnectionState.Open && newState == ConnectionState.Closed) ? s_stateChangeOpenClosed : new StateChangeEventArgs(connectionState, newState))));
			OnStateChange(stateChange);
		}
	}

	private MySqlConnection(MySqlConnection other, MySqlDataSource? dataSource, string connectionString, bool hasBeenOpened)
		: this(connectionString, other.LoggingConfiguration)
	{
		m_dataSource = dataSource;
		m_hasBeenOpened = hasBeenOpened;
		ProvideClientCertificatesCallback = other.ProvideClientCertificatesCallback;
		ProvidePasswordCallback = other.ProvidePasswordCallback;
		RemoteCertificateValidationCallback = other.RemoteCertificateValidationCallback;
		ConnectionOpenedCallback = other.ConnectionOpenedCallback;
	}

	private void VerifyNotDisposed()
	{
		ObjectDisposedException.ThrowIf(m_isDisposed, this);
	}

	private async Task CloseAsync(bool changeState, IOBehavior ioBehavior)
	{
		if (m_activeReader != null || CurrentTransaction != null || m_enlistedTransaction != null || !(m_connectionSettings?.Pooling ?? false))
		{
			await DoCloseAsync(changeState, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
			return;
		}
		m_cachedProcedures = null;
		if (m_session != null)
		{
			await m_session.ReturnToPoolAsync(ioBehavior, this).ConfigureAwait(continueOnCapturedContext: false);
			m_session = null;
		}
		if (changeState)
		{
			SetState(ConnectionState.Closed);
		}
	}

	private async Task DoCloseAsync(bool changeState, IOBehavior ioBehavior)
	{
		if (m_enlistedTransaction != null)
		{
			EnlistedTransactionBase enlistedTransactionForClose = m_enlistedTransaction;
			if (m_activeReader != null)
			{
				await m_activeReader.DisposeAsync(ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
			}
			m_activeReader = null;
			bool flag = false;
			lock (s_lock)
			{
				if (s_transactionConnections.TryGetValue(enlistedTransactionForClose.Transaction, out List<EnlistedTransactionBase> value) && value.Contains(enlistedTransactionForClose))
				{
					MySqlConnection mySqlConnection = new MySqlConnection(this, m_dataSource, m_connectionString, hasBeenOpened: true)
					{
						m_connectionSettings = m_connectionSettings,
						m_connectionState = m_connectionState
					};
					mySqlConnection.TakeSessionFrom(this);
					foreach (EnlistedTransactionBase item in value)
					{
						if (item.Connection == this)
						{
							item.Connection = mySqlConnection;
							item.IsIdle = true;
							break;
						}
					}
					flag = true;
				}
			}
			if (flag)
			{
				if (changeState)
				{
					SetState(ConnectionState.Closed);
				}
				return;
			}
			m_enlistedTransaction = null;
		}
		m_cachedProcedures = null;
		try
		{
			if (m_activeReader != null || CurrentTransaction != null)
			{
				await CloseDatabaseAsync(ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		finally
		{
			if (m_session != null)
			{
				m_activeReader = null;
				if (GetInitializedConnectionSettings().Pooling)
				{
					await m_session.ReturnToPoolAsync(ioBehavior, this).ConfigureAwait(continueOnCapturedContext: false);
				}
				else
				{
					await m_session.DisposeAsync(ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
					m_session.OwningConnection = null;
				}
				m_session = null;
			}
			if (changeState)
			{
				SetState(ConnectionState.Closed);
			}
		}
	}

	private async ValueTask CloseDatabaseAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (m_activeReader != null)
		{
			await m_activeReader.DisposeAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (CurrentTransaction != null && m_session.IsConnected)
		{
			await CurrentTransaction.DisposeAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			CurrentTransaction = null;
		}
	}

	private ConnectionSettings GetConnectionSettings()
	{
		return m_connectionSettings ?? (m_connectionSettings = new ConnectionSettings(new MySqlConnectionStringBuilder(m_connectionString)));
	}

	private ConnectionSettings GetInitializedConnectionSettings()
	{
		return m_connectionSettings;
	}

	void IServerSessionOwner.SetState(ConnectionState newState)
	{
		SetState(newState);
	}
}
