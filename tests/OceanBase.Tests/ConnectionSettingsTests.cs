using System;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using OceanBase.Core;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// <see cref="ConnectionSettings"/> 的解析与派生逻辑测试（internal，无需服务端）。
///
/// 该类型把连接串 builder 转换成连接层真正使用的强类型设置：
/// 多主机拆分、Unix socket / 命名管道判定、TLS 版本位、Guid 格式推导、
/// 池大小校验与克隆等都在这里发生。
/// </summary>
public sealed class ConnectionSettingsTests
{
	private static ConnectionSettings Create(string connectionString)
	{
		return new ConnectionSettings(new MySqlConnectionStringBuilder(connectionString));
	}

	// ---------- 基础字段映射 ----------

	[Fact]
	public void Maps_basic_connection_options()
	{
		var settings = Create("Server=db.example.com;Port=2883;User Id=alice;Password=secret;Database=mydb;");
		Assert.Equal("db.example.com", settings.HostNames![0]);
		Assert.Equal(2883, settings.Port);
		Assert.Equal("alice", settings.UserID);
		Assert.Equal("secret", settings.Password);
		Assert.Equal("mydb", settings.Database);
		Assert.Equal(MySqlConnectionProtocol.Sockets, settings.ConnectionProtocol);
	}

	[Fact]
	public void ConnectionString_is_preserved()
	{
		var settings = Create("Server=h;Port=2883;");
		Assert.Equal(settings.ConnectionStringBuilder.ConnectionString, settings.ConnectionString);
	}

	// ---------- 多主机拆分 ----------

	[Fact]
	public void Splits_multiple_hosts_with_ports()
	{
		var settings = Create("Server=host1:3306,host2:3307,host3;");
		Assert.Equal(new[] { "host1", "host2", "host3" }, settings.HostNames!.ToArray());
		Assert.NotNull(settings.HostPorts);
		Assert.Equal(3306, settings.HostPorts!["host1"]);
		Assert.Equal(3307, settings.HostPorts["host2"]);
		Assert.False(settings.HostPorts.ContainsKey("host3"));
	}

	[Fact]
	public void Trims_whitespace_and_skips_empty_hosts()
	{
		var settings = Create("Server= host1 , , host2 ;");
		Assert.Equal(new[] { "host1", "host2" }, settings.HostNames!.ToArray());
	}

	[Fact]
	public void Host_without_valid_port_is_kept_verbatim()
	{
		// 端口非法（越界/非数字/为空）时整段当作主机名
		var settings = Create("Server=host1:0,host2:99999,host3:abc,host4:;");
		Assert.Equal(new[] { "host1:0", "host2:99999", "host3:abc", "host4:" }, settings.HostNames!.ToArray());
		Assert.Null(settings.HostPorts);
	}

	// ---------- TLS 版本 ----------

	[Fact]
	public void Default_tls_versions_come_from_platform_default()
	{
		var settings = Create("Server=h;");
		Assert.Equal(OceanBase.Utilities.Utility.GetDefaultSslProtocols(), settings.TlsVersions);
	}

	[Fact]
	public void Parses_tls_12_and_13()
	{
		var settings = Create("Server=h;TLS Version=TLSv1.2,TLSv1.3;");
		Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, settings.TlsVersions);
	}

	[Fact]
	public void Parses_tls_10_and_11()
	{
#pragma warning disable SYSLIB0039 // 测试需要断言旧协议位
		var settings = Create("Server=h;TLS Version=TLSv1.0,TLSv1.1;");
		Assert.Equal(SslProtocols.Tls | SslProtocols.Tls11, settings.TlsVersions);
#pragma warning restore SYSLIB0039
	}

	[Fact]
	public void Tls_versions_accumulate_across_the_list()
	{
		var settings = Create("Server=h;TLS Version=TLSv1.0,TLSv1.2,TLSv1.3;");
		Assert.True(settings.TlsVersions.HasFlag(SslProtocols.Tls12));
		Assert.True(settings.TlsVersions.HasFlag(SslProtocols.Tls13));
	}

	// ---------- TLS 密码套件 ----------

	[Fact]
	public void Tls_cipher_suites_parse_by_name()
	{
		var settings = Create("Server=h;TLS Cipher Suites=TLS_AES_256_GCM_SHA384;");
		Assert.NotNull(settings.TlsCipherSuites);
		Assert.Single(settings.TlsCipherSuites!);
		Assert.Equal(TlsCipherSuite.TLS_AES_256_GCM_SHA384, settings.TlsCipherSuites![0]);
	}

	[Fact]
	public void Tls_cipher_suites_accept_name_without_prefix()
	{
		// 允许省略 "TLS_" 前缀
		var settings = Create("Server=h;TLS Cipher Suites=AES_256_GCM_SHA384;");
		Assert.Equal(TlsCipherSuite.TLS_AES_256_GCM_SHA384, settings.TlsCipherSuites![0]);
	}

	[Fact]
	public void Tls_cipher_suites_accept_multiple_values()
	{
		var settings = Create("Server=h;TLS Cipher Suites=TLS_AES_128_GCM_SHA256, TLS_AES_256_GCM_SHA384;");
		Assert.Equal(2, settings.TlsCipherSuites!.Count);
	}

	[Fact]
	public void Tls_cipher_suites_are_null_when_unset()
	{
		Assert.Null(Create("Server=h;").TlsCipherSuites);
	}

	[Fact]
	public void Tls_cipher_suites_reject_unknown_value()
	{
		Assert.Throws<NotSupportedException>(() => Create("Server=h;TLS Cipher Suites=NOT_A_CIPHER;"));
	}

	// ---------- Guid 格式推导 ----------

	[Fact]
	public void Default_guid_format_becomes_char36()
	{
		Assert.Equal(MySqlGuidFormat.Char36, Create("Server=h;").GuidFormat);
	}

	[Fact]
	public void OldGuids_switches_default_to_little_endian_binary16()
	{
		Assert.Equal(MySqlGuidFormat.LittleEndianBinary16, Create("Server=h;Old Guids=true;").GuidFormat);
	}

	[Theory]
	[InlineData("Char36", MySqlGuidFormat.Char36)]
	[InlineData("Char32", MySqlGuidFormat.Char32)]
	[InlineData("Binary16", MySqlGuidFormat.Binary16)]
	[InlineData("TimeSwapBinary16", MySqlGuidFormat.TimeSwapBinary16)]
	[InlineData("LittleEndianBinary16", MySqlGuidFormat.LittleEndianBinary16)]
	public void Explicit_guid_format_is_preserved(string text, MySqlGuidFormat expected)
	{
		Assert.Equal(expected, Create($"Server=h;Guid Format={text};").GuidFormat);
	}

	[Fact]
	public void OldGuids_with_explicit_guid_format_throws()
	{
		var ex = Assert.Throws<MySqlException>(() => Create("Server=h;Old Guids=true;Guid Format=Char36;"));
		Assert.Contains("OldGuids cannot be used with GuidFormat", ex.Message);
	}

	// ---------- 池大小校验 ----------

	[Fact]
	public void Minimum_greater_than_maximum_throws()
	{
		var ex = Assert.Throws<MySqlException>(() => Create("Server=h;Minimum Pool Size=10;Maximum Pool Size=5;"));
		Assert.Contains("MaximumPoolSize must be greater than or equal to MinimumPoolSize", ex.Message);
	}

	[Fact]
	public void Equal_pool_sizes_are_allowed()
	{
		var settings = Create("Server=h;Minimum Pool Size=5;Maximum Pool Size=5;");
		Assert.Equal(5, settings.MinimumPoolSize);
		Assert.Equal(5, settings.MaximumPoolSize);
	}

	[Fact]
	public void Connection_lifetime_is_clamped_and_converted_to_milliseconds()
	{
		// 秒 → 毫秒，且上限 4294967 秒
		Assert.Equal(120_000u, Create("Server=h;Connection Lifetime=120;").ConnectionLifeTime);
		Assert.Equal(4294967u * 1000u, Create("Server=h;Connection Lifetime=99999999;").ConnectionLifeTime);
	}

	[Fact]
	public void Application_name_empty_becomes_null()
	{
		Assert.Null(Create("Server=h;").ApplicationName);
		Assert.Equal("app", Create("Server=h;Application Name=app;").ApplicationName);
	}

	[Fact]
	public void Pipelining_is_null_when_not_specified()
	{
		Assert.Null(Create("Server=h;").Pipelining);
		Assert.True(Create("Server=h;Pipelining=true;").Pipelining);
		Assert.False(Create("Server=h;Pipelining=false;").Pipelining);
	}

	[Fact]
	public void Connection_timeout_milliseconds_is_computed_lazily()
	{
		Assert.Equal(15_000, Create("Server=h;Connection Timeout=15;").ConnectionTimeoutMilliseconds);
	}

	[Fact]
	public void Connection_timeout_milliseconds_saturates_on_overflow()
	{
		// 极大的秒数乘 1000 会溢出 → 饱和到 int.MaxValue
		var settings = Create("Server=h;Connection Timeout=4294967295;");
		Assert.Equal(int.MaxValue, settings.ConnectionTimeoutMilliseconds);
	}

	// ---------- 命名管道 ----------

	[Fact]
	public void Pipe_protocol_uses_localhost_pipe_servers_for_dot()
	{
		var settings = Create("Server=.;Connection Protocol=Pipe;Pipe Name=MYPIPE;");
		Assert.Equal(MySqlConnectionProtocol.Pipe, settings.ConnectionProtocol);
		Assert.Equal("MYPIPE", settings.PipeName);
		Assert.NotNull(settings.HostNames);
		Assert.NotEmpty(settings.HostNames!);
	}

	[Fact]
	public void Pipe_protocol_uses_given_host_for_explicit_name()
	{
		var settings = Create("Server=myhost;Connection Protocol=Pipe;");
		Assert.Equal(new[] { "myhost" }, settings.HostNames!.ToArray());
	}

	[Fact]
	public void Pipe_protocol_with_load_balance_throws()
	{
		// 只有 RoundRobin（默认）被允许；其它负载均衡策略在命名管道下不支持
		var ex = Assert.Throws<NotSupportedException>(
			() => Create("Server=.;Connection Protocol=Pipe;Load Balance=FailOver;"));
		Assert.Contains("LoadBalance not supported when ConnectionProtocol=NamedPipe", ex.Message);
	}

	[Fact]
	public void Pipe_protocol_with_round_robin_is_allowed()
	{
		// RoundRobin 是默认值，也是命名管道唯一允许的策略
		var settings = Create("Server=.;Connection Protocol=Pipe;Load Balance=RoundRobin;");
		Assert.Equal(MySqlConnectionProtocol.Pipe, settings.ConnectionProtocol);
	}

	[Fact]
	public void Shared_memory_protocol_is_not_supported()
	{
		var ex = Assert.Throws<NotSupportedException>(
			() => Create("Server=h;Connection Protocol=SharedMemory;"));
		Assert.Contains("Shared Memory connections are not supported", ex.Message);
	}

	// ---------- Unix socket ----------

	[Fact]
	public void Unix_socket_protocol_requires_existing_file()
	{
		var ex = Assert.Throws<MySqlException>(
			() => Create("Server=/nonexistent/mysql.sock;Connection Protocol=UnixSocket;"));
		Assert.Contains("Cannot find Unix Socket", ex.Message);
	}

	[Fact]
	public void Unix_socket_with_load_balance_throws()
	{
		// Load Balance 非 RoundRobin 时先抛 NotSupported
		Assert.Throws<NotSupportedException>(
			() => Create("Server=/nonexistent/mysql.sock;Connection Protocol=UnixSocket;Load Balance=FailOver;"));
	}

	// ---------- CloneWith ----------

	[Fact]
	public void CloneWith_host_overrides_host_and_port()
	{
		var settings = Create("Server=original;Port=3306;User Id=u;Password=p;Database=d;");
		var clone = settings.CloneWith("newhost", 3307, "newuser");

		Assert.Equal(new[] { "newhost" }, clone.HostNames!.ToArray());
		Assert.Equal(3307, clone.Port);
		Assert.Equal("newuser", clone.UserID);
		Assert.Equal(MySqlConnectionProtocol.Sockets, clone.ConnectionProtocol);
		Assert.Null(clone.IPAddress);
		// 其余设置应被继承
		Assert.Equal("p", clone.Password);
		Assert.Equal("d", clone.Database);
	}

	[Fact]
	public void CloneWith_ip_address_sets_IPAddress()
	{
		var settings = Create("Server=original;Port=3306;User Id=u;");
		var clone = settings.CloneWith(IPAddress.Loopback);
		Assert.Equal(IPAddress.Loopback, clone.IPAddress);
	}

	[Fact]
	public void CloneWith_preserves_pooling_and_tls_settings()
	{
		var settings = Create("Server=original;Pooling=false;Maximum Pool Size=7;Ssl Mode=Required;");
		var clone = settings.CloneWith("h2", 3306, "u");
		Assert.False(clone.Pooling);
		Assert.Equal(7, clone.MaximumPoolSize);
		Assert.Equal(MySqlSslMode.Required, clone.SslMode);
	}

	// ---------- Oracle 构造 ----------

	[Fact]
	public void Oracle_ctor_copies_oracle_specific_options()
	{
		var csb = new OracleConnectionStringBuilder
		{
			Server = "h",
			Database = "schema",
			UseLobLocatorV2 = false,
			UseOraclePrepareExecute = true,
			UseOracleServerParameterMetadata = true,
			UseArrayBinding = true,
			PrepareExecuteFetchSize = 42,
			IgnorePrepareExecuteSequenceCheck = true,
			AutoCommit = false,
		};
		var settings = new ConnectionSettings(csb);

		Assert.False(settings.UseLobLocatorV2);
		Assert.True(settings.UseOraclePrepareExecute);
		Assert.True(settings.UseOracleServerParameterMetadata);
		Assert.True(settings.UseArrayBinding);
		Assert.Equal(42, settings.PrepareExecuteFetchSize);
		Assert.True(settings.IgnorePrepareExecuteSequenceCheck);
		Assert.False(settings.AutoCommit);
		Assert.Equal("schema", settings.Database);
	}

	// ---------- ConnectionAttributes ----------

	[Fact]
	public void ConnectionAttributes_default_to_null_and_are_settable()
	{
		var settings = Create("Server=h;");
		Assert.Null(settings.ConnectionAttributes);

		settings.ConnectionAttributes = new byte[] { 1, 2, 3 };
		Assert.Equal(new byte[] { 1, 2, 3 }, settings.ConnectionAttributes);
	}
}

/// <summary>
/// <see cref="ConnectionFacade"/> 的适配器测试（internal，无需服务端）。
///
/// 该结构体在 MySqlConnection / OracleConnection 与命令之间提供统一视图；
/// 未初始化的实例必须在读取属性时抛出明确异常，而不是 NullReferenceException。
/// </summary>
public sealed class ConnectionFacadeTests
{
	private static object CreateFacade(params object[] args)
	{
		var type = typeof(MySqlConnection).Assembly.GetType("OceanBase.Core.ConnectionFacade", throwOnError: true)!;
		return Activator.CreateInstance(type, args)!;
	}

	private static object? GetProperty(object facade, string name)
	{
		return facade.GetType().GetProperty(name)!.GetValue(facade);
	}

	[Fact]
	public void Facade_from_MySqlConnection_reports_its_kind()
	{
		using var connection = new MySqlConnection("Server=h;");
		var facade = CreateFacade(connection);

		Assert.True((bool)GetProperty(facade, "IsMySqlConnection")!);
		Assert.False((bool)GetProperty(facade, "IsOracleConnection")!);
		Assert.False((bool)GetProperty(facade, "IsMySqlCommand")!);
		Assert.False((bool)GetProperty(facade, "IsOracleCommand")!);
	}

	[Fact]
	public void Facade_from_OracleConnection_reports_its_kind()
	{
		using var connection = new OracleConnection("Server=h;Database=s;");
		var facade = CreateFacade(connection);

		Assert.False((bool)GetProperty(facade, "IsMySqlConnection")!);
		Assert.True((bool)GetProperty(facade, "IsOracleConnection")!);
	}

	[Fact]
	public void Facade_from_MySqlCommand_reports_its_kind()
	{
		using var command = new MySqlCommand();
		var facade = CreateFacade(command);

		Assert.True((bool)GetProperty(facade, "IsMySqlCommand")!);
		Assert.False((bool)GetProperty(facade, "IsOracleCommand")!);
	}

	[Fact]
	public void Facade_from_OracleCommand_reports_its_kind()
	{
		using var command = new OracleCommand();
		var facade = CreateFacade(command);

		Assert.False((bool)GetProperty(facade, "IsMySqlCommand")!);
		Assert.True((bool)GetProperty(facade, "IsOracleCommand")!);
	}

	[Fact]
	public void Facade_constructors_reject_null_arguments()
	{
		// 四个构造函数都做 null 检查；按参数类型显式绑定，避免 Activator 歧义
		var type = typeof(MySqlConnection).Assembly.GetType("OceanBase.Core.ConnectionFacade", throwOnError: true)!;
		var connectionType = typeof(MySqlConnection).Assembly.GetType("OceanBase.Core.IMySqlCommand", throwOnError: true)!;
		var oracleCommandType = typeof(MySqlConnection).Assembly.GetType("OceanBase.Core.IOracleCommand", throwOnError: true)!;

		foreach (var parameterType in new[] { typeof(MySqlConnection), typeof(OracleConnection), connectionType, oracleCommandType })
		{
			var ctor = type.GetConstructor(new[] { parameterType })!;
			var ex = Assert.Throws<System.Reflection.TargetInvocationException>(
				() => ctor.Invoke(new object?[] { null }));
			Assert.IsType<ArgumentNullException>(ex.InnerException);
		}
	}

	[Fact]
	public void Facade_delegates_State_to_underlying_connection()
	{
		using var connection = new MySqlConnection("Server=h;");
		var facade = CreateFacade(connection);
		Assert.Equal(ConnectionState.Closed, (ConnectionState)GetProperty(facade, "State")!);
		Assert.Equal(ConnectionState.Closed, (ConnectionState)GetProperty(facade, "ConnectionState")!);
	}

	[Fact]
	public void Facade_delegates_boolean_options_to_connection()
	{
		using var connection = new MySqlConnection("Server=h;Allow Zero DateTime=true;Treat Tiny As Boolean=false;");
		var facade = CreateFacade(connection);

		// 注意：这些属性走 GetInitializedConnectionSettings()，未 Open 时返回 null → 抛 NRE。
		// 这里注入设置后再验证委派行为。
		InjectSettings(connection, connection.ConnectionString);

		Assert.True((bool)GetProperty(facade, "AllowZeroDateTime")!);
		Assert.False((bool)GetProperty(facade, "TreatTinyAsBoolean")!);
	}

	[Fact]
	public void Facade_delegates_Database_to_connection()
	{
		using var connection = new MySqlConnection("Server=h;Database=mydb;");
		var facade = CreateFacade(connection);
		Assert.Equal("mydb", (string)GetProperty(facade, "Database")!);
	}

	[Fact]
	public void Facade_delegates_guid_format_to_connection()
	{
		using var connection = new MySqlConnection("Server=h;");
		InjectSettings(connection, "Server=h;Guid Format=Char32;");
		var facade = CreateFacade(connection);
		Assert.Equal(MySqlGuidFormat.Char32, (MySqlGuidFormat)GetProperty(facade, "GuidFormat")!);
	}

	[Fact]
	public void Facade_SessionId_throws_when_connection_is_not_open()
	{
		// SessionId 直接读取 Session，而 Session 要求连接处于 Open 状态
		using var connection = new MySqlConnection("Server=h;");
		var facade = CreateFacade(connection);
		var ex = Assert.Throws<System.Reflection.TargetInvocationException>(
			() => GetProperty(facade, "SessionId"));
		Assert.IsType<InvalidOperationException>(ex.InnerException);
		Assert.Contains("Connection must be Open", ex.InnerException!.Message);
	}

	[Fact]
	public void Facade_InnerCommand_returns_the_command()
	{
		using var command = new MySqlCommand();
		var facade = CreateFacade(command);
		Assert.Same(command, GetProperty(facade, "InnerCommand"));
	}

	[Fact]
	public void Facade_AsOracleConnection_returns_null_for_mysql_connection()
	{
		using var connection = new MySqlConnection("Server=h;");
		var facade = CreateFacade(connection);
		Assert.Null(GetProperty(facade, "AsOracleConnection"));
	}

	[Fact]
	public void Facade_AsOracleConnection_returns_oracle_connection()
	{
		using var connection = new OracleConnection("Server=h;Database=s;");
		var facade = CreateFacade(connection);
		Assert.Same(connection, GetProperty(facade, "AsOracleConnection"));
	}

	[Fact]
	public void Facade_AsyncIOBehavior_follows_ForceSynchronous()
	{
		using var async = new MySqlConnection("Server=h;");
		Assert.Equal("Asynchronous", GetProperty(CreateFacade(async), "AsyncIOBehavior")!.ToString());

		using var sync = new MySqlConnection("Server=h;Force Synchronous=true;");
		Assert.Equal("Synchronous", GetProperty(CreateFacade(sync), "AsyncIOBehavior")!.ToString());
	}

	/// <summary>注入 ConnectionSettings，使依赖 m_connectionSettings 的属性可读。</summary>
	private static void InjectSettings(MySqlConnection connection, string connectionString)
	{
		var assembly = typeof(MySqlConnection).Assembly;
		var settingsType = assembly.GetType("OceanBase.Core.ConnectionSettings", throwOnError: true)!;
		var settings = Activator.CreateInstance(settingsType, new MySqlConnectionStringBuilder(connectionString))!;
		typeof(MySqlConnection)
			.GetField("m_connectionSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
			.SetValue(connection, settings);
	}
}
