using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OceanBase;

internal abstract class OracleConnectionStringOption
{
	public static readonly OracleConnectionStringReferenceOption<string> Server;

	public static readonly OracleConnectionStringValueOption<uint> Port;

	public static readonly OracleConnectionStringReferenceOption<string> UserID;

	public static readonly OracleConnectionStringReferenceOption<string> Password;

	public static readonly OracleConnectionStringReferenceOption<string> Database;

	public static readonly OracleConnectionStringValueOption<MySqlLoadBalance> LoadBalance;

	public static readonly OracleConnectionStringValueOption<OracleConnectionProtocol> ConnectionProtocol;

	public static readonly OracleConnectionStringReferenceOption<string> PipeName;

	public static readonly OracleConnectionStringValueOption<MySqlSslMode> SslMode;

	public static readonly OracleConnectionStringReferenceOption<string> CertificateFile;

	public static readonly OracleConnectionStringReferenceOption<string> CertificatePassword;

	public static readonly OracleConnectionStringValueOption<MySqlCertificateStoreLocation> CertificateStoreLocation;

	public static readonly OracleConnectionStringReferenceOption<string> CertificateThumbprint;

	public static readonly OracleConnectionStringReferenceOption<string> SslCert;

	public static readonly OracleConnectionStringReferenceOption<string> SslKey;

	public static readonly OracleConnectionStringReferenceOption<string> SslCa;

	public static readonly OracleConnectionStringReferenceOption<string> TlsVersion;

	public static readonly OracleConnectionStringReferenceOption<string> TlsCipherSuites;

	public static readonly OracleConnectionStringValueOption<bool> Pooling;

	public static readonly OracleConnectionStringValueOption<uint> ConnectionLifeTime;

	public static readonly OracleConnectionStringValueOption<bool> ConnectionReset;

	public static readonly OracleConnectionStringValueOption<bool> DeferConnectionReset;

	public static readonly OracleConnectionStringValueOption<uint> ConnectionIdlePingTime;

	public static readonly OracleConnectionStringValueOption<uint> ConnectionIdleTimeout;

	public static readonly OracleConnectionStringValueOption<uint> MinimumPoolSize;

	public static readonly OracleConnectionStringValueOption<uint> MaximumPoolSize;

	public static readonly OracleConnectionStringValueOption<uint> DnsCheckInterval;

	public static readonly OracleConnectionStringValueOption<bool> AllowLoadLocalInfile;

	public static readonly OracleConnectionStringValueOption<bool> AllowPublicKeyRetrieval;

	public static readonly OracleConnectionStringValueOption<bool> AllowUserVariables;

	public static readonly OracleConnectionStringValueOption<bool> AllowZeroDateTime;

	public static readonly OracleConnectionStringReferenceOption<string> ApplicationName;

	public static readonly OracleConnectionStringValueOption<bool> AutoEnlist;

	public static readonly OracleConnectionStringValueOption<bool> AutoCommit;

	public static readonly OracleConnectionStringValueOption<int> CancellationTimeout;

	public static readonly OracleConnectionStringReferenceOption<string> CharacterSet;

	public static readonly OracleConnectionStringValueOption<uint> ConnectionTimeout;

	public static readonly OracleConnectionStringValueOption<bool> ConvertZeroDateTime;

	public static readonly OracleConnectionStringValueOption<MySqlDateTimeKind> DateTimeKind;

	public static readonly OracleConnectionStringValueOption<uint> DefaultCommandTimeout;

	public static readonly OracleConnectionStringValueOption<bool> ForceSynchronous;

	public static readonly OracleConnectionStringValueOption<MySqlGuidFormat> GuidFormat;

	public static readonly OracleConnectionStringValueOption<bool> IgnoreCommandTransaction;

	public static readonly OracleConnectionStringValueOption<bool> IgnorePrepare;

	public static readonly OracleConnectionStringValueOption<bool> AutoClosePreparedStatements;

	public static readonly OracleConnectionStringValueOption<bool> InteractiveSession;

	public static readonly OracleConnectionStringValueOption<uint> Keepalive;

	public static readonly OracleConnectionStringValueOption<bool> NoBackslashEscapes;

	public static readonly OracleConnectionStringValueOption<bool> OldGuids;

	public static readonly OracleConnectionStringValueOption<bool> PersistSecurityInfo;

	public static readonly OracleConnectionStringValueOption<bool> UseLobLocatorV2;

	public static readonly OracleConnectionStringValueOption<bool> UseOraclePrepareExecute;

	public static readonly OracleConnectionStringValueOption<bool> UseOracleServerParameterMetadata;

	public static readonly OracleConnectionStringValueOption<bool> UseArrayBinding;

	public static readonly OracleConnectionStringValueOption<int> PrepareExecuteFetchSize;

	public static readonly OracleConnectionStringValueOption<bool> Pipelining;

	public static readonly OracleConnectionStringValueOption<MySqlServerRedirectionMode> ServerRedirectionMode;

	public static readonly OracleConnectionStringReferenceOption<string> ServerRsaPublicKeyFile;

	public static readonly OracleConnectionStringReferenceOption<string> ServerSPN;

	public static readonly OracleConnectionStringValueOption<bool> TreatTinyAsBoolean;

	public static readonly OracleConnectionStringValueOption<bool> UseAffectedRows;

	public static readonly OracleConnectionStringValueOption<bool> UseCompression;

	public static readonly OracleConnectionStringValueOption<bool> UseXaTransactions;

	public static readonly OracleConnectionStringValueOption<bool> IgnorePrepareExecuteSequenceCheck;

	private const string c_tlsVersionsRegexPattern = "\\s*TLS( ?v?(1|1\\.?0|1\\.?1|1\\.?2|1\\.?3))?$";

	private static readonly Regex s_tlsVersionsRegex;

	private static readonly Dictionary<string, OracleConnectionStringOption> s_options;

	private readonly IReadOnlyList<string> m_keys;

	public static List<string> OptionNames { get; }

	public string Key => m_keys[0];

	public IReadOnlyList<string> Keys => m_keys;

	public static OracleConnectionStringOption? TryGetOptionForKey(string key)
	{
		if (!s_options.TryGetValue(key, out OracleConnectionStringOption value))
		{
			return null;
		}
		return value;
	}

	public static OracleConnectionStringOption GetOptionForKey(string key)
	{
		return TryGetOptionForKey(key) ?? throw new ArgumentException("Option '" + key + "' not supported.");
	}

	public abstract object GetObject(OracleConnectionStringBuilder builder);

	public abstract void SetObject(OracleConnectionStringBuilder builder, object value);

	protected OracleConnectionStringOption(IReadOnlyList<string> keys)
	{
		m_keys = keys;
	}

	private static void AddOption(Dictionary<string, OracleConnectionStringOption> options, OracleConnectionStringOption option)
	{
		foreach (string key in option.m_keys)
		{
			options.Add(key, option);
		}
		OptionNames.Add(option.m_keys[0]);
	}

	static OracleConnectionStringOption()
	{
		OptionNames = new List<string>();
		s_tlsVersionsRegex = new Regex("\\s*TLS( ?v?(1|1\\.?0|1\\.?1|1\\.?2|1\\.?3))?$", RegexOptions.IgnoreCase);
		Dictionary<string, OracleConnectionStringOption> options = new Dictionary<string, OracleConnectionStringOption>(StringComparer.OrdinalIgnoreCase);
		AddOption(options, Server = new OracleConnectionStringReferenceOption<string>(new _003C_003Ez__ReadOnlyArray<string>(new string[7] { "Server", "Host", "Data Source", "DataSource", "Address", "Addr", "Network Address" }), ""));
		AddOption(options, Port = new OracleConnectionStringValueOption<uint>(new _003C_003Ez__ReadOnlySingleElementList<string>("Port"), 3306u));
		AddOption(options, UserID = new OracleConnectionStringReferenceOption<string>(new _003C_003Ez__ReadOnlyArray<string>(new string[6] { "User ID", "UserID", "Username", "Uid", "User name", "User" }), ""));
		AddOption(options, Password = new OracleConnectionStringReferenceOption<string>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Password", "pwd" }), ""));
		AddOption(options, Database = new OracleConnectionStringReferenceOption<string>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Database", "Initial Catalog" }), ""));
		AddOption(options, LoadBalance = new OracleConnectionStringValueOption<MySqlLoadBalance>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Load Balance", "LoadBalance" }), MySqlLoadBalance.RoundRobin));
		AddOption(options, ConnectionProtocol = new OracleConnectionStringValueOption<OracleConnectionProtocol>(new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "Connection Protocol", "ConnectionProtocol", "Protocol" }), OracleConnectionProtocol.Sockets));
		AddOption(options, PipeName = new OracleConnectionStringReferenceOption<string>(new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "Pipe Name", "PipeName", "Pipe" }), "MYSQL"));
		AddOption(options, SslMode = new OracleConnectionStringValueOption<MySqlSslMode>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "SSL Mode", "SslMode" }), MySqlSslMode.None));
		AddOption(options, CertificateFile = new OracleConnectionStringReferenceOption<string>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Certificate File", "CertificateFile" }), ""));
		AddOption(options, CertificatePassword = new OracleConnectionStringReferenceOption<string>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Certificate Password", "CertificatePassword" }), ""));
		AddOption(options, CertificateStoreLocation = new OracleConnectionStringValueOption<MySqlCertificateStoreLocation>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Certificate Store Location", "CertificateStoreLocation" }), MySqlCertificateStoreLocation.None));
		AddOption(options, CertificateThumbprint = new OracleConnectionStringReferenceOption<string>(new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "Certificate Thumbprint", "CertificateThumbprint", "Certificate Thumb Print" }), ""));
		AddOption(options, SslCert = new OracleConnectionStringReferenceOption<string>(new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "SSL Cert", "SslCert", "Ssl-Cert" }), ""));
		AddOption(options, SslKey = new OracleConnectionStringReferenceOption<string>(new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "SSL Key", "SslKey", "Ssl-Key" }), ""));
		AddOption(options, SslCa = new OracleConnectionStringReferenceOption<string>(new _003C_003Ez__ReadOnlyArray<string>(new string[5] { "SSL CA", "CACertificateFile", "CA Certificate File", "SslCa", "Ssl-Ca" }), ""));
		AddOption(options, TlsVersion = new OracleConnectionStringReferenceOption<string>(new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "TLS Version", "TlsVersion", "Tls-Version" }), "", delegate(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return "";
			}
			Span<bool> span = stackalloc bool[4];
			string[] array = value.TrimStart('[', '(').TrimEnd(')', ']').Split(new char[1] { ',' });
			foreach (string text in array)
			{
				Match match = TlsVersionsRegex().Match(text);
				if (!match.Success)
				{
					throw new ArgumentException("Unrecognized TlsVersion protocol version '" + text + "'; permitted versions are: TLS 1.0, TLS 1.1, TLS 1.2, TLS 1.3.");
				}
				string value2 = match.Groups[2].Value;
				if (value2 == null || value2.Length != 0)
				{
					switch (value2)
					{
					case "1":
					case "10":
					case "1.0":
						break;
					default:
						goto IL_00da;
					}
				}
				bool flag = true;
				goto IL_00dd;
				IL_00da:
				flag = false;
				goto IL_00dd;
				IL_00dd:
				if (flag)
				{
					span[0] = true;
				}
				else if ((value2 == "11" || value2 == "1.1") ? true : false)
				{
					span[1] = true;
				}
				else if ((value2 == "12" || value2 == "1.2") ? true : false)
				{
					span[2] = true;
				}
				else if ((value2 == "13" || value2 == "1.3") ? true : false)
				{
					span[3] = true;
				}
			}
			string text2 = "";
			_ = stackalloc char[7];
			for (int i = 0; i < span.Length; i++)
			{
				if (span[i])
				{
					if (text2.Length != 0)
					{
						text2 += ", ";
					}
					text2 += FormattableString.Invariant($"TLS 1.{i}");
				}
			}
			return text2;
		}));
		AddOption(options, TlsCipherSuites = new OracleConnectionStringReferenceOption<string>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "TLS Cipher Suites", "TlsCipherSuites" }), ""));
		AddOption(options, Pooling = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlySingleElementList<string>("Pooling"), defaultValue: true));
		AddOption(options, ConnectionLifeTime = new OracleConnectionStringValueOption<uint>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Connection Lifetime", "ConnectionLifeTime" }), 0u));
		AddOption(options, ConnectionReset = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Connection Reset", "ConnectionReset" }), defaultValue: false));
		AddOption(options, DeferConnectionReset = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Defer Connection Reset", "DeferConnectionReset" }), defaultValue: true));
		AddOption(options, ConnectionIdlePingTime = new OracleConnectionStringValueOption<uint>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Connection Idle Ping Time", "ConnectionIdlePingTime" }), 0u));
		AddOption(options, ConnectionIdleTimeout = new OracleConnectionStringValueOption<uint>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Connection Idle Timeout", "ConnectionIdleTimeout" }), 180u));
		AddOption(options, MinimumPoolSize = new OracleConnectionStringValueOption<uint>(new _003C_003Ez__ReadOnlyArray<string>(new string[4] { "Minimum Pool Size", "Min Pool Size", "MinimumPoolSize", "minpoolsize" }), 0u));
		AddOption(options, MaximumPoolSize = new OracleConnectionStringValueOption<uint>(new _003C_003Ez__ReadOnlyArray<string>(new string[4] { "Maximum Pool Size", "Max Pool Size", "MaximumPoolSize", "maxpoolsize" }), 100u));
		AddOption(options, DnsCheckInterval = new OracleConnectionStringValueOption<uint>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "DNS Check Interval", "DnsCheckInterval" }), 0u));
		AddOption(options, AllowLoadLocalInfile = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Allow Load Local Infile", "AllowLoadLocalInfile" }), defaultValue: false));
		AddOption(options, AllowPublicKeyRetrieval = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Allow Public Key Retrieval", "AllowPublicKeyRetrieval" }), defaultValue: false));
		AddOption(options, AllowUserVariables = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Allow User Variables", "AllowUserVariables" }), defaultValue: false));
		AddOption(options, AllowZeroDateTime = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Allow Zero DateTime", "AllowZeroDateTime" }), defaultValue: false));
		AddOption(options, ApplicationName = new OracleConnectionStringReferenceOption<string>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Application Name", "ApplicationName" }), ""));
		AddOption(options, AutoEnlist = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Auto Enlist", "AutoEnlist" }), defaultValue: true));
		AddOption(options, AutoCommit = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Auto Commit", "AutoCommit" }), defaultValue: true));
		AddOption(options, CancellationTimeout = new OracleConnectionStringValueOption<int>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Cancellation Timeout", "CancellationTimeout" }), 2, delegate(int x)
		{
			if (x < -1)
			{
				throw new ArgumentOutOfRangeException("CancellationTimeout", "CancellationTimeout must be greater than or equal to -1");
			}
			return x;
		}));
		AddOption(options, CharacterSet = new OracleConnectionStringReferenceOption<string>(new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "Character Set", "CharSet", "CharacterSet" }), ""));
		AddOption(options, ConnectionTimeout = new OracleConnectionStringValueOption<uint>(new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "Connection Timeout", "ConnectionTimeout", "Connect Timeout" }), 15u));
		AddOption(options, ConvertZeroDateTime = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Convert Zero DateTime", "ConvertZeroDateTime" }), defaultValue: false));
		AddOption(options, DateTimeKind = new OracleConnectionStringValueOption<MySqlDateTimeKind>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "DateTime Kind", "DateTimeKind" }), MySqlDateTimeKind.Unspecified));
		AddOption(options, DefaultCommandTimeout = new OracleConnectionStringValueOption<uint>(new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "Default Command Timeout", "DefaultCommandTimeout", "Command Timeout" }), 30u));
		AddOption(options, ForceSynchronous = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Force Synchronous", "ForceSynchronous" }), defaultValue: false));
		AddOption(options, GuidFormat = new OracleConnectionStringValueOption<MySqlGuidFormat>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "GUID Format", "GuidFormat" }), MySqlGuidFormat.Default));
		AddOption(options, IgnoreCommandTransaction = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Ignore Command Transaction", "IgnoreCommandTransaction" }), defaultValue: false));
		AddOption(options, IgnorePrepare = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Ignore Prepare", "IgnorePrepare" }), defaultValue: false));
		AddOption(options, AutoClosePreparedStatements = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Auto Close Prepared Statements", "AutoClosePreparedStatements" }), defaultValue: true));
		AddOption(options, InteractiveSession = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "Interactive Session", "InteractiveSession", "Interactive" }), defaultValue: false));
		AddOption(options, Keepalive = new OracleConnectionStringValueOption<uint>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Keep Alive", "Keepalive" }), 0u));
		AddOption(options, NoBackslashEscapes = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "No Backslash Escapes", "NoBackslashEscapes" }), defaultValue: false));
		AddOption(options, OldGuids = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Old Guids", "OldGuids" }), defaultValue: false));
		AddOption(options, PersistSecurityInfo = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Persist Security Info", "PersistSecurityInfo" }), defaultValue: false));
		AddOption(options, PrepareExecuteFetchSize = new OracleConnectionStringValueOption<int>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Prepare Execute Fetch Size", "PrepareExecuteFetchSize" }), 10));
		AddOption(options, UseLobLocatorV2 = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Use LOB Locator V2", "UseLobLocatorV2" }), defaultValue: true));
		AddOption(options, UseOraclePrepareExecute = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Use Oracle Prepare Execute", "UseOraclePrepareExecute" }), defaultValue: false));
		AddOption(options, UseOracleServerParameterMetadata = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Use Oracle Server Parameter Metadata", "UseOracleServerParameterMetadata" }), defaultValue: true));
		AddOption(options, UseArrayBinding = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Use Array Binding", "UseArrayBinding" }), defaultValue: false));
		AddOption(options, Pipelining = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlySingleElementList<string>("Pipelining"), defaultValue: true));
		AddOption(options, ServerRedirectionMode = new OracleConnectionStringValueOption<MySqlServerRedirectionMode>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Server Redirection Mode", "ServerRedirectionMode" }), MySqlServerRedirectionMode.Disabled));
		AddOption(options, ServerRsaPublicKeyFile = new OracleConnectionStringReferenceOption<string>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Server RSA Public Key File", "ServerRsaPublicKeyFile" }), ""));
		AddOption(options, ServerSPN = new OracleConnectionStringReferenceOption<string>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Server SPN", "ServerSPN" }), ""));
		AddOption(options, TreatTinyAsBoolean = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Treat Tiny As Boolean", "TreatTinyAsBoolean" }), defaultValue: true));
		AddOption(options, UseAffectedRows = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Use Affected Rows", "UseAffectedRows" }), defaultValue: false));
		AddOption(options, UseCompression = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "Use Compression", "Compress", "UseCompression" }), defaultValue: false));
		AddOption(options, UseXaTransactions = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Use XA Transactions", "UseXaTransactions" }), defaultValue: true));
		AddOption(options, IgnorePrepareExecuteSequenceCheck = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Ignore Prepare Execute Sequence Check", "IgnorePrepareExecuteSequenceCheck" }), defaultValue: true));
		s_options = options;
	}

	private static Regex TlsVersionsRegex()
	{
		return s_tlsVersionsRegex;
	}
}
