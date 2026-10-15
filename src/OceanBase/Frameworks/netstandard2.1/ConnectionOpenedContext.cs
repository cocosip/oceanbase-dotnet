using System.Data.Common;

namespace OceanBase;

/// <summary>
/// Contains information passed to <see cref="T:OceanBase.ConnectionOpenedCallback" /> when a new <see cref="T:OceanBase.MySqlConnection" /> is opened.
/// </summary>
public sealed class ConnectionOpenedContext
{
	/// <summary>
	/// The <see cref="T:OceanBase.MySqlConnection" /> that was opened.
	/// </summary>
	public DbConnection Connection { get; }

	/// <summary>
	/// Bitflags giving the conditions under which a connection was opened.
	/// </summary>
	public ConnectionOpenedConditions Conditions { get; }

	internal ConnectionOpenedContext(DbConnection connection, ConnectionOpenedConditions conditions)
	{
		Connection = connection;
		Conditions = conditions;
	}
}
