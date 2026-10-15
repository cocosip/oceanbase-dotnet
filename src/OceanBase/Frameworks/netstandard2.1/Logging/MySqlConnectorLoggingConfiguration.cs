using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OceanBase.Logging;

internal sealed class MySqlConnectorLoggingConfiguration(ILoggerFactory loggerFactory)
{
	public ILogger DataSourceLogger { get; } = loggerFactory.CreateLogger("OceanBase.MySqlDataSource");

	public ILogger ConnectionLogger { get; } = loggerFactory.CreateLogger("OceanBase.MySqlConnection");

	public ILogger CommandLogger { get; } = loggerFactory.CreateLogger("OceanBase.MySqlCommand");

	public ILogger PoolLogger { get; } = loggerFactory.CreateLogger("OceanBase.ConnectionPool");

	public ILogger BulkCopyLogger { get; } = loggerFactory.CreateLogger("OceanBase.MySqlBulkCopy");

	public ILogger TransactionLogger { get; } = loggerFactory.CreateLogger("OceanBase.Transaction");

	public static MySqlConnectorLoggingConfiguration NullConfiguration { get; } = new MySqlConnectorLoggingConfiguration(NullLoggerFactory.Instance);

	public static MySqlConnectorLoggingConfiguration GlobalConfiguration { get; set; } = NullConfiguration;
}
