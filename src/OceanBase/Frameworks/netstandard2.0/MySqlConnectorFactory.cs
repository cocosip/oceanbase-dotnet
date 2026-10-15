using System.Data.Common;

namespace OceanBase;

/// <summary>
/// An implementation of <see cref="T:System.Data.Common.DbProviderFactory" /> that creates MySqlConnector objects.
/// </summary>
public sealed class MySqlConnectorFactory : DbProviderFactory
{
	/// <summary>
	/// Provides an instance of <see cref="T:System.Data.Common.DbProviderFactory" /> that can create MySqlConnector objects.
	/// </summary>
	public static readonly MySqlConnectorFactory Instance = new MySqlConnectorFactory();

	/// <summary>
	/// Returns <c>false</c>.
	/// </summary>
	/// <remarks><see cref="T:System.Data.Common.DbDataSourceEnumerator" /> is not supported by OceanBase.</remarks>
	public override bool CanCreateDataSourceEnumerator => false;

	/// <summary>
	/// Returns <c>true</c>.
	/// </summary>
	public bool CanCreateBatch => true;

	/// <summary>
	/// Creates a new <see cref="T:OceanBase.MySqlCommand" /> object.
	/// </summary>
	public override DbCommand CreateCommand()
	{
		return new MySqlCommand();
	}

	/// <summary>
	/// Creates a new <see cref="T:OceanBase.MySqlConnection" /> object.
	/// </summary>
	public override DbConnection CreateConnection()
	{
		return new MySqlConnection();
	}

	/// <summary>
	/// Creates a new <see cref="T:OceanBase.MySqlConnectionStringBuilder" /> object.
	/// </summary>
	public override DbConnectionStringBuilder CreateConnectionStringBuilder()
	{
		return new MySqlConnectionStringBuilder();
	}

	/// <summary>
	/// Creates a new <see cref="T:OceanBase.MySqlParameter" /> object.
	/// </summary>
	public override DbParameter CreateParameter()
	{
		return new MySqlParameter();
	}

	/// <summary>
	/// Creates a new <see cref="T:OceanBase.MySqlCommandBuilder" /> object.
	/// </summary>
	public override DbCommandBuilder CreateCommandBuilder()
	{
		return new MySqlCommandBuilder();
	}

	/// <summary>
	/// Creates a new <see cref="T:OceanBase.MySqlDataAdapter" /> object.
	/// </summary>
	public override DbDataAdapter CreateDataAdapter()
	{
		return new MySqlDataAdapter();
	}

	/// <summary>
	/// Creates a new <see cref="T:OceanBase.MySqlBatch" /> object.
	/// </summary>
	public MySqlBatch CreateBatch()
	{
		return new MySqlBatch();
	}

	/// <summary>
	/// Creates a new <see cref="T:OceanBase.MySqlBatchCommand" /> object.
	/// </summary>
	public MySqlBatchCommand CreateBatchCommand()
	{
		return new MySqlBatchCommand();
	}

	/// <summary>
	/// Creates a new <see cref="T:OceanBase.MySqlDataSource" /> object.
	/// </summary>
	/// <param name="connectionString">The connection string.</param>
	public DbDataSource CreateDataSource(string connectionString)
	{
		return new MySqlDataSource(connectionString);
	}

	private MySqlConnectorFactory()
	{
	}
}
