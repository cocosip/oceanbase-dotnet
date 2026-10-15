using System;
using System.Text;
using OceanBase.Core;
using OceanBase.Protocol;
using OceanBase.Protocol.Payloads;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 协议载荷（Payload）解析测试（internal 类型，无需服务端）。
///
/// 这些类把服务端返回的原始字节解码成结构化数据，是结果集 / 错误 / 会话状态
/// 处理的基础；用手工构造的字节流验证各字段与边界行为。
/// </summary>
public sealed class PayloadTests
{
	// ---------- OkPayload ----------

	/// <summary>可控的服务端能力位（OkPayload 解析依赖它选择分支）。</summary>
	private sealed class FakeServerCapabilities : IServerCapabilities
	{
		public bool SupportsDeprecateEof { get; set; }
		public bool SupportsSessionTrack { get; set; }
		public bool SupportsProtocol41 { get; set; } = true;
		public bool SupportsTransactions { get; set; } = true;
		public bool UseOracleMode { get; set; }
		public bool UseOceanBaseProtocolV2 { get; set; }
		public bool EnableFullLinkTrace { get; set; }
		public bool UseNewExtraInfo { get; set; }
		public bool UseNewResultSetMetadata { get; set; }
	}

	private static ByteBufferWriter OkHeader(ulong affectedRows, ulong lastInsertId, ServerStatus status, int warnings = 0, bool protocol41 = true)
	{
		var w = new ByteBufferWriter(32);
		w.Write(OkPayload.Signature);
		w.WriteLengthEncodedInteger(affectedRows);
		w.WriteLengthEncodedInteger(lastInsertId);
		w.Write((ushort)status);
		if (protocol41)
			w.Write((ushort)warnings);
		return w;
	}

	[Fact]
	public void OkPayload_parses_basic_protocol41_packet()
	{
		var bytes = OkHeader(affectedRows: 3, lastInsertId: 42, ServerStatus.AutoCommit, warnings: 7).ToPayloadData().Span.ToArray();
		Assert.True(OkPayload.IsOk(bytes, new FakeServerCapabilities()));

		var ok = OkPayload.Create(bytes, new FakeServerCapabilities());
		Assert.Equal(3ul, ok.AffectedRowCount);
		Assert.Equal(42ul, ok.LastInsertId);
		Assert.Equal(ServerStatus.AutoCommit, ok.ServerStatus);
		Assert.Equal(7, ok.WarningCount);
		Assert.Null(ok.StatusInfo);
		Assert.Null(ok.NewSchema);
	}

	[Fact]
	public void OkPayload_parses_length_encoded_affected_rows()
	{
		// 18446744073709551615 = ulong.MaxValue，编码为 0xFE + 8 字节
		var bytes = OkHeader(ulong.MaxValue, 0, ServerStatus.AutoCommit).ToPayloadData().Span.ToArray();
		var ok = OkPayload.Create(bytes, new FakeServerCapabilities());
		Assert.Equal(ulong.MaxValue, ok.AffectedRowCount);
	}

	[Fact]
	public void OkPayload_without_protocol41_has_no_warning_count()
	{
		var bytes = OkHeader(1, 2, ServerStatus.InTransaction, protocol41: false).ToPayloadData().Span.ToArray();
		var ok = OkPayload.Create(bytes, new FakeServerCapabilities { SupportsProtocol41 = false });
		Assert.Equal(1ul, ok.AffectedRowCount);
		Assert.Equal(ServerStatus.InTransaction, ok.ServerStatus);
	}

	[Fact]
	public void OkPayload_parses_status_info_without_session_track()
	{
		// 未协商 SessionTrack 时，剩余字节是人读状态信息；
		// 首字节等于"剩余长度-1"时会被剥掉（MySQL 的长度前缀惯例）。
		var w = OkHeader(0, 0, ServerStatus.AutoCommit);
		w.Write((byte)4); // "info" 长度前缀：后面 4 字节
		w.Write("info"u8);
		var ok = OkPayload.Create(w.ToPayloadData().Span, new FakeServerCapabilities { SupportsSessionTrack = false });
		Assert.NotNull(ok.StatusInfo);
		Assert.Equal("info", Encoding.UTF8.GetString(ok.StatusInfo));
	}

	[Fact]
	public void OkPayload_parses_session_track_schema()
	{
		var caps = new FakeServerCapabilities { SupportsSessionTrack = true };
		var w = OkHeader(0, 0, ServerStatus.AutoCommit | ServerStatus.SessionStateChanged);
		w.Write((byte)0); // 空的人类可读状态信息（lenenc 空串）

		// 会话状态块：长度前缀 + (kind, 值长度, 值)；Schema 的值本身是 lenenc 字符串
		var value = new ByteBufferWriter(16);
		value.WriteLengthEncodedString("newdb");
		var block = new ByteBufferWriter(16);
		block.Write((byte)SessionTrackKind.Schema);
		block.WriteLengthEncodedInteger((ulong)value.Position);
		block.Write(value.ArraySegment);
		w.WriteLengthEncodedInteger((ulong)block.Position);
		w.Write(block.ArraySegment);

		var ok = OkPayload.Create(w.ToPayloadData().Span, caps);
		Assert.Equal("newdb", ok.NewSchema);
	}

	[Fact]
	public void OkPayload_parses_session_track_system_variables()
	{
		var caps = new FakeServerCapabilities { SupportsSessionTrack = true };
		var w = OkHeader(0, 0, ServerStatus.AutoCommit | ServerStatus.SessionStateChanged);
		w.Write((byte)0);

		var vars = new ByteBufferWriter(64);
		void WriteVar(string name, string value)
		{
			vars.WriteLengthEncodedString(name);
			vars.WriteLengthEncodedInteger((ulong)Encoding.UTF8.GetByteCount(value));
			vars.Write(Encoding.UTF8.GetBytes(value));
		}
		WriteVar("character_set_client", "utf8mb4");
		WriteVar("character_set_connection", "utf8mb4");
		WriteVar("character_set_results", "utf8mb4");
		WriteVar("connection_id", "12345");
		WriteVar("redirect_url", "obproxy:2881");

		var block = new ByteBufferWriter(128);
		block.Write((byte)SessionTrackKind.SystemVariables);
		block.WriteLengthEncodedInteger((ulong)vars.Position);
		block.Write(vars.ArraySegment);
		w.WriteLengthEncodedInteger((ulong)block.Position);
		w.Write(block.ArraySegment);

		var ok = OkPayload.Create(w.ToPayloadData().Span, caps);
		Assert.Equal(CharacterSet.Utf8Mb4Binary, ok.NewCharacterSet);
		Assert.Equal(12345, ok.NewConnectionId);
		Assert.Equal("obproxy:2881", ok.RedirectionUrl);
	}

	[Fact]
	public void OkPayload_new_character_set_requires_all_three_variables()
	{
		// 三个 character_set_* 变量都为 utf8 → Utf8Mb3Binary；
		// 只要不全是同一种（如 latin1）→ None
		Assert.Equal(CharacterSet.Utf8Mb3Binary, CreateOkWithCharsets("utf8"));
		Assert.Equal(CharacterSet.None, CreateOkWithCharsets("latin1"));

		static CharacterSet? CreateOkWithCharsets(string charsetValue)
		{
			var caps = new FakeServerCapabilities { SupportsSessionTrack = true };
			var w = OkHeader(0, 0, ServerStatus.AutoCommit | ServerStatus.SessionStateChanged);
			w.Write((byte)0);
			var vars = new ByteBufferWriter(64);
			foreach (var name in new[] { "character_set_client", "character_set_connection", "character_set_results" })
			{
				vars.WriteLengthEncodedString(name);
				vars.WriteLengthEncodedInteger((ulong)Encoding.UTF8.GetByteCount(charsetValue));
				vars.Write(Encoding.UTF8.GetBytes(charsetValue));
			}
			var block = new ByteBufferWriter(64);
			block.Write((byte)SessionTrackKind.SystemVariables);
			block.WriteLengthEncodedInteger((ulong)vars.Position);
			block.Write(vars.ArraySegment);
			w.WriteLengthEncodedInteger((ulong)block.Position);
			w.Write(block.ArraySegment);
			return OkPayload.Create(w.ToPayloadData().Span, caps).NewCharacterSet;
		}
	}

	[Fact]
	public void OkPayload_ob_capability_flag_toggles_oceanbase_capabilities()
	{
		var caps = new FakeServerCapabilities { SupportsSessionTrack = true };
		var w = OkHeader(0, 0, ServerStatus.AutoCommit | ServerStatus.SessionStateChanged);
		w.Write((byte)0);

		var vars = new ByteBufferWriter(64);
		var value = (0x100 | 0x4000 | 0x8000).ToString(); // V2 协议 + 全链路追踪 + 新扩展信息
		vars.WriteLengthEncodedString("ob_capability_flag");
		vars.WriteLengthEncodedInteger((ulong)value.Length);
		vars.Write(Encoding.ASCII.GetBytes(value));
		var block = new ByteBufferWriter(64);
		block.Write((byte)SessionTrackKind.SystemVariables);
		block.WriteLengthEncodedInteger((ulong)vars.Position);
		block.Write(vars.ArraySegment);
		w.WriteLengthEncodedInteger((ulong)block.Position);
		w.Write(block.ArraySegment);

		OkPayload.Create(w.ToPayloadData().Span, caps);
		Assert.True(caps.UseOceanBaseProtocolV2);
		Assert.True(caps.EnableFullLinkTrace);
		Assert.True(caps.UseNewExtraInfo);
	}

	[Fact]
	public void OkPayload_oracle_mode_status_flag_sets_useOracleMode()
	{
		var caps = new FakeServerCapabilities();
		var bytes = OkHeader(0, 0, ServerStatus.AutoCommit | ServerStatus.ObserverUseOracleMode).ToPayloadData().Span.ToArray();
		OkPayload.Create(bytes, caps);
		Assert.True(caps.UseOracleMode);
	}

	[Fact]
	public void OkPayload_1xFE_signature_only_accepted_when_deperecate_eof_supported()
	{
		var bytes = new byte[] { 0xFE, 0, 0, 0, 0, 0, 0 };
		Assert.False(OkPayload.IsOk(bytes, new FakeServerCapabilities()));
		Assert.True(OkPayload.IsOk(bytes, new FakeServerCapabilities { SupportsDeprecateEof = true }));

		// 解析：0xFE 头在 SupportsDeprecateEof 下等价于 OK 包
		var ok = OkPayload.Create(bytes, new FakeServerCapabilities { SupportsDeprecateEof = true });
		Assert.Equal(0ul, ok.AffectedRowCount);
	}

	[Fact]
	public void OkPayload_Verify_throws_for_bad_signature()
	{
		var bytes = new byte[] { 0xAB, 0, 0, 0, 0, 0, 0 };
		Assert.False(OkPayload.IsOk(bytes, new FakeServerCapabilities()));
		Assert.Throws<FormatException>(() => OkPayload.Verify(bytes, new FakeServerCapabilities()));
	}

	// ---------- ErrorPayload ----------

	[Fact]
	public void ErrorPayload_parses_sqlstate_format()
	{
		// 0xFF + 错误码(2) + '#' + SQLSTATE(5) + 消息
		var w = new ByteBufferWriter(64);
		w.Write(ErrorPayload.Signature);
		w.Write((ushort)1064);
		w.Write("#"u8);
		w.Write(Encoding.ASCII.GetBytes("42000"));
		w.Write(Encoding.UTF8.GetBytes("You have an error in your SQL syntax"));
		var error = ErrorPayload.Create(w.ToPayloadData().Span);

		Assert.Equal(1064, error.ErrorCode);
		Assert.Equal("42000", error.State);
		Assert.Equal("You have an error in your SQL syntax", error.Message);
	}

	[Fact]
	public void ErrorPayload_without_sqlstate_marker_uses_default_state()
	{
		// 老协议：0xFF + 错误码(2) + 消息（无 '#' 标记）→ State 固定 HY000
		var w = new ByteBufferWriter(64);
		w.Write(ErrorPayload.Signature);
		w.Write((ushort)1045);
		w.Write("A"u8);
		w.Write(Encoding.UTF8.GetBytes("ccess denied for user"));
		var error = ErrorPayload.Create(w.ToPayloadData().Span);

		Assert.Equal(1045, error.ErrorCode);
		Assert.Equal("HY000", error.State);
		Assert.Equal("Access denied for user", error.Message);
	}

	[Fact]
	public void ErrorPayload_ToException_carries_code_state_and_message()
	{
		var w = new ByteBufferWriter(64);
		w.Write(ErrorPayload.Signature);
		w.Write((ushort)1045);
		w.Write("#"u8);
		w.Write(Encoding.ASCII.GetBytes("28000"));
		w.Write(Encoding.UTF8.GetBytes("denied"));
		var exception = ErrorPayload.Create(w.ToPayloadData().Span).ToException();

		Assert.Equal((MySqlErrorCode)1045, exception.ErrorCode);
		Assert.Equal("28000", exception.SqlState);
		Assert.Equal("denied", exception.Message);
	}

	// ---------- EofPayload ----------

	[Fact]
	public void EofPayload_Create_parses_warning_count_and_status()
	{
		var eof = EofPayload.Create(new byte[] { 0xFE, 0x02, 0x00, 0x02, 0x00 });
		Assert.Equal(2, eof.WarningCount);
		Assert.Equal(ServerStatus.AutoCommit, eof.ServerStatus);
	}

	[Fact]
	public void EofPayload_Create_rejects_too_long_packet()
	{
		Assert.Throws<FormatException>(() => EofPayload.Create(new byte[] { 0xFE, 0, 0, 0, 0, 0 }));
	}

	[Fact]
	public void EofPayload_Create_throws_when_truncated()
	{
		// 少于 5 字节：读完头部后越界读取
		Assert.ThrowsAny<InvalidOperationException>(() => EofPayload.Create(new byte[] { 0xFE, 0, 0, 0 }));
	}

	[Theory]
	[InlineData(new byte[] { 0xFE, 0, 0, 0, 0 }, true)]   // <9 字节且头是 0xFE
	[InlineData(new byte[] { 0x00, 0, 0, 0, 0 }, false)]  // 头不是 0xFE
	[InlineData(new byte[] { 0xFE, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, false)] // ≥9 字节可能是 lenenc 整数
	public void EofPayload_IsEof_identifies_eof_packets(byte[] data, bool expected)
	{
		Assert.Equal(expected, EofPayload.IsEof(new PayloadData(data)));
	}

	// ---------- ColumnCountPayload ----------

	[Fact]
	public void ColumnCountPayload_without_optional_metadata()
	{
		var w = new ByteBufferWriter(4);
		w.WriteLengthEncodedInteger(3);
		var payload = ColumnCountPayload.Create(w.ToPayloadData().Span, supportsOptionalMetadata: false);
		Assert.Equal(3, payload.ColumnCount);
		Assert.True(payload.MetadataFollows);
	}

	[Theory]
	[InlineData(new byte[] { 3, 1 }, true)]   // 显式跟随元数据
	[InlineData(new byte[] { 3, 0 }, false)]  // 显式无元数据（CURSOR 型结果集）
	[InlineData(new byte[] { 3 }, true)]      // 没有标志字节 → 默认跟随
	public void ColumnCountPayload_with_optional_metadata(byte[] data, bool metadataFollows)
	{
		var payload = ColumnCountPayload.Create(data, supportsOptionalMetadata: true);
		Assert.Equal(3, payload.ColumnCount);
		Assert.Equal(metadataFollows, payload.MetadataFollows);
	}

	// ---------- InitialHandshakePayload ----------

	[Fact]
	public void InitialHandshakePayload_parses_v10_handshake()
	{
		var w = new ByteBufferWriter(128);
		w.Write((byte)10);                            // 协议版本
		w.WriteNullTerminatedString("5.7.36-oceanbase");
		w.Write(0x11223344);                          // 连接 ID
		w.Write(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }); // auth data 第一段（8 字节）
		w.Write((byte)0);                             // 填充
		var lower = (ushort)(ProtocolCapabilities.LongPassword | ProtocolCapabilities.Protocol41 | ProtocolCapabilities.SecureConnection);
		w.Write(lower);
		w.Write((byte)45);                            // 字符集 utf8mb4
		w.Write((ushort)ServerStatus.AutoCommit);     // 状态位
		w.Write((ushort)((ulong)ProtocolCapabilities.PluginAuth >> 16)); // 能力位高 16 位
		w.Write((byte)21);                            // auth data 总长
		w.Write(new byte[10]);                        // 保留字段的低 6 字节 + 高 4 字节
		w.Write(new byte[13]);                        // auth data 第二段（12+NUL）
		w.WriteNullTerminatedString("mysql_native_password");

		var handshake = InitialHandshakePayload.Create(w.ToPayloadData().Span);

		Assert.Equal(0x11223344, handshake.ConnectionId);
		Assert.Equal("mysql_native_password", handshake.AuthPluginName);
		Assert.Equal(21, handshake.AuthPluginData.Length);
		Assert.Equal(8 + 13, handshake.AuthPluginData.Length);
		Assert.Equal(ProtocolCapabilities.LongPassword | ProtocolCapabilities.Protocol41 | ProtocolCapabilities.SecureConnection | ProtocolCapabilities.PluginAuth, handshake.ProtocolCapabilities);
	}

	[Fact]
	public void InitialHandshakePayload_without_plugin_auth_name()
	{
		var w = new ByteBufferWriter(128);
		w.Write((byte)10);
		w.WriteNullTerminatedString("5.6.0");
		w.Write(1);
		w.Write(new byte[8]);
		w.Write((byte)0);
		w.Write((ushort)(ProtocolCapabilities.LongPassword | ProtocolCapabilities.SecureConnection));
		// BytesRemaining == 0：旧式短握手包，只解析到这里
		var handshake = InitialHandshakePayload.Create(w.ToPayloadData().Span);

		Assert.Null(handshake.AuthPluginName);
		// 未经 SecureConnection 扩展时，auth data 只有第一段的 8 字节
		Assert.Equal(8, handshake.AuthPluginData.Length);
	}

	// ---------- ColumnDefinitionPayload ----------

	private static byte[] CreateColumnDefinitionBytes(
		string name = "col",
		CharacterSet characterSet = CharacterSet.Utf8Mb4GeneralCaseInsensitive,
		uint columnLength = 100,
		byte protocolType = 3,
		ColumnFlags flags = (ColumnFlags)0,
		byte decimals = 0,
		byte precision = 0,
		byte inoutType = 0,
		string? complexTypeName = null)
	{
		var w = new ByteBufferWriter(128);
		w.WriteLengthEncodedString("def");        // catalog
		w.WriteLengthEncodedString("db");         // schema
		w.WriteLengthEncodedString("t");          // table
		w.WriteLengthEncodedString("t");          // physical table
		w.WriteLengthEncodedString(name);
		w.WriteLengthEncodedString(name);         // physical name
		w.Write((byte)12);                        // filler
		w.Write((ushort)characterSet);
		w.Write(columnLength);
		w.Write(protocolType);
		w.Write((ushort)flags);
		w.Write(decimals);
		w.Write(precision);
		w.Write(inoutType);
		if (complexTypeName is not null)
		{
			w.WriteLengthEncodedString("");          // 复杂列元数据前的占位字符串
			w.WriteLengthEncodedString(complexTypeName);
		}
		return w.ToPayloadData().Span.ToArray();
	}

	private static ColumnDefinitionPayload CreateColumnDefinition(byte[] bytes, bool isOracleMode = false)
	{
		var resizable = new ResizableArray<byte>();
		Utility.Resize(ref resizable, bytes.Length);
		bytes.CopyTo(resizable.Array!.AsSpan(0));
		ColumnDefinitionPayload payload = null!;
		ColumnDefinitionPayload.Initialize(ref payload, new ResizableArraySegment<byte>(resizable, 0, bytes.Length), isOracleMode);
		return payload;
	}

	[Fact]
	public void ColumnDefinitionPayload_reads_names_lazily()
	{
		var payload = CreateColumnDefinition(CreateColumnDefinitionBytes(name: "amount"));
		Assert.Equal("amount", payload.Name);
		Assert.Equal("db", payload.SchemaName);
		Assert.Equal("def", payload.CatalogName);
		Assert.Equal("t", payload.Table);
		Assert.Equal("t", payload.PhysicalTable);
		Assert.Equal("amount", payload.PhysicalName);
	}

	[Fact]
	public void ColumnDefinitionPayload_reads_fixed_fields()
	{
		var payload = CreateColumnDefinition(CreateColumnDefinitionBytes(
			characterSet: CharacterSet.Utf8Mb4Binary, columnLength: 12345, protocolType: 3,
			flags: ColumnFlags.Unsigned | ColumnFlags.NotNull, decimals: 31, precision: 10, inoutType: 2));
		Assert.Equal(CharacterSet.Utf8Mb4Binary, payload.CharacterSet);
		Assert.Equal(12345u, payload.ColumnLength);
		Assert.Equal(ColumnType.Long, payload.ColumnType);
		Assert.Equal(ColumnFlags.Unsigned | ColumnFlags.NotNull, payload.ColumnFlags);
		Assert.Equal(31, payload.Decimals);
		Assert.Equal(10, payload.Precision);
		Assert.Equal(2, payload.InoutType);
	}

	[Fact]
	public void ColumnDefinitionPayload_decimal_in_oracle_mode_adjusts_precision()
	{
		// Oracle 模式下带符号 NUMBER：PrecisionAdjustFactor = -1
		var payload = CreateColumnDefinition(CreateColumnDefinitionBytes(protocolType: 246, decimals: 171, precision: 255), isOracleMode: true);
		Assert.Equal(ColumnType.NewDecimal, payload.ColumnType);
		Assert.Equal(-1, payload.PrecisionAdjustFactor);
		Assert.Equal(0, payload.Precision); // 0xFF 归零
		Assert.Equal(0, payload.Decimals);  // 171 归零

		// Oracle 模式协议类型 4 = BINARY_FLOAT：+1
		var floatPayload = CreateColumnDefinition(CreateColumnDefinitionBytes(protocolType: 4), isOracleMode: true);
		Assert.Equal(ColumnType.OracleBinaryFloat, floatPayload.ColumnType);
		Assert.Equal(1, floatPayload.PrecisionAdjustFactor);

		// MySQL 模式：普通类型为 0
		var mysqlPayload = CreateColumnDefinition(CreateColumnDefinitionBytes(protocolType: 246));
		Assert.Equal(0, mysqlPayload.PrecisionAdjustFactor);
	}

	[Fact]
	public void ColumnDefinitionPayload_reads_complex_type_name()
	{
		var payload = CreateColumnDefinition(CreateColumnDefinitionBytes(protocolType: 160, complexTypeName: "UDT2_BASIC_O"), isOracleMode: true);
		Assert.Equal(ColumnType.OracleComplex, payload.ColumnType);
		Assert.Equal("UDT2_BASIC_O", payload.ComplexTypeName);

		// 非 COMPLEX(160-162) 列返回 null
		var simple = CreateColumnDefinition(CreateColumnDefinitionBytes(protocolType: 3), isOracleMode: true);
		Assert.Null(simple.ComplexTypeName);
	}
}
