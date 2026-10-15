using System;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace OceanBase.DependencyInjection.Tests;

public sealed class OceanBaseServiceCollectionExtensionsTests
{
	private const string MySqlConnectionString = "Server=obhost;Port=2881;User ID=root@sys#ob;Password=pw;Database=db;";
	private const string OracleConnectionString = "Server=obhost;Port=2883;User Id=u@t#c;Password=pw;Database=schema;";

	[Fact]
	public void AddOceanBase_registers_MySqlDataSource_as_singleton()
	{
		var services = new ServiceCollection();
		services.AddOceanBase(MySqlConnectionString);

		using var provider = services.BuildServiceProvider();
		var first = provider.GetRequiredService<MySqlDataSource>();
		var second = provider.GetRequiredService<MySqlDataSource>();

		Assert.Same(first, second);
		Assert.Contains("obhost", first.ConnectionString);
	}

	[Fact]
	public void AddOceanBase_applies_configure_callback()
	{
		var services = new ServiceCollection();
		services.AddOceanBase(MySqlConnectionString, b => b.UseName("my-ob"));

		using var provider = services.BuildServiceProvider();
		var ds = provider.GetRequiredService<MySqlDataSource>();

		Assert.NotNull(ds);
	}

	[Fact]
	public void AddOceanBaseOracle_registers_OracleDataSource()
	{
		var services = new ServiceCollection();
		services.AddOceanBaseOracle(OracleConnectionString);

		using var provider = services.BuildServiceProvider();
		var ds = provider.GetRequiredService<OracleDataSource>();

		Assert.Contains("obhost", ds.ConnectionString);
	}

	[Fact]
	public void AddOceanBaseConnection_registers_scoped_connection()
	{
		var services = new ServiceCollection();
		services.AddOceanBase(MySqlConnectionString);
		services.AddOceanBaseConnection();

		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();
		var conn = scope.ServiceProvider.GetRequiredService<MySqlConnection>();

		Assert.NotNull(conn);
		Assert.Equal(System.Data.ConnectionState.Closed, conn.State);
	}

	[Fact]
	public void AddOceanBaseOracleConnection_registers_scoped_oracle_connection()
	{
		var services = new ServiceCollection();
		services.AddOceanBaseOracle(OracleConnectionString);
		services.AddOceanBaseOracleConnection();

		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();
		var conn = scope.ServiceProvider.GetRequiredService<OracleConnection>();

		Assert.NotNull(conn);
	}

	[Fact]
	public void AddOceanBase_rejects_empty_connection_string()
	{
		var services = new ServiceCollection();
		Assert.Throws<ArgumentException>(() => services.AddOceanBase("   "));
	}

	[Fact]
	public void AddOceanBase_rejects_null_services()
	{
		IServiceCollection? services = null;
		Assert.Throws<ArgumentNullException>(() => services!.AddOceanBase(MySqlConnectionString));
	}
}
