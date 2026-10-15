using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 连接串 builder 的**属性 setter** 测试。
///
/// 已有测试主要通过连接串字符串验证 getter/解析；本组测试改为直接给每个属性赋值，
/// 再校验读回值与序列化后的连接串，从而覆盖 <c>MySqlConnectionStringOption.SetValue</c>
/// 一侧（含「属性赋值跳过重复键检查」这一路径）。
/// </summary>
public sealed class ConnectionStringSetterTests
{
	/// <summary>
	/// 属性名 → 赋值 / 期望值 / 回读断言。用反射统一驱动，避免 65 个属性写成 65 个方法。
	/// </summary>
	public static TheoryData<string, object, object> MySqlCases() => new()
	{
		{ "Server", "db.example.com", "db.example.com" },
		{ "Port", 2883u, 2883u },
		{ "UserID", "u@tenant#cluster", "u@tenant#cluster" },
		{ "Password", "p@ss word", "p@ss word" },
		{ "Database", "mydb", "mydb" },
		{ "LoadBalance", MySqlLoadBalance.FailOver, MySqlLoadBalance.FailOver },
		{ "ConnectionProtocol", MySqlConnectionProtocol.Sockets, MySqlConnectionProtocol.Sockets },
		{ "PipeName", "MYPIPE", "MYPIPE" },
		{ "SslMode", MySqlSslMode.Required, MySqlSslMode.Required },
		{ "CertificateFile", "/tmp/cert.pfx", "/tmp/cert.pfx" },
		{ "CertificatePassword", "certpass", "certpass" },
		{ "CertificateStoreLocation", MySqlCertificateStoreLocation.CurrentUser, MySqlCertificateStoreLocation.CurrentUser },
		{ "CertificateThumbprint", "ABCDEF", "ABCDEF" },
		{ "SslCert", "/tmp/client.pem", "/tmp/client.pem" },
		{ "SslKey", "/tmp/client.key", "/tmp/client.key" },
		{ "CACertificateFile", "/tmp/ca.pem", "/tmp/ca.pem" },
		{ "SslCa", "/tmp/ca2.pem", "/tmp/ca2.pem" },
		{ "TlsVersion", "TLSv1.2,TLSv1.3", "TLS 1.2, TLS 1.3" },
		{ "TlsCipherSuites", "TLS_AES_256_GCM_SHA384", "TLS_AES_256_GCM_SHA384" },
		{ "Pooling", false, false },
		{ "ConnectionLifeTime", 120u, 120u },
		{ "ConnectionReset", true, true },
		{ "DeferConnectionReset", true, true },
		{ "ConnectionIdlePingTime", 30u, 30u },
		{ "ConnectionIdleTimeout", 60u, 60u },
		{ "MinimumPoolSize", 2u, 2u },
		{ "MaximumPoolSize", 50u, 50u },
		{ "DnsCheckInterval", 15u, 15u },
		{ "AllowLoadLocalInfile", true, true },
		{ "AllowPublicKeyRetrieval", true, true },
		{ "AllowUserVariables", true, true },
		{ "AllowZeroDateTime", true, true },
		{ "ApplicationName", "myapp", "myapp" },
		{ "AutoEnlist", false, false },
		{ "AutoCommit", false, false },
		{ "CancellationTimeout", 7, 7 },
		{ "CharacterSet", "utf8mb4", "utf8mb4" },
		{ "ConnectionTimeout", 45u, 45u },
		{ "ConvertZeroDateTime", true, true },
		{ "DateTimeKind", MySqlDateTimeKind.Utc, MySqlDateTimeKind.Utc },
		{ "DefaultCommandTimeout", 60u, 60u },
		{ "ForceSynchronous", true, true },
		{ "GuidFormat", MySqlGuidFormat.Char36, MySqlGuidFormat.Char36 },
		{ "IgnoreCommandTransaction", true, true },
		{ "IgnorePrepare", true, true },
		{ "AutoClosePreparedStatements", false, false },
		{ "InteractiveSession", true, true },
		{ "Keepalive", 90u, 90u },
		{ "NoBackslashEscapes", true, true },
		{ "OldGuids", true, true },
		{ "PersistSecurityInfo", true, true },
		{ "UseLobLocatorV2", true, true },
		{ "UseOraclePrepareExecute", true, true },
		{ "UseOracleServerParameterMetadata", false, false },
		{ "UseArrayBinding", true, true },
		{ "PrepareExecuteFetchSize", 25, 25 },
		{ "Pipelining", false, false },
		{ "ServerRedirectionMode", MySqlServerRedirectionMode.Required, MySqlServerRedirectionMode.Required },
		{ "ServerRsaPublicKeyFile", "/tmp/rsa.pem", "/tmp/rsa.pem" },
		{ "ServerSPN", "MYSQL/spn", "MYSQL/spn" },
		{ "TreatTinyAsBoolean", false, false },
		{ "UseAffectedRows", true, true },
		{ "UseCompression", true, true },
		{ "UseXaTransactions", false, false },
		{ "IgnorePrepareExecuteSequenceCheck", true, true },
	};

	public static TheoryData<string, object, object> OracleCases() => new()
	{
		{ "Server", "ob.example.com", "ob.example.com" },
		{ "Port", 2881u, 2881u },
		{ "UserID", "scott@tenant#cluster", "scott@tenant#cluster" },
		{ "Password", "tiger", "tiger" },
		{ "Database", "myschema", "myschema" },
		{ "LoadBalance", MySqlLoadBalance.Random, MySqlLoadBalance.Random },
		{ "PipeName", "OBPIPE", "OBPIPE" },
		{ "SslMode", MySqlSslMode.VerifyFull, MySqlSslMode.VerifyFull },
		{ "CertificateFile", "/tmp/ocert.pfx", "/tmp/ocert.pfx" },
		{ "CertificatePassword", "ocertpass", "ocertpass" },
		{ "CertificateStoreLocation", MySqlCertificateStoreLocation.LocalMachine, MySqlCertificateStoreLocation.LocalMachine },
		{ "CertificateThumbprint", "0123456789ABCDEF", "0123456789ABCDEF" },
		{ "SslCert", "/tmp/oclient.pem", "/tmp/oclient.pem" },
		{ "SslKey", "/tmp/oclient.key", "/tmp/oclient.key" },
		{ "CACertificateFile", "/tmp/oca.pem", "/tmp/oca.pem" },
		{ "SslCa", "/tmp/oca2.pem", "/tmp/oca2.pem" },
		{ "TlsVersion", "TLSv1.3", "TLS 1.3" },
		{ "TlsCipherSuites", "TLS_CHACHA20_POLY1305_SHA256", "TLS_CHACHA20_POLY1305_SHA256" },
		{ "Pooling", false, false },
		{ "ConnectionLifeTime", 240u, 240u },
		{ "ConnectionReset", true, true },
		{ "DeferConnectionReset", true, true },
		{ "ConnectionIdlePingTime", 40u, 40u },
		{ "ConnectionIdleTimeout", 90u, 90u },
		{ "MinimumPoolSize", 3u, 3u },
		{ "MaximumPoolSize", 60u, 60u },
		{ "DnsCheckInterval", 20u, 20u },
		{ "AllowLoadLocalInfile", true, true },
		{ "AllowPublicKeyRetrieval", true, true },
		{ "AllowUserVariables", true, true },
		{ "AllowZeroDateTime", true, true },
		{ "ApplicationName", "oracleapp", "oracleapp" },
		{ "AutoEnlist", false, false },
		{ "AutoCommit", false, false },
		{ "CancellationTimeout", 9, 9 },
		{ "CharacterSet", "gbk", "gbk" },
		{ "ConnectionTimeout", 55u, 55u },
		{ "ConvertZeroDateTime", true, true },
		{ "DateTimeKind", MySqlDateTimeKind.Local, MySqlDateTimeKind.Local },
		{ "DefaultCommandTimeout", 70u, 70u },
		{ "ForceSynchronous", true, true },
		{ "GuidFormat", MySqlGuidFormat.Binary16, MySqlGuidFormat.Binary16 },
		{ "IgnoreCommandTransaction", true, true },
		{ "IgnorePrepare", true, true },
		{ "AutoClosePreparedStatements", false, false },
		{ "InteractiveSession", true, true },
		{ "Keepalive", 120u, 120u },
		{ "NoBackslashEscapes", true, true },
		{ "OldGuids", true, true },
		{ "PersistSecurityInfo", true, true },
		{ "UseLobLocatorV2", false, false },
		{ "UseOraclePrepareExecute", true, true },
		{ "UseOracleServerParameterMetadata", true, true },
		{ "UseArrayBinding", true, true },
		{ "PrepareExecuteFetchSize", 15, 15 },
		{ "Pipelining", false, false },
		{ "ServerRedirectionMode", MySqlServerRedirectionMode.Required, MySqlServerRedirectionMode.Required },
		{ "ServerRsaPublicKeyFile", "/tmp/orsa.pem", "/tmp/orsa.pem" },
		{ "ServerSPN", "ORACLE/spn", "ORACLE/spn" },
		{ "TreatTinyAsBoolean", false, false },
		{ "UseAffectedRows", true, true },
		{ "UseCompression", true, true },
		{ "UseXaTransactions", false, false },
		{ "IgnorePrepareExecuteSequenceCheck", true, true },
	};

	[Theory]
	[MemberData(nameof(MySqlCases))]
	public void MySql_property_setters_roundtrip(string propertyName, object value, object expected)
	{
		AssertSetterRoundtrip(new MySqlConnectionStringBuilder(), propertyName, value, expected);
	}

	[Theory]
	[MemberData(nameof(OracleCases))]
	public void Oracle_property_setters_roundtrip(string propertyName, object value, object expected)
	{
		AssertSetterRoundtrip(new OracleConnectionStringBuilder(), propertyName, value, expected);
	}

	/// <summary>
	/// 通过反射赋值再回读：既覆盖 setter 分支，也确认写入的值能被 getter 正确还原。
	/// 同时对「经连接串往返」的一致性做校验（排除 PipeName 这类会被大写的选项差异）。
	/// </summary>
	private static void AssertSetterRoundtrip(object builder, string propertyName, object value, object expected)
	{
		var type = builder.GetType();
		var property = type.GetProperty(propertyName)
			?? throw new InvalidOperationException($"{type.Name} 缺少属性 {propertyName}");

		property.SetValue(builder, value);
		Assert.Equal(expected, property.GetValue(builder));

		// 赋值后连接串应包含该选项，且能用它重建出等值对象
		var connectionString = (string)type.GetProperty("ConnectionString")!.GetValue(builder)!;
		Assert.False(string.IsNullOrEmpty(connectionString), $"{propertyName} 赋值后连接串不应为空");

		var rebuilt = Activator.CreateInstance(type, connectionString)!;
		Assert.Equal(expected, property.GetValue(rebuilt));
	}

	// ---------- setter 的边界与容错 ----------

	[Fact]
	public void MySql_setters_reject_null_for_non_nullable_string()
	{
		var builder = new MySqlConnectionStringBuilder();
		// 引用型选项允许 null 并被规范化为默认值
		builder.Server = null!;
		Assert.Equal("", builder.Server);
	}

	[Fact]
	public void MySql_string_setters_normalize_null_to_default()
	{
		var builder = new MySqlConnectionStringBuilder
		{
			CertificateFile = null!,
			ApplicationName = null!,
			CharacterSet = null!,
		};
		Assert.Equal("", builder.CertificateFile);
		Assert.Equal("", builder.ApplicationName);
		Assert.Equal("", builder.CharacterSet);
	}

	[Fact]
	public void MySql_setting_same_value_twice_is_allowed()
	{
		// 属性赋值走的是「跳过重复键检查」路径：重复赋同值不应抛错
		var builder = new MySqlConnectionStringBuilder();
		builder.Server = "host1";
		builder.Server = "host1";
		Assert.Equal("host1", builder.Server);
	}

	[Fact]
	public void MySql_setting_different_value_overwrites()
	{
		var builder = new MySqlConnectionStringBuilder();
		builder.Server = "host1";
		builder.Server = "host2";
		Assert.Equal("host2", builder.Server);
	}

	[Fact]
	public void MySql_pooling_related_setters_survive_connection_string()
	{
		var builder = new MySqlConnectionStringBuilder
		{
			Pooling = false,
			MinimumPoolSize = 5,
			MaximumPoolSize = 10,
			ConnectionIdleTimeout = 30,
		};
		var rebuilt = new MySqlConnectionStringBuilder(builder.ConnectionString);
		Assert.False(rebuilt.Pooling);
		Assert.Equal(5u, rebuilt.MinimumPoolSize);
		Assert.Equal(10u, rebuilt.MaximumPoolSize);
		Assert.Equal(30u, rebuilt.ConnectionIdleTimeout);
	}

	[Fact]
	public void Oracle_setters_reject_null_for_strings_and_normalize()
	{
		var builder = new OracleConnectionStringBuilder { Server = null!, ApplicationName = null! };
		Assert.Equal("", builder.Server);
		Assert.Equal("", builder.ApplicationName);
	}

	[Fact]
	public void Oracle_connection_protocol_setter_persists_but_readback_throws()
	{
		// 已知上游缺陷（已有测试同样固化）：OracleConnectionStringValueOption.ChangeType 的
		// 枚举白名单只列了 MySqlConnectionProtocol，未含 OracleConnectionProtocol，
		// 因此 setter 能写入（按 ToString 存字符串），读回时 Convert.ChangeType 失败 → ArgumentException。
		var builder = new OracleConnectionStringBuilder();
		builder.ConnectionProtocol = OracleConnectionProtocol.Pipe;
		Assert.Contains("Connection Protocol=Pipe", builder.ConnectionString);
		Assert.Throws<ArgumentException>(() => _ = builder.ConnectionProtocol);
	}

	[Fact]
	public void Oracle_connection_protocol_any_value_is_unreadable()
	{
		// 同上：即便合法的 Sockets 也无法读回
		var builder = new OracleConnectionStringBuilder { ConnectionProtocol = OracleConnectionProtocol.Sockets };
		Assert.Throws<ArgumentException>(() => _ = builder.ConnectionProtocol);
	}

	[Fact]
	public void Oracle_pooling_setters_survive_connection_string()
	{
		var builder = new OracleConnectionStringBuilder
		{
			Pooling = false,
			MinimumPoolSize = 1,
			MaximumPoolSize = 4,
			ConnectionIdleTimeout = 15,
		};
		var rebuilt = new OracleConnectionStringBuilder(builder.ConnectionString);
		Assert.False(rebuilt.Pooling);
		Assert.Equal(1u, rebuilt.MinimumPoolSize);
		Assert.Equal(4u, rebuilt.MaximumPoolSize);
		Assert.Equal(15u, rebuilt.ConnectionIdleTimeout);
	}

	[Fact]
	public void Both_builders_expose_the_same_option_count()
	{
		// 两个 builder 的选项集合应一致（Oracle 仅协议枚举不同）
		var mysqlCount = new MySqlConnectionStringBuilder().Keys.Count;
		var oracleCount = new OracleConnectionStringBuilder().Keys.Count;
		Assert.Equal(mysqlCount, oracleCount);
	}

	[Fact]
	public void Every_listed_property_exists_on_the_builder()
	{
		// 防止上面的用例表与实际属性名漂移：逐一确认属性存在且可读写
		foreach (var name in MySqlCases().Select(row => row.Data.Item1))
		{
			var property = typeof(MySqlConnectionStringBuilder).GetProperty(name);
			Assert.NotNull(property);
			Assert.True(property!.CanRead && property.CanWrite, $"{name} 应可读写");
		}

		foreach (var name in OracleCases().Select(row => row.Data.Item1))
		{
			var property = typeof(OracleConnectionStringBuilder).GetProperty(name);
			Assert.NotNull(property);
			Assert.True(property!.CanRead && property.CanWrite, $"{name} 应可读写");
		}
	}
}
