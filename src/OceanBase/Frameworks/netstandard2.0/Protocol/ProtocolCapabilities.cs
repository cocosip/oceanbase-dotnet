using System;

namespace OceanBase.Protocol;

/// <summary>
/// The <a href="https://dev.mysql.com/doc/internals/en/capability-flags.html">MySQL Capability flags</a>.
/// </summary>
[Flags]
internal enum ProtocolCapabilities : ulong
{
	/// <summary>
	/// No specified capabilities.
	/// </summary>
	None = 0uL,
	/// <summary>
	/// Use the improved version of Old Password Authentication.
	/// </summary>
	LongPassword = 1uL,
	/// <summary>
	/// Send found rows instead of affected rows in EOF_Packet.
	/// </summary>
	FoundRows = 2uL,
	/// <summary>
	/// Longer flags in Protocol::ColumnDefinition320.
	/// </summary>
	LongFlag = 4uL,
	/// <summary>
	/// Database (schema) name can be specified on connect in Handshake Response Packet.
	/// </summary>
	ConnectWithDatabase = 8uL,
	/// <summary>
	/// Do not permit database.table.column.
	/// </summary>
	NoSchema = 0x10uL,
	/// <summary>
	/// Supports compression.
	/// </summary>
	Compress = 0x20uL,
	/// <summary>
	/// Special handling of ODBC behavior.
	/// </summary>
	Odbc = 0x40uL,
	/// <summary>
	/// Enables the LOCAL INFILE request of LOAD DATA|XML.
	/// </summary>
	LocalFiles = 0x80uL,
	/// <summary>
	/// Parser can ignore spaces before '('.
	/// </summary>
	IgnoreSpace = 0x100uL,
	/// <summary>
	/// Supports the 4.1 protocol.
	/// </summary>
	Protocol41 = 0x200uL,
	/// <summary>
	/// Server: Supports interactive and noninteractive clients. Client: The session <code>wait_timeout</code> variable is set to the value of the session <code>interactive_timeout</code> variable.
	/// </summary>
	Interactive = 0x400uL,
	/// <summary>
	/// Supports SSL.
	/// </summary>
	Ssl = 0x800uL,
	IgnoreSigpipe = 0x1000uL,
	/// <summary>
	/// Can send status flags in EOF_Packet.
	/// </summary>
	Transactions = 0x2000uL,
	/// <summary>
	/// Supports Authentication::Native41.
	/// </summary>
	SecureConnection = 0x8000uL,
	/// <summary>
	/// Can handle multiple statements per COM_QUERY and COM_STMT_PREPARE.
	/// </summary>
	MultiStatements = 0x10000uL,
	/// <summary>
	/// Can send multiple resultsets for COM_QUERY.
	/// </summary>
	MultiResults = 0x20000uL,
	/// <summary>
	/// Can send multiple resultsets for COM_STMT_EXECUTE.
	/// </summary>
	PreparedStatementMultiResults = 0x40000uL,
	/// <summary>
	/// Sends extra data in Initial Handshake Packet and supports the pluggable authentication protocol.
	/// </summary>
	PluginAuth = 0x80000uL,
	/// <summary>
	/// Permits connection attributes in Protocol::HandshakeResponse41.
	/// </summary>
	ConnectionAttributes = 0x100000uL,
	/// <summary>
	/// Understands length-encoded integer for auth response data in Protocol::HandshakeResponse41.
	/// </summary>
	PluginAuthLengthEncodedClientData = 0x200000uL,
	/// <summary>
	/// Announces support for expired password extension.
	/// </summary>
	CanHandleExpiredPasswords = 0x400000uL,
	/// <summary>
	/// Can set SERVER_SESSION_STATE_CHANGED in the Status Flags and send session-state change data after a OK packet.
	/// </summary>
	SessionTrack = 0x800000uL,
	/// <summary>
	/// Can send OK after a Text Resultset.
	/// </summary>
	DeprecateEof = 0x1000000uL,
	/// <summary>
	/// The client can handle optional metadata information in the resultset.
	/// </summary>
	/// <remarks>Corresponds to CLIENT_OPTIONAL_RESULTSET_METADATA.</remarks>
	ClientOptionalResultsetMetadata = 0x2000000uL,
	/// <summary>
	/// The client supports the Zstandard compression algorithm.
	/// </summary>
	/// <remarks>Corresponds to CLIENT_ZSTD_COMPRESSION_ALGORITHM.</remarks>
	ZstandardCompressionAlgorithm = 0x4000000uL,
	/// <summary>
	/// Supports query attributes (CLIENT_QUERY_ATTRIBUTES).
	/// OceanBase oracle packet, client need set it to 1 to use oracle mode.
	/// </summary>
	QueryAttributes = 0x8000000uL,
	/// <summary>
	/// Client supports lob locator.
	/// </summary>
	ClientSupportLobLocator = 0x20000000uL,
	/// <summary>
	/// Client supports progress indicator.
	/// </summary>
	MariaDbClientProgress = 0x100000000uL,
	/// <summary>
	/// Client supports COM_MULTI (i.e., CommandKind.Multi)
	/// </summary>
	MariaDbComMulti = 0x200000000uL,
	/// <summary>
	/// Support of array binding.
	/// </summary>
	MariaDbStatementBulkOperations = 0x400000000uL,
	/// <summary>
	/// metadata extended information
	/// </summary>
	MariaDbExtendedTypeInfo = 0x800000000uL,
	/// <summary>
	/// Client supports caching metadata for prepared statements (MARIADB_CLIENT_CACHE_METADATA).
	/// </summary>
	MariaDbCacheMetadata = 0x1000000000uL
}
