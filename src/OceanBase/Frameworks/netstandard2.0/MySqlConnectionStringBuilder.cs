using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace OceanBase;

/// <summary>
/// <see cref="T:OceanBase.MySqlConnectionStringBuilder" /> allows you to construct a MySQL connection string by setting properties on the builder then reading the <see cref="P:System.Data.Common.DbConnectionStringBuilder.ConnectionString" /> property.
/// </summary>
/// <remarks>See <a href="https://mysqlconnector.net/connection-options/">Connection String Options</a> for more documentation on the options.</remarks>
public sealed class MySqlConnectionStringBuilder : DbConnectionStringBuilder
{
	private string? m_cachedConnectionString;

	private string? m_cachedConnectionStringWithoutPassword;

	/// <summary>
	/// <para>The host name or network address of the MySQL Server to which to connect. Multiple hosts can be specified in a comma-delimited list.</para>
	/// <para>On Unix-like systems, this can be a fully qualified path to a MySQL socket file, which will cause a Unix socket to be used instead of a TCP/IP socket. Only a single socket name can be specified.</para>
	/// </summary>
	[Category("Connection")]
	[DefaultValue("")]
	[Description("The host name or network address of the MySQL Server to which to connect.")]
	[DisplayName("Server")]
	public string Server
	{
		get
		{
			return MySqlConnectionStringOption.Server.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			MySqlConnectionStringOption.Server.SetValue(this, value);
		}
	}

	/// <summary>
	/// The TCP port on which MySQL Server is listening for connections.
	/// </summary>
	[Category("Connection")]
	[DefaultValue(3306L)]
	[Description("The TCP port on which MySQL Server is listening for connections.")]
	[DisplayName("Port")]
	public uint Port
	{
		get
		{
			return MySqlConnectionStringOption.Port.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.Port.SetValue(this, value);
		}
	}

	/// <summary>
	/// The MySQL user ID.
	/// </summary>
	[Category("Connection")]
	[DefaultValue("")]
	[Description("The MySQL user ID.")]
	[DisplayName("User ID")]
	public string UserID
	{
		get
		{
			return MySqlConnectionStringOption.UserID.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			MySqlConnectionStringOption.UserID.SetValue(this, value);
		}
	}

	/// <summary>
	/// The password for the MySQL user.
	/// </summary>
	[Category("Connection")]
	[DefaultValue("")]
	[Description("The password for the MySQL user.")]
	[DisplayName("Password")]
	public string Password
	{
		get
		{
			return MySqlConnectionStringOption.Password.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			MySqlConnectionStringOption.Password.SetValue(this, value);
		}
	}

	/// <summary>
	/// (Optional) The case-sensitive name of the initial database to use. This may be required if the MySQL user account only has access rights to particular databases on the server.
	/// </summary>
	[Category("Connection")]
	[DefaultValue("The case-sensitive name of the initial database to use.")]
	[Description("The case-sensitive name of the initial database to use")]
	[DisplayName("Database")]
	public string Database
	{
		get
		{
			return MySqlConnectionStringOption.Database.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			MySqlConnectionStringOption.Database.SetValue(this, value);
		}
	}

	/// <summary>
	/// Specifies how load is distributed across backend servers.
	/// </summary>
	[Category("Connection")]
	[DefaultValue(MySqlLoadBalance.RoundRobin)]
	[Description("Specifies how load is distributed across backend servers.")]
	[DisplayName("Load Balance")]
	public MySqlLoadBalance LoadBalance
	{
		get
		{
			return MySqlConnectionStringOption.LoadBalance.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.LoadBalance.SetValue(this, value);
		}
	}

	/// <summary>
	/// The protocol to use to connect to the MySQL Server.
	/// </summary>
	[Category("Connection")]
	[DefaultValue(MySqlConnectionProtocol.Sockets)]
	[Description("The protocol to use to connect to the MySQL Server.")]
	[DisplayName("Connection Protocol")]
	public MySqlConnectionProtocol ConnectionProtocol
	{
		get
		{
			return MySqlConnectionStringOption.ConnectionProtocol.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.ConnectionProtocol.SetValue(this, value);
		}
	}

	/// <summary>
	/// The name of the Windows named pipe to use to connect to the server. You must also set <see cref="P:OceanBase.MySqlConnectionStringBuilder.ConnectionProtocol" /> to <see cref="F:OceanBase.MySqlConnectionProtocol.NamedPipe" /> to used named pipes.
	/// </summary>
	[Category("Connection")]
	[DefaultValue("MYSQL")]
	[Description("The name of the Windows named pipe to use to connect to the server.")]
	[DisplayName("Pipe Name")]
	public string PipeName
	{
		get
		{
			return MySqlConnectionStringOption.PipeName.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			MySqlConnectionStringOption.PipeName.SetValue(this, value);
		}
	}

	/// <summary>
	/// Whether to use SSL/TLS when connecting to the MySQL server.
	/// </summary>
	[Category("TLS")]
	[DefaultValue(MySqlSslMode.None)]
	[Description("Whether to use SSL/TLS when connecting to the MySQL server.")]
	[DisplayName("SSL Mode")]
	public MySqlSslMode SslMode
	{
		get
		{
			return MySqlConnectionStringOption.SslMode.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.SslMode.SetValue(this, value);
		}
	}

	/// <summary>
	/// The path to a certificate file in PKCS #12 (.pfx) format containing a bundled Certificate and Private Key used for mutual authentication.
	/// </summary>
	[Category("TLS")]
	[DefaultValue("")]
	[Description("The path to a certificate file in PKCS #12 (.pfx) format containing a bundled Certificate and Private Key used for mutual authentication.")]
	[DisplayName("Certificate File")]
	public string CertificateFile
	{
		get
		{
			return MySqlConnectionStringOption.CertificateFile.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			MySqlConnectionStringOption.CertificateFile.SetValue(this, value);
		}
	}

	/// <summary>
	/// The password for the certificate specified using the <see cref="P:OceanBase.MySqlConnectionStringBuilder.CertificateFile" /> option. Not required if the certificate file is not password protected.
	/// </summary>
	[Category("TLS")]
	[DefaultValue("")]
	[Description("The password for the certificate specified using the Certificate File option.")]
	[DisplayName("Certificate Password")]
	public string CertificatePassword
	{
		get
		{
			return MySqlConnectionStringOption.CertificatePassword.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			MySqlConnectionStringOption.CertificatePassword.SetValue(this, value);
		}
	}

	/// <summary>
	/// Uses a certificate from the specified Certificate Store on the machine. The default value of <see cref="F:OceanBase.MySqlCertificateStoreLocation.None" /> means the certificate store is not used; a value of <see cref="F:OceanBase.MySqlCertificateStoreLocation.CurrentUser" /> or <see cref="F:OceanBase.MySqlCertificateStoreLocation.LocalMachine" /> uses the specified store.
	/// </summary>
	[Category("TLS")]
	[DefaultValue(MySqlCertificateStoreLocation.None)]
	[Description("Uses a certificate from the specified Certificate Store on the machine.")]
	[DisplayName("Certificate Store Location")]
	public MySqlCertificateStoreLocation CertificateStoreLocation
	{
		get
		{
			return MySqlConnectionStringOption.CertificateStoreLocation.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.CertificateStoreLocation.SetValue(this, value);
		}
	}

	/// <summary>
	/// Specifies which certificate should be used from the Certificate Store specified in <see cref="P:OceanBase.MySqlConnectionStringBuilder.CertificateStoreLocation" />. This option must be used to indicate which certificate in the store should be used for authentication.
	/// </summary>
	[Category("TLS")]
	[DisplayName("Certificate Thumbprint")]
	[DefaultValue("")]
	[Description("Specifies which certificate should be used from the certificate store specified in Certificate Store Location")]
	public string CertificateThumbprint
	{
		get
		{
			return MySqlConnectionStringOption.CertificateThumbprint.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			MySqlConnectionStringOption.CertificateThumbprint.SetValue(this, value);
		}
	}

	/// <summary>
	/// The path to the client’s SSL certificate file in PEM format. <see cref="P:OceanBase.MySqlConnectionStringBuilder.SslKey" /> must also be specified, and <see cref="P:OceanBase.MySqlConnectionStringBuilder.CertificateFile" /> should not be.
	/// </summary>
	[Category("TLS")]
	[DefaultValue("")]
	[Description("The path to the client’s SSL certificate file in PEM format.")]
	[DisplayName("SSL Cert")]
	public string SslCert
	{
		get
		{
			return MySqlConnectionStringOption.SslCert.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			MySqlConnectionStringOption.SslCert.SetValue(this, value);
		}
	}

	/// <summary>
	/// The path to the client’s SSL private key in PEM format. <see cref="P:OceanBase.MySqlConnectionStringBuilder.SslCert" /> must also be specified, and <see cref="P:OceanBase.MySqlConnectionStringBuilder.CertificateFile" /> should not be.
	/// </summary>
	[Category("TLS")]
	[DefaultValue("")]
	[Description("The path to the client’s SSL private key in PEM format.")]
	[DisplayName("SSL Key")]
	public string SslKey
	{
		get
		{
			return MySqlConnectionStringOption.SslKey.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			MySqlConnectionStringOption.SslKey.SetValue(this, value);
		}
	}

	/// <summary>
	/// Use <see cref="P:OceanBase.MySqlConnectionStringBuilder.SslCa" /> instead.
	/// </summary>
	[Browsable(false)]
	[Category("Obsolete")]
	[DisplayName("CA Certificate File")]
	[Obsolete("Use SslCa instead.")]
	public string CACertificateFile
	{
		get
		{
			return MySqlConnectionStringOption.SslCa.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			MySqlConnectionStringOption.SslCa.SetValue(this, value);
		}
	}

	/// <summary>
	/// The path to a CA certificate file in a PEM Encoded (.pem) format. This should be used with a value for the <see cref="P:OceanBase.MySqlConnectionStringBuilder.SslMode" /> property of <see cref="F:OceanBase.MySqlSslMode.VerifyCA" /> or <see cref="F:OceanBase.MySqlSslMode.VerifyFull" /> to enable verification of a CA certificate that is not trusted by the operating system’s certificate store.
	/// </summary>
	[Category("TLS")]
	[DefaultValue("")]
	[Description("The path to a CA certificate file in a PEM Encoded (.pem) format.")]
	[DisplayName("SSL CA")]
	public string SslCa
	{
		get
		{
			return MySqlConnectionStringOption.SslCa.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			MySqlConnectionStringOption.SslCa.SetValue(this, value);
		}
	}

	/// <summary>
	/// The TLS versions which may be used during TLS negotiation, or empty to use OS defaults.
	/// </summary>
	[Category("TLS")]
	[DisplayName("TLS Version")]
	[DefaultValue("")]
	[Description("The TLS versions which may be used during TLS negotiation.")]
	public string TlsVersion
	{
		get
		{
			return MySqlConnectionStringOption.TlsVersion.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			MySqlConnectionStringOption.TlsVersion.SetValue(this, value);
		}
	}

	/// <summary>
	/// The TLS cipher suites which may be used during TLS negotiation. The default value (the empty string) allows the OS to determine the TLS cipher suites to use; this is the recommended setting.
	/// </summary>
	[Category("TLS")]
	[DefaultValue("")]
	[Description("The TLS cipher suites which may be used during TLS negotiation.")]
	[DisplayName("TLS Cipher Suites")]
	public string TlsCipherSuites
	{
		get
		{
			return MySqlConnectionStringOption.TlsCipherSuites.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			MySqlConnectionStringOption.TlsCipherSuites.SetValue(this, value);
		}
	}

	/// <summary>
	/// Enables connection pooling.
	/// </summary>
	[Category("Pooling")]
	[DefaultValue(true)]
	[Description("Enables connection pooling.")]
	[DisplayName("Pooling")]
	public bool Pooling
	{
		get
		{
			return MySqlConnectionStringOption.Pooling.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.Pooling.SetValue(this, value);
		}
	}

	/// <summary>
	/// The maximum lifetime (in seconds) for any connection, or <c>0</c> for no lifetime limit.
	/// </summary>
	[Category("Pooling")]
	[DefaultValue(0L)]
	[Description("The maximum lifetime (in seconds) for any connection, or 0 for no lifetime limit.")]
	[DisplayName("Connection Lifetime")]
	public uint ConnectionLifeTime
	{
		get
		{
			return MySqlConnectionStringOption.ConnectionLifeTime.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.ConnectionLifeTime.SetValue(this, value);
		}
	}

	/// <summary>
	/// Whether connections are reset when being retrieved from the pool.
	/// </summary>
	[Category("Pooling")]
	[DefaultValue(false)]
	[Description("Whether connections are reset when being retrieved from the pool.")]
	[DisplayName("Connection Reset")]
	public bool ConnectionReset
	{
		get
		{
			return MySqlConnectionStringOption.ConnectionReset.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.ConnectionReset.SetValue(this, value);
		}
	}

	/// <summary>
	/// This option is no longer supported.
	/// </summary>
	[Category("Obsolete")]
	[DefaultValue(true)]
	[DisplayName("Defer Connection Reset")]
	[Obsolete("This option is no longer supported in MySqlConnector >= 1.4.0.")]
	public bool DeferConnectionReset
	{
		get
		{
			return MySqlConnectionStringOption.DeferConnectionReset.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.DeferConnectionReset.SetValue(this, value);
		}
	}

	/// <summary>
	/// This option is no longer supported.
	/// </summary>
	[Category("Obsolete")]
	[DefaultValue(0L)]
	[DisplayName("Connection Idle Ping Time")]
	[Obsolete("This option is no longer supported in MySqlConnector >= 1.4.0.")]
	public uint ConnectionIdlePingTime
	{
		get
		{
			return MySqlConnectionStringOption.ConnectionIdlePingTime.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.ConnectionIdlePingTime.SetValue(this, value);
		}
	}

	/// <summary>
	/// The amount of time (in seconds) that a connection can remain idle in the pool.
	/// </summary>
	[Category("Pooling")]
	[DefaultValue(180L)]
	[Description("The amount of time (in seconds) that a connection can remain idle in the pool.")]
	[DisplayName("Connection Idle Timeout")]
	public uint ConnectionIdleTimeout
	{
		get
		{
			return MySqlConnectionStringOption.ConnectionIdleTimeout.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.ConnectionIdleTimeout.SetValue(this, value);
		}
	}

	/// <summary>
	/// The minimum number of connections to leave in the pool if <see cref="P:OceanBase.MySqlConnectionStringBuilder.ConnectionIdleTimeout" /> is reached.
	/// </summary>
	[Category("Pooling")]
	[DefaultValue(0L)]
	[Description("The minimum number of connections to leave in the pool if Connection Idle Timeout is reached.")]
	[DisplayName("Minimum Pool Size")]
	public uint MinimumPoolSize
	{
		get
		{
			return MySqlConnectionStringOption.MinimumPoolSize.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.MinimumPoolSize.SetValue(this, value);
		}
	}

	/// <summary>
	/// The maximum number of connections allowed in the pool.
	/// </summary>
	[Category("Pooling")]
	[DefaultValue(100L)]
	[Description("The maximum number of connections allowed in the pool.")]
	[DisplayName("Maximum Pool Size")]
	public uint MaximumPoolSize
	{
		get
		{
			return MySqlConnectionStringOption.MaximumPoolSize.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.MaximumPoolSize.SetValue(this, value);
		}
	}

	/// <summary>
	/// The number of seconds between checks for DNS changes, or 0 to disable periodic checks.
	/// </summary>
	[Category("Pooling")]
	[DefaultValue(0L)]
	[Description("The number of seconds between checks for DNS changes.")]
	[DisplayName("DNS Check Interval")]
	public uint DnsCheckInterval
	{
		get
		{
			return MySqlConnectionStringOption.DnsCheckInterval.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.DnsCheckInterval.SetValue(this, value);
		}
	}

	/// <summary>
	/// Allows the <c>LOAD DATA LOCAL</c> command to request files from the client.
	/// </summary>
	[Category("Other")]
	[DefaultValue(false)]
	[Description("Allows the LOAD DATA LOCAL command to request files from the client.")]
	[DisplayName("Allow Load Local Infile")]
	public bool AllowLoadLocalInfile
	{
		get
		{
			return MySqlConnectionStringOption.AllowLoadLocalInfile.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.AllowLoadLocalInfile.SetValue(this, value);
		}
	}

	/// <summary>
	/// Allows the client to automatically request the RSA public key from the server.
	/// </summary>
	[Category("Other")]
	[DefaultValue(false)]
	[Description("Allows the client to automatically request the RSA public key from the server.")]
	[DisplayName("Allow Public Key Retrieval")]
	public bool AllowPublicKeyRetrieval
	{
		get
		{
			return MySqlConnectionStringOption.AllowPublicKeyRetrieval.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.AllowPublicKeyRetrieval.SetValue(this, value);
		}
	}

	/// <summary>
	/// Allows user-defined variables (prefixed with <c>@</c>) to be used in SQL statements.
	/// </summary>
	[Category("Other")]
	[DefaultValue(false)]
	[Description("Allows user-defined variables (prefixed with @) to be used in SQL statements.")]
	[DisplayName("Allow User Variables")]
	public bool AllowUserVariables
	{
		get
		{
			return MySqlConnectionStringOption.AllowUserVariables.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.AllowUserVariables.SetValue(this, value);
		}
	}

	/// <summary>
	/// Returns <c>DATETIME</c> fields as <see cref="T:OceanBase.MySqlDateTime" /> objects instead of <see cref="T:System.DateTime" /> objects.
	/// </summary>
	[Category("Other")]
	[DefaultValue(false)]
	[Description("Returns DATETIME fields as MySqlDateTime objects instead of DateTime objects.")]
	[DisplayName("Allow Zero DateTime")]
	public bool AllowZeroDateTime
	{
		get
		{
			return MySqlConnectionStringOption.AllowZeroDateTime.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.AllowZeroDateTime.SetValue(this, value);
		}
	}

	/// <summary>
	/// Sets the <c>program_name</c> connection attribute passed to MySQL Server.
	/// </summary>
	[Category("Other")]
	[DefaultValue("")]
	[Description("Sets the program_name connection attribute passed to MySQL Server.")]
	[DisplayName("Application Name")]
	public string ApplicationName
	{
		get
		{
			return MySqlConnectionStringOption.ApplicationName.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			MySqlConnectionStringOption.ApplicationName.SetValue(this, value);
		}
	}

	/// <summary>
	/// Automatically enlists this connection in any active <see cref="T:System.Transactions.TransactionScope" />.
	/// </summary>
	[Category("Other")]
	[DefaultValue(true)]
	[Description("Automatically enlists this connection in any active TransactionScope.")]
	[DisplayName("Auto Enlist")]
	public bool AutoEnlist
	{
		get
		{
			return MySqlConnectionStringOption.AutoEnlist.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.AutoEnlist.SetValue(this, value);
		}
	}

	/// <summary>
	/// For Oracle-mode connections only: after <c>SET NAMES utf8mb4</c> (or equivalent charset setup), sends <c>SET autocommit=1</c> when <c>true</c> (default) or <c>SET autocommit=0</c> when <c>false</c>. No effect on plain MySQL sessions.
	/// </summary>
	[Category("Other")]
	[DefaultValue(true)]
	[Description("Oracle mode only: set session autocommit after charset initialization (default true = SET autocommit=1).")]
	[DisplayName("Auto Commit")]
	public bool AutoCommit
	{
		get
		{
			return MySqlConnectionStringOption.AutoCommit.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.AutoCommit.SetValue(this, value);
		}
	}

	/// <summary>
	/// The length of time (in seconds) to wait for a query to be canceled when <see cref="P:OceanBase.MySqlCommand.CommandTimeout" /> expires, or zero for no timeout.
	/// </summary>
	[Category("Other")]
	[DefaultValue(2)]
	[Description("The length of time (in seconds) to wait for a query to be canceled when MySqlCommand.CommandTimeout expires, or zero for no timeout.")]
	[DisplayName("Cancellation Timeout")]
	public int CancellationTimeout
	{
		get
		{
			return MySqlConnectionStringOption.CancellationTimeout.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.CancellationTimeout.SetValue(this, value);
		}
	}

	/// <summary>
	/// Supported for backwards compatibility; MySqlConnector always uses <c>utf8mb4</c>.
	/// </summary>
	[Category("Obsolete")]
	[DefaultValue("")]
	[DisplayName("Character Set")]
	public string CharacterSet
	{
		get
		{
			return MySqlConnectionStringOption.CharacterSet.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			MySqlConnectionStringOption.CharacterSet.SetValue(this, value);
		}
	}

	/// <summary>
	/// The length of time (in seconds) to wait for a connection to the server before terminating the attempt and generating an error.
	/// The default value is 15.
	/// </summary>
	[Category("Connection")]
	[Description("The length of time (in seconds) to wait for a connection to the server before terminating the attempt and generating an error.")]
	[DefaultValue(15L)]
	[DisplayName("Connection Timeout")]
	public uint ConnectionTimeout
	{
		get
		{
			return MySqlConnectionStringOption.ConnectionTimeout.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.ConnectionTimeout.SetValue(this, value);
		}
	}

	/// <summary>
	/// Whether invalid <c>DATETIME</c> fields should be converted to <see cref="F:System.DateTime.MinValue" />.
	/// </summary>
	[Category("Other")]
	[DefaultValue(false)]
	[Description("Whether invalid DATETIME fields should be converted to DateTime.MinValue.")]
	[DisplayName("Convert Zero DateTime")]
	public bool ConvertZeroDateTime
	{
		get
		{
			return MySqlConnectionStringOption.ConvertZeroDateTime.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.ConvertZeroDateTime.SetValue(this, value);
		}
	}

	/// <summary>
	/// The <see cref="P:OceanBase.MySqlConnectionStringBuilder.DateTimeKind" /> to use when deserializing <c>DATETIME</c> values.
	/// </summary>
	[Category("Other")]
	[DefaultValue(MySqlDateTimeKind.Unspecified)]
	[Description("The DateTimeKind to use when deserializing DATETIME values.")]
	[DisplayName("DateTime Kind")]
	public MySqlDateTimeKind DateTimeKind
	{
		get
		{
			return MySqlConnectionStringOption.DateTimeKind.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.DateTimeKind.SetValue(this, value);
		}
	}

	/// <summary>
	/// The length of time (in seconds) each command can execute before the query is cancelled on the server, or zero to disable timeouts.
	/// </summary>
	[Category("Other")]
	[DefaultValue(30L)]
	[Description("The length of time (in seconds) each command can execute before the query is cancelled on the server, or zero to disable timeouts.")]
	[DisplayName("Default Command Timeout")]
	public uint DefaultCommandTimeout
	{
		get
		{
			return MySqlConnectionStringOption.DefaultCommandTimeout.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.DefaultCommandTimeout.SetValue(this, value);
		}
	}

	/// <summary>
	/// Forces all async methods to execute synchronously. This can be useful for debugging.
	/// </summary>
	[Category("Other")]
	[DefaultValue(false)]
	[Description("Forces all async methods to execute synchronously.")]
	[DisplayName("Force Synchronous")]
	public bool ForceSynchronous
	{
		get
		{
			return MySqlConnectionStringOption.ForceSynchronous.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.ForceSynchronous.SetValue(this, value);
		}
	}

	/// <summary>
	/// Determines which column type (if any) should be read as a <see cref="T:System.Guid" />.
	/// </summary>
	[Category("Other")]
	[DefaultValue(MySqlGuidFormat.Default)]
	[Description("Determines which column type (if any) should be read as a Guid.")]
	[DisplayName("GUID Format")]
	public MySqlGuidFormat GuidFormat
	{
		get
		{
			return MySqlConnectionStringOption.GuidFormat.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.GuidFormat.SetValue(this, value);
		}
	}

	/// <summary>
	/// Does not check the <see cref="P:OceanBase.MySqlCommand.Transaction" /> property for validity when executing a command.
	/// </summary>
	[Category("Other")]
	[DefaultValue(false)]
	[Description("Does not check the MySqlCommand.Transaction property for validity when executing a command.")]
	[DisplayName("Ignore Command Transaction")]
	public bool IgnoreCommandTransaction
	{
		get
		{
			return MySqlConnectionStringOption.IgnoreCommandTransaction.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.IgnoreCommandTransaction.SetValue(this, value);
		}
	}

	/// <summary>
	/// Ignores calls to <see cref="M:OceanBase.MySqlCommand.Prepare" /> and <c>PrepareAsync</c>.
	/// </summary>
	[Category("Other")]
	[DefaultValue(false)]
	[Description("Ignores calls to MySqlCommand.Prepare and PrepareAsync.")]
	[DisplayName("Ignore Prepare")]
	public bool IgnorePrepare
	{
		get
		{
			return MySqlConnectionStringOption.IgnorePrepare.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.IgnorePrepare.SetValue(this, value);
		}
	}

	/// <summary>
	/// Sends COM_STMT_CLOSE for explicitly prepared statements when their command object is disposed.
	/// </summary>
	[Category("Other")]
	[DefaultValue(true)]
	[Description("Sends COM_STMT_CLOSE for explicitly prepared statements when their command object is disposed.")]
	[DisplayName("Auto Close Prepared Statements")]
	public bool AutoClosePreparedStatements
	{
		get
		{
			return MySqlConnectionStringOption.AutoClosePreparedStatements.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.AutoClosePreparedStatements.SetValue(this, value);
		}
	}

	/// <summary>
	/// Instructs the MySQL server that this is an interactive session.
	/// </summary>
	[Category("Connection")]
	[DefaultValue(false)]
	[Description("Instructs the MySQL server that this is an interactive session.")]
	[DisplayName("Interactive Session")]
	public bool InteractiveSession
	{
		get
		{
			return MySqlConnectionStringOption.InteractiveSession.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.InteractiveSession.SetValue(this, value);
		}
	}

	/// <summary>
	/// TCP Keepalive idle time (in seconds), or 0 to use OS defaults.
	/// </summary>
	[Category("Connection")]
	[DefaultValue(0L)]
	[Description("TCP Keepalive idle time (in seconds), or 0 to use OS defaults.")]
	[DisplayName("Keep Alive")]
	public uint Keepalive
	{
		get
		{
			return MySqlConnectionStringOption.Keepalive.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.Keepalive.SetValue(this, value);
		}
	}

	/// <summary>
	/// Doesn't escape backslashes in string parameters. For use with the <c>NO_BACKSLASH_ESCAPES</c> MySQL server mode.
	/// </summary>
	[Category("Other")]
	[DefaultValue(false)]
	[Description("Doesn't escape backslashes in string parameters. For use with the NO_BACKSLASH_ESCAPES MySQL server mode.")]
	[DisplayName("No Backslash Escapes")]
	public bool NoBackslashEscapes
	{
		get
		{
			return MySqlConnectionStringOption.NoBackslashEscapes.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.NoBackslashEscapes.SetValue(this, value);
		}
	}

	/// <summary>
	/// Use the <see cref="P:OceanBase.MySqlConnectionStringBuilder.GuidFormat" /> property instead.
	/// </summary>
	[Category("Obsolete")]
	[DisplayName("Old Guids")]
	[DefaultValue(false)]
	public bool OldGuids
	{
		get
		{
			return MySqlConnectionStringOption.OldGuids.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.OldGuids.SetValue(this, value);
		}
	}

	/// <summary>
	/// If true, preserves security-sensitive information in the connection string retrieved from any open <see cref="T:OceanBase.MySqlConnection" />.
	/// </summary>
	[Category("Other")]
	[DisplayName("Persist Security Info")]
	[DefaultValue(false)]
	[Description("Preserves security-sensitive information in the connection string retrieved from any open MySqlConnection.")]
	public bool PersistSecurityInfo
	{
		get
		{
			return MySqlConnectionStringOption.PersistSecurityInfo.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.PersistSecurityInfo.SetValue(this, value);
		}
	}

	/// <summary>
	/// Enables OceanBase LOB Locator V2 functionality. When enabled, the client will advertise support for the ObCapLobLocatorV2 capability.
	/// </summary>
	[Category("OceanBase")]
	[DisplayName("Use LOB Locator V2")]
	[DefaultValue(true)]
	[Description("Enables OceanBase LOB Locator V2 functionality.")]
	public bool UseLobLocatorV2
	{
		get
		{
			return MySqlConnectionStringOption.UseLobLocatorV2.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.UseLobLocatorV2.SetValue(this, value);
		}
	}

	/// <summary>
	/// Enables OceanBase Oracle-mode PS COM_STMT_PREPARE_EXECUTE (0xA1) protocol to send prepare+execute in a single request.
	/// Only takes effect when Oracle mode is active and the server supports this protocol.
	/// </summary>
	[Category("OceanBase")]
	[DisplayName("Use Oracle Prepare Execute")]
	[DefaultValue(false)]
	[Description("Enables OceanBase Oracle-mode PS prepare+execute (0xA1) protocol for better performance.")]
	public bool UseOraclePrepareExecute
	{
		get
		{
			return MySqlConnectionStringOption.UseOraclePrepareExecute.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.UseOraclePrepareExecute.SetValue(this, value);
		}
	}

	/// <summary>
	/// Uses OceanBase Oracle-mode prepare-execute parameter metadata to update parameter directions.
	/// </summary>
	[Category("OceanBase")]
	[DisplayName("Use Oracle Server Parameter Metadata")]
	[DefaultValue(true)]
	[Description("Uses OceanBase Oracle-mode prepare-execute parameter metadata to update parameter directions.")]
	public bool UseOracleServerParameterMetadata
	{
		get
		{
			return MySqlConnectionStringOption.UseOracleServerParameterMetadata.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.UseOracleServerParameterMetadata.SetValue(this, value);
		}
	}

	/// <summary>
	/// Enables OceanBase Oracle-mode ArrayBinding for COM_STMT_PREPARE_EXECUTE (0xA1).
	/// Only takes effect in Oracle mode when <see cref="P:OceanBase.MySqlConnectionStringBuilder.UseOraclePrepareExecute" /> is enabled and <see cref="P:OceanBase.OracleCommand.ArrayBindCount" /> is greater than zero.
	/// </summary>
	[Category("OceanBase")]
	[DisplayName("Use Array Binding")]
	[DefaultValue(false)]
	[Description("Enables OceanBase Oracle-mode ArrayBinding for PS prepare+execute (0xA1).")]
	public bool UseArrayBinding
	{
		get
		{
			return MySqlConnectionStringOption.UseArrayBinding.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.UseArrayBinding.SetValue(this, value);
		}
	}

	/// <summary>
	/// Controls the number of rows fetched per round-trip for SELECT queries using the PS prepare+execute protocol (0xA1).
	/// Only takes effect in Oracle mode when <see cref="P:OceanBase.MySqlConnectionStringBuilder.UseOraclePrepareExecute" /> is enabled.
	/// </summary>
	[Category("OceanBase")]
	[DisplayName("Prepare Execute Fetch Size")]
	[DefaultValue(10)]
	[Description("Number of rows fetched per COM_STMT_FETCH round-trip for SELECT via PS prepare+execute (0xA1).")]
	public int PrepareExecuteFetchSize
	{
		get
		{
			return MySqlConnectionStringOption.PrepareExecuteFetchSize.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.PrepareExecuteFetchSize.SetValue(this, value);
		}
	}

	/// <summary>
	/// Enables query pipelining.
	/// </summary>
	[Category("Other")]
	[DefaultValue(true)]
	[Description("Enables query pipelining.")]
	[DisplayName("Pipelining")]
	public bool Pipelining
	{
		get
		{
			return MySqlConnectionStringOption.Pipelining.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.Pipelining.SetValue(this, value);
		}
	}

	/// <summary>
	/// Whether to use server redirection.
	/// </summary>
	[Category("Connection")]
	[DefaultValue(MySqlServerRedirectionMode.Disabled)]
	[Description("Whether to use server redirection.")]
	[DisplayName("Server Redirection Mode")]
	public MySqlServerRedirectionMode ServerRedirectionMode
	{
		get
		{
			return MySqlConnectionStringOption.ServerRedirectionMode.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.ServerRedirectionMode.SetValue(this, value);
		}
	}

	/// <summary>
	/// The path to a file containing the server's RSA public key.
	/// </summary>
	[Category("Connection")]
	[DisplayName("Server RSA Public Key File")]
	[DefaultValue("")]
	[Description("The path to a file containing the server's RSA public key.")]
	public string ServerRsaPublicKeyFile
	{
		get
		{
			return MySqlConnectionStringOption.ServerRsaPublicKeyFile.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			MySqlConnectionStringOption.ServerRsaPublicKeyFile.SetValue(this, value);
		}
	}

	/// <summary>
	/// The server’s Service Principal Name (for <c>auth_gssapi_client</c> authentication).
	/// </summary>
	[Category("Connection")]
	[DefaultValue("")]
	[Description("The server’s Service Principal Name (for auth_gssapi_client authentication).")]
	[DisplayName("Server SPN")]
	public string ServerSPN
	{
		get
		{
			return MySqlConnectionStringOption.ServerSPN.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			MySqlConnectionStringOption.ServerSPN.SetValue(this, value);
		}
	}

	/// <summary>
	/// Returns <c>TINYINT(1)</c> fields as <see cref="T:System.Boolean" /> values.
	/// </summary>
	[Category("Other")]
	[DisplayName("Treat Tiny As Boolean")]
	[DefaultValue(true)]
	[Description("Returns TINYINT(1) fields as Boolean values.")]
	public bool TreatTinyAsBoolean
	{
		get
		{
			return MySqlConnectionStringOption.TreatTinyAsBoolean.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.TreatTinyAsBoolean.SetValue(this, value);
		}
	}

	/// <summary>
	/// Report changed rows instead of found rows.
	/// </summary>
	[Category("Other")]
	[DefaultValue(false)]
	[Description("Report changed rows instead of found rows.")]
	[DisplayName("Use Affected Rows")]
	public bool UseAffectedRows
	{
		get
		{
			return MySqlConnectionStringOption.UseAffectedRows.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.UseAffectedRows.SetValue(this, value);
		}
	}

	/// <summary>
	/// Compress packets sent to and from the server.
	/// </summary>
	[Category("Other")]
	[DefaultValue(false)]
	[Description("Compress packets sent to and from the server.")]
	[DisplayName("Use Compression")]
	public bool UseCompression
	{
		get
		{
			return MySqlConnectionStringOption.UseCompression.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.UseCompression.SetValue(this, value);
		}
	}

	/// <summary>
	/// Use XA transactions to implement <see cref="T:System.Transactions.TransactionScope" /> distributed transactions.
	/// </summary>
	[Category("Other")]
	[DefaultValue(true)]
	[Description("Use XA transactions to implement System.Transactions distributed transactions.")]
	[DisplayName("Use XA Transactions")]
	public bool UseXaTransactions
	{
		get
		{
			return MySqlConnectionStringOption.UseXaTransactions.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.UseXaTransactions.SetValue(this, value);
		}
	}

	/// <summary>
	/// Ignore packet sequence number validation for Oracle Prepare-Execute protocol.
	/// This is useful for OceanBase 4.x servers that send both the prepare-execute header and trailing OK
	/// with the same MySQL packet sequence number (1), which would otherwise cause "Packet received out-of-order" errors.
	/// </summary>
	[Category("Other")]
	[DefaultValue(false)]
	[Description("Ignore packet sequence number validation for Oracle Prepare-Execute protocol (for OceanBase 4.x compatibility).")]
	[DisplayName("Ignore Prepare Execute Sequence Check")]
	public bool IgnorePrepareExecuteSequenceCheck
	{
		get
		{
			return MySqlConnectionStringOption.IgnorePrepareExecuteSequenceCheck.GetValue(this);
		}
		set
		{
			MySqlConnectionStringOption.IgnorePrepareExecuteSequenceCheck.SetValue(this, value);
		}
	}

	/// <summary>
	/// Returns an <see cref="T:System.Collections.ICollection" /> that contains the keys in the <see cref="T:OceanBase.MySqlConnectionStringBuilder" />.
	/// </summary>
	public override ICollection Keys => (from string x in base.Keys
		orderby MySqlConnectionStringOption.OptionNames.IndexOf(x)
		select x).ToList();

	/// <summary>
	/// Retrieves an option value by name.
	/// </summary>
	/// <param name="key">The option name.</param>
	/// <returns>That option's value, if set.</returns>
	public override object this[string key]
	{
		get
		{
			return MySqlConnectionStringOption.GetOptionForKey(key).GetObject(this);
		}
		[param: AllowNull]
		set
		{
			MySqlConnectionStringOption optionForKey = MySqlConnectionStringOption.GetOptionForKey(key);
			if (value == null)
			{
				base[optionForKey.Key] = null;
			}
			else
			{
				optionForKey.SetObject(this, value);
			}
		}
	}

	/// <summary>
	/// Initializes a new <see cref="T:OceanBase.MySqlConnectionStringBuilder" />.
	/// </summary>
	public MySqlConnectionStringBuilder()
	{
	}

	/// <summary>
	/// Initializes a new <see cref="T:OceanBase.MySqlConnectionStringBuilder" /> with properties set from the specified connection string.
	/// </summary>
	/// <param name="connectionString">The connection string to use to set property values on this object.</param>
	public MySqlConnectionStringBuilder(string connectionString)
	{
		ValidateConnectionStringForDuplicates(connectionString);
		base.ConnectionString = connectionString;
	}

	/// <summary>
	/// Validates the connection string for duplicate parameters and throws an exception if any are found.
	/// </summary>
	/// <param name="connectionString">The connection string to validate.</param>
	/// <exception cref="T:System.ArgumentException">Thrown when duplicate parameters are found.</exception>
	private static void ValidateConnectionStringForDuplicates(string? connectionString)
	{
		if (string.IsNullOrEmpty(connectionString))
		{
			return;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<string> list = new List<string>();
		string[] array = connectionString.Split(new char[1] { ';' });
		foreach (string text in array)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				continue;
			}
			int num = text.IndexOf('=');
			if (num <= 0)
			{
				continue;
			}
			string text2 = text.Substring(0, num).Trim();
			if (!string.IsNullOrEmpty(text2))
			{
				MySqlConnectionStringOption mySqlConnectionStringOption = MySqlConnectionStringOption.TryGetOptionForKey(text2);
				string text3 = ((mySqlConnectionStringOption != null) ? mySqlConnectionStringOption.Key : text2);
				if (!hashSet.Add(text3) && !list.Contains<string>(text3, StringComparer.OrdinalIgnoreCase))
				{
					list.Add(text3);
				}
			}
		}
		if (list.Count <= 0)
		{
			return;
		}
		string text4 = string.Join(", ", list);
		throw new ArgumentException("Duplicate connection string parameter(s): " + text4 + ". Each parameter may only be specified once.", "connectionString");
	}

	/// <summary>
	/// Whether this <see cref="T:OceanBase.MySqlConnectionStringBuilder" /> contains a set option with the specified name.
	/// </summary>
	/// <param name="keyword">The option name.</param>
	/// <returns><c>true</c> if an option with that name is set; otherwise, <c>false</c>.</returns>
	public override bool ContainsKey(string keyword)
	{
		MySqlConnectionStringOption mySqlConnectionStringOption = MySqlConnectionStringOption.TryGetOptionForKey(keyword);
		if (mySqlConnectionStringOption != null)
		{
			return base.ContainsKey(mySqlConnectionStringOption.Key);
		}
		return false;
	}

	/// <summary>
	/// Removes the option with the specified name.
	/// </summary>
	/// <param name="keyword">The option name.</param>
	public override bool Remove(string keyword)
	{
		MySqlConnectionStringOption mySqlConnectionStringOption = MySqlConnectionStringOption.TryGetOptionForKey(keyword);
		if (mySqlConnectionStringOption != null)
		{
			return base.Remove(mySqlConnectionStringOption.Key);
		}
		return false;
	}

	internal void DoSetValue(string key, object? value)
	{
		base[key] = value;
	}

	internal string GetConnectionString(bool includePassword)
	{
		string connectionString = base.ConnectionString;
		if (includePassword)
		{
			return connectionString;
		}
		if (m_cachedConnectionString != connectionString)
		{
			MySqlConnectionStringBuilder mySqlConnectionStringBuilder = new MySqlConnectionStringBuilder(connectionString);
			foreach (string key in Keys)
			{
				foreach (string key2 in MySqlConnectionStringOption.Password.Keys)
				{
					if (string.Equals(key, key2, StringComparison.OrdinalIgnoreCase))
					{
						mySqlConnectionStringBuilder.Remove(key);
					}
				}
			}
			m_cachedConnectionStringWithoutPassword = mySqlConnectionStringBuilder.ConnectionString;
			m_cachedConnectionString = connectionString;
		}
		return m_cachedConnectionStringWithoutPassword;
	}

	/// <summary>
	/// Fills in <paramref name="propertyDescriptors" /> with information about the available properties on this object.
	/// </summary>
	/// <param name="propertyDescriptors">The collection of <see cref="T:System.ComponentModel.PropertyDescriptor" /> objects to populate.</param>
	protected override void GetProperties(Hashtable propertyDescriptors)
	{
		base.GetProperties(propertyDescriptors);
		foreach (PropertyDescriptor item in (from PropertyDescriptor x in propertyDescriptors.Values
			where !x.Attributes.OfType<CategoryAttribute>().Any() || x.Attributes.OfType<ObsoleteAttribute>().Any()
			select x).ToList())
		{
			propertyDescriptors.Remove(item.DisplayName);
		}
	}
}
