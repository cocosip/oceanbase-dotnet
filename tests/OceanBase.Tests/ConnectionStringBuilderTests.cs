using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using Xunit;

namespace OceanBase.Tests;

/// <summary>连接字符串构建器的单元测试（不依赖服务端）。</summary>
public sealed class ConnectionStringBuilderTests
{
	// ---------- MySQL 模式 ----------

	[Theory]
	[InlineData("Server", "host")]
	[InlineData("Host", "host")]
	[InlineData("Data Source", "host")]
	[InlineData("DataSource", "host")]
	[InlineData("Address", "host")]
	[InlineData("Addr", "host")]
	[InlineData("Network Address", "host")]
	public void MySql_server_accepts_all_documented_aliases(string keyword, string expected)
	{
		var b = new MySqlConnectionStringBuilder($"{keyword}=host;");
		Assert.Equal(expected, b.Server);
	}

	[Theory]
	[InlineData("User ID")]
	[InlineData("UserID")]
	[InlineData("Username")]
	[InlineData("Uid")]
	[InlineData("User name")]
	[InlineData("User")]
	public void MySql_user_accepts_all_documented_aliases(string keyword)
	{
		var b = new MySqlConnectionStringBuilder($"{keyword}=alice;");
		Assert.Equal("alice", b.UserID);
	}

	[Theory]
	[InlineData("Password")]
	[InlineData("pwd")]
	public void MySql_password_accepts_all_documented_aliases(string keyword)
	{
		var b = new MySqlConnectionStringBuilder($"{keyword}=secret;");
		Assert.Equal("secret", b.Password);
	}

	[Theory]
	[InlineData("Database")]
	[InlineData("Initial Catalog")]
	public void MySql_database_accepts_all_documented_aliases(string keyword)
	{
		var b = new MySqlConnectionStringBuilder($"{keyword}=mydb;");
		Assert.Equal("mydb", b.Database);
	}

	[Fact]
	public void MySql_keywords_are_case_insensitive()
	{
		var b = new MySqlConnectionStringBuilder("server=h;PORT=2881;user id=u;password=p;database=d;");
		Assert.Equal("h", b.Server);
		Assert.Equal(2881u, b.Port);
		Assert.Equal("u", b.UserID);
		Assert.Equal("d", b.Database);
	}

	[Fact]
	public void MySql_whitespace_around_key_and_value_is_tolerated()
	{
		var b = new MySqlConnectionStringBuilder("  Server = h ; Port = 2881 ; ");
		Assert.Equal("h", b.Server);
		Assert.Equal(2881u, b.Port);
	}

	[Fact]
	public void MySql_three_part_user_is_preserved_verbatim()
	{
		// ODP 三段式：用户名@租户名#集群名
		var b = new MySqlConnectionStringBuilder("Server=h;User ID=root@sys#obcluster;Password=p;");
		Assert.Equal("root@sys#obcluster", b.UserID);
	}

	[Fact]
	public void MySql_unknown_keyword_is_rejected()
	{
		// DbConnectionStringBuilder 的默认行为：未知关键字抛 ArgumentException
		Assert.Throws<ArgumentException>(() => new MySqlConnectionStringBuilder("Server=h;Definitely Not A Keyword=1;"));
	}

	[Fact]
	public void MySql_defaults_match_documented_values()
	{
		var b = new MySqlConnectionStringBuilder();
		Assert.Equal("", b.Server);
		Assert.Equal(3306u, b.Port);
		Assert.Equal(MySqlSslMode.None, b.SslMode);
		Assert.True(b.Pooling);
		Assert.False(b.ConnectionReset);
		Assert.Equal(100u, b.MaximumPoolSize);
		Assert.Equal(15u, b.ConnectionTimeout);
	}

	[Fact]
	public void MySql_oracle_specific_defaults_match_the_original()
	{
		var b = new MySqlConnectionStringBuilder();
		Assert.True(b.AutoCommit);
		Assert.True(b.UseLobLocatorV2);
		Assert.False(b.UseArrayBinding);
	}

	[Fact]
	public void MySql_connection_string_roundtrips()
	{
		const string cs = "Server=obhost;Port=2881;User ID=root@sys#ob;Password=pw;Database=db";
		var b = new MySqlConnectionStringBuilder(cs);
		var again = new MySqlConnectionStringBuilder(b.ConnectionString);
		Assert.Equal(b.ConnectionString, again.ConnectionString);
		Assert.Equal("obhost", again.Server);
		Assert.Equal(2881u, again.Port);
	}

	[Fact]
	public void MySql_clear_resets_all_values()
	{
		var b = new MySqlConnectionStringBuilder("Server=h;Port=2881;Database=d;");
		b.Clear();
		Assert.Equal("", b.Server);
		Assert.Equal("", b.Database);
	}

	[Fact]
	public void MySql_remove_drops_the_key()
	{
		var b = new MySqlConnectionStringBuilder("Server=h;Database=d;");
		Assert.True(b.Remove("Database"));
		Assert.Equal("", b.Database);
	}

	// ---------- Oracle 模式 ----------

	[Fact]
	public void Oracle_parses_host_and_port_from_Server_and_Port()
	{
		var b = new OracleConnectionStringBuilder("Server=host;Port=2883;Database=schema;User Id=u;Password=p;");
		Assert.Equal("host", b.Server);
		Assert.Equal(2883u, b.Port);
		Assert.Equal("schema", b.Database);
		Assert.Equal("u", b.UserID);
	}

	[Fact]
	public void Oracle_Data_Source_with_port_keeps_full_string_in_Server()
	{
		// 实测行为：Data Source=host:port 不会拆分到 Server/Port，整串留在 Server
		var b = new OracleConnectionStringBuilder("Data Source=host:2883;User Id=u;Password=p;Database=s;");
		Assert.Equal("host:2883", b.Server);
		Assert.Equal(3306u, b.Port);
	}

	[Fact]
	public void Oracle_defaults_match_documented_values()
	{
		var b = new OracleConnectionStringBuilder();
		Assert.Equal(3306u, b.Port);
		Assert.Equal(MySqlSslMode.None, b.SslMode);
		Assert.True(b.Pooling);
		Assert.False(b.ConnectionReset);
		Assert.True(b.AutoCommit);
		Assert.True(b.UseLobLocatorV2);
		Assert.Equal(100u, b.MaximumPoolSize);
		Assert.Equal(15u, b.ConnectionTimeout);
	}

	[Theory]
	[InlineData("Character Set")]
	[InlineData("CharSet")]
	[InlineData("CharacterSet")]
	public void Oracle_character_set_accepts_all_documented_aliases(string keyword)
	{
		var b = new OracleConnectionStringBuilder($"Server=h;Database=s;User Id=u;Password=p;{keyword}=gbk;");
		Assert.Equal("gbk", b.CharacterSet);
	}

	[Fact]
	public void Oracle_three_part_user_is_preserved_verbatim()
	{
		var b = new OracleConnectionStringBuilder("Server=h;Database=s;User Id=u@tenant#cluster;Password=p;");
		Assert.Equal("u@tenant#cluster", b.UserID);
	}

	[Fact]
	public void Oracle_connection_string_roundtrips()
	{
		const string cs = "Server=obhost;Port=2883;Database=schema;User ID=u@t#c;Password=pw";
		var b = new OracleConnectionStringBuilder(cs);
		var again = new OracleConnectionStringBuilder(b.ConnectionString);
		Assert.Equal(b.ConnectionString, again.ConnectionString);
	}

	// ---------- 非法取值 ----------

	[Theory]
	[InlineData("Server=h;Port=abc;")]
	[InlineData("Server=h;Port=-1;")]
	[InlineData("Server=h;SslMode=NotAMode;")]
	[InlineData("Server=h;Guid Format=Bogus;")]
	[InlineData("Server=h;Load Balance=Bogus;")]
	[InlineData("Server=h;Connection Protocol=Bogus;")]
	public void Invalid_values_throw(string connectionString)
	{
		Assert.ThrowsAny<ArgumentException>(() => new MySqlConnectionStringBuilder(connectionString));
	}

	[Fact]
	public void Invalid_tls_version_throws()
	{
		Assert.Throws<ArgumentException>(() => new OracleConnectionStringBuilder { TlsVersion = "SSLv3" });
	}

	[Fact]
	public void Valid_tls_version_is_normalized()
	{
		var b = new OracleConnectionStringBuilder { TlsVersion = "TLSv1.2" };
		Assert.Equal("TLS 1.2", b.TlsVersion);
	}

	// ---------- 两种模式互不干扰 ----------

	[Fact]
	public void Builders_are_independent_types()
	{
		Assert.NotEqual(typeof(MySqlConnectionStringBuilder), typeof(OracleConnectionStringBuilder));
		Assert.IsAssignableFrom<DbConnectionStringBuilder>(new MySqlConnectionStringBuilder());
		Assert.IsAssignableFrom<DbConnectionStringBuilder>(new OracleConnectionStringBuilder());
	}
}
