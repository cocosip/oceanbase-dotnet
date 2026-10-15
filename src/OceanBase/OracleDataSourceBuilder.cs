using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OceanBase.Logging;
using OceanBase.Plugins;

namespace OceanBase;

/// <summary>
/// <see cref="T:OceanBase.OracleDataSourceBuilder" /> provides an API for configuring and creating an <see cref="T:OceanBase.OracleDataSource" />,
/// from which <see cref="T:OceanBase.OracleConnection" /> objects can be obtained.
/// </summary>
public sealed class OracleDataSourceBuilder
{
	private ILoggerFactory? m_loggerFactory;

	private string? m_name;

	private Func<X509CertificateCollection, ValueTask>? m_clientCertificatesCallback;

	private RemoteCertificateValidationCallback? m_remoteCertificateValidationCallback;

	private Func<MySqlProvidePasswordContext, CancellationToken, ValueTask<string>>? m_periodicPasswordProvider;

	private TimeSpan m_periodicPasswordProviderSuccessRefreshInterval;

	private TimeSpan m_periodicPasswordProviderFailureRefreshInterval;

	private ConnectionOpenedCallback? m_connectionOpenedCallback;

	private List<Action<MySqlConnectorTracingOptionsBuilder>>? m_tracingOptionsBuilderCallbacks;

	/// <summary>
	/// A <see cref="T:OceanBase.OracleConnectionStringBuilder" /> that can be used to configure the connection string on this <see cref="T:OceanBase.OracleDataSourceBuilder" />.
	/// </summary>
	public OracleConnectionStringBuilder ConnectionStringBuilder { get; }

	internal ZstandardPlugin? ZstandardPlugin { get; set; }

	/// <summary>
	/// Initializes a new <see cref="T:OceanBase.OracleDataSourceBuilder" /> with the specified connection string.
	/// </summary>
	/// <param name="connectionString">The optional connection string to use.</param>
	public OracleDataSourceBuilder(string? connectionString = null)
	{
		ConnectionStringBuilder = new OracleConnectionStringBuilder(connectionString ?? "");
	}

	/// <summary>
	/// Configures OpenTelemetry tracing options.
	/// </summary>
	public OracleDataSourceBuilder ConfigureTracing(Action<MySqlConnectorTracingOptionsBuilder> configureAction)
	{
		ArgumentNullException.ThrowIfNull(configureAction, "configureAction");
		if (m_tracingOptionsBuilderCallbacks == null)
		{
			m_tracingOptionsBuilderCallbacks = new List<Action<MySqlConnectorTracingOptionsBuilder>>();
		}
		m_tracingOptionsBuilderCallbacks.Add(configureAction);
		return this;
	}

	/// <summary>
	/// Sets the <see cref="T:Microsoft.Extensions.Logging.ILoggerFactory" /> that will be used for logging.
	/// </summary>
	public OracleDataSourceBuilder UseLoggerFactory(ILoggerFactory? loggerFactory)
	{
		m_loggerFactory = loggerFactory;
		return this;
	}

	/// <summary>
	/// Sets the name of the <see cref="T:OceanBase.OracleDataSource" /> that will be created.
	/// </summary>
	public OracleDataSourceBuilder UseName(string? name)
	{
		m_name = name;
		return this;
	}

	/// <summary>
	/// Sets the callback used to provide client certificates for connecting to a server.
	/// </summary>
	public OracleDataSourceBuilder UseClientCertificatesCallback(Func<X509CertificateCollection, ValueTask>? callback)
	{
		m_clientCertificatesCallback = callback;
		return this;
	}

	/// <summary>
	/// Configures a periodic password provider, which is automatically called by the data source at some regular interval.
	/// </summary>
	public OracleDataSourceBuilder UsePeriodicPasswordProvider(Func<MySqlProvidePasswordContext, CancellationToken, ValueTask<string>>? passwordProvider, TimeSpan successRefreshInterval, TimeSpan failureRefreshInterval)
	{
		m_periodicPasswordProvider = passwordProvider;
		m_periodicPasswordProviderSuccessRefreshInterval = successRefreshInterval;
		m_periodicPasswordProviderFailureRefreshInterval = failureRefreshInterval;
		return this;
	}

	/// <summary>
	/// Sets the callback used to verify that the server's certificate is valid.
	/// </summary>
	public OracleDataSourceBuilder UseRemoteCertificateValidationCallback(RemoteCertificateValidationCallback callback)
	{
		m_remoteCertificateValidationCallback = callback;
		return this;
	}

	/// <summary>
	/// Adds a callback that is invoked when a new <see cref="T:OceanBase.OracleConnection" /> is opened.
	/// </summary>
	public OracleDataSourceBuilder UseConnectionOpenedCallback(ConnectionOpenedCallback callback)
	{
		m_connectionOpenedCallback = (ConnectionOpenedCallback)Delegate.Combine(m_connectionOpenedCallback, callback);
		return this;
	}

	/// <summary>
	/// Builds an <see cref="T:OceanBase.OracleDataSource" /> which is ready for use.
	/// </summary>
	public OracleDataSource Build()
	{
		MySqlConnectorLoggingConfiguration loggingConfiguration = ((m_loggerFactory == null) ? MySqlConnectorLoggingConfiguration.NullConfiguration : new MySqlConnectorLoggingConfiguration(m_loggerFactory));
		MySqlConnectorTracingOptionsBuilder mySqlConnectorTracingOptionsBuilder = new MySqlConnectorTracingOptionsBuilder();
		IEnumerable<Action<MySqlConnectorTracingOptionsBuilder>> tracingOptionsBuilderCallbacks = m_tracingOptionsBuilderCallbacks;
		foreach (Action<MySqlConnectorTracingOptionsBuilder> item in tracingOptionsBuilderCallbacks ?? Array.Empty<Action<MySqlConnectorTracingOptionsBuilder>>())
		{
			item(mySqlConnectorTracingOptionsBuilder);
		}
		MySqlConnectorTracingOptions tracingOptions = mySqlConnectorTracingOptionsBuilder.Build();
		return new OracleDataSource(ConnectionStringBuilder.ConnectionString, loggingConfiguration, tracingOptions, m_name, m_clientCertificatesCallback, m_remoteCertificateValidationCallback, m_periodicPasswordProvider, m_periodicPasswordProviderSuccessRefreshInterval, m_periodicPasswordProviderFailureRefreshInterval, ZstandardPlugin, m_connectionOpenedCallback);
	}
}
