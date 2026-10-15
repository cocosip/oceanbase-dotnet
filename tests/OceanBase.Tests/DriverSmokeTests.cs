using System;
using System.Data.Common;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 冒烟测试：验证重建驱动的基础行为与原版一致。
/// 这些断言依据官方文档与对原始程序集的实测结果编写。
/// </summary>
public sealed class DriverSmokeTests
{
	[Fact]
	public void MySql_connection_string_parses_odp_style_user()
	{
		var b = new MySqlConnectionStringBuilder("Server=obhost;Port=2881;User ID=root@sys#obcluster;Password=pw;Database=db;");
		Assert.Equal("obhost", b.Server);
		Assert.Equal(2881u, b.Port);
		Assert.Equal("root@sys#obcluster", b.UserID);
		Assert.Equal("db", b.Database);
	}

	[Fact]
	public void Oracle_connection_string_parses_three_part_user()
	{
		var b = new OracleConnectionStringBuilder("Server=obhost;Port=2883;Database=schema;User Id=u@tenant#cluster;Password=pw;");
		Assert.Equal("obhost", b.Server);
		Assert.Equal(2883u, b.Port);
		Assert.Equal("schema", b.Database);
		Assert.Equal("u@tenant#cluster", b.UserID);
	}

	[Fact]
	public void Documented_defaults_match_the_original_assembly()
	{
		var o = new OracleConnectionStringBuilder();
		Assert.Equal(MySqlSslMode.None, o.SslMode);
		Assert.True(o.Pooling);
		Assert.False(o.ConnectionReset);
		Assert.True(o.AutoCommit);
		Assert.True(o.UseLobLocatorV2);
		Assert.Equal(100u, o.MaximumPoolSize);
		Assert.Equal(15u, o.ConnectionTimeout);
	}

	[Fact]
	public void Oracle_mode_requires_database_schema()
	{
		using var conn = new OracleConnection("Server=127.0.0.1;Port=2883;User Id=u@t;Password=pw;");
		var ex = Assert.Throws<ArgumentException>(() => conn.Open());
		Assert.Contains("Database", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void DataSource_builders_produce_working_data_sources()
	{
		using var mds = new MySqlDataSourceBuilder("Server=h;Port=2881;User ID=u;Password=p;Database=d;").Build();
		Assert.Equal("MySqlDataSource", mds.GetType().Name);

		using var ods = new OracleDataSourceBuilder("Server=h;Port=2883;User Id=u;Password=p;Database=s;").Build();
		Assert.Equal("OracleDataSource", ods.GetType().Name);
	}

	[Fact]
	public void Tls_version_regex_accepts_valid_and_rejects_invalid()
	{
		// 对应反编译中 [GeneratedRegex] 改写为普通 Regex 的那处逻辑
		var ok = new OracleConnectionStringBuilder { TlsVersion = "TLSv1.2" };
		Assert.Equal("TLS 1.2", ok.TlsVersion);

		Assert.Throws<ArgumentException>(() => new OracleConnectionStringBuilder { TlsVersion = "SSLv3" });
	}

	[Fact]
	public void Connections_derive_from_DbConnection()
	{
		using var mysql = new MySqlConnection("Server=h;Port=2881;User ID=u;Password=p;Database=d;");
		Assert.IsAssignableFrom<DbConnection>(mysql);

		using var oracle = new OracleConnection("Server=h;Port=2883;User Id=u;Password=p;Database=s;");
		Assert.IsAssignableFrom<DbConnection>(oracle);
	}
}
