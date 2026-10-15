using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Security;
using System.Security.Authentication;
using Microsoft.Extensions.Logging;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Logging;

internal static class Log
{
	/// <summary> This API supports the logging infrastructure and is not intended to be used directly from your code. It is subject to change in the future. </summary>
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	private readonly struct __SessionMadeConnectionStruct(string sessionId, string serverVersion, int connectionId, bool supportsCompression, bool supportsAttributes, bool supportsDeprecateEof, bool supportsCachedMetadata, bool supportsSsl, bool supportsSessionTrack, bool supportsPipelining, bool supportsQueryAttributes) : IReadOnlyList<KeyValuePair<string, object?>>, IEnumerable<KeyValuePair<string, object?>>, IEnumerable, IReadOnlyCollection<KeyValuePair<string, object?>>
	{
		private readonly string _sessionId = sessionId;

		private readonly string _serverVersion = serverVersion;

		private readonly int _connectionId = connectionId;

		private readonly bool _supportsCompression = supportsCompression;

		private readonly bool _supportsAttributes = supportsAttributes;

		private readonly bool _supportsDeprecateEof = supportsDeprecateEof;

		private readonly bool _supportsCachedMetadata = supportsCachedMetadata;

		private readonly bool _supportsSsl = supportsSsl;

		private readonly bool _supportsSessionTrack = supportsSessionTrack;

		private readonly bool _supportsPipelining = supportsPipelining;

		private readonly bool _supportsQueryAttributes = supportsQueryAttributes;

		public static readonly Func<__SessionMadeConnectionStruct, Exception?, string> Format = (__SessionMadeConnectionStruct state, Exception? ex) => state.ToString();

		public int Count => 12;

		public KeyValuePair<string, object?> this[int index] => index switch
		{
			0 => new KeyValuePair<string, object>("SessionId", _sessionId), 
			1 => new KeyValuePair<string, object>("ServerVersion", _serverVersion), 
			2 => new KeyValuePair<string, object>("ConnectionId", _connectionId), 
			3 => new KeyValuePair<string, object>("SupportsCompression", _supportsCompression), 
			4 => new KeyValuePair<string, object>("SupportsAttributes", _supportsAttributes), 
			5 => new KeyValuePair<string, object>("SupportsDeprecateEof", _supportsDeprecateEof), 
			6 => new KeyValuePair<string, object>("SupportsCachedMetadata", _supportsCachedMetadata), 
			7 => new KeyValuePair<string, object>("SupportsSsl", _supportsSsl), 
			8 => new KeyValuePair<string, object>("SupportsSessionTrack", _supportsSessionTrack), 
			9 => new KeyValuePair<string, object>("SupportsPipelining", _supportsPipelining), 
			10 => new KeyValuePair<string, object>("SupportsQueryAttributes", _supportsQueryAttributes), 
			11 => new KeyValuePair<string, object>("{OriginalFormat}", "Session {SessionId} made connection; server version {ServerVersion}; connection ID {ConnectionId}; supports: compression {SupportsCompression}, attributes {SupportsAttributes}, deprecate EOF {SupportsDeprecateEof}, cached metadata {SupportsCachedMetadata}, SSL {SupportsSsl}, session track {SupportsSessionTrack}, pipelining {SupportsPipelining}, query attributes {SupportsQueryAttributes}"), 
			_ => throw new IndexOutOfRangeException("index"), 
		};

		public override string ToString()
		{
			string sessionId = _sessionId;
			string serverVersion = _serverVersion;
			int connectionId = _connectionId;
			bool supportsCompression = _supportsCompression;
			bool supportsAttributes = _supportsAttributes;
			bool supportsDeprecateEof = _supportsDeprecateEof;
			bool supportsCachedMetadata = _supportsCachedMetadata;
			bool supportsSsl = _supportsSsl;
			bool supportsSessionTrack = _supportsSessionTrack;
			bool supportsPipelining = _supportsPipelining;
			bool supportsQueryAttributes = _supportsQueryAttributes;
			return $"Session {sessionId} made connection; server version {serverVersion}; connection ID {connectionId}; supports: compression {supportsCompression}, attributes {supportsAttributes}, deprecate EOF {supportsDeprecateEof}, cached metadata {supportsCachedMetadata}, SSL {supportsSsl}, session track {supportsSessionTrack}, pipelining {supportsPipelining}, query attributes {supportsQueryAttributes}";
		}

		public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
		{
			for (int i = 0; i < 12; i++)
			{
				yield return this[i];
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	/// <summary> This API supports the logging infrastructure and is not intended to be used directly from your code. It is subject to change in the future. </summary>
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	private readonly struct __ConnectingToIpAddressStruct(string sessionId, string ipAddress, int ipAddressIndex, int ipAddressCount, string hostName, int hostNameIndex, int hostNameCount) : IReadOnlyList<KeyValuePair<string, object?>>, IEnumerable<KeyValuePair<string, object?>>, IEnumerable, IReadOnlyCollection<KeyValuePair<string, object?>>
	{
		private readonly string _sessionId = sessionId;

		private readonly string _ipAddress = ipAddress;

		private readonly int _ipAddressIndex = ipAddressIndex;

		private readonly int _ipAddressCount = ipAddressCount;

		private readonly string _hostName = hostName;

		private readonly int _hostNameIndex = hostNameIndex;

		private readonly int _hostNameCount = hostNameCount;

		public static readonly Func<__ConnectingToIpAddressStruct, Exception?, string> Format = (__ConnectingToIpAddressStruct state, Exception? ex) => state.ToString();

		public int Count => 8;

		public KeyValuePair<string, object?> this[int index] => index switch
		{
			0 => new KeyValuePair<string, object>("SessionId", _sessionId), 
			1 => new KeyValuePair<string, object>("IpAddress", _ipAddress), 
			2 => new KeyValuePair<string, object>("IpAddressIndex", _ipAddressIndex), 
			3 => new KeyValuePair<string, object>("IpAddressCount", _ipAddressCount), 
			4 => new KeyValuePair<string, object>("HostName", _hostName), 
			5 => new KeyValuePair<string, object>("HostNameIndex", _hostNameIndex), 
			6 => new KeyValuePair<string, object>("HostNameCount", _hostNameCount), 
			7 => new KeyValuePair<string, object>("{OriginalFormat}", "Session {SessionId} connecting to IP address {IpAddress} ({IpAddressIndex} of {IpAddressCount}) for host name {HostName} ({HostNameIndex} of {HostNameCount})"), 
			_ => throw new IndexOutOfRangeException("index"), 
		};

		public override string ToString()
		{
			string sessionId = _sessionId;
			string ipAddress = _ipAddress;
			int ipAddressIndex = _ipAddressIndex;
			int ipAddressCount = _ipAddressCount;
			string hostName = _hostName;
			int hostNameIndex = _hostNameIndex;
			int hostNameCount = _hostNameCount;
			return $"Session {sessionId} connecting to IP address {ipAddress} ({ipAddressIndex} of {ipAddressCount}) for host name {hostName} ({hostNameIndex} of {hostNameCount})";
		}

		public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
		{
			for (int i = 0; i < 8; i++)
			{
				yield return this[i];
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	/// <summary> This API supports the logging infrastructure and is not intended to be used directly from your code. It is subject to change in the future. </summary>
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	private readonly struct __FailedToConnectToIpAddressStruct(string sessionId, string ipAddress, int ipAddressIndex, int ipAddressCount, string hostName, int hostNameIndex, int hostNameCount, string exceptionMessage) : IReadOnlyList<KeyValuePair<string, object?>>, IEnumerable<KeyValuePair<string, object?>>, IEnumerable, IReadOnlyCollection<KeyValuePair<string, object?>>
	{
		private readonly string _sessionId = sessionId;

		private readonly string _ipAddress = ipAddress;

		private readonly int _ipAddressIndex = ipAddressIndex;

		private readonly int _ipAddressCount = ipAddressCount;

		private readonly string _hostName = hostName;

		private readonly int _hostNameIndex = hostNameIndex;

		private readonly int _hostNameCount = hostNameCount;

		private readonly string _exceptionMessage = exceptionMessage;

		public static readonly Func<__FailedToConnectToIpAddressStruct, Exception?, string> Format = (__FailedToConnectToIpAddressStruct state, Exception? ex) => state.ToString();

		public int Count => 9;

		public KeyValuePair<string, object?> this[int index] => index switch
		{
			0 => new KeyValuePair<string, object>("SessionId", _sessionId), 
			1 => new KeyValuePair<string, object>("IpAddress", _ipAddress), 
			2 => new KeyValuePair<string, object>("IpAddressIndex", _ipAddressIndex), 
			3 => new KeyValuePair<string, object>("IpAddressCount", _ipAddressCount), 
			4 => new KeyValuePair<string, object>("HostName", _hostName), 
			5 => new KeyValuePair<string, object>("HostNameIndex", _hostNameIndex), 
			6 => new KeyValuePair<string, object>("HostNameCount", _hostNameCount), 
			7 => new KeyValuePair<string, object>("ExceptionMessage", _exceptionMessage), 
			8 => new KeyValuePair<string, object>("{OriginalFormat}", "Session {SessionId} failed to connect to IP address {IpAddress} ({IpAddressIndex} of {IpAddressCount}) for host name {HostName} ({HostNameIndex} of {HostNameCount}): {ExceptionMessage}"), 
			_ => throw new IndexOutOfRangeException("index"), 
		};

		public override string ToString()
		{
			string sessionId = _sessionId;
			string ipAddress = _ipAddress;
			int ipAddressIndex = _ipAddressIndex;
			int ipAddressCount = _ipAddressCount;
			string hostName = _hostName;
			int hostNameIndex = _hostNameIndex;
			int hostNameCount = _hostNameCount;
			string exceptionMessage = _exceptionMessage;
			return $"Session {sessionId} failed to connect to IP address {ipAddress} ({ipAddressIndex} of {ipAddressCount}) for host name {hostName} ({hostNameIndex} of {hostNameCount}): {exceptionMessage}";
		}

		public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
		{
			for (int i = 0; i < 9; i++)
			{
				yield return this[i];
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	/// <summary> This API supports the logging infrastructure and is not intended to be used directly from your code. It is subject to change in the future. </summary>
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	private readonly struct __CouldNotLoadCaCertificateFromFileStruct(string sessionId, string caCertificateFile) : IReadOnlyList<KeyValuePair<string, object?>>, IEnumerable<KeyValuePair<string, object?>>, IEnumerable, IReadOnlyCollection<KeyValuePair<string, object?>>
	{
		private readonly string _sessionId = sessionId;

		private readonly string _caCertificateFile = caCertificateFile;

		public static readonly Func<__CouldNotLoadCaCertificateFromFileStruct, Exception?, string> Format = (__CouldNotLoadCaCertificateFromFileStruct state, Exception? ex) => state.ToString();

		public int Count => 3;

		public KeyValuePair<string, object?> this[int index] => index switch
		{
			0 => new KeyValuePair<string, object>("SessionId", _sessionId), 
			1 => new KeyValuePair<string, object>("CACertificateFile", _caCertificateFile), 
			2 => new KeyValuePair<string, object>("{OriginalFormat}", "Session {SessionId} couldn't load CA certificate from '{CACertificateFile}'"), 
			_ => throw new IndexOutOfRangeException("index"), 
		};

		public override string ToString()
		{
			string sessionId = _sessionId;
			string caCertificateFile = _caCertificateFile;
			return "Session " + sessionId + " couldn't load CA certificate from '" + caCertificateFile + "'";
		}

		public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
		{
			for (int i = 0; i < 3; i++)
			{
				yield return this[i];
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	/// <summary> This API supports the logging infrastructure and is not intended to be used directly from your code. It is subject to change in the future. </summary>
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	private readonly struct __ConnectedTlsDetailedPreliminaryStruct(string sessionId, SslPolicyErrors sslPolicyErrors, SslProtocols sslProtocol, CipherAlgorithmType cipherAlgorithm, HashAlgorithmType hashAlgorithm, ExchangeAlgorithmType keyExchangeAlgorithm, int keyExchangeStrength) : IReadOnlyList<KeyValuePair<string, object?>>, IEnumerable<KeyValuePair<string, object?>>, IEnumerable, IReadOnlyCollection<KeyValuePair<string, object?>>
	{
		private readonly string _sessionId = sessionId;

		private readonly SslPolicyErrors _sslPolicyErrors = sslPolicyErrors;

		private readonly SslProtocols _sslProtocol = sslProtocol;

		private readonly CipherAlgorithmType _cipherAlgorithm = cipherAlgorithm;

		private readonly HashAlgorithmType _hashAlgorithm = hashAlgorithm;

		private readonly ExchangeAlgorithmType _keyExchangeAlgorithm = keyExchangeAlgorithm;

		private readonly int _keyExchangeStrength = keyExchangeStrength;

		public static readonly Func<__ConnectedTlsDetailedPreliminaryStruct, Exception?, string> Format = (__ConnectedTlsDetailedPreliminaryStruct state, Exception? ex) => state.ToString();

		public int Count => 8;

		public KeyValuePair<string, object?> this[int index] => index switch
		{
			0 => new KeyValuePair<string, object>("SessionId", _sessionId), 
			1 => new KeyValuePair<string, object>("SslPolicyErrors", _sslPolicyErrors), 
			2 => new KeyValuePair<string, object>("SslProtocol", _sslProtocol), 
			3 => new KeyValuePair<string, object>("CipherAlgorithm", _cipherAlgorithm), 
			4 => new KeyValuePair<string, object>("HashAlgorithm", _hashAlgorithm), 
			5 => new KeyValuePair<string, object>("KeyExchangeAlgorithm", _keyExchangeAlgorithm), 
			6 => new KeyValuePair<string, object>("KeyExchangeStrength", _keyExchangeStrength), 
			7 => new KeyValuePair<string, object>("{OriginalFormat}", "Session {SessionId} provisionally connected TLS with error {SslPolicyErrors} using {SslProtocol}, {CipherAlgorithm}, {HashAlgorithm}, {KeyExchangeAlgorithm}, {KeyExchangeStrength}"), 
			_ => throw new IndexOutOfRangeException("index"), 
		};

		public override string ToString()
		{
			string sessionId = _sessionId;
			SslPolicyErrors sslPolicyErrors = _sslPolicyErrors;
			SslProtocols sslProtocol = _sslProtocol;
			CipherAlgorithmType cipherAlgorithm = _cipherAlgorithm;
			HashAlgorithmType hashAlgorithm = _hashAlgorithm;
			ExchangeAlgorithmType keyExchangeAlgorithm = _keyExchangeAlgorithm;
			int keyExchangeStrength = _keyExchangeStrength;
			return $"Session {sessionId} provisionally connected TLS with error {sslPolicyErrors} using {sslProtocol}, {cipherAlgorithm}, {hashAlgorithm}, {keyExchangeAlgorithm}, {keyExchangeStrength}";
		}

		public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
		{
			for (int i = 0; i < 8; i++)
			{
				yield return this[i];
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	/// <summary> This API supports the logging infrastructure and is not intended to be used directly from your code. It is subject to change in the future. </summary>
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	private readonly struct __SuccessfullyPingedServerStruct(string sessionId) : IReadOnlyList<KeyValuePair<string, object?>>, IEnumerable<KeyValuePair<string, object?>>, IEnumerable, IReadOnlyCollection<KeyValuePair<string, object?>>
	{
		private readonly string _sessionId = sessionId;

		public static readonly Func<__SuccessfullyPingedServerStruct, Exception?, string> Format = (__SuccessfullyPingedServerStruct state, Exception? ex) => state.ToString();

		public int Count => 2;

		public KeyValuePair<string, object?> this[int index] => index switch
		{
			0 => new KeyValuePair<string, object>("SessionId", _sessionId), 
			1 => new KeyValuePair<string, object>("{OriginalFormat}", "Session {SessionId} successfully pinged server"), 
			_ => throw new IndexOutOfRangeException("index"), 
		};

		public override string ToString()
		{
			string sessionId = _sessionId;
			return "Session " + sessionId + " successfully pinged server";
		}

		public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
		{
			for (int i = 0; i < 2; i++)
			{
				yield return this[i];
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, int, string, Exception?> __DataSourceCreatedWithPoolWithNameCallback = LoggerMessage.Define<int, int, string>(LogLevel.Information, new EventId(1000, "DataSourceCreatedWithPoolWithName"), "Data source {DataSourceId} created with pool {PoolId} and name {DataSourceName}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, string, Exception?> __DataSourceCreatedWithoutPoolWithNameCallback = LoggerMessage.Define<int, string>(LogLevel.Information, new EventId(1001, "DataSourceCreatedWithoutPoolWithName"), "Data source {DataSourceId} created with name {DataSourceName} and no pool", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, int, Exception?> __DataSourceCreatedWithPoolWithoutNameCallback = LoggerMessage.Define<int, int>(LogLevel.Information, new EventId(1002, "DataSourceCreatedWithPoolWithoutName"), "Data source {DataSourceId} created with pool {PoolId} and no name", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, Exception?> __DataSourceCreatedWithoutPoolWithoutNameCallback = LoggerMessage.Define<int>(LogLevel.Information, new EventId(1003, "DataSourceCreatedWithoutPoolWithoutName"), "Data source {DataSourceId} created with no pool and no name", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, string, Exception?> __PeriodicPasswordProviderFailedCallback = LoggerMessage.Define<int, string>(LogLevel.Error, new EventId(1100, "PeriodicPasswordProviderFailed"), "Periodic password provider for data source {DataSourceId} failed: {ExceptionMessage}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __CreatedNonPooledSessionCallback = LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2001, "CreatedNonPooledSession"), "Created new non-pooled session {SessionId}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __ResettingConnectionCallback = LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2002, "ResettingConnection"), "Session {SessionId} resetting connection", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __ResettingConnectionFailedCallback = LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(2017, "ResettingConnectionFailed"), "Session {SessionId} failed to reset connection: {Message}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __ResetConnectionCallback = LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2018, "ResetConnection"), "Session {SessionId} reset connection", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, int, Exception?> __ReturningToPoolCallback = LoggerMessage.Define<string, int>(LogLevel.Trace, new EventId(2003, "ReturningToPool"), "Session {SessionId} returning to pool {PoolId}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __SendingQuitCommandCallback = LoggerMessage.Define<string>(LogLevel.Trace, new EventId(2004, "SendingQuitCommand"), "Session {SessionId} sending QUIT command", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __ConnectingFailedCallback = LoggerMessage.Define<string>(LogLevel.Error, new EventId(2100, "ConnectingFailed"), "Session {SessionId} connecting failed", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __ServerSentAuthPluginNameCallback = LoggerMessage.Define<string, string>(LogLevel.Trace, new EventId(2101, "ServerSentAuthPluginName"), "Session {SessionId} server sent auth plugin name {AuthPluginName}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __UnsupportedAuthenticationMethodCallback = LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(2102, "UnsupportedAuthenticationMethod"), "Session {SessionId} unsupported authentication method {AuthPluginName}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __AutoDetectedAurora57Callback = LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(2103, "AutoDetectedAurora57"), "Session {SessionId} auto-detected Aurora 5.7 at '{HostName}'; disabling pipelining", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __ServerDoesNotSupportSslCallback = LoggerMessage.Define<string>(LogLevel.Error, new EventId(2105, "ServerDoesNotSupportSsl"), "Session {SessionId} requires SSL but server doesn't support it", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __CouldNotConnectToServerCallback = LoggerMessage.Define<string>(LogLevel.Error, new EventId(2108, "CouldNotConnectToServer"), "Session {SessionId} couldn't connect to server", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __SendingPipelinedResetConnectionRequestCallback = LoggerMessage.Define<string, string>(LogLevel.Trace, new EventId(2109, "SendingPipelinedResetConnectionRequest"), "Session {SessionId} server version {ServerVersion} supports reset connection and pipelining; sending pipelined reset connection request", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __SendingResetConnectionRequestCallback = LoggerMessage.Define<string, string>(LogLevel.Trace, new EventId(2110, "SendingResetConnectionRequest"), "Session {SessionId} server version {ServerVersion} supports reset connection; sending reset connection request", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __SendingChangeUserRequestCallback = LoggerMessage.Define<string, string>(LogLevel.Trace, new EventId(2111, "SendingChangeUserRequest"), "Session {SessionId} server version {ServerVersion} doesn't support reset connection; sending change user request", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __SendingChangeUserRequestDueToChangedDatabaseCallback = LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(2112, "SendingChangeUserRequestDueToChangedDatabase"), "Session {SessionId} sending change user request due to changed database {Database}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __OptimisticReauthenticationFailedCallback = LoggerMessage.Define<string>(LogLevel.Trace, new EventId(2113, "OptimisticReauthenticationFailed"), "Session {SessionId} optimistic reauthentication failed; logging in again", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __IgnoringFailureInTryResetConnectionAsyncCallback = LoggerMessage.Define<string, string>(LogLevel.Trace, new EventId(2114, "IgnoringFailureInTryResetConnectionAsync"), "Session {SessionId} ignoring {Failure} in TryResetConnectionAsync", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __SwitchingToAuthenticationMethodCallback = LoggerMessage.Define<string, string>(LogLevel.Trace, new EventId(2115, "SwitchingToAuthenticationMethod"), "Session {SessionId} switching to authentication method {AuthenticationMethod}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __NeedsSecureConnectionCallback = LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(2116, "NeedsSecureConnection"), "Session {SessionId} needs a secure connection to use authentication method {AuthenticationMethod}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __AuthenticationMethodNotSupportedCallback = LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(2117, "AuthenticationMethodNotSupported"), "Session {SessionId} is requesting authentication method {AuthenticationMethod} which is not supported", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __CouldNotLoadServerRsaPublicKeyCallback = LoggerMessage.Define<string>(LogLevel.Error, new EventId(2118, "CouldNotLoadServerRsaPublicKey"), "Session {SessionId} couldn't load server's RSA public key", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __CouldNotLoadServerRsaPublicKeyFromFileCallback = LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(2119, "CouldNotLoadServerRsaPublicKeyFromFile"), "Session {SessionId} couldn't load server's RSA public key from '{PublicKeyFilePath}'", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __CouldNotUseAuthenticationMethodForRsaCallback = LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(2120, "CouldNotUseAuthenticationMethodForRsa"), "Session {SessionId} couldn't use authentication method {AuthenticationMethod} because RSA key wasn't specified or couldn't be retrieved", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, int, int, string, Exception?> __FailedToResolveHostNameCallback = LoggerMessage.Define<string, string, int, int, string>(LogLevel.Warning, new EventId(2121, "FailedToResolveHostName"), "Session {SessionId} failed to resolve host name {HostName} ({HostNameIndex} of {HostNameCount}): {ExceptionMessage}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, string, Exception?> __ConnectTimeoutExpiredCallback = LoggerMessage.Define<string, string, string>(LogLevel.Information, new EventId(2123, "ConnectTimeoutExpired"), "Session {SessionId} connect timeout expired connecting to IP address {IpAddress} for host name {HostName}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, string, string, Exception?> __FailedToConnectToSingleIpAddressCallback = LoggerMessage.Define<string, string, string, string>(LogLevel.Information, new EventId(2124, "FailedToConnectToSingleIpAddress"), "Session {SessionId} failed to connect to IP address {IpAddress} for host name {HostName}: {ExceptionMessage}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, string, int?, Exception?> __ConnectedToIpAddressCallback = LoggerMessage.Define<string, string, string, int?>(LogLevel.Trace, new EventId(2126, "ConnectedToIpAddress"), "Session {SessionId} connected to IP address {IpAddress} for host name {HostName} with local port {LocalPort}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __ConnectingToUnixSocketCallback = LoggerMessage.Define<string, string>(LogLevel.Trace, new EventId(2127, "ConnectingToUnixSocket"), "Session {SessionId} connecting to UNIX socket {SocketPath}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __ConnectTimeoutExpiredForUnixSocketCallback = LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(2128, "ConnectTimeoutExpiredForUnixSocket"), "Session {SessionId} connect timeout expired connecting to UNIX socket {SocketPath}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, string, Exception?> __ConnectingToNamedPipeCallback = LoggerMessage.Define<string, string, string>(LogLevel.Trace, new EventId(2129, "ConnectingToNamedPipe"), "Session {SessionId} connecting to named pipe {PipeName} on server {HostName}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, string, Exception?> __ConnectTimeoutExpiredForNamedPipeCallback = LoggerMessage.Define<string, string, string>(LogLevel.Information, new EventId(2130, "ConnectTimeoutExpiredForNamedPipe"), "Session {SessionId} connect timeout expired connecting to named pipe {PipeName} on server {HostName}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __InitializingTlsConnectionCallback = LoggerMessage.Define<string>(LogLevel.Trace, new EventId(2131, "InitializingTlsConnection"), "Session {SessionId} initializing TLS connection", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __NoCertificatesFoundCallback = LoggerMessage.Define<string>(LogLevel.Error, new EventId(2132, "NoCertificatesFound"), "Session {SessionId} found no certificates in the certificate store", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __CertificateNotFoundInStoreCallback = LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(2133, "CertificateNotFoundInStore"), "Session {SessionId} certificate with thumbprint {Thumbprint} not found in store", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, MySqlCertificateStoreLocation, Exception?> __CouldNotLoadCertificateCallback = LoggerMessage.Define<string, MySqlCertificateStoreLocation>(LogLevel.Error, new EventId(2134, "CouldNotLoadCertificate"), "Session {SessionId} couldn't load certificate from {CertificateStoreLocation}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __NoPrivateKeyIncludedWithCertificateFileCallback = LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(2135, "NoPrivateKeyIncludedWithCertificateFile"), "Session {SessionId} no private key included with certificate file '{CertificateFile}'", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __CouldNotLoadCertificateFromFileCallback = LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(2136, "CouldNotLoadCertificateFromFile"), "Session {SessionId} couldn't load certificate from '{CertificateFile}'", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __FailedToObtainClientCertificatesCallback = LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(2137, "FailedToObtainClientCertificates"), "Session {SessionId} failed to obtain client certificates via ProvideClientCertificatesCallback: {ExceptionMessage}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __LoadingCaCertificatesFromFileCallback = LoggerMessage.Define<string, string>(LogLevel.Trace, new EventId(2138, "LoadingCaCertificatesFromFile"), "Session {SessionId} loading CA certificate(s) from '{CACertificateFile}'", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, int, Exception?> __LoadingCaCertificateCallback = LoggerMessage.Define<string, int>(LogLevel.Trace, new EventId(2140, "LoadingCaCertificate"), "Session {SessionId} loading certificate at index {Index} in the CA certificate file.", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, int, string, Exception?> __LoadedCaCertificatesFromFileCallback = LoggerMessage.Define<string, int, string>(LogLevel.Trace, new EventId(2141, "LoadedCaCertificatesFromFile"), "Session {SessionId} loaded {CertificateCount} certificate(s) from '{CACertificateFile}'", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __NotUsingRemoteCertificateValidationCallbackDueToSslCaCallback = LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2142, "NotUsingRemoteCertificateValidationCallbackDueToSslCa"), "Session {SessionId} not using client-provided RemoteCertificateValidationCallback because SslCA is specified", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, MySqlSslMode, Exception?> __NotUsingRemoteCertificateValidationCallbackDueToSslModeCallback = LoggerMessage.Define<string, MySqlSslMode>(LogLevel.Warning, new EventId(2143, "NotUsingRemoteCertificateValidationCallbackDueToSslMode"), "Session {SessionId} not using client-provided RemoteCertificateValidationCallback because SslMode is {SslMode}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __UsingRemoteCertificateValidationCallbackCallback = LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2144, "UsingRemoteCertificateValidationCallback"), "Session {SessionId} using client-provided RemoteCertificateValidationCallback", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, SslProtocols, CipherAlgorithmType, HashAlgorithmType, ExchangeAlgorithmType, int, Exception?> __ConnectedTlsDetailedCallback = LoggerMessage.Define<string, SslProtocols, CipherAlgorithmType, HashAlgorithmType, ExchangeAlgorithmType, int>(LogLevel.Debug, new EventId(2146, "ConnectedTlsDetailed"), "Session {SessionId} connected TLS using {SslProtocol}, {CipherAlgorithm}, {HashAlgorithm}, {KeyExchangeAlgorithm}, {KeyExchangeStrength}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __CouldNotInitializeTlsConnectionCallback = LoggerMessage.Define<string>(LogLevel.Error, new EventId(2147, "CouldNotInitializeTlsConnection"), "Session {SessionId} couldn't initialize TLS connection", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __LoadingClientKeyFromKeyFileCallback = LoggerMessage.Define<string, string>(LogLevel.Trace, new EventId(2148, "LoadingClientKeyFromKeyFile"), "Session {SessionId} loading client key from '{ClientKeyFilePath}'", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __CouldNotLoadClientKeyFromKeyFileCallback = LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(2149, "CouldNotLoadClientKeyFromKeyFile"), "Session {SessionId} couldn't load client key from '{ClientKeyFilePath}'", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __DetectedProxyCallback = LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2150, "DetectedProxy"), "Session {SessionId} detected proxy; getting CONNECTION_ID(), VERSION() from server", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, int, int, string, string, Exception?> __ChangingConnectionIdCallback = LoggerMessage.Define<string, int, int, string, string>(LogLevel.Debug, new EventId(2151, "ChangingConnectionId"), "Session {SessionId} changing connection id from {OldConnectionId} to {ConnectionId} and server version from {OldServerVersion} to {ServerVersion}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __FailedToGetConnectionIdCallback = LoggerMessage.Define<string>(LogLevel.Information, new EventId(2152, "FailedToGetConnectionId"), "Session {SessionId} failed to get CONNECTION_ID(), VERSION()", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __ClosingStreamSocketCallback = LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2005, "ClosingStreamSocket"), "Session {SessionId} closing stream/socket", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __CreatingConnectionAttributesCallback = LoggerMessage.Define<string>(LogLevel.Trace, new EventId(2153, "CreatingConnectionAttributes"), "Session {SessionId} creating connection attributes", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __ObtainingPasswordViaProvidePasswordCallbackCallback = LoggerMessage.Define<string>(LogLevel.Trace, new EventId(2154, "ObtainingPasswordViaProvidePasswordCallback"), "Session {SessionId} obtaining password via ProvidePasswordCallback", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __FailedToObtainPasswordCallback = LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(2155, "FailedToObtainPassword"), "Session {SessionId} failed to obtain password via ProvidePasswordCallback: {ExceptionMessage}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, SslPolicyErrors, Exception?> __CertificateErrorUnixSocketCallback = LoggerMessage.Define<string, SslPolicyErrors>(LogLevel.Trace, new EventId(2158, "CertificateErrorUnixSocket"), "Session {SessionId} ignoring remote certificate error {SslPolicyErrors} due to Unix socket connection", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, SslPolicyErrors, Exception?> __CertificateErrorNoPasswordCallback = LoggerMessage.Define<string, SslPolicyErrors>(LogLevel.Trace, new EventId(2159, "CertificateErrorNoPassword"), "Session {SessionId} acknowledging remote certificate error {SslPolicyErrors} due to passwordless connection", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, SslPolicyErrors, Exception?> __CertificateErrorValidThumbprintCallback = LoggerMessage.Define<string, SslPolicyErrors>(LogLevel.Trace, new EventId(2160, "CertificateErrorValidThumbprint"), "Session {SessionId} ignoring remote certificate error {SslPolicyErrors} due to valid signature in OK packet", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, Exception?> __IgnoringCancellationForCommandCallback = LoggerMessage.Define<int>(LogLevel.Trace, new EventId(2300, "IgnoringCancellationForCommand"), "Ignoring cancellation for closed connection or invalid command {CommandId}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, string, string, Exception?> __CommandHasBeenCanceledCallback = LoggerMessage.Define<int, string, string>(LogLevel.Debug, new EventId(2301, "CommandHasBeenCanceled"), "Command {CommandId} for session {SessionId} has been canceled via {CancellationSource}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __IgnoringCancellationForClosedConnectionCallback = LoggerMessage.Define<string>(LogLevel.Information, new EventId(2302, "IgnoringCancellationForClosedConnection"), "Session {SessionId} ignoring cancellation for closed connection", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, int, Exception?> __CancelingCommandFailedCallback = LoggerMessage.Define<string, int>(LogLevel.Information, new EventId(2303, "CancelingCommandFailed"), "Session {SessionId} cancelling command {CommandId} failed", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, int, int, string?, Exception?> __WillCancelCommandCallback = LoggerMessage.Define<string, int, int, string>(LogLevel.Debug, new EventId(2304, "WillCancelCommand"), "Session {SessionId} will cancel command {CommandId} ({CancelAttemptCount} attempts); CommandText: {CommandText}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, int, string, string?, Exception?> __CancelingCommandFromSessionCallback = LoggerMessage.Define<string, int, string, string>(LogLevel.Information, new EventId(2305, "CancelingCommandFromSession"), "Session {SessionId} canceling command {CommandId} from session {CancelingSessionId}; CommandText: {CommandText}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, int, int, Exception?> __IgnoringCancellationForInactiveCommandCallback = LoggerMessage.Define<string, int, int>(LogLevel.Debug, new EventId(2306, "IgnoringCancellationForInactiveCommand"), "Session {SessionId} active command {ActiveCommandId} is not the command {CommandId} being canceled; ignoring cancellation.", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, int, string, Exception?> __CancelingCommandCallback = LoggerMessage.Define<string, int, string>(LogLevel.Debug, new EventId(2307, "CancelingCommand"), "Session {SessionId} canceling command {CommandId} with text {CommandText}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __SendingSleepToClearPendingCancellationCallback = LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2308, "SendingSleepToClearPendingCancellation"), "Session {SessionId} sending 'SLEEP(0)' command to clear pending cancellation", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __GettingCachedProcedureCallback = LoggerMessage.Define<string, string>(LogLevel.Trace, new EventId(2400, "GettingCachedProcedure"), "Session {SessionId} getting cached procedure named {ProcedureName}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, int?, Exception?> __PoolDoesNotHaveSharedProcedureCacheCallback = LoggerMessage.Define<string, int?>(LogLevel.Information, new EventId(2401, "PoolDoesNotHaveSharedProcedureCache"), "Session {SessionId} pool {PoolId} doesn't have a shared procedure cache; procedure will only be cached on this connection", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, string, Exception?> __CouldNotNormalizeDatabaseAndNameCallback = LoggerMessage.Define<string, string, string>(LogLevel.Information, new EventId(2402, "CouldNotNormalizeDatabaseAndName"), "Session {SessionId} couldn't normalize the name '{ProcedureName}' in database {Database}; not caching procedure", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, string, Exception?> __FailedToCacheProcedureCallback = LoggerMessage.Define<string, string, string>(LogLevel.Information, new EventId(2403, "FailedToCacheProcedure"), "Session {SessionId} failed to cache procedure {Schema}.{Component}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, string, Exception?> __CachingProcedureCallback = LoggerMessage.Define<string, string, string>(LogLevel.Trace, new EventId(2404, "CachingProcedure"), "Session {SessionId} caching procedure {Schema}.{Component}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, int, Exception?> __ProcedureCacheCountCallback = LoggerMessage.Define<string, int>(LogLevel.Trace, new EventId(2405, "ProcedureCacheCount"), "Session {SessionId} procedure cache count is {ProcedureCacheCount}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, string, Exception?> __DidNotFindCachedProcedureCallback = LoggerMessage.Define<string, string, string>(LogLevel.Information, new EventId(2406, "DidNotFindCachedProcedure"), "Session {SessionId} did not find cached procedure {Schema}.{Component}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, string, Exception?> __ReturningCachedProcedureCallback = LoggerMessage.Define<string, string, string>(LogLevel.Trace, new EventId(2407, "ReturningCachedProcedure"), "Session {SessionId} returning cached procedure {Schema}.{Component}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, string, string, Exception?> __FailedToRetrieveProcedureMetadataCallback = LoggerMessage.Define<string, string, string, string>(LogLevel.Information, new EventId(2408, "FailedToRetrieveProcedureMetadata"), "Session {SessionId} failed to retrieve metadata for {Schema}.{Component}; falling back to INFORMATION_SCHEMA: {ExceptionMessage}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __ServerDoesNotSupportCachedProceduresCallback = LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(2409, "ServerDoesNotSupportCachedProcedures"), "Session {SessionId} server version {ServerVersion} does not support cached procedures", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, int, int, Exception?> __ProcedureHasRoutineCountCallback = LoggerMessage.Define<string, string, int, int>(LogLevel.Trace, new EventId(2410, "ProcedureHasRoutineCount"), "Procedure for {Schema}.{Component} has {RoutineCount} routines and {ParameterCount} parameters", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __CreatedNewSessionCallback = LoggerMessage.Define<string>(LogLevel.Trace, new EventId(2000, "CreatedNewSession"), "Created new session {SessionId}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __PingingServerCallback = LoggerMessage.Define<string>(LogLevel.Trace, new EventId(2500, "PingingServer"), "Session {SessionId} pinging server", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __PingFailedCallback = LoggerMessage.Define<string, string>(LogLevel.Trace, new EventId(2502, "PingFailed"), "Session {SessionId} ping failed due to {Failure}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __SettingStateToFailedCallback = LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2009, "SettingStateToFailed"), "Session {SessionId} setting state to Failed", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, int, int, Exception?> __ExpectedToReadMoreBytesCallback = LoggerMessage.Define<string, int, int>(LogLevel.Error, new EventId(2010, "ExpectedToReadMoreBytes"), "Session {SessionId} expected to read {ExpectedByteCount} bytes but only read {ReadByteCount}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, int, string, string, Exception?> __ErrorPayloadCallback = LoggerMessage.Define<string, int, string, string>(LogLevel.Debug, new EventId(2006, "ErrorPayload"), "Session {SessionId} got error payload: {ErrorCode}, {State}, {Message}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, IOBehavior, int, Exception?> __CommandExecutorExecuteReaderCallback = LoggerMessage.Define<string, IOBehavior, int>(LogLevel.Trace, new EventId(2202, "CommandExecutorExecuteReader"), "Session {SessionId} ExecuteReader {IOBehavior} for {CommandCount} command(s)", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __QueryWasInterruptedCallback = LoggerMessage.Define<string>(LogLevel.Information, new EventId(2203, "QueryWasInterrupted"), "Session {SessionId} query was interrupted", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __PreparingCommandPayloadCallback = LoggerMessage.Define<string, string>(LogLevel.Trace, new EventId(2204, "PreparingCommandPayload"), "Session {SessionId} preparing command payload for: {CommandText}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, int, string, Exception?> __PreparingCommandPayloadWithIdCallback = LoggerMessage.Define<string, int, string>(LogLevel.Trace, new EventId(2205, "PreparingCommandPayloadWithId"), "Session {SessionId} preparing statement payload with ID {StatementId} for: {CommandText}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __QueryAttributesNotSupportedCallback = LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(2206, "QueryAttributesNotSupported"), "Session {SessionId} has query attributes but server doesn't support them; CommandText: {CommandText}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, int, Exception?> __QueryAttributesNotSupportedWithIdCallback = LoggerMessage.Define<string, int>(LogLevel.Warning, new EventId(2207, "QueryAttributesNotSupportedWithId"), "Session {SessionId} has attributes for statement {StatementId} but the server does not support them", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, string, Exception?> __IgnoringExceptionInDisposeAsyncCallback = LoggerMessage.Define<string, string, string>(LogLevel.Warning, new EventId(2208, "IgnoringExceptionInDisposeAsync"), "Session {SessionId} ignoring exception in MySqlDataReader.DisposeAsync. Message: {ExceptionMessage}. CommandText: {CommandText}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __StartingBulkCopyCallback = LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2503, "StartingBulkCopy"), "Starting bulk copy to {TableName}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, string, Exception?> __AddingDefaultColumnMappingCallback = LoggerMessage.Define<int, string>(LogLevel.Debug, new EventId(2504, "AddingDefaultColumnMapping"), "Adding default column mapping from {SourceOrdinal} to {DestinationColumn}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, Exception?> __IgnoringColumnCallback = LoggerMessage.Define<int>(LogLevel.Debug, new EventId(2505, "IgnoringColumn"), "Ignoring column with source ordinal {SourceOrdinal}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __FinishedBulkCopyCallback = LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2506, "FinishedBulkCopy"), "Finished bulk copy to {TableName}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, int, int, Exception?> __BulkCopyFailedCallback = LoggerMessage.Define<string, int, int>(LogLevel.Error, new EventId(2507, "BulkCopyFailed"), "Bulk copy to {TableName} failed: {RowsCopied} row(s) copied; {RowsInserted} row(s) inserted", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, string, string, Exception?> __ColumnMappingAlreadyHasExpressionCallback = LoggerMessage.Define<int, string, string>(LogLevel.Information, new EventId(2508, "ColumnMappingAlreadyHasExpression"), "Column mapping for {SourceOrdinal} to {DestinationColumn} already has expression {Expression}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, string, string, Exception?> __SettingExpressionToMapColumnCallback = LoggerMessage.Define<int, string, string>(LogLevel.Trace, new EventId(2509, "SettingExpressionToMapColumn"), "Setting expression to map column {SourceOrdinal} to {DestinationColumn}: {Expression}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __StartingTransactionCallback = LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2700, "StartingTransaction"), "Session {SessionId} starting transaction", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __StartedTransactionCallback = LoggerMessage.Define<string>(LogLevel.Trace, new EventId(2701, "StartedTransaction"), "Session {SessionId} started transaction", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __CommittingTransactionCallback = LoggerMessage.Define<string>(LogLevel.Trace, new EventId(2702, "CommittingTransaction"), "Session {SessionId} committing transaction", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __CommittedTransactionCallback = LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2703, "CommittedTransaction"), "Session {SessionId} committed transaction", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __RollingBackTransactionCallback = LoggerMessage.Define<string>(LogLevel.Trace, new EventId(2704, "RollingBackTransaction"), "Session {SessionId} rolling back transaction", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __RolledBackTransactionCallback = LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2705, "RolledBackTransaction"), "Session {SessionId} rolled back transaction", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, Exception?> __WaitingForAvailableSessionCallback = LoggerMessage.Define<int>(LogLevel.Trace, new EventId(3000, "WaitingForAvailableSession"), "Pool {PoolId} waiting for an available session", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __FailedInReceiveReplyAsyncCallback = LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2008, "FailedInReceiveReplyAsync"), "Session {SessionId} failed in ReceiveReplyAsync", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __FailedInSendReplyAsyncCallback = LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2007, "FailedInSendReplyAsync"), "Session {SessionId} failed in SendReplyAsync", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, string, Exception?> __CreatingNewConnectionPoolCallback = LoggerMessage.Define<int, string>(LogLevel.Information, new EventId(3001, "CreatingNewConnectionPool"), "Creating new connection pool {PoolId} for {ConnectionString}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, Exception?> __ScanningForLeakedSessionsCallback = LoggerMessage.Define<int>(LogLevel.Debug, new EventId(3002, "ScanningForLeakedSessions"), "Pool {PoolId} is empty; scanning for any leaked sessions", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, Exception?> __FoundExistingSessionCallback = LoggerMessage.Define<int>(LogLevel.Trace, new EventId(3003, "FoundExistingSession"), "Pool {PoolId} found an existing session; checking it for validity", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, Exception?> __DiscardingSessionDueToWrongGenerationCallback = LoggerMessage.Define<int>(LogLevel.Trace, new EventId(3004, "DiscardingSessionDueToWrongGeneration"), "Pool {PoolId} discarding session due to wrong generation", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, string, Exception?> __SessionIsUnusableCallback = LoggerMessage.Define<int, string>(LogLevel.Information, new EventId(3005, "SessionIsUnusable"), "Pool {PoolId} session {SessionId} is unusable; destroying it", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, string, int, Exception?> __ReturningPooledSessionCallback = LoggerMessage.Define<int, string, int>(LogLevel.Trace, new EventId(3006, "ReturningPooledSession"), "Pool {PoolId} returning pooled session {SessionId} to caller; {LeasedSessionCount} leased session(s)", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, string, int, Exception?> __ReturningNewSessionCallback = LoggerMessage.Define<int, string, int>(LogLevel.Trace, new EventId(3007, "ReturningNewSession"), "Pool {PoolId} returning new session {SessionId} to caller; {LeasedSessionCount} leased session(s)", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, string, string, Exception?> __DisposingCreatedSessionDueToExceptionCallback = LoggerMessage.Define<int, string, string>(LogLevel.Debug, new EventId(3008, "DisposingCreatedSessionDueToException"), "Pool {PoolId} disposing created session {SessionId} due to exception: {ExceptionMessage}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, string, Exception?> __UnexpectedErrorInGetSessionAsyncCallback = LoggerMessage.Define<int, string>(LogLevel.Warning, new EventId(3009, "UnexpectedErrorInGetSessionAsync"), "Pool {PoolId} unexpected error in GetSessionAsync: {ExceptionMessage}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, string, Exception?> __ReceivingSessionBackCallback = LoggerMessage.Define<int, string>(LogLevel.Trace, new EventId(3010, "ReceivingSessionBack"), "Pool {PoolId} receiving session {SessionId} back", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, string, Exception?> __ReceivedInvalidSessionCallback = LoggerMessage.Define<int, string>(LogLevel.Information, new EventId(3011, "ReceivedInvalidSession"), "Pool {PoolId} received invalid session {SessionId}; destroying it", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, string, Exception?> __ReceivedExpiredSessionCallback = LoggerMessage.Define<int, string>(LogLevel.Debug, new EventId(3012, "ReceivedExpiredSession"), "Pool {PoolId} received expired session {SessionId}; destroying it", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, Exception?> __ClearingConnectionPoolCallback = LoggerMessage.Define<int>(LogLevel.Information, new EventId(3013, "ClearingConnectionPool"), "Pool {PoolId} clearing connection pool", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, Exception?> __DisposingConnectionPoolCallback = LoggerMessage.Define<int>(LogLevel.Debug, new EventId(3014, "DisposingConnectionPool"), "Pool {PoolId} disposing connection pool", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, Exception?> __RecoveredNoSessionsCallback = LoggerMessage.Define<int>(LogLevel.Trace, new EventId(3015, "RecoveredNoSessions"), "Pool {PoolId} recovered no sessions", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, int, Exception?> __RecoveredSessionCountCallback = LoggerMessage.Define<int, int>(LogLevel.Warning, new EventId(3016, "RecoveredSessionCount"), "Pool {PoolId} recovered {SessionCount} sessions", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, string, Exception?> __FoundSessionToCleanUpCallback = LoggerMessage.Define<int, string>(LogLevel.Debug, new EventId(3017, "FoundSessionToCleanUp"), "Pool {PoolId} found session {SessionId} to clean up", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __HasServerRedirectionHeaderCallback = LoggerMessage.Define<string, string>(LogLevel.Trace, new EventId(3200, "HasServerRedirectionHeader"), "Session {SessionId} has server redirection header {Header}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __ServerRedirectionIsDisabledCallback = LoggerMessage.Define<string>(LogLevel.Trace, new EventId(3201, "ServerRedirectionIsDisabled"), "Session {SessionId} server redirection is disabled; ignoring redirection", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, int, string, Exception?> __OpeningNewConnectionCallback = LoggerMessage.Define<string, string, int, string>(LogLevel.Debug, new EventId(3202, "OpeningNewConnection"), "Session {SessionId} opening new connection to {Host}:{Port} as {User}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __FailedToConnectRedirectedSessionCallback = LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(3203, "FailedToConnectRedirectedSession"), "Session {SessionId} failed to connect redirected session {RedirectedSessionId}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, string, Exception?> __ClosingSessionToUseRedirectedSessionCallback = LoggerMessage.Define<string, string>(LogLevel.Trace, new EventId(3204, "ClosingSessionToUseRedirectedSession"), "Closing session {SessionId} to use redirected session {RedirectedSessionId} instead", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __SessionAlreadyConnectedToServerCallback = LoggerMessage.Define<string>(LogLevel.Trace, new EventId(3205, "SessionAlreadyConnectedToServer"), "Session {SessionId} is already connected to this server; ignoring redirection", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, Exception?> __RequiresServerRedirectionCallback = LoggerMessage.Define<string>(LogLevel.Error, new EventId(3206, "RequiresServerRedirection"), "Session {SessionId} requires server redirection but server doesn't support it", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, Exception?> __CreatedPoolWillNotBeUsedCallback = LoggerMessage.Define<int>(LogLevel.Debug, new EventId(3020, "CreatedPoolWillNotBeUsed"), "Pool {PoolId} was created but will not be used (due to race)", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, Exception?> __ReapingConnectionPoolCallback = LoggerMessage.Define<int>(LogLevel.Trace, new EventId(3100, "ReapingConnectionPool"), "Pool {PoolId} reaping connection pool", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, Exception?> __CheckingForDnsChangesCallback = LoggerMessage.Define<int>(LogLevel.Trace, new EventId(3101, "CheckingForDnsChanges"), "Pool {PoolId} checking for DNS changes", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, string, string, string, Exception?> __DetectedDnsChangeCallback = LoggerMessage.Define<int, string, string, string>(LogLevel.Debug, new EventId(3102, "DetectedDnsChange"), "Pool {PoolId} detected DNS change for '{HostName}': {OldAddresses} to {NewAddresses}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, string, string, Exception?> __DnsCheckFailedCallback = LoggerMessage.Define<int, string, string>(LogLevel.Debug, new EventId(3103, "DnsCheckFailed"), "Pool {PoolId} DNS check failed; ignoring '{HostName}': {ExceptionMessage}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, int, Exception?> __ClearingPoolDueToDnsChangesCallback = LoggerMessage.Define<int>(LogLevel.Information, new EventId(3104, "ClearingPoolDueToDnsChanges"), "Pool {PoolId} clearing pool due to DNS changes", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void DataSourceCreatedWithPoolWithName(ILogger logger, int dataSourceId, int poolId, string dataSourceName)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__DataSourceCreatedWithPoolWithNameCallback(logger, dataSourceId, poolId, dataSourceName, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void DataSourceCreatedWithoutPoolWithName(ILogger logger, int dataSourceId, string dataSourceName)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__DataSourceCreatedWithoutPoolWithNameCallback(logger, dataSourceId, dataSourceName, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void DataSourceCreatedWithPoolWithoutName(ILogger logger, int dataSourceId, int poolId)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__DataSourceCreatedWithPoolWithoutNameCallback(logger, dataSourceId, poolId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void DataSourceCreatedWithoutPoolWithoutName(ILogger logger, int dataSourceId)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__DataSourceCreatedWithoutPoolWithoutNameCallback(logger, dataSourceId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void PeriodicPasswordProviderFailed(ILogger logger, Exception exception, int dataSourceId, string exceptionMessage)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__PeriodicPasswordProviderFailedCallback(logger, dataSourceId, exceptionMessage, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CreatedNonPooledSession(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__CreatedNonPooledSessionCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ResettingConnection(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__ResettingConnectionCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ResettingConnectionFailed(ILogger logger, string sessionId, string message)
	{
		if (logger.IsEnabled(LogLevel.Warning))
		{
			__ResettingConnectionFailedCallback(logger, sessionId, message, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ResetConnection(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__ResetConnectionCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ReturningToPool(ILogger logger, string sessionId, int poolId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__ReturningToPoolCallback(logger, sessionId, poolId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void SendingQuitCommand(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__SendingQuitCommandCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ConnectingFailed(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__ConnectingFailedCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ServerSentAuthPluginName(ILogger logger, string sessionId, string authPluginName)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__ServerSentAuthPluginNameCallback(logger, sessionId, authPluginName, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void UnsupportedAuthenticationMethod(ILogger logger, string sessionId, string authPluginName)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__UnsupportedAuthenticationMethodCallback(logger, sessionId, authPluginName, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void AutoDetectedAurora57(ILogger logger, string sessionId, string hostName)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__AutoDetectedAurora57Callback(logger, sessionId, hostName, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void SessionMadeConnection(ILogger logger, string sessionId, string serverVersion, int connectionId, bool supportsCompression, bool supportsAttributes, bool supportsDeprecateEof, bool supportsCachedMetadata, bool supportsSsl, bool supportsSessionTrack, bool supportsPipelining, bool supportsQueryAttributes)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			logger.Log(LogLevel.Debug, new EventId(2104, "SessionMadeConnection"), new __SessionMadeConnectionStruct(sessionId, serverVersion, connectionId, supportsCompression, supportsAttributes, supportsDeprecateEof, supportsCachedMetadata, supportsSsl, supportsSessionTrack, supportsPipelining, supportsQueryAttributes), null, __SessionMadeConnectionStruct.Format);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ServerDoesNotSupportSsl(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__ServerDoesNotSupportSslCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CouldNotConnectToServer(ILogger logger, Exception exception, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__CouldNotConnectToServerCallback(logger, sessionId, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void SendingPipelinedResetConnectionRequest(ILogger logger, string sessionId, string serverVersion)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__SendingPipelinedResetConnectionRequestCallback(logger, sessionId, serverVersion, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void SendingResetConnectionRequest(ILogger logger, string sessionId, string serverVersion)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__SendingResetConnectionRequestCallback(logger, sessionId, serverVersion, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void SendingChangeUserRequest(ILogger logger, string sessionId, string serverVersion)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__SendingChangeUserRequestCallback(logger, sessionId, serverVersion, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void SendingChangeUserRequestDueToChangedDatabase(ILogger logger, string sessionId, string database)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__SendingChangeUserRequestDueToChangedDatabaseCallback(logger, sessionId, database, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void OptimisticReauthenticationFailed(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__OptimisticReauthenticationFailedCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void IgnoringFailureInTryResetConnectionAsync(ILogger logger, Exception exception, string sessionId, string failure)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__IgnoringFailureInTryResetConnectionAsyncCallback(logger, sessionId, failure, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void SwitchingToAuthenticationMethod(ILogger logger, string sessionId, string authenticationMethod)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__SwitchingToAuthenticationMethodCallback(logger, sessionId, authenticationMethod, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void NeedsSecureConnection(ILogger logger, string sessionId, string authenticationMethod)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__NeedsSecureConnectionCallback(logger, sessionId, authenticationMethod, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void AuthenticationMethodNotSupported(ILogger logger, string sessionId, string authenticationMethod)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__AuthenticationMethodNotSupportedCallback(logger, sessionId, authenticationMethod, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CouldNotLoadServerRsaPublicKey(ILogger logger, Exception exception, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__CouldNotLoadServerRsaPublicKeyCallback(logger, sessionId, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CouldNotLoadServerRsaPublicKeyFromFile(ILogger logger, Exception exception, string sessionId, string publicKeyFilePath)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__CouldNotLoadServerRsaPublicKeyFromFileCallback(logger, sessionId, publicKeyFilePath, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CouldNotUseAuthenticationMethodForRsa(ILogger logger, string sessionId, string authenticationMethod)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__CouldNotUseAuthenticationMethodForRsaCallback(logger, sessionId, authenticationMethod, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void FailedToResolveHostName(ILogger logger, Exception exception, string sessionId, string hostName, int hostNameIndex, int hostNameCount, string exceptionMessage)
	{
		if (logger.IsEnabled(LogLevel.Warning))
		{
			__FailedToResolveHostNameCallback(logger, sessionId, hostName, hostNameIndex, hostNameCount, exceptionMessage, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ConnectingToIpAddress(ILogger logger, string sessionId, string ipAddress, int ipAddressIndex, int ipAddressCount, string hostName, int hostNameIndex, int hostNameCount)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			logger.Log(LogLevel.Trace, new EventId(2122, "ConnectingToIpAddress"), new __ConnectingToIpAddressStruct(sessionId, ipAddress, ipAddressIndex, ipAddressCount, hostName, hostNameIndex, hostNameCount), null, __ConnectingToIpAddressStruct.Format);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ConnectTimeoutExpired(ILogger logger, Exception? exception, string sessionId, string ipAddress, string hostName)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__ConnectTimeoutExpiredCallback(logger, sessionId, ipAddress, hostName, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void FailedToConnectToSingleIpAddress(ILogger logger, Exception exception, string sessionId, string ipAddress, string hostName, string exceptionMessage)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__FailedToConnectToSingleIpAddressCallback(logger, sessionId, ipAddress, hostName, exceptionMessage, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void FailedToConnectToIpAddress(ILogger logger, Exception exception, LogLevel logLevel, string sessionId, string ipAddress, int ipAddressIndex, int ipAddressCount, string hostName, int hostNameIndex, int hostNameCount, string exceptionMessage)
	{
		if (logger.IsEnabled(logLevel))
		{
			logger.Log(logLevel, new EventId(2125, "FailedToConnectToIpAddress"), new __FailedToConnectToIpAddressStruct(sessionId, ipAddress, ipAddressIndex, ipAddressCount, hostName, hostNameIndex, hostNameCount, exceptionMessage), exception, __FailedToConnectToIpAddressStruct.Format);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ConnectedToIpAddress(ILogger logger, string sessionId, string ipAddress, string hostName, int? localPort)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__ConnectedToIpAddressCallback(logger, sessionId, ipAddress, hostName, localPort, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ConnectingToUnixSocket(ILogger logger, string sessionId, string socketPath)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__ConnectingToUnixSocketCallback(logger, sessionId, socketPath, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ConnectTimeoutExpiredForUnixSocket(ILogger logger, string sessionId, string socketPath)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__ConnectTimeoutExpiredForUnixSocketCallback(logger, sessionId, socketPath, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ConnectingToNamedPipe(ILogger logger, string sessionId, string pipeName, string hostName)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__ConnectingToNamedPipeCallback(logger, sessionId, pipeName, hostName, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ConnectTimeoutExpiredForNamedPipe(ILogger logger, Exception exception, string sessionId, string pipeName, string hostName)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__ConnectTimeoutExpiredForNamedPipeCallback(logger, sessionId, pipeName, hostName, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void InitializingTlsConnection(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__InitializingTlsConnectionCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void NoCertificatesFound(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__NoCertificatesFoundCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CertificateNotFoundInStore(ILogger logger, string sessionId, string thumbprint)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__CertificateNotFoundInStoreCallback(logger, sessionId, thumbprint, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CouldNotLoadCertificate(ILogger logger, Exception exception, string sessionId, MySqlCertificateStoreLocation certificateStoreLocation)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__CouldNotLoadCertificateCallback(logger, sessionId, certificateStoreLocation, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void NoPrivateKeyIncludedWithCertificateFile(ILogger logger, string sessionId, string certificateFile)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__NoPrivateKeyIncludedWithCertificateFileCallback(logger, sessionId, certificateFile, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CouldNotLoadCertificateFromFile(ILogger logger, Exception exception, string sessionId, string certificateFile)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__CouldNotLoadCertificateFromFileCallback(logger, sessionId, certificateFile, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void FailedToObtainClientCertificates(ILogger logger, Exception exception, string sessionId, string exceptionMessage)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__FailedToObtainClientCertificatesCallback(logger, sessionId, exceptionMessage, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void LoadingCaCertificatesFromFile(ILogger logger, string sessionId, string caCertificateFile)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__LoadingCaCertificatesFromFileCallback(logger, sessionId, caCertificateFile, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CouldNotLoadCaCertificateFromFile(ILogger logger, Exception exception, LogLevel logLevel, string sessionId, string caCertificateFile)
	{
		if (logger.IsEnabled(logLevel))
		{
			logger.Log(logLevel, new EventId(2139, "CouldNotLoadCaCertificateFromFile"), new __CouldNotLoadCaCertificateFromFileStruct(sessionId, caCertificateFile), exception, __CouldNotLoadCaCertificateFromFileStruct.Format);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void LoadingCaCertificate(ILogger logger, string sessionId, int index)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__LoadingCaCertificateCallback(logger, sessionId, index, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void LoadedCaCertificatesFromFile(ILogger logger, string sessionId, int certificateCount, string caCertificateFile)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__LoadedCaCertificatesFromFileCallback(logger, sessionId, certificateCount, caCertificateFile, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void NotUsingRemoteCertificateValidationCallbackDueToSslCa(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Warning))
		{
			__NotUsingRemoteCertificateValidationCallbackDueToSslCaCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void NotUsingRemoteCertificateValidationCallbackDueToSslMode(ILogger logger, string sessionId, MySqlSslMode sslMode)
	{
		if (logger.IsEnabled(LogLevel.Warning))
		{
			__NotUsingRemoteCertificateValidationCallbackDueToSslModeCallback(logger, sessionId, sslMode, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void UsingRemoteCertificateValidationCallback(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__UsingRemoteCertificateValidationCallbackCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ConnectedTlsDetailed(ILogger logger, string sessionId, SslProtocols sslProtocol, CipherAlgorithmType cipherAlgorithm, HashAlgorithmType hashAlgorithm, ExchangeAlgorithmType keyExchangeAlgorithm, int keyExchangeStrength)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__ConnectedTlsDetailedCallback(logger, sessionId, sslProtocol, cipherAlgorithm, hashAlgorithm, keyExchangeAlgorithm, keyExchangeStrength, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CouldNotInitializeTlsConnection(ILogger logger, Exception exception, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__CouldNotInitializeTlsConnectionCallback(logger, sessionId, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void LoadingClientKeyFromKeyFile(ILogger logger, string sessionId, string clientKeyFilePath)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__LoadingClientKeyFromKeyFileCallback(logger, sessionId, clientKeyFilePath, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CouldNotLoadClientKeyFromKeyFile(ILogger logger, Exception exception, string sessionId, string clientKeyFilePath)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__CouldNotLoadClientKeyFromKeyFileCallback(logger, sessionId, clientKeyFilePath, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void DetectedProxy(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__DetectedProxyCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ChangingConnectionId(ILogger logger, string sessionId, int oldConnectionId, int connectionId, string oldServerVersion, string serverVersion)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__ChangingConnectionIdCallback(logger, sessionId, oldConnectionId, connectionId, oldServerVersion, serverVersion, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void FailedToGetConnectionId(ILogger logger, Exception exception, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__FailedToGetConnectionIdCallback(logger, sessionId, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ClosingStreamSocket(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__ClosingStreamSocketCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CreatingConnectionAttributes(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__CreatingConnectionAttributesCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ObtainingPasswordViaProvidePasswordCallback(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__ObtainingPasswordViaProvidePasswordCallbackCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void FailedToObtainPassword(ILogger logger, Exception exception, string sessionId, string exceptionMessage)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__FailedToObtainPasswordCallback(logger, sessionId, exceptionMessage, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ConnectedTlsDetailedPreliminary(ILogger logger, string sessionId, SslPolicyErrors sslPolicyErrors, SslProtocols sslProtocol, CipherAlgorithmType cipherAlgorithm, HashAlgorithmType hashAlgorithm, ExchangeAlgorithmType keyExchangeAlgorithm, int keyExchangeStrength)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			logger.Log(LogLevel.Debug, new EventId(2157, "ConnectedTlsDetailedPreliminary"), new __ConnectedTlsDetailedPreliminaryStruct(sessionId, sslPolicyErrors, sslProtocol, cipherAlgorithm, hashAlgorithm, keyExchangeAlgorithm, keyExchangeStrength), null, __ConnectedTlsDetailedPreliminaryStruct.Format);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CertificateErrorUnixSocket(ILogger logger, string sessionId, SslPolicyErrors sslPolicyErrors)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__CertificateErrorUnixSocketCallback(logger, sessionId, sslPolicyErrors, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CertificateErrorNoPassword(ILogger logger, string sessionId, SslPolicyErrors sslPolicyErrors)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__CertificateErrorNoPasswordCallback(logger, sessionId, sslPolicyErrors, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CertificateErrorValidThumbprint(ILogger logger, string sessionId, SslPolicyErrors sslPolicyErrors)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__CertificateErrorValidThumbprintCallback(logger, sessionId, sslPolicyErrors, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void IgnoringCancellationForCommand(ILogger logger, int commandId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__IgnoringCancellationForCommandCallback(logger, commandId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CommandHasBeenCanceled(ILogger logger, int commandId, string sessionId, string cancellationSource)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__CommandHasBeenCanceledCallback(logger, commandId, sessionId, cancellationSource, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void IgnoringCancellationForClosedConnection(ILogger logger, Exception exception, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__IgnoringCancellationForClosedConnectionCallback(logger, sessionId, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CancelingCommandFailed(ILogger logger, Exception exception, string sessionId, int commandId)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__CancelingCommandFailedCallback(logger, sessionId, commandId, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void WillCancelCommand(ILogger logger, string sessionId, int commandId, int cancelAttemptCount, string? commandText)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__WillCancelCommandCallback(logger, sessionId, commandId, cancelAttemptCount, commandText, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CancelingCommandFromSession(ILogger logger, string sessionId, int commandId, string cancelingSessionId, string? commandText)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__CancelingCommandFromSessionCallback(logger, sessionId, commandId, cancelingSessionId, commandText, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void IgnoringCancellationForInactiveCommand(ILogger logger, string sessionId, int activeCommandId, int commandId)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__IgnoringCancellationForInactiveCommandCallback(logger, sessionId, activeCommandId, commandId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CancelingCommand(ILogger logger, string sessionId, int commandId, string commandText)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__CancelingCommandCallback(logger, sessionId, commandId, commandText, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void SendingSleepToClearPendingCancellation(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__SendingSleepToClearPendingCancellationCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void GettingCachedProcedure(ILogger logger, string sessionId, string procedureName)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__GettingCachedProcedureCallback(logger, sessionId, procedureName, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void PoolDoesNotHaveSharedProcedureCache(ILogger logger, string sessionId, int? poolId)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__PoolDoesNotHaveSharedProcedureCacheCallback(logger, sessionId, poolId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CouldNotNormalizeDatabaseAndName(ILogger logger, string sessionId, string procedureName, string database)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__CouldNotNormalizeDatabaseAndNameCallback(logger, sessionId, procedureName, database, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void FailedToCacheProcedure(ILogger logger, string sessionId, string schema, string component)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__FailedToCacheProcedureCallback(logger, sessionId, schema, component, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CachingProcedure(ILogger logger, string sessionId, string schema, string component)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__CachingProcedureCallback(logger, sessionId, schema, component, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ProcedureCacheCount(ILogger logger, string sessionId, int procedureCacheCount)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__ProcedureCacheCountCallback(logger, sessionId, procedureCacheCount, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void DidNotFindCachedProcedure(ILogger logger, string sessionId, string schema, string component)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__DidNotFindCachedProcedureCallback(logger, sessionId, schema, component, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ReturningCachedProcedure(ILogger logger, string sessionId, string schema, string component)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__ReturningCachedProcedureCallback(logger, sessionId, schema, component, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void FailedToRetrieveProcedureMetadata(ILogger logger, Exception exception, string sessionId, string schema, string component, string exceptionMessage)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__FailedToRetrieveProcedureMetadataCallback(logger, sessionId, schema, component, exceptionMessage, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ServerDoesNotSupportCachedProcedures(ILogger logger, string sessionId, string serverVersion)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__ServerDoesNotSupportCachedProceduresCallback(logger, sessionId, serverVersion, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ProcedureHasRoutineCount(ILogger logger, string schema, string component, int routineCount, int parameterCount)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__ProcedureHasRoutineCountCallback(logger, schema, component, routineCount, parameterCount, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CreatedNewSession(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__CreatedNewSessionCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void PingingServer(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__PingingServerCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void SuccessfullyPingedServer(ILogger logger, LogLevel logLevel, string sessionId)
	{
		if (logger.IsEnabled(logLevel))
		{
			logger.Log(logLevel, new EventId(2501, "SuccessfullyPingedServer"), new __SuccessfullyPingedServerStruct(sessionId), null, __SuccessfullyPingedServerStruct.Format);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void PingFailed(ILogger logger, Exception exception, string sessionId, string failure)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__PingFailedCallback(logger, sessionId, failure, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void SettingStateToFailed(ILogger logger, Exception exception, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__SettingStateToFailedCallback(logger, sessionId, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ExpectedToReadMoreBytes(ILogger logger, string sessionId, int expectedByteCount, int readByteCount)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__ExpectedToReadMoreBytesCallback(logger, sessionId, expectedByteCount, readByteCount, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ErrorPayload(ILogger logger, string sessionId, int errorCode, string state, string message)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__ErrorPayloadCallback(logger, sessionId, errorCode, state, message, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CommandExecutorExecuteReader(ILogger logger, string sessionId, IOBehavior ioBehavior, int commandCount)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__CommandExecutorExecuteReaderCallback(logger, sessionId, ioBehavior, commandCount, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void QueryWasInterrupted(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__QueryWasInterruptedCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void PreparingCommandPayload(ILogger logger, string sessionId, string commandText)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__PreparingCommandPayloadCallback(logger, sessionId, commandText, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void PreparingCommandPayloadWithId(ILogger logger, string sessionId, int statementId, string commandText)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__PreparingCommandPayloadWithIdCallback(logger, sessionId, statementId, commandText, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void QueryAttributesNotSupported(ILogger logger, string sessionId, string commandText)
	{
		if (logger.IsEnabled(LogLevel.Warning))
		{
			__QueryAttributesNotSupportedCallback(logger, sessionId, commandText, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void QueryAttributesNotSupportedWithId(ILogger logger, string sessionId, int statementId)
	{
		if (logger.IsEnabled(LogLevel.Warning))
		{
			__QueryAttributesNotSupportedWithIdCallback(logger, sessionId, statementId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void IgnoringExceptionInDisposeAsync(ILogger logger, Exception exception, string sessionId, string exceptionMessage, string commandText)
	{
		if (logger.IsEnabled(LogLevel.Warning))
		{
			__IgnoringExceptionInDisposeAsyncCallback(logger, sessionId, exceptionMessage, commandText, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void StartingBulkCopy(ILogger logger, string tableName)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__StartingBulkCopyCallback(logger, tableName, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void AddingDefaultColumnMapping(ILogger logger, int sourceOrdinal, string destinationColumn)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__AddingDefaultColumnMappingCallback(logger, sourceOrdinal, destinationColumn, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void IgnoringColumn(ILogger logger, int sourceOrdinal)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__IgnoringColumnCallback(logger, sourceOrdinal, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void FinishedBulkCopy(ILogger logger, string tableName)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__FinishedBulkCopyCallback(logger, tableName, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void BulkCopyFailed(ILogger logger, string tableName, int rowsCopied, int rowsInserted)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__BulkCopyFailedCallback(logger, tableName, rowsCopied, rowsInserted, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ColumnMappingAlreadyHasExpression(ILogger logger, int sourceOrdinal, string destinationColumn, string expression)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__ColumnMappingAlreadyHasExpressionCallback(logger, sourceOrdinal, destinationColumn, expression, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void SettingExpressionToMapColumn(ILogger logger, int sourceOrdinal, string destinationColumn, string expression)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__SettingExpressionToMapColumnCallback(logger, sourceOrdinal, destinationColumn, expression, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void StartingTransaction(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__StartingTransactionCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void StartedTransaction(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__StartedTransactionCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CommittingTransaction(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__CommittingTransactionCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CommittedTransaction(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__CommittedTransactionCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void RollingBackTransaction(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__RollingBackTransactionCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void RolledBackTransaction(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__RolledBackTransactionCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void WaitingForAvailableSession(ILogger logger, int poolId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__WaitingForAvailableSessionCallback(logger, poolId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void FailedInReceiveReplyAsync(ILogger logger, Exception exception, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__FailedInReceiveReplyAsyncCallback(logger, sessionId, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void FailedInSendReplyAsync(ILogger logger, Exception exception, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__FailedInSendReplyAsyncCallback(logger, sessionId, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CreatingNewConnectionPool(ILogger logger, int poolId, string connectionString)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__CreatingNewConnectionPoolCallback(logger, poolId, connectionString, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ScanningForLeakedSessions(ILogger logger, int poolId)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__ScanningForLeakedSessionsCallback(logger, poolId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void FoundExistingSession(ILogger logger, int poolId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__FoundExistingSessionCallback(logger, poolId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void DiscardingSessionDueToWrongGeneration(ILogger logger, int poolId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__DiscardingSessionDueToWrongGenerationCallback(logger, poolId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void SessionIsUnusable(ILogger logger, int poolId, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__SessionIsUnusableCallback(logger, poolId, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ReturningPooledSession(ILogger logger, int poolId, string sessionId, int leasedSessionCount)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__ReturningPooledSessionCallback(logger, poolId, sessionId, leasedSessionCount, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ReturningNewSession(ILogger logger, int poolId, string sessionId, int leasedSessionCount)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__ReturningNewSessionCallback(logger, poolId, sessionId, leasedSessionCount, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void DisposingCreatedSessionDueToException(ILogger logger, Exception exception, int poolId, string sessionId, string exceptionMessage)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__DisposingCreatedSessionDueToExceptionCallback(logger, poolId, sessionId, exceptionMessage, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void UnexpectedErrorInGetSessionAsync(ILogger logger, Exception exception, int poolId, string exceptionMessage)
	{
		if (logger.IsEnabled(LogLevel.Warning))
		{
			__UnexpectedErrorInGetSessionAsyncCallback(logger, poolId, exceptionMessage, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ReceivingSessionBack(ILogger logger, int poolId, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__ReceivingSessionBackCallback(logger, poolId, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ReceivedInvalidSession(ILogger logger, int poolId, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__ReceivedInvalidSessionCallback(logger, poolId, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ReceivedExpiredSession(ILogger logger, int poolId, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__ReceivedExpiredSessionCallback(logger, poolId, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ClearingConnectionPool(ILogger logger, int poolId)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__ClearingConnectionPoolCallback(logger, poolId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void DisposingConnectionPool(ILogger logger, int poolId)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__DisposingConnectionPoolCallback(logger, poolId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void RecoveredNoSessions(ILogger logger, int poolId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__RecoveredNoSessionsCallback(logger, poolId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void RecoveredSessionCount(ILogger logger, int poolId, int sessionCount)
	{
		if (logger.IsEnabled(LogLevel.Warning))
		{
			__RecoveredSessionCountCallback(logger, poolId, sessionCount, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void FoundSessionToCleanUp(ILogger logger, int poolId, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__FoundSessionToCleanUpCallback(logger, poolId, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void HasServerRedirectionHeader(ILogger logger, string sessionId, string header)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__HasServerRedirectionHeaderCallback(logger, sessionId, header, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ServerRedirectionIsDisabled(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__ServerRedirectionIsDisabledCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void OpeningNewConnection(ILogger logger, string sessionId, string host, int port, string user)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__OpeningNewConnectionCallback(logger, sessionId, host, port, user, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void FailedToConnectRedirectedSession(ILogger logger, Exception ex, string sessionId, string redirectedSessionId)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__FailedToConnectRedirectedSessionCallback(logger, sessionId, redirectedSessionId, ex);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ClosingSessionToUseRedirectedSession(ILogger logger, string sessionId, string redirectedSessionId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__ClosingSessionToUseRedirectedSessionCallback(logger, sessionId, redirectedSessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void SessionAlreadyConnectedToServer(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__SessionAlreadyConnectedToServerCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void RequiresServerRedirection(ILogger logger, string sessionId)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__RequiresServerRedirectionCallback(logger, sessionId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CreatedPoolWillNotBeUsed(ILogger logger, int poolId)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__CreatedPoolWillNotBeUsedCallback(logger, poolId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ReapingConnectionPool(ILogger logger, int poolId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__ReapingConnectionPoolCallback(logger, poolId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void CheckingForDnsChanges(ILogger logger, int poolId)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__CheckingForDnsChangesCallback(logger, poolId, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void DetectedDnsChange(ILogger logger, int poolId, string hostName, string oldAddresses, string newAddresses)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__DetectedDnsChangeCallback(logger, poolId, hostName, oldAddresses, newAddresses, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void DnsCheckFailed(ILogger logger, Exception exception, int poolId, string hostName, string exceptionMessage)
	{
		if (logger.IsEnabled(LogLevel.Debug))
		{
			__DnsCheckFailedCallback(logger, poolId, hostName, exceptionMessage, exception);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	public static void ClearingPoolDueToDnsChanges(ILogger logger, int poolId)
	{
		if (logger.IsEnabled(LogLevel.Information))
		{
			__ClearingPoolDueToDnsChangesCallback(logger, poolId, null);
		}
	}
}
