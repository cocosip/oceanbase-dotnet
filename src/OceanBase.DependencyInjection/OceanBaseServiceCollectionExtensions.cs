using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OceanBase.DependencyInjection;

/// <summary>
/// <see cref="IServiceCollection"/> extensions for registering OceanBase data sources.
/// </summary>
public static class OceanBaseServiceCollectionExtensions
{
	/// <summary>
	/// Registers a <see cref="MySqlDataSource"/> (OceanBase MySQL mode) built from
	/// <paramref name="connectionString"/> as a singleton.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">
	/// A MySqlConnector-style connection string, e.g.
	/// <c>Server=host;Port=2881;Database=db;Uid=user@tenant#cluster;Pwd=pw;</c>
	/// </param>
	/// <param name="configure">Optional callback to further configure the data source builder.</param>
	public static IServiceCollection AddOceanBase(
		this IServiceCollection services,
		string connectionString,
		Action<MySqlDataSourceBuilder>? configure = null)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));
		if (string.IsNullOrWhiteSpace(connectionString))
			throw new ArgumentException("Connection string must not be null or empty.", nameof(connectionString));

		var builder = new MySqlDataSourceBuilder(connectionString);
		configure?.Invoke(builder);
		services.AddSingleton(builder.Build());
		return services;
	}

	/// <summary>
	/// Registers a <see cref="MySqlDataSource"/> using the connection string named
	/// <paramref name="connectionStringName"/> (defaults to <c>OceanBase</c>).
	/// </summary>
	public static IServiceCollection AddOceanBase(
		this IServiceCollection services,
		IConfiguration configuration,
		string connectionStringName = "OceanBase",
		Action<MySqlDataSourceBuilder>? configure = null)
	{
		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));

		var connectionString = configuration.GetConnectionString(connectionStringName);
		if (string.IsNullOrWhiteSpace(connectionString))
			throw new InvalidOperationException($"Connection string '{connectionStringName}' was not found in configuration.");

		return services.AddOceanBase(connectionString, configure);
	}

	/// <summary>
	/// Registers an <see cref="OracleDataSource"/> (OceanBase Oracle mode) built from
	/// <paramref name="connectionString"/> as a singleton.
	/// </summary>
	/// <remarks>
	/// OceanBase Oracle mode requires <c>Database</c> (the schema name) in the connection string,
	/// e.g. <c>Data Source=host:2883;User Id=user@tenant#cluster;Password=pw;Database=schema;</c>
	/// </remarks>
	public static IServiceCollection AddOceanBaseOracle(
		this IServiceCollection services,
		string connectionString,
		Action<OracleDataSourceBuilder>? configure = null)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));
		if (string.IsNullOrWhiteSpace(connectionString))
			throw new ArgumentException("Connection string must not be null or empty.", nameof(connectionString));

		var builder = new OracleDataSourceBuilder(connectionString);
		configure?.Invoke(builder);
		services.AddSingleton(builder.Build());
		return services;
	}

	/// <summary>
	/// Registers an <see cref="OracleDataSource"/> using the connection string named
	/// <paramref name="connectionStringName"/> (defaults to <c>OceanBaseOracle</c>).
	/// </summary>
	public static IServiceCollection AddOceanBaseOracle(
		this IServiceCollection services,
		IConfiguration configuration,
		string connectionStringName = "OceanBaseOracle",
		Action<OracleDataSourceBuilder>? configure = null)
	{
		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));

		var connectionString = configuration.GetConnectionString(connectionStringName);
		if (string.IsNullOrWhiteSpace(connectionString))
			throw new InvalidOperationException($"Connection string '{connectionStringName}' was not found in configuration.");

		return services.AddOceanBaseOracle(connectionString, configure);
	}

	/// <summary>
	/// Registers a scoped <see cref="MySqlConnection"/> (OceanBase MySQL mode) using the
	/// singleton <see cref="MySqlDataSource"/> registered by <see cref="AddOceanBase(IServiceCollection, string, Action{MySqlDataSourceBuilder}?)"/>.
	/// </summary>
	public static IServiceCollection AddOceanBaseConnection(this IServiceCollection services)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		services.AddScoped(static sp => sp.GetRequiredService<MySqlDataSource>().CreateConnection());
		return services;
	}

	/// <summary>
	/// Registers a scoped <see cref="OracleConnection"/> (OceanBase Oracle mode) using the
	/// singleton <see cref="OracleDataSource"/> registered by <see cref="AddOceanBaseOracle(IServiceCollection, string, Action{OracleDataSourceBuilder}?)"/>.
	/// </summary>
	public static IServiceCollection AddOceanBaseOracleConnection(this IServiceCollection services)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		services.AddScoped(static sp => sp.GetRequiredService<OracleDataSource>().CreateConnection());
		return services;
	}
}
