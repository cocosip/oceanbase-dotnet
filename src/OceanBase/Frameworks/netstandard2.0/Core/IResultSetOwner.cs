using System.Diagnostics;

namespace OceanBase.Core;

/// <summary>
/// Internal contract that <see cref="T:OceanBase.Core.ResultSet" /> uses to interact with a data reader implementation.
/// This allows sharing <see cref="T:OceanBase.Core.ResultSet" /> between <see cref="T:OceanBase.MySqlDataReader" /> and <see cref="T:OceanBase.OracleDataReader" />.
/// </summary>
internal interface IResultSetOwner
{
	Activity? Activity { get; }

	CommandFacade? Command { get; }

	ConnectionFacade? Connection { get; }

	ulong? RealRecordsAffected { get; set; }

	PreparedStatement? LastUsedPreparedStatement { get; }

	bool ExpectOraclePrepareExecuteResponse { get; }

	ServerSession? Session { get; }
}
