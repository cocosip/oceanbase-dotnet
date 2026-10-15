using System;
using System.Text;
using OceanBase.Core;
using OceanBase.Protocol;
using OceanBase.Protocol.Payloads;
using OceanBase.Protocol.Serialization;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 握手阶段两个「客户端 → 服务端」报文的编码测试（internal，无需服务端）。
///
/// <see cref="HandshakeResponse41Payload"/> 与 <see cref="ChangeUserPayload"/> 决定
/// 客户端声明的能力位、字符集、认证响应与用户名/schema 的改写规则；
/// 它们一旦编码错位，服务端会直接拒绝连接或按错误 schema 解析后续 SQL。
/// </summary>
public sealed class HandshakePayloadTests
{
	private static ConnectionSettings Settings(string connectionString)
	{
		return new ConnectionSettings(new MySqlConnectionStringBuilder(connectionString));
	}

	/// <summary>
	/// 构造 v10 握手包。第二段 auth data 仅在服务端声明 <c>SecureConnection</c> 时写入，
	/// 插件名仅在声明 <c>PluginAuth</c> 时写入 —— 与 <see cref="InitialHandshakePayload.Create"/>
	/// 的解析分支一一对应，否则会因多出字节而抛 <see cref="FormatException"/>。
	/// </summary>
	private static InitialHandshakePayload Handshake(
		ProtocolCapabilities capabilities = ProtocolCapabilities.LongPassword | ProtocolCapabilities.Protocol41
			| ProtocolCapabilities.SecureConnection | ProtocolCapabilities.PluginAuth,
		string authPluginName = "mysql_native_password")
	{
		var secureConnection = capabilities.HasFlag(ProtocolCapabilities.SecureConnection);
		var pluginAuth = capabilities.HasFlag(ProtocolCapabilities.PluginAuth);

		var w = new ByteBufferWriter(128);
		w.Write((byte)10);
		w.WriteNullTerminatedString("5.7.36-oceanbase");
		w.Write(0x11223344);
		w.Write(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
		w.Write((byte)0);
		w.Write((ushort)((ulong)capabilities & 0xFFFF));
		w.Write((byte)45);
		w.Write((ushort)ServerStatus.AutoCommit);
		w.Write((ushort)(((ulong)capabilities >> 16) & 0xFFFF));
		w.Write((byte)21);                       // auth data 总长
		w.Write(new byte[10]);                   // 保留字段（6 + 4）
		if (secureConnection)
		{
			w.Write(new byte[13]);               // auth data 第二段（max(13, 21-8)）
		}
		if (pluginAuth)
		{
			w.WriteNullTerminatedString(authPluginName);
		}
		return InitialHandshakePayload.Create(w.ToPayloadData().Span);
	}

	// ---------- HandshakeResponse41Payload.Create ----------

	[Fact]
	public void HandshakeResponse41_starts_with_capabilities_and_max_packet()
	{
		var cs = Settings("Server=h;User Id=u;Password=p;");
		var bytes = HandshakeResponse41Payload.Create(
			Handshake(), cs, new byte[] { 0xAA }, CompressionMethod.None, null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();

		var reader = new ByteArrayReader(bytes);
		var capabilities = (ProtocolCapabilities)(uint)reader.ReadInt32();
		Assert.Equal(1073741824, reader.ReadInt32());
		Assert.Equal((byte)CharacterSet.Utf8Mb4GeneralCaseInsensitive, reader.ReadByte());

		// 恒定声明的能力位
		Assert.True(capabilities.HasFlag(ProtocolCapabilities.Protocol41));
		Assert.True(capabilities.HasFlag(ProtocolCapabilities.SecureConnection));
		Assert.True(capabilities.HasFlag(ProtocolCapabilities.MultiStatements));
		Assert.True(capabilities.HasFlag(ProtocolCapabilities.MultiResults));
		Assert.True(capabilities.HasFlag(ProtocolCapabilities.QueryAttributes));
		// ClientSupportLobLocator = 0x20000000 恒置位
		Assert.True(capabilities.HasFlag(ProtocolCapabilities.ClientSupportLobLocator));
		// 服务端未提供的能力不应出现
		Assert.False(capabilities.HasFlag(ProtocolCapabilities.SessionTrack));
		Assert.False(capabilities.HasFlag(ProtocolCapabilities.Compress));
	}

	[Fact]
	public void HandshakeResponse41_writes_user_and_auth_response()
	{
		var cs = Settings("Server=h;User Id=alice;Password=p;");
		var bytes = HandshakeResponse41Payload.Create(
			Handshake(), cs, new byte[] { 0xDE, 0xAD }, CompressionMethod.None, null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();

		// 跳过 4(cap) + 4(maxpacket) + 1(charset) + 19(reserved) + 4(高 32 位)
		var offset = 32;
		Assert.Equal((byte)'a', bytes[offset]);
		Assert.Equal((byte)'l', bytes[offset + 1]);
		Assert.Equal((byte)'i', bytes[offset + 2]);
		Assert.Equal((byte)'c', bytes[offset + 3]);
		Assert.Equal((byte)'e', bytes[offset + 4]);
		Assert.Equal(0, bytes[offset + 5]);                 // NUL 结尾
		Assert.Equal(2, bytes[offset + 6]);                 // 认证响应长度
		Assert.Equal(0xDE, bytes[offset + 7]);
		Assert.Equal(0xAD, bytes[offset + 8]);
	}

	[Fact]
	public void HandshakeResponse41_omits_database_when_not_set()
	{
		var cs = Settings("Server=h;User Id=u;");
		var bytes = HandshakeResponse41Payload.Create(
			Handshake(), cs, Array.Empty<byte>(), CompressionMethod.None, null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();

		// 未设 Database → 不声明 ConnectWithDatabase(8) 能力位
		var capabilities = (ProtocolCapabilities)(uint)new ByteArrayReader(bytes).ReadInt32();
		Assert.False(capabilities.HasFlag(ProtocolCapabilities.ConnectWithDatabase));
	}

	[Fact]
	public void HandshakeResponse41_declares_database_capability_when_set()
	{
		var cs = Settings("Server=h;User Id=u;Database=mydb;");
		var bytes = HandshakeResponse41Payload.Create(
			Handshake(), cs, Array.Empty<byte>(), CompressionMethod.None, null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();

		var capabilities = (ProtocolCapabilities)(uint)new ByteArrayReader(bytes).ReadInt32();
		Assert.True(capabilities.HasFlag(ProtocolCapabilities.ConnectWithDatabase));
		Assert.Contains("mydb", Encoding.UTF8.GetString(bytes));
	}

	[Fact]
	public void HandshakeResponse41_use_affected_rows_toggles_found_rows_bit()
	{
		// 默认 UseAffectedRows=false → 声明 FoundRows
		var defaultCs = Settings("Server=h;User Id=u;");
		var defaultBytes = HandshakeResponse41Payload.Create(
			Handshake(), defaultCs, Array.Empty<byte>(), CompressionMethod.None, null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();
		Assert.True(((ProtocolCapabilities)(uint)new ByteArrayReader(defaultBytes).ReadInt32())
			.HasFlag(ProtocolCapabilities.FoundRows));

		// UseAffectedRows=true → 不声明 FoundRows
		var affectedCs = Settings("Server=h;User Id=u;Use Affected Rows=true;");
		var affectedBytes = HandshakeResponse41Payload.Create(
			Handshake(), affectedCs, Array.Empty<byte>(), CompressionMethod.None, null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();
		Assert.False(((ProtocolCapabilities)(uint)new ByteArrayReader(affectedBytes).ReadInt32())
			.HasFlag(ProtocolCapabilities.FoundRows));
	}

	[Fact]
	public void HandshakeResponse41_local_infile_bit_follows_connection_option()
	{
		var off = HandshakeResponse41Payload.Create(
			Handshake(), Settings("Server=h;User Id=u;"), Array.Empty<byte>(), CompressionMethod.None, null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();
		Assert.False(((ProtocolCapabilities)(uint)new ByteArrayReader(off).ReadInt32())
			.HasFlag(ProtocolCapabilities.LocalFiles));

		var on = HandshakeResponse41Payload.Create(
			Handshake(), Settings("Server=h;User Id=u;Allow Load Local Infile=true;"), Array.Empty<byte>(),
			CompressionMethod.None, null, CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();
		Assert.True(((ProtocolCapabilities)(uint)new ByteArrayReader(on).ReadInt32())
			.HasFlag(ProtocolCapabilities.LocalFiles));
	}

	[Fact]
	public void HandshakeResponse41_zlib_compression_sets_compress_bit()
	{
		var bytes = HandshakeResponse41Payload.Create(
			Handshake(), Settings("Server=h;User Id=u;"), Array.Empty<byte>(),
			CompressionMethod.Zlib, null, CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();
		Assert.True(((ProtocolCapabilities)(uint)new ByteArrayReader(bytes).ReadInt32())
			.HasFlag(ProtocolCapabilities.Compress));
	}

	[Fact]
	public void HandshakeResponse41_zstandard_appends_compression_level()
	{
		// Zstandard 时末尾追加 1 字节压缩级别，缺省为 10
		var bytes = HandshakeResponse41Payload.Create(
			Handshake(), Settings("Server=h;User Id=u;"), Array.Empty<byte>(),
			CompressionMethod.Zstandard, null, CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();
		Assert.Equal(10, bytes[bytes.Length - 1]);

		var explicitLevel = HandshakeResponse41Payload.Create(
			Handshake(), Settings("Server=h;User Id=u;"), Array.Empty<byte>(),
			CompressionMethod.Zstandard, 3, CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();
		Assert.Equal(3, explicitLevel[explicitLevel.Length - 1]);
	}

	[Fact]
	public void HandshakeResponse41_interactive_session_mirrors_server_capability()
	{
		// 仅当客户端要交互式会话**且**服务端支持时才声明 Interactive
		var caps = ProtocolCapabilities.Protocol41 | ProtocolCapabilities.PluginAuth | ProtocolCapabilities.Interactive;

		var plain = HandshakeResponse41Payload.Create(
			Handshake(caps), Settings("Server=h;User Id=u;"), Array.Empty<byte>(), CompressionMethod.None, null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();
		Assert.False(((ProtocolCapabilities)(uint)new ByteArrayReader(plain).ReadInt32())
			.HasFlag(ProtocolCapabilities.Interactive));

		var interactive = HandshakeResponse41Payload.Create(
			Handshake(caps), Settings("Server=h;User Id=u;Interactive Session=true;"), Array.Empty<byte>(),
			CompressionMethod.None, null, CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();
		Assert.True(((ProtocolCapabilities)(uint)new ByteArrayReader(interactive).ReadInt32())
			.HasFlag(ProtocolCapabilities.Interactive));
	}

	[Fact]
	public void HandshakeResponse41_writes_native_password_plugin_name()
	{
		var bytes = HandshakeResponse41Payload.Create(
			Handshake(), Settings("Server=h;User Id=u;"), Array.Empty<byte>(), CompressionMethod.None, null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();
		Assert.Contains("mysql_native_password", Encoding.UTF8.GetString(bytes));
	}

	[Fact]
	public void HandshakeResponse41_writes_caching_sha2_plugin_name_when_server_asks()
	{
		var bytes = HandshakeResponse41Payload.Create(
			Handshake(authPluginName: "caching_sha2_password"), Settings("Server=h;User Id=u;"),
			Array.Empty<byte>(), CompressionMethod.None, null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();
		Assert.Contains("caching_sha2_password", Encoding.UTF8.GetString(bytes));
	}

	[Fact]
	public void HandshakeResponse41_omits_plugin_name_without_plugin_auth()
	{
		// 服务端未声明 PluginAuth 时不应写入插件名
		var bytes = HandshakeResponse41Payload.Create(
			Handshake(ProtocolCapabilities.Protocol41), Settings("Server=h;User Id=u;"), Array.Empty<byte>(),
			CompressionMethod.None, null, CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();
		Assert.DoesNotContain("mysql_native_password", Encoding.UTF8.GetString(bytes));
	}

	[Fact]
	public void HandshakeResponse41_appends_connection_attributes_verbatim()
	{
		var attributes = new byte[] { 0x01, 0x02, 0x03 };
		var without = HandshakeResponse41Payload.Create(
			Handshake(), Settings("Server=h;User Id=u;"), Array.Empty<byte>(), CompressionMethod.None, null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();
		var with = HandshakeResponse41Payload.Create(
			Handshake(), Settings("Server=h;User Id=u;"), Array.Empty<byte>(), CompressionMethod.None, null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, attributes).Span.ToArray();

		Assert.Equal(without.Length + attributes.Length, with.Length);
		Assert.Equal(attributes, with[without.Length..]);
	}

	[Fact]
	public void HandshakeResponse41_long_password_absent_writes_high_capability_bits()
	{
		// 服务端不支持 LongPassword 时，高 32 位能力位写在保留区之后
		var caps = ProtocolCapabilities.Protocol41 | ProtocolCapabilities.PluginAuth | ProtocolCapabilities.SessionTrack;
		var bytes = HandshakeResponse41Payload.Create(
			Handshake(caps), Settings("Server=h;User Id=u;"), Array.Empty<byte>(), CompressionMethod.None, null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();

		var capabilities = (ProtocolCapabilities)(uint)new ByteArrayReader(bytes).ReadInt32();
		Assert.True(capabilities.HasFlag(ProtocolCapabilities.SessionTrack));
	}

	[Fact]
	public void HandshakeResponse41_CreateWithSsl_sets_ssl_bit()
	{
		var bytes = HandshakeResponse41Payload.CreateWithSsl(
			ProtocolCapabilities.Protocol41, Settings("Server=h;User Id=u;"), CompressionMethod.None,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive).Span.ToArray();

		var capabilities = (ProtocolCapabilities)(uint)new ByteArrayReader(bytes).ReadInt32();
		Assert.True(capabilities.HasFlag(ProtocolCapabilities.Ssl));
		// SSL 请求包只含头部：4(能力位) + 4(最大包长) + 1(字符集) + 19(保留) + 4(高位能力位) = 32
		Assert.Equal(32, bytes.Length);
	}

	// ---------- ChangeUserPayload ----------

	[Fact]
	public void ChangeUserPayload_writes_command_and_fields()
	{
		var payload = ChangeUserPayload.Create(
			"bob", new byte[] { 0x11, 0x22 }, "mydb", CharacterSet.Utf8Mb4GeneralCaseInsensitive, null);
		var bytes = payload.Span.ToArray();

		var reader = new ByteArrayReader(bytes);
		Assert.Equal(17, reader.ReadByte());            // COM_CHANGE_USER
		Assert.Equal("bob", Encoding.UTF8.GetString(reader.ReadNullTerminatedByteString()));
		Assert.Equal(2, reader.ReadByte());             // 认证响应长度
		Assert.Equal(new byte[] { 0x11, 0x22 }, reader.ReadByteString(2).ToArray());
		Assert.Equal("mydb", Encoding.UTF8.GetString(reader.ReadNullTerminatedByteString()));
		Assert.Equal((byte)CharacterSet.Utf8Mb4GeneralCaseInsensitive, reader.ReadByte());
		Assert.Equal(0, reader.ReadByte());             // 字符集扩展占位
		Assert.Equal("mysql_native_password", Encoding.UTF8.GetString(reader.ReadNullTerminatedByteString()));
		Assert.Equal(0, reader.BytesRemaining);
	}

	[Fact]
	public void ChangeUserPayload_writes_empty_schema_as_empty_string()
	{
		var bytes = ChangeUserPayload.Create(
			"bob", Array.Empty<byte>(), null, CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();

		var reader = new ByteArrayReader(bytes);
		reader.ReadByte();
		reader.ReadNullTerminatedByteString();
		reader.ReadByte();
		// schema 为 null → 写空串（只有一个 NUL）
		Assert.Equal("", Encoding.UTF8.GetString(reader.ReadNullTerminatedByteString()));
	}

	[Fact]
	public void ChangeUserPayload_appends_connection_attributes()
	{
		var attributes = new byte[] { 0xAA, 0xBB };
		var bytes = ChangeUserPayload.Create(
			"bob", Array.Empty<byte>(), "db", CharacterSet.Utf8Mb4GeneralCaseInsensitive, attributes).Span.ToArray();

		Assert.Equal(attributes, bytes[^2..]);
	}

	[Fact]
	public void ChangeUserPayload_oracle_mode_uppercases_user_name_part()
	{
		// Oracle 模式：'@' 之前的部分转大写，之后（租户#集群）原样保留
		var bytes = ChangeUserPayload.Create(
			"scott@tenant#cluster", Array.Empty<byte>(), null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null, isOracle: true).Span.ToArray();

		var reader = new ByteArrayReader(bytes);
		reader.ReadByte();
		Assert.Equal("SCOTT@tenant#cluster", Encoding.UTF8.GetString(reader.ReadNullTerminatedByteString()));
	}

	[Fact]
	public void ChangeUserPayload_oracle_mode_uppercases_user_without_at()
	{
		var bytes = ChangeUserPayload.Create(
			"scott", Array.Empty<byte>(), null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null, isOracle: true).Span.ToArray();

		var reader = new ByteArrayReader(bytes);
		reader.ReadByte();
		Assert.Equal("SCOTT", Encoding.UTF8.GetString(reader.ReadNullTerminatedByteString()));
	}

	[Fact]
	public void ChangeUserPayload_oracle_mode_keeps_already_uppercase_name()
	{
		// 全大写时不重新分配（行为等价，这里校验结果仍正确）
		var bytes = ChangeUserPayload.Create(
			"SCOTT@T#C", Array.Empty<byte>(), null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null, isOracle: true).Span.ToArray();

		var reader = new ByteArrayReader(bytes);
		reader.ReadByte();
		Assert.Equal("SCOTT@T#C", Encoding.UTF8.GetString(reader.ReadNullTerminatedByteString()));
	}

	[Fact]
	public void ChangeUserPayload_oracle_mode_empty_user_stays_empty()
	{
		var bytes = ChangeUserPayload.Create(
			"", Array.Empty<byte>(), null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null, isOracle: true).Span.ToArray();

		var reader = new ByteArrayReader(bytes);
		reader.ReadByte();
		Assert.Equal("", Encoding.UTF8.GetString(reader.ReadNullTerminatedByteString()));
	}

	[Fact]
	public void ChangeUserPayload_oracle_mode_derives_schema_from_user_prefix()
	{
		// 未显式给 schema 时，取 user 中 '@' 之前的部分并转大写
		var bytes = ChangeUserPayload.Create(
			"scott@tenant#cluster", Array.Empty<byte>(), null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null, isOracle: true).Span.ToArray();

		var reader = new ByteArrayReader(bytes);
		reader.ReadByte();
		reader.ReadNullTerminatedByteString();          // user
		reader.ReadByte();                              // auth response 长度
		Assert.Equal("SCOTT", Encoding.UTF8.GetString(reader.ReadNullTerminatedByteString()));
	}

	[Fact]
	public void ChangeUserPayload_oracle_mode_explicit_schema_wins()
	{
		var bytes = ChangeUserPayload.Create(
			"scott@tenant#cluster", Array.Empty<byte>(), "myschema",
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null, isOracle: true).Span.ToArray();

		var reader = new ByteArrayReader(bytes);
		reader.ReadByte();
		reader.ReadNullTerminatedByteString();
		reader.ReadByte();
		Assert.Equal("MYSCHEMA", Encoding.UTF8.GetString(reader.ReadNullTerminatedByteString()));
	}

	[Fact]
	public void ChangeUserPayload_oracle_mode_blank_schema_falls_back_to_user_prefix()
	{
		// 全空白 schema 视为未提供
		var bytes = ChangeUserPayload.Create(
			"scott@t", Array.Empty<byte>(), "   ",
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null, isOracle: true).Span.ToArray();

		var reader = new ByteArrayReader(bytes);
		reader.ReadByte();
		reader.ReadNullTerminatedByteString();
		reader.ReadByte();
		Assert.Equal("SCOTT", Encoding.UTF8.GetString(reader.ReadNullTerminatedByteString()));
	}

	[Fact]
	public void ChangeUserPayload_oracle_mode_user_without_at_yields_same_schema()
	{
		var bytes = ChangeUserPayload.Create(
			"scott", Array.Empty<byte>(), null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null, isOracle: true).Span.ToArray();

		var reader = new ByteArrayReader(bytes);
		reader.ReadByte();
		reader.ReadNullTerminatedByteString();
		reader.ReadByte();
		Assert.Equal("SCOTT", Encoding.UTF8.GetString(reader.ReadNullTerminatedByteString()));
	}

	[Fact]
	public void ChangeUserPayload_oracle_mode_leading_at_keeps_whole_name()
	{
		// '@' 位于下标 0 时（IndexOf <= 0）不做截断，整串转大写
		var bytes = ChangeUserPayload.Create(
			"@tenant", Array.Empty<byte>(), null,
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null, isOracle: true).Span.ToArray();

		var reader = new ByteArrayReader(bytes);
		reader.ReadByte();
		Assert.Equal("@TENANT", Encoding.UTF8.GetString(reader.ReadNullTerminatedByteString()));
	}

	[Fact]
	public void ChangeUserPayload_mysql_mode_leaves_names_unchanged()
	{
		// MySQL 模式不做任何大小写改写
		var bytes = ChangeUserPayload.Create(
			"scott@tenant#cluster", Array.Empty<byte>(), "mydb",
			CharacterSet.Utf8Mb4GeneralCaseInsensitive, null).Span.ToArray();

		var reader = new ByteArrayReader(bytes);
		reader.ReadByte();
		Assert.Equal("scott@tenant#cluster", Encoding.UTF8.GetString(reader.ReadNullTerminatedByteString()));
		reader.ReadByte();
		Assert.Equal("mydb", Encoding.UTF8.GetString(reader.ReadNullTerminatedByteString()));
	}

	[Fact]
	public void ChangeUserPayload_writes_non_utf8_character_set()
	{
		var bytes = ChangeUserPayload.Create(
			"u", Array.Empty<byte>(), "db", CharacterSet.GbkChineseCaseInsensitive, null).Span.ToArray();

		var reader = new ByteArrayReader(bytes);
		reader.ReadByte();
		reader.ReadNullTerminatedByteString();
		reader.ReadByte();
		reader.ReadNullTerminatedByteString();
		Assert.Equal((byte)CharacterSet.GbkChineseCaseInsensitive, reader.ReadByte());
	}
}
