using System;
using System.CodeDom.Compiler;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
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

	private static readonly FrozenDictionary<string, OracleConnectionStringOption> s_options;

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
		Dictionary<string, OracleConnectionStringOption> dictionary = new Dictionary<string, OracleConnectionStringOption>(StringComparer.OrdinalIgnoreCase);
		AddOption(dictionary, Server = new OracleConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[7] { "Server", "Host", "Data Source", "DataSource", "Address", "Addr", "Network Address" }), ""));
		AddOption(dictionary, Port = new OracleConnectionStringValueOption<uint>(new _003C_003Ez__ReadOnlySingleElementList<string>("Port"), 3306u));
		AddOption(dictionary, UserID = new OracleConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[6] { "User ID", "UserID", "Username", "Uid", "User name", "User" }), ""));
		AddOption(dictionary, Password = new OracleConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Password", "pwd" }), ""));
		AddOption(dictionary, Database = new OracleConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Database", "Initial Catalog" }), ""));
		AddOption(dictionary, LoadBalance = new OracleConnectionStringValueOption<MySqlLoadBalance>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Load Balance", "LoadBalance" }), MySqlLoadBalance.RoundRobin));
		AddOption(dictionary, ConnectionProtocol = new OracleConnectionStringValueOption<OracleConnectionProtocol>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "Connection Protocol", "ConnectionProtocol", "Protocol" }), OracleConnectionProtocol.Sockets));
		AddOption(dictionary, PipeName = new OracleConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "Pipe Name", "PipeName", "Pipe" }), "MYSQL"));
		AddOption(dictionary, SslMode = new OracleConnectionStringValueOption<MySqlSslMode>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "SSL Mode", "SslMode" }), MySqlSslMode.None));
		AddOption(dictionary, CertificateFile = new OracleConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Certificate File", "CertificateFile" }), ""));
		AddOption(dictionary, CertificatePassword = new OracleConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Certificate Password", "CertificatePassword" }), ""));
		AddOption(dictionary, CertificateStoreLocation = new OracleConnectionStringValueOption<MySqlCertificateStoreLocation>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Certificate Store Location", "CertificateStoreLocation" }), MySqlCertificateStoreLocation.None));
		AddOption(dictionary, CertificateThumbprint = new OracleConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "Certificate Thumbprint", "CertificateThumbprint", "Certificate Thumb Print" }), ""));
		AddOption(dictionary, SslCert = new OracleConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "SSL Cert", "SslCert", "Ssl-Cert" }), ""));
		AddOption(dictionary, SslKey = new OracleConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "SSL Key", "SslKey", "Ssl-Key" }), ""));
		AddOption(dictionary, SslCa = new OracleConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[5] { "SSL CA", "CACertificateFile", "CA Certificate File", "SslCa", "Ssl-Ca" }), ""));
		AddOption(dictionary, TlsVersion = new OracleConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "TLS Version", "TlsVersion", "Tls-Version" }), "", delegate(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return "";
			}
			Span<bool> span = stackalloc bool[4];
			string[] array = value.TrimStart(new char[2] { '[', '(' }).TrimEnd(new char[2] { ')', ']' }).Split(',');
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
						goto IL_00d4;
					}
				}
				bool flag = true;
				goto IL_00d7;
				IL_00d4:
				flag = false;
				goto IL_00d7;
				IL_00d7:
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
			Span<char> span2 = stackalloc char[7];
			for (int i = 0; i < span.Length; i++)
			{
				if (span[i])
				{
					if (text2.Length != 0)
					{
						text2 += ", ";
					}
					string text3 = text2;
					IFormatProvider invariantCulture = CultureInfo.InvariantCulture;
					Span<char> initialBuffer = span2;
					DefaultInterpolatedStringHandler handler = new DefaultInterpolatedStringHandler(6, 1, invariantCulture, initialBuffer);
					handler.AppendLiteral("TLS 1.");
					handler.AppendFormatted(i);
					text2 = text3 + string.Create(invariantCulture, initialBuffer, ref handler);
				}
			}
			return text2;
		}));
		AddOption(dictionary, TlsCipherSuites = new OracleConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "TLS Cipher Suites", "TlsCipherSuites" }), ""));
		AddOption(dictionary, Pooling = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlySingleElementList<string>("Pooling"), defaultValue: true));
		AddOption(dictionary, ConnectionLifeTime = new OracleConnectionStringValueOption<uint>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Connection Lifetime", "ConnectionLifeTime" }), 0u));
		AddOption(dictionary, ConnectionReset = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Connection Reset", "ConnectionReset" }), defaultValue: false));
		AddOption(dictionary, DeferConnectionReset = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Defer Connection Reset", "DeferConnectionReset" }), defaultValue: true));
		AddOption(dictionary, ConnectionIdlePingTime = new OracleConnectionStringValueOption<uint>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Connection Idle Ping Time", "ConnectionIdlePingTime" }), 0u));
		AddOption(dictionary, ConnectionIdleTimeout = new OracleConnectionStringValueOption<uint>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Connection Idle Timeout", "ConnectionIdleTimeout" }), 180u));
		AddOption(dictionary, MinimumPoolSize = new OracleConnectionStringValueOption<uint>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[4] { "Minimum Pool Size", "Min Pool Size", "MinimumPoolSize", "minpoolsize" }), 0u));
		AddOption(dictionary, MaximumPoolSize = new OracleConnectionStringValueOption<uint>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[4] { "Maximum Pool Size", "Max Pool Size", "MaximumPoolSize", "maxpoolsize" }), 100u));
		AddOption(dictionary, DnsCheckInterval = new OracleConnectionStringValueOption<uint>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "DNS Check Interval", "DnsCheckInterval" }), 0u));
		AddOption(dictionary, AllowLoadLocalInfile = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Allow Load Local Infile", "AllowLoadLocalInfile" }), defaultValue: false));
		AddOption(dictionary, AllowPublicKeyRetrieval = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Allow Public Key Retrieval", "AllowPublicKeyRetrieval" }), defaultValue: false));
		AddOption(dictionary, AllowUserVariables = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Allow User Variables", "AllowUserVariables" }), defaultValue: false));
		AddOption(dictionary, AllowZeroDateTime = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Allow Zero DateTime", "AllowZeroDateTime" }), defaultValue: false));
		AddOption(dictionary, ApplicationName = new OracleConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Application Name", "ApplicationName" }), ""));
		AddOption(dictionary, AutoEnlist = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Auto Enlist", "AutoEnlist" }), defaultValue: true));
		AddOption(dictionary, AutoCommit = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Auto Commit", "AutoCommit" }), defaultValue: true));
		AddOption(dictionary, CancellationTimeout = new OracleConnectionStringValueOption<int>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Cancellation Timeout", "CancellationTimeout" }), 2, delegate(int x)
		{
			if (x < -1)
			{
				throw new ArgumentOutOfRangeException("CancellationTimeout", "CancellationTimeout must be greater than or equal to -1");
			}
			return x;
		}));
		AddOption(dictionary, CharacterSet = new OracleConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "Character Set", "CharSet", "CharacterSet" }), ""));
		AddOption(dictionary, ConnectionTimeout = new OracleConnectionStringValueOption<uint>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "Connection Timeout", "ConnectionTimeout", "Connect Timeout" }), 15u));
		AddOption(dictionary, ConvertZeroDateTime = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Convert Zero DateTime", "ConvertZeroDateTime" }), defaultValue: false));
		AddOption(dictionary, DateTimeKind = new OracleConnectionStringValueOption<MySqlDateTimeKind>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "DateTime Kind", "DateTimeKind" }), MySqlDateTimeKind.Unspecified));
		AddOption(dictionary, DefaultCommandTimeout = new OracleConnectionStringValueOption<uint>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "Default Command Timeout", "DefaultCommandTimeout", "Command Timeout" }), 30u));
		AddOption(dictionary, ForceSynchronous = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Force Synchronous", "ForceSynchronous" }), defaultValue: false));
		AddOption(dictionary, GuidFormat = new OracleConnectionStringValueOption<MySqlGuidFormat>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "GUID Format", "GuidFormat" }), MySqlGuidFormat.Default));
		AddOption(dictionary, IgnoreCommandTransaction = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Ignore Command Transaction", "IgnoreCommandTransaction" }), defaultValue: false));
		AddOption(dictionary, IgnorePrepare = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Ignore Prepare", "IgnorePrepare" }), defaultValue: false));
		AddOption(dictionary, AutoClosePreparedStatements = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Auto Close Prepared Statements", "AutoClosePreparedStatements" }), defaultValue: true));
		AddOption(dictionary, InteractiveSession = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "Interactive Session", "InteractiveSession", "Interactive" }), defaultValue: false));
		AddOption(dictionary, Keepalive = new OracleConnectionStringValueOption<uint>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Keep Alive", "Keepalive" }), 0u));
		AddOption(dictionary, NoBackslashEscapes = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "No Backslash Escapes", "NoBackslashEscapes" }), defaultValue: false));
		AddOption(dictionary, OldGuids = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Old Guids", "OldGuids" }), defaultValue: false));
		AddOption(dictionary, PersistSecurityInfo = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Persist Security Info", "PersistSecurityInfo" }), defaultValue: false));
		AddOption(dictionary, PrepareExecuteFetchSize = new OracleConnectionStringValueOption<int>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Prepare Execute Fetch Size", "PrepareExecuteFetchSize" }), 10));
		AddOption(dictionary, UseLobLocatorV2 = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Use LOB Locator V2", "UseLobLocatorV2" }), defaultValue: true));
		AddOption(dictionary, UseOraclePrepareExecute = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Use Oracle Prepare Execute", "UseOraclePrepareExecute" }), defaultValue: false));
		AddOption(dictionary, UseOracleServerParameterMetadata = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Use Oracle Server Parameter Metadata", "UseOracleServerParameterMetadata" }), defaultValue: true));
		AddOption(dictionary, UseArrayBinding = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Use Array Binding", "UseArrayBinding" }), defaultValue: false));
		AddOption(dictionary, Pipelining = new OracleConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlySingleElementList<string>("Pipelining"), defaultValue: true));
		AddOption(dictionary, ServerRedirectionMode = new OracleConnectionStringValueOption<MySqlServerRedirectionMode>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Server Redirection Mode", "ServerRedirectionMode" }), MySqlServerRedirectionMode.Disabled));
		AddOption(dictionary, ServerRsaPublicKeyFile = new OracleConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Server RSA Public Key File", "ServerRsaPublicKeyFile" }), ""));
		AddOption(dictionary, ServerSPN = new OracleConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Server SPN", "ServerSPN" }), ""));
		AddOption(dictionary, TreatTinyAsBoolean = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Treat Tiny As Boolean", "TreatTinyAsBoolean" }), defaultValue: true));
		AddOption(dictionary, UseAffectedRows = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Use Affected Rows", "UseAffectedRows" }), defaultValue: false));
		AddOption(dictionary, UseCompression = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "Use Compression", "Compress", "UseCompression" }), defaultValue: false));
		AddOption(dictionary, UseXaTransactions = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Use XA Transactions", "UseXaTransactions" }), defaultValue: true));
		AddOption(dictionary, IgnorePrepareExecuteSequenceCheck = new OracleConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Ignore Prepare Execute Sequence Check", "IgnorePrepareExecuteSequenceCheck" }), defaultValue: true));
		s_options = dictionary.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
	}

	/// <remarks>
	/// Pattern:<br />
	/// <code>\\s*TLS( ?v?(1|1\\.?0|1\\.?1|1\\.?2|1\\.?3))?$</code><br />
	/// Options:<br />
	/// <code>RegexOptions.IgnoreCase</code><br />
	/// Explanation:<br />
	/// <code>
	/// ○ Match a whitespace character atomically any number of times.<br />
	/// ○ Match a character in the set [Tt].<br />
	/// ○ Match a character in the set [Ll].<br />
	/// ○ Match a character in the set [Ss].<br />
	/// ○ Optional (greedy).<br />
	///     ○ 1st capture group.<br />
	///         ○ Match ' ' atomically, optionally.<br />
	///         ○ Match a character in the set [Vv] atomically, optionally.<br />
	///         ○ 2nd capture group.<br />
	///             ○ Match '1'.<br />
	///             ○ Match with 5 alternative expressions.<br />
	///                 ○ Match an empty string.<br />
	///                 ○ Match a sequence of expressions.<br />
	///                     ○ Match '.' atomically, optionally.<br />
	///                     ○ Match '0'.<br />
	///                 ○ Match a sequence of expressions.<br />
	///                     ○ Match '.' atomically, optionally.<br />
	///                     ○ Match '1'.<br />
	///                 ○ Match a sequence of expressions.<br />
	///                     ○ Match '.' atomically, optionally.<br />
	///                     ○ Match '2'.<br />
	///                 ○ Match a sequence of expressions.<br />
	///                     ○ Match '.' atomically, optionally.<br />
	///                     ○ Match '3'.<br />
	/// ○ Match if at the end of the string or if before an ending newline.<br />
	/// </code>
	/// </remarks>
	private static Regex TlsVersionsRegex() => new Regex("\\s*TLS( ?v?(1|1\\.?0|1\\.?1|1\\.?2|1\\.?3))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
