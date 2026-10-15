using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace OceanBase;

/// <summary>
/// <see cref="T:OceanBase.OracleConnectionStringBuilder" /> allows you to construct a Oracle connection string by setting properties on the builder then reading the <see cref="P:System.Data.Common.DbConnectionStringBuilder.ConnectionString" /> property.
/// </summary>
/// <remarks>See <a href="https://mysqlconnector.net/connection-options/">Connection String Options</a> for more documentation on the options.</remarks>
public sealed class OracleConnectionStringBuilder : DbConnectionStringBuilder
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
			return OracleConnectionStringOption.Server.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			OracleConnectionStringOption.Server.SetValue(this, value);
		}
	}

	/// <summary>
	/// The TCP port on which MySQL Server is listening for connections.
	/// </summary>
	[Category("Connection")]
	[DefaultValue(3306u)]
	[Description("The TCP port on which MySQL Server is listening for connections.")]
	[DisplayName("Port")]
	public uint Port
	{
		get
		{
			return OracleConnectionStringOption.Port.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.Port.SetValue(this, value);
		}
	}

	/// <summary>
	/// The MySQL user ID.
	/// </summary>
	[Category("Connection")]
	[DefaultValue("")]
	[Description("The Oracle user ID.")]
	[DisplayName("User ID")]
	public string UserID
	{
		get
		{
			return OracleConnectionStringOption.UserID.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			OracleConnectionStringOption.UserID.SetValue(this, value);
		}
	}

	/// <summary>
	/// The password for the MySQL user.
	/// </summary>
	[Category("Connection")]
	[DefaultValue("")]
	[Description("The password for the Oracle user.")]
	[DisplayName("Password")]
	public string Password
	{
		get
		{
			return OracleConnectionStringOption.Password.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			OracleConnectionStringOption.Password.SetValue(this, value);
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
			return OracleConnectionStringOption.Database.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			OracleConnectionStringOption.Database.SetValue(this, value);
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
			return OracleConnectionStringOption.LoadBalance.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.LoadBalance.SetValue(this, value);
		}
	}

	/// <summary>
	/// The protocol to use to connect to the MySQL Server.
	/// </summary>
	[Category("Connection")]
	[DefaultValue(OracleConnectionProtocol.Sockets)]
	[Description("The protocol to use to connect to the MySQL Server.")]
	[DisplayName("Connection Protocol")]
	public OracleConnectionProtocol ConnectionProtocol
	{
		get
		{
			return OracleConnectionStringOption.ConnectionProtocol.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.ConnectionProtocol.SetValue(this, value);
		}
	}

	/// <summary>
	/// The name of the Windows named pipe to use to connect to the server. You must also set <see cref="P:OceanBase.OracleConnectionStringBuilder.ConnectionProtocol" /> to <see cref="F:OceanBase.MySqlConnectionProtocol.NamedPipe" /> to used named pipes.
	/// </summary>
	[Category("Connection")]
	[DefaultValue("MYSQL")]
	[Description("The name of the Windows named pipe to use to connect to the server.")]
	[DisplayName("Pipe Name")]
	public string PipeName
	{
		get
		{
			return OracleConnectionStringOption.PipeName.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			OracleConnectionStringOption.PipeName.SetValue(this, value);
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
			return OracleConnectionStringOption.SslMode.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.SslMode.SetValue(this, value);
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
			return OracleConnectionStringOption.CertificateFile.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			OracleConnectionStringOption.CertificateFile.SetValue(this, value);
		}
	}

	/// <summary>
	/// The password for the certificate specified using the <see cref="P:OceanBase.OracleConnectionStringBuilder.CertificateFile" /> option. Not required if the certificate file is not password protected.
	/// </summary>
	[Category("TLS")]
	[DefaultValue("")]
	[Description("The password for the certificate specified using the Certificate File option.")]
	[DisplayName("Certificate Password")]
	public string CertificatePassword
	{
		get
		{
			return OracleConnectionStringOption.CertificatePassword.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			OracleConnectionStringOption.CertificatePassword.SetValue(this, value);
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
			return OracleConnectionStringOption.CertificateStoreLocation.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.CertificateStoreLocation.SetValue(this, value);
		}
	}

	/// <summary>
	/// Specifies which certificate should be used from the Certificate Store specified in <see cref="P:OceanBase.OracleConnectionStringBuilder.CertificateStoreLocation" />. This option must be used to indicate which certificate in the store should be used for authentication.
	/// </summary>
	[Category("TLS")]
	[DisplayName("Certificate Thumbprint")]
	[DefaultValue("")]
	[Description("Specifies which certificate should be used from the certificate store specified in Certificate Store Location")]
	public string CertificateThumbprint
	{
		get
		{
			return OracleConnectionStringOption.CertificateThumbprint.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			OracleConnectionStringOption.CertificateThumbprint.SetValue(this, value);
		}
	}

	/// <summary>
	/// The path to the client’s SSL certificate file in PEM format. <see cref="P:OceanBase.OracleConnectionStringBuilder.SslKey" /> must also be specified, and <see cref="P:OceanBase.OracleConnectionStringBuilder.CertificateFile" /> should not be.
	/// </summary>
	[Category("TLS")]
	[DefaultValue("")]
	[Description("The path to the client’s SSL certificate file in PEM format.")]
	[DisplayName("SSL Cert")]
	public string SslCert
	{
		get
		{
			return OracleConnectionStringOption.SslCert.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			OracleConnectionStringOption.SslCert.SetValue(this, value);
		}
	}

	/// <summary>
	/// The path to the client’s SSL private key in PEM format. <see cref="P:OceanBase.OracleConnectionStringBuilder.SslCert" /> must also be specified, and <see cref="P:OceanBase.OracleConnectionStringBuilder.CertificateFile" /> should not be.
	/// </summary>
	[Category("TLS")]
	[DefaultValue("")]
	[Description("The path to the client’s SSL private key in PEM format.")]
	[DisplayName("SSL Key")]
	public string SslKey
	{
		get
		{
			return OracleConnectionStringOption.SslKey.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			OracleConnectionStringOption.SslKey.SetValue(this, value);
		}
	}

	/// <summary>
	/// Use <see cref="P:OceanBase.OracleConnectionStringBuilder.SslCa" /> instead.
	/// </summary>
	[Browsable(false)]
	[Category("Obsolete")]
	[DisplayName("CA Certificate File")]
	[Obsolete("Use SslCa instead.")]
	public string CACertificateFile
	{
		get
		{
			return OracleConnectionStringOption.SslCa.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			OracleConnectionStringOption.SslCa.SetValue(this, value);
		}
	}

	/// <summary>
	/// The path to a CA certificate file in a PEM Encoded (.pem) format. This should be used with a value for the <see cref="P:OceanBase.OracleConnectionStringBuilder.SslMode" /> property of <see cref="F:OceanBase.MySqlSslMode.VerifyCA" /> or <see cref="F:OceanBase.MySqlSslMode.VerifyFull" /> to enable verification of a CA certificate that is not trusted by the operating system’s certificate store.
	/// </summary>
	[Category("TLS")]
	[DefaultValue("")]
	[Description("The path to a CA certificate file in a PEM Encoded (.pem) format.")]
	[DisplayName("SSL CA")]
	public string SslCa
	{
		get
		{
			return OracleConnectionStringOption.SslCa.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			OracleConnectionStringOption.SslCa.SetValue(this, value);
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
			return OracleConnectionStringOption.TlsVersion.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			OracleConnectionStringOption.TlsVersion.SetValue(this, value);
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
			return OracleConnectionStringOption.TlsCipherSuites.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			OracleConnectionStringOption.TlsCipherSuites.SetValue(this, value);
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
			return OracleConnectionStringOption.Pooling.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.Pooling.SetValue(this, value);
		}
	}

	/// <summary>
	/// The maximum lifetime (in seconds) for any connection, or <c>0</c> for no lifetime limit.
	/// </summary>
	[Category("Pooling")]
	[DefaultValue(0u)]
	[Description("The maximum lifetime (in seconds) for any connection, or 0 for no lifetime limit.")]
	[DisplayName("Connection Lifetime")]
	public uint ConnectionLifeTime
	{
		get
		{
			return OracleConnectionStringOption.ConnectionLifeTime.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.ConnectionLifeTime.SetValue(this, value);
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
			return OracleConnectionStringOption.ConnectionReset.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.ConnectionReset.SetValue(this, value);
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
			return OracleConnectionStringOption.DeferConnectionReset.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.DeferConnectionReset.SetValue(this, value);
		}
	}

	/// <summary>
	/// This option is no longer supported.
	/// </summary>
	[Category("Obsolete")]
	[DefaultValue(0u)]
	[DisplayName("Connection Idle Ping Time")]
	[Obsolete("This option is no longer supported in MySqlConnector >= 1.4.0.")]
	public uint ConnectionIdlePingTime
	{
		get
		{
			return OracleConnectionStringOption.ConnectionIdlePingTime.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.ConnectionIdlePingTime.SetValue(this, value);
		}
	}

	/// <summary>
	/// The amount of time (in seconds) that a connection can remain idle in the pool.
	/// </summary>
	[Category("Pooling")]
	[DefaultValue(180u)]
	[Description("The amount of time (in seconds) that a connection can remain idle in the pool.")]
	[DisplayName("Connection Idle Timeout")]
	public uint ConnectionIdleTimeout
	{
		get
		{
			return OracleConnectionStringOption.ConnectionIdleTimeout.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.ConnectionIdleTimeout.SetValue(this, value);
		}
	}

	/// <summary>
	/// The minimum number of connections to leave in the pool if <see cref="P:OceanBase.OracleConnectionStringBuilder.ConnectionIdleTimeout" /> is reached.
	/// </summary>
	[Category("Pooling")]
	[DefaultValue(0u)]
	[Description("The minimum number of connections to leave in the pool if Connection Idle Timeout is reached.")]
	[DisplayName("Minimum Pool Size")]
	public uint MinimumPoolSize
	{
		get
		{
			return OracleConnectionStringOption.MinimumPoolSize.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.MinimumPoolSize.SetValue(this, value);
		}
	}

	/// <summary>
	/// The maximum number of connections allowed in the pool.
	/// </summary>
	[Category("Pooling")]
	[DefaultValue(100u)]
	[Description("The maximum number of connections allowed in the pool.")]
	[DisplayName("Maximum Pool Size")]
	public uint MaximumPoolSize
	{
		get
		{
			return OracleConnectionStringOption.MaximumPoolSize.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.MaximumPoolSize.SetValue(this, value);
		}
	}

	/// <summary>
	/// The number of seconds between checks for DNS changes, or 0 to disable periodic checks.
	/// </summary>
	[Category("Pooling")]
	[DefaultValue(0u)]
	[Description("The number of seconds between checks for DNS changes.")]
	[DisplayName("DNS Check Interval")]
	public uint DnsCheckInterval
	{
		get
		{
			return OracleConnectionStringOption.DnsCheckInterval.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.DnsCheckInterval.SetValue(this, value);
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
			return OracleConnectionStringOption.AllowLoadLocalInfile.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.AllowLoadLocalInfile.SetValue(this, value);
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
			return OracleConnectionStringOption.AllowPublicKeyRetrieval.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.AllowPublicKeyRetrieval.SetValue(this, value);
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
			return OracleConnectionStringOption.AllowUserVariables.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.AllowUserVariables.SetValue(this, value);
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
			return OracleConnectionStringOption.AllowZeroDateTime.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.AllowZeroDateTime.SetValue(this, value);
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
			return OracleConnectionStringOption.ApplicationName.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			OracleConnectionStringOption.ApplicationName.SetValue(this, value);
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
			return OracleConnectionStringOption.AutoEnlist.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.AutoEnlist.SetValue(this, value);
		}
	}

	/// <summary>
	/// Initial default for <see cref="P:OceanBase.OracleConnection.AutoCommit" /> before the property is set explicitly on the connection (connection string only).
	/// After <c>SET NAMES utf8mb4</c> (or equivalent charset setup), sends <c>SET autocommit=1</c> when <c>true</c> (default) or <c>SET autocommit=0</c> when <c>false</c>. Oracle-mode sessions only.
	/// </summary>
	[Category("Other")]
	[DefaultValue(true)]
	[Description("Oracle mode: set session autocommit after charset initialization (default true = SET autocommit=1).")]
	[DisplayName("Auto Commit")]
	public bool AutoCommit
	{
		get
		{
			return OracleConnectionStringOption.AutoCommit.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.AutoCommit.SetValue(this, value);
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
			return OracleConnectionStringOption.CancellationTimeout.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.CancellationTimeout.SetValue(this, value);
		}
	}

	/// <summary>
	/// The character set for the connection (e.g., <c>utf8mb4</c>, <c>gbk</c>, <c>latin1</c>).
	/// For OceanBase Oracle mode, this controls the session character set used for VARCHAR2/CHAR encoding and decoding.
	/// </summary>
	[Category("Connection")]
	[DefaultValue("")]
	[DisplayName("Character Set")]
	public string CharacterSet
	{
		get
		{
			return OracleConnectionStringOption.CharacterSet.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			OracleConnectionStringOption.CharacterSet.SetValue(this, value);
		}
	}

	/// <summary>
	/// The length of time (in seconds) to wait for a connection to the server before terminating the attempt and generating an error.
	/// The default value is 15.
	/// </summary>
	[Category("Connection")]
	[Description("The length of time (in seconds) to wait for a connection to the server before terminating the attempt and generating an error.")]
	[DefaultValue(15u)]
	[DisplayName("Connection Timeout")]
	public uint ConnectionTimeout
	{
		get
		{
			return OracleConnectionStringOption.ConnectionTimeout.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.ConnectionTimeout.SetValue(this, value);
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
			return OracleConnectionStringOption.ConvertZeroDateTime.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.ConvertZeroDateTime.SetValue(this, value);
		}
	}

	/// <summary>
	/// The <see cref="P:OceanBase.OracleConnectionStringBuilder.DateTimeKind" /> to use when deserializing <c>DATETIME</c> values.
	/// </summary>
	[Category("Other")]
	[DefaultValue(MySqlDateTimeKind.Unspecified)]
	[Description("The DateTimeKind to use when deserializing DATETIME values.")]
	[DisplayName("DateTime Kind")]
	public MySqlDateTimeKind DateTimeKind
	{
		get
		{
			return OracleConnectionStringOption.DateTimeKind.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.DateTimeKind.SetValue(this, value);
		}
	}

	/// <summary>
	/// The length of time (in seconds) each command can execute before the query is cancelled on the server, or zero to disable timeouts.
	/// </summary>
	[Category("Other")]
	[DefaultValue(30u)]
	[Description("The length of time (in seconds) each command can execute before the query is cancelled on the server, or zero to disable timeouts.")]
	[DisplayName("Default Command Timeout")]
	public uint DefaultCommandTimeout
	{
		get
		{
			return OracleConnectionStringOption.DefaultCommandTimeout.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.DefaultCommandTimeout.SetValue(this, value);
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
			return OracleConnectionStringOption.ForceSynchronous.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.ForceSynchronous.SetValue(this, value);
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
			return OracleConnectionStringOption.GuidFormat.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.GuidFormat.SetValue(this, value);
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
			return OracleConnectionStringOption.IgnoreCommandTransaction.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.IgnoreCommandTransaction.SetValue(this, value);
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
			return OracleConnectionStringOption.IgnorePrepare.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.IgnorePrepare.SetValue(this, value);
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
			return OracleConnectionStringOption.AutoClosePreparedStatements.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.AutoClosePreparedStatements.SetValue(this, value);
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
			return OracleConnectionStringOption.InteractiveSession.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.InteractiveSession.SetValue(this, value);
		}
	}

	/// <summary>
	/// TCP Keepalive idle time (in seconds), or 0 to use OS defaults.
	/// </summary>
	[Category("Connection")]
	[DefaultValue(0u)]
	[Description("TCP Keepalive idle time (in seconds), or 0 to use OS defaults.")]
	[DisplayName("Keep Alive")]
	public uint Keepalive
	{
		get
		{
			return OracleConnectionStringOption.Keepalive.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.Keepalive.SetValue(this, value);
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
			return OracleConnectionStringOption.NoBackslashEscapes.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.NoBackslashEscapes.SetValue(this, value);
		}
	}

	/// <summary>
	/// Use the <see cref="P:OceanBase.OracleConnectionStringBuilder.GuidFormat" /> property instead.
	/// </summary>
	[Category("Obsolete")]
	[DisplayName("Old Guids")]
	[DefaultValue(false)]
	public bool OldGuids
	{
		get
		{
			return OracleConnectionStringOption.OldGuids.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.OldGuids.SetValue(this, value);
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
			return OracleConnectionStringOption.PersistSecurityInfo.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.PersistSecurityInfo.SetValue(this, value);
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
			return OracleConnectionStringOption.UseLobLocatorV2.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.UseLobLocatorV2.SetValue(this, value);
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
			return OracleConnectionStringOption.UseOraclePrepareExecute.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.UseOraclePrepareExecute.SetValue(this, value);
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
			return OracleConnectionStringOption.UseOracleServerParameterMetadata.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.UseOracleServerParameterMetadata.SetValue(this, value);
		}
	}

	/// <summary>
	/// Enables OceanBase Oracle-mode ArrayBinding for COM_STMT_PREPARE_EXECUTE (0xA1).
	/// The runtime path also requires <c>UseOraclePrepareExecute=true</c> in the connection string.
	/// </summary>
	[Category("OceanBase")]
	[DisplayName("Use Array Binding")]
	[DefaultValue(false)]
	[Description("Enables OceanBase Oracle-mode ArrayBinding for PS prepare+execute (0xA1).")]
	public bool UseArrayBinding
	{
		get
		{
			return OracleConnectionStringOption.UseArrayBinding.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.UseArrayBinding.SetValue(this, value);
		}
	}

	/// <summary>
	/// Controls the number of rows fetched per round-trip for SELECT queries using the PS prepare+execute protocol (0xA1).
	/// Only takes effect in Oracle mode when <see cref="P:OceanBase.OracleConnectionStringBuilder.UseOraclePrepareExecute" /> is enabled.
	/// </summary>
	[Category("OceanBase")]
	[DisplayName("Prepare Execute Fetch Size")]
	[DefaultValue(10)]
	[Description("Number of rows fetched per COM_STMT_FETCH round-trip for SELECT via PS prepare+execute (0xA1).")]
	public int PrepareExecuteFetchSize
	{
		get
		{
			return OracleConnectionStringOption.PrepareExecuteFetchSize.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.PrepareExecuteFetchSize.SetValue(this, value);
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
			return OracleConnectionStringOption.Pipelining.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.Pipelining.SetValue(this, value);
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
			return OracleConnectionStringOption.ServerRedirectionMode.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.ServerRedirectionMode.SetValue(this, value);
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
			return OracleConnectionStringOption.ServerRsaPublicKeyFile.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			OracleConnectionStringOption.ServerRsaPublicKeyFile.SetValue(this, value);
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
			return OracleConnectionStringOption.ServerSPN.GetValue(this);
		}
		[param: AllowNull]
		set
		{
			OracleConnectionStringOption.ServerSPN.SetValue(this, value);
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
			return OracleConnectionStringOption.TreatTinyAsBoolean.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.TreatTinyAsBoolean.SetValue(this, value);
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
			return OracleConnectionStringOption.UseAffectedRows.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.UseAffectedRows.SetValue(this, value);
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
			return OracleConnectionStringOption.UseCompression.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.UseCompression.SetValue(this, value);
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
			return OracleConnectionStringOption.UseXaTransactions.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.UseXaTransactions.SetValue(this, value);
		}
	}

	/// <summary>
	/// Ignore packet sequence number validation for Oracle Prepare-Execute protocol.
	/// This is useful for OceanBase 4.x servers that send both the prepare-execute header and trailing OK
	/// with the same MySQL packet sequence number (1), which would otherwise cause "Packet received out-of-order" errors.
	/// </summary>
	[Category("Other")]
	[DefaultValue(true)]
	[Description("Ignore packet sequence number validation for Oracle Prepare-Execute protocol (for OceanBase 4.x compatibility).")]
	[DisplayName("Ignore Prepare Execute Sequence Check")]
	public bool IgnorePrepareExecuteSequenceCheck
	{
		get
		{
			return OracleConnectionStringOption.IgnorePrepareExecuteSequenceCheck.GetValue(this);
		}
		set
		{
			OracleConnectionStringOption.IgnorePrepareExecuteSequenceCheck.SetValue(this, value);
		}
	}

	/// <summary>
	/// Returns an <see cref="T:System.Collections.ICollection" /> that contains the keys in the <see cref="T:OceanBase.OracleConnectionStringBuilder" />.
	/// </summary>
	public override ICollection Keys => (from string x in base.Keys
		orderby OracleConnectionStringOption.OptionNames.IndexOf(x)
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
			return OracleConnectionStringOption.GetOptionForKey(key).GetObject(this);
		}
		[param: AllowNull]
		set
		{
			OracleConnectionStringOption optionForKey = OracleConnectionStringOption.GetOptionForKey(key);
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
	/// Initializes a new <see cref="T:OceanBase.OracleConnectionStringBuilder" />.
	/// </summary>
	public OracleConnectionStringBuilder()
	{
	}

	/// <summary>
	/// Initializes a new <see cref="T:OceanBase.OracleConnectionStringBuilder" /> with properties set from the specified connection string.
	/// </summary>
	/// <param name="connectionString">The connection string to use to set property values on this object.</param>
	public OracleConnectionStringBuilder(string connectionString)
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
		string[] array = connectionString.Split(';');
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
				OracleConnectionStringOption oracleConnectionStringOption = OracleConnectionStringOption.TryGetOptionForKey(text2);
				string text3 = ((oracleConnectionStringOption != null) ? oracleConnectionStringOption.Key : text2);
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
	/// Whether this <see cref="T:OceanBase.OracleConnectionStringBuilder" /> contains a set option with the specified name.
	/// </summary>
	/// <param name="keyword">The option name.</param>
	/// <returns><c>true</c> if an option with that name is set; otherwise, <c>false</c>.</returns>
	public override bool ContainsKey(string keyword)
	{
		OracleConnectionStringOption oracleConnectionStringOption = OracleConnectionStringOption.TryGetOptionForKey(keyword);
		if (oracleConnectionStringOption != null)
		{
			return base.ContainsKey(oracleConnectionStringOption.Key);
		}
		return false;
	}

	/// <summary>
	/// Removes the option with the specified name.
	/// </summary>
	/// <param name="keyword">The option name.</param>
	public override bool Remove(string keyword)
	{
		OracleConnectionStringOption oracleConnectionStringOption = OracleConnectionStringOption.TryGetOptionForKey(keyword);
		if (oracleConnectionStringOption != null)
		{
			return base.Remove(oracleConnectionStringOption.Key);
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
			OracleConnectionStringBuilder oracleConnectionStringBuilder = new OracleConnectionStringBuilder(connectionString);
			foreach (string key in Keys)
			{
				foreach (string key2 in OracleConnectionStringOption.Password.Keys)
				{
					if (string.Equals(key, key2, StringComparison.OrdinalIgnoreCase))
					{
						oracleConnectionStringBuilder.Remove(key);
					}
				}
			}
			m_cachedConnectionStringWithoutPassword = oracleConnectionStringBuilder.ConnectionString;
			m_cachedConnectionString = connectionString;
		}
		return m_cachedConnectionStringWithoutPassword;
	}

	/// <summary>
	/// Fills in <paramref name="propertyDescriptors" /> with information about the available properties on this object.
	/// </summary>
	/// <param name="propertyDescriptors">The collection of <see cref="T:System.ComponentModel.PropertyDescriptor" /> objects to populate.</param>
	[RequiresUnreferencedCode("PropertyDescriptor's PropertyType cannot be statically discovered.")]
	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2112:ReflectionToRequiresUnreferencedCode", Justification = "Suppressing the same warnings as suppressed in the base DbConnectionStringBuilder. See https://github.com/mysql-net/MySqlConnector/issues/1607")]
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
