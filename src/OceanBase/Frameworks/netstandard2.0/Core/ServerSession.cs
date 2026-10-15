using System;
using System.Buffers.Text;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OceanBase.Authentication;
using OceanBase.ColumnReaders;
using OceanBase.Logging;
using OceanBase.Protocol;
using OceanBase.Protocol.Payloads;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;

namespace OceanBase.Core;

internal sealed class ServerSession : IServerCapabilities
{
	internal sealed class SslClientAuthenticationOptions
	{
		public X509RevocationMode CertificateRevocationCheckMode { get; set; }

		public X509CertificateCollection? ClientCertificates { get; set; }

		public SslProtocols EnabledSslProtocols { get; set; }

		public string? TargetHost { get; set; }
	}

	private enum State
	{
		Created,
		Connecting,
		Connected,
		Querying,
		CancelingQuery,
		ClearingPendingCancellation,
		Closing,
		Closed,
		Failed
	}

	private sealed class DelimiterSqlParser(IMySqlCommand command) : SqlParser(new StatementPreparer(command.CommandText, null, command.CreateStatementPreparerOptions()))
	{
		public bool HasDelimiter { get; private set; }

		private string Sql { get; } = command.CommandText;

		protected override void OnStatementBegin(int index)
		{
			if (index + 10 < Sql.Length && Sql.AsSpan(index, 10).Equals("delimiter ".AsSpan(), StringComparison.OrdinalIgnoreCase))
			{
				HasDelimiter = true;
			}
		}
	}

	private sealed class DelimiterSqlParser2(string commandText, StatementPreparerOptions options) : SqlParser(new StatementPreparer(commandText, null, options))
	{
		public bool HasDelimiter { get; private set; }

		private string Sql { get; } = commandText;

		protected override void OnStatementBegin(int index)
		{
			if (index + 10 < Sql.Length && Sql.AsSpan(index, 10).Equals("delimiter ".AsSpan(), StringComparison.OrdinalIgnoreCase))
			{
				HasDelimiter = true;
			}
		}
	}

	/// <summary> This API supports the logging infrastructure and is not intended to be used directly from your code. It is subject to change in the future. </summary>
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	private readonly struct __ExpectedSessionState6Struct(string sessionId, State expectedState1, State expectedState2, State expectedState3, State expectedState4, State expectedState5, State expectedState6, State sessionState) : IReadOnlyList<KeyValuePair<string, object?>>, IEnumerable<KeyValuePair<string, object?>>, IEnumerable, IReadOnlyCollection<KeyValuePair<string, object?>>
	{
		private readonly string _sessionId = sessionId;

		private readonly State _expectedState1 = expectedState1;

		private readonly State _expectedState2 = expectedState2;

		private readonly State _expectedState3 = expectedState3;

		private readonly State _expectedState4 = expectedState4;

		private readonly State _expectedState5 = expectedState5;

		private readonly State _expectedState6 = expectedState6;

		private readonly State _sessionState = sessionState;

		public static readonly Func<__ExpectedSessionState6Struct, Exception?, string> Format = (__ExpectedSessionState6Struct state, Exception? ex) => state.ToString();

		public int Count => 9;

		public KeyValuePair<string, object?> this[int index] => index switch
		{
			0 => new KeyValuePair<string, object>("SessionId", _sessionId), 
			1 => new KeyValuePair<string, object>("ExpectedState1", _expectedState1), 
			2 => new KeyValuePair<string, object>("ExpectedState2", _expectedState2), 
			3 => new KeyValuePair<string, object>("ExpectedState3", _expectedState3), 
			4 => new KeyValuePair<string, object>("ExpectedState4", _expectedState4), 
			5 => new KeyValuePair<string, object>("ExpectedState5", _expectedState5), 
			6 => new KeyValuePair<string, object>("ExpectedState6", _expectedState6), 
			7 => new KeyValuePair<string, object>("SessionState", _sessionState), 
			8 => new KeyValuePair<string, object>("{OriginalFormat}", "Session {SessionId} should have state {ExpectedState1} or {ExpectedState2} or {ExpectedState3} or {ExpectedState4} or {ExpectedState5} or {ExpectedState6} but was {SessionState}"), 
			_ => throw new IndexOutOfRangeException("index"), 
		};

		public override string ToString()
		{
			string sessionId = _sessionId;
			State expectedState = _expectedState1;
			State expectedState2 = _expectedState2;
			State expectedState3 = _expectedState3;
			State expectedState4 = _expectedState4;
			State expectedState5 = _expectedState5;
			State expectedState6 = _expectedState6;
			State sessionState = _sessionState;
			return $"Session {sessionId} should have state {expectedState} or {expectedState2} or {expectedState3} or {expectedState4} or {expectedState5} or {expectedState6} but was {sessionState}";
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

	private static readonly PayloadData s_setNamesUtf8NoAttributesPayload = QueryPayload.Create(supportsQueryAttributes: false, "SET NAMES utf8;"u8);

	private static readonly PayloadData s_setNamesUtf8mb4NoAttributesPayload = QueryPayload.Create(supportsQueryAttributes: false, "SET NAMES utf8mb4;"u8);

	private static readonly PayloadData s_setNamesUtf8WithAttributesPayload = QueryPayload.Create(supportsQueryAttributes: true, "SET NAMES utf8;"u8);

	private static readonly PayloadData s_setNamesUtf8mb4WithAttributesPayload = QueryPayload.Create(supportsQueryAttributes: true, "SET NAMES utf8mb4;"u8);

	private static readonly PayloadData s_setAutocommit1NoAttributesPayload = QueryPayload.Create(supportsQueryAttributes: false, "SET autocommit=1;"u8);

	private static readonly PayloadData s_setAutocommit1WithAttributesPayload = QueryPayload.Create(supportsQueryAttributes: true, "SET autocommit=1;"u8);

	private static readonly PayloadData s_setAutocommit0NoAttributesPayload = QueryPayload.Create(supportsQueryAttributes: false, "SET autocommit=0;"u8);

	private static readonly PayloadData s_setAutocommit0WithAttributesPayload = QueryPayload.Create(supportsQueryAttributes: true, "SET autocommit=0;"u8);

	private static readonly PayloadData s_sleepNoAttributesPayload = QueryPayload.Create(supportsQueryAttributes: false, "SELECT SLEEP(0) INTO @__MySqlConnector__Sleep;"u8);

	private static readonly PayloadData s_sleepWithAttributesPayload = QueryPayload.Create(supportsQueryAttributes: true, "SELECT SLEEP(0) INTO @__MySqlConnector__Sleep;"u8);

	private static readonly PayloadData s_oracleClearPendingCancellationNoAttributesPayload = QueryPayload.Create(supportsQueryAttributes: false, "SELECT 1 FROM DUAL"u8);

	private static readonly PayloadData s_oracleClearPendingCancellationWithAttributesPayload = QueryPayload.Create(supportsQueryAttributes: true, "SELECT 1 FROM DUAL"u8);

	private static readonly PayloadData s_selectConnectionIdVersionNoAttributesPayload = QueryPayload.Create(supportsQueryAttributes: false, "SELECT CONNECTION_ID(), VERSION();"u8);

	private static readonly PayloadData s_selectConnectionIdVersionWithAttributesPayload = QueryPayload.Create(supportsQueryAttributes: true, "SELECT CONNECTION_ID(), VERSION();"u8);

	private static readonly PayloadData s_selectConnectionIdVersionOracleNoAttributesPayload = QueryPayload.Create(supportsQueryAttributes: false, "SELECT SYS_CONTEXT('USERENV', 'SESSIONID') AS connection_id, ob_version() AS version FROM DUAL"u8);

	private static readonly PayloadData s_selectConnectionIdVersionOracleWithAttributesPayload = QueryPayload.Create(supportsQueryAttributes: true, "SELECT SYS_CONTEXT('USERENV', 'SESSIONID') AS connection_id, ob_version() AS version FROM DUAL"u8);

	private static readonly PayloadData s_selectNlsNcharCharacterSetNoAttributesPayload = QueryPayload.Create(supportsQueryAttributes: false, "select @@NLS_NCHAR_CHARACTERSET from dual"u8);

	private static readonly PayloadData s_selectNlsNcharCharacterSetWithAttributesPayload = QueryPayload.Create(supportsQueryAttributes: true, "select @@NLS_NCHAR_CHARACTERSET from dual"u8);

	private static readonly PayloadData s_setNcharacterSetConnectionUtf8NoAttributesPayload = QueryPayload.Create(supportsQueryAttributes: false, "set NCHARACTER_SET_CONNECTION = 'UTF8'"u8);

	private static readonly PayloadData s_setNcharacterSetConnectionUtf8WithAttributesPayload = QueryPayload.Create(supportsQueryAttributes: true, "set NCHARACTER_SET_CONNECTION = 'UTF8'"u8);

	private static readonly PayloadData s_setNcharacterSetConnectionUtf16NoAttributesPayload = QueryPayload.Create(supportsQueryAttributes: false, "set NCHARACTER_SET_CONNECTION = 'UTF16'"u8);

	private static readonly PayloadData s_setNcharacterSetConnectionUtf16WithAttributesPayload = QueryPayload.Create(supportsQueryAttributes: true, "set NCHARACTER_SET_CONNECTION = 'UTF16'"u8);

	private readonly ILogger m_logger;

	private readonly object m_lock;

	private readonly ArraySegmentHolder<byte> m_payloadCache;

	private readonly ActivityTagsCollection m_activityTags;

	private State m_state;

	private TcpClient? m_tcpClient;

	private Socket? m_socket;

	private Stream? m_stream;

	private SslStream? m_sslStream;

	private X509Certificate2? m_clientCertificate;

	private IPayloadHandler? m_payloadHandler;

	private CompressionMethod m_compressionMethod;

	private bool m_isSecureConnection;

	private bool m_isLoopbackConnection;

	private bool m_supportsConnectionAttributes;

	private bool m_supportsPipelining;

	private bool m_enableOraclePrepareExecute;

	private CharacterSet m_characterSet;

	private PayloadData m_setNamesPayload;

	private byte[]? m_pipelinedResetConnectionBytes;

	private Dictionary<string, PreparedStatements>? m_preparedStatements;

	private byte[]? m_passwordHash;

	private byte[]? m_remoteCertificateSha2Thumbprint;

	private SslPolicyErrors m_sslPolicyErrors;

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, State, Exception?> __CannotExecuteNewCommandInStateCallback = LoggerMessage.Define<string, State>(LogLevel.Error, new EventId(2200, "CannotExecuteNewCommandInState"), "Session {SessionId} can't execute new command when in state {SessionState}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, State, Exception?> __EnteringFinishQueryingCallback = LoggerMessage.Define<string, State>(LogLevel.Trace, new EventId(2201, "EnteringFinishQuerying"), "Session {SessionId} entering FinishQuerying; state is {SessionState}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static readonly Action<ILogger, string, State, State, Exception?> __ExpectedSessionState1Callback = LoggerMessage.Define<string, State, State>(LogLevel.Error, new EventId(2011, "ExpectedSessionState1"), "Session {SessionId} should have state {ExpectedState1} but was {SessionState}", new LogDefineOptions
	{
		SkipEnabledCheck = true
	});

	public string Id { get; }

	public ServerVersion ServerVersion { get; set; }

	public ServerVersion OceanBaseServerVersion { get; set; }

	public bool SupportsPerQueryVariables
	{
		get
		{
			if (ServerVersion.IsMariaDb)
			{
				return ServerVersion.Version >= ServerVersions.MariaDbSupportsPerQueryVariables;
			}
			return false;
		}
	}

	public int ActiveCommandId { get; private set; }

	public int CancellationTimeout { get; private set; }

	public int ConnectionId { get; set; }

	public byte[]? AuthPluginData { get; set; }

	public long CreatedTimestamp { get; }

	public ConnectionPool? Pool { get; }

	public int PoolGeneration { get; }

	public long LastLeasedTimestamp { get; set; }

	public long LastReturnedTimestamp { get; private set; }

	public string? DatabaseOverride { get; set; }

	public string HostName { get; private set; }

	public IPEndPoint? IPEndPoint => m_tcpClient?.Client.RemoteEndPoint as IPEndPoint;

	public string? UserID { get; private set; }

	public WeakReference<IServerSessionOwner>? OwningConnection { get; set; }

	public bool SupportsDeprecateEof { get; private set; }

	public bool SupportsCachedPreparedMetadata { get; private set; }

	public bool SupportsQueryAttributes { get; private set; }

	public bool SupportsSessionTrack { get; private set; }

	public bool SupportsProtocol41 { get; private set; }

	public bool SupportsTransactions { get; private set; }

	public bool UseOracleMode { get; set; }

	public bool UseOceanBaseProtocolV2 { get; set; }

	public bool EnableFullLinkTrace { get; set; }

	public bool UseNewExtraInfo { get; set; }

	public bool UseNewResultSetMetadata { get; set; }

	public bool ProcAccessDenied { get; set; }

	public bool UseLobLocatorV2 { get; set; }

	public bool UseOraclePrepareExecute
	{
		get
		{
			if (UseOracleMode && m_enableOraclePrepareExecute)
			{
				return OceanBaseServerVersion.Version >= ServerVersions.OceanbaseSupportsPrepareExecute;
			}
			return false;
		}
	}

	public bool UseOracleServerParameterMetadata { get; private set; } = true;

	/// <summary>
	/// Returns true if the server supports accurate parameter in/out info in prepare_execute response.
	/// Beginning with versions 4.2.5.6, 4.3.5.5, 4.4.2 and 4.5.1 the server provides support for including
	/// precise 'inout parameter information in the prepare_execute response.
	/// </summary>
	public bool SupportsAccurateParameterInfo
	{
		get
		{
			if (UseOracleMode)
			{
				if ((!(OceanBaseServerVersion.Version >= ServerVersions.OceanbaseSupportsAccurateParameterInfo_4_2) || !(OceanBaseServerVersion.Version < new Version(4, 3))) && (!(OceanBaseServerVersion.Version >= ServerVersions.OceanbaseSupportsAccurateParameterInfo_4_3) || !(OceanBaseServerVersion.Version < new Version(4, 4))) && (!(OceanBaseServerVersion.Version >= ServerVersions.OceanbaseSupportsAccurateParameterInfo_4_4) || !(OceanBaseServerVersion.Version < new Version(4, 5))))
				{
					return OceanBaseServerVersion.Version >= ServerVersions.OceanbaseSupportsAccurateParameterInfo_4_5;
				}
				return true;
			}
			return false;
		}
	}

	public bool UseArrayBinding { get; private set; }

	public int PrepareExecuteFetchSize { get; private set; } = 10;

	public bool OracleAutoCommit { get; private set; } = true;

	/// <summary>
	/// When true, ignores packet sequence number validation for Oracle Prepare-Execute protocol.
	/// This is needed for OceanBase 4.x servers that send both the prepare-execute header and trailing OK
	/// with the same MySQL packet sequence number (1).
	/// </summary>
	public bool IgnorePrepareExecuteSequenceCheck { get; set; }

	public ICollection<KeyValuePair<string, object?>> ActivityTags => (ICollection<KeyValuePair<string, object?>>)m_activityTags;

	public MySqlDataReader DataReader { get; set; }

	public OracleDataReader OracleDataReader { get; set; }

	public ConnectionOpenedConditions Conditions { get; private set; }

	/// <summary>
	/// The .NET encoding used for Oracle-mode NCHAR/NVARCHAR2 data when <c>NCHARACTER_SET_CONNECTION</c> is configured.
	/// This is <c>null</c> for non-Oracle sessions or when initialization failed.
	/// </summary>
	internal Encoding? NCharacterEncoding { get; private set; }

	/// <summary>
	/// The server-side value assigned to <c>NCHARACTER_SET_CONNECTION</c> (e.g., <c>UTF8</c> or <c>UTF16</c>) for Oracle-mode sessions.
	/// This is <c>null</c> for non-Oracle sessions or when initialization failed.
	/// </summary>
	internal string? NCharacterSetConnection { get; private set; }

	/// <summary>
	/// The .NET encoding used for VARCHAR2/CHAR data (session character encoding).
	/// This is derived from the <c>CharacterSet</c> connection-string option and applied via <c>SET NAMES</c>.
	/// Defaults to <see cref="P:System.Text.Encoding.UTF8" /> when not explicitly configured.
	/// </summary>
	internal Encoding? CharacterEncoding { get; private set; }

	/// <summary>
	/// A cached <see cref="T:OceanBase.ColumnReaders.StringColumnReader" /> for the session's <see cref="P:OceanBase.Core.ServerSession.CharacterEncoding" />.
	/// Avoids allocating a new reader per column when a non-UTF-8 charset is in use.
	/// Returns <c>null</c> when the encoding is UTF-8 (callers should use <see cref="P:OceanBase.ColumnReaders.StringColumnReader.Instance" />).
	/// </summary>
	internal StringColumnReader? CachedStringColumnReader { get; private set; }

	public bool IsConnected
	{
		get
		{
			lock (m_lock)
			{
				return m_state == State.Connected;
			}
		}
	}

	public bool IsCancelingQuery => m_state == State.CancelingQuery;

	internal bool SslIsEncrypted => m_sslStream?.IsEncrypted ?? false;

	internal bool SslIsSigned => m_sslStream?.IsSigned ?? false;

	internal bool SslIsAuthenticated => m_sslStream?.IsAuthenticated ?? false;

	internal bool SslIsMutuallyAuthenticated => m_sslStream?.IsMutuallyAuthenticated ?? false;

	internal SslProtocols SslProtocol => m_sslStream?.SslProtocol ?? SslProtocols.None;

	public ServerSession(ILogger logger, IConnectionPoolMetadata pool)
	{
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Expected O, but got Unknown
		m_logger = logger;
		m_lock = new object();
		m_payloadCache = new ArraySegmentHolder<byte>();
		Id = pool.Id + "." + pool.GetNewSessionId();
		ServerVersion = OceanBase.Core.ServerVersion.Empty;
		OceanBaseServerVersion = OceanBase.Core.ServerVersion.Empty;
		CreatedTimestamp = Stopwatch.GetTimestamp();
		Pool = pool.ConnectionPool;
		PoolGeneration = pool.Generation;
		HostName = "";
		m_activityTags = new ActivityTagsCollection();
		DataReader = new MySqlDataReader();
		OracleDataReader = new OracleDataReader();
		Log.CreatedNewSession(m_logger, Id);
	}

	public ValueTask ReturnToPoolAsync(IOBehavior ioBehavior, IServerSessionOwner? owningConnection)
	{
		Log.ReturningToPool(m_logger, Id, Pool?.Id ?? 0);
		Conditions = ConnectionOpenedConditions.None;
		LastReturnedTimestamp = Stopwatch.GetTimestamp();
		if (Pool == null)
		{
			return default(ValueTask);
		}
		byte[]? array = m_payloadCache.Array;
		if (array != null && array.Length > 16777216)
		{
			m_payloadCache.ArraySegment = default(ArraySegment<byte>);
		}
		MetricsReporter.RecordUseTime(Pool, Utility.GetElapsedSeconds(LastLeasedTimestamp, LastReturnedTimestamp));
		LastLeasedTimestamp = 0L;
		return Pool.ReturnAsync(ioBehavior, this);
	}

	public bool TryStartCancel(ICancellableCommand command)
	{
		lock (m_lock)
		{
			if (ActiveCommandId != command.CommandId)
			{
				return false;
			}
			VerifyState(State.Querying, State.CancelingQuery, State.ClearingPendingCancellation, State.Closing, State.Closed, State.Failed);
			if (m_state != State.Querying)
			{
				return false;
			}
			if (command.CancelAttemptCount++ >= 10)
			{
				return false;
			}
			m_state = State.CancelingQuery;
		}
		Log.WillCancelCommand(m_logger, Id, command.CommandId, command.CancelAttemptCount, (command as MySqlCommand)?.CommandText);
		return true;
	}

	public void DoCancel(ICancellableCommand commandToCancel, DbCommand killCommand)
	{
		ServerSession sessionFromConnection = GetSessionFromConnection(killCommand.Connection);
		Log.CancelingCommandFromSession(m_logger, Id, commandToCancel.CommandId, sessionFromConnection.Id, (commandToCancel as MySqlCommand)?.CommandText);
		lock (m_lock)
		{
			if (ActiveCommandId != commandToCancel.CommandId)
			{
				Log.IgnoringCancellationForInactiveCommand(m_logger, Id, ActiveCommandId, commandToCancel.CommandId);
				return;
			}
			Log.CancelingCommand(m_logger, sessionFromConnection.Id, commandToCancel.CommandId, killCommand.CommandText);
			killCommand.ExecuteNonQuery();
		}
	}

	private static ServerSession GetSessionFromConnection(DbConnection connection)
	{
		if (!(connection is MySqlConnection mySqlConnection))
		{
			if (connection is OracleConnection oracleConnection)
			{
				return oracleConnection.Session;
			}
			throw new InvalidOperationException("Unsupported connection type: " + connection.GetType().Name);
		}
		return mySqlConnection.Session;
	}

	public void AbortCancel(ICancellableCommand command)
	{
		lock (m_lock)
		{
			if (ActiveCommandId == command.CommandId && m_state == State.CancelingQuery)
			{
				m_state = State.Querying;
			}
		}
	}

	public Task PrepareAsync(IMySqlCommand command, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		return PrepareAsync(new CommandFacade(command), ioBehavior, cancellationToken);
	}

	public Task PrepareAsync(IOracleCommand command, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		return PrepareAsync(new CommandFacade(command), ioBehavior, cancellationToken);
	}

	public async Task PrepareAsync(CommandFacade command, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (UseOraclePrepareExecute)
		{
			PrepareLocallyForOraclePrepareExecute(command);
			return;
		}
		string commandText = command.CommandText;
		string commandText2;
		if (command.CommandType == CommandType.StoredProcedure)
		{
			ConnectionFacade connection = command.Connection ?? throw new InvalidOperationException("Command.Connection is null.");
			CachedProcedure obj = await connection.GetCachedProcedure(commandText, revalidateMissing: false, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			int num = obj?.Parameters.Count ?? 0;
			int num2 = command.RawParameters?.Count ?? 0;
			if (UseOracleMode && commandText.AsSpan().Contains("DBMS_LOB.".AsSpan(), StringComparison.OrdinalIgnoreCase) && num2 > 0 && num != num2)
			{
				num = num2;
			}
			if (obj == null && num == 0)
			{
				NormalizedSchema normalizedSchema = NormalizedSchema.MustNormalize(command.CommandText, connection.Database);
				throw new MySqlException("Procedure or function '" + normalizedSchema.Component + "' cannot be found in database '" + normalizedSchema.Schema + "'.");
			}
			StringBuilder stringBuilder = new StringBuilder("CALL ", commandText.Length + 8 + num * 2);
			stringBuilder.Append(commandText);
			stringBuilder.Append('(');
			for (int i = 0; i < num; i++)
			{
				stringBuilder.Append("?,");
			}
			if (num == 0)
			{
				stringBuilder.Append(')');
			}
			else
			{
				stringBuilder[stringBuilder.Length - 1] = ')';
			}
			stringBuilder.Append(';');
			commandText2 = stringBuilder.ToString();
		}
		else
		{
			commandText2 = commandText;
		}
		StatementPreparer statementPreparer = new StatementPreparer(commandText2, command.RawParameters, command.CreateStatementPreparerOptions(), UseOracleMode, this);
		ParsedStatements parsedStatements = statementPreparer.SplitStatements();
		ResizableArray<byte> columnsAndParameters = new ResizableArray<byte>();
		int columnsAndParametersSize = 0;
		List<PreparedStatement> preparedStatements = new List<PreparedStatement>(parsedStatements.Statements.Count);
		foreach (ParsedStatement statement in parsedStatements.Statements)
		{
			await SendAsync(new PayloadData(statement.StatementBytes), ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			PayloadData payloadData;
			try
			{
				payloadData = await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (MySqlException exception)
			{
				ThrowIfStatementContainsDelimiter(exception, command);
				throw;
			}
			StatementPrepareResponsePayload response = StatementPrepareResponsePayload.Create(payloadData.Span);
			ColumnDefinitionPayload[] parameters = null;
			if (response.ParameterCount > 0)
			{
				parameters = new ColumnDefinitionPayload[response.ParameterCount];
				for (int j = 0; j < response.ParameterCount; j++)
				{
					payloadData = await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					int length = payloadData.Span.Length;
					Utility.Resize(ref columnsAndParameters, columnsAndParametersSize + length);
					payloadData.Span.CopyTo(columnsAndParameters.AsSpan(columnsAndParametersSize));
					ColumnDefinitionPayload.Initialize(ref parameters[j], new ResizableArraySegment<byte>(columnsAndParameters, columnsAndParametersSize, length), UseOracleMode);
					columnsAndParametersSize += length;
				}
				if (!SupportsDeprecateEof)
				{
					payloadData = await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					EofPayload.Create(payloadData.Span);
				}
			}
			ColumnDefinitionPayload[] columns = null;
			if (response.ColumnCount > 0)
			{
				columns = new ColumnDefinitionPayload[response.ColumnCount];
				for (int j = 0; j < response.ColumnCount; j++)
				{
					payloadData = await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					int length2 = payloadData.Span.Length;
					Utility.Resize(ref columnsAndParameters, columnsAndParametersSize + length2);
					payloadData.Span.CopyTo(columnsAndParameters.AsSpan(columnsAndParametersSize));
					ColumnDefinitionPayload.Initialize(ref columns[j], new ResizableArraySegment<byte>(columnsAndParameters, columnsAndParametersSize, length2), UseOracleMode);
					columnsAndParametersSize += length2;
				}
				if (!SupportsDeprecateEof)
				{
					payloadData = await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					EofPayload.Create(payloadData.Span);
				}
			}
			preparedStatements.Add(new PreparedStatement(response.StatementId, statement, columns, parameters));
		}
		if (m_preparedStatements == null)
		{
			m_preparedStatements = new Dictionary<string, PreparedStatements>();
		}
		m_preparedStatements.Add(commandText, new PreparedStatements(preparedStatements, parsedStatements));
	}

	private void PrepareLocallyForOraclePrepareExecute(CommandFacade command)
	{
		string commandText = command.CommandText;
		if (m_preparedStatements == null)
		{
			m_preparedStatements = new Dictionary<string, PreparedStatements>();
		}
		if (m_preparedStatements.ContainsKey(commandText))
		{
			return;
		}
		string commandText2;
		if (command.CommandType == CommandType.StoredProcedure)
		{
			int num = command.RawParameters?.Count ?? 0;
			StringBuilder stringBuilder = new StringBuilder("CALL ", commandText.Length + 8 + num * 2);
			stringBuilder.Append(commandText);
			stringBuilder.Append('(');
			for (int i = 0; i < num; i++)
			{
				stringBuilder.Append("?,");
			}
			if (num == 0)
			{
				stringBuilder.Append(')');
			}
			else
			{
				stringBuilder[stringBuilder.Length - 1] = ')';
			}
			stringBuilder.Append(';');
			commandText2 = stringBuilder.ToString();
		}
		else
		{
			commandText2 = commandText;
		}
		ParsedStatements parsedStatements = new StatementPreparer(commandText2, command.RawParameters, command.CreateStatementPreparerOptions(), true, this).SplitStatements();
		List<PreparedStatement> list = new List<PreparedStatement>(parsedStatements.Statements.Count);
		foreach (ParsedStatement statement in parsedStatements.Statements)
		{
			list.Add(new PreparedStatement(0, statement, null, null));
		}
		m_preparedStatements.Add(commandText, new PreparedStatements(list, parsedStatements));
	}

	public PreparedStatements? TryGetPreparedStatement(string commandText)
	{
		if (m_preparedStatements == null || !m_preparedStatements.TryGetValue(commandText, out PreparedStatements value))
		{
			return null;
		}
		return value;
	}

	internal void TryClosePreparedStatementsNoThrow(string commandText)
	{
		TryClosePreparedStatementsNoThrowAsync(commandText, IOBehavior.Synchronous, CancellationToken.None).AsTask().GetAwaiter().GetResult();
	}

	internal async ValueTask TryClosePreparedStatementsNoThrowAsync(string commandText, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		PreparedStatements preparedStatements;
		lock (m_lock)
		{
			preparedStatements = RemovePreparedStatements(commandText);
		}
		if (preparedStatements == null)
		{
			return;
		}
		try
		{
			foreach (PreparedStatement statement in preparedStatements.Statements)
			{
				await TryCloseStatementNoThrowAsync(statement.StatementId, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		finally
		{
			preparedStatements.Dispose();
		}
	}

	internal void TryCloseStatementNoThrow(int statementId)
	{
		TryCloseStatementNoThrowAsync(statementId, IOBehavior.Synchronous, CancellationToken.None).AsTask().GetAwaiter().GetResult();
	}

	internal async ValueTask TryCloseStatementNoThrowAsync(int statementId, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (statementId == 0)
		{
			return;
		}
		bool entered = false;
		lock (m_lock)
		{
			if (m_state != State.Connected)
			{
				return;
			}
			m_state = State.Querying;
			ActiveCommandId = 0;
			entered = true;
		}
		try
		{
			ByteBufferWriter byteBufferWriter = new ByteBufferWriter();
			byteBufferWriter.Write((byte)25);
			byteBufferWriter.Write(statementId);
			using PayloadData payload = byteBufferWriter.ToPayloadData();
			await SendAsync(payload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch
		{
		}
		finally
		{
			if (entered)
			{
				lock (m_lock)
				{
					if (m_state == State.Querying)
					{
						m_state = State.Connected;
						ActiveCommandId = 0;
					}
				}
			}
		}
	}

	public void StartQuerying(ICancellableCommand command)
	{
		lock (m_lock)
		{
			State state = m_state;
			if ((uint)(state - 3) <= 1u)
			{
				CannotExecuteNewCommandInState(m_logger, Id, m_state);
				throw new InvalidOperationException("This Connection is already in use. See https://mysqlconnector.net/conn-reuse");
			}
			VerifyState(State.Connected);
			m_state = State.Querying;
			command.CancelAttemptCount = 0;
			ActiveCommandId = command.CommandId;
		}
	}

	public void FinishQuerying()
	{
		EnteringFinishQuerying(m_logger, Id, m_state);
		bool flag = false;
		lock (m_lock)
		{
			if (m_state == State.CancelingQuery)
			{
				m_state = State.ClearingPendingCancellation;
				flag = true;
			}
			else
			{
				if (m_state == State.Querying)
				{
					m_state = State.Connected;
				}
				else
				{
					VerifyState(State.Failed);
				}
				ActiveCommandId = 0;
			}
		}
		if (!flag)
		{
			return;
		}
		Log.SendingSleepToClearPendingCancellation(m_logger, Id);
		PayloadData payload = ((!SupportsQueryAttributes) ? (UseOracleMode ? s_oracleClearPendingCancellationNoAttributesPayload : s_sleepNoAttributesPayload) : (UseOracleMode ? s_oracleClearPendingCancellationWithAttributesPayload : s_sleepWithAttributesPayload));
		SendAsync(payload, IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
		try
		{
			payload = ReceiveReplyAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
			if (!UseOracleMode)
			{
				OkPayload.Verify(payload.Span, this);
			}
			else
			{
				ReceiveReplyAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
				if (!SupportsDeprecateEof)
				{
					EofPayload.Create(ReceiveReplyAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult().Span);
				}
				ReceiveReplyAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
				payload = ReceiveReplyAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
				if (OkPayload.IsOk(payload.Span, this))
				{
					OkPayload.Verify(payload.Span, this);
				}
				else
				{
					EofPayload.Create(payload.Span);
				}
			}
		}
		catch (MySqlException ex) when (UseOracleMode && ex.ErrorCode == MySqlErrorCode.QueryInterrupted)
		{
		}
		lock (m_lock)
		{
			VerifyState(State.ClearingPendingCancellation);
			m_state = State.Connected;
			ActiveCommandId = 0;
		}
	}

	public void SetTimeout(int timeoutMilliseconds)
	{
		m_payloadHandler.ByteHandler.RemainingTimeout = timeoutMilliseconds;
	}

	public Activity? StartActivity(string name, string? tagName1 = null, object? tagValue1 = null)
	{
		Activity activity = ActivitySourceHelper.StartActivity(name, (IEnumerable<KeyValuePair<string, object?>>?)m_activityTags);
		if (activity != null && activity.IsAllDataRequested)
		{
			if (DatabaseOverride != null)
			{
				activity.SetTag("db.name", (object)DatabaseOverride);
			}
			if (tagName1 != null)
			{
				activity.SetTag(tagName1, tagValue1);
			}
		}
		return activity;
	}

	public async Task DisposeAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (m_payloadHandler != null)
		{
			State state2;
			lock (m_lock)
			{
				State state = m_state;
				if ((state == State.Connected || state == State.Failed) ? true : false)
				{
					m_state = State.Closing;
				}
				state2 = m_state;
			}
			if (state2 == State.Closing)
			{
				try
				{
					Log.SendingQuitCommand(m_logger, Id);
					m_payloadHandler.StartNewConversation();
					await m_payloadHandler.WritePayloadAsync(QuitPayload.Instance.Memory, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch (IOException)
				{
				}
				catch (NotSupportedException)
				{
				}
				catch (ObjectDisposedException)
				{
				}
				catch (SocketException)
				{
				}
			}
		}
		ClearPreparedStatements();
		ShutdownSocket();
		lock (m_lock)
		{
			m_state = State.Closed;
		}
	}

	private async Task<string?> ConnectAsync(ConnectionSettings cs, IServerSessionOwner connection, long startingTimestamp, ILoadBalancer? loadBalancer, Activity? activity, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		_ = 17;
		try
		{
			lock (m_lock)
			{
				VerifyState(State.Created);
				m_state = State.Connecting;
			}
			if (connection is OracleConnection)
			{
				UseOracleMode = true;
			}
			string connectionString = cs.ConnectionStringBuilder.GetConnectionString(cs.ConnectionStringBuilder.PersistSecurityInfo);
			m_activityTags.Add("db.system", (object)"mysql");
			m_activityTags.Add("db.connection_string", (object)connectionString);
			m_activityTags.Add("db.user", (object)cs.UserID);
			if (cs.Database.Length != 0)
			{
				m_activityTags.Add("db.name", (object)cs.Database);
			}
			if (activity != null && activity.IsAllDataRequested)
			{
				activity.SetTag("db.system", (object)"mysql").SetTag("db.connection_string", (object)connectionString).SetTag("db.user", (object)cs.UserID);
				if (cs.Database.Length != 0)
				{
					activity.SetTag("db.name", (object)cs.Database);
				}
			}
			Conditions = ConnectionOpenedConditions.New;
			if (cs.ConnectionProtocol switch
			{
				MySqlConnectionProtocol.Sockets => (await OpenTcpSocketAsync(cs, loadBalancer ?? throw new ArgumentNullException("loadBalancer"), activity, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) ? 1 : 0, 
				MySqlConnectionProtocol.UnixSocket => (await OpenUnixSocketAsync(cs, activity, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) ? 1 : 0, 
				MySqlConnectionProtocol.Pipe => (await OpenNamedPipeAsync(cs, startingTimestamp, activity, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) ? 1 : 0, 
				_ => 0, 
			} == 0)
			{
				lock (m_lock)
				{
					m_state = State.Failed;
				}
				Log.ConnectingFailed(m_logger, Id);
				throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "Unable to connect to any of the specified MySQL hosts.");
			}
			IByteHandler byteHandler2;
			if (m_socket != null)
			{
				IByteHandler byteHandler = new SocketByteHandler(m_socket);
				byteHandler2 = byteHandler;
			}
			else
			{
				IByteHandler byteHandler = new StreamByteHandler(m_stream);
				byteHandler2 = byteHandler;
			}
			IByteHandler byteHandler3 = byteHandler2;
			if (cs.ConnectionTimeout != 0)
			{
				byteHandler3.RemainingTimeout = Math.Max(1, cs.ConnectionTimeoutMilliseconds - Utility.GetElapsedMilliseconds(startingTimestamp));
			}
			m_payloadHandler = new StandardPayloadHandler(byteHandler3);
			InitialHandshakePayload initialHandshake = InitialHandshakePayload.Create((await ReceiveAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Span);
			string currentAuthenticationMethod = (((initialHandshake.ProtocolCapabilities & ProtocolCapabilities.PluginAuth) != ProtocolCapabilities.None) ? initialHandshake.AuthPluginName : (((initialHandshake.ProtocolCapabilities & ProtocolCapabilities.SecureConnection) == ProtocolCapabilities.None) ? "mysql_old_password" : "mysql_native_password"));
			Log.ServerSentAuthPluginName(m_logger, Id, currentAuthenticationMethod);
			switch (currentAuthenticationMethod)
			{
			default:
				Log.UnsupportedAuthenticationMethod(m_logger, Id, currentAuthenticationMethod);
				throw new NotSupportedException("Authentication method '" + initialHandshake.AuthPluginName + "' is not supported.");
			case "mysql_native_password":
			case "sha256_password":
			case "caching_sha2_password":
			{
				ServerVersion = new ServerVersion(initialHandshake.ServerVersion);
				ConnectionId = initialHandshake.ConnectionId;
				AuthPluginData = initialHandshake.AuthPluginData;
				m_compressionMethod = (cs.UseCompression ? (((initialHandshake.ProtocolCapabilities & ProtocolCapabilities.ZstandardCompressionAlgorithm) != ProtocolCapabilities.None && connection.ZstandardPlugin != null) ? CompressionMethod.Zstandard : (((initialHandshake.ProtocolCapabilities & ProtocolCapabilities.Compress) != ProtocolCapabilities.None) ? CompressionMethod.Zlib : CompressionMethod.None)) : CompressionMethod.None);
				CancellationTimeout = cs.CancellationTimeout;
				UserID = cs.UserID;
				UseLobLocatorV2 = cs.UseLobLocatorV2;
				m_enableOraclePrepareExecute = cs.UseOraclePrepareExecute;
				UseOracleServerParameterMetadata = cs.UseOracleServerParameterMetadata;
				UseArrayBinding = cs.UseArrayBinding;
				PrepareExecuteFetchSize = ((cs.PrepareExecuteFetchSize > 0) ? cs.PrepareExecuteFetchSize : 10);
				IgnorePrepareExecuteSequenceCheck = cs.IgnorePrepareExecuteSequenceCheck;
				string text = ConnectionId.ToString(CultureInfo.InvariantCulture);
				m_activityTags["db.connection_id"] = text;
				if (activity != null && activity.IsAllDataRequested)
				{
					activity.SetTag("db.connection_id", (object)text);
				}
				m_supportsConnectionAttributes = (initialHandshake.ProtocolCapabilities & ProtocolCapabilities.ConnectionAttributes) != 0;
				SupportsDeprecateEof = (initialHandshake.ProtocolCapabilities & ProtocolCapabilities.DeprecateEof) != 0;
				SupportsCachedPreparedMetadata = (initialHandshake.ProtocolCapabilities & ProtocolCapabilities.MariaDbCacheMetadata) != 0;
				SupportsQueryAttributes = false;
				SupportsSessionTrack = (initialHandshake.ProtocolCapabilities & ProtocolCapabilities.SessionTrack) != 0;
				SupportsProtocol41 = (initialHandshake.ProtocolCapabilities & ProtocolCapabilities.Protocol41) != 0;
				SupportsTransactions = (initialHandshake.ProtocolCapabilities & ProtocolCapabilities.Transactions) != 0;
				bool flag = (initialHandshake.ProtocolCapabilities & ProtocolCapabilities.Ssl) != 0;
				m_characterSet = ((ServerVersion.Version >= ServerVersions.SupportsUtf8Mb4) ? CharacterSet.Utf8Mb4GeneralCaseInsensitive : CharacterSet.Utf8Mb3GeneralCaseInsensitive);
				string connectionStringCharset = cs.ConnectionStringBuilder.CharacterSet;
				if (UseOracleMode && !string.IsNullOrWhiteSpace(connectionStringCharset) && !connectionStringCharset.StartsWith("utf8", StringComparison.OrdinalIgnoreCase) && !connectionStringCharset.StartsWith("utf-8", StringComparison.OrdinalIgnoreCase))
				{
					m_setNamesPayload = QueryPayload.Create(SupportsQueryAttributes, Encoding.UTF8.GetBytes("SET NAMES " + connectionStringCharset + ";"));
				}
				else
				{
					m_setNamesPayload = ((!(ServerVersion.Version >= ServerVersions.SupportsUtf8Mb4)) ? (SupportsQueryAttributes ? s_setNamesUtf8WithAttributesPayload : s_setNamesUtf8NoAttributesPayload) : (SupportsQueryAttributes ? s_setNamesUtf8mb4WithAttributesPayload : s_setNamesUtf8mb4NoAttributesPayload));
				}
				if (!cs.Pipelining.HasValue && ServerVersion.Version.Major == 5 && ServerVersion.Version.Minor == 7 && HostName.EndsWith(".rds.amazonaws.com", StringComparison.OrdinalIgnoreCase))
				{
					Log.AutoDetectedAurora57(m_logger, Id, HostName);
					m_supportsPipelining = false;
				}
				else
				{
					m_supportsPipelining = m_compressionMethod == CompressionMethod.None && !((!cs.Pipelining) ?? false);
					if (m_supportsPipelining)
					{
						m_pipelinedResetConnectionBytes = new byte[m_setNamesPayload.Span.Length + 9];
						m_pipelinedResetConnectionBytes[0] = 1;
						m_pipelinedResetConnectionBytes[4] = 31;
						m_pipelinedResetConnectionBytes[5] = (byte)m_setNamesPayload.Span.Length;
						ReadOnlySpan<byte> span = m_setNamesPayload.Span;
						Span<byte> span2 = m_pipelinedResetConnectionBytes.AsSpan();
						span.CopyTo(span2.Slice(9, span2.Length - 9));
					}
				}
				Log.SessionMadeConnection(m_logger, Id, ServerVersion.OriginalString, ConnectionId, m_compressionMethod != CompressionMethod.None, m_supportsConnectionAttributes, SupportsDeprecateEof, SupportsCachedPreparedMetadata, flag, SupportsSessionTrack, m_supportsPipelining, SupportsQueryAttributes);
				if (cs.SslMode != MySqlSslMode.None && (cs.SslMode != MySqlSslMode.Preferred || flag))
				{
					if (!flag)
					{
						Log.ServerDoesNotSupportSsl(m_logger, Id);
						throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "Server does not support SSL");
					}
					await InitSslAsync(initialHandshake.ProtocolCapabilities, cs, connection, cs.TlsVersions, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				if (m_supportsConnectionAttributes && cs.ConnectionAttributes == null)
				{
					cs.ConnectionAttributes = CreateConnectionAttributes(cs.ApplicationName);
				}
				string password = GetPassword(cs, connection);
				bool useCachingSha2 = initialHandshake.AuthPluginName == "caching_sha2_password";
				byte[] authenticationResponse;
				if (useCachingSha2)
				{
					authenticationResponse = AuthenticationUtility.CreateScrambleResponse(Utility.TrimZeroByte(initialHandshake.AuthPluginData.AsSpan()), password);
				}
				else
				{
					AuthenticationUtility.CreateResponseAndPasswordHash(password, initialHandshake.AuthPluginData, out authenticationResponse, out m_passwordHash);
				}
				using (PayloadData handshakeResponsePayload = HandshakeResponse41Payload.Create(initialHandshake, cs, authenticationResponse, m_compressionMethod, connection.ZstandardPlugin?.CompressionLevel, m_characterSet, m_supportsConnectionAttributes ? cs.ConnectionAttributes : null))
				{
					await SendReplyAsync(handshakeResponsePayload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				PayloadData payload = await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				while (payload.HeaderByte == 254)
				{
					payload = await SwitchAuthenticationAsync(cs, password, payload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				if (useCachingSha2 && payload.HeaderByte == 1)
				{
					if (CachingSha2ServerResponsePayload.Create(payload.Span).Succeeded)
					{
						payload = await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					}
					else if (!m_isSecureConnection && password.Length != 0)
					{
						byte[] rsaPublicKey = await GetRsaPublicKeyAsync(currentAuthenticationMethod, cs, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
						payload = await SendEncryptedPasswordAsync(AuthPluginData, rsaPublicKey, password, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					}
					else
					{
						payload = await SendClearPasswordAsync(password, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					}
				}
				OkPayload ok = OkPayload.Create(payload.Span, this);
				if (m_sslPolicyErrors != SslPolicyErrors.None)
				{
					bool flag2 = false;
					if (cs.ConnectionProtocol == MySqlConnectionProtocol.UnixSocket)
					{
						Log.CertificateErrorUnixSocket(m_logger, Id, m_sslPolicyErrors);
						flag2 = true;
					}
					else if (string.IsNullOrEmpty(password))
					{
						Log.CertificateErrorNoPassword(m_logger, Id, m_sslPolicyErrors);
					}
					else if (ValidateFingerprint(ok.StatusInfo, initialHandshake.AuthPluginData.AsSpan(0, 20)))
					{
						Log.CertificateErrorValidThumbprint(m_logger, Id, m_sslPolicyErrors);
						flag2 = true;
					}
					if (!flag2)
					{
						ShutdownSocket();
						HostName = "";
						lock (m_lock)
						{
							m_state = State.Failed;
						}
						AuthenticationException ex = new AuthenticationException($"The remote certificate was rejected due to the following error: {m_sslPolicyErrors}");
						Log.CouldNotInitializeTlsConnection(m_logger, ex, Id);
						throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "SSL Authentication Error", ex);
					}
				}
				string redirectionUrl = ok.RedirectionUrl;
				if (m_compressionMethod == CompressionMethod.Zlib)
				{
					m_payloadHandler = new CompressedPayloadHandler(m_payloadHandler.ByteHandler);
				}
				else if (m_compressionMethod == CompressionMethod.Zstandard)
				{
					m_payloadHandler = connection.ZstandardPlugin.CreatePayloadHandler(m_payloadHandler.ByteHandler);
				}
				bool num = UseOracleMode && !string.IsNullOrWhiteSpace(connectionStringCharset) && !connectionStringCharset.StartsWith("utf8", StringComparison.OrdinalIgnoreCase) && !connectionStringCharset.StartsWith("utf-8", StringComparison.OrdinalIgnoreCase);
				CharacterSet characterSet = ((ServerVersion.Version >= ServerVersions.SupportsUtf8Mb4) ? CharacterSet.Utf8Mb4Binary : CharacterSet.Utf8Mb3Binary);
				if (num || ok.NewCharacterSet != characterSet)
				{
					await SendAsync(m_setNamesPayload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					OkPayload.Verify((await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Span, this);
				}
				await TrySetOracleAutoCommitAsync(cs, connection, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				await TryInitOracleNationalCharacterSetAsync(cs, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				await TryInitOracleCharacterEncodingAsync(cs, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (ShouldGetRealServerDetails(cs))
				{
					await GetRealServerDetailsAsync(ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
				}
				else
				{
					int? newConnectionId = ok.NewConnectionId;
					if (newConnectionId.HasValue)
					{
						int valueOrDefault = newConnectionId.GetValueOrDefault();
						if (valueOrDefault != ConnectionId)
						{
							Log.ChangingConnectionId(m_logger, Id, ConnectionId, valueOrDefault, ServerVersion.OriginalString, ServerVersion.OriginalString);
							ConnectionId = valueOrDefault;
						}
					}
				}
				m_payloadHandler.ByteHandler.RemainingTimeout = int.MaxValue;
				return redirectionUrl;
			}
			}
		}
		catch (ArgumentException ex2)
		{
			Log.CouldNotConnectToServer(m_logger, ex2, Id);
			throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "Couldn't connect to server", ex2);
		}
		catch (IOException ex3)
		{
			Log.CouldNotConnectToServer(m_logger, ex3, Id);
			throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "Couldn't connect to server", ex3);
		}
	}

	/// <summary>
	/// Validate SSL validation hash (from OK packet).
	/// </summary>
	/// <param name="validationHash">The validation hash received from the server.</param>
	/// <param name="challenge">The auth plugin data from the initial handshake.</param>
	/// <returns><c>true</c> if the validation hash matches the locally-computed value; otherwise, <c>false</c>.</returns>
	private bool ValidateFingerprint(byte[]? validationHash, ReadOnlySpan<byte> challenge)
	{
		if (validationHash == null || validationHash.Length != 65 || validationHash[0] != 1)
		{
			return false;
		}
		if (m_passwordHash == null)
		{
			return false;
		}
		Span<byte> span = stackalloc byte[32];
		byte[] passwordHash = m_passwordHash;
		ReadOnlySpan<byte> readOnlySpan = challenge;
		byte[] remoteCertificateSha2Thumbprint = m_remoteCertificateSha2Thumbprint;
		int num = 0;
		byte[] array = new byte[passwordHash.Length + readOnlySpan.Length + remoteCertificateSha2Thumbprint.Length];
		ReadOnlySpan<byte> readOnlySpan2 = new ReadOnlySpan<byte>(passwordHash);
		readOnlySpan2.CopyTo(new Span<byte>(array).Slice(num, readOnlySpan2.Length));
		num += readOnlySpan2.Length;
		readOnlySpan.CopyTo(new Span<byte>(array).Slice(num, readOnlySpan.Length));
		num += readOnlySpan.Length;
		ReadOnlySpan<byte> readOnlySpan3 = new ReadOnlySpan<byte>(remoteCertificateSha2Thumbprint);
		readOnlySpan3.CopyTo(new Span<byte>(array).Slice(num, readOnlySpan3.Length));
		num += readOnlySpan3.Length;
		Span<byte> span2 = new Span<byte>(array);
		using (SHA256 hashAlgorithm = SHA256.Create())
		{
			Utility.TryComputeHash(hashAlgorithm, span2, span, out num);
			Span<byte> span3 = span2.Slice(0, 32);
			return TryConvertFromHexString(validationHash.AsSpan(1), span3) && span3.SequenceEqual(span);
		}
		static bool TryConvertFromHexString(ReadOnlySpan<byte> hexChars, Span<byte> data)
		{
			ReadOnlySpan<byte> span4 = "0123456789ABCDEFabcdef"u8;
			for (int i = 0; i < hexChars.Length; i += 2)
			{
				int num2 = span4.IndexOf(hexChars[i]);
				int num3 = span4.IndexOf(hexChars[i + 1]);
				if (num2 == -1 || num3 == -1)
				{
					return false;
				}
				if (num2 > 15)
				{
					num2 -= 6;
				}
				if (num3 > 15)
				{
					num3 -= 6;
				}
				data[i / 2] = (byte)((num2 << 4) | num3);
			}
			return true;
		}
	}

	public static async ValueTask<ServerSession> ConnectAndRedirectAsync(ILogger connectionLogger, ILogger poolLogger, IConnectionPoolMetadata pool, ConnectionSettings cs, ILoadBalancer? loadBalancer, IServerSessionOwner connection, Action<ILogger, int, string, Exception?>? logMessage, long startingTimestamp, Activity? activity, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		bool shouldRetryOnAuthError = loadBalancer != null && cs.ConnectionProtocol == MySqlConnectionProtocol.Sockets && cs.HostNames != null && cs.HostNames.Count > 1 && (cs.LoadBalance == MySqlLoadBalance.FailOver || cs.LoadBalance == MySqlLoadBalance.Random);
		ServerSession session = null;
		Exception lastException = null;
		string redirectionUrl = null;
		IReadOnlyList<string> hostNames = (shouldRetryOnAuthError ? loadBalancer.LoadBalance(cs.HostNames) : null);
		int maxAttempts = ((!shouldRetryOnAuthError) ? 1 : hostNames.Count);
		for (int attempt = 0; attempt < maxAttempts; attempt++)
		{
			if (session != null)
			{
				try
				{
					await session.DisposeAsync(ioBehavior, default(CancellationToken)).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch
				{
				}
			}
			session = new ServerSession(connectionLogger, pool);
			if (logMessage != null && poolLogger.IsEnabled(LogLevel.Debug))
			{
				logMessage(poolLogger, pool.Id, session.Id, null);
			}
			try
			{
				ConnectionSettings cs2 = cs;
				ILoadBalancer loadBalancer2 = loadBalancer;
				if (shouldRetryOnAuthError && attempt > 0 && hostNames != null)
				{
					string text = hostNames[attempt];
					int value;
					int port = ((cs.HostPorts != null && cs.HostPorts.TryGetValue(text, out value)) ? value : cs.Port);
					cs2 = cs.CloneWith(text, port, cs.UserID);
					loadBalancer2 = FailOverLoadBalancer.Instance;
				}
				redirectionUrl = await session.ConnectAsync(cs2, connection, startingTimestamp, loadBalancer2, activity, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				lastException = null;
			}
			catch (MySqlException ex) when (shouldRetryOnAuthError && attempt < maxAttempts - 1 && (ex.ErrorCode == MySqlErrorCode.AccessDenied || ex.ErrorCode == MySqlErrorCode.UnknownDatabase || ex.ErrorCode == MySqlErrorCode.DatabaseAccessDenied))
			{
				lastException = ex;
				string text2 = ((hostNames != null && attempt < hostNames.Count) ? hostNames[attempt] : "unknown");
				Log.FailedToConnectToIpAddress(connectionLogger, ex, LogLevel.Trace, session.Id, text2, 1, 1, text2, attempt + 1, maxAttempts, $"Authentication/database error (code {ex.ErrorCode}): {ex.Message}");
				continue;
			}
			catch (Exception)
			{
				await session.DisposeAsync(ioBehavior, default(CancellationToken)).ConfigureAwait(continueOnCapturedContext: false);
				throw;
			}
			break;
		}
		if (lastException != null)
		{
			if (session != null)
			{
				try
				{
					await session.DisposeAsync(ioBehavior, default(CancellationToken)).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch
				{
				}
			}
			throw lastException;
		}
		if (session == null)
		{
			throw new InvalidOperationException("Failed to create session");
		}
		Exception redirectionException = null;
		if (redirectionUrl != null)
		{
			Log.HasServerRedirectionHeader(connectionLogger, session.Id, redirectionUrl);
			if (cs.ServerRedirectionMode == MySqlServerRedirectionMode.Disabled)
			{
				Log.ServerRedirectionIsDisabled(connectionLogger, session.Id);
				return session;
			}
			if (Utility.TryParseRedirectionHeader(redirectionUrl, cs.UserID, out string host, out int port2, out string user))
			{
				if (host != cs.HostNames[0] || port2 != cs.Port || user != cs.UserID)
				{
					ConnectionSettings cs3 = cs.CloneWith(host, port2, user);
					Log.OpeningNewConnection(connectionLogger, session.Id, host, port2, user);
					ServerSession redirectedSession = new ServerSession(connectionLogger, pool);
					try
					{
						await redirectedSession.ConnectAsync(cs3, connection, startingTimestamp, loadBalancer, activity, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
						Log.ClosingSessionToUseRedirectedSession(connectionLogger, session.Id, redirectedSession.Id);
						await session.DisposeAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
						return redirectedSession;
					}
					catch (Exception ex3)
					{
						redirectionException = ex3;
						Log.FailedToConnectRedirectedSession(connectionLogger, ex3, session.Id, redirectedSession.Id);
						try
						{
							await redirectedSession.DisposeAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
						}
						catch (Exception)
						{
						}
					}
				}
				else
				{
					Log.SessionAlreadyConnectedToServer(connectionLogger, session.Id);
				}
			}
		}
		if (cs.ServerRedirectionMode == MySqlServerRedirectionMode.Required)
		{
			Log.RequiresServerRedirection(connectionLogger, session.Id);
			throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "Server does not support redirection", redirectionException);
		}
		return session;
	}

	public async Task<bool> TryResetConnectionAsync(ConnectionSettings cs, IServerSessionOwner connection, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		VerifyState(State.Connected);
		Conditions |= ConnectionOpenedConditions.Reset;
		try
		{
			ClearPreparedStatements();
			if (DatabaseOverride == null && ((!ServerVersion.IsMariaDb && ServerVersion.Version.CompareTo(ServerVersions.SupportsResetConnection) >= 0) || (ServerVersion.IsMariaDb && ServerVersion.Version.CompareTo(ServerVersions.MariaDbSupportsResetConnection) >= 0)))
			{
				if (m_supportsPipelining)
				{
					Log.SendingPipelinedResetConnectionRequest(m_logger, Id, ServerVersion.OriginalString);
					await SendRawAsync(m_pipelinedResetConnectionBytes, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					OkPayload.Verify((await ReceiveReplyAsync(1, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Span, this);
					OkPayload.Verify((await ReceiveReplyAsync(1, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Span, this);
					await TrySetOracleAutoCommitAsync(cs, connection, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					await TryInitOracleNationalCharacterSetAsync(cs, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					await TryInitOracleCharacterEncodingAsync(cs, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					return true;
				}
				Log.SendingResetConnectionRequest(m_logger, Id, ServerVersion.OriginalString);
				await SendAsync(ResetConnectionPayload.Instance, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				OkPayload.Verify((await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Span, this);
			}
			else
			{
				if (DatabaseOverride == null)
				{
					Log.SendingChangeUserRequest(m_logger, Id, ServerVersion.OriginalString);
				}
				else
				{
					Log.SendingChangeUserRequestDueToChangedDatabase(m_logger, Id, DatabaseOverride);
					DatabaseOverride = null;
				}
				string password = GetPassword(cs, connection);
				AuthenticationUtility.CreateResponseAndPasswordHash(password, AuthPluginData, out byte[] authenticationResponse, out m_passwordHash);
				using (PayloadData changeUserPayload = ChangeUserPayload.Create(cs.UserID, authenticationResponse, cs.Database, m_characterSet, m_supportsConnectionAttributes ? cs.ConnectionAttributes : null, UseOracleMode))
				{
					await SendAsync(changeUserPayload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				PayloadData payload = await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (payload.HeaderByte == 254)
				{
					Log.OptimisticReauthenticationFailed(m_logger, Id);
					payload = await SwitchAuthenticationAsync(cs, password, payload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				OkPayload.Verify(payload.Span, this);
			}
			await SendAsync(m_setNamesPayload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			OkPayload.Verify((await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Span, this);
			await TrySetOracleAutoCommitAsync(cs, connection, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await TryInitOracleNationalCharacterSetAsync(cs, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await TryInitOracleCharacterEncodingAsync(cs, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return true;
		}
		catch (IOException exception)
		{
			Log.IgnoringFailureInTryResetConnectionAsync(m_logger, exception, Id, "IOException");
		}
		catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.ClientInteractionTimeout)
		{
			Log.IgnoringFailureInTryResetConnectionAsync(m_logger, ex, Id, "ClientInteractionTimeout MySqlException");
		}
		catch (ObjectDisposedException exception2)
		{
			Log.IgnoringFailureInTryResetConnectionAsync(m_logger, exception2, Id, "ObjectDisposedException");
		}
		catch (SocketException exception3)
		{
			Log.IgnoringFailureInTryResetConnectionAsync(m_logger, exception3, Id, "SocketException");
		}
		Conditions &= ~ConnectionOpenedConditions.Reset;
		return false;
	}

	internal async Task ReinitializeOracleSessionAfterResetAsync(ConnectionSettings cs, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (UseOracleMode)
		{
			await SendAsync(m_setNamesPayload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			OkPayload.Verify((await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Span, this);
			await TrySetOracleAutoCommitAsync(cs, GetOwningConnectionOrNull(), ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await TryInitOracleNationalCharacterSetAsync(cs, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await TryInitOracleCharacterEncodingAsync(cs, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private Task TrySetOracleAutoCommitAsync(ConnectionSettings cs, IServerSessionOwner? connection, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		return SetOracleAutoCommitAsync(GetOracleAutoCommitForSessionInit(cs, connection), ioBehavior, cancellationToken, skipIfUnchanged: false);
	}

	private static bool GetOracleAutoCommitForSessionInit(ConnectionSettings cs, IServerSessionOwner? connection)
	{
		if (!(connection is OracleConnection oracleConnection))
		{
			return cs.AutoCommit;
		}
		return oracleConnection.GetEffectiveOracleAutoCommitForSessionInit(cs);
	}

	private IServerSessionOwner? GetOwningConnectionOrNull()
	{
		if (OwningConnection == null || !OwningConnection.TryGetTarget(out IServerSessionOwner target))
		{
			return null;
		}
		return target;
	}

	internal Task SetOracleAutoCommitAsync(bool autoCommit, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		return SetOracleAutoCommitAsync(autoCommit, ioBehavior, cancellationToken, skipIfUnchanged: true);
	}

	private async Task SetOracleAutoCommitAsync(bool autoCommit, IOBehavior ioBehavior, CancellationToken cancellationToken, bool skipIfUnchanged)
	{
		if (UseOracleMode && (!skipIfUnchanged || OracleAutoCommit != autoCommit))
		{
			PayloadData payload = ((!autoCommit) ? (SupportsQueryAttributes ? s_setAutocommit0WithAttributesPayload : s_setAutocommit0NoAttributesPayload) : (SupportsQueryAttributes ? s_setAutocommit1WithAttributesPayload : s_setAutocommit1NoAttributesPayload));
			await SendAsync(payload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			OkPayload.Verify((await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Span, this);
			OracleAutoCommit = autoCommit;
		}
	}

	private async Task TryInitOracleNationalCharacterSetAsync(ConnectionSettings cs, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (!UseOracleMode)
		{
			return;
		}
		try
		{
			string text = GetOptionalConnectionStringValue(cs.ConnectionStringBuilder, "nCharacterEncoding");
			if (string.IsNullOrWhiteSpace(text))
			{
				text = GetOptionalConnectionStringValue(cs.ConnectionStringBuilder, "NCharacterEncoding");
			}
			if (string.IsNullOrWhiteSpace(text))
			{
				PayloadData queryPayload = (SupportsQueryAttributes ? s_selectNlsNcharCharacterSetWithAttributesPayload : s_selectNlsNcharCharacterSetNoAttributesPayload);
				text = await QuerySingleStringAsync(queryPayload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (string.IsNullOrWhiteSpace(text))
				{
					return;
				}
				text = NormalizeNlsNcharCharacterSet(text);
			}
			string serverNCharEncoding = NormalizeServerNCharEncodingName(text);
			if (!string.IsNullOrWhiteSpace(serverNCharEncoding) && IsSafeOracleCharsetName(serverNCharEncoding))
			{
				PayloadData payload = (serverNCharEncoding.Equals("UTF8", StringComparison.OrdinalIgnoreCase) ? (SupportsQueryAttributes ? s_setNcharacterSetConnectionUtf8WithAttributesPayload : s_setNcharacterSetConnectionUtf8NoAttributesPayload) : ((!serverNCharEncoding.Equals("UTF16", StringComparison.OrdinalIgnoreCase)) ? QueryPayload.Create(SupportsQueryAttributes, Encoding.UTF8.GetBytes("set NCHARACTER_SET_CONNECTION = '" + serverNCharEncoding + "'")) : (SupportsQueryAttributes ? s_setNcharacterSetConnectionUtf16WithAttributesPayload : s_setNcharacterSetConnectionUtf16NoAttributesPayload)));
				await SendAsync(payload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				OkPayload.Verify((await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Span, this);
				NCharacterSetConnection = serverNCharEncoding;
				NCharacterEncoding = CreateDotNetEncodingForOracleNChar(serverNCharEncoding);
			}
		}
		catch
		{
			NCharacterEncoding = null;
			NCharacterSetConnection = null;
		}
	}

	private Task TryInitOracleCharacterEncodingAsync(ConnectionSettings cs, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (!UseOracleMode)
		{
			CharacterEncoding = Encoding.UTF8;
			CachedStringColumnReader = null;
			return Task.CompletedTask;
		}
		string characterSet = cs.ConnectionStringBuilder.CharacterSet;
		if (string.IsNullOrWhiteSpace(characterSet))
		{
			CharacterEncoding = Encoding.UTF8;
			CachedStringColumnReader = null;
			return Task.CompletedTask;
		}
		try
		{
			CharacterEncoding = CreateDotNetEncodingForCharacterSet(characterSet);
		}
		catch
		{
			CharacterEncoding = Encoding.UTF8;
		}
		CachedStringColumnReader = (CharacterEncoding.Equals(Encoding.UTF8) ? null : new StringColumnReader(CharacterEncoding));
		return Task.CompletedTask;
	}

	private static Encoding CreateDotNetEncodingForCharacterSet(string charsetName)
	{
		if (charsetName.AsSpan().Contains("utf8".AsSpan(), StringComparison.OrdinalIgnoreCase) || charsetName.AsSpan().Contains("utf-8".AsSpan(), StringComparison.OrdinalIgnoreCase))
		{
			return Encoding.UTF8;
		}
		if (charsetName.Equals("latin1", StringComparison.OrdinalIgnoreCase))
		{
			return Encoding.GetEncoding("ISO-8859-1");
		}
		if (charsetName.Equals("gbk", StringComparison.OrdinalIgnoreCase) || charsetName.Equals("gb2312", StringComparison.OrdinalIgnoreCase))
		{
			return Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
		}
		return Encoding.GetEncoding(charsetName);
	}

	private static string? GetOptionalConnectionStringValue(MySqlConnectionStringBuilder csb, string key)
	{
		if (!csb.TryGetValue(key, out var value))
		{
			return null;
		}
		return Convert.ToString(value, CultureInfo.InvariantCulture);
	}

	private async Task<string?> QuerySingleStringAsync(PayloadData queryPayload, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		await SendAsync(queryPayload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		PayloadData payloadData = await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		int columnCount = checked((int)new ByteArrayReader(payloadData.Span).ReadLengthEncodedInteger());
		for (int i = 0; i < columnCount; i++)
		{
			await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (!SupportsDeprecateEof)
		{
			payloadData = await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			EofPayload.Create(payloadData.Span);
		}
		string result = null;
		bool haveResult = false;
		while (true)
		{
			payloadData = await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (OkPayload.IsOk(payloadData.Span, this))
			{
				OkPayload.Verify(payloadData.Span, this);
				break;
			}
			if (!SupportsDeprecateEof && payloadData.HeaderByte == 254)
			{
				EofPayload.Create(payloadData.Span);
				break;
			}
			if (!haveResult)
			{
				ByteArrayReader byteArrayReader = new ByteArrayReader(payloadData.Span);
				int num = byteArrayReader.ReadLengthEncodedIntegerOrNull();
				if (num != -1)
				{
					ReadOnlySpan<byte> span = byteArrayReader.ReadByteString(num);
					result = Utility.GetString(Encoding.UTF8, span);
				}
				haveResult = true;
			}
		}
		return result;
	}

	private static string NormalizeNlsNcharCharacterSet(string nlsNcharCharacterSet)
	{
		string text = nlsNcharCharacterSet.Trim();
		if (text.AsSpan().Contains("UTF8".AsSpan(), StringComparison.OrdinalIgnoreCase))
		{
			return "UTF8";
		}
		if (text.AsSpan().Contains("UTF16".AsSpan(), StringComparison.OrdinalIgnoreCase))
		{
			return "UTF16";
		}
		return text;
	}

	private static string NormalizeServerNCharEncodingName(string nCharacterEncoding)
	{
		string text = nCharacterEncoding.Trim();
		if (text.AsSpan().Contains("UTF-16BE".AsSpan(), StringComparison.OrdinalIgnoreCase) || text.AsSpan().Contains("UTF16".AsSpan(), StringComparison.OrdinalIgnoreCase) || text.AsSpan().Contains("UTF-16".AsSpan(), StringComparison.OrdinalIgnoreCase))
		{
			return "UTF16";
		}
		if (text.AsSpan().Contains("UTF8".AsSpan(), StringComparison.OrdinalIgnoreCase) || text.AsSpan().Contains("UTF-8".AsSpan(), StringComparison.OrdinalIgnoreCase))
		{
			return "UTF8";
		}
		return text;
	}

	private static bool IsSafeOracleCharsetName(string value)
	{
		foreach (char c in value)
		{
			if (!char.IsLetterOrDigit(c) && c != '_')
			{
				return false;
			}
		}
		return value.Length != 0;
	}

	private static Encoding? CreateDotNetEncodingForOracleNChar(string serverNCharEncoding)
	{
		if (!serverNCharEncoding.AsSpan().Contains("UTF16".AsSpan(), StringComparison.OrdinalIgnoreCase))
		{
			if (!serverNCharEncoding.AsSpan().Contains("UTF8".AsSpan(), StringComparison.OrdinalIgnoreCase))
			{
				return TryGetEncoding(serverNCharEncoding);
			}
			return Encoding.UTF8;
		}
		return Encoding.BigEndianUnicode;
	}

	private static Encoding? TryGetEncoding(string encodingName)
	{
		try
		{
			return Encoding.GetEncoding(encodingName);
		}
		catch
		{
			return null;
		}
	}

	private async Task<PayloadData> SwitchAuthenticationAsync(ConnectionSettings cs, string password, PayloadData payload, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		AuthenticationMethodSwitchRequestPayload switchRequest = AuthenticationMethodSwitchRequestPayload.Create(payload.Span);
		Log.SwitchingToAuthenticationMethod(m_logger, Id, switchRequest.Name);
		switch (switchRequest.Name)
		{
		case "mysql_native_password":
		{
			AuthPluginData = switchRequest.Data;
			AuthenticationUtility.CreateResponseAndPasswordHash(password, AuthPluginData, out byte[] authenticationResponse2, out m_passwordHash);
			payload = new PayloadData(authenticationResponse2);
			await SendReplyAsync(payload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		case "mysql_clear_password":
		{
			if (!m_isSecureConnection && !m_isLoopbackConnection)
			{
				Log.NeedsSecureConnection(m_logger, Id, switchRequest.Name);
				throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "Authentication method '" + switchRequest.Name + "' requires a secure connection.");
			}
			byte[] nullTerminatedPasswordBytes = AuthenticationUtility.GetNullTerminatedPasswordBytes(password);
			payload = new PayloadData(nullTerminatedPasswordBytes);
			await SendReplyAsync(payload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		case "caching_sha2_password":
		{
			byte[] data = AuthenticationUtility.CreateScrambleResponse(Utility.TrimZeroByte(switchRequest.Data.AsSpan()), password);
			payload = new PayloadData(data);
			await SendReplyAsync(payload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			payload = await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (OkPayload.IsOk(payload.Span, this))
			{
				return payload;
			}
			if (CachingSha2ServerResponsePayload.Create(payload.Span).Succeeded)
			{
				return await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			goto case "sha256_password";
		}
		case "sha256_password":
			if (!m_isSecureConnection && password.Length != 0)
			{
				byte[] rsaPublicKey = await GetRsaPublicKeyAsync(switchRequest.Name, cs, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				return await SendEncryptedPasswordAsync(switchRequest.Data, rsaPublicKey, password, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			return await SendClearPasswordAsync(password, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		case "auth_gssapi_client":
			return await AuthGSSAPI.AuthenticateAsync(cs, switchRequest.Data, this, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		case "mysql_old_password":
			Log.AuthenticationMethodNotSupported(m_logger, Id, switchRequest.Name);
			throw new NotSupportedException("'MySQL Server is requesting the insecure pre-4.1 auth mechanism (mysql_old_password). The user password must be upgraded; see https://dev.mysql.com/doc/refman/5.7/en/account-upgrades.html.");
		case "client_ed25519":
		{
			if (!AuthenticationPlugins.TryGetPlugin(switchRequest.Name, out IAuthenticationPlugin plugin2) || !(plugin2 is IAuthenticationPlugin3 authenticationPlugin))
			{
				throw new NotSupportedException("You must install the OceanBase.Authentication.Ed25519 package and call Ed25519AuthenticationPlugin.Install to use client_ed25519 authentication.");
			}
			authenticationPlugin.CreateResponseAndPasswordHash(password, switchRequest.Data, out byte[] authenticationResponse3, out m_passwordHash);
			payload = new PayloadData(authenticationResponse3);
			await SendReplyAsync(payload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		case "parsec":
		{
			if (!AuthenticationPlugins.TryGetPlugin(switchRequest.Name, out IAuthenticationPlugin plugin) || !(plugin is IAuthenticationPlugin3 parsecPlugin3))
			{
				throw new NotSupportedException("You must install the OceanBase.Authentication.Ed25519 package and call ParsecAuthenticationPlugin.Install to use parsec authentication.");
			}
			payload = new PayloadData(Array.Empty<byte>());
			await SendReplyAsync(payload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			payload = await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			ReadOnlySpan<byte> readOnlySpan = payload.Span;
			if (readOnlySpan.Length > 2 && readOnlySpan[0] == 1 && readOnlySpan[1] == 80)
			{
				readOnlySpan = readOnlySpan.Slice(1, readOnlySpan.Length - 1);
			}
			Span<byte> span = stackalloc byte[switchRequest.Data.Length + readOnlySpan.Length];
			switchRequest.Data.CopyTo(span);
			readOnlySpan.CopyTo(span.Slice(switchRequest.Data.Length));
			parsecPlugin3.CreateResponseAndPasswordHash(password, span, out byte[] authenticationResponse, out m_passwordHash);
			payload = new PayloadData(authenticationResponse);
			await SendReplyAsync(payload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		default:
			Log.AuthenticationMethodNotSupported(m_logger, Id, switchRequest.Name);
			throw new NotSupportedException("Authentication method '" + switchRequest.Name + "' is not supported.");
		}
	}

	private async Task<PayloadData> SendClearPasswordAsync(string password, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		byte[] nullTerminatedPasswordBytes = AuthenticationUtility.GetNullTerminatedPasswordBytes(password);
		PayloadData payload = new PayloadData(nullTerminatedPasswordBytes);
		await SendReplyAsync(payload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task<PayloadData> SendEncryptedPasswordAsync(byte[] switchRequestData, byte[] rsaPublicKey, string password, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		using RSA rsa = RSA.Create();
		RSAParameters rsaParameters;
		try
		{
			rsaParameters = Utility.GetRsaParameters(Encoding.ASCII.GetString(rsaPublicKey));
		}
		catch (Exception ex)
		{
			Log.CouldNotLoadServerRsaPublicKey(m_logger, ex, Id);
			throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "Couldn't load server's RSA public key; try using a secure connection instead.", ex);
		}
		rsa.ImportParameters(rsaParameters);
		byte[] nullTerminatedPasswordBytes = AuthenticationUtility.GetNullTerminatedPasswordBytes(password);
		AuthPluginData = Utility.TrimZeroByte(switchRequestData);
		for (int i = 0; i < nullTerminatedPasswordBytes.Length; i++)
		{
			nullTerminatedPasswordBytes[i] ^= AuthPluginData[i % AuthPluginData.Length];
		}
		RSAEncryptionPadding oaepSHA = RSAEncryptionPadding.OaepSHA1;
		byte[] data = rsa.Encrypt(nullTerminatedPasswordBytes, oaepSHA);
		PayloadData payload = new PayloadData(data);
		await SendReplyAsync(payload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task<byte[]> GetRsaPublicKeyAsync(string switchRequestName, ConnectionSettings cs, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (cs.ServerRsaPublicKeyFile.Length != 0)
		{
			try
			{
				return File.ReadAllBytes(cs.ServerRsaPublicKeyFile);
			}
			catch (IOException ex)
			{
				Log.CouldNotLoadServerRsaPublicKeyFromFile(m_logger, ex, Id, cs.ServerRsaPublicKeyFile);
				throw new MySqlException("Couldn't load server's RSA public key from '" + cs.ServerRsaPublicKeyFile + "'", ex);
			}
		}
		if (cs.AllowPublicKeyRetrieval || m_isLoopbackConnection)
		{
			byte b = (byte)((!(switchRequestName == "caching_sha2_password")) ? 1 : 2);
			await SendReplyAsync(new PayloadData(new byte[1] { b }), ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return AuthenticationMoreDataPayload.Create((await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Span).Data;
		}
		Log.CouldNotUseAuthenticationMethodForRsa(m_logger, Id, switchRequestName);
		throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "Authentication method '" + switchRequestName + "' failed. Either use a secure connection, specify the server's RSA public key with ServerRSAPublicKeyFile, or set AllowPublicKeyRetrieval=True.");
	}

	public async ValueTask<bool> TryPingAsync(bool logInfo, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		VerifyState(State.Connected);
		try
		{
			Log.PingingServer(m_logger, Id);
			await SendAsync(PingPayload.Instance, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			OkPayload.Verify((await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Span, this);
			Log.SuccessfullyPingedServer(m_logger, logInfo ? LogLevel.Information : LogLevel.Trace, Id);
			return true;
		}
		catch (IOException exception)
		{
			Log.PingFailed(m_logger, exception, Id, "IOException");
		}
		catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.ClientInteractionTimeout)
		{
			Log.PingFailed(m_logger, ex, Id, "ClientInteractionTimeout MySqlException");
		}
		catch (SocketException exception2)
		{
			Log.PingFailed(m_logger, exception2, Id, "SocketException");
		}
		VerifyState(State.Failed);
		return false;
	}

	public ValueTask SendAsync(PayloadData payload, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		m_payloadHandler.StartNewConversation();
		return SendReplyAsync(payload, ioBehavior, cancellationToken);
	}

	public ValueTask<PayloadData> ReceiveAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		m_payloadHandler.StartNewConversation();
		return ReceiveReplyAsync(ioBehavior, cancellationToken);
	}

	public async ValueTask<PayloadData> ReceiveReplyAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		InvalidOperationException ex = CreateExceptionForInvalidState();
		if (ex != null)
		{
			Log.FailedInReceiveReplyAsync(m_logger, ex, Id);
			throw ex;
		}
		ArraySegment<byte> arraySegment;
		try
		{
			arraySegment = await m_payloadHandler.ReadPayloadAsync(m_payloadCache, ProtocolErrorBehavior.Throw, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex2)
		{
			if (ex2 is MySqlEndOfStreamException ex3)
			{
				Log.ExpectedToReadMoreBytes(m_logger, Id, ex3.ExpectedByteCount, ex3.ReadByteCount);
			}
			SetFailed(ex2);
			throw;
		}
		PayloadData result = new PayloadData(arraySegment);
		if (result.HeaderByte != byte.MaxValue)
		{
			return result;
		}
		throw CreateExceptionForErrorPayload(result.Span);
	}

	public ValueTask<PayloadData> ReceiveReplyAsync(int expectedSequenceNumber, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		m_payloadHandler.SetNextSequenceNumber(expectedSequenceNumber);
		return ReceiveReplyAsync(ioBehavior, cancellationToken);
	}

	/// <summary>
	/// Continues a conversation with the server by receiving a response, with optional protocol error handling.
	/// This overload allows ignoring packet sequence number validation for compatibility with certain server versions.
	/// </summary>
	/// <param name="protocolErrorBehavior">How to handle protocol errors (e.g., out-of-order packets).</param>
	/// <param name="ioBehavior">The IO behavior to use.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The received payload.</returns>
	public async ValueTask<PayloadData> ReceiveReplyAsync(ProtocolErrorBehavior protocolErrorBehavior, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		InvalidOperationException ex = CreateExceptionForInvalidState();
		if (ex != null)
		{
			Log.FailedInReceiveReplyAsync(m_logger, ex, Id);
			throw ex;
		}
		ArraySegment<byte> arraySegment;
		try
		{
			arraySegment = await m_payloadHandler.ReadPayloadAsync(m_payloadCache, protocolErrorBehavior, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex2)
		{
			if (ex2 is MySqlEndOfStreamException ex3)
			{
				Log.ExpectedToReadMoreBytes(m_logger, Id, ex3.ExpectedByteCount, ex3.ReadByteCount);
			}
			SetFailed(ex2);
			throw;
		}
		PayloadData result = new PayloadData(arraySegment);
		if (result.HeaderByte != byte.MaxValue)
		{
			return result;
		}
		throw CreateExceptionForErrorPayload(result.Span);
	}

	public async ValueTask SendReplyAsync(PayloadData payload, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		InvalidOperationException ex = CreateExceptionForInvalidState();
		if (ex != null)
		{
			Log.FailedInSendReplyAsync(m_logger, ex, Id);
			throw ex;
		}
		try
		{
			await m_payloadHandler.WritePayloadAsync(payload.Memory, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception failed)
		{
			SetFailed(failed);
			throw;
		}
	}

	public async ValueTask SendRawAsync(ReadOnlyMemory<byte> data, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		InvalidOperationException ex = CreateExceptionForInvalidState();
		if (ex != null)
		{
			Log.FailedInSendReplyAsync(m_logger, ex, Id);
			throw ex;
		}
		try
		{
			await m_payloadHandler.ByteHandler.WriteBytesAsync(data, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception failed)
		{
			SetFailed(failed);
			throw;
		}
	}

	public static void ThrowIfStatementContainsDelimiter(MySqlException exception, IMySqlCommand command)
	{
		if (exception.ErrorCode != MySqlErrorCode.ParseError)
		{
			return;
		}
		string? commandText = command.CommandText;
		if (commandText != null && commandText.IndexOf("delimiter", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			DelimiterSqlParser delimiterSqlParser = new DelimiterSqlParser(command);
			delimiterSqlParser.Parse(command.CommandText);
			if (delimiterSqlParser.HasDelimiter)
			{
				throw new MySqlException(MySqlErrorCode.DelimiterNotSupported, "'DELIMITER' should not be used with OceanBase. See https://mysqlconnector.net/delimiter", exception);
			}
		}
	}

	public static void ThrowIfStatementContainsDelimiter(MySqlException exception, CommandFacade command)
	{
		if (exception.ErrorCode != MySqlErrorCode.ParseError)
		{
			return;
		}
		string? commandText = command.CommandText;
		if (commandText != null && commandText.IndexOf("delimiter", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			DelimiterSqlParser2 delimiterSqlParser = new DelimiterSqlParser2(command.CommandText, command.CreateStatementPreparerOptions());
			delimiterSqlParser.Parse(command.CommandText);
			if (delimiterSqlParser.HasDelimiter)
			{
				throw new MySqlException(MySqlErrorCode.DelimiterNotSupported, "'DELIMITER' should not be used with OceanBase. See https://mysqlconnector.net/delimiter", exception);
			}
		}
	}

	private InvalidOperationException? CreateExceptionForInvalidState()
	{
		lock (m_lock)
		{
			switch (m_state)
			{
			case State.Closed:
				return new ObjectDisposedException("ServerSession");
			case State.Connected:
			case State.Querying:
			case State.CancelingQuery:
			case State.ClearingPendingCancellation:
			case State.Closing:
				return null;
			default:
				return new InvalidOperationException("ServerSession is not connected.");
			}
		}
	}

	private async Task<bool> OpenTcpSocketAsync(ConnectionSettings cs, ILoadBalancer loadBalancer, Activity? activity, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		m_activityTags.Add("net.transport", (object)"ip_tcp");
		string text = ((cs.Port == 3306) ? null : cs.Port.ToString(CultureInfo.InvariantCulture));
		if (text != null)
		{
			m_activityTags.Add("net.peer.port", (object)text);
		}
		if (activity != null && activity.IsAllDataRequested)
		{
			activity.SetTag("net.transport", (object)"ip_tcp");
			if (text != null)
			{
				activity.SetTag("net.peer.port", (object)text);
			}
		}
		IReadOnlyList<string> hostNames = loadBalancer.LoadBalance(cs.HostNames);
		bool flag = default(bool);
		for (int hostNameIndex = 0; hostNameIndex < hostNames.Count; hostNameIndex++)
		{
			string hostName = hostNames[hostNameIndex];
			int value;
			int port = ((cs.HostPorts != null && cs.HostPorts.TryGetValue(hostName, out value)) ? value : cs.Port);
			IPAddress[] ipAddresses;
			try
			{
				IPAddress iPAddress = cs.IPAddress;
				if (iPAddress != null)
				{
					ipAddresses = new IPAddress[1] { iPAddress };
				}
				else
				{
					IPAddress[] array = ((ioBehavior != IOBehavior.Asynchronous) ? Dns.GetHostAddresses(hostName) : (await Dns.GetHostAddressesAsync(hostName).ConfigureAwait(continueOnCapturedContext: false)));
					ipAddresses = array;
				}
			}
			catch (SocketException ex)
			{
				Log.FailedToResolveHostName(m_logger, ex, Id, hostName, hostNameIndex + 1, hostNames.Count, ex.Message);
				continue;
			}
			for (int ipAddressIndex = 0; ipAddressIndex < ipAddresses.Length; ipAddressIndex++)
			{
				IPAddress ipAddress = ipAddresses[ipAddressIndex];
				string ipAddressString = ipAddress.ToString();
				Log.ConnectingToIpAddress(m_logger, Id, ipAddressString, ipAddressIndex + 1, ipAddresses.Length, hostName, hostNameIndex + 1, hostNames.Count);
				m_activityTags["net.peer.ip"] = ipAddressString;
				if (ipAddressString != hostName)
				{
					m_activityTags["net.peer.name"] = hostName;
				}
				else
				{
					m_activityTags.Remove("net.peer.name");
				}
				if (activity != null && activity.IsAllDataRequested)
				{
					activity.SetTag("net.peer.ip", (object)ipAddressString);
					if (ipAddressString != hostName)
					{
						activity.SetTag("net.peer.name", (object)hostName);
					}
					else
					{
						activity.SetTag("net.peer.name", (object)null);
					}
				}
				TcpClient tcpClient = null;
				try
				{
					tcpClient = new TcpClient(ipAddress.AddressFamily);
					using (cancellationToken.Register(delegate
					{
						tcpClient?.Client?.Dispose();
					}))
					{
						_ = 1;
						try
						{
							if (ioBehavior == IOBehavior.Asynchronous)
							{
								await tcpClient.ConnectAsync(ipAddress, port).ConfigureAwait(continueOnCapturedContext: false);
							}
							else if (Utility.IsWindows())
							{
								tcpClient.Connect(ipAddress, port);
							}
							else
							{
								int sendTimeout = tcpClient.Client.SendTimeout;
								int receiveTimeout = tcpClient.Client.ReceiveTimeout;
								tcpClient.Client.SendTimeout = cs.ConnectionTimeoutMilliseconds;
								tcpClient.Client.ReceiveTimeout = cs.ConnectionTimeoutMilliseconds;
								tcpClient.Connect(ipAddress, port);
								tcpClient.Client.SendTimeout = sendTimeout;
								tcpClient.Client.ReceiveTimeout = receiveTimeout;
							}
						}
						catch (Exception ex2) when (((Func<bool>)delegate
						{
							// Could not convert BlockContainer to single expression
							flag = cancellationToken.IsCancellationRequested;
							if (flag)
							{
								flag = ((ex2 is ObjectDisposedException || ex2 is SocketException) ? true : false);
							}
							return flag;
						}).Invoke())
						{
							SafeDispose(ref tcpClient);
							Log.ConnectTimeoutExpired(m_logger, ex2, Id, ipAddressString, hostName);
							throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "Connect Timeout expired.");
						}
					}
				}
				catch (SocketException ex3)
				{
					SafeDispose(ref tcpClient);
					if (hostNameIndex == hostNames.Count - 1 && ipAddressIndex == ipAddresses.Length - 1)
					{
						lock (m_lock)
						{
							m_state = State.Failed;
						}
						if (hostNames.Count == 1 && ipAddresses.Length == 1)
						{
							Log.FailedToConnectToSingleIpAddress(m_logger, ex3, Id, ipAddressString, hostName, ex3.Message);
						}
						else
						{
							Log.FailedToConnectToIpAddress(m_logger, ex3, LogLevel.Information, Id, ipAddressString, ipAddressIndex + 1, ipAddresses.Length, hostName, hostNameIndex + 1, hostNames.Count, ex3.Message);
						}
						throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "Unable to connect to any of the specified MySQL hosts.");
					}
					Log.FailedToConnectToIpAddress(m_logger, ex3, LogLevel.Trace, Id, ipAddressString, ipAddressIndex + 1, ipAddresses.Length, hostName, hostNameIndex + 1, hostNames.Count, ex3.Message);
					continue;
				}
				if (!tcpClient.Connected && cancellationToken.IsCancellationRequested)
				{
					SafeDispose(ref tcpClient);
					Log.ConnectTimeoutExpired(m_logger, null, Id, ipAddressString, hostName);
					throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "Connect Timeout expired.");
				}
				try
				{
					HostName = hostName;
					m_tcpClient = tcpClient;
					m_socket = m_tcpClient.Client;
					m_socket.NoDelay = true;
					m_stream = m_tcpClient.GetStream();
					m_socket.SetKeepAlive(cs.Keepalive);
					m_isLoopbackConnection = IPAddress.IsLoopback(ipAddress);
				}
				catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
				{
					Utility.Dispose(ref m_stream);
					SafeDispose(ref m_tcpClient);
					SafeDispose(ref m_socket);
					Log.ConnectTimeoutExpired(m_logger, null, Id, ipAddressString, hostName);
					throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "Connect Timeout expired.");
				}
				lock (m_lock)
				{
					m_state = State.Connected;
				}
				Log.ConnectedToIpAddress(m_logger, Id, ipAddressString, hostName, (m_socket.LocalEndPoint as IPEndPoint)?.Port);
				return true;
			}
		}
		return false;
	}

	private async Task<bool> OpenUnixSocketAsync(ConnectionSettings cs, Activity? activity, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		Log.ConnectingToUnixSocket(m_logger, Id, cs.UnixSocket);
		m_activityTags.Add("net.transport", (object)"unix");
		m_activityTags.Add("net.peer.name", (object)cs.UnixSocket);
		if (activity != null && activity.IsAllDataRequested)
		{
			activity.SetTag("net.transport", (object)"unix").SetTag("net.peer.name", (object)cs.UnixSocket);
		}
		Socket socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
		UnixDomainSocketEndPoint unixDomainSocketEndPoint = new UnixDomainSocketEndPoint(cs.UnixSocket);
		try
		{
			using (cancellationToken.Register(socket.Dispose))
			{
				try
				{
					if (ioBehavior == IOBehavior.Asynchronous)
					{
						await Task.Factory.FromAsync(socket.BeginConnect, socket.EndConnect, unixDomainSocketEndPoint, null).ConfigureAwait(continueOnCapturedContext: false);
					}
					else
					{
						socket.Connect(unixDomainSocketEndPoint);
					}
				}
				catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
				{
					Log.ConnectTimeoutExpiredForUnixSocket(m_logger, Id, cs.UnixSocket);
					throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "Connect Timeout expired.");
				}
			}
		}
		catch (SocketException)
		{
			socket.Dispose();
		}
		if (socket.Connected)
		{
			m_socket = socket;
			m_stream = new NetworkStream(socket);
			lock (m_lock)
			{
				m_state = State.Connected;
			}
			return true;
		}
		return false;
	}

	private async Task<bool> OpenNamedPipeAsync(ConnectionSettings cs, long startingTimestamp, Activity? activity, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		Log.ConnectingToNamedPipe(m_logger, Id, cs.PipeName, cs.HostNames[0]);
		string text = "\\\\" + cs.HostNames[0] + "\\pipe\\" + cs.PipeName;
		m_activityTags.Add("net.transport", (object)"pipe");
		m_activityTags.Add("net.peer.name", (object)text);
		if (activity != null && activity.IsAllDataRequested)
		{
			activity.SetTag("net.transport", (object)"pipe");
			activity.SetTag("net.peer.name", (object)text);
		}
		NamedPipeClientStream namedPipeStream = new NamedPipeClientStream(cs.HostNames[0], cs.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
		int timeout = Math.Max(1, cs.ConnectionTimeoutMilliseconds - Utility.GetElapsedMilliseconds(startingTimestamp));
		try
		{
			using (cancellationToken.Register(namedPipeStream.Dispose))
			{
				try
				{
					if (ioBehavior == IOBehavior.Asynchronous)
					{
						await namedPipeStream.ConnectAsync(timeout, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					}
					else
					{
						namedPipeStream.Connect(timeout);
					}
				}
				catch (Exception ex) when ((ex is ObjectDisposedException && cancellationToken.IsCancellationRequested) || ex is TimeoutException)
				{
					Log.ConnectTimeoutExpiredForNamedPipe(m_logger, ex, Id, cs.PipeName, cs.HostNames[0]);
					throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "Connect Timeout expired.");
				}
			}
		}
		catch (IOException)
		{
			namedPipeStream.Dispose();
		}
		if (namedPipeStream.IsConnected)
		{
			m_stream = namedPipeStream;
			lock (m_lock)
			{
				m_state = State.Connected;
			}
			return true;
		}
		return false;
	}

	private async Task InitSslAsync(ProtocolCapabilities serverCapabilities, ConnectionSettings cs, IServerSessionOwner connection, SslProtocols sslProtocols, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		Log.InitializingTlsConnection(m_logger, Id);
		X509CertificateCollection clientCertificates = null;
		if (cs.CertificateStoreLocation != MySqlCertificateStoreLocation.None)
		{
			try
			{
				StoreLocation storeLocation = ((cs.CertificateStoreLocation == MySqlCertificateStoreLocation.CurrentUser) ? StoreLocation.CurrentUser : StoreLocation.LocalMachine);
				using X509Store x509Store = new X509Store(StoreName.My, storeLocation);
				x509Store.Open(OpenFlags.OpenExistingOnly);
				if (cs.CertificateThumbprint.Length == 0)
				{
					if (x509Store.Certificates.Count == 0)
					{
						Log.NoCertificatesFound(m_logger, Id);
						throw new MySqlException("No certificates were found in the certificate store");
					}
					clientCertificates = new X509CertificateCollection(x509Store.Certificates);
				}
				else
				{
					MySqlSslMode sslMode = cs.SslMode;
					bool flag = (uint)(sslMode - 3) <= 1u;
					bool validOnly = flag;
					X509Certificate2Collection x509Certificate2Collection = x509Store.Certificates.Find(X509FindType.FindByThumbprint, cs.CertificateThumbprint, validOnly);
					if (x509Certificate2Collection.Count == 0)
					{
						Log.CertificateNotFoundInStore(m_logger, Id, cs.CertificateThumbprint);
						throw new MySqlException("Certificate with Thumbprint " + cs.CertificateThumbprint + " not found");
					}
					clientCertificates = new X509CertificateCollection(x509Certificate2Collection);
				}
			}
			catch (CryptographicException ex)
			{
				Log.CouldNotLoadCertificate(m_logger, ex, Id, cs.CertificateStoreLocation);
				throw new MySqlException("Certificate couldn't be loaded from the CertificateStoreLocation", ex);
			}
		}
		if (cs.SslKeyFile.Length != 0 && cs.SslCertificateFile.Length != 0)
		{
			clientCertificates = LoadCertificate(cs.SslKeyFile, cs.SslCertificateFile);
		}
		else if (cs.CertificateFile.Length != 0)
		{
			try
			{
				X509Certificate2 x509Certificate = new X509Certificate2(cs.CertificateFile, cs.CertificatePassword, X509KeyStorageFlags.MachineKeySet);
				if (!x509Certificate.HasPrivateKey)
				{
					x509Certificate.Dispose();
					Log.NoPrivateKeyIncludedWithCertificateFile(m_logger, Id, cs.CertificateFile);
					throw new MySqlException("CertificateFile does not contain a private key. CertificateFile should be in PKCS #12 (.pfx) format and contain both a Certificate and Private Key");
				}
				m_clientCertificate = x509Certificate;
				clientCertificates = new X509CertificateCollection { x509Certificate };
			}
			catch (CryptographicException ex2)
			{
				Log.CouldNotLoadCertificateFromFile(m_logger, ex2, Id, cs.CertificateFile);
				if (!File.Exists(cs.CertificateFile))
				{
					throw new MySqlException("Cannot find Certificate File", ex2);
				}
				throw new MySqlException("Either the Certificate Password is incorrect or the Certificate File is invalid", ex2);
			}
		}
		if (clientCertificates == null)
		{
			Func<X509CertificateCollection, ValueTask> provideClientCertificatesCallback = connection.ProvideClientCertificatesCallback;
			if (provideClientCertificatesCallback != null)
			{
				clientCertificates = new X509CertificateCollection();
				try
				{
					await provideClientCertificatesCallback(clientCertificates).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch (Exception ex3)
				{
					Log.FailedToObtainClientCertificates(m_logger, ex3, Id, ex3.Message);
					throw new MySqlException("Failed to obtain client certificates via ProvideClientCertificatesCallback", ex3);
				}
			}
		}
		X509Chain caCertificateChain = null;
		if (cs.CACertificateFile.Length != 0)
		{
			X509Chain x509Chain = new X509Chain
			{
				ChainPolicy = 
				{
					RevocationMode = X509RevocationMode.NoCheck,
					VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority
				}
			};
			try
			{
				Log.LoadingCaCertificatesFromFile(m_logger, Id, cs.CACertificateFile);
				byte[] array;
				try
				{
					array = File.ReadAllBytes(cs.CACertificateFile);
				}
				catch (Exception ex4)
				{
					Log.CouldNotLoadCaCertificateFromFile(m_logger, ex4, LogLevel.Error, Id, cs.CACertificateFile);
					if (!File.Exists(cs.CACertificateFile))
					{
						throw new MySqlException("Cannot find CA Certificate File: " + cs.CACertificateFile, ex4);
					}
					throw new MySqlException("Could not load CA Certificate File: " + cs.CACertificateFile, ex4);
				}
				int num = 0;
				while (num != -1)
				{
					int num2 = Utility.FindNextIndex(array, num + 1, "-----BEGIN CERTIFICATE-----"u8);
					try
					{
						Log.LoadingCaCertificate(m_logger, Id, num);
						X509Certificate2 certificate = new X509Certificate2(Utility.ArraySlice(array, num, ((num2 == -1) ? array.Length : num2) - num), (string)null, X509KeyStorageFlags.MachineKeySet);
						x509Chain.ChainPolicy.ExtraStore.Add(certificate);
					}
					catch (CryptographicException exception)
					{
						Log.CouldNotLoadCaCertificateFromFile(m_logger, exception, LogLevel.Warning, Id, cs.CACertificateFile);
					}
					num = num2;
				}
				Log.LoadedCaCertificatesFromFile(m_logger, Id, x509Chain.ChainPolicy.ExtraStore.Count, cs.CACertificateFile);
				caCertificateChain = x509Chain;
				x509Chain = null;
			}
			finally
			{
				x509Chain?.Dispose();
			}
		}
		RemoteCertificateValidationCallback userCertificateValidationCallback = ValidateRemoteCertificate;
		if (connection.RemoteCertificateValidationCallback != null)
		{
			if (caCertificateChain != null)
			{
				Log.NotUsingRemoteCertificateValidationCallbackDueToSslCa(m_logger, Id);
			}
			else
			{
				MySqlSslMode sslMode = cs.SslMode;
				if (sslMode != MySqlSslMode.Preferred && sslMode != MySqlSslMode.Required)
				{
					Log.NotUsingRemoteCertificateValidationCallbackDueToSslMode(m_logger, Id, cs.SslMode);
				}
				else
				{
					Log.UsingRemoteCertificateValidationCallback(m_logger, Id);
					userCertificateValidationCallback = connection.RemoteCertificateValidationCallback;
				}
			}
		}
		SslStream sslStream = ((clientCertificates == null) ? new SslStream(m_stream, leaveInnerStreamOpen: false, userCertificateValidationCallback) : new SslStream(m_stream, leaveInnerStreamOpen: false, userCertificateValidationCallback, ValidateLocalCertificate));
		bool checkCertificateRevocation = cs.SslMode == MySqlSslMode.VerifyFull;
		using (PayloadData initSsl = HandshakeResponse41Payload.CreateWithSsl(serverCapabilities, cs, m_compressionMethod, m_characterSet))
		{
			await SendReplyAsync(initSsl, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		SslClientAuthenticationOptions sslClientAuthenticationOptions = new SslClientAuthenticationOptions
		{
			EnabledSslProtocols = sslProtocols,
			ClientCertificates = clientCertificates,
			TargetHost = HostName,
			CertificateRevocationCheckMode = (checkCertificateRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck)
		};
		try
		{
			if (ioBehavior == IOBehavior.Asynchronous)
			{
				await sslStream.AuthenticateAsClientAsync(sslClientAuthenticationOptions.TargetHost, sslClientAuthenticationOptions.ClientCertificates, sslClientAuthenticationOptions.EnabledSslProtocols, checkCertificateRevocation).ConfigureAwait(continueOnCapturedContext: false);
			}
			else
			{
				sslStream.AuthenticateAsClient(sslClientAuthenticationOptions.TargetHost, sslClientAuthenticationOptions.ClientCertificates, sslClientAuthenticationOptions.EnabledSslProtocols, checkCertificateRevocation);
			}
			StreamByteHandler byteHandler = new StreamByteHandler(sslStream);
			m_payloadHandler.ByteHandler = byteHandler;
			m_isSecureConnection = true;
			m_sslStream = sslStream;
			if (m_sslPolicyErrors != SslPolicyErrors.None)
			{
				Log.ConnectedTlsDetailedPreliminary(m_logger, Id, m_sslPolicyErrors, sslStream.SslProtocol, sslStream.CipherAlgorithm, sslStream.HashAlgorithm, sslStream.KeyExchangeAlgorithm, sslStream.KeyExchangeStrength);
			}
			else
			{
				Log.ConnectedTlsDetailed(m_logger, Id, sslStream.SslProtocol, sslStream.CipherAlgorithm, sslStream.HashAlgorithm, sslStream.KeyExchangeAlgorithm, sslStream.KeyExchangeStrength);
			}
		}
		catch (Exception ex5)
		{
			if (!(ex5 is OperationCanceledException))
			{
				Log.CouldNotInitializeTlsConnection(m_logger, ex5, Id);
			}
			sslStream.Dispose();
			ShutdownSocket();
			HostName = "";
			lock (m_lock)
			{
				m_state = State.Failed;
			}
			if (ex5 is AuthenticationException)
			{
				throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "SSL Authentication Error", ex5);
			}
			if (ex5 is IOException && clientCertificates != null)
			{
				throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "MySQL Server rejected client certificate", ex5);
			}
			if (ex5 is Win32Exception { NativeErrorCode: -2146893007 })
			{
				throw new MySqlException(MySqlErrorCode.UnableToConnectToHost, "The server doesn't support the client's specified TLS versions.", ex5);
			}
			throw;
		}
		finally
		{
			caCertificateChain?.Dispose();
		}
		static X509CertificateCollection LoadCertificate(string sslKeyFile, string sslCertificateFile)
		{
			throw new NotSupportedException("SslCert and SslKey connection string options are not supported in netstandard2.0.");
		}
		static X509Certificate ValidateLocalCertificate(object lcbSender, string lcbTargetHost, X509CertificateCollection lcbLocalCertificates, X509Certificate? lcbRemoteCertificate, string[] lcbAcceptableIssuers)
		{
			return lcbLocalCertificates[0];
		}
		bool ValidateRemoteCertificate(object rcbSender, X509Certificate? rcbCertificate, X509Chain? rcbChain, SslPolicyErrors rcbPolicyErrors)
		{
			MySqlSslMode sslMode2 = cs.SslMode;
			if ((uint)(sslMode2 - 1) <= 1u)
			{
				return true;
			}
			if ((rcbPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None && rcbChain != null && caCertificateChain != null)
			{
				X509Chain x509Chain2 = caCertificateChain;
				X509ChainElementCollection chainElements = rcbChain.ChainElements;
				if (x509Chain2.Build(chainElements[chainElements.Count - 1].Certificate) && caCertificateChain.ChainStatus.Length != 0 && caCertificateChain.ChainStatus[0].Status == X509ChainStatusFlags.UntrustedRoot)
				{
					X509ChainElementCollection chainElements2 = caCertificateChain.ChainElements;
					X509Certificate2 certificate2 = chainElements2[chainElements2.Count - 1].Certificate;
					X509Certificate2Enumerator enumerator = caCertificateChain.ChainPolicy.ExtraStore.GetEnumerator();
					while (enumerator.MoveNext())
					{
						X509Certificate2 current = enumerator.Current;
						if (certificate2.RawData.AsSpan().SequenceEqual(current.RawData))
						{
							rcbPolicyErrors &= ~SslPolicyErrors.RemoteCertificateChainErrors;
							break;
						}
					}
				}
			}
			if (cs.SslMode == MySqlSslMode.VerifyCA)
			{
				rcbPolicyErrors &= ~SslPolicyErrors.RemoteCertificateNameMismatch;
			}
			if (rcbCertificate is X509Certificate2 x509Certificate2)
			{
				using SHA256 sHA = SHA256.Create();
				m_remoteCertificateSha2Thumbprint = sHA.ComputeHash(x509Certificate2.RawData);
				m_sslPolicyErrors = rcbPolicyErrors;
				return true;
			}
			return rcbPolicyErrors == SslPolicyErrors.None;
		}
	}

	private bool ShouldGetRealServerDetails(ConnectionSettings cs)
	{
		if (UseOracleMode)
		{
			return true;
		}
		bool flag;
		switch (ServerVersion.OriginalString)
		{
		case "5.6.47.0":
		case "5.6.42.0":
		case "5.6.39.0":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			return true;
		}
		if (cs.ConnectionProtocol == MySqlConnectionProtocol.Sockets && Enumerable.Contains(cs.UserID, '@'))
		{
			if (!HostName.EndsWith(".mysql.database.azure.com", StringComparison.OrdinalIgnoreCase) && !HostName.EndsWith(".database.windows.net", StringComparison.OrdinalIgnoreCase))
			{
				return HostName.EndsWith(".mysql.database.chinacloudapi.cn", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
		if (HostName.EndsWith(".rds.amazonaws.com", StringComparison.OrdinalIgnoreCase) && HostName.AsSpan().Contains(".proxy-".AsSpan(), StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		return false;
	}

	private async Task GetRealServerDetailsAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		Log.DetectedProxy(m_logger, Id);
		try
		{
			PayloadData payload = ((!SupportsQueryAttributes) ? (UseOracleMode ? s_selectConnectionIdVersionOracleNoAttributesPayload : s_selectConnectionIdVersionNoAttributesPayload) : (UseOracleMode ? s_selectConnectionIdVersionOracleWithAttributesPayload : s_selectConnectionIdVersionWithAttributesPayload));
			await SendAsync(payload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await ReceiveReplyAsync(ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
			await ReceiveReplyAsync(ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
			if (!SupportsDeprecateEof)
			{
				payload = await ReceiveReplyAsync(ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
				EofPayload.Create(payload.Span);
			}
			payload = await ReceiveReplyAsync(ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
			ByteArrayReader byteArrayReader = new ByteArrayReader(payload.Span);
			int num = byteArrayReader.ReadLengthEncodedIntegerOrNull();
			long? connectionId = null;
			if (num != -1)
			{
				ReadOnlySpan<byte> source = byteArrayReader.ReadByteString(num);
				if (Utf8Parser.TryParse(source, out long value, out int bytesConsumed, '\0') && bytesConsumed == source.Length)
				{
					connectionId = value;
				}
			}
			num = byteArrayReader.ReadLengthEncodedIntegerOrNull();
			ServerVersion serverVersion = ((num != -1) ? new ServerVersion(byteArrayReader.ReadByteString(num)) : null);
			payload = await ReceiveReplyAsync(ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
			if (OkPayload.IsOk(payload.Span, this))
			{
				OkPayload.Verify(payload.Span, this);
			}
			else
			{
				EofPayload.Create(payload.Span);
			}
			if (serverVersion != null)
			{
				OceanBaseServerVersion = serverVersion;
			}
			if (connectionId.HasValue)
			{
				long valueOrDefault = connectionId.GetValueOrDefault();
				if (valueOrDefault >= int.MinValue && valueOrDefault <= int.MaxValue)
				{
					int connectionId2 = (int)valueOrDefault;
					Log.ChangingConnectionId(m_logger, Id, ConnectionId, connectionId2, ServerVersion.OriginalString, serverVersion?.OriginalString ?? ServerVersion.OriginalString);
					ConnectionId = connectionId2;
				}
			}
		}
		catch (MySqlException exception)
		{
			Log.FailedToGetConnectionId(m_logger, exception, Id);
		}
	}

	private void ShutdownSocket()
	{
		Log.ClosingStreamSocket(m_logger, Id);
		Utility.Dispose(ref m_payloadHandler);
		Utility.Dispose(ref m_stream);
		SafeDispose(ref m_tcpClient);
		SafeDispose(ref m_socket);
		Utility.Dispose(ref m_clientCertificate);
		m_activityTags.Clear();
	}

	/// <summary>
	/// Disposes and sets <paramref name="disposable" /> to <c>null</c>, ignoring any
	/// <see cref="T:System.IO.IOException" /> or <see cref="T:System.Net.Sockets.SocketException" /> that is thrown.
	/// </summary>
	/// <typeparam name="T">An <see cref="T:System.IDisposable" /> type.</typeparam>
	/// <param name="disposable">The object to dispose.</param>
	private static void SafeDispose<T>(ref T? disposable) where T : class, IDisposable
	{
		if (disposable != null)
		{
			try
			{
				disposable.Dispose();
			}
			catch (IOException)
			{
			}
			catch (SocketException)
			{
			}
			disposable = null;
		}
	}

	internal void SetFailed(Exception exception)
	{
		Log.SettingStateToFailed(m_logger, exception, Id);
		lock (m_lock)
		{
			m_state = State.Failed;
		}
		if (OwningConnection != null && OwningConnection.TryGetTarget(out IServerSessionOwner target))
		{
			target.SetState(ConnectionState.Broken);
		}
	}

	private void VerifyState(State state)
	{
		if (m_state != state)
		{
			ExpectedSessionState1(m_logger, Id, state, m_state);
			throw new InvalidOperationException($"Expected state to be {state} but was {m_state}.");
		}
	}

	private void VerifyState(State state1, State state2, State state3, State state4, State state5, State state6)
	{
		if (m_state != state1 && m_state != state2 && m_state != state3 && m_state != state4 && m_state != state5 && m_state != state6)
		{
			ExpectedSessionState6(m_logger, Id, state1, state2, state3, state4, state5, state6, m_state);
			throw new InvalidOperationException($"Expected state to be ({state1}|{state2}|{state3}|{state4}|{state5}|{state6}) but was {m_state}.");
		}
	}

	private byte[] CreateConnectionAttributes(string? programName)
	{
		Log.CreatingConnectionAttributes(m_logger, Id);
		ByteBufferWriter byteBufferWriter = new ByteBufferWriter();
		byteBufferWriter.WriteLengthEncodedString("__ob_client_name");
		byteBufferWriter.WriteLengthEncodedString("OceanBase Connector/NET");
		byteBufferWriter.WriteLengthEncodedString("__mysql_client_type");
		byteBufferWriter.WriteLengthEncodedString("__ob_dotnet_client");
		byteBufferWriter.WriteLengthEncodedString("__ob_client_version");
		string text = typeof(ServerSession).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
		int num = text.IndexOf('+');
		if (num != -1)
		{
			text = text.Substring(0, num);
		}
		byteBufferWriter.WriteLengthEncodedString(text);
		try
		{
			Utility.GetOSDetails(out string os, out string osDescription, out string architecture);
			if (os != null)
			{
				byteBufferWriter.WriteLengthEncodedString("__os");
				byteBufferWriter.WriteLengthEncodedString(os);
			}
			byteBufferWriter.WriteLengthEncodedString("__os_details");
			byteBufferWriter.WriteLengthEncodedString(osDescription);
			byteBufferWriter.WriteLengthEncodedString("__platform");
			byteBufferWriter.WriteLengthEncodedString(architecture);
		}
		catch (PlatformNotSupportedException)
		{
		}
		using Process process = Process.GetCurrentProcess();
		int id = process.Id;
		byteBufferWriter.WriteLengthEncodedString("__pid");
		byteBufferWriter.WriteLengthEncodedString(id.ToString(CultureInfo.InvariantCulture));
		if (!string.IsNullOrEmpty(programName))
		{
			byteBufferWriter.WriteLengthEncodedString("program_name");
			byteBufferWriter.WriteLengthEncodedString(programName);
		}
		ulong num2 = 1504uL;
		byteBufferWriter.WriteLengthEncodedString("__proxy_capability_flag");
		byteBufferWriter.WriteLengthEncodedString(num2.ToString(CultureInfo.InvariantCulture));
		ulong num3 = 0uL;
		if (UseLobLocatorV2)
		{
			num3 = 1uL;
		}
		if (num3 != 0L)
		{
			byteBufferWriter.WriteLengthEncodedString("__ob_client_attribute_capability_flag");
			byteBufferWriter.WriteLengthEncodedString(num3.ToString(CultureInfo.InvariantCulture));
		}
		using PayloadData payloadData = byteBufferWriter.ToPayloadData();
		ReadOnlySpan<byte> span = payloadData.Span;
		ByteBufferWriter byteBufferWriter2 = new ByteBufferWriter(span.Length + 9);
		byteBufferWriter2.WriteLengthEncodedInteger((ulong)span.Length);
		byteBufferWriter2.Write(span);
		using PayloadData payloadData2 = byteBufferWriter2.ToPayloadData();
		return payloadData2.Memory.ToArray();
	}

	private MySqlException CreateExceptionForErrorPayload(ReadOnlySpan<byte> span)
	{
		ErrorPayload errorPayload = ErrorPayload.Create(span);
		Log.ErrorPayload(m_logger, Id, errorPayload.ErrorCode, errorPayload.State, errorPayload.Message);
		MySqlException ex = errorPayload.ToException();
		if (ex.ErrorCode == MySqlErrorCode.ClientInteractionTimeout)
		{
			SetFailed(ex);
		}
		return ex;
	}

	private void ClearPreparedStatements()
	{
		if (m_preparedStatements == null)
		{
			return;
		}
		foreach (KeyValuePair<string, PreparedStatements> preparedStatement in m_preparedStatements)
		{
			preparedStatement.Value.Dispose();
		}
		m_preparedStatements.Clear();
	}

	private PreparedStatements? RemovePreparedStatements(string commandText)
	{
		if (m_preparedStatements == null || !m_preparedStatements.TryGetValue(commandText, out PreparedStatements value))
		{
			return null;
		}
		m_preparedStatements.Remove(commandText);
		return value;
	}

	private string GetPassword(ConnectionSettings cs, IServerSessionOwner connection)
	{
		if (cs.Password.Length != 0)
		{
			return cs.Password;
		}
		Func<MySqlProvidePasswordContext, string> providePasswordCallback = connection.ProvidePasswordCallback;
		if (providePasswordCallback != null)
		{
			try
			{
				Log.ObtainingPasswordViaProvidePasswordCallback(m_logger, Id);
				return providePasswordCallback(new MySqlProvidePasswordContext(HostName, cs.Port, cs.UserID, cs.Database));
			}
			catch (Exception ex)
			{
				Log.FailedToObtainPassword(m_logger, ex, Id, ex.Message);
				throw new MySqlException(MySqlErrorCode.ProvidePasswordCallbackFailed, "Failed to obtain password via ProvidePasswordCallback", ex);
			}
		}
		return "";
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static void CannotExecuteNewCommandInState(ILogger logger, string sessionId, State sessionState)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__CannotExecuteNewCommandInStateCallback(logger, sessionId, sessionState, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static void EnteringFinishQuerying(ILogger logger, string sessionId, State sessionState)
	{
		if (logger.IsEnabled(LogLevel.Trace))
		{
			__EnteringFinishQueryingCallback(logger, sessionId, sessionState, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static void ExpectedSessionState1(ILogger logger, string sessionId, State expectedState1, State sessionState)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			__ExpectedSessionState1Callback(logger, sessionId, expectedState1, sessionState, null);
		}
	}
	[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.10.46610")]
	private static void ExpectedSessionState6(ILogger logger, string sessionId, State expectedState1, State expectedState2, State expectedState3, State expectedState4, State expectedState5, State expectedState6, State sessionState)
	{
		if (logger.IsEnabled(LogLevel.Error))
		{
			logger.Log(LogLevel.Error, new EventId(2016, "ExpectedSessionState6"), new __ExpectedSessionState6Struct(sessionId, expectedState1, expectedState2, expectedState3, expectedState4, expectedState5, expectedState6, sessionState), null, __ExpectedSessionState6Struct.Format);
		}
	}
}
