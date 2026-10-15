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
/// <see cref="T:OceanBase.MySqlDataSourceBuilder" /> provides an API for configuring and creating a <see cref="T:OceanBase.MySqlDataSource" />,
/// from which <see cref="T:OceanBase.MySqlConnection" /> objects can be obtained.
/// </summary>
public sealed class MySqlDataSourceBuilder
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
	/// A <see cref="T:OceanBase.MySqlConnectionStringBuilder" /> that can be used to configure the connection string on this <see cref="T:OceanBase.MySqlDataSourceBuilder" />.
	/// </summary>
	public MySqlConnectionStringBuilder ConnectionStringBuilder { get; }

	internal ZstandardPlugin? ZstandardPlugin { get; set; }

	/// <summary>
	/// Initializes a new <see cref="T:OceanBase.MySqlDataSourceBuilder" /> with the specified connection string.
	/// </summary>
	/// <param name="connectionString">The optional connection string to use.</param>
	public MySqlDataSourceBuilder(string? connectionString = null)
	{
		ConnectionStringBuilder = new MySqlConnectionStringBuilder(connectionString ?? "");
	}

	/// <summary>
	/// Configures OpenTelemetry tracing options.
	/// </summary>
	/// <returns>This builder, so that method calls can be chained.</returns>
	public MySqlDataSourceBuilder ConfigureTracing(Action<MySqlConnectorTracingOptionsBuilder> configureAction)
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
	/// <param name="loggerFactory">The logger factory.</param>
	/// <returns>This builder, so that method calls can be chained.</returns>
	public MySqlDataSourceBuilder UseLoggerFactory(ILoggerFactory? loggerFactory)
	{
		m_loggerFactory = loggerFactory;
		return this;
	}

	/// <summary>
	/// Sets the name of the <see cref="T:OceanBase.MySqlDataSource" /> that will be created.
	/// </summary>
	/// <param name="name">The data source name.</param>
	/// <returns>This builder, so that method calls can be chained.</returns>
	/// <remarks>The connection pool name is used to set the <c>program_name</c> connection attribute
	/// (which is visible to some diagnostic tools) and the <c>pool.name</c> tag supplied with
	/// <a href="https://mysqlconnector.net/diagnostics/metrics/">connection pool metrics</a>.</remarks>
	public MySqlDataSourceBuilder UseName(string? name)
	{
		m_name = name;
		return this;
	}

	/// <summary>
	/// Sets the callback used to provide client certificates for connecting to a server.
	/// </summary>
	/// <param name="callback">The callback that will provide client certificates. The <see cref="T:System.Security.Cryptography.X509Certificates.X509CertificateCollection" />
	/// provided to the callback should be filled with the client certificate(s) needed to connect to the server.</param>
	/// <returns>This builder, so that method calls can be chained.</returns>
	public MySqlDataSourceBuilder UseClientCertificatesCallback(Func<X509CertificateCollection, ValueTask>? callback)
	{
		m_clientCertificatesCallback = callback;
		return this;
	}

	/// <summary>
	/// Configures a periodic password provider, which is automatically called by the data source at some regular interval. This is the
	/// recommended way to fetch a rotating access token.
	/// </summary>
	/// <param name="passwordProvider">A callback which returns the password to be used by any new MySQL connections that are made.</param>
	/// <param name="successRefreshInterval">How long to cache the password before re-invoking the callback.</param>
	/// <param name="failureRefreshInterval">How long to wait before re-invoking the callback on failure. This should
	/// typically be much shorter than <paramref name="successRefreshInterval" />.</param>
	/// <returns>This builder, so that method calls can be chained.</returns>
	public MySqlDataSourceBuilder UsePeriodicPasswordProvider(Func<MySqlProvidePasswordContext, CancellationToken, ValueTask<string>>? passwordProvider, TimeSpan successRefreshInterval, TimeSpan failureRefreshInterval)
	{
		m_periodicPasswordProvider = passwordProvider;
		m_periodicPasswordProviderSuccessRefreshInterval = successRefreshInterval;
		m_periodicPasswordProviderFailureRefreshInterval = failureRefreshInterval;
		return this;
	}

	/// <summary>
	/// Sets the callback used to verify that the server's certificate is valid.
	/// </summary>
	/// <param name="callback">The callback used to verify that the server's certificate is valid.</param>
	/// <returns>This builder, so that method calls can be chained.</returns>
	/// <remarks><see cref="P:OceanBase.MySqlConnectionStringBuilder.SslMode" /> must be set to <see cref="F:OceanBase.MySqlSslMode.Preferred" />
	/// or <see cref="F:OceanBase.MySqlSslMode.Required" /> in order for this delegate to be invoked. See the documentation for
	/// <see cref="T:System.Net.Security.RemoteCertificateValidationCallback" /> for more information on the values passed to this delegate.</remarks>
	public MySqlDataSourceBuilder UseRemoteCertificateValidationCallback(RemoteCertificateValidationCallback callback)
	{
		m_remoteCertificateValidationCallback = callback;
		return this;
	}

	/// <summary>
	/// Adds a callback that is invoked when a new <see cref="T:OceanBase.MySqlConnection" /> is opened.
	/// </summary>
	/// <param name="callback">The callback to invoke.</param>
	/// <returns>This builder, so that method calls can be chained.</returns>
	public MySqlDataSourceBuilder UseConnectionOpenedCallback(ConnectionOpenedCallback callback)
	{
		m_connectionOpenedCallback = (ConnectionOpenedCallback)Delegate.Combine(m_connectionOpenedCallback, callback);
		return this;
	}

	/// <summary>
	/// Builds a <see cref="T:OceanBase.MySqlDataSource" /> which is ready for use.
	/// </summary>
	/// <returns>A new <see cref="T:OceanBase.MySqlDataSource" /> with the settings configured through this <see cref="T:OceanBase.MySqlDataSourceBuilder" />.</returns>
	public MySqlDataSource Build()
	{
		MySqlConnectorLoggingConfiguration loggingConfiguration = ((m_loggerFactory == null) ? MySqlConnectorLoggingConfiguration.NullConfiguration : new MySqlConnectorLoggingConfiguration(m_loggerFactory));
		MySqlConnectorTracingOptionsBuilder mySqlConnectorTracingOptionsBuilder = new MySqlConnectorTracingOptionsBuilder();
		IEnumerable<Action<MySqlConnectorTracingOptionsBuilder>> tracingOptionsBuilderCallbacks = m_tracingOptionsBuilderCallbacks;
		foreach (Action<MySqlConnectorTracingOptionsBuilder> item in tracingOptionsBuilderCallbacks ?? Array.Empty<Action<MySqlConnectorTracingOptionsBuilder>>())
		{
			item(mySqlConnectorTracingOptionsBuilder);
		}
		MySqlConnectorTracingOptions tracingOptions = mySqlConnectorTracingOptionsBuilder.Build();
		return new MySqlDataSource(ConnectionStringBuilder.ConnectionString, loggingConfiguration, tracingOptions, m_name, m_clientCertificatesCallback, m_remoteCertificateValidationCallback, m_periodicPasswordProvider, m_periodicPasswordProviderSuccessRefreshInterval, m_periodicPasswordProviderFailureRefreshInterval, ZstandardPlugin, m_connectionOpenedCallback);
	}
}
