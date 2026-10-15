using System.Data.Common;

namespace OceanBase;

/// <summary>
/// An implementation of <see cref="T:System.Data.Common.DbProviderFactory" /> that creates OracleClient objects.
/// </summary>
public sealed class OracleClientFactory : DbProviderFactory
{
	/// <summary>
	/// Provides an instance of <see cref="T:System.Data.Common.DbProviderFactory" /> that can create Oracle-mode objects.
	/// </summary>
	public static readonly OracleClientFactory Instance = new OracleClientFactory();

	/// <summary>
	/// Returns <c>false</c>.
	/// </summary>
	/// <remarks><see cref="T:System.Data.Common.DbDataSourceEnumerator" /> is not supported by OceanBase.</remarks>
	public override bool CanCreateDataSourceEnumerator => false;

	/// <summary>
	/// Returns <c>true</c>.
	/// </summary>
	public override bool CanCreateCommandBuilder => true;

	/// <summary>
	/// Returns <c>true</c>.
	/// </summary>
	public override bool CanCreateDataAdapter => true;

	/// <summary>
	/// Returns <c>true</c>.
	/// </summary>
	public override bool CanCreateBatch => true;

	/// <summary>
	/// Creates a new <see cref="T:OceanBase.OracleCommand" /> object.
	/// </summary>
	public override DbCommand CreateCommand()
	{
		return new OracleCommand();
	}

	/// <summary>
	/// Creates a new <see cref="T:OceanBase.OracleConnection" /> object.
	/// </summary>
	public override DbConnection CreateConnection()
	{
		return new OracleConnection();
	}

	/// <summary>
	/// Creates a new <see cref="T:OceanBase.OracleConnectionStringBuilder" /> object.
	/// </summary>
	public override DbConnectionStringBuilder CreateConnectionStringBuilder()
	{
		return new OracleConnectionStringBuilder();
	}

	/// <summary>
	/// Creates a new <see cref="T:OceanBase.OracleParameter" /> object.
	/// </summary>
	public override DbParameter CreateParameter()
	{
		return new OracleParameter();
	}

	/// <summary>
	/// Creates a new <see cref="T:OceanBase.OracleCommandBuilder" /> object.
	/// </summary>
	public override DbCommandBuilder CreateCommandBuilder()
	{
		return new OracleCommandBuilder();
	}

	/// <summary>
	/// Creates a new <see cref="T:OceanBase.OracleDataAdapter" /> object.
	/// </summary>
	public override DbDataAdapter CreateDataAdapter()
	{
		return new OracleDataAdapter();
	}

	/// <summary>
	/// Creates a new <see cref="T:OceanBase.OracleDataSource" /> object.
	/// </summary>
	/// <param name="connectionString">The connection string.</param>
	public override DbDataSource CreateDataSource(string connectionString)
	{
		return new OracleDataSource(connectionString);
	}

	private OracleClientFactory()
	{
	}
}
