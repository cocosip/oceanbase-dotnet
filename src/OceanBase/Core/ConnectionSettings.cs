using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using OceanBase.Utilities;

namespace OceanBase.Core;

internal sealed class ConnectionSettings
{
	private int? m_connectionTimeoutMilliseconds;

	private static readonly string[] s_localhostPipeServer = new string[1] { "." };

	/// <summary>
	/// The <see cref="T:OceanBase.MySqlConnectionStringBuilder" /> that was used to create this <see cref="T:OceanBase.Core.ConnectionSettings" />.!--
	/// This object must not be mutated.
	/// </summary>
	public MySqlConnectionStringBuilder ConnectionStringBuilder { get; }

	public string ConnectionString { get; }

	public MySqlConnectionProtocol ConnectionProtocol { get; }

	public IReadOnlyList<string>? HostNames { get; }

	public IReadOnlyDictionary<string, int>? HostPorts { get; }

	public IPAddress? IPAddress { get; }

	public MySqlLoadBalance LoadBalance { get; }

	public int Port { get; }

	public string PipeName { get; }

	public string? UnixSocket { get; }

	public string UserID { get; }

	public string Password { get; }

	public string Database { get; }

	public MySqlSslMode SslMode { get; }

	public string CertificateFile { get; }

	public string CertificatePassword { get; }

	public string CACertificateFile { get; }

	public string SslCertificateFile { get; }

	public string SslKeyFile { get; }

	public MySqlCertificateStoreLocation CertificateStoreLocation { get; }

	public string CertificateThumbprint { get; }

	public SslProtocols TlsVersions { get; }

	public IReadOnlyList<TlsCipherSuite>? TlsCipherSuites { get; }

	public bool Pooling { get; }

	public uint ConnectionLifeTime { get; }

	public bool ConnectionReset { get; }

	public int ConnectionIdleTimeout { get; }

	public int MinimumPoolSize { get; }

	public int MaximumPoolSize { get; }

	public int DnsCheckInterval { get; }

	public bool AllowLoadLocalInfile { get; }

	public bool AllowPublicKeyRetrieval { get; }

	public bool AllowUserVariables { get; }

	public bool AllowZeroDateTime { get; }

	public string? ApplicationName { get; }

	public bool AutoEnlist { get; }

	/// <summary>
	/// Initial default for <see cref="P:OceanBase.OracleConnection.AutoCommit" /> before the property is set explicitly on the connection (connection string only).
	/// After charset initialization on Oracle-mode sessions, sends <c>SET autocommit=1</c> when <c>true</c> (default) or <c>SET autocommit=0</c> when <c>false</c>.
	/// Ignored for non-Oracle (MySQL) sessions. Pooled session reset restores this connection-string default.
	/// </summary>
	public bool AutoCommit { get; }

	public int CancellationTimeout { get; }

	public int ConnectionTimeout { get; }

	public bool ConvertZeroDateTime { get; }

	public DateTimeKind DateTimeKind { get; }

	public int DefaultCommandTimeout { get; }

	public bool ForceSynchronous { get; }

	public MySqlGuidFormat GuidFormat { get; }

	public bool IgnoreCommandTransaction { get; }

	public bool IgnorePrepare { get; }

	public bool AutoClosePreparedStatements { get; }

	public bool InteractiveSession { get; }

	public uint Keepalive { get; }

	public bool NoBackslashEscapes { get; }

	public bool PersistSecurityInfo { get; }

	public bool? Pipelining { get; }

	public bool UseLobLocatorV2 { get; }

	public bool UseOraclePrepareExecute { get; }

	public bool UseOracleServerParameterMetadata { get; }

	public bool UseArrayBinding { get; }

	public int PrepareExecuteFetchSize { get; }

	/// <summary>
	/// When true, ignores packet sequence number validation for Oracle Prepare-Execute protocol.
	/// This is needed for OceanBase 4.x servers that send both the prepare-execute header and trailing OK
	/// with the same MySQL packet sequence number (1).
	/// </summary>
	public bool IgnorePrepareExecuteSequenceCheck { get; }

	public MySqlServerRedirectionMode ServerRedirectionMode { get; }

	public string ServerRsaPublicKeyFile { get; }

	public string ServerSPN { get; }

	public bool TreatTinyAsBoolean { get; }

	public bool UseAffectedRows { get; }

	public bool UseCompression { get; }

	public bool UseXaTransactions { get; }

	public byte[]? ConnectionAttributes { get; set; }

	public int ConnectionTimeoutMilliseconds
	{
		get
		{
			if (!m_connectionTimeoutMilliseconds.HasValue)
			{
				try
				{
					m_connectionTimeoutMilliseconds = checked(ConnectionTimeout * 1000);
				}
				catch (OverflowException)
				{
					m_connectionTimeoutMilliseconds = int.MaxValue;
				}
			}
			return m_connectionTimeoutMilliseconds.Value;
		}
	}

	public ConnectionSettings(MySqlConnectionStringBuilder csb)
	{
		ConnectionStringBuilder = csb;
		ConnectionString = csb.ConnectionString;
		if (csb.ConnectionProtocol == MySqlConnectionProtocol.UnixSocket || (!Utility.IsWindows() && (csb.Server.StartsWith('/') || csb.Server.StartsWith("./", StringComparison.Ordinal))))
		{
			if (csb.LoadBalance != MySqlLoadBalance.RoundRobin)
			{
				throw new NotSupportedException("LoadBalance not supported when ConnectionProtocol=UnixSocket");
			}
			if (!File.Exists(csb.Server))
			{
				throw new MySqlException("Cannot find Unix Socket at " + csb.Server);
			}
			ConnectionProtocol = MySqlConnectionProtocol.UnixSocket;
			UnixSocket = Path.GetFullPath(csb.Server);
			PipeName = "";
		}
		else if (csb.ConnectionProtocol == MySqlConnectionProtocol.Pipe)
		{
			if (csb.LoadBalance != MySqlLoadBalance.RoundRobin)
			{
				throw new NotSupportedException("LoadBalance not supported when ConnectionProtocol=NamedPipe");
			}
			ConnectionProtocol = MySqlConnectionProtocol.Pipe;
			HostNames = ((csb.Server == "." || string.Equals(csb.Server, "localhost", StringComparison.OrdinalIgnoreCase)) ? ((IReadOnlyList<string>?)s_localhostPipeServer) : ((IReadOnlyList<string>?)new string[1] { csb.Server }));
			PipeName = csb.PipeName;
		}
		else
		{
			if (csb.ConnectionProtocol == MySqlConnectionProtocol.SharedMemory)
			{
				throw new NotSupportedException("Shared Memory connections are not supported");
			}
			ConnectionProtocol = MySqlConnectionProtocol.Sockets;
			string[] array = csb.Server.Split(',');
			List<string> list = new List<string>(array.Length);
			Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			string[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				string text = array2[i].Trim();
				if (string.IsNullOrEmpty(text))
				{
					continue;
				}
				int num = text.LastIndexOf(':');
				if (num > 0 && num < text.Length - 1)
				{
					string text2 = text.Substring(0, num);
					if (int.TryParse(text.Substring(num + 1), out var result) && result > 0 && result <= 65535)
					{
						list.Add(text2);
						dictionary[text2] = result;
						continue;
					}
				}
				list.Add(text);
			}
			HostNames = list;
			HostPorts = ((dictionary.Count > 0) ? dictionary : null);
			LoadBalance = csb.LoadBalance;
			Port = (int)csb.Port;
			PipeName = "";
		}
		UserID = csb.UserID;
		Password = csb.Password;
		Database = csb.Database;
		SslMode = csb.SslMode;
		CertificateFile = csb.CertificateFile;
		CertificatePassword = csb.CertificatePassword;
		SslCertificateFile = csb.SslCert;
		SslKeyFile = csb.SslKey;
		CACertificateFile = csb.SslCa;
		CertificateStoreLocation = csb.CertificateStoreLocation;
		CertificateThumbprint = csb.CertificateThumbprint;
		if (csb.TlsVersion.Length == 0)
		{
			TlsVersions = Utility.GetDefaultSslProtocols();
		}
		else
		{
			TlsVersions = SslProtocols.None;
			for (int j = 6; j < csb.TlsVersion.Length; j += 9)
			{
				char c = csb.TlsVersion[j];
				switch (c)
				{
				case '0':
					#pragma warning disable SYSLIB0039 // 驱动需支持连接串显式指定的 TLS 1.0/1.1（兼容旧服务端）
									TlsVersions |= SslProtocols.Tls;
					#pragma warning restore SYSLIB0039
					continue;
				case '1':
					#pragma warning disable SYSLIB0039 // 同上
									TlsVersions |= SslProtocols.Tls11;
					#pragma warning restore SYSLIB0039
					continue;
				case '2':
					TlsVersions |= SslProtocols.Tls12;
					continue;
				case '3':
					TlsVersions |= SslProtocols.Tls13;
					continue;
				}
				throw new InvalidOperationException($"Unexpected character '{c}' for TLS minor version.");
			}
			if (TlsVersions == SslProtocols.None)
			{
				throw new NotSupportedException("All specified TLS versions are incompatible with this platform.");
			}
		}
		if (csb.TlsCipherSuites.Length != 0)
		{
			List<TlsCipherSuite> list2 = new List<TlsCipherSuite>();
			string[] array2 = csb.TlsCipherSuites.Split(',');
			for (int i = 0; i < array2.Length; i++)
			{
				string text3 = array2[i].Trim();
				if (Enum.TryParse<TlsCipherSuite>(text3, ignoreCase: true, out var result2))
				{
					list2.Add(result2);
					continue;
				}
				if (int.TryParse(text3, out var result3) && Enum.IsDefined(typeof(TlsCipherSuite), result3))
				{
					list2.Add((TlsCipherSuite)result3);
					continue;
				}
				if (Enum.TryParse<TlsCipherSuite>("TLS_" + text3, ignoreCase: true, out result2))
				{
					list2.Add(result2);
					continue;
				}
				throw new NotSupportedException("Unknown value '" + text3 + "' for TlsCipherSuites.");
			}
			TlsCipherSuites = list2;
		}
		Pooling = csb.Pooling;
		ConnectionLifeTime = Math.Min(csb.ConnectionLifeTime, 4294967u) * 1000;
		ConnectionReset = csb.ConnectionReset;
		ConnectionIdleTimeout = (int)csb.ConnectionIdleTimeout;
		if (csb.MinimumPoolSize > csb.MaximumPoolSize)
		{
			throw new MySqlException("MaximumPoolSize must be greater than or equal to MinimumPoolSize");
		}
		MinimumPoolSize = ToSigned(csb.MinimumPoolSize);
		MaximumPoolSize = ToSigned(csb.MaximumPoolSize);
		DnsCheckInterval = ToSigned(csb.DnsCheckInterval);
		AllowLoadLocalInfile = csb.AllowLoadLocalInfile;
		AllowPublicKeyRetrieval = csb.AllowPublicKeyRetrieval;
		AllowUserVariables = csb.AllowUserVariables;
		AllowZeroDateTime = csb.AllowZeroDateTime;
		string applicationName = csb.ApplicationName;
		ApplicationName = ((applicationName != null && applicationName.Length == 0) ? null : csb.ApplicationName);
		AutoEnlist = csb.AutoEnlist;
		AutoCommit = csb.AutoCommit;
		CancellationTimeout = csb.CancellationTimeout;
		ConnectionTimeout = ToSigned(csb.ConnectionTimeout);
		ConvertZeroDateTime = csb.ConvertZeroDateTime;
		DateTimeKind = (DateTimeKind)csb.DateTimeKind;
		DefaultCommandTimeout = ToSigned(csb.DefaultCommandTimeout);
		ForceSynchronous = csb.ForceSynchronous;
		IgnoreCommandTransaction = csb.IgnoreCommandTransaction;
		IgnorePrepare = csb.IgnorePrepare;
		AutoClosePreparedStatements = csb.AutoClosePreparedStatements;
		InteractiveSession = csb.InteractiveSession;
		GuidFormat = GetEffectiveGuidFormat(csb.GuidFormat, csb.OldGuids);
		Keepalive = csb.Keepalive;
		NoBackslashEscapes = csb.NoBackslashEscapes;
		PersistSecurityInfo = csb.PersistSecurityInfo;
		Pipelining = (csb.ContainsKey("Pipelining") ? new bool?(csb.Pipelining) : ((bool?)null));
		UseLobLocatorV2 = csb.UseLobLocatorV2;
		UseOraclePrepareExecute = csb.UseOraclePrepareExecute;
		UseOracleServerParameterMetadata = csb.UseOracleServerParameterMetadata;
		UseArrayBinding = csb.UseArrayBinding;
		PrepareExecuteFetchSize = csb.PrepareExecuteFetchSize;
		IgnorePrepareExecuteSequenceCheck = csb.IgnorePrepareExecuteSequenceCheck;
		ServerRedirectionMode = csb.ServerRedirectionMode;
		ServerRsaPublicKeyFile = csb.ServerRsaPublicKeyFile;
		ServerSPN = csb.ServerSPN;
		TreatTinyAsBoolean = csb.TreatTinyAsBoolean;
		UseAffectedRows = csb.UseAffectedRows;
		UseCompression = csb.UseCompression;
		UseXaTransactions = csb.UseXaTransactions;
		static int ToSigned(uint value)
		{
			if (value < int.MaxValue)
			{
				return (int)value;
			}
			return int.MaxValue;
		}
	}

	public ConnectionSettings(OracleConnectionStringBuilder csb)
		: this(new MySqlConnectionStringBuilder(csb.ConnectionString))
	{
		UseLobLocatorV2 = csb.UseLobLocatorV2;
		UseOraclePrepareExecute = csb.UseOraclePrepareExecute;
		UseOracleServerParameterMetadata = csb.UseOracleServerParameterMetadata;
		UseArrayBinding = csb.UseArrayBinding;
		PrepareExecuteFetchSize = csb.PrepareExecuteFetchSize;
		IgnorePrepareExecuteSequenceCheck = csb.IgnorePrepareExecuteSequenceCheck;
		AutoCommit = csb.AutoCommit;
	}

	public ConnectionSettings CloneWith(string host, int port, string userId)
	{
		return new ConnectionSettings(this, host, port, userId, null);
	}

	public ConnectionSettings CloneWith(IPAddress ipAddress)
	{
		return new ConnectionSettings(this, HostNames[0], Port, UserID, ipAddress);
	}

	private static MySqlGuidFormat GetEffectiveGuidFormat(MySqlGuidFormat guidFormat, bool oldGuids)
	{
		switch (guidFormat)
		{
		case MySqlGuidFormat.Default:
			if (!oldGuids)
			{
				return MySqlGuidFormat.Char36;
			}
			return MySqlGuidFormat.LittleEndianBinary16;
		case MySqlGuidFormat.None:
		case MySqlGuidFormat.Char36:
		case MySqlGuidFormat.Char32:
		case MySqlGuidFormat.Binary16:
		case MySqlGuidFormat.TimeSwapBinary16:
		case MySqlGuidFormat.LittleEndianBinary16:
			if (oldGuids)
			{
				throw new MySqlException("OldGuids cannot be used with GuidFormat");
			}
			return guidFormat;
		default:
			throw new MySqlException("Unknown GuidFormat");
		}
	}

	private ConnectionSettings(ConnectionSettings other, string host, int port, string userId, IPAddress? ipAddress)
	{
		ConnectionStringBuilder = new MySqlConnectionStringBuilder(other.ConnectionString);
		ConnectionStringBuilder.Port = (uint)port;
		ConnectionStringBuilder.Server = host;
		ConnectionStringBuilder.UserID = userId;
		ConnectionString = ConnectionStringBuilder.ConnectionString;
		ConnectionProtocol = MySqlConnectionProtocol.Sockets;
		HostNames = new _003C_003Ez__ReadOnlySingleElementList<string>(host);
		HostPorts = null;
		IPAddress = ipAddress;
		LoadBalance = other.LoadBalance;
		Port = port;
		PipeName = other.PipeName;
		UserID = userId;
		Password = other.Password;
		Database = other.Database;
		SslMode = other.SslMode;
		CertificateFile = other.CertificateFile;
		CertificatePassword = other.CertificatePassword;
		SslCertificateFile = other.SslCertificateFile;
		SslKeyFile = other.SslKeyFile;
		CACertificateFile = other.CACertificateFile;
		CertificateStoreLocation = other.CertificateStoreLocation;
		CertificateThumbprint = other.CertificateThumbprint;
		Pooling = other.Pooling;
		ConnectionLifeTime = other.ConnectionLifeTime;
		ConnectionReset = other.ConnectionReset;
		ConnectionIdleTimeout = other.ConnectionIdleTimeout;
		MinimumPoolSize = other.MinimumPoolSize;
		MaximumPoolSize = other.MaximumPoolSize;
		DnsCheckInterval = other.DnsCheckInterval;
		AllowLoadLocalInfile = other.AllowLoadLocalInfile;
		AllowPublicKeyRetrieval = other.AllowPublicKeyRetrieval;
		AllowUserVariables = other.AllowUserVariables;
		AllowZeroDateTime = other.AllowZeroDateTime;
		ApplicationName = other.ApplicationName;
		AutoEnlist = other.AutoEnlist;
		AutoCommit = other.AutoCommit;
		ConnectionTimeout = other.ConnectionTimeout;
		ConvertZeroDateTime = other.ConvertZeroDateTime;
		DateTimeKind = other.DateTimeKind;
		DefaultCommandTimeout = other.DefaultCommandTimeout;
		ForceSynchronous = other.ForceSynchronous;
		IgnoreCommandTransaction = other.IgnoreCommandTransaction;
		IgnorePrepare = other.IgnorePrepare;
		AutoClosePreparedStatements = other.AutoClosePreparedStatements;
		InteractiveSession = other.InteractiveSession;
		GuidFormat = other.GuidFormat;
		Keepalive = other.Keepalive;
		NoBackslashEscapes = other.NoBackslashEscapes;
		PersistSecurityInfo = other.PersistSecurityInfo;
		Pipelining = other.Pipelining;
		UseLobLocatorV2 = other.UseLobLocatorV2;
		UseOraclePrepareExecute = other.UseOraclePrepareExecute;
		UseOracleServerParameterMetadata = other.UseOracleServerParameterMetadata;
		UseArrayBinding = other.UseArrayBinding;
		PrepareExecuteFetchSize = other.PrepareExecuteFetchSize;
		IgnorePrepareExecuteSequenceCheck = other.IgnorePrepareExecuteSequenceCheck;
		ServerRedirectionMode = other.ServerRedirectionMode;
		ServerRsaPublicKeyFile = other.ServerRsaPublicKeyFile;
		ServerSPN = other.ServerSPN;
		TreatTinyAsBoolean = other.TreatTinyAsBoolean;
		UseAffectedRows = other.UseAffectedRows;
		UseCompression = other.UseCompression;
		UseXaTransactions = other.UseXaTransactions;
	}
}
