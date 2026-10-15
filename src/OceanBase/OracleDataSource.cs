using System;
using System.Data.Common;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OceanBase.Core;
using OceanBase.Logging;
using OceanBase.Plugins;
using OceanBase.Protocol.Serialization;

namespace OceanBase;

/// <summary>
/// <see cref="T:OceanBase.OracleDataSource" /> implements an Oracle-mode data source which can be used to obtain open connections.
/// </summary>
public sealed class OracleDataSource : DbDataSource, IMySqlConnectorDataSource
{
	private static int s_lastId;

	private readonly ILogger m_logger;

	private readonly int m_id;

	private readonly string m_connectionString;

	private readonly Func<X509CertificateCollection, ValueTask>? m_clientCertificatesCallback;

	private readonly RemoteCertificateValidationCallback? m_remoteCertificateValidationCallback;

	private readonly Func<MySqlProvidePasswordContext, CancellationToken, ValueTask<string>>? m_periodicPasswordProvider;

	private readonly TimeSpan m_periodicPasswordProviderSuccessRefreshInterval;

	private readonly TimeSpan m_periodicPasswordProviderFailureRefreshInterval;

	private readonly ZstandardPlugin? m_zstandardPlugin;

	private readonly ConnectionOpenedCallback? m_connectionOpenedCallback;

	private readonly MySqlProvidePasswordContext? m_providePasswordContext;

	private readonly CancellationTokenSource? m_passwordProviderTimerCancellationTokenSource;

	private readonly Timer? m_passwordProviderTimer;

	private readonly Task? m_initialPasswordRefreshTask;

	private bool m_isDisposed;

	private string? m_password;

	private Func<MySqlProvidePasswordContext, string>? m_providePasswordCallback;

	/// <summary>
	/// Gets the connection string of the database represented by this <see cref="T:OceanBase.OracleDataSource" />.
	/// </summary>
	public override string ConnectionString => m_connectionString;

	/// <summary>
	/// Sets the password that will be used by the next <see cref="T:OceanBase.OracleConnection" /> created from this <see cref="T:OceanBase.OracleDataSource" />.
	/// </summary>
	/// <remarks>
	/// <para>This can be used to update the password for database servers that periodically rotate authentication tokens, without
	/// affecting connection pooling. The <see cref="P:OceanBase.OracleConnectionStringBuilder.Password" /> property must not be specified in
	/// order for this field to be used.</para>
	/// <para>Consider using <see cref="M:OceanBase.OracleDataSourceBuilder.UsePeriodicPasswordProvider(System.Func{OceanBase.MySqlProvidePasswordContext,System.Threading.CancellationToken,System.Threading.Tasks.ValueTask{System.String}},System.TimeSpan,System.TimeSpan)" /> instead.</para>
	/// </remarks>
	public string Password
	{
		set
		{
			if (m_periodicPasswordProvider != null)
			{
				throw new InvalidOperationException("Cannot set Password when this OracleDataSource is configured with a PeriodicPasswordProvider.");
			}
			m_password = value ?? throw new ArgumentNullException("value");
			m_providePasswordCallback = ProvidePasswordFromField;
		}
	}

	internal ConnectionPool? Pool { get; }

	internal MySqlConnectorLoggingConfiguration LoggingConfiguration { get; }

	internal MySqlConnectorTracingOptions TracingOptions { get; }

	internal string? Name { get; }

	MySqlConnectorTracingOptions IMySqlConnectorDataSource.TracingOptions => TracingOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="T:OceanBase.OracleDataSource" /> class.
	/// </summary>
	/// <param name="connectionString">The connection string for the Oracle-mode server. This parameter is required.</param>
	/// <exception cref="T:System.ArgumentNullException">Thrown if <paramref name="connectionString" /> is <c>null</c>.</exception>
	public OracleDataSource(string connectionString)
		: this(connectionString ?? throw new ArgumentNullException("connectionString"), MySqlConnectorLoggingConfiguration.NullConfiguration, null, null, null, null, null, default(TimeSpan), default(TimeSpan), null, null)
	{
	}

	internal OracleDataSource(string connectionString, MySqlConnectorLoggingConfiguration loggingConfiguration, MySqlConnectorTracingOptions? tracingOptions, string? name, Func<X509CertificateCollection, ValueTask>? clientCertificatesCallback, RemoteCertificateValidationCallback? remoteCertificateValidationCallback, Func<MySqlProvidePasswordContext, CancellationToken, ValueTask<string>>? periodicPasswordProvider, TimeSpan periodicPasswordProviderSuccessRefreshInterval, TimeSpan periodicPasswordProviderFailureRefreshInterval, ZstandardPlugin? zstandardPlugin, ConnectionOpenedCallback? connectionOpenedCallback)
	{
		m_connectionString = connectionString;
		LoggingConfiguration = loggingConfiguration;
		TracingOptions = tracingOptions ?? MySqlConnectorTracingOptions.Default;
		Name = name;
		m_clientCertificatesCallback = clientCertificatesCallback;
		m_remoteCertificateValidationCallback = remoteCertificateValidationCallback;
		m_logger = loggingConfiguration.DataSourceLogger;
		m_zstandardPlugin = zstandardPlugin;
		m_connectionOpenedCallback = connectionOpenedCallback;
		Pool = ConnectionPool.CreatePool(m_connectionString, LoggingConfiguration, name, useOracleConnectionStringBuilder: true);
		m_id = Interlocked.Increment(ref s_lastId);
		if (Pool != null && Name != null)
		{
			Log.DataSourceCreatedWithPoolWithName(m_logger, m_id, Pool.Id, Name);
		}
		else if (Pool != null)
		{
			Log.DataSourceCreatedWithPoolWithoutName(m_logger, m_id, Pool.Id);
		}
		else if (Name != null)
		{
			Log.DataSourceCreatedWithoutPoolWithName(m_logger, m_id, Name);
		}
		else
		{
			Log.DataSourceCreatedWithoutPoolWithoutName(m_logger, m_id);
		}
		if (periodicPasswordProvider != null)
		{
			m_periodicPasswordProvider = periodicPasswordProvider;
			m_periodicPasswordProviderSuccessRefreshInterval = periodicPasswordProviderSuccessRefreshInterval;
			m_periodicPasswordProviderFailureRefreshInterval = periodicPasswordProviderFailureRefreshInterval;
			m_passwordProviderTimerCancellationTokenSource = new CancellationTokenSource();
			OracleConnectionStringBuilder oracleConnectionStringBuilder = new OracleConnectionStringBuilder(m_connectionString);
			m_providePasswordContext = new MySqlProvidePasswordContext(oracleConnectionStringBuilder.Server, (int)oracleConnectionStringBuilder.Port, oracleConnectionStringBuilder.UserID, oracleConnectionStringBuilder.Database);
			m_passwordProviderTimer = new Timer(delegate(object? _)
			{
				_ = RefreshPassword();
			}, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
			m_initialPasswordRefreshTask = Task.Run((Func<Task?>)RefreshPassword);
			m_providePasswordCallback = ProvidePasswordFromInitialRefreshTask;
		}
	}

	/// <summary>
	/// Creates a new <see cref="T:OceanBase.OracleConnection" /> that can connect to the database represented by this <see cref="T:OceanBase.OracleDataSource" />.
	/// </summary>
	public new OracleConnection CreateConnection()
	{
		return (OracleConnection)base.CreateConnection();
	}

	/// <summary>
	/// Returns a new, open <see cref="T:OceanBase.OracleConnection" /> to the database represented by this <see cref="T:OceanBase.OracleDataSource" />.
	/// </summary>
	public new OracleConnection OpenConnection()
	{
		return (OracleConnection)base.OpenConnection();
	}

	/// <summary>
	/// Asynchronously returns a new, open <see cref="T:OceanBase.OracleConnection" /> to the database represented by this <see cref="T:OceanBase.OracleDataSource" />.
	/// </summary>
	public new async ValueTask<OracleConnection> OpenConnectionAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return (OracleConnection)(await base.OpenConnectionAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}

	protected override DbConnection CreateDbConnection()
	{
		ObjectDisposedException.ThrowIf(m_isDisposed, this);
		return new OracleConnection(this)
		{
			ProvideClientCertificatesCallback = m_clientCertificatesCallback,
			ProvidePasswordCallback = m_providePasswordCallback,
			RemoteCertificateValidationCallback = m_remoteCertificateValidationCallback,
			ConnectionOpenedCallback = m_connectionOpenedCallback
		};
	}

	protected override void Dispose(bool disposing)
	{
		try
		{
			if (disposing)
			{
				DisposeAsync(IOBehavior.Synchronous).GetAwaiter().GetResult();
			}
		}
		finally
		{
			base.Dispose(disposing);
		}
	}

	protected override ValueTask DisposeAsyncCore()
	{
		return DisposeAsync(IOBehavior.Asynchronous);
	}

	private async ValueTask DisposeAsync(IOBehavior ioBehavior)
	{
		CancellationTokenSource passwordProviderTimerCancellationTokenSource = m_passwordProviderTimerCancellationTokenSource;
		if (passwordProviderTimerCancellationTokenSource != null)
		{
			passwordProviderTimerCancellationTokenSource.Cancel();
			passwordProviderTimerCancellationTokenSource.Dispose();
		}
		if (Pool != null)
		{
			await Pool.ClearAsync(ioBehavior, default(CancellationToken)).ConfigureAwait(continueOnCapturedContext: false);
			Pool.Dispose();
		}
		m_isDisposed = true;
	}

	private async Task RefreshPassword()
	{
		try
		{
			m_password = await m_periodicPasswordProvider(m_providePasswordContext, m_passwordProviderTimerCancellationTokenSource.Token).ConfigureAwait(continueOnCapturedContext: false);
			m_providePasswordCallback = ProvidePasswordFromField;
			m_passwordProviderTimer.Change(m_periodicPasswordProviderSuccessRefreshInterval, Timeout.InfiniteTimeSpan);
		}
		catch (Exception ex)
		{
			Log.PeriodicPasswordProviderFailed(m_logger, ex, m_id, ex.Message);
			m_passwordProviderTimer.Change(m_periodicPasswordProviderFailureRefreshInterval, Timeout.InfiniteTimeSpan);
			throw new MySqlException("The periodic password provider failed", ex);
		}
	}

	private string ProvidePasswordFromField(MySqlProvidePasswordContext context)
	{
		return m_password;
	}

	private string ProvidePasswordFromInitialRefreshTask(MySqlProvidePasswordContext context)
	{
		if (m_password == null)
		{
			m_initialPasswordRefreshTask.GetAwaiter().GetResult();
			m_providePasswordCallback = ProvidePasswordFromField;
		}
		return m_password;
	}
}
