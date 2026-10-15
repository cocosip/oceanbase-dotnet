using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 行为对齐校验（差分测试）。
///
/// <see cref="PublicApiParityTests"/> 只比对“签名是否一致”；本测试进一步**真正执行**
/// 原始程序集与重建程序集，在无网络依赖的纯逻辑路径上逐项比对结果，
/// 用于发现“签名一致但行为不同”的重建缺陷。
///
/// 原始程序集属开发期校验资产（不进版本库），放置方式见 <see cref="BaselineLocator"/>。
///
/// 实现要点（踩过的坑，勿改）：
///   <c>Keys</c> 只列出**已显式设置**的关键字，空连接串时为 0 项。
///   若直接遍历 Keys 做比对，两边都是空字符串 → 测试恒过（假绿）。
///   因此这里用固定的全量关键字清单 + 索引器读取每个关键字的取值/默认值。
/// </summary>
public sealed class BehaviorParityTests
{
	private static readonly Assembly? s_original = TryLoadOriginalAssembly();
	private static readonly Assembly s_rebuilt = typeof(MySqlConnection).Assembly;

	/// <summary>MySQL 模式的全量关键字（含别名，覆盖官方文档与原始程序集实测）。</summary>
	private static readonly string[] s_mySqlKeywords =
	{
		"Server", "Host", "Data Source", "DataSource", "Address", "Addr", "Network Address",
		"Port", "User ID", "UserID", "Username", "Uid", "User name", "User",
		"Password", "pwd", "Database", "Initial Catalog",
		"Load Balance", "Connection Protocol", "Pipe Name", "SslMode",
		"Certificate File", "Certificate Password", "Certificate Store Location", "Certificate Thumbprint",
		"SslCert", "SslKey", "SslCa", "TlsVersion", "TlsCipherSuites",
		"Allow User Variables", "Allow Load Local Infile", "Allow Public Key Retrieval",
		"Auto Enlist", "Interactive Session", "Persist Security Info", "Pooling",
		"Minimum Pool Size", "Maximum Pool Size", "Connection Idle Timeout", "Connection Life Time",
		"Connection Idle Ping Time", "Connection Reset", "Connection Timeout", "Default Command Timeout",
		"Use Affected Rows", "Treat Tiny As Boolean", "Convert Zero Datetime", "Allow Zero Datetime",
		"Guid Format", "Old Guids", "DateTime Kind", "Character Set",
		"Use Compression", "Pipelining", "Keep Alive", "Server Redirection Mode",
		"Ignore Prepare", "Ignore Command Transaction", "Auto Close Prepared Statements",
		"Use Xa Transactions", "Defer Connection Reset", "Force Synchronous", "No Backslash Escapes",
		"Use Oracle Prepare Execute", "Use Lob Locator V2", "Use Array Binding", "Array Bind Count",
		"Prepare Execute Fetch Size", "Server SPN", "Server RSA Public Key File", "Cancellation Timeout",
		"Dns Check Interval", "Application Name", "Use Oracle Server Parameter Metadata",
		"Ignore Prepare Execute Sequence Check",
	};

	/// <summary>Oracle 模式的全量关键字。</summary>
	private static readonly string[] s_oracleKeywords =
	{
		"Server", "Host", "Data Source", "DataSource", "Address", "Addr", "Network Address",
		"Port", "User ID", "UserID", "Username", "Uid", "User name", "User",
		"Password", "pwd", "Database", "Initial Catalog",
		"Load Balance", "Connection Protocol", "Pipe Name", "SslMode",
		"Certificate File", "Certificate Password", "Certificate Store Location", "Certificate Thumbprint",
		"SslCert", "SslKey", "SslCa", "TlsVersion", "TlsCipherSuites",
		"Allow User Variables", "Allow Load Local Infile", "Allow Public Key Retrieval",
		"Auto Enlist", "Interactive Session", "Persist Security Info", "Pooling",
		"Minimum Pool Size", "Maximum Pool Size", "Connection Idle Timeout", "Connection Life Time",
		"Connection Idle Ping Time", "Connection Reset", "Connection Timeout", "Default Command Timeout",
		"Use Affected Rows", "Treat Tiny As Boolean", "Convert Zero Datetime", "Allow Zero Datetime",
		"Guid Format", "Old Guids", "DateTime Kind", "Character Set",
		"Use Compression", "Pipelining", "Keep Alive", "Server Redirection Mode",
		"Ignore Prepare", "Ignore Command Transaction", "Auto Close Prepared Statements",
		"Use Xa Transactions", "Defer Connection Reset", "Force Synchronous", "No Backslash Escapes",
		"Use Oracle Prepare Execute", "Use Lob Locator V2", "Use Array Binding", "Array Bind Count",
		"Prepare Execute Fetch Size", "Server SPN", "Server RSA Public Key File", "Cancellation Timeout",
		"Dns Check Interval", "Application Name", "Auto Commit",
		"Use Oracle Server Parameter Metadata", "Ignore Prepare Execute Sequence Check",
	};

	/// <summary>MySQL 模式语料：覆盖别名、取值形态、大小写、空白。</summary>
	private static readonly string[] s_mySqlCorpus =
	{
		"",
		"Server=host;Port=2881;User ID=u@t#c;Password=pw;Database=db;",
		"Host=obhost;Port=2883;Username=root@sys#ob;Pwd=pw;Database=test;",
		"Data Source=10.0.0.1;Addr=10.0.0.2;Network Address=10.0.0.3;Uid=u;Password=p;",
		"Server=h;Initial Catalog=mydb;User=u;Password=p;",
		"Server=h;SslMode=None;Pooling=false;Connection Reset=true;",
		"Server=h;SslMode=Required;SslCa=ca.pem;SslCert=cert.pem;SslKey=key.pem;",
		"Server=h;SslMode=VerifyCA;TlsVersion=TLSv1.2;",
		"Server=h;Connection Timeout=30;Default Command Timeout=60;Maximum Pool Size=50;Minimum Pool Size=5;",
		"Server=h;Connection Idle Timeout=120;Connection Life Time=300;Connection Idle Ping Time=60;",
		"Server=h;Allow User Variables=true;Allow Load Local Infile=true;Allow Public Key Retrieval=true;",
		"Server=h;Use Affected Rows=true;Treat Tiny As Boolean=false;Convert Zero Datetime=true;Allow Zero Datetime=true;",
		"Server=h;Guid Format=Char36;Old Guids=true;DateTime Kind=Utc;",
		"Server=h;Character Set=utf8mb4;Use Compression=true;Pipelining=true;Keep Alive=30;",
		"Server=h;Load Balance=RoundRobin;Connection Protocol=Sockets;Server Redirection Mode=Disabled;",
		"Server=h;Auto Enlist=false;Interactive Session=true;Persist Security Info=true;",
		"Server=h;Certificate Store Location=CurrentUser;Certificate Thumbprint=abc;",
		"Server=h;Ignore Prepare=true;Ignore Command Transaction=true;Auto Close Prepared Statements=false;",
		"Server=h;Use Xa Transactions=true;Defer Connection Reset=true;Force Synchronous=true;",
		"Server=h;No Backslash Escapes=true;Use Oracle Prepare Execute=true;Use Lob Locator V2=false;",
		"Server=h;Use Array Binding=true;Array Bind Count=10;Prepare Execute Fetch Size=100;",
		"Server=h;Server SPN=mysql/host;Server RSA Public Key File=k.pem;Cancellation Timeout=5;",
		"Server=h;Pipe Name=mysql;Dns Check Interval=60;Application Name=myapp;",
		"Server=h;Use Oracle Server Parameter Metadata=true;Ignore Prepare Execute Sequence Check=true;",
		"Server=h;Port=3306;",
		"Server=h;Database=;User ID=;Password=;",
		"server=h;port=2881;user id=u;password=p;database=d;",
		"  Server = h ; Port = 2881 ; ",
		"Server=h;Unknown Keyword=xyz;",
	};

	/// <summary>Oracle 模式语料。</summary>
	private static readonly string[] s_oracleCorpus =
	{
		"",
		"Server=host;Port=2883;Database=schema;User Id=u@t#c;Password=pw;",
		"Data Source=host:2883;User Id=u@t#c;Password=pw;Database=schema;",
		"Server=h;Database=s;User ID=u;Password=p;SslMode=None;",
		"Server=h;Database=s;User ID=u;Password=p;Auto Commit=false;",
		"Server=h;Database=s;User ID=u;Password=p;Use Lob Locator V2=true;",
		"Server=h;Database=s;User ID=u;Password=p;Use Array Binding=true;Array Bind Count=5;",
		"Server=h;Database=s;User ID=u;Password=p;Character Set=gbk;",
		"Server=h;Database=s;User ID=u;Password=p;Charset=gbk;",
		"Server=h;Database=s;User ID=u;Password=p;CharacterSet=gbk;",
		"Server=h;Database=s;User ID=u;Password=p;Pooling=true;Maximum Pool Size=20;",
		"Server=h;Database=s;User ID=u;Password=p;TlsVersion=TLSv1.2;",
		"Server=h;Database=s;User ID=u;Password=p;Connection Protocol=UnixSocket;",
		"Server=h;Database=s;User ID=u;Password=p;Load Balance=FailOver;",
	};

	/// <summary>非法取值：两边应抛同样的异常类型与消息。</summary>
	private static readonly string[] s_invalidCorpus =
	{
		"Server=h;Port=abc;",
		"Server=h;Port=-1;",
		"Server=h;SslMode=NotAMode;",
		"Server=h;Guid Format=Bogus;",
		"Server=h;Load Balance=Bogus;",
		"Server=h;Connection Protocol=Bogus;",
		"Server=h;DateTime Kind=Bogus;",
		"Server=h;TlsVersion=SSLv3;",
		"Server=h;TlsVersion=TLSv9.9;",
		"Server=h;Certificate Store Location=Bogus;",
		"Server=h;Server Redirection Mode=Bogus;",
		"Server=h;Maximum Pool Size=notanumber;",
	};

	[Fact]
	public void ConnectionStringBuilder_behaves_identically_to_the_original()
	{
		Assert.SkipUnless(s_original is not null, BaselineLocator.SkipReason);
		var original = s_original!;
		var report = new StringBuilder();
		var comparisons = 0;
		var mismatches = 0;

		foreach (var (typeName, keywords, corpus, label) in new[]
		{
			("OceanBase.MySqlConnectionStringBuilder", s_mySqlKeywords, s_mySqlCorpus, "MySql"),
			("OceanBase.OracleConnectionStringBuilder", s_oracleKeywords, s_oracleCorpus, "Oracle"),
		})
		{
			// 1) 空连接串下的全部关键字默认值（最关键：暴露默认值漂移）
			comparisons++;
			mismatches += Compare(report, $"{label}: defaults (all {keywords.Length} keywords)",
				() => DumpKeywords(original, typeName, "", keywords),
				() => DumpKeywords(s_rebuilt, typeName, "", keywords));

			// 2) 语料库：每个连接串导出全部关键字 + ConnectionString
			foreach (var cs in corpus)
			{
				comparisons++;
				mismatches += Compare(report,
					$"{label}: [{cs}]",
					() => DumpKeywords(original, typeName, cs, keywords),
					() => DumpKeywords(s_rebuilt, typeName, cs, keywords));
			}

			// 3) 非法取值 → 异常类型与消息
			foreach (var cs in s_invalidCorpus)
			{
				comparisons++;
				mismatches += Compare(report,
					$"{label}: invalid [{cs}]",
					() => CreateOutcome(original, typeName, cs),
					() => CreateOutcome(s_rebuilt, typeName, cs));
			}
		}

		// 防空转：确保确实做了大量比对
		Assert.True(comparisons >= 50, $"比对次数过少（{comparisons}），测试可能失效。");
		Assert.True(mismatches == 0, $"共 {comparisons} 组比对，发现 {mismatches} 处行为差异：\n{report}");
	}

	[Fact]
	public void Public_enums_have_identical_members_and_values()
	{
		Assert.SkipUnless(s_original is not null, BaselineLocator.SkipReason);
		var original = s_original!;
		var report = new StringBuilder();
		var mismatches = 0;
		var count = 0;

		foreach (var enumName in new[]
		{
			"OceanBase.MySqlSslMode", "OceanBase.MySqlGuidFormat", "OceanBase.MySqlLoadBalance",
			"OceanBase.MySqlConnectionProtocol", "OceanBase.MySqlDateTimeKind",
			"OceanBase.MySqlServerRedirectionMode", "OceanBase.OracleConnectionProtocol",
			"OceanBase.MySqlCertificateStoreLocation", "OceanBase.MySqlDbType", "OceanBase.OracleDbType",
			"OceanBase.MySqlBulkLoaderConflictOption", "OceanBase.MySqlBulkLoaderPriority",
		})
		{
			count++;
			mismatches += Compare(report, $"enum {enumName}",
				() => DumpEnum(original, enumName),
				() => DumpEnum(s_rebuilt, enumName));
		}

		Assert.True(count >= 10, $"枚举比对数量过少（{count}）。");
		Assert.True(mismatches == 0, $"发现 {mismatches} 处枚举差异：\n{report}");
	}

	[Fact]
	public void Connection_open_fails_identically_without_a_server()
	{
		Assert.SkipUnless(s_original is not null, BaselineLocator.SkipReason);
		var original = s_original!;
		var report = new StringBuilder();
		var mismatches = 0;

		mismatches += Compare(report, "MySqlConnection.Open (no server)",
			() => OpenOutcome(original, "OceanBase.MySqlConnection", "Server=127.0.0.1;Port=1;User ID=u;Password=p;Database=d;"),
			() => OpenOutcome(s_rebuilt, "OceanBase.MySqlConnection", "Server=127.0.0.1;Port=1;User ID=u;Password=p;Database=d;"));

		mismatches += Compare(report, "OracleConnection.Open without Database (guard)",
			() => OpenOutcome(original, "OceanBase.OracleConnection", "Server=127.0.0.1;Port=1;User Id=u;Password=p;"),
			() => OpenOutcome(s_rebuilt, "OceanBase.OracleConnection", "Server=127.0.0.1;Port=1;User Id=u;Password=p;"));

		mismatches += Compare(report, "OracleConnection.Open with Database (no server)",
			() => OpenOutcome(original, "OceanBase.OracleConnection", "Server=127.0.0.1;Port=1;User Id=u;Password=p;Database=s;"),
			() => OpenOutcome(s_rebuilt, "OceanBase.OracleConnection", "Server=127.0.0.1;Port=1;User Id=u;Password=p;Database=s;"));

		Assert.True(mismatches == 0, $"发现 {mismatches} 处行为差异：\n{report}");
	}

	[Fact]
	public void Value_types_roundtrip_identically()
	{
		Assert.SkipUnless(s_original is not null, BaselineLocator.SkipReason);
		var original = s_original!;
		var report = new StringBuilder();
		var mismatches = 0;
		var count = 0;

		foreach (var (typeName, samples) in new (string, string[])[]
		{
			("OceanBase.MySqlDateTime", new[] { "2024-01-02 03:04:05", "0000-00-00", "2024-01-02 03:04:05.123456", "9999-12-31 23:59:59" }),
			("OceanBase.MySqlDecimal", new[] { "1.5", "-2.25", "0", "123456789.123456789" }),
			("OceanBase.MySqlGeometry", new[] { "POINT(1 2)", "LINESTRING(0 0,1 1)", "POLYGON((0 0,1 0,1 1,0 0))" }),
			("OceanBase.OracleIntervalDS", new[] { "1 2:3:4.5", "-1 2:3:4.5" }),
			("OceanBase.OracleIntervalYM", new[] { "1-2", "-1-2" }),
			("OceanBase.OracleTimeStamp", new[] { "2024-01-02 03:04:05.123456", "2024-01-02 03:04:05" }),
			("OceanBase.OracleTimeStampLTZ", new[] { "2024-01-02 03:04:05.123456" }),
			("OceanBase.OracleTimeStampTZ", new[] { "2024-01-02 03:04:05.123456 +08:00" }),
		})
		{
			foreach (var sample in samples)
			{
				count++;
				mismatches += Compare(report, $"{typeName}.Parse(\"{sample}\")",
					() => ParseOutcome(original, typeName, sample),
					() => ParseOutcome(s_rebuilt, typeName, sample));
			}
		}

		Assert.True(count >= 15, $"值类型比对数量过少（{count}）。");
		Assert.True(mismatches == 0, $"发现 {mismatches} 处行为差异：\n{report}");
	}

	// ---------- helpers ----------

	private static int Compare(StringBuilder report, string label, Func<string> original, Func<string> rebuilt)
	{
		string a, b;
		try { a = original(); }
		catch (Exception ex) { a = "EX:" + ex.GetType().Name + ":" + ex.Message; }
		try { b = rebuilt(); }
		catch (Exception ex) { b = "EX:" + ex.GetType().Name + ":" + ex.Message; }

		if (string.Equals(a, b, StringComparison.Ordinal))
			return 0;

		report.AppendLine($"--- {label}");
		report.AppendLine("  原始: " + Truncate(a));
		report.AppendLine("  重建: " + Truncate(b));
		return 1;
	}

	private static string Truncate(string s) => s.Length <= 800 ? s : s.Substring(0, 800) + "…";

	/// <summary>
	/// 用固定关键字清单 + 索引器导出取值。不要遍历 Keys：
	/// Keys 只含“已显式设置”的关键字，空连接串时为 0 项，会导致比对恒空（假绿）。
	/// </summary>
	private static string DumpKeywords(Assembly asm, string typeName, string connectionString, string[] keywords)
	{
		var type = asm.GetType(typeName, throwOnError: true)!;
		var builder = Activator.CreateInstance(type, new object?[] { connectionString })!;
		var indexer = type.GetProperty("Item", new[] { typeof(string) })!;

		var sb = new StringBuilder();
		foreach (var key in keywords)
		{
			string value;
			try
			{
				var v = indexer.GetValue(builder, new object?[] { key });
				value = v is null ? "(null)" : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? "(null)";
			}
			catch (Exception ex)
			{
				value = "EX:" + ex.GetType().Name;
			}
			sb.Append(key).Append('=').Append(value).Append('\n');
		}
		sb.Append("ConnectionString=").Append(type.GetProperty("ConnectionString")!.GetValue(builder));
		return sb.ToString();
	}

	private static string CreateOutcome(Assembly asm, string typeName, string connectionString)
	{
		var type = asm.GetType(typeName, throwOnError: true)!;
		Activator.CreateInstance(type, new object?[] { connectionString });
		return "OK";
	}

	private static string DumpEnum(Assembly asm, string enumName)
	{
		var type = asm.GetType(enumName, throwOnError: true)!;
		return string.Join("\n", Enum.GetNames(type)
			.Zip(Enum.GetValues(type).Cast<object>(), (n, v) => $"{n} = {Convert.ToInt64(v)}"));
	}

	private static string OpenOutcome(Assembly asm, string typeName, string connectionString)
	{
		var type = asm.GetType(typeName, throwOnError: true)!;
		var conn = (IDisposable)Activator.CreateInstance(type, new object?[] { connectionString })!;
		try
		{
			type.GetMethod("Open", Type.EmptyTypes)!.Invoke(conn, null);
			return "OPENED";
		}
		catch (TargetInvocationException ex)
		{
			var inner = ex.InnerException!;
			var msg = inner.Message.Replace("\r", " ").Replace("\n", " ");
			var cut = msg.IndexOf('.');
			return inner.GetType().Name + ": " + (cut > 0 ? msg.Substring(0, cut) : msg);
		}
		finally { conn.Dispose(); }
	}

	private static string ParseOutcome(Assembly asm, string typeName, string sample)
	{
		var type = asm.GetType(typeName, throwOnError: true)!;

		var parse = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
		object? value;
		if (parse is not null)
		{
			value = parse.Invoke(null, new object?[] { sample });
		}
		else
		{
			var ctor = type.GetConstructor(new[] { typeof(string) });
			if (ctor is null)
				return "NO_PARSE_API";
			value = ctor.Invoke(new object?[] { sample });
		}

		return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "(null)";
	}

	/// <summary>
	/// 加载基准程序集。基准属开发期校验资产，不进版本库；
	/// 找不到时由调用方通过 <see cref="BaselineLocator.SkipReason"/> 跳过测试。
	/// </summary>
	private static Assembly? TryLoadOriginalAssembly()
	{
		var path = BaselineLocator.Find("net10.0");
		return path is null ? null : Assembly.LoadFrom(path);
	}
}
