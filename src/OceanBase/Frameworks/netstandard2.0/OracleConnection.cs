using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
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
/// <see cref="T:OceanBase.OracleConnection" /> represents a connection to a Oracle database.
/// </summary>
public sealed class OracleConnection : DbConnection, ICloneable, IServerSessionOwner
{
	private static readonly StateChangeEventArgs s_stateChangeClosedConnecting = new StateChangeEventArgs(ConnectionState.Closed, ConnectionState.Connecting);

	private static readonly StateChangeEventArgs s_stateChangeConnectingOpen = new StateChangeEventArgs(ConnectionState.Connecting, ConnectionState.Open);

	private static readonly StateChangeEventArgs s_stateChangeOpenClosed = new StateChangeEventArgs(ConnectionState.Open, ConnectionState.Closed);

	private static readonly object s_lock = new object();

	private static readonly Dictionary<Transaction, List<EnlistedTransactionBase>> s_transactionConnections = new Dictionary<Transaction, List<EnlistedTransactionBase>>();

	private static readonly ReadOnlyMemory<byte>[] s_startTransactionPayloads = new ReadOnlyMemory<byte>[30];

	private readonly OracleDataSource? m_dataSource;

	private readonly ILogger m_logger;

	private readonly ILogger m_transactionLogger;

	private string m_connectionString;

	private ConnectionSettings? m_connectionSettings;

	private ServerSession? m_session;

	private ConnectionState m_connectionState;

	private bool m_hasBeenOpened;

	private bool m_isDisposed;

	private Dictionary<string, CachedProcedure?>? m_cachedProcedures;

	private OracleDataReader? m_activeReader;

	private EnlistedTransactionBase? m_enlistedTransaction;

	private EnlistedTransactionBase? m_transferredEnlistedTransaction;

	private bool m_autoCommit = true;

	private bool m_autoCommitIsExplicit;

	private bool? m_autoCommitBeforeExplicitTransaction;

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
			m_autoCommitIsExplicit = false;
		}
	}

	/// <summary>
	/// Gets the character set / encoding name used for decoding character data (e.g., CLOB) for this connection.
	/// This is derived from the connection string (preferring JDBC-style <c>characterEncoding</c> if present, otherwise <c>CharacterSet</c>)
	/// and normalized to a .NET <see cref="T:System.Text.Encoding" /> name (defaults to <c>UTF-8</c>).
	/// </summary>
	public string CharacterSet => GetNormalizedEncodingName(GetConnectionSettings().ConnectionStringBuilder);

	/// <summary>
	/// Gets the local time zone information for the current process/host.
	/// This can be used by Oracle-mode types (e.g., TIMESTAMP WITH LOCAL TIME ZONE) that need local time zone context.
	/// </summary>
	public static TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;

	public override string Database => m_session?.DatabaseOverride ?? GetConnectionSettings().Database;

	public override ConnectionState State => m_connectionState;

	public override string DataSource => GetConnectionSettings().ConnectionStringBuilder.Server;

	/// <summary>
	/// Gets or sets whether each statement is automatically committed.
	/// The initial value comes from the connection string <c>AutoCommit</c> option (default <c>true</c>).
	/// Can be set before or after <see cref="M:OceanBase.OracleConnection.Open" />. Aligns with ODP.NET. Oracle mode only.
	/// </summary>
	public bool AutoCommit
	{
		get
		{
			if (m_session != null)
			{
				if (CurrentTransaction != null)
				{
					return false;
				}
				return m_session.OracleAutoCommit;
			}
			if (!m_autoCommitIsExplicit)
			{
				return GetConnectionSettings().AutoCommit;
			}
			return m_autoCommit;
		}
		set
		{
			SetAutoCommit(value);
		}
	}

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
	/// <see cref="P:OceanBase.OracleConnection.RemoteCertificateValidationCallback" /> for more information on the values passed to this delegate.</remarks>
	public RemoteCertificateValidationCallback? RemoteCertificateValidationCallback { get; set; }

	protected override DbProviderFactory DbProviderFactory => OracleClientFactory.Instance;

	/// <summary>
	/// Gets the time (in seconds) to wait while trying to establish a connection
	/// before terminating the attempt and generating an error. This value
	/// is controlled by <see cref="P:OceanBase.MySqlConnectionStringBuilder.ConnectionTimeout" />,
	/// which defaults to 15 seconds.
	/// </summary>
	public override int ConnectionTimeout => GetConnectionSettings().ConnectionTimeout;

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

	internal OracleTransaction? CurrentTransaction { get; set; }

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

	internal OracleDataSource? OracleDataSource => m_dataSource;

	Func<X509CertificateCollection, ValueTask>? IServerSessionOwner.ProvideClientCertificatesCallback => ProvideClientCertificatesCallback;

	Func<MySqlProvidePasswordContext, string>? IServerSessionOwner.ProvidePasswordCallback => ProvidePasswordCallback;

	RemoteCertificateValidationCallback? IServerSessionOwner.RemoteCertificateValidationCallback => RemoteCertificateValidationCallback;

	ZstandardPlugin? IServerSessionOwner.ZstandardPlugin => ZstandardPlugin;

	public event MySqlInfoMessageEventHandler? InfoMessage;

	public OracleConnection()
		: this("")
	{
	}

	public OracleConnection(string? connectionString)
		: this(connectionString ?? "", MySqlConnectorLoggingConfiguration.GlobalConfiguration)
	{
	}

	internal OracleConnection(OracleDataSource dataSource)
		: this(dataSource.ConnectionString, dataSource.LoggingConfiguration)
	{
		m_dataSource = dataSource;
	}

	private OracleConnection(string connectionString, MySqlConnectorLoggingConfiguration loggingConfiguration)
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
	/// <returns>A <see cref="T:OceanBase.OracleTransaction" /> representing the new database transaction.</returns>
	/// <remarks>Transactions may not be nested.</remarks>
	public new OracleTransaction BeginTransaction()
	{
		return BeginTransactionAsync(System.Data.IsolationLevel.Unspecified, null, IOBehavior.Synchronous, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Begins a database transaction.
	/// </summary>
	/// <param name="isolationLevel">The <see cref="T:System.Data.IsolationLevel" /> for the transaction.</param>
	/// <returns>A <see cref="T:OceanBase.OracleTransaction" /> representing the new database transaction.</returns>
	/// <remarks>Transactions may not be nested.</remarks>
	public new OracleTransaction BeginTransaction(System.Data.IsolationLevel isolationLevel)
	{
		return BeginTransactionAsync(isolationLevel, null, IOBehavior.Synchronous, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Begins a database transaction.
	/// </summary>
	/// <param name="isolationLevel">The <see cref="T:System.Data.IsolationLevel" /> for the transaction.</param>
	/// <param name="isReadOnly">If <c>true</c>, changes to tables used in the transaction are prohibited; otherwise, they are permitted.</param>
	/// <returns>A <see cref="T:OceanBase.OracleTransaction" /> representing the new database transaction.</returns>
	/// <remarks>Transactions may not be nested.</remarks>
	public OracleTransaction BeginTransaction(System.Data.IsolationLevel isolationLevel, bool isReadOnly)
	{
		return BeginTransactionAsync(isolationLevel, isReadOnly, IOBehavior.Synchronous, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Begins a database transaction.
	/// </summary>
	/// <param name="isolationLevel">The <see cref="T:System.Data.IsolationLevel" /> for the transaction.</param>
	/// <returns>A <see cref="T:OceanBase.OracleTransaction" /> representing the new database transaction.</returns>
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
	public ValueTask<OracleTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default(CancellationToken))
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
	public ValueTask<OracleTransaction> BeginTransactionAsync(System.Data.IsolationLevel isolationLevel, CancellationToken cancellationToken = default(CancellationToken))
	{
		return BeginTransactionAsync(isolationLevel, null, AsyncIOBehavior, cancellationToken);
	}

	/// <summary>
	/// Begins a database transaction asynchronously.
	/// </summary>
	/// <param name="isolationLevel">The <see cref="T:System.Data.IsolationLevel" /> for the transaction.</param>
	/// <param name="isReadOnly">If <c>true</c>, changes to tables used in the transaction are prohibited; otherwise, they are permitted.</param>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> representing the new database transaction.</returns>
	/// <remarks>Transactions may not be nested.</remarks>
	public ValueTask<OracleTransaction> BeginTransactionAsync(System.Data.IsolationLevel isolationLevel, bool isReadOnly, CancellationToken cancellationToken = default(CancellationToken))
	{
		return BeginTransactionAsync(isolationLevel, isReadOnly, AsyncIOBehavior, cancellationToken);
	}

	private async ValueTask<OracleTransaction> BeginTransactionAsync(System.Data.IsolationLevel isolationLevel, bool? isReadOnly, IOBehavior ioBehavior, CancellationToken cancellationToken)
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
		OracleTransaction oracleTransaction = new OracleTransaction(this, isolationLevel, m_transactionLogger);
		m_autoCommitBeforeExplicitTransaction = m_autoCommit;
		CurrentTransaction = oracleTransaction;
		return oracleTransaction;
	}

	/// <summary>
	/// Returns a <see cref="T:System.ReadOnlyMemory`1" /> containing two payloads to set the isolation level and start a transaction.
	/// </summary>
	internal static ReadOnlyMemory<byte> GetStartTransactionPayload(System.Data.IsolationLevel isolationLevel, bool? isReadOnly, bool supportsQueryAttributes)
	{
		int num = isolationLevel switch
		{
			System.Data.IsolationLevel.ReadUncommitted => throw new NotSupportedException("isolation level READ-UNCOMMITTED not supported. Use ReadCommitted, RepeatableRead, or Serializable."), 
			System.Data.IsolationLevel.ReadCommitted => 1, 
			System.Data.IsolationLevel.Serializable => 2, 
			System.Data.IsolationLevel.RepeatableRead => 3, 
			System.Data.IsolationLevel.Snapshot => throw new NotSupportedException("isolation level SNAPSHOT not supported. Use ReadCommitted, RepeatableRead, or Serializable."), 
			System.Data.IsolationLevel.Unspecified => 3, 
			_ => throw new NotSupportedException($"IsolationLevel.{isolationLevel} is not supported."), 
		};
		int num2 = 0;
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
		if ((object)transaction != null)
		{
			switch (transaction.IsolationLevel)
			{
			case System.Transactions.IsolationLevel.ReadUncommitted:
				throw new NotSupportedException("transaction scope isolation level READ-UNCOMMITTED not supported. Use ReadCommitted, RepeatableRead, or Serializable.");
			case System.Transactions.IsolationLevel.Snapshot:
				throw new NotSupportedException("transaction scope isolation level SNAPSHOT not supported. Use ReadCommitted, RepeatableRead, or Serializable.");
			}
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
			throw new InvalidOperationException("Can't enlist in a Transaction when there is an active OracleTransaction.");
		}
		if ((object)transaction == null)
		{
			return;
		}
		OracleConnection oracleConnection = FindExistingEnlistedSession(transaction);
		if (oracleConnection != null)
		{
			CloseAsync(changeState: false, IOBehavior.Synchronous).GetAwaiter().GetResult();
			TakeSessionFrom(oracleConnection);
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
		EnlistedTransactionBase enlistedTransactionBase = m_enlistedTransaction ?? m_transferredEnlistedTransaction;
		if (enlistedTransactionBase == null)
		{
			throw new InvalidOperationException("Didn't find enlisted transaction");
		}
		Transaction transaction = enlistedTransactionBase.Transaction;
		OracleConnection oracleConnection = null;
		bool flag = false;
		lock (s_lock)
		{
			if (!s_transactionConnections.TryGetValue(transaction, out List<EnlistedTransactionBase> value))
			{
				throw new InvalidOperationException("Didn't find transaction");
			}
			for (int i = 0; i < value.Count; i++)
			{
				if (value[i] != enlistedTransactionBase)
				{
					continue;
				}
				EnlistedTransactionBase enlistedTransactionBase2 = value[i];
				if (enlistedTransactionBase2.Connection is OracleConnection oracleConnection2)
				{
					if (oracleConnection2.m_enlistedTransaction == enlistedTransactionBase2)
					{
						oracleConnection2.m_enlistedTransaction = null;
					}
					if (oracleConnection2.m_transferredEnlistedTransaction == enlistedTransactionBase2)
					{
						oracleConnection2.m_transferredEnlistedTransaction = null;
					}
					if (enlistedTransactionBase2.IsIdle)
					{
						oracleConnection = oracleConnection2;
					}
				}
				if (m_enlistedTransaction == enlistedTransactionBase2)
				{
					m_enlistedTransaction = null;
				}
				if (m_transferredEnlistedTransaction == enlistedTransactionBase2)
				{
					m_transferredEnlistedTransaction = null;
				}
				value.RemoveAt(i);
				flag = true;
				break;
			}
			if (value.Count == 0)
			{
				s_transactionConnections.Remove(transaction);
			}
		}
		if (!flag)
		{
			throw new InvalidOperationException("Didn't find transaction");
		}
		oracleConnection?.Close();
	}

	private OracleConnection? FindExistingEnlistedSession(Transaction transaction)
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
					if (item.IsIdle && item.Connection is OracleConnection oracleConnection && oracleConnection.m_connectionString == m_connectionString && oracleConnection.m_autoCommit == GetEffectiveOracleAutoCommitForSessionInit(GetInitializedConnectionSettings()))
					{
						item.Connection = this;
						item.IsIdle = false;
						return oracleConnection;
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

	private void TakeSessionFrom(OracleConnection other)
	{
		m_session = other.m_session;
		m_session.OwningConnection = new WeakReference<IServerSessionOwner>(this);
		other.m_session = null;
		if (!m_autoCommitIsExplicit)
		{
			m_autoCommit = GetInitializedConnectionSettings().AutoCommit;
		}
		m_cachedProcedures = other.m_cachedProcedures;
		other.m_cachedProcedures = null;
		m_enlistedTransaction = other.m_enlistedTransaction;
		other.m_enlistedTransaction = null;
	}

	public override void Close()
	{
		CloseAsync(changeState: true, IOBehavior.Synchronous).GetAwaiter().GetResult();
	}

	public Task CloseAsync()
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

	public Task ChangeDatabaseAsync(string databaseName, CancellationToken cancellationToken = default(CancellationToken))
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

	public new OracleCommand CreateCommand()
	{
		return (OracleCommand)base.CreateCommand();
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
		Activity activity = ActivitySourceHelper.StartActivity("Open");
		try
		{
			_ = 3;
			try
			{
				SetState(ConnectionState.Connecting);
				try
				{
					ConnectionPool connectionPool = m_dataSource?.Pool ?? ConnectionPool.GetPool(m_connectionString, LoggingConfiguration, createIfNotFound: true, useOracleConnectionStringBuilder: true);
					if (m_connectionSettings == null)
					{
						m_connectionSettings = connectionPool?.ConnectionSettings ?? new ConnectionSettings(new OracleConnectionStringBuilder(m_connectionString));
					}
					if (string.IsNullOrWhiteSpace(m_connectionSettings.Database))
					{
						throw new ArgumentException("Database (schema) name is required for Oracle mode connections. Please specify the 'Database' parameter in the connection string.");
					}
					if (m_connectionSettings.AutoEnlist && (object)Transaction.Current != null)
					{
						OracleConnection oracleConnection = FindExistingEnlistedSession(Transaction.Current);
						if (oracleConnection != null)
						{
							TakeSessionFrom(oracleConnection);
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
					if (!m_autoCommitIsExplicit)
					{
						m_autoCommit = m_connectionSettings.AutoCommit;
					}
					if (m_session.OracleAutoCommit != m_autoCommit)
					{
						await ApplyAutoCommitAsync(m_autoCommit, m_autoCommit && !m_session.OracleAutoCommit, ioBehavior ?? AsyncIOBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					}
					ConnectionOpenedCallback connectionOpenedCallback2 = ConnectionOpenedCallback;
					if (connectionOpenedCallback2 != null)
					{
						cancellationToken.ThrowIfCancellationRequested();
						await connectionOpenedCallback2(new ConnectionOpenedContext(this, m_session.Conditions), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					}
				}
				catch (Exception)
				{
					if (State == ConnectionState.Connecting)
					{
						SetState(ConnectionState.Closed);
					}
					throw;
				}
			}
			catch (Exception exception) when (activity?.IsAllDataRequested ?? false)
			{
				if (State == ConnectionState.Connecting)
				{
					SetState(ConnectionState.Closed);
				}
				MySqlConnectionStringBuilder mySqlConnectionStringBuilder = m_connectionSettings?.ConnectionStringBuilder;
				if (mySqlConnectionStringBuilder != null)
				{
					activity.SetTag("db.connection_string", (object)mySqlConnectionStringBuilder.GetConnectionString(mySqlConnectionStringBuilder.PersistSecurityInfo));
				}
				activity.SetException(exception);
				throw;
			}
		}
		finally
		{
			((IDisposable)activity)?.Dispose();
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
			await session.ReinitializeOracleSessionAfterResetAsync(GetConnectionSettings(), AsyncIOBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (m_session.OracleAutoCommit != m_autoCommit)
			{
				await ApplyAutoCommitAsync(m_autoCommit, m_autoCommit && !m_session.OracleAutoCommit, AsyncIOBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
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
	/// <param name="connection">The <see cref="T:OceanBase.OracleConnection" /> whose connection pool will be cleared.</param>
	public static void ClearPool(OracleConnection connection)
	{
		ClearPoolAsync(connection, IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Asynchronously clears the connection pool that <paramref name="connection" /> belongs to.
	/// </summary>
	/// <param name="connection">The <see cref="T:OceanBase.OracleConnection" /> whose connection pool will be cleared.</param>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task" /> representing the asynchronous operation.</returns>
	public static Task ClearPoolAsync(OracleConnection connection, CancellationToken cancellationToken = default(CancellationToken))
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

	private static async Task ClearPoolAsync(OracleConnection connection, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (connection == null)
		{
			throw new ArgumentNullException("connection");
		}
		ConnectionPool pool = ConnectionPool.GetPool(connection.m_connectionString, null, createIfNotFound: false, useOracleConnectionStringBuilder: true);
		if (pool != null)
		{
			await pool.ClearAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	protected override DbCommand CreateDbCommand()
	{
		return new OracleCommand(this, null);
	}

	/// <summary>
	/// Returns schema information for the data source of this <see cref="T:OceanBase.OracleConnection" />.
	/// </summary>
	/// <returns>A <see cref="T:System.Data.DataTable" /> containing schema information.</returns>
	public override DataTable GetSchema()
	{
		return GetSchemaProvider().GetSchemaAsync(IOBehavior.Synchronous, "MetaDataCollections", null, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Returns schema information for the data source of this <see cref="T:OceanBase.OracleConnection" />.
	/// </summary>
	/// <param name="collectionName">The name of the schema to return. See <a href="https://mysqlconnector.net/overview/schema-collections/">Supported Schema Collections</a> for the list of supported schema names.</param>
	/// <returns>A <see cref="T:System.Data.DataTable" /> containing schema information.</returns>
	public override DataTable GetSchema(string collectionName)
	{
		return GetSchemaProvider().GetSchemaAsync(IOBehavior.Synchronous, collectionName, null, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Returns schema information for the data source of this <see cref="T:OceanBase.OracleConnection" />.
	/// </summary>
	/// <param name="collectionName">The name of the schema to return. See <a href="https://mysqlconnector.net/overview/schema-collections/">Supported Schema Collections</a> for the list of supported schema names.</param>
	/// <param name="restrictionValues">The restrictions to apply to the schema.</param>
	/// <returns>A <see cref="T:System.Data.DataTable" /> containing schema information.</returns>
	public override DataTable GetSchema(string collectionName, string?[] restrictionValues)
	{
		return GetSchemaProvider().GetSchemaAsync(IOBehavior.Synchronous, collectionName, restrictionValues, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Asynchronously returns schema information for the data source of this <see cref="T:OceanBase.OracleConnection" />.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> containing schema information.</returns>
	/// <remarks>The proposed ADO.NET API that this is based on is not finalized; this API may change in the future.</remarks>
	public Task<DataTable> GetSchemaAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return GetSchemaProvider().GetSchemaAsync(AsyncIOBehavior, "MetaDataCollections", null, cancellationToken).AsTask();
	}

	/// <summary>
	/// Asynchronously returns schema information for the data source of this <see cref="T:OceanBase.OracleConnection" />.
	/// </summary>
	/// <param name="collectionName">The name of the schema to return.</param>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> containing schema information.</returns>
	/// <remarks>The proposed ADO.NET API that this is based on is not finalized; this API may change in the future.</remarks>
	public Task<DataTable> GetSchemaAsync(string collectionName, CancellationToken cancellationToken = default(CancellationToken))
	{
		return GetSchemaProvider().GetSchemaAsync(AsyncIOBehavior, collectionName, null, cancellationToken).AsTask();
	}

	/// <summary>
	/// Asynchronously returns schema information for the data source of this <see cref="T:OceanBase.OracleConnection" />.
	/// </summary>
	/// <param name="collectionName">The name of the schema to return.</param>
	/// <param name="restrictionValues">The restrictions to apply to the schema.</param>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> containing schema information.</returns>
	/// <remarks>The proposed ADO.NET API that this is based on is not finalized; this API may change in the future.</remarks>
	public Task<DataTable> GetSchemaAsync(string collectionName, string?[] restrictionValues, CancellationToken cancellationToken = default(CancellationToken))
	{
		return GetSchemaProvider().GetSchemaAsync(AsyncIOBehavior, collectionName, restrictionValues, cancellationToken).AsTask();
	}

	private SchemaProvider GetSchemaProvider()
	{
		throw new NotSupportedException("Schema retrieval is not supported for OracleConnection.");
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

	public async Task DisposeAsync()
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

	public OracleConnection Clone()
	{
		return new OracleConnection(this, m_dataSource, m_connectionString, m_hasBeenOpened);
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
	/// <returns>A new <see cref="T:OceanBase.OracleConnection" /> with different connection string options but
	/// the same password as this connection (unless overridden by <paramref name="connectionString" />).</returns>
	public OracleConnection CloneWith(string connectionString)
	{
		OracleConnectionStringBuilder oracleConnectionStringBuilder = new OracleConnectionStringBuilder(connectionString ?? throw new ArgumentNullException("connectionString"));
		MySqlConnectionStringBuilder connectionStringBuilder = GetConnectionSettings().ConnectionStringBuilder;
		bool flag = oracleConnectionStringBuilder.Password.Length == 0 && (!oracleConnectionStringBuilder.PersistSecurityInfo || connectionStringBuilder.PersistSecurityInfo);
		if (flag)
		{
			oracleConnectionStringBuilder.Password = connectionStringBuilder.Password;
		}
		string connectionString2 = oracleConnectionStringBuilder.ConnectionString;
		OracleDataSource dataSource = ((connectionString2 == connectionStringBuilder.ConnectionString) ? m_dataSource : null);
		return new OracleConnection(this, dataSource, connectionString2, m_hasBeenOpened && flag && !connectionStringBuilder.PersistSecurityInfo);
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
			OracleConnectionStringBuilder oracleConnectionStringBuilder = new OracleConnectionStringBuilder(m_connectionString)
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
					oracleConnectionStringBuilder.Server = session.HostName;
					oracleConnectionStringBuilder.Port = (uint)port;
					iPAddress = address;
				}
			}
			oracleConnectionStringBuilder.UserID = session.UserID;
			int cancellationTimeout = GetConnectionSettings().CancellationTimeout;
			oracleConnectionStringBuilder.ConnectionTimeout = ((cancellationTimeout < 1) ? 3u : ((uint)cancellationTimeout));
			ConnectionSettings connectionSettings = new ConnectionSettings(oracleConnectionStringBuilder);
			if (iPAddress != null)
			{
				connectionSettings = connectionSettings.CloneWith(iPAddress);
			}
			using OracleConnection oracleConnection = CloneWith(oracleConnectionStringBuilder.ConnectionString);
			oracleConnection.m_connectionSettings = connectionSettings;
			oracleConnection.ConnectionOpenedCallback = null;
			oracleConnection.Open();
			int num = command.Session?.ConnectionId ?? session.ConnectionId;
			using OracleCommand oracleCommand = new OracleCommand(FormattableString.Invariant($"KILL QUERY {num}"), oracleConnection);
			oracleCommand.CommandTimeout = ((cancellationTimeout < 1) ? 3 : cancellationTimeout);
			session.DoCancel(command, oracleCommand);
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
		bool isOracleMode = true;
		NormalizedSchema normalized = NormalizedSchema.MustNormalize(name, Database, isOracleMode);
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
			value = await CachedProcedure.FillAsync(ioBehavior, this, normalized.Schema, normalized.Component, normalized.PackageName, m_logger, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
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

	internal void SetActiveReader(OracleDataReader dataReader)
	{
		if (dataReader == null)
		{
			throw new ArgumentNullException("dataReader");
		}
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
		using (OracleCommand oracleCommand = new OracleCommand("SHOW WARNINGS;", this))
		{
			oracleCommand.Transaction = CurrentTransaction;
			using OracleDataReader oracleDataReader = oracleCommand.ExecuteReader();
			while (oracleDataReader.Read())
			{
				list.Add(new MySqlError(oracleDataReader.GetString(0), oracleDataReader.GetInt32(1), oracleDataReader.GetString(2)));
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

	private OracleConnection(OracleConnection other, OracleDataSource? dataSource, string connectionString, bool hasBeenOpened)
		: this(connectionString, other.LoggingConfiguration)
	{
		m_dataSource = dataSource;
		m_hasBeenOpened = hasBeenOpened;
		ProvideClientCertificatesCallback = other.ProvideClientCertificatesCallback;
		ProvidePasswordCallback = other.ProvidePasswordCallback;
		RemoteCertificateValidationCallback = other.RemoteCertificateValidationCallback;
		ConnectionOpenedCallback = other.ConnectionOpenedCallback;
		m_autoCommit = other.m_autoCommit;
		m_autoCommitIsExplicit = other.m_autoCommitIsExplicit;
	}

	private void VerifyNotDisposed()
	{
		if (m_isDisposed)
		{
			throw new ObjectDisposedException(GetType().Name);
		}
	}

	private async Task CloseAsync(bool changeState, IOBehavior ioBehavior)
	{
		if (m_activeReader == null && CurrentTransaction == null && m_enlistedTransaction == null && m_autoCommit)
		{
			bool? flag = m_session?.OracleAutoCommit;
			if (flag.HasValue && flag == true && (m_connectionSettings?.Pooling ?? false))
			{
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
				return;
			}
		}
		await DoCloseAsync(changeState, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
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
					OracleConnection oracleConnection = new OracleConnection(this, m_dataSource, m_connectionString, hasBeenOpened: true)
					{
						m_connectionSettings = m_connectionSettings,
						m_connectionState = m_connectionState
					};
					m_transferredEnlistedTransaction = enlistedTransactionForClose;
					oracleConnection.TakeSessionFrom(this);
					foreach (EnlistedTransactionBase item in value)
					{
						if (item.Connection == this)
						{
							item.Connection = oracleConnection;
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
			m_transferredEnlistedTransaction = null;
		}
		m_cachedProcedures = null;
		bool pooling = m_connectionSettings?.Pooling ?? false;
		bool returnToPool = false;
		try
		{
			if (m_activeReader != null || CurrentTransaction != null)
			{
				await CloseDatabaseAsync(ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
				returnToPool = pooling;
			}
			else
			{
				returnToPool = await RollbackImplicitOracleTransactionAsync(ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false) && pooling;
			}
		}
		finally
		{
			if (m_session != null)
			{
				m_activeReader = null;
				if (returnToPool)
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

	private async ValueTask<bool> RollbackImplicitOracleTransactionAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (m_session == null || !m_session.IsConnected || (m_autoCommit && m_session.OracleAutoCommit))
		{
			return true;
		}
		try
		{
			using OracleCommand cmd = new OracleCommand("rollback", this)
			{
				NoActivity = true
			};
			await cmd.ExecuteNonQueryAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return true;
		}
		catch (IOException)
		{
			return false;
		}
		catch (SocketException)
		{
			return false;
		}
		catch (MySqlException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
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

	internal async ValueTask RestoreAutoCommitAfterExplicitTransactionAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		bool? autoCommitBeforeExplicitTransaction = m_autoCommitBeforeExplicitTransaction;
		if (!autoCommitBeforeExplicitTransaction.HasValue)
		{
			return;
		}
		bool valueOrDefault = autoCommitBeforeExplicitTransaction == true;
		m_autoCommitBeforeExplicitTransaction = null;
		if (m_autoCommit != valueOrDefault)
		{
			m_autoCommit = valueOrDefault;
			if (State == ConnectionState.Open && m_session != null)
			{
				await ApplyAutoCommitAsync(valueOrDefault, valueOrDefault, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
	}

	private void SetAutoCommit(bool value)
	{
		VerifyNotDisposed();
		if (AutoCommit != value)
		{
			if (m_enlistedTransaction != null)
			{
				throw new InvalidOperationException("Cannot set AutoCommit when enlisted in a transaction.");
			}
			if (CurrentTransaction != null)
			{
				throw new InvalidOperationException("Cannot set AutoCommit while a transaction is active.");
			}
			if (State == ConnectionState.Open)
			{
				ApplyAutoCommitAsync(value, value, AsyncIOBehavior, CancellationToken.None).AsTask().GetAwaiter().GetResult();
				m_autoCommitIsExplicit = true;
				m_autoCommit = value;
			}
			else
			{
				m_autoCommitIsExplicit = true;
				m_autoCommit = value;
			}
		}
	}

	private async ValueTask ApplyAutoCommitAsync(bool autoCommit, bool commitBeforeEnablingAutoCommit, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (m_session == null || !m_session.IsConnected)
		{
			return;
		}
		if (autoCommit && commitBeforeEnablingAutoCommit && CurrentTransaction == null && !m_session.OracleAutoCommit)
		{
			using OracleCommand cmd = new OracleCommand("commit", this)
			{
				NoActivity = true
			};
			await cmd.ExecuteNonQueryAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		await m_session.SetOracleAutoCommitAsync(autoCommit, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private ConnectionSettings GetConnectionSettings()
	{
		return m_connectionSettings ?? (m_connectionSettings = new ConnectionSettings(new OracleConnectionStringBuilder(m_connectionString)));
	}

	private ConnectionSettings GetInitializedConnectionSettings()
	{
		return m_connectionSettings;
	}

	/// <summary>
	/// Connect/Reset 时 session 应对齐的 autocommit：已显式 set <see cref="P:OceanBase.OracleConnection.AutoCommit" /> 用属性值，否则用连接串默认。
	/// </summary>
	internal bool GetEffectiveOracleAutoCommitForSessionInit(ConnectionSettings cs)
	{
		if (!m_autoCommitIsExplicit)
		{
			return cs.AutoCommit;
		}
		return m_autoCommit;
	}

	private static string GetNormalizedEncodingName(MySqlConnectionStringBuilder csb)
	{
		string characterSet = csb.CharacterSet;
		if (string.IsNullOrWhiteSpace(characterSet))
		{
			return "UTF-8";
		}
		if (characterSet.IndexOf("utf8", StringComparison.OrdinalIgnoreCase) >= 0 || characterSet.IndexOf("utf-8", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return "UTF-8";
		}
		if (characterSet.Equals("latin1", StringComparison.OrdinalIgnoreCase))
		{
			return "ISO-8859-1";
		}
		return characterSet;
	}

	void IServerSessionOwner.SetState(ConnectionState newState)
	{
		SetState(newState);
	}
}
