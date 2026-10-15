using System;
using System.Globalization;
using System.Linq;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 连接字符串选项解析的补充单元测试（不依赖服务端）。
/// 在 ConnectionStringBuilderTests 已覆盖的别名/默认值/非法值之外，
/// 扩大覆盖：SSL/TLS 选项、超时、池化、负载均衡、压缩、GUID 格式、
/// TlsVersion / CancellationTimeout 的 coerce 回调、同义词键到规范键的折叠、
/// 重复参数校验、索引器行为、Keys 顺序与 GetConnectionString 的密码剥离缓存。
/// </summary>
public sealed class ConnectionStringOptionsTests
{
	// ---------- MySQL：SSL/TLS ----------

	[Theory]
	[InlineData("None", MySqlSslMode.None)]
	[InlineData("Disabled", MySqlSslMode.None)]   // Disabled 与 None 同值（=0）
	[InlineData("Preferred", MySqlSslMode.Preferred)]
	[InlineData("required", MySqlSslMode.Required)]   // Enum.Parse 忽略大小写
	[InlineData("VerifyCA", MySqlSslMode.VerifyCA)]
	[InlineData("VERIFYFULL", MySqlSslMode.VerifyFull)]
	public void MySql_ssl_mode_accepts_documented_names(string text, MySqlSslMode expected)
	{
		Assert.Equal(expected, new MySqlConnectionStringBuilder($"SSL Mode={text};").SslMode);
	}

	[Fact]
	public void MySql_ssl_mode_accepts_numeric_strings()
	{
		// 枚举分支走 Enum.Parse，接受数值字符串（含未定义的值）
		Assert.Equal(MySqlSslMode.Required, new MySqlConnectionStringBuilder("SSL Mode=2").SslMode);
		Assert.Equal((MySqlSslMode)99, new MySqlConnectionStringBuilder("SSL Mode=99").SslMode);
	}

	[Theory]
	[InlineData("TLSv1.2", "TLS 1.2")]
	[InlineData("TLS 1.2", "TLS 1.2")]            // 已是规范形式，幂等
	[InlineData("tlsv1.3", "TLS 1.3")]            // 正则 IgnoreCase
	[InlineData("TLSv1", "TLS 1.0")]
	[InlineData("TLS1.1", "TLS 1.1")]             // v 可省略
	[InlineData("  TLSv1.2", "TLS 1.2")]          // 前导空白由正则的 \s* 消化
	[InlineData("TLS", "TLS 1.0")]                // 无版本号时捕获组为空，按 TLS 1.0 记录
	[InlineData("(TLSv1.1)", "TLS 1.1")]          // 括号被 TrimStart/TrimEnd 剥离
	[InlineData("[TLSv1.2, TLSv1.3]", "TLS 1.2, TLS 1.3")]
	[InlineData("TLSv1.3,TLSv1.0", "TLS 1.0, TLS 1.3")]   // 输出按版本号升序，与输入顺序无关
	public void MySql_tls_version_is_normalized(string input, string expected)
	{
		var b = new MySqlConnectionStringBuilder($"Server=h;TLS Version={input};");
		Assert.Equal(expected, b.TlsVersion);
	}

	[Theory]
	[InlineData("SSLv3")]
	[InlineData("TLSv1.4")]
	[InlineData("TLSv2")]
	[InlineData("TLS 2.0")]
	[InlineData("TLSv")]
	public void MySql_invalid_tls_version_throws(string value)
	{
		Assert.Throws<ArgumentException>(() => new MySqlConnectionStringBuilder($"Server=h;TLS Version={value};"));
	}

	[Fact]
	public void MySql_tls_version_blank_or_null_normalizes_to_empty()
	{
		var b = new MySqlConnectionStringBuilder();
		b.TlsVersion = "   ";
		Assert.Equal("", b.TlsVersion);
		b.TlsVersion = null;
		Assert.Equal("", b.TlsVersion);   // 空白 / null 都归一化为空串（使用 OS 默认）
	}

	[Fact]
	public void MySql_tls_version_trailing_whitespace_is_rejected_by_property_setter()
	{
		// 连接字符串解析会先修剪两端空白，但属性赋值不修剪；正则 \s* 只匹配前导空白，尾随空格导致失配
		var b = new MySqlConnectionStringBuilder();
		Assert.Throws<ArgumentException>(() => b.TlsVersion = "TLSv1.2 ");
	}

	[Fact]
	public void MySql_tls_version_property_set_normalizes_before_persisting()
	{
		var b = new MySqlConnectionStringBuilder();
		b.TlsVersion = "TLSv1.2";
		Assert.Equal("TLS 1.2", b.TlsVersion);
		// coerce 在写入字典前执行，ConnectionString 里已是规范形式；
		// 基类会对含空白的值加引号输出
		Assert.Contains("TLS Version=\"TLS 1.2\"", b.ConnectionString);
	}

	[Theory]
	[InlineData("SSL CA")]
	[InlineData("SslCa")]
	[InlineData("Ssl-Ca")]
	[InlineData("CACertificateFile")]
	[InlineData("CA Certificate File")]   // 已过时的别名仍然注册
	public void MySql_ssl_ca_accepts_all_documented_aliases(string keyword)
	{
		Assert.Equal("ca.pem", new MySqlConnectionStringBuilder($"{keyword}=ca.pem;").SslCa);
	}

	[Fact]
	public void MySql_tls_related_string_options_roundtrip()
	{
		// 无 coerce 的字符串选项：原样保存，规范键出现在 ConnectionString 中
		var b = new MySqlConnectionStringBuilder(
			"Ssl-Cert=c.pem;Ssl-Key=k.pem;TLS Cipher Suites=A,B;Certificate File=client.pfx;" +
			"Certificate Password=px;Certificate Thumb Print=ab12;Server RSA Public Key File=rsa.pem;Server SPN=spn;");
		Assert.Equal("c.pem", b.SslCert);
		Assert.Equal("k.pem", b.SslKey);
		Assert.Equal("A,B", b.TlsCipherSuites);
		Assert.Equal("client.pfx", b.CertificateFile);
		Assert.Equal("px", b.CertificatePassword);
		Assert.Equal("ab12", b.CertificateThumbprint);
		Assert.Equal("rsa.pem", b.ServerRsaPublicKeyFile);
		Assert.Equal("spn", b.ServerSPN);
		Assert.Contains("SSL Cert=c.pem", b.ConnectionString);
	}

	[Theory]
	[InlineData("None", MySqlCertificateStoreLocation.None)]
	[InlineData("CurrentUser", MySqlCertificateStoreLocation.CurrentUser)]
	[InlineData("LocalMachine", MySqlCertificateStoreLocation.LocalMachine)]
	[InlineData("currentuser", MySqlCertificateStoreLocation.CurrentUser)]
	public void MySql_certificate_store_location_parses_names(string text, MySqlCertificateStoreLocation expected)
	{
		Assert.Equal(expected, new MySqlConnectionStringBuilder($"Certificate Store Location={text};").CertificateStoreLocation);
	}

	// ---------- MySQL：超时 ----------

	[Theory]
	[InlineData("Connection Timeout")]
	[InlineData("ConnectionTimeout")]
	[InlineData("Connect Timeout")]
	public void MySql_connection_timeout_accepts_all_aliases(string keyword)
	{
		var b = new MySqlConnectionStringBuilder($"{keyword}=42;");
		Assert.Equal(42u, b.ConnectionTimeout);
		// 同义词在内部折叠为规范键
		Assert.Contains("Connection Timeout=42", b.ConnectionString);
	}

	[Theory]
	[InlineData("Default Command Timeout")]
	[InlineData("DefaultCommandTimeout")]
	[InlineData("Command Timeout")]
	public void MySql_default_command_timeout_accepts_all_aliases(string keyword)
	{
		var b = new MySqlConnectionStringBuilder($"{keyword}=77;");
		Assert.Equal(77u, b.DefaultCommandTimeout);
		Assert.Contains("Default Command Timeout=77", b.ConnectionString);
	}

	[Fact]
	public void MySql_cancellation_timeout_coerce_bounds()
	{
		// coerce 允许 -1（表示无取消超时）及以上的所有值
		var b = new MySqlConnectionStringBuilder();
		b.CancellationTimeout = -1;
		Assert.Equal(-1, b.CancellationTimeout);
		b.CancellationTimeout = 0;
		Assert.Equal(0, b.CancellationTimeout);
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => b.CancellationTimeout = -2);
		Assert.Contains("greater than or equal to -1", ex.Message);
		// 连接字符串路径同样触发 coerce（索引器写入时执行）
		Assert.Equal(-1, new MySqlConnectionStringBuilder("Cancellation Timeout=-1").CancellationTimeout);
		Assert.Throws<ArgumentOutOfRangeException>(() => new MySqlConnectionStringBuilder("Cancellation Timeout=-2;"));
	}

	[Fact]
	public void MySql_keepalive_and_lifetime_parse()
	{
		var b = new MySqlConnectionStringBuilder("Keep Alive=30;Connection Lifetime=3600;");
		Assert.Equal(30u, b.Keepalive);
		Assert.Equal(3600u, b.ConnectionLifeTime);
	}

	// ---------- MySQL：池化 ----------

	[Theory]
	[InlineData("yes", true)]
	[InlineData("no", false)]
	[InlineData("YES", true)]   // yes/no 比较使用 OrdinalIgnoreCase
	[InlineData("No", false)]
	public void MySql_bool_options_accept_yes_no(string text, bool expected)
	{
		Assert.Equal(expected, new MySqlConnectionStringBuilder($"Pooling={text};").Pooling);
	}

	[Theory]
	[InlineData("1")]
	[InlineData("on")]
	[InlineData("off")]
	[InlineData("y")]
	public void MySql_bool_options_reject_non_boolean_text(string text)
	{
		// 除 true/false/yes/no 之外的文本（含 "1"）都会转换失败并包装为 ArgumentException
		Assert.Throws<ArgumentException>(() => new MySqlConnectionStringBuilder($"Pooling={text};"));
	}

	[Theory]
	[InlineData("Minimum Pool Size")]
	[InlineData("Min Pool Size")]
	[InlineData("MinimumPoolSize")]
	[InlineData("minpoolsize")]
	public void MySql_min_pool_size_accepts_all_aliases(string keyword)
	{
		var b = new MySqlConnectionStringBuilder($"{keyword}=3;");
		Assert.Equal(3u, b.MinimumPoolSize);
		Assert.Contains("Minimum Pool Size=3", b.ConnectionString);
	}

	[Theory]
	[InlineData("Maximum Pool Size")]
	[InlineData("Max Pool Size")]
	[InlineData("MaximumPoolSize")]
	[InlineData("maxpoolsize")]
	public void MySql_max_pool_size_accepts_all_aliases(string keyword)
	{
		var b = new MySqlConnectionStringBuilder($"{keyword}=9;");
		Assert.Equal(9u, b.MaximumPoolSize);
		Assert.Contains("Maximum Pool Size=9", b.ConnectionString);
	}

	[Fact]
	public void MySql_idle_pool_options_parse()
	{
		var b = new MySqlConnectionStringBuilder("Connection Idle Timeout=60;DNS Check Interval=30;Connection Reset=true;");
		Assert.Equal(60u, b.ConnectionIdleTimeout);
		Assert.Equal(30u, b.DnsCheckInterval);
		Assert.True(b.ConnectionReset);
	}

	// ---------- MySQL：枚举选项 ----------

	[Theory]
	[InlineData("RoundRobin", MySqlLoadBalance.RoundRobin)]
	[InlineData("FailOver", MySqlLoadBalance.FailOver)]
	[InlineData("Random", MySqlLoadBalance.Random)]
	[InlineData("LeastConnections", MySqlLoadBalance.LeastConnections)]
	[InlineData("random", MySqlLoadBalance.Random)]
	public void MySql_load_balance_accepts_all_names(string text, MySqlLoadBalance expected)
	{
		Assert.Equal(expected, new MySqlConnectionStringBuilder($"Load Balance={text};").LoadBalance);
	}

	[Theory]
	[InlineData("Default", MySqlGuidFormat.Default)]
	[InlineData("None", MySqlGuidFormat.None)]
	[InlineData("Char36", MySqlGuidFormat.Char36)]
	[InlineData("Char32", MySqlGuidFormat.Char32)]
	[InlineData("Binary16", MySqlGuidFormat.Binary16)]
	[InlineData("TimeSwapBinary16", MySqlGuidFormat.TimeSwapBinary16)]
	[InlineData("LittleEndianBinary16", MySqlGuidFormat.LittleEndianBinary16)]
	[InlineData("char36", MySqlGuidFormat.Char36)]
	public void MySql_guid_format_accepts_all_names(string text, MySqlGuidFormat expected)
	{
		Assert.Equal(expected, new MySqlConnectionStringBuilder($"GUID Format={text};").GuidFormat);
	}

	[Theory]
	[InlineData("Sockets", MySqlConnectionProtocol.Sockets)]
	[InlineData("Socket", MySqlConnectionProtocol.Sockets)]
	[InlineData("Tcp", MySqlConnectionProtocol.Sockets)]
	[InlineData("Pipe", MySqlConnectionProtocol.Pipe)]
	[InlineData("NamedPipe", MySqlConnectionProtocol.Pipe)]
	[InlineData("UnixSocket", MySqlConnectionProtocol.UnixSocket)]
	[InlineData("Unix", MySqlConnectionProtocol.UnixSocket)]
	[InlineData("SharedMemory", MySqlConnectionProtocol.SharedMemory)]
	[InlineData("Memory", MySqlConnectionProtocol.SharedMemory)]
	public void MySql_connection_protocol_aliases_share_values(string text, MySqlConnectionProtocol expected)
	{
		Assert.Equal(expected, new MySqlConnectionStringBuilder($"Connection Protocol={text};").ConnectionProtocol);
	}

	[Fact]
	public void MySql_protocol_and_pipe_key_aliases()
	{
		// Protocol 是 Connection Protocol 的同义词键；Pipe 是 Pipe Name 的同义词键
		var b = new MySqlConnectionStringBuilder("Protocol=Pipe;Pipe=office_pipe;");
		Assert.Equal(MySqlConnectionProtocol.Pipe, b.ConnectionProtocol);
		Assert.Equal("office_pipe", b.PipeName);
		// 数值字符串经由 Enum.Parse 解析（2 = Pipe）
		Assert.Equal(MySqlConnectionProtocol.Pipe, new MySqlConnectionStringBuilder("Protocol=2;").ConnectionProtocol);
		Assert.Contains("Pipe Name=office_pipe", b.ConnectionString);
	}

	[Theory]
	[InlineData("Unspecified", MySqlDateTimeKind.Unspecified)]
	[InlineData("Utc", MySqlDateTimeKind.Utc)]
	[InlineData("Local", MySqlDateTimeKind.Local)]
	public void MySql_date_time_kind_parses_names(string text, MySqlDateTimeKind expected)
	{
		Assert.Equal(expected, new MySqlConnectionStringBuilder($"DateTime Kind={text};").DateTimeKind);
	}

	[Theory]
	[InlineData("Disabled", MySqlServerRedirectionMode.Disabled)]
	[InlineData("Preferred", MySqlServerRedirectionMode.Preferred)]
	[InlineData("Required", MySqlServerRedirectionMode.Required)]
	[InlineData("preferred", MySqlServerRedirectionMode.Preferred)]
	public void MySql_server_redirection_mode_parses_names(string text, MySqlServerRedirectionMode expected)
	{
		Assert.Equal(expected, new MySqlConnectionStringBuilder($"Server Redirection Mode={text};").ServerRedirectionMode);
	}

	// ---------- MySQL：字符集 / 压缩 / 杂项 ----------

	[Theory]
	[InlineData("Character Set")]
	[InlineData("CharSet")]
	[InlineData("CharacterSet")]
	public void MySql_character_set_accepts_all_aliases(string keyword)
	{
		var b = new MySqlConnectionStringBuilder($"{keyword}=utf8mb4;");
		Assert.Equal("utf8mb4", b.CharacterSet);
		Assert.Contains("Character Set=utf8mb4", b.ConnectionString);
	}

	[Fact]
	public void MySql_compression_accepts_compress_alias()
	{
		var b = new MySqlConnectionStringBuilder("Compress=true;");
		Assert.True(b.UseCompression);
		Assert.Contains("Use Compression=True", b.ConnectionString);
	}

	[Fact]
	public void MySql_misc_boolean_options_parse_from_string()
	{
		var b = new MySqlConnectionStringBuilder(
			"Connection Reset=true;Allow Public Key Retrieval=true;Allow User Variables=yes;" +
			"Convert Zero DateTime=true;Interactive=true;No Backslash Escapes=true;Persist Security Info=true;" +
			"Use Affected Rows=true;Treat Tiny As Boolean=no;Auto Enlist=no;Pipelining=no;" +
			"Use XA Transactions=no;Auto Close Prepared Statements=no;Application Name=myapp;");
		Assert.True(b.ConnectionReset);
		Assert.True(b.AllowPublicKeyRetrieval);
		Assert.True(b.AllowUserVariables);
		Assert.True(b.ConvertZeroDateTime);
		Assert.True(b.InteractiveSession);
		Assert.True(b.NoBackslashEscapes);
		Assert.True(b.PersistSecurityInfo);
		Assert.True(b.UseAffectedRows);
		Assert.False(b.TreatTinyAsBoolean);
		Assert.False(b.AutoEnlist);
		Assert.False(b.Pipelining);
		Assert.False(b.UseXaTransactions);
		Assert.False(b.AutoClosePreparedStatements);
		Assert.Equal("myapp", b.ApplicationName);
	}

	[Fact]
	public void MySql_obsolete_options_still_parse()
	{
		// 已过时选项仅标注 Obsolete，解析行为保留；经索引器读取以避免编译警告
		var b = new MySqlConnectionStringBuilder(
			"Defer Connection Reset=no;Connection Idle Ping Time=7;Old Guids=true;CA Certificate File=ca.pem;");
		Assert.False((bool)b["DeferConnectionReset"]!);
		Assert.Equal(7u, (uint)b["ConnectionIdlePingTime"]!);
		Assert.True((bool)b["OldGuids"]!);
		Assert.Equal("ca.pem", b.SslCa);
	}

	[Fact]
	public void MySql_defaults_beyond_the_basics()
	{
		var b = new MySqlConnectionStringBuilder();
		Assert.Equal(0u, b.ConnectionLifeTime);
		Assert.Equal(180u, b.ConnectionIdleTimeout);
		Assert.Equal(0u, b.MinimumPoolSize);
		Assert.Equal(0u, b.DnsCheckInterval);
		Assert.Equal(30u, b.DefaultCommandTimeout);
		Assert.Equal(2, b.CancellationTimeout);
		Assert.Equal(0u, b.Keepalive);
		Assert.Equal(MySqlDateTimeKind.Unspecified, b.DateTimeKind);
		Assert.Equal(MySqlServerRedirectionMode.Disabled, b.ServerRedirectionMode);
		Assert.Equal(MySqlCertificateStoreLocation.None, b.CertificateStoreLocation);
		Assert.Equal(MySqlLoadBalance.RoundRobin, b.LoadBalance);
		Assert.Equal(MySqlConnectionProtocol.Sockets, b.ConnectionProtocol);
		Assert.Equal("MYSQL", b.PipeName);
		Assert.Equal(MySqlGuidFormat.Default, b.GuidFormat);
		Assert.Equal(10, b.PrepareExecuteFetchSize);
		Assert.False(b.UseOraclePrepareExecute);
		Assert.True(b.UseOracleServerParameterMetadata);
		Assert.False(b.IgnorePrepareExecuteSequenceCheck);
		Assert.False(b.IgnorePrepare);
		Assert.False(b.IgnoreCommandTransaction);
		Assert.True(b.AutoClosePreparedStatements);
		Assert.True(b.Pipelining);
		Assert.True(b.AutoEnlist);
		Assert.True(b.TreatTinyAsBoolean);
		Assert.True(b.UseXaTransactions);
		Assert.False(b.UseCompression);
		Assert.False(b.UseAffectedRows);
		Assert.False(b.PersistSecurityInfo);
		Assert.False(b.InteractiveSession);
		Assert.False(b.ForceSynchronous);
		Assert.False(b.AllowUserVariables);
		Assert.False(b.AllowZeroDateTime);
		Assert.False(b.ConvertZeroDateTime);
		Assert.False(b.AllowPublicKeyRetrieval);
		Assert.False(b.AllowLoadLocalInfile);
		Assert.False(b.NoBackslashEscapes);
		Assert.False((bool)b["OldGuids"]!);
		Assert.Equal("", b.CharacterSet);
		Assert.Equal("", b.ApplicationName);
		Assert.Equal("", b.CertificateFile);
		Assert.Equal("", b.CertificatePassword);
		Assert.Equal("", b.CertificateThumbprint);
		Assert.Equal("", b.SslCert);
		Assert.Equal("", b.SslKey);
		Assert.Equal("", b.SslCa);
		Assert.Equal("", b.TlsVersion);
		Assert.Equal("", b.TlsCipherSuites);
		Assert.Equal("", b.ServerRsaPublicKeyFile);
		Assert.Equal("", b.ServerSPN);
	}

	// ---------- MySQL：数值边界 ----------

	[Fact]
	public void MySql_port_accepts_uint_boundaries()
	{
		Assert.Equal(0u, new MySqlConnectionStringBuilder("Port=0;").Port);
		Assert.Equal(4294967295u, new MySqlConnectionStringBuilder("Port=4294967295;").Port);
	}

	[Theory]
	[InlineData("Port=4294967296;")]           // 溢出 uint
	[InlineData("Port=-1;")]                   // 负数
	[InlineData("Connection Timeout=-1;")]
	[InlineData("Max Pool Size=10000000000;")]
	public void MySql_out_of_range_uint_values_throw(string connectionString)
	{
		Assert.Throws<ArgumentException>(() => new MySqlConnectionStringBuilder(connectionString));
	}

	// ---------- MySQL：索引器 / Keys / 同义词折叠 ----------

	[Fact]
	public void MySql_indexer_reads_defaults_and_accepts_alias_keys()
	{
		var b = new MySqlConnectionStringBuilder();
		Assert.Equal(3306u, (uint)b["Port"]!);
		Assert.Equal(MySqlSslMode.None, (MySqlSslMode)b["ssl mode"]!);   // 别名 + 小写
		b["connect timeout"] = "42";
		Assert.Equal(42u, b.ConnectionTimeout);
		b["Port"] = 1234;
		Assert.Equal(1234u, b.Port);
	}

	[Fact]
	public void MySql_indexer_unknown_key_throws_on_get_and_set()
	{
		var b = new MySqlConnectionStringBuilder();
		Assert.Throws<ArgumentException>(() => _ = b["Not A Keyword"]);
		Assert.Throws<ArgumentException>(() => b["Not A Keyword"] = "x");
	}

	[Fact]
	public void MySql_indexer_null_removes_option()
	{
		var b = new MySqlConnectionStringBuilder("SSL Mode=Required;");
		Assert.True(b.ContainsKey("SslMode"));
		b["SslMode"] = null;   // 值为 null 时直接从底层字典移除
		Assert.False(b.ContainsKey("SslMode"));
		Assert.Equal(MySqlSslMode.None, b.SslMode);
	}

	[Fact]
	public void MySql_contains_key_and_remove_work_via_any_synonym()
	{
		var b = new MySqlConnectionStringBuilder("minpoolsize=5;");
		Assert.True(b.ContainsKey("Min Pool Size"));
		Assert.True(b.ContainsKey("MINPOOLSIZE"));
		Assert.False(b.ContainsKey("Max Pool Size"));
		Assert.True(b.Remove("Minimum Pool Size"));
		Assert.Equal(0u, b.MinimumPoolSize);
		Assert.False(b.Remove("Not A Keyword"));
	}

	[Fact]
	public void MySql_keys_follow_option_registration_order()
	{
		// Keys 按选项注册顺序（OptionNames 索引）排序，与写入顺序无关
		var b = new MySqlConnectionStringBuilder("Max Pool Size=5;Pooling=false;Server=h;");
		Assert.Equal(new[] { "Server", "Pooling", "Maximum Pool Size" }, b.Keys.Cast<string>().ToArray());
	}

	[Fact]
	public void MySql_option_registry_maps_synonyms_to_one_option()
	{
		var byUid = MySqlConnectionStringOption.TryGetOptionForKey("Uid");
		Assert.NotNull(byUid);
		Assert.Equal("User ID", byUid!.Key);
		Assert.Contains("User name", byUid.Keys);
		Assert.Same(MySqlConnectionStringOption.TryGetOptionForKey("USER ID"), byUid);
		Assert.Null(MySqlConnectionStringOption.TryGetOptionForKey("Not A Keyword"));
		Assert.Throws<ArgumentException>(() => MySqlConnectionStringOption.GetOptionForKey("Not A Keyword"));
	}

	// ---------- MySQL：GetConnectionString ----------

	[Fact]
	public void MySql_get_connection_string_strips_password_and_caches()
	{
		var b = new MySqlConnectionStringBuilder("Server=h;Database=d;pwd=secret;");
		Assert.Equal(b.ConnectionString, b.GetConnectionString(includePassword: true));
		var without = b.GetConnectionString(includePassword: false);
		Assert.DoesNotContain("secret", without, StringComparison.Ordinal);
		Assert.DoesNotContain("Password", without, StringComparison.Ordinal);
		Assert.Contains("Server=h", without);
		Assert.Equal(without, b.GetConnectionString(includePassword: false));   // 结果被缓存
		// 修改密码后缓存失效并重建，新密码同样被剥离
		b.Password = "n3w";
		var without2 = b.GetConnectionString(includePassword: false);
		Assert.DoesNotContain("n3w", without2, StringComparison.Ordinal);
		Assert.DoesNotContain("secret", without2, StringComparison.Ordinal);
	}

	// ---------- MySQL：重复参数校验 ----------

	[Theory]
	[InlineData("Server=a;Host=b")]                        // 同义词折叠到同一规范键
	[InlineData("server=a;SERVER=b")]                      // 键大小写不敏感
	[InlineData("Server=a;Data Source=b;Addr=c")]          // 三个同义词同用
	[InlineData("Uid=u1;User ID=u2")]
	[InlineData("Password=p;pwd=q")]
	[InlineData("Port=1;Port=2")]
	[InlineData("Database=d;Initial Catalog=e")]
	[InlineData("CharSet=a;Character Set=b")]
	[InlineData("Connect Timeout=1;Connection Timeout=2")]
	[InlineData("TLS Version=TLSv1.2;Tls-Version=TLSv1.3")]
	[InlineData("NotAKeyword=1;NotAKeyword=2")]            // 未知键的重复同样先被校验拦截
	public void MySql_duplicate_options_rejected_in_constructor(string connectionString)
	{
		var ex = Assert.Throws<ArgumentException>(() => new MySqlConnectionStringBuilder(connectionString));
		// ParamName 证明异常来自重复校验，而非“未知关键字”路径
		Assert.Equal("connectionString", ex.ParamName);
		Assert.Contains("Duplicate", ex.Message);
	}

	[Fact]
	public void MySql_unknown_single_keyword_rejected_by_lookup()
	{
		var ex = Assert.Throws<ArgumentException>(() => new MySqlConnectionStringBuilder("Server=h;NotAKeyword=1;"));
		Assert.Null(ex.ParamName);   // GetOptionForKey 抛出的异常不带 ParamName
	}

	[Fact]
	public void MySql_duplicate_check_is_skipped_when_assigning_property()
	{
		// 重复校验只在构造函数里执行；运行期赋值走基类默认行为（后值覆盖前值）
		var b = new MySqlConnectionStringBuilder();
		b.ConnectionString = "Server=a;Server=b";
		Assert.Equal("b", b.Server);
	}

	// ---------- MySQL：区域设置无关性 ----------

	[Fact]
	public void MySql_option_parsing_is_independent_of_current_culture()
	{
		// 所有解析路径均使用 InvariantCulture / OrdinalIgnoreCase；
		// 用 tr-TR（土耳其语 I）验证键大小写折叠与数值解析不受当前区域影响
		var original = CultureInfo.CurrentCulture;
		CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
		try
		{
			var b = new MySqlConnectionStringBuilder(
				"server=h;ssl mode=required;pooling=yes;connect timeout=42;guid format=char36");
			Assert.Equal("h", b.Server);
			Assert.Equal(MySqlSslMode.Required, b.SslMode);
			Assert.True(b.Pooling);
			Assert.Equal(42u, b.ConnectionTimeout);
			Assert.Equal(MySqlGuidFormat.Char36, b.GuidFormat);
		}
		finally
		{
			CultureInfo.CurrentCulture = original;
		}
	}

	// ---------- MySQL：往返与容错 ----------

	[Fact]
	public void MySql_empty_segments_and_trailing_semicolons_are_tolerated()
	{
		var b = new MySqlConnectionStringBuilder("Server=h1,h2;;Port=2881;");
		Assert.Equal("h1,h2", b.Server);   // 逗号分隔的多主机原样保留
		Assert.Equal(2881u, b.Port);
		var empty = new MySqlConnectionStringBuilder("");
		Assert.Equal("", empty.ConnectionString);
		Assert.Equal("", empty.Server);
	}

	[Fact]
	public void MySql_quoted_password_roundtrips()
	{
		var b = new MySqlConnectionStringBuilder("Server=h;Password=\"p;w\";");
		Assert.Equal("p;w", b.Password);   // 引号内的分号不属于键值分隔符
		Assert.Equal("p;w", new MySqlConnectionStringBuilder(b.ConnectionString).Password);
	}

	[Fact]
	public void MySql_many_options_roundtrip_through_connection_string()
	{
		var b = new MySqlConnectionStringBuilder
		{
			Server = "h1,h2",
			Port = 2881,
			UserID = "root@sys#ob",
			Password = "p",
			Database = "db",
			SslMode = MySqlSslMode.VerifyFull,
			TlsVersion = "TLSv1.2",
			CertificateFile = "client.pfx",
			SslCa = "ca.pem",
			Pooling = false,
			MinimumPoolSize = 2,
			MaximumPoolSize = 9,
			ConnectionTimeout = 25,
			DefaultCommandTimeout = 60,
			GuidFormat = MySqlGuidFormat.Char36,
			LoadBalance = MySqlLoadBalance.LeastConnections,
			ConnectionProtocol = MySqlConnectionProtocol.Tcp,
			UseCompression = true,
			AllowPublicKeyRetrieval = true,
			CharacterSet = "utf8mb4",
		};
		var b2 = new MySqlConnectionStringBuilder(b.ConnectionString);
		Assert.Equal(b.ConnectionString, b2.ConnectionString);
		Assert.Equal("h1,h2", b2.Server);
		Assert.Equal(2881u, b2.Port);
		Assert.Equal("root@sys#ob", b2.UserID);
		Assert.Equal("db", b2.Database);
		Assert.Equal(MySqlSslMode.VerifyFull, b2.SslMode);
		Assert.Equal("TLS 1.2", b2.TlsVersion);
		Assert.Equal("client.pfx", b2.CertificateFile);
		Assert.Equal("ca.pem", b2.SslCa);
		Assert.False(b2.Pooling);
		Assert.Equal(2u, b2.MinimumPoolSize);
		Assert.Equal(9u, b2.MaximumPoolSize);
		Assert.Equal(25u, b2.ConnectionTimeout);
		Assert.Equal(60u, b2.DefaultCommandTimeout);
		Assert.Equal(MySqlGuidFormat.Char36, b2.GuidFormat);
		Assert.Equal(MySqlLoadBalance.LeastConnections, b2.LoadBalance);
		Assert.Equal(MySqlConnectionProtocol.Tcp, b2.ConnectionProtocol);
		Assert.True(b2.UseCompression);
		Assert.True(b2.AllowPublicKeyRetrieval);
		Assert.Equal("utf8mb4", b2.CharacterSet);
	}

	// ---------- Oracle：默认值与模式差异 ----------

	[Fact]
	public void Oracle_defaults_beyond_the_basics()
	{
		var b = new OracleConnectionStringBuilder();
		Assert.True(b.IgnorePrepareExecuteSequenceCheck);
		Assert.Equal(10, b.PrepareExecuteFetchSize);
		Assert.False(b.UseOraclePrepareExecute);
		Assert.True(b.UseOracleServerParameterMetadata);
		Assert.False(b.UseCompression);
		Assert.False(b.UseAffectedRows);
		Assert.True(b.Pipelining);
		Assert.True(b.AutoEnlist);
		Assert.True(b.TreatTinyAsBoolean);
		Assert.True(b.UseXaTransactions);
		Assert.True(b.AutoClosePreparedStatements);
		Assert.False(b.PersistSecurityInfo);
		Assert.False(b.InteractiveSession);
		Assert.False((bool)b["OldGuids"]!);
		Assert.Equal(30u, b.DefaultCommandTimeout);
		Assert.Equal(2, b.CancellationTimeout);
		Assert.Equal(180u, b.ConnectionIdleTimeout);
		Assert.Equal(0u, b.ConnectionLifeTime);
		Assert.Equal(0u, b.MinimumPoolSize);
		Assert.Equal(0u, b.DnsCheckInterval);
		Assert.Equal(0u, b.Keepalive);
		Assert.Equal("MYSQL", b.PipeName);
		Assert.Equal("", b.CharacterSet);
		Assert.Equal("", b.ApplicationName);
		Assert.Equal(MySqlLoadBalance.RoundRobin, b.LoadBalance);
		Assert.Equal(OracleConnectionProtocol.Sockets, b.ConnectionProtocol);
		Assert.Equal(MySqlGuidFormat.Default, b.GuidFormat);
		Assert.Equal(MySqlDateTimeKind.Unspecified, b.DateTimeKind);
		Assert.Equal(MySqlServerRedirectionMode.Disabled, b.ServerRedirectionMode);
		Assert.Equal(MySqlCertificateStoreLocation.None, b.CertificateStoreLocation);
	}

	[Fact]
	public void Oracle_mode_defaults_differ_from_mysql_mode()
	{
		// IgnorePrepareExecuteSequenceCheck 在两种模式下默认值不同（Oracle 4.x 兼容）
		Assert.True(new OracleConnectionStringBuilder().IgnorePrepareExecuteSequenceCheck);
		Assert.False(new MySqlConnectionStringBuilder().IgnorePrepareExecuteSequenceCheck);
	}

	// ---------- Oracle：超时 / 池化 / 字符集别名 ----------

	[Fact]
	public void Oracle_timeout_and_pooling_aliases()
	{
		var b = new OracleConnectionStringBuilder(
			"Server=h;Connect Timeout=30;Command Timeout=44;min pool size=3;maxpoolsize=9;" +
			"Connection Lifetime=120;Connection Idle Timeout=60;Pooling=no;");
		Assert.Equal(30u, b.ConnectionTimeout);
		Assert.Equal(44u, b.DefaultCommandTimeout);
		Assert.Equal(3u, b.MinimumPoolSize);
		Assert.Equal(9u, b.MaximumPoolSize);
		Assert.Equal(120u, b.ConnectionLifeTime);
		Assert.Equal(60u, b.ConnectionIdleTimeout);
		Assert.False(b.Pooling);
		Assert.Contains("Connection Timeout=30", b.ConnectionString);
		Assert.Contains("Minimum Pool Size=3", b.ConnectionString);
	}

	[Fact]
	public void Oracle_compress_and_charset_aliases()
	{
		var b = new OracleConnectionStringBuilder("Server=h;Compress=true;CharSet=utf8mb4;");
		Assert.True(b.UseCompression);
		Assert.Equal("utf8mb4", b.CharacterSet);
		Assert.Contains("Use Compression=True", b.ConnectionString);
		Assert.Contains("Character Set=utf8mb4", b.ConnectionString);
	}

	[Fact]
	public void Oracle_cancellation_timeout_coerce_bounds()
	{
		Assert.Equal(-1, new OracleConnectionStringBuilder("Cancellation Timeout=-1;").CancellationTimeout);
		Assert.Throws<ArgumentOutOfRangeException>(() => new OracleConnectionStringBuilder("Cancellation Timeout=-2;"));
	}

	// ---------- Oracle：协议特有行为 ----------

	[Fact]
	public void Oracle_protocol_cannot_be_set_from_string()
	{
		// OracleConnectionStringValueOption.ChangeType 的枚举白名单只包含 MySqlConnectionProtocol，
		// 未包含 OracleConnectionProtocol，因此即便合法的协议名（甚至数值）也无法通过
		// Convert.ChangeType 转换，最终包装成 ArgumentException 抛出
		Assert.Throws<ArgumentException>(() => new OracleConnectionStringBuilder("Server=h;Protocol=Sockets;"));
		Assert.Throws<ArgumentException>(() => new OracleConnectionStringBuilder("Server=h;Connection Protocol=Tcp;"));
		Assert.Throws<ArgumentException>(() => new OracleConnectionStringBuilder("Server=h;Protocol=1;"));
	}

	[Fact]
	public void Oracle_protocol_property_set_persists_but_readback_throws()
	{
		// 强类型属性赋值本身可行：基类把值按 ToString() 字符串化存储
		//（枚举同值名取声明顺序的第一个，NamedPipe=2 序列化为 "Pipe"）
		var b = new OracleConnectionStringBuilder();
		b.ConnectionProtocol = OracleConnectionProtocol.NamedPipe;
		Assert.Contains("Connection Protocol=Pipe", b.ConnectionString);
		// 但由于白名单缺少 OracleConnectionProtocol，从字典读回时
		// Convert.ChangeType 无法把字符串转成该枚举，读取属性同样抛 ArgumentException
		Assert.Throws<ArgumentException>(() => _ = b.ConnectionProtocol);
	}

	// ---------- Oracle：TLS / 枚举 ----------

	[Theory]
	[InlineData("TLSv1.1,TLSv1.3", "TLS 1.1, TLS 1.3")]
	[InlineData("[TLSv1.0, TLSv1.2]", "TLS 1.0, TLS 1.2")]
	[InlineData("TLS", "TLS 1.0")]
	[InlineData("tlsv1.3", "TLS 1.3")]
	[InlineData("(TLSv1.2)", "TLS 1.2")]
	public void Oracle_tls_version_is_normalized(string input, string expected)
	{
		Assert.Equal(expected, new OracleConnectionStringBuilder($"Server=h;TLS Version={input};").TlsVersion);
	}

	[Theory]
	[InlineData("SSLv2")]
	[InlineData("TLSv1.4")]
	[InlineData("TLSv9.9")]
	public void Oracle_invalid_tls_version_throws(string value)
	{
		Assert.Throws<ArgumentException>(() => new OracleConnectionStringBuilder($"Server=h;TLS Version={value};"));
	}

	[Fact]
	public void Oracle_shared_enums_parse()
	{
		// 与 MySQL 模式共用 MySqlLoadBalance / MySqlGuidFormat / MySqlSslMode 枚举及解析白名单
		var b = new OracleConnectionStringBuilder(
			"Server=h;Load Balance=LeastConnections;GUID Format=Char32;SSL Mode=VerifyCA;");
		Assert.Equal(MySqlLoadBalance.LeastConnections, b.LoadBalance);
		Assert.Equal(MySqlGuidFormat.Char32, b.GuidFormat);
		Assert.Equal(MySqlSslMode.VerifyCA, b.SslMode);
	}

	// ---------- Oracle：索引器 / Keys / 选项注册表 ----------

	[Fact]
	public void Oracle_indexer_supports_alias_keys()
	{
		var b = new OracleConnectionStringBuilder();
		Assert.Equal(3306u, (uint)b["PORT"]!);
		b["connect timeout"] = "20";
		Assert.Equal(20u, b.ConnectionTimeout);
		b["ssl mode"] = "VerifyCA";
		Assert.Equal(MySqlSslMode.VerifyCA, b.SslMode);
		Assert.Throws<ArgumentException>(() => _ = b["nope"]);
	}

	[Fact]
	public void Oracle_keys_follow_option_registration_order()
	{
		var b = new OracleConnectionStringBuilder("Pooling=no;Port=2883;Server=h;");
		Assert.Equal(new[] { "Server", "Port", "Pooling" }, b.Keys.Cast<string>().ToArray());
	}

	[Fact]
	public void Oracle_option_registry_maps_synonyms_to_one_option()
	{
		var byConnect = OracleConnectionStringOption.TryGetOptionForKey("Connect Timeout");
		Assert.NotNull(byConnect);
		Assert.Equal("Connection Timeout", byConnect!.Key);
		Assert.Same(OracleConnectionStringOption.TryGetOptionForKey("CONNECTION TIMEOUT"), byConnect);
		// 任务描述中提到的 ODP.NET 风格选项在本重建源码中并未注册
		Assert.Null(OracleConnectionStringOption.TryGetOptionForKey("Statement Cache Size"));
		Assert.Throws<ArgumentException>(() => OracleConnectionStringOption.GetOptionForKey("Proxy User"));
	}

	// ---------- Oracle：重复参数校验 / 密码剥离 / 往返 ----------

	[Theory]
	[InlineData("Server=a;Data Source=b")]
	[InlineData("HOST=a;Addr=b")]
	[InlineData("Uid=u1;Username=u2")]
	[InlineData("CharSet=x;Character Set=y")]
	[InlineData("Connect Timeout=1;ConnectionTimeout=2")]
	[InlineData("NotAKeyword=1;NotAKeyword=2")]
	public void Oracle_duplicate_options_rejected_in_constructor(string connectionString)
	{
		var ex = Assert.Throws<ArgumentException>(() => new OracleConnectionStringBuilder(connectionString));
		Assert.Equal("connectionString", ex.ParamName);
		Assert.Contains("Duplicate", ex.Message);
	}

	[Fact]
	public void Oracle_get_connection_string_strips_password()
	{
		var b = new OracleConnectionStringBuilder("Server=h;User ID=u;pwd=secret;");
		var without = b.GetConnectionString(includePassword: false);
		Assert.DoesNotContain("secret", without, StringComparison.Ordinal);
		Assert.DoesNotContain("Password", without, StringComparison.Ordinal);
		Assert.Contains("User ID=u", without);
		Assert.Equal(b.ConnectionString, b.GetConnectionString(includePassword: true));
	}

	[Fact]
	public void Oracle_extended_options_roundtrip()
	{
		var b = new OracleConnectionStringBuilder(
			"Server=ob;Port=2883;User ID=u@t#c;Password=p;Database=s;" +
			"Connect Timeout=30;Command Timeout=44;Min Pool Size=2;Max Pool Size=8;" +
			"Pooling=no;CharSet=utf8mb4;Compress=true;Load Balance=random;" +
			"GUID Format=Char32;Auto Commit=no;SSL Mode=VerifyCA;Certificate Store Location=CurrentUser;" +
			"DateTime Kind=Utc;Server Redirection Mode=Preferred;DNS Check Interval=15;Keep Alive=30;");
		var b2 = new OracleConnectionStringBuilder(b.ConnectionString);
		Assert.Equal(b.ConnectionString, b2.ConnectionString);
		Assert.Equal(30u, b2.ConnectionTimeout);
		Assert.Equal(44u, b2.DefaultCommandTimeout);
		Assert.Equal(2u, b2.MinimumPoolSize);
		Assert.Equal(8u, b2.MaximumPoolSize);
		Assert.False(b2.Pooling);
		Assert.Equal("utf8mb4", b2.CharacterSet);
		Assert.True(b2.UseCompression);
		Assert.Equal(MySqlLoadBalance.Random, b2.LoadBalance);
		Assert.Equal(MySqlGuidFormat.Char32, b2.GuidFormat);
		Assert.False(b2.AutoCommit);
		Assert.Equal(MySqlSslMode.VerifyCA, b2.SslMode);
		Assert.Equal(MySqlCertificateStoreLocation.CurrentUser, b2.CertificateStoreLocation);
		Assert.Equal(MySqlDateTimeKind.Utc, b2.DateTimeKind);
		Assert.Equal(MySqlServerRedirectionMode.Preferred, b2.ServerRedirectionMode);
		Assert.Equal(15u, b2.DnsCheckInterval);
		Assert.Equal(30u, b2.Keepalive);
		Assert.Equal("u@t#c", b2.UserID);
	}
}
