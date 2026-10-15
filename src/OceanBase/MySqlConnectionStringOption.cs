using System;
using System.CodeDom.Compiler;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace OceanBase;

internal abstract class MySqlConnectionStringOption
{
	public static readonly MySqlConnectionStringReferenceOption<string> Server;

	public static readonly MySqlConnectionStringValueOption<uint> Port;

	public static readonly MySqlConnectionStringReferenceOption<string> UserID;

	public static readonly MySqlConnectionStringReferenceOption<string> Password;

	public static readonly MySqlConnectionStringReferenceOption<string> Database;

	public static readonly MySqlConnectionStringValueOption<MySqlLoadBalance> LoadBalance;

	public static readonly MySqlConnectionStringValueOption<MySqlConnectionProtocol> ConnectionProtocol;

	public static readonly MySqlConnectionStringReferenceOption<string> PipeName;

	public static readonly MySqlConnectionStringValueOption<MySqlSslMode> SslMode;

	public static readonly MySqlConnectionStringReferenceOption<string> CertificateFile;

	public static readonly MySqlConnectionStringReferenceOption<string> CertificatePassword;

	public static readonly MySqlConnectionStringValueOption<MySqlCertificateStoreLocation> CertificateStoreLocation;

	public static readonly MySqlConnectionStringReferenceOption<string> CertificateThumbprint;

	public static readonly MySqlConnectionStringReferenceOption<string> SslCert;

	public static readonly MySqlConnectionStringReferenceOption<string> SslKey;

	public static readonly MySqlConnectionStringReferenceOption<string> SslCa;

	public static readonly MySqlConnectionStringReferenceOption<string> TlsVersion;

	public static readonly MySqlConnectionStringReferenceOption<string> TlsCipherSuites;

	public static readonly MySqlConnectionStringValueOption<bool> Pooling;

	public static readonly MySqlConnectionStringValueOption<uint> ConnectionLifeTime;

	public static readonly MySqlConnectionStringValueOption<bool> ConnectionReset;

	public static readonly MySqlConnectionStringValueOption<bool> DeferConnectionReset;

	public static readonly MySqlConnectionStringValueOption<uint> ConnectionIdlePingTime;

	public static readonly MySqlConnectionStringValueOption<uint> ConnectionIdleTimeout;

	public static readonly MySqlConnectionStringValueOption<uint> MinimumPoolSize;

	public static readonly MySqlConnectionStringValueOption<uint> MaximumPoolSize;

	public static readonly MySqlConnectionStringValueOption<uint> DnsCheckInterval;

	public static readonly MySqlConnectionStringValueOption<bool> AllowLoadLocalInfile;

	public static readonly MySqlConnectionStringValueOption<bool> AllowPublicKeyRetrieval;

	public static readonly MySqlConnectionStringValueOption<bool> AllowUserVariables;

	public static readonly MySqlConnectionStringValueOption<bool> AllowZeroDateTime;

	public static readonly MySqlConnectionStringReferenceOption<string> ApplicationName;

	public static readonly MySqlConnectionStringValueOption<bool> AutoEnlist;

	public static readonly MySqlConnectionStringValueOption<bool> AutoCommit;

	public static readonly MySqlConnectionStringValueOption<int> CancellationTimeout;

	public static readonly MySqlConnectionStringReferenceOption<string> CharacterSet;

	public static readonly MySqlConnectionStringValueOption<uint> ConnectionTimeout;

	public static readonly MySqlConnectionStringValueOption<bool> ConvertZeroDateTime;

	public static readonly MySqlConnectionStringValueOption<MySqlDateTimeKind> DateTimeKind;

	public static readonly MySqlConnectionStringValueOption<uint> DefaultCommandTimeout;

	public static readonly MySqlConnectionStringValueOption<bool> ForceSynchronous;

	public static readonly MySqlConnectionStringValueOption<MySqlGuidFormat> GuidFormat;

	public static readonly MySqlConnectionStringValueOption<bool> IgnoreCommandTransaction;

	public static readonly MySqlConnectionStringValueOption<bool> IgnorePrepare;

	public static readonly MySqlConnectionStringValueOption<bool> IgnorePrepareExecuteSequenceCheck;

	public static readonly MySqlConnectionStringValueOption<bool> AutoClosePreparedStatements;

	public static readonly MySqlConnectionStringValueOption<bool> InteractiveSession;

	public static readonly MySqlConnectionStringValueOption<uint> Keepalive;

	public static readonly MySqlConnectionStringValueOption<bool> NoBackslashEscapes;

	public static readonly MySqlConnectionStringValueOption<bool> OldGuids;

	public static readonly MySqlConnectionStringValueOption<bool> PersistSecurityInfo;

	public static readonly MySqlConnectionStringValueOption<bool> UseLobLocatorV2;

	public static readonly MySqlConnectionStringValueOption<bool> UseOraclePrepareExecute;

	public static readonly MySqlConnectionStringValueOption<bool> UseOracleServerParameterMetadata;

	public static readonly MySqlConnectionStringValueOption<bool> UseArrayBinding;

	public static readonly MySqlConnectionStringValueOption<int> PrepareExecuteFetchSize;

	public static readonly MySqlConnectionStringValueOption<bool> Pipelining;

	public static readonly MySqlConnectionStringValueOption<MySqlServerRedirectionMode> ServerRedirectionMode;

	public static readonly MySqlConnectionStringReferenceOption<string> ServerRsaPublicKeyFile;

	public static readonly MySqlConnectionStringReferenceOption<string> ServerSPN;

	public static readonly MySqlConnectionStringValueOption<bool> TreatTinyAsBoolean;

	public static readonly MySqlConnectionStringValueOption<bool> UseAffectedRows;

	public static readonly MySqlConnectionStringValueOption<bool> UseCompression;

	public static readonly MySqlConnectionStringValueOption<bool> UseXaTransactions;

	private const string c_tlsVersionsRegexPattern = "\\s*TLS( ?v?(1|1\\.?0|1\\.?1|1\\.?2|1\\.?3))?$";

	private static readonly FrozenDictionary<string, MySqlConnectionStringOption> s_options;

	private readonly IReadOnlyList<string> m_keys;

	public static List<string> OptionNames { get; }

	public string Key => m_keys[0];

	public IReadOnlyList<string> Keys => m_keys;

	public static MySqlConnectionStringOption? TryGetOptionForKey(string key)
	{
		if (!s_options.TryGetValue(key, out MySqlConnectionStringOption value))
		{
			return null;
		}
		return value;
	}

	public static MySqlConnectionStringOption GetOptionForKey(string key)
	{
		return TryGetOptionForKey(key) ?? throw new ArgumentException("Option '" + key + "' not supported.");
	}

	public abstract object GetObject(MySqlConnectionStringBuilder builder);

	public abstract void SetObject(MySqlConnectionStringBuilder builder, object value);

	protected MySqlConnectionStringOption(IReadOnlyList<string> keys)
	{
		m_keys = keys;
	}

	private static void AddOption(Dictionary<string, MySqlConnectionStringOption> options, MySqlConnectionStringOption option)
	{
		foreach (string key in option.m_keys)
		{
			options.Add(key, option);
		}
		OptionNames.Add(option.m_keys[0]);
	}

	static MySqlConnectionStringOption()
	{
		OptionNames = new List<string>();
		Dictionary<string, MySqlConnectionStringOption> dictionary = new Dictionary<string, MySqlConnectionStringOption>(StringComparer.OrdinalIgnoreCase);
		AddOption(dictionary, Server = new MySqlConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[7] { "Server", "Host", "Data Source", "DataSource", "Address", "Addr", "Network Address" }), ""));
		AddOption(dictionary, Port = new MySqlConnectionStringValueOption<uint>(new _003C_003Ez__ReadOnlySingleElementList<string>("Port"), 3306u));
		AddOption(dictionary, UserID = new MySqlConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[6] { "User ID", "UserID", "Username", "Uid", "User name", "User" }), ""));
		AddOption(dictionary, Password = new MySqlConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Password", "pwd" }), ""));
		AddOption(dictionary, Database = new MySqlConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Database", "Initial Catalog" }), ""));
		AddOption(dictionary, LoadBalance = new MySqlConnectionStringValueOption<MySqlLoadBalance>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Load Balance", "LoadBalance" }), MySqlLoadBalance.RoundRobin));
		AddOption(dictionary, ConnectionProtocol = new MySqlConnectionStringValueOption<MySqlConnectionProtocol>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "Connection Protocol", "ConnectionProtocol", "Protocol" }), MySqlConnectionProtocol.Sockets));
		AddOption(dictionary, PipeName = new MySqlConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "Pipe Name", "PipeName", "Pipe" }), "MYSQL"));
		AddOption(dictionary, SslMode = new MySqlConnectionStringValueOption<MySqlSslMode>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "SSL Mode", "SslMode" }), MySqlSslMode.None));
		AddOption(dictionary, CertificateFile = new MySqlConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Certificate File", "CertificateFile" }), ""));
		AddOption(dictionary, CertificatePassword = new MySqlConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Certificate Password", "CertificatePassword" }), ""));
		AddOption(dictionary, CertificateStoreLocation = new MySqlConnectionStringValueOption<MySqlCertificateStoreLocation>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Certificate Store Location", "CertificateStoreLocation" }), MySqlCertificateStoreLocation.None));
		AddOption(dictionary, CertificateThumbprint = new MySqlConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "Certificate Thumbprint", "CertificateThumbprint", "Certificate Thumb Print" }), ""));
		AddOption(dictionary, SslCert = new MySqlConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "SSL Cert", "SslCert", "Ssl-Cert" }), ""));
		AddOption(dictionary, SslKey = new MySqlConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "SSL Key", "SslKey", "Ssl-Key" }), ""));
		AddOption(dictionary, SslCa = new MySqlConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[5] { "SSL CA", "CACertificateFile", "CA Certificate File", "SslCa", "Ssl-Ca" }), ""));
		AddOption(dictionary, TlsVersion = new MySqlConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "TLS Version", "TlsVersion", "Tls-Version" }), "", delegate(string? value)
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
		AddOption(dictionary, TlsCipherSuites = new MySqlConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "TLS Cipher Suites", "TlsCipherSuites" }), ""));
		AddOption(dictionary, Pooling = new MySqlConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlySingleElementList<string>("Pooling"), defaultValue: true));
		AddOption(dictionary, ConnectionLifeTime = new MySqlConnectionStringValueOption<uint>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Connection Lifetime", "ConnectionLifeTime" }), 0u));
		AddOption(dictionary, ConnectionReset = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Connection Reset", "ConnectionReset" }), defaultValue: false));
		AddOption(dictionary, DeferConnectionReset = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Defer Connection Reset", "DeferConnectionReset" }), defaultValue: true));
		AddOption(dictionary, ConnectionIdlePingTime = new MySqlConnectionStringValueOption<uint>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Connection Idle Ping Time", "ConnectionIdlePingTime" }), 0u));
		AddOption(dictionary, ConnectionIdleTimeout = new MySqlConnectionStringValueOption<uint>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Connection Idle Timeout", "ConnectionIdleTimeout" }), 180u));
		AddOption(dictionary, MinimumPoolSize = new MySqlConnectionStringValueOption<uint>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[4] { "Minimum Pool Size", "Min Pool Size", "MinimumPoolSize", "minpoolsize" }), 0u));
		AddOption(dictionary, MaximumPoolSize = new MySqlConnectionStringValueOption<uint>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[4] { "Maximum Pool Size", "Max Pool Size", "MaximumPoolSize", "maxpoolsize" }), 100u));
		AddOption(dictionary, DnsCheckInterval = new MySqlConnectionStringValueOption<uint>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "DNS Check Interval", "DnsCheckInterval" }), 0u));
		AddOption(dictionary, AllowLoadLocalInfile = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Allow Load Local Infile", "AllowLoadLocalInfile" }), defaultValue: false));
		AddOption(dictionary, AllowPublicKeyRetrieval = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Allow Public Key Retrieval", "AllowPublicKeyRetrieval" }), defaultValue: false));
		AddOption(dictionary, AllowUserVariables = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Allow User Variables", "AllowUserVariables" }), defaultValue: false));
		AddOption(dictionary, AllowZeroDateTime = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Allow Zero DateTime", "AllowZeroDateTime" }), defaultValue: false));
		AddOption(dictionary, ApplicationName = new MySqlConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Application Name", "ApplicationName" }), ""));
		AddOption(dictionary, AutoEnlist = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Auto Enlist", "AutoEnlist" }), defaultValue: true));
		AddOption(dictionary, AutoCommit = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Auto Commit", "AutoCommit" }), defaultValue: true));
		AddOption(dictionary, CancellationTimeout = new MySqlConnectionStringValueOption<int>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Cancellation Timeout", "CancellationTimeout" }), 2, delegate(int x)
		{
			if (x < -1)
			{
				throw new ArgumentOutOfRangeException("CancellationTimeout", "CancellationTimeout must be greater than or equal to -1");
			}
			return x;
		}));
		AddOption(dictionary, CharacterSet = new MySqlConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "Character Set", "CharSet", "CharacterSet" }), ""));
		AddOption(dictionary, ConnectionTimeout = new MySqlConnectionStringValueOption<uint>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "Connection Timeout", "ConnectionTimeout", "Connect Timeout" }), 15u));
		AddOption(dictionary, ConvertZeroDateTime = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Convert Zero DateTime", "ConvertZeroDateTime" }), defaultValue: false));
		AddOption(dictionary, DateTimeKind = new MySqlConnectionStringValueOption<MySqlDateTimeKind>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "DateTime Kind", "DateTimeKind" }), MySqlDateTimeKind.Unspecified));
		AddOption(dictionary, DefaultCommandTimeout = new MySqlConnectionStringValueOption<uint>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "Default Command Timeout", "DefaultCommandTimeout", "Command Timeout" }), 30u));
		AddOption(dictionary, ForceSynchronous = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Force Synchronous", "ForceSynchronous" }), defaultValue: false));
		AddOption(dictionary, GuidFormat = new MySqlConnectionStringValueOption<MySqlGuidFormat>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "GUID Format", "GuidFormat" }), MySqlGuidFormat.Default));
		AddOption(dictionary, IgnoreCommandTransaction = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Ignore Command Transaction", "IgnoreCommandTransaction" }), defaultValue: false));
		AddOption(dictionary, IgnorePrepare = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Ignore Prepare", "IgnorePrepare" }), defaultValue: false));
		AddOption(dictionary, IgnorePrepareExecuteSequenceCheck = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Ignore Prepare Execute Sequence Check", "IgnorePrepareExecuteSequenceCheck" }), defaultValue: false));
		AddOption(dictionary, AutoClosePreparedStatements = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Auto Close Prepared Statements", "AutoClosePreparedStatements" }), defaultValue: true));
		AddOption(dictionary, InteractiveSession = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "Interactive Session", "InteractiveSession", "Interactive" }), defaultValue: false));
		AddOption(dictionary, Keepalive = new MySqlConnectionStringValueOption<uint>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Keep Alive", "Keepalive" }), 0u));
		AddOption(dictionary, NoBackslashEscapes = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "No Backslash Escapes", "NoBackslashEscapes" }), defaultValue: false));
		AddOption(dictionary, OldGuids = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Old Guids", "OldGuids" }), defaultValue: false));
		AddOption(dictionary, PersistSecurityInfo = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Persist Security Info", "PersistSecurityInfo" }), defaultValue: false));
		AddOption(dictionary, UseLobLocatorV2 = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Use LOB Locator V2", "UseLobLocatorV2" }), defaultValue: true));
		AddOption(dictionary, UseOraclePrepareExecute = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Use Oracle Prepare Execute", "UseOraclePrepareExecute" }), defaultValue: false));
		AddOption(dictionary, UseOracleServerParameterMetadata = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Use Oracle Server Parameter Metadata", "UseOracleServerParameterMetadata" }), defaultValue: true));
		AddOption(dictionary, UseArrayBinding = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Use Array Binding", "UseArrayBinding" }), defaultValue: false));
		AddOption(dictionary, PrepareExecuteFetchSize = new MySqlConnectionStringValueOption<int>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Prepare Execute Fetch Size", "PrepareExecuteFetchSize" }), 10));
		AddOption(dictionary, Pipelining = new MySqlConnectionStringValueOption<bool>(new _003C_003Ez__ReadOnlySingleElementList<string>("Pipelining"), defaultValue: true));
		AddOption(dictionary, ServerRedirectionMode = new MySqlConnectionStringValueOption<MySqlServerRedirectionMode>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Server Redirection Mode", "ServerRedirectionMode" }), MySqlServerRedirectionMode.Disabled));
		AddOption(dictionary, ServerRsaPublicKeyFile = new MySqlConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Server RSA Public Key File", "ServerRsaPublicKeyFile" }), ""));
		AddOption(dictionary, ServerSPN = new MySqlConnectionStringReferenceOption<string>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Server SPN", "ServerSPN" }), ""));
		AddOption(dictionary, TreatTinyAsBoolean = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Treat Tiny As Boolean", "TreatTinyAsBoolean" }), defaultValue: true));
		AddOption(dictionary, UseAffectedRows = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Use Affected Rows", "UseAffectedRows" }), defaultValue: false));
		AddOption(dictionary, UseCompression = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[3] { "Use Compression", "Compress", "UseCompression" }), defaultValue: false));
		AddOption(dictionary, UseXaTransactions = new MySqlConnectionStringValueOption<bool>(new global::_003C_003Ez__ReadOnlyArray<string>(new string[2] { "Use XA Transactions", "UseXaTransactions" }), defaultValue: true));
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
