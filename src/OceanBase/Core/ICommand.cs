using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;

namespace OceanBase.Core;

/// <summary>
/// <see cref="T:OceanBase.Core.ICommand" /> provides a common abstraction over <see cref="T:OceanBase.Core.IMySqlCommand" /> and <see cref="T:OceanBase.Core.IOracleCommand" />.
/// </summary>
internal interface ICommand
{
	string? CommandText { get; }

	CommandType CommandType { get; }

	bool AllowUserVariables { get; }

	CommandBehavior CommandBehavior { get; }

	DbParameterCollection? RawParameters { get; }

	MySqlAttributeCollection? RawAttributes { get; }

	DbConnection? Connection { get; }

	long LastInsertedId { get; }

	DbParameterCollection? OutParameters { get; set; }

	DbParameter? ReturnParameter { get; set; }

	ICancellableCommand CancellableCommand { get; }

	ILogger Logger { get; }

	PreparedStatements? TryGetPreparedStatements();

	void SetLastInsertedId(long lastInsertedId);
}
