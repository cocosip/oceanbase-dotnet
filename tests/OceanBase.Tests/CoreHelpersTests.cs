using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using OceanBase.Core;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// Core/Utilities 里无需服务端的小型辅助类型测试：
/// 版本号解析、命令列表游标、负载均衡器、过程参数缓存、CRC32、
/// 序列化整数读写、RSA/PEM 装载、时间跨度与重定向头解析等纯逻辑。
/// </summary>
public sealed class CoreHelpersTests
{
	// ---------- ServerVersion ----------

	[Theory]
	[InlineData("5.7.24", 5, 7, 24, -1)]
	[InlineData("8.0.31", 8, 0, 31, -1)]
	[InlineData("8.0", 8, 0, 0, -1)]          // 缺 build 时补 0（Revision 为 -1 表示三段版本）
	[InlineData("4.2.5.6", 4, 2, 5, 6)]       // 四段版本保留 Revision
	[InlineData("10.4.12-MariaDB", 10, 4, 12, -1)]
	public void ServerVersion_parses_version_numbers(string version, int major, int minor, int build, int revision)
	{
		var sv = new ServerObserver(version);
		Assert.Equal(major, sv.Version.Major);
		Assert.Equal(minor, sv.Version.Minor);
		Assert.Equal(build, sv.Version.Build);
		Assert.Equal(revision, sv.Version.Revision);
		Assert.Equal(version, sv.OriginalString);
	}

	[Fact]
	public void ServerVersion_detects_mariadb_suffix()
	{
		var sv = new ServerObserver("10.5.13-MariaDB-log");
		Assert.True(sv.IsMariaDb);
		// 版本取数字部分，后缀忽略
		Assert.Equal(new Version(10, 5, 13), sv.Version);
	}

	[Fact]
	public void ServerVersion_strips_5_5_5_mariadb_prefix()
	{
		// MariaDB 为兼容 MySQL 5.5 在版本串前加 "5.5.5-"，解析时应剥掉
		var sv = new ServerObserver("5.5.5-10.6.7-MariaDB");
		Assert.True(sv.IsMariaDb);
		Assert.Equal(new Version(10, 6, 7), sv.Version);
		// 原始串保留完整前缀
		Assert.Equal("5.5.5-10.6.7-MariaDB", sv.OriginalString);
	}

	[Fact]
	public void ServerVersion_mysql_version_is_not_mariadb()
	{
		Assert.False(new ServerObserver("8.0.31").IsMariaDb);
	}

	[Fact]
	public void ServerVersion_empty_is_zero_version()
	{
		// Empty 走私有构造：System.Version 的无参构造是 "0.0"（Build/Revision 均为 -1），
		// 与「解析失败」路径的 new Version(0,0,0) 不同，这里刻意固化该差异。
		Assert.Equal(new Version(0, 0), ServerVersion.Empty.Version);
		Assert.Equal(-1, ServerVersion.Empty.Version.Build);
		Assert.Equal(-1, ServerVersion.Empty.Version.Revision);
		Assert.Equal("", ServerVersion.Empty.OriginalString);
	}

	[Fact]
	public void ServerVersion_non_numeric_falls_back_to_zero()
	{
		var sv = new ServerObserver("unknown");
		Assert.Equal(new Version(0, 0, 0), sv.Version);
	}

	private readonly struct ServerObserver
	{
		public ServerObserver(string version)
		{
			// 构造函数接收 ReadOnlySpan<byte>，从字符串转 UTF-8
			var bytes = System.Text.Encoding.UTF8.GetBytes(version);
			var sv = new ServerVersion(bytes);
			Version = sv.Version;
			IsMariaDb = sv.IsMariaDb;
			OriginalString = sv.OriginalString;
		}

		public Version Version { get; }
		public bool IsMariaDb { get; }
		public string OriginalString { get; }
	}

	// ---------- ServerVersions 常量 ----------

	[Fact]
	public void ServerVersions_constants_have_expected_values()
	{
		Assert.Equal(new Version(5, 5, 3), ServerVersions.SupportsUtf8Mb4);
		Assert.Equal(new Version(5, 7, 3), ServerVersions.SupportsResetConnection);
		Assert.Equal(new Version(10, 2, 4), ServerVersions.MariaDbSupportsResetConnection);
		Assert.Equal(new Version(8, 0, 0), ServerVersions.RemovesMySqlProcTable);
		// OceanBase 特有的版本门槛
		Assert.Equal(new Version(2, 2, 7, 6), ServerVersions.OceanbaseSupportsPrepareExecute);
		Assert.Equal(new Version(4, 5, 1), ServerVersions.OceanbaseSupportsAccurateParameterInfo_4_5);
	}

	// ---------- CommandListPosition ----------

	[Fact]
	public void CommandListPosition_single_command_has_count_one()
	{
		using var command = new MySqlCommand();
		var position = new CommandListPosition(command);
		Assert.Equal(1, position.CommandCount);
		Assert.Equal(0, position.CommandIndex);
		Assert.Null(position.PreparedStatements);
		_ = position.CommandAt(0);
		Assert.Throws<ArgumentOutOfRangeException>(() => position.CommandAt(1));
	}

	[Fact]
	public void CommandListPosition_oracle_command_has_count_one()
	{
		using var command = new OracleCommand();
		var position = new CommandListPosition(command);
		Assert.Equal(1, position.CommandCount);
		_ = position.CommandAt(0);
		Assert.Throws<ArgumentOutOfRangeException>(() => position.CommandAt(-1));
	}

	[Fact]
	public void CommandListPosition_batch_list_tracks_command_count()
	{
		var commands = new List<MySqlBatchCommand> { new MySqlBatchCommand(), new MySqlBatchCommand(), new MySqlBatchCommand() };
		var position = new CommandListPosition(commands);
		Assert.Equal(3, position.CommandCount);
		_ = position.CommandAt(2);
		Assert.Throws<ArgumentOutOfRangeException>(() => position.CommandAt(3));
	}

	[Fact]
	public void CommandListPosition_unknown_object_yields_zero_count()
	{
		var position = new CommandListPosition("not a command");
		Assert.Equal(0, position.CommandCount);
		Assert.Throws<ArgumentOutOfRangeException>(() => position.CommandAt(0));
	}

	// ---------- 负载均衡器 ----------

	[Fact]
	public void RoundRobinLoadBalancer_rotates_through_hosts()
	{
		var balancer = new RoundRobinLoadBalancer();
		var hosts = new[] { "a", "b", "c" };
		Assert.Equal(new[] { "a", "b", "c" }, balancer.LoadBalance(hosts));
		Assert.Equal(new[] { "b", "c", "a" }, balancer.LoadBalance(hosts));
		Assert.Equal(new[] { "c", "a", "b" }, balancer.LoadBalance(hosts));
		// 计数器回绕后从头开始
		Assert.Equal(new[] { "a", "b", "c" }, balancer.LoadBalance(hosts));
	}

	[Fact]
	public void FailOverLoadBalancer_returns_hosts_unchanged()
	{
		var hosts = new[] { "a", "b", "c" };
		Assert.Same(hosts, FailOverLoadBalancer.Instance.LoadBalance(hosts));
	}

	[Fact]
	public void RandomLoadBalancer_returns_permutation_of_hosts()
	{
		var hosts = new[] { "a", "b", "c", "d" };
		var balanced = RandomLoadBalancer.Instance.LoadBalance(hosts);
		// 洗牌后必须是同元素集合且原数组不被修改
		Assert.Equal(4, balanced.Count);
		Assert.Equal(new HashSet<string>(hosts), new HashSet<string>(balanced));
		Assert.Equal(new[] { "a", "b", "c", "d" }, hosts);
	}

	// ---------- CachedParameter ----------

	[Theory]
	[InlineData(0, null, ParameterDirection.ReturnValue)]  // 位置 0 恒为返回值
	[InlineData(1, "in", ParameterDirection.Input)]
	[InlineData(2, "IN", ParameterDirection.Input)]        // 模式串大小写不敏感
	[InlineData(3, "inout", ParameterDirection.InputOutput)]
	[InlineData(4, "OUT", ParameterDirection.Output)]
	[InlineData(5, "whatever", (ParameterDirection)0)]     // 未知模式：保持默认值 0（非合法枚举成员，非 Input）
	public void CachedParameter_maps_direction(int position, string? mode, ParameterDirection expected)
	{
		// MySQL 侧构造：dataType 走 MySQL 元数据查找（"INT" → Int32）
		var parameter = new CachedParameter(position, mode, "p", "INT", unsigned: false, 0, MySqlGuidFormat.Default);
		Assert.Equal(expected, parameter.Direction);
		Assert.Equal(position, parameter.Position);
		Assert.Equal("p", parameter.Name);
		Assert.Equal(MySqlDbType.Int32, parameter.MySqlDbType);
	}

	[Theory]
	[InlineData(0, null, ParameterDirection.ReturnValue)]
	[InlineData(1, "in", ParameterDirection.Input)]
	[InlineData(2, "InOut", ParameterDirection.InputOutput)]
	[InlineData(3, "out", ParameterDirection.Output)]
	[InlineData(4, "bogus", (ParameterDirection)0)]        // 未知模式：保持默认值 0
	public void CachedParameter_oracle_ctor_maps_direction(int position, string? mode, ParameterDirection expected)
	{
		// Oracle 侧构造：dataType 走 Oracle 元数据查找（"NUMBER" → Int32）
		var parameter = new CachedParameter(position, mode, "p", "NUMBER", unsigned: false, 0);
		Assert.Equal(expected, parameter.Direction);
		Assert.Equal(OracleDbType.Int32, parameter.OracleDbType);
	}

	[Fact]
	public void CachedParameter_unknown_mode_is_not_a_defined_direction()
	{
		// ParameterDirection 的合法值为 1/2/3/6，没有 0；未知 mode 既不抛错也不回落到 Input，
		// 而是留下未定义值 0 —— 调用方若直接 switch 会落到 default 分支。
		var parameter = new CachedParameter(1, "bogus", "p", "INT", unsigned: false, 0, MySqlGuidFormat.Default);
		Assert.False(Enum.IsDefined(parameter.Direction));
		Assert.Equal(0, (int)parameter.Direction);
		Assert.NotEqual(ParameterDirection.Input, parameter.Direction);
	}

	[Fact]
	public void CachedParameter_unknown_type_name_throws()
	{
		// 类型名在元数据表中不存在时，MySQL / Oracle 两侧都返回 null 元数据，取成员即抛 NRE。
		// 固化这一「先决条件」行为，避免调用方误以为会得到默认枚举值。
		Assert.Throws<NullReferenceException>(
			() => new CachedParameter(1, "in", "p", "NOT_A_REAL_TYPE", unsigned: false, 0, MySqlGuidFormat.Default));
		Assert.Throws<NullReferenceException>(
			() => new CachedParameter(1, "in", "p", "NOT_A_REAL_TYPE", unsigned: false, 0));
	}

	[Fact]
	public void CachedParameter_oracle_ctor_sets_overload_and_udt()
	{
		var parameter = new CachedParameter(1, "in", "p", "NUMBER", unsigned: false, 0, "2", "MY_TYPE");
		Assert.Equal(OracleDbType.Int32, parameter.OracleDbType);
		Assert.Equal("2", parameter.Overload);
		Assert.Equal("MY_TYPE", parameter.UdtTypeName);
	}

	[Fact]
	public void CachedParameter_intermediate_ctor_leaves_overload_null()
	{
		var parameter = new CachedParameter(1, "out", "p", "VARCHAR2", unsigned: false, 10);
		Assert.Equal(ParameterDirection.Output, parameter.Direction);
		Assert.Null(parameter.Overload);
		Assert.Null(parameter.UdtTypeName);
	}

	// ---------- OceanBaseCRC32 ----------

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(0x31)]
	public void OceanBaseCRC32_matches_bitwise_reference(uint firstByte)
	{
		// 与逐位参考实现（同一多项式 0x82F63B78，即 CRC-32C）一致
		var data = new byte[] { (byte)firstByte, 0x32, 0x33, 0x34 };
		Assert.Equal(ReferenceCrc32C(data), OceanBaseCRC32.Calculate(data));
	}

	[Fact]
	public void OceanBaseCRC32_empty_buffer_is_zero()
	{
		Assert.Equal(0u, OceanBaseCRC32.Calculate(Array.Empty<byte>()));
	}

	[Fact]
	public void OceanBaseCRC32_known_check_value()
	{
		// 注意：本实现是「初值 0、无最终异或」的裸 CRC-32C，不是标准 CRC-32C。
		// 标准 CRC-32C("123456789") = 0xE3069283（初值 0xFFFFFFFF 且结果取反），
		// 而这里是 0x58E3FA20；两者不可直接比较（初值不同，非简单取反关系）。
		var check = OceanBaseCRC32.Calculate("123456789"u8.ToArray());
		Assert.Equal(0x58E3FA20u, check);
		// 与逐位参考实现自洽
		Assert.Equal(ReferenceCrc32C("123456789"u8), check);
		// 明确排除标准 CRC-32C 的校验值，防止有人「顺手改成」标准实现
		Assert.NotEqual(0xE3069283u, check);
	}

	[Fact]
	public void OceanBaseCRC32_is_order_sensitive()
	{
		Assert.NotEqual(OceanBaseCRC32.Calculate(new byte[] { 1, 2 }), OceanBaseCRC32.Calculate(new byte[] { 2, 1 }));
	}

	private static uint ReferenceCrc32C(ReadOnlySpan<byte> data)
	{
		uint crc = 0;
		foreach (var b in data)
		{
			crc ^= b;
			for (int i = 0; i < 8; i++)
				crc = (crc & 1) != 0 ? (crc >> 1) ^ 0x82F63B78u : crc >> 1;
		}
		return crc;
	}

	// ---------- SerializationUtility ----------

	[Fact]
	public void SerializationUtility_ReadUInt32_is_little_endian()
	{
		Assert.Equal(1u, SerializationUtility.ReadUInt32(new byte[] { 1, 0, 0, 0 }));
		Assert.Equal(0xFFu, SerializationUtility.ReadUInt32(new byte[] { 0xFF, 0, 0, 0 }));
		Assert.Equal(0x0201u, SerializationUtility.ReadUInt32(new byte[] { 1, 2 }));
	}

	[Fact]
	public void SerializationUtility_WriteUInt32_roundtrips()
	{
		foreach (var value in new[] { 0u, 1u, 0xFFu, 0xFFFFu, 0xFFFFFFu, 0xFFFFFFFFu })
		{
			var buffer = new byte[8];
			SerializationUtility.WriteUInt32(value, buffer, 2, 4);
			Assert.All(new byte[] { buffer[0], buffer[1], buffer[6], buffer[7] }, b => Assert.Equal(0, b));
			Assert.Equal(value, SerializationUtility.ReadUInt32(buffer.AsSpan(2, 4)));
		}
	}

	[Fact]
	public void SerializationUtility_WriteUInt32_truncates_to_count_bytes()
	{
		var buffer = new byte[2];
		SerializationUtility.WriteUInt32(0xABCD12, buffer, 0, 2);
		// 只写低 2 字节（小端），高位丢弃
		Assert.Equal(new byte[] { 0x12, 0xCD }, buffer);
	}

	// ---------- Packet ----------

	[Fact]
	public void Packet_holds_contents_segment()
	{
		var bytes = new byte[] { 10, 20, 30, 40 };
		var packet = new Packet(new ArraySegment<byte>(bytes, 1, 2));
		Assert.Equal(1, packet.Contents.Offset);
		Assert.Equal(2, packet.Contents.Count);
		Assert.Equal(20, packet.Contents.Array![packet.Contents.Offset]);
	}

	// ---------- ResizableArray / Utility.Resize ----------

	[Fact]
	public void Utility_Resize_allocates_and_grows()
	{
		ResizableArray<int>? array = null;
		Utility.Resize(ref array, 4);
		Assert.Equal(4, array!.Count);
		Utility.Resize(ref array, 10);
		Assert.True(array.Count >= 10);
	}

	[Fact]
	public void Utility_Resize_keeps_existing_contents()
	{
		ResizableArray<string>? array = null;
		Utility.Resize(ref array, 2);
		array.Array![0] = "a";
		array.Array![1] = "b";
		Utility.Resize(ref array, 4);
		Assert.Equal("a", array.Array![0]);
		Assert.Equal("b", array.Array[1]);
	}

	// ---------- Utility 杂项 ----------

	[Fact]
	public void Utility_Dispose_disposes_and_nulls_reference()
	{
		var disposable = new DisposableTracker();
		Utility.Dispose(ref disposable);
		Assert.Null(disposable);
		Utility.Dispose(ref disposable); // 对 null 再调一次不抛
	}

	[Fact]
	public void Utility_FindNextIndex_respects_offset_and_returns_minus_one()
	{
		var data = new byte[] { 1, 2, 3, 1, 2, 3 };
		Assert.Equal(0, Utility.FindNextIndex(data, 0, new byte[] { 1, 2 }));
		Assert.Equal(3, Utility.FindNextIndex(data, 1, new byte[] { 1, 2 }));
		Assert.Equal(-1, Utility.FindNextIndex(data, 0, new byte[] { 9 }));
	}

	[Fact]
	public void Utility_TrimZeroByte_removes_single_trailing_zero()
	{
		Assert.Equal(new byte[] { 1, 2 }, Utility.TrimZeroByte(new byte[] { 1, 2, 0 }));
		// 只去一个零：两个尾零剩一个
		Assert.Equal(new byte[] { 1, 0 }, Utility.TrimZeroByte(new byte[] { 1, 0, 0 }));
		Assert.Equal(new byte[] { 1, 2 }, Utility.TrimZeroByte(new byte[] { 1, 2 }));
		Assert.Null(Utility.TrimZeroByte((byte[]?)null));
		// Span 重载同语义（显式转型以命中 ReadOnlySpan<byte> 重载）
		Assert.Equal(new byte[] { 7 }, Utility.TrimZeroByte((ReadOnlySpan<byte>)new byte[] { 7, 0 }).ToArray());
		Assert.Equal(new byte[] { 7 }, Utility.TrimZeroByte((ReadOnlySpan<byte>)new byte[] { 7 }).ToArray());
	}

	[Fact]
	public void Utility_SwapBytes_exchanges_positions()
	{
		var bytes = new byte[] { 1, 2, 3, 4 };
		Utility.SwapBytes(bytes, 0, 3);
		Assert.Equal(new byte[] { 4, 2, 3, 1 }, bytes);
		Utility.SwapBytes(bytes, 1, 2);
		Assert.Equal(new byte[] { 4, 3, 2, 1 }, bytes);
	}

	[Fact]
	public void Utility_GetDefaultSslProtocols_is_none()
	{
		Assert.Equal(System.Security.Authentication.SslProtocols.None, Utility.GetDefaultSslProtocols());
	}

	[Fact]
	public void Utility_GetElapsedMilliseconds_is_nonnegative()
	{
		var start = StopwatchGetTimestamp();
		Assert.True(Utility.GetElapsedMilliseconds(start) >= 0);
		Assert.True(Utility.GetElapsedSeconds(start, StopwatchGetTimestamp()) >= 0);
	}

	private static long StopwatchGetTimestamp() => System.Diagnostics.Stopwatch.GetTimestamp();

	// ---------- ParseTimeSpan ----------

	[Theory]
	[InlineData("11:22:33", 11, 22, 33, 0, 0)]
	[InlineData("00:00:00", 0, 0, 0, 0, 0)]
	[InlineData("838:59:59", 838, 59, 59, 0, 0)]     // MySQL TIME 上界
	[InlineData("1:02:03.5", 1, 2, 3, 500, 0)]       // 一位小数按十分之一秒
	[InlineData("1:02:03.250", 1, 2, 3, 250, 0)]
	[InlineData("1:02:03.123456", 1, 2, 3, 123, 456)]
	public void ParseTimeSpan_parses_mysql_time_format(string text, int h, int m, int s, int ms, int us)
	{
		var value = Utility.ParseTimeSpan(System.Text.Encoding.UTF8.GetBytes(text));
		Assert.Equal(new TimeSpan(0, h, m, s, ms) + TimeSpan.FromTicks(us * 10), value);
	}

	[Fact]
	public void ParseTimeSpan_negates_all_components()
	{
		var value = Utility.ParseTimeSpan(System.Text.Encoding.UTF8.GetBytes("-1:02:03"));
		Assert.Equal(new TimeSpan(0, 1, 2, 3) * -1, value);
	}

	[Theory]
	[InlineData("abc")]        // 非数字
	[InlineData("839:00:00")]  // 小时超界
	[InlineData("01:60:00")]   // 分钟超界
	[InlineData("1:2:3")]      // 分/秒必须两位
	[InlineData("11:22")]      // 缺秒段
	[InlineData("11:22:33.")]  // 空小数段
	public void ParseTimeSpan_rejects_invalid_input(string text)
	{
		Assert.Throws<FormatException>(() => Utility.ParseTimeSpan(System.Text.Encoding.UTF8.GetBytes(text)));
	}

	// ---------- TryParseRedirectionHeader ----------

	[Fact]
	public void TryParseRedirectionHeader_parses_mysql_url()
	{
		var ok = Utility.TryParseRedirectionHeader("mysql://someuser@newhost:3307", "olduser", out var host, out var port, out var user);
		Assert.True(ok);
		Assert.Equal("newhost", host);
		Assert.Equal(3307, port);
		Assert.Equal("someuser", user);
	}

	[Fact]
	public void TryParseRedirectionHeader_supports_mariadb_and_default_port()
	{
		var ok = Utility.TryParseRedirectionHeader("mariadb://u@h", "olduser", out var host, out var port, out var user);
		Assert.True(ok);
		Assert.Equal("h", host);
		Assert.Equal(-1, port); // Uri 未显式给端口时为 -1
		Assert.Equal("u", user);
	}

	[Fact]
	public void TryParseRedirectionHeader_falls_back_to_query_then_initial_user()
	{
		Assert.True(Utility.TryParseRedirectionHeader("mysql://h?user=queried", "initial", out _, out _, out var fromQuery));
		Assert.Equal("queried", fromQuery);
		Assert.True(Utility.TryParseRedirectionHeader("mysql://h", "initial", out _, out _, out var fromInitial));
		Assert.Equal("initial", fromInitial);
	}

	[Theory]
	[InlineData("http://h")]
	[InlineData("not a url at all")]
	public void TryParseRedirectionHeader_rejects_non_mysql_scheme(string url)
	{
		Assert.False(Utility.TryParseRedirectionHeader(url, "u", out _, out _, out _));
	}

	// ---------- LoadRsaParameters ----------

	[Fact]
	public void LoadRsaParameters_imports_pkcs1_private_key()
	{
		using var rsa = RSA.Create(2048);
		var pem = System.Text.Encoding.ASCII.GetBytes(rsa.ExportRSAPrivateKeyPem());
		using var imported = RSA.Create();
		Utility.LoadRsaParameters(pem, imported);
		Assert.Equal(rsa.ExportParameters(false).Modulus, imported.ExportParameters(false).Modulus);
	}

	[Fact]
	public void LoadRsaParameters_imports_subject_public_key()
	{
		using var rsa = RSA.Create(2048);
		var pem = System.Text.Encoding.ASCII.GetBytes(rsa.ExportSubjectPublicKeyInfoPem());
		using var imported = RSA.Create();
		Utility.LoadRsaParameters(pem, imported);
		var expected = rsa.ExportParameters(false);
		var actual = imported.ExportParameters(false);
		Assert.Equal(expected.Modulus, actual.Modulus);
		Assert.Equal(expected.Exponent, actual.Exponent);
	}

	[Fact]
	public void LoadRsaParameters_rejects_non_pem_data()
	{
		using var rsa = RSA.Create();
		Assert.Throws<FormatException>(() => Utility.LoadRsaParameters(System.Text.Encoding.ASCII.GetBytes("garbage"), rsa));
	}

	private sealed class DisposableTracker : IDisposable
	{
		public bool Disposed { get; private set; }
		public void Dispose() => Disposed = true;
	}
}
