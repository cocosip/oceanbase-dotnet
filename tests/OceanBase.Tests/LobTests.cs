using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using Xunit;

#pragma warning disable SYSLIB0050 // 测试直接调用 ISerializable.GetObjectData，需要构造 SerializationInfo

namespace OceanBase.Tests;

/// <summary>
/// LOB 离线单元测试：Lob 基类与内存实现（OracleBlob/OracleClob）的读写、寻址、裁剪、流适配，
/// 以及 ObLobLocator V1/V2 定位符二进制解析（ObMemLobCommon/ObLobCommon 标志位判别）。
/// 全部使用内存构造路径（字节数组 / internal 构造函数），不依赖数据库连接。
/// </summary>
public sealed class LobTests
{
	private const uint LobMagicCode = 0x4C4F4221u; // 字节 "!BOL"（33,66,79,76）按 LE 读出的值（1280262689）

	private sealed class StubLocator : ObLobLocator
	{
		public StubLocator()
		{
		}

		public StubLocator(ReadOnlyMemory<byte> binaryData, long payloadSize)
			: base(binaryData, payloadSize, null)
		{
		}
	}

	// ---------- 二进制构造辅助 ----------

	private static void WriteLE(byte[] buffer, int offset, ulong value, int byteCount)
	{
		for (int i = 0; i < byteCount; i++)
		{
			buffer[offset + i] = (byte)(value >> (8 * i));
		}
	}

	/// <summary>构造 V1 定位符：40 字节头 + rowId（payloadOffset）+ payload。</summary>
	private static byte[] MakeV1LocatorBytes(byte[] rowId, byte[] payload, ulong snapshotVersion = 0x1122334455667788UL)
	{
		var header = new byte[40];
		WriteLE(header, 0, LobMagicCode, 4);
		WriteLE(header, 4, 1, 4);                      // version = 1
		WriteLE(header, 8, snapshotVersion, 8);        // snapshot version
		header[16] = 0xA1;                             // table id（8 字节）
		header[17] = 0xA2;
		header[18] = 0xA3;
		header[19] = 0xA4;
		header[20] = 0xA5;
		header[21] = 0xA6;
		header[22] = 0xA7;
		header[23] = 0xA8;
		WriteLE(header, 24, 7, 4);                     // column id
		WriteLE(header, 28, 3, 2);                     // flags
		WriteLE(header, 30, 5, 2);                     // option
		WriteLE(header, 32, (ulong)rowId.Length, 4);   // payload offset == rowid 长度
		WriteLE(header, 36, (ulong)payload.Length, 4); // payload size
		var ms = new MemoryStream();
		ms.Write(header, 0, 40);
		ms.Write(rowId, 0, rowId.Length);
		ms.Write(payload, 0, payload.Length);
		return ms.ToArray();
	}

	/// <summary>构造 V2「简单 + in-row」定位符：8 字节头 + payload（version 40962 = 0xA002）。</summary>
	private static byte[] MakeV2SimpleLocatorBytes(byte[] payload)
	{
		var data = new byte[8 + payload.Length];
		WriteLE(data, 0, LobMagicCode, 4);
		WriteLE(data, 4, 40962u, 4);
		payload.AsSpan().CopyTo(data.AsSpan(8));
		return data;
	}

	private static OracleBlob BlobFromLocator(byte[] locatorBytes) => new OracleBlob(true, locatorBytes, null, (OracleConnection?)null);

	private static OracleClob ClobFromLocator(byte[] locatorBytes, string? encoding = "utf-8") => new OracleClob(true, locatorBytes, encoding, (OracleConnection?)null);

	private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

	private static byte[] ReadAll(Stream stream)
	{
		using var ms = new MemoryStream();
		stream.CopyTo(ms);
		return ms.ToArray();
	}

	// ---------- ObMemLobCommon / ObLobCommon 标志判别 ----------

	[Theory]
	[InlineData(40962u, true, true, false)]  // V2 简单 in-row：flags = 0xA
	[InlineData(1u, false, false, false)]    // V1：flags = 0
	[InlineData(65538u, false, false, true)] // 仅 hasExtern：flags = 0x10
	[InlineData(73730u, true, false, true)]  // in-row + extern：flags = 0x12
	[InlineData(8194u, true, false, false)]  // 仅 hasInRowData：flags = 0x2
	[InlineData(4098u, false, false, false)] // readOnly（flags=0x1）不影响三个方法
	[InlineData(16386u, false, false, false)] // isOpen（flags=0x4）不影响三个方法
	public void ObMemLobCommon_parses_flags_from_version(uint version, bool hasInRow, bool isSimple, bool hasExtern)
	{
		var common = new ObMemLobCommon(LobMagicCode, version);
		Assert.Equal(hasInRow, common.HasInrowData());
		Assert.Equal(isSimple, common.IsSimple());
		Assert.Equal(hasExtern, common.HasExtern());
	}

	[Theory]
	[InlineData(0, false, false)]
	[InlineData(1, true, false)]
	[InlineData(2, false, false)] // isEmpty 位：两个方法都不受影响
	[InlineData(4, false, true)]
	[InlineData(5, true, true)]
	[InlineData(7, true, true)]
	public void ObLobCommon_parses_flags(int flags, bool isInit, bool inRow)
	{
		var common = new ObLobCommon(flags);
		Assert.Equal(isInit, common.IsInit());
		Assert.Equal(inRow, common.InRow());
	}

	// ---------- ObLobLocator 基类 ----------

	[Fact]
	public void ObLobLocator_ctor_stores_binary_data_and_payload_size()
	{
		var binary = new byte[] { 9, 8, 7 };
		var locator = new StubLocator(binary, 42L);
		Assert.Equal(binary, locator.BinaryData.ToArray());
		Assert.Equal(42L, locator.PayloadSize);
		Assert.Null(locator.GetConnection()); // 未设置连接时返回 null
	}

	[Fact]
	public void ObLobLocator_set_connection_null_keeps_null()
	{
		var locator = new StubLocator();
		locator.SetConnection(null);
		Assert.Null(locator.GetConnection());
	}

	[Fact]
	public void ObLobLocator_dispose_clears_binary_data_and_is_idempotent()
	{
		var locator = new StubLocator(new byte[] { 1, 2 }, 5L);
		locator.Dispose();
		Assert.Equal(0, locator.BinaryData.Length); // Dispose 后二进制数据被清空
		Assert.Equal(5L, locator.PayloadSize);      // payload 长度保留
		Assert.Null(locator.GetConnection());
		locator.Dispose(); // 二次 Dispose 不抛
	}

	[Fact]
	public void ObLobLocator_clone_unknown_version_throws()
	{
		Assert.Throws<InvalidOperationException>(() => new StubLocator().Clone());
	}

	[Fact]
	public void ObLobLocator_clone_v1_copies_all_fields_independently()
	{
		var blob = BlobFromLocator(MakeV1LocatorBytes(new byte[] { 0x0A, 0x0B }, new byte[] { 1, 2, 3 }));
		var v1 = Assert.IsType<ObLobLocatorV1>(blob.LocatorInternal);
		var clone = Assert.IsType<ObLobLocatorV1>(v1.Clone());
		Assert.NotSame(v1, clone);
		Assert.Equal(v1.MagicCode, clone.MagicCode);
		Assert.Equal(v1.Version, clone.Version);
		Assert.Equal(v1.SnapshotVersion, clone.SnapshotVersion);
		Assert.Equal(v1.TableId.ToArray(), clone.TableId.ToArray());
		Assert.Equal(v1.ColumnId, clone.ColumnId);
		Assert.Equal(v1.Flags, clone.Flags);
		Assert.Equal(v1.Option, clone.Option);
		Assert.Equal(v1.PayloadOffset, clone.PayloadOffset);
		Assert.Equal(v1.RowId.ToArray(), clone.RowId.ToArray());
		Assert.Equal(v1.PayloadSize, clone.PayloadSize);
		Assert.Equal(v1.BinaryData.ToArray(), clone.BinaryData.ToArray());
		// 修改副本不影响原件
		clone.PayloadSize = 99L;
		Assert.Equal(3L, v1.PayloadSize);
	}

	[Fact]
	public void ObLobLocator_clone_v2_shares_common_reference()
	{
		var blob = BlobFromLocator(MakeV2SimpleLocatorBytes(new byte[] { 1, 2 }));
		var v2 = Assert.IsType<ObLobLocatorV2>(blob.LocatorInternal);
		var clone = Assert.IsType<ObLobLocatorV2>(v2.Clone());
		Assert.NotSame(v2, clone);
		Assert.Same(v2.Common, clone.Common); // Common 对象按引用共享
		Assert.Equal(v2.PayloadSize, clone.PayloadSize);
	}

	// ---------- V1 定位符解析（BuildObLobLocatorV1） ----------

	[Fact]
	public void BuildObLobLocatorV1_parses_header_fields()
	{
		var rowId = new byte[] { 0x0A, 0x0B };
		var payload = new byte[] { 1, 2, 3 };
		var buffer = MakeV1LocatorBytes(rowId, payload);
		var blob = BlobFromLocator(buffer);
		var v1 = Assert.IsType<ObLobLocatorV1>(blob.LocatorInternal);
		Assert.Equal((long)LobMagicCode, v1.MagicCode);
		Assert.Equal(1L, v1.Version);
		Assert.Equal((long)0x1122334455667788UL, v1.SnapshotVersion);
		Assert.Equal(new byte[] { 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7, 0xA8 }, v1.TableId.ToArray());
		Assert.Equal(7L, v1.ColumnId);
		Assert.Equal(3, v1.Flags);
		Assert.Equal(5, v1.Option);
		Assert.Equal(2L, v1.PayloadOffset);
		Assert.Equal(3L, v1.PayloadSize);
		Assert.Equal(rowId, v1.RowId.ToArray());
		Assert.Equal(buffer, v1.BinaryData.ToArray()); // 原始二进制整体保留
		Assert.Null(blob.LocatorInternal!.GetConnection());
	}

	[Fact]
	public void BuildObLobLocatorV1_reads_inrow_payload_into_blob()
	{
		var blob = BlobFromLocator(MakeV1LocatorBytes(new byte[] { 0x0A }, new byte[] { 4, 5, 6 }));
		Assert.Equal(new byte[] { 4, 5, 6 }, blob.GetBytes(1, 3));
		Assert.Equal(3L, blob.Length); // V1 定位符：长度取内存数据长度
		Assert.False(blob.IsEmpty);    // PayloadSize != 0
	}

	[Fact]
	public void BuildObLobLocatorV1_declared_size_larger_than_buffer_skips_data()
	{
		var buffer = MakeV1LocatorBytes(new byte[] { 0x0A }, new byte[] { 1, 2 });
		WriteLE(buffer, 36, 100, 4); // 声明的 payload size 超出实际缓冲区剩余字节
		var blob = BlobFromLocator(buffer);
		var v1 = Assert.IsType<ObLobLocatorV1>(blob.LocatorInternal);
		Assert.Equal(100L, v1.PayloadSize);
		Assert.Equal(0L, blob.Length); // 校验失败时不读 in-row 数据
		Assert.Empty(blob.GetBytes(1, 10));
		Assert.False(blob.IsEmpty); // 判空只看定位符 PayloadSize
	}

	[Fact]
	public void BuildObLobLocatorV1_clob_decodes_char_data()
	{
		var clob = ClobFromLocator(MakeV1LocatorBytes(new byte[0], Utf8("AB")));
		Assert.IsType<ObLobLocatorV1>(clob.LocatorInternal);
		Assert.Equal("AB", clob.GetString()); // CLOB 路径把 in-row 字节按编码解码为字符
		Assert.Equal(2L, clob.length());
	}

	[Fact]
	public void GetEmptyBLOB_builds_empty_v1_locator()
	{
		var blob = OracleBlob.GetEmptyBLOB();
		var v1 = Assert.IsType<ObLobLocatorV1>(blob.LocatorInternal);
		Assert.Equal((long)LobMagicCode, v1.MagicCode);
		Assert.Equal(1L, v1.Version);
		Assert.Equal(0L, v1.SnapshotVersion);
		Assert.Equal(0L, v1.PayloadOffset);
		Assert.Equal(0L, v1.PayloadSize);
		Assert.Empty(v1.RowId.ToArray());
		Assert.Equal(40, v1.BinaryData.Length);
		Assert.True(blob.IsEmpty); // 定位符 PayloadSize == 0
		Assert.Equal(0L, blob.Length);
		Assert.Empty(blob.TryGetInMemoryBytes()!);
	}

	[Fact]
	public void GetEmptyOracleClob_builds_empty_v1_locator()
	{
		var clob = OracleClob.GetEmptyOracleClob();
		Assert.True(clob.IsEmptyLob());
		Assert.Equal(string.Empty, clob.ToString());
		Assert.Equal(0L, clob.length());
		Assert.True(OracleClob.getEmptyOracleClob().IsEmptyLob()); // 小写别名直接转发
	}

	// ---------- V2 定位符解析（BuildObLobLocatorV2） ----------

	[Fact]
	public void BuildObLobLocatorV2_simple_inrow_reads_payload()
	{
		var payload = new byte[] { 7, 8, 9 };
		var blob = BlobFromLocator(MakeV2SimpleLocatorBytes(payload));
		var v2 = Assert.IsType<ObLobLocatorV2>(blob.LocatorInternal);
		Assert.NotNull(v2.Common);
		Assert.Equal(3L, v2.PayloadSize);
		Assert.Equal(3L, blob.Length); // V2 定位符：长度直接取 PayloadSize
		Assert.Equal(payload, blob.GetBytes(1, 3));
		Assert.False(blob.IsEmpty);
	}

	[Fact]
	public void BuildObLobLocatorV2_simple_inrow_clob_decodes_utf8()
	{
		var clob = ClobFromLocator(MakeV2SimpleLocatorBytes(Utf8("中文")));
		var v2 = Assert.IsType<ObLobLocatorV2>(clob.LocatorInternal);
		Assert.Equal(6L, v2.PayloadSize); // 字节长度
		Assert.Equal("中文", clob.ToString());
		Assert.Equal(2L, clob.length()); // 字符长度
	}

	[Fact]
	public void BuildObLobLocatorV2_no_inrow_no_extern_reads_only_payload_size()
	{
		var data = new byte[36];
		WriteLE(data, 0, LobMagicCode, 4);
		WriteLE(data, 4, 4098u, 4);  // flags = readOnly，无 in-row、无 extern
		WriteLE(data, 28, 9uL, 8);   // 8+4+16 偏移处的 8 字节 payload size
		var blob = BlobFromLocator(data);
		var v2 = Assert.IsType<ObLobLocatorV2>(blob.LocatorInternal);
		Assert.Equal(9L, v2.PayloadSize);
		Assert.Equal(9L, blob.Length);
		Assert.False(blob.IsEmpty);
		Assert.Null(blob.TryGetInMemoryBytes()); // 无 in-row 数据且未连接服务器
	}

	[Fact]
	public void BuildObLobLocatorV2_extern_only_skips_extern_bytes()
	{
		var data = new byte[73];
		WriteLE(data, 0, LobMagicCode, 4);
		WriteLE(data, 4, 65538u, 4); // 仅 hasExtern
		WriteLE(data, 30, 2u, 2);    // extern 头里的第一个 short（额外跳过 2 字节）
		WriteLE(data, 40, 1u, 2);    // extern 头里的第二个 short（额外跳过 1 字节）
		WriteLE(data, 65, 7uL, 8);   // 跳过 2+1+4+16 之后的 8 字节 payload size
		var blob = BlobFromLocator(data);
		var v2 = Assert.IsType<ObLobLocatorV2>(blob.LocatorInternal);
		Assert.Equal(7L, v2.PayloadSize);
		Assert.Equal(7L, blob.Length);
		Assert.False(blob.IsEmpty);
	}

	[Fact]
	public void BuildObLobLocatorV2_inrow_and_extern_reads_payload_after_extern()
	{
		var data = new byte[45];
		WriteLE(data, 0, LobMagicCode, 4);
		WriteLE(data, 4, 73730u, 4); // hasInRowData + hasExtern
		WriteLE(data, 32, 3u, 4);    // extern 数据长度
		WriteLE(data, 36, 2u, 4);    // payload size
		data[40] = 0xAA;             // extern 数据（3 字节）
		data[41] = 0xAB;
		data[42] = 0xAC;
		data[43] = 0x21;             // payload（2 字节）
		data[44] = 0x22;
		var blob = BlobFromLocator(data);
		var v2 = Assert.IsType<ObLobLocatorV2>(blob.LocatorInternal);
		Assert.Equal(2L, v2.PayloadSize);
		Assert.Equal(new byte[] { 0x21, 0x22 }, blob.GetBytes(1, 2)); // extern 之后才是 in-row 数据
	}

	[Fact]
	public void BuildObLobLocatorV2_inrow_extern_size_mismatch_skips_data()
	{
		var data = new byte[45];
		WriteLE(data, 0, LobMagicCode, 4);
		WriteLE(data, 4, 73730u, 4);
		WriteLE(data, 32, 100u, 4);  // extern 长度超出剩余缓冲区
		WriteLE(data, 36, 2u, 4);
		var blob = BlobFromLocator(data);
		var v2 = Assert.IsType<ObLobLocatorV2>(blob.LocatorInternal);
		Assert.Equal(2L, v2.PayloadSize);
		Assert.Equal(2L, blob.Length); // 长度仍取定位符 PayloadSize
		Assert.Null(blob.TryGetInMemoryBytes()); // 长度校验失败时不读 in-row 数据
	}

	[Fact]
	public void BuildObLobLocatorV2_inrow_not_simple_not_init_reads_all_remaining()
	{
		var payload = new byte[] { 1, 2, 3, 4 };
		var data = new byte[12 + payload.Length];
		WriteLE(data, 0, LobMagicCode, 4);
		WriteLE(data, 4, 8194u, 4); // 仅 hasInRowData（非 simple、非 extern）
		// data[8] = 0（1 字节保留），data[9..11] = ObLobCommon flags = 0（未初始化）
		payload.AsSpan().CopyTo(data.AsSpan(12));
		var blob = BlobFromLocator(data);
		var v2 = Assert.IsType<ObLobLocatorV2>(blob.LocatorInternal);
		Assert.Equal(4L, v2.PayloadSize);
		Assert.Equal(payload, blob.GetBytes(1, 4));
	}

	[Fact]
	public void BuildObLobLocatorV2_inrow_not_simple_init_inrow_reads_payload_at_36()
	{
		var data = new byte[38];
		WriteLE(data, 0, LobMagicCode, 4);
		WriteLE(data, 4, 8194u, 4);
		data[9] = 5;               // ObLobCommon flags：isInit + inRow
		WriteLE(data, 28, 2uL, 8); // 12+16 偏移处的 payload size
		data[36] = 0x51;
		data[37] = 0x52;
		var blob = BlobFromLocator(data);
		var v2 = Assert.IsType<ObLobLocatorV2>(blob.LocatorInternal);
		Assert.Equal(2L, v2.PayloadSize);
		Assert.Equal(new byte[] { 0x51, 0x52 }, blob.GetBytes(1, 2)); // inRow：紧跟 payload size
	}

	[Fact]
	public void BuildObLobLocatorV2_inrow_not_simple_init_not_inrow_skips_56()
	{
		var data = new byte[94];
		WriteLE(data, 0, LobMagicCode, 4);
		WriteLE(data, 4, 8194u, 4);
		data[9] = 1;                // ObLobCommon flags：仅 isInit（非 inRow）
		WriteLE(data, 28, 2uL, 8);
		data[92] = 0x61;
		data[93] = 0x62;
		var blob = BlobFromLocator(data);
		var v2 = Assert.IsType<ObLobLocatorV2>(blob.LocatorInternal);
		Assert.Equal(2L, v2.PayloadSize);
		Assert.Equal(new byte[] { 0x61, 0x62 }, blob.GetBytes(1, 2)); // 非 inRow：额外跳过 56 字节
	}

	[Fact]
	public void OracleBlob_bind_ctor_builds_v2_locator_from_payload()
	{
		var blob = new OracleBlob(new byte[] { 1, 2, 3 }, useLobLocatorV2: true);
		var v2 = Assert.IsType<ObLobLocatorV2>(blob.LocatorInternal);
		Assert.Equal(3L, v2.PayloadSize);
		Assert.Equal(3L, blob.Length);
		Assert.Equal(new byte[] { 1, 2, 3 }, blob.GetBytes(1, 3));
		Assert.Equal(MakeV2SimpleLocatorBytes(new byte[] { 1, 2, 3 }), v2.BinaryData.ToArray());
	}

	[Fact]
	public void OracleBlob_bind_ctor_builds_v1_locator_from_payload()
	{
		var blob = new OracleBlob(new byte[] { 9, 9 }, useLobLocatorV2: false);
		var v1 = Assert.IsType<ObLobLocatorV1>(blob.LocatorInternal);
		Assert.Equal((long)LobMagicCode, v1.MagicCode);
		Assert.Equal(1L, v1.Version);
		Assert.Equal(0L, v1.PayloadOffset);
		Assert.Equal(2L, v1.PayloadSize);
		Assert.Empty(v1.RowId.ToArray());
		Assert.Equal(new byte[] { 9, 9 }, blob.GetBytes(1, 2));
	}

	[Theory]
	[InlineData(3u)]
	[InlineData(257u)] // 低字节不是 2 的高版本号
	public void OracleBlob_unknown_locator_version_throws(uint version)
	{
		var data = new byte[8];
		WriteLE(data, 0, LobMagicCode, 4);
		WriteLE(data, 4, version, 4);
		var ex = Assert.Throws<MySqlException>(() => BlobFromLocator(data));
		Assert.Equal($"UnKnown version of LOB locator: {version}", ex.Message);
	}

	[Fact]
	public void OracleClob_unknown_locator_version_throws()
	{
		var data = new byte[8];
		WriteLE(data, 0, LobMagicCode, 4);
		WriteLE(data, 4, 3u, 4);
		var ex = Assert.Throws<MySqlException>(() => ClobFromLocator(data));
		Assert.Equal("UnKnown version of LOB locator: 3", ex.Message);
	}

	[Fact]
	public void OracleClob_without_locator_decodes_data_to_chars()
	{
		var clob = new OracleClob(false, Utf8("中文"), "utf-8", (OracleConnection?)null);
		Assert.Equal("中文", clob.GetString());
		Assert.Equal(2L, clob.length());
	}

	// ---------- Lob 基类：构造 / 判空 / 读取 ----------

	[Fact]
	public void OracleBlob_constructors_offline()
	{
		Assert.True(new OracleBlob().IsEmpty);
		Assert.Equal(0L, new OracleBlob().Length);
		Assert.True(new OracleBlob((byte[]?)null).IsEmpty); // null 输入按空数据处理
		var blob = new OracleBlob(new byte[] { 1, 2, 3 });
		Assert.Equal(3L, blob.Length);
		Assert.False(blob.IsEmpty);
		Assert.Equal(0L, blob.Position);
		Assert.True(blob.CanRead);
		Assert.True(blob.CanWrite);
		Assert.True(blob.CanSeek);
		var withInterceptor = new OracleBlob(new byte[] { 1 }, new object());
		Assert.Equal(1L, withInterceptor.Length);
	}

	[Fact]
	public void OracleBlob_ctor_offset_length_clamps_to_available()
	{
		var blob = new OracleBlob(new byte[] { 1, 2, 3, 4 }, 1, 100);
		Assert.Equal(3L, blob.Length); // 长度钳制到 (数组长度 - offset)
		Assert.Equal(new byte[] { 2, 3, 4 }, blob.GetBytes(1, 3));
	}

	[Fact]
	public void OracleClob_ctor_offset_length_clamps_to_available()
	{
		var clob = new OracleClob(Utf8("abcdef"), 2, 100);
		Assert.Equal(4L, clob.length());
		Assert.Equal("cdef", clob.GetString());
	}

	[Fact]
	public void OracleBlob_copy_ctor_copies_state()
	{
		var source = new OracleBlob(new byte[] { 1, 2, 3 });
		source.Position = 2;
		var copy = new OracleBlob(source);
		Assert.Equal(3L, copy.Length);
		Assert.Equal(2L, copy.Position);
		Assert.Throws<ArgumentNullException>(() => new OracleBlob((OracleBlob)null!)); // 拷贝构造拒绝 null
	}

	[Theory]
	[InlineData(true, "")]           // 空字符串
	[InlineData(false, "abc")]       // 有字符数据
	public void OracleClob_isEmpty_reflects_char_data(bool expectedEmpty, string charData)
	{
		var clob = new OracleClob(charData, null);
		Assert.Equal(expectedEmpty, clob.IsEmptyLob()); // 判空同时检查字节数组与字符数据
	}

	[Fact]
	public void Lob_GetBytes_clamps_length_to_data_size()
	{
		var blob = new OracleBlob(new byte[] { 1, 2, 3 });
		Assert.Equal(new byte[] { 1, 2, 3 }, blob.GetBytes(1, 100)); // 请求超出时按数据长度截断
		Assert.Empty(blob.GetBytes(1, 0));
	}

	[Fact]
	public void Lob_GetBytes_beyond_remaining_returns_zero_padded_array()
	{
		var blob = new OracleBlob(new byte[] { 1, 2, 3, 4, 5 });
		var result = blob.GetBytes(3, 100);
		// 数组大小按整表长度（5）分配，但仅复制剩余 3 字节，尾部补零——既有行为
		Assert.Equal(5, result.Length);
		Assert.Equal(new byte[] { 3, 4, 5, 0, 0 }, result);
	}

	[Fact]
	public void Lob_GetBytes_position_below_one_throws()
	{
		var blob = new OracleBlob(new byte[] { 1 });
		var ex = Assert.Throws<MySqlException>(() => blob.GetBytes(0, 1));
		Assert.Equal("Out of range (position should be > 0, but is 0)", ex.Message);
	}

	[Fact]
	public void Lob_GetBinaryStream_returns_read_only_view()
	{
		var blob = new OracleBlob(new byte[] { 1, 2, 3 });
		using var stream = blob.GetBinaryStream();
		Assert.True(stream.CanRead);
		Assert.True(stream.CanSeek);
		Assert.False(stream.CanWrite); // 以只读视图打开
		Assert.Equal(new byte[] { 1, 2, 3 }, ReadAll(stream));
	}

	[Fact]
	public void Lob_GetBinaryStream_pos_len_slices_data()
	{
		var blob = new OracleBlob(new byte[] { 1, 2, 3 });
		using var stream = blob.GetBinaryStream(2, 2); // 1 基位置
		Assert.Equal(new byte[] { 2, 3 }, ReadAll(stream));
		using var tail = blob.GetBinaryStream(4, 0); // 末尾零长度切片合法
		Assert.Empty(ReadAll(tail));
	}

	[Theory]
	[InlineData(0, 1, "Out of range (position should be > 0)")]
	[InlineData(7, 1, "Out of range (position > stream size)")]
	[InlineData(4, 3, "Out of range (position + length - 1 > streamSize)")]
	public void Lob_GetBinary_stream_out_of_range_throws(long pos, long length, string message)
	{
		var blob = new OracleBlob(new byte[] { 1, 2, 3, 4, 5 });
		var ex = Assert.Throws<MySqlException>(() => blob.GetBinaryStream(pos, length));
		Assert.Equal(message, ex.Message);
	}

	// ---------- Lob：SetBinaryStream / LobOutputStream ----------

	[Fact]
	public void Lob_SetBinaryStream_returns_write_only_stream()
	{
		using var stream = new OracleBlob(new byte[] { 1, 2, 3 }).SetBinaryStream(1);
		Assert.False(stream.CanRead);
		Assert.False(stream.CanSeek);
		Assert.True(stream.CanWrite);
		Assert.Equal(0L, stream.Position); // 位置 1（1 基）对应流内偏移 0
		Assert.Throws<NotSupportedException>(() => stream.Length);
		Assert.Throws<NotSupportedException>(() => stream.Position = 1);
		Assert.Throws<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
		Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
		Assert.Throws<NotSupportedException>(() => stream.SetLength(1));
		stream.Flush(); // Flush 为空实现，不抛
	}

	[Fact]
	public void LobOutputStream_write_overwrites_then_grows()
	{
		var blob = new OracleBlob(new byte[] { 1, 2, 3 });
		using (var stream = blob.SetBinaryStream(2))
		{
			Assert.Equal(1L, stream.Position);
			stream.Write(new byte[] { 9, 9 }, 0, 2); // 覆盖中间两字节
			stream.Write(new byte[] { 8 }, 0, 1);    // 追加触发扩容
			Assert.Equal(4L, stream.Position);
		}
		Assert.Equal(new byte[] { 1, 9, 9, 8 }, blob.GetBytes(1, 4));
	}

	[Fact]
	public void LobOutputStream_write_beyond_end_zero_fills_gap()
	{
		var blob = new OracleBlob(new byte[] { 65 });
		using (var stream = blob.SetBinaryStream(3))
		{
			stream.Write(new byte[] { 57 }, 0, 1); // 从位置 3（1 基）写入，中间空隙补零
		}
		Assert.Equal(new byte[] { 65, 0, 57 }, blob.GetBytes(1, 3));
	}

	[Fact]
	public void LobOutputStream_write_argument_validation()
	{
		using var stream = new OracleBlob(new byte[] { 1 }).SetBinaryStream(1);
		Assert.Throws<ArgumentNullException>(() => stream.Write(null!, 0, 1));
		Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(new byte[2], 3, 0)); // offset 超出缓冲区
		Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(new byte[2], 0, 3)); // count 超出缓冲区
	}

	[Fact]
	public void Lob_SetBinaryStream_compacts_offset_buffer()
	{
		var blob = new OracleBlob(new byte[] { 0, 1, 2, 3 }, 1, 2); // 数据窗口 = {1,2}
		using (var stream = blob.SetBinaryStream(1))
		{
			stream.Write(new byte[] { 9 }, 0, 1); // 触发窗口压缩到偏移 0 后写入
		}
		Assert.Equal(new byte[] { 9, 2 }, blob.GetBytes(1, 2));
	}

	[Fact]
	public void Lob_SetBinaryStream_position_validation()
	{
		var closed = new OracleBlob(new byte[] { 1 });
		closed.Free();
		Assert.Throws<MySqlException>(() => closed.SetBinaryStream(1)); // 已关闭

		var mysqlBlob = new OracleBlob(new byte[] { 1 });
		Assert.Throws<MySqlException>(() => mysqlBlob.SetBinaryStream(0)); // 非Oracle模式：位置0非法

		var oracleBlob = new OracleBlob(new byte[] { 1 });
		oracleBlob.SetOracleMode(true);
		using (var stream = oracleBlob.SetBinaryStream(0)) // Oracle模式：位置0合法，等价于位置1
		{
			stream.Write(new byte[] { 6 }, 0, 1);
		}
		Assert.Equal(new byte[] { 6 }, oracleBlob.GetBytes(1, 1));
		Assert.Throws<ArgumentOutOfRangeException>(() => oracleBlob.SetBinaryStream(-1)); // 负位置仍非法
	}

	[Fact]
	public void Lob_GetBinaryStream_on_closed_lob_throws()
	{
		var blob = new OracleBlob(new byte[] { 1 });
		blob.Free();
		Assert.Throws<MySqlException>(() => blob.GetBinaryStream());
	}

	// ---------- Lob：SetBytes（经 OracleClob 调用基类实现） ----------

	[Fact]
	public void Lob_SetBytes_on_clob_pads_gap_with_spaces()
	{
		var clob = new OracleClob(Utf8("AB"));
		int written = clob.SetBytes(4, new byte[] { 0x43 }); // 位置4 越过末尾，间隙用空格填充
		Assert.Equal(1, written);
		Assert.Equal("AB C", clob.GetString()); // Clob 扩容时空位填 0x20
	}

	[Fact]
	public void Lob_SetBytes_position_below_one_throws()
	{
		var clob = new OracleClob(Utf8("AB"));
		var ex = Assert.Throws<MySqlException>(() => clob.SetBytes(0, new byte[] { 1 }));
		Assert.Equal("pos should be > 0, first position is 1.", ex.Message);
		Assert.Throws<ArgumentNullException>(() => clob.SetBytes(1, null!));
	}

	// ---------- Lob：Copy / ClearData / Free / Dispose / 序列化 ----------

	[Fact]
	public void Lob_Copy_transfers_content()
	{
		var source = new OracleBlob(new byte[] { 1, 2, 3 });
		var target = new OracleBlob();
		target.Copy(source);
		Assert.Equal(3L, target.Length);
		Assert.Equal(new byte[] { 1, 2, 3 }, target.GetBytes(1, 3));
		Assert.False(target.IsEmpty);
	}

	[Fact]
	public void Lob_Copy_null_throws_and_self_copy_is_noop()
	{
		var blob = new OracleBlob(new byte[] { 1 });
		Assert.Throws<ArgumentNullException>(() => blob.Copy(null!));
		blob.Copy(blob); // 自拷贝不抛、不改动
		Assert.Equal(1L, blob.Length);
	}

	[Fact]
	public void Lob_Copy_clones_locator()
	{
		var source = OracleBlob.GetEmptyBLOB();
		var target = new OracleBlob();
		target.Copy(source);
		Assert.NotNull(target.LocatorInternal);
		Assert.NotSame(source.LocatorInternal, target.LocatorInternal); // 定位符按副本复制
		Assert.IsType<ObLobLocatorV1>(target.LocatorInternal);
		Assert.True(target.IsEmpty);
	}

	[Fact]
	public void Lob_ClearData_resets_content()
	{
		var blob = new OracleBlob(new byte[] { 1, 2, 3 });
		blob.ClearData();
		Assert.Equal(0L, blob.Length);
		Assert.True(blob.IsEmpty);
		Assert.Empty(blob.GetBytes(1, 3));

		var clob = new OracleClob("hello", null);
		clob.ClearData();
		Assert.Equal(string.Empty, clob.GetString()); // 字符数据一并清空
	}

	[Fact]
	public void Lob_Free_closes_and_rejects_later_operations()
	{
		var blob = new OracleBlob(new byte[] { 1, 2, 3 });
		blob.Free();
		Assert.True(blob.IsEmpty);
		Assert.False(blob.CanRead);
		Assert.False(blob.CanWrite);
		Assert.False(blob.CanSeek);
		Assert.Throws<MySqlException>(() => blob.GetBytes(1, 1));
		Assert.Throws<MySqlException>(() => blob.GetBinaryStream());
		Assert.Throws<MySqlException>(() => blob.SetBinaryStream(1));
		Assert.Throws<MySqlException>(() => blob.SetBytes(1, new byte[] { 1 }));
		Assert.Throws<MySqlException>(() => blob.Truncate(0));
		Assert.Throws<MySqlException>(() => blob.Read(new byte[1], 0, 1));
		Assert.Throws<MySqlException>(() => blob.Write(new byte[1], 0, 1));
		Assert.Throws<MySqlException>(() => blob.Seek(0, SeekOrigin.Begin));
		Assert.Throws<ObjectDisposedException>(() => blob.Length); // Length 抛 ObjectDisposedException
		Assert.Throws<MySqlException>(() => blob.TryGetInMemoryBytes());
	}

	[Fact]
	public void Lob_Dispose_is_idempotent()
	{
		var blob = new OracleBlob(new byte[] { 1 });
		blob.Dispose();
		blob.Dispose(); // 二次释放不抛
		Assert.False(blob.CanRead);
	}

	[Fact]
	public void Lob_GetObjectData_stores_blob_state()
	{
		var blob = new OracleBlob(new byte[] { 1, 2, 3 });
		var info = new SerializationInfo(typeof(OracleBlob), new FormatterConverter());
		blob.GetObjectData(info, default(StreamingContext));
		Assert.Equal(0, info.GetInt32("m_offset"));
		Assert.Equal(3, info.GetInt32("m_length"));
		Assert.Equal(new byte[] { 1, 2, 3 }, Assert.IsType<byte[]>(info.GetValue("m_data", typeof(byte[]))));
		Assert.Null(info.GetValue("m_locator", typeof(ObLobLocator)));
		Assert.Null(info.GetString("m_encoding"));
		Assert.Null(info.GetString("m_charData"));
		Assert.Equal(-1, info.GetInt32("m_lengthFromServer"));
		Assert.False(info.GetBoolean("m_isOracleMode"));
		Assert.False(info.GetBoolean("m_isClosed"));
	}

	[Fact]
	public void Lob_GetObjectData_stores_clob_state()
	{
		var clob = new OracleClob("hello", null);
		var info = new SerializationInfo(typeof(OracleClob), new FormatterConverter());
		clob.GetObjectData(info, default(StreamingContext));
		Assert.Equal("hello", info.GetString("m_charData"));
		Assert.Equal("utf-8", info.GetString("m_encoding")); // Clob 构造时固定 UTF-8
		Assert.Equal(5, info.GetInt32("m_length"));
	}

	// ---------- OracleBlob：流式 Read/Write/Seek/Position ----------

	[Fact]
	public void OracleBlob_Read_reads_from_current_position()
	{
		var blob = new OracleBlob(Utf8("ABCDE"));
		var buffer = new byte[10];
		Assert.Equal(2, blob.Read(buffer, 0, 2)); // 'A','B'
		Assert.Equal(3, blob.Read(buffer, 2, 8)); // 剩余 3 字节：部分读
		Assert.Equal(Encoding.ASCII.GetBytes("ABCDE"), buffer.AsSpan(0, 5).ToArray());
		Assert.Equal(0, blob.Read(buffer, 0, 1)); // 到达末尾返回 0
	}

	[Fact]
	public void OracleBlob_Read_argument_validation()
	{
		var blob = new OracleBlob(new byte[] { 1 });
		Assert.Throws<ArgumentNullException>(() => blob.Read(null!, 0, 1));
		Assert.Throws<ArgumentOutOfRangeException>(() => blob.Read(new byte[2], 3, 0));
		Assert.Throws<ArgumentOutOfRangeException>(() => blob.Read(new byte[2], 0, 3));
	}

	[Fact]
	public void OracleBlob_Write_appends_and_supports_partial_buffers()
	{
		var blob = new OracleBlob();
		blob.Write(new byte[] { 1, 2 }, 0, 2);
		blob.Write(new byte[] { 3, 4, 5 }, 1, 2); // 只写入缓冲区片段 {4,5}
		Assert.Equal(new byte[] { 1, 2, 4, 5 }, blob.GetBytes(1, 4));
		Assert.Equal(4L, blob.Position);
		blob.Write(new byte[3], 0, 0); // 零长度写入不影响位置与数据
		Assert.Equal(4L, blob.Position);
	}

	[Fact]
	public void OracleBlob_Write_argument_validation()
	{
		var blob = new OracleBlob(new byte[] { 1 });
		Assert.Throws<ArgumentNullException>(() => blob.Write(null!, 0, 1));
		Assert.Throws<ArgumentOutOfRangeException>(() => blob.Write(new byte[2], 3, 0));
		Assert.Throws<ArgumentOutOfRangeException>(() => blob.Write(new byte[2], 0, 3));
	}

	[Fact]
	public void OracleBlob_write_read_roundtrip()
	{
		var blob = new OracleBlob();
		var payload = new byte[257];
		for (int i = 0; i < payload.Length; i++)
		{
			payload[i] = (byte)(i % 251);
		}
		blob.Write(payload, 0, payload.Length);
		blob.Position = 0;
		var read = new byte[payload.Length];
		Assert.Equal(payload.Length, blob.Read(read, 0, read.Length));
		Assert.Equal(payload, read);
	}

	[Fact]
	public void OracleBlob_Seek_supports_all_origins()
	{
		var blob = new OracleBlob(Utf8("ABCDE"));
		Assert.Equal(2L, blob.Seek(2, SeekOrigin.Begin));
		Assert.Equal(3L, blob.Seek(1, SeekOrigin.Current));
		Assert.Equal(4L, blob.Seek(-1, SeekOrigin.End));
		blob.Position = 1; // Position 赋值等价于从 Begin 定位
		Assert.Equal(1L, blob.Position);
		var one = new byte[1];
		Assert.Equal(1, blob.Read(one, 0, 1));
		Assert.Equal((byte)'B', one[0]);
	}

	[Fact]
	public void OracleBlob_Seek_edge_cases()
	{
		var blob = new OracleBlob(Utf8("AB"));
		Assert.Throws<IOException>(() => blob.Seek(-1, SeekOrigin.Begin)); // 起始前定位抛 IOException
		Assert.Throws<ArgumentOutOfRangeException>(() => blob.Seek(0, (SeekOrigin)99)); // 非法 origin
		Assert.Equal(10L, blob.Seek(10, SeekOrigin.Begin)); // 允许越过末尾
		Assert.Equal(0, blob.Read(new byte[1], 0, 1)); // 越过末尾后读取返回 0
	}

	// ---------- OracleBlob：SetBytes / Truncate ----------

	[Fact]
	public void OracleBlob_SetBytes_overwrites_in_place()
	{
		var blob = new OracleBlob(new byte[] { 1, 2, 3 });
		Assert.Equal(2, blob.SetBytes(2, new byte[] { 8, 9 })); // 完整落在数据内部：原地覆盖
		Assert.Equal(new byte[] { 1, 8, 9 }, blob.GetBytes(1, 3));
		Assert.Equal(3L, blob.Length);
	}

	[Fact]
	public void OracleBlob_SetBytes_appends_and_extends()
	{
		var blob = new OracleBlob(new byte[] { 1, 2, 3 });
		blob.SetBytes(4, new byte[] { 7, 8 }); // 从末尾追加
		Assert.Equal(new byte[] { 1, 2, 3, 7, 8 }, blob.GetBytes(1, 5));
		Assert.Equal(5L, blob.Length);
	}

	[Fact]
	public void OracleBlob_SetBytes_with_offset_len_writes_fragment()
	{
		var blob = new OracleBlob(new byte[] { 1, 2, 3 });
		Assert.Equal(2, blob.SetBytes(2, new byte[] { 7, 8, 9, 10 }, 1, 2)); // 只写 {8,9}
		Assert.Equal(new byte[] { 1, 8, 9 }, blob.GetBytes(1, 3));
	}

	[Fact]
	public void OracleBlob_SetBytes_position_below_one_throws()
	{
		var blob = new OracleBlob(new byte[] { 1 });
		var ex = Assert.Throws<MySqlException>(() => blob.SetBytes(0, new byte[] { 1 }));
		Assert.Equal("pos should be > 0, first position is 1.", ex.Message);
	}

	[Fact]
	public void OracleBlob_SetBytes_oracle_mode_fills_gap_with_zeros()
	{
		var blob = new OracleBlob(new byte[] { 1, 2 });
		blob.SetOracleMode(true);
		Assert.Equal(1, blob.SetBytes(5, new byte[] { 8 })); // Oracle 模式扩容到 pos+len，空隙补零
		Assert.Equal(new byte[] { 1, 2, 0, 0, 8 }, blob.GetBytes(1, 5));
		Assert.Equal(0, blob.SetBytes(1, Array.Empty<byte>())); // Oracle 模式空写入直接返回 0
	}

	[Fact]
	public void OracleBlob_SetBytes_on_locator_requires_connection()
	{
		var blob = new OracleBlob(new byte[] { 1 }, useLobLocatorV2: true);
		Assert.Throws<MySqlException>(() => blob.SetBytes(1, new byte[] { 2 })); // 无连接时拒绝服务器更新
		Assert.Throws<MySqlException>(() => blob.SetBytes(1, new byte[] { 2 }, 0, 1));
	}

	[Fact]
	public void OracleBlob_Truncate_shrinks_or_keeps_length()
	{
		var blob = new OracleBlob(new byte[] { 1, 2, 3, 4 });
		blob.Truncate(2);
		Assert.Equal(2L, blob.Length);
		Assert.Equal(new byte[] { 1, 2 }, blob.GetBytes(1, 2));
		blob.Truncate(2); // 等长裁剪无变化
		Assert.Equal(2L, blob.Length);
		blob.Truncate(0);
		Assert.Equal(0L, blob.Length);
	}

	[Theory]
	[InlineData(-1, "len argument can not be < 1")]
	[InlineData(5, "len argument can not be larger than the BLOB's length")]
	public void OracleBlob_Truncate_invalid_arguments_throw(long len, string message)
	{
		var blob = new OracleBlob(new byte[] { 1, 2, 3 });
		var ex = Assert.Throws<MySqlException>(() => blob.Truncate(len));
		Assert.Equal(message, ex.Message);
	}

	[Fact]
	public void OracleBlob_Truncate_oracle_mode_negative_message()
	{
		var blob = new OracleBlob(new byte[] { 1 });
		blob.SetOracleMode(true);
		var ex = Assert.Throws<MySqlException>(() => blob.Truncate(-1));
		Assert.Equal("len should be >= 0", ex.Message); // Oracle 模式下负数报错文案不同
	}

	// ---------- OracleBlob：FindPosition / GetCharacterStream / TryGetInMemoryBytes ----------

	[Theory]
	[InlineData("b", 1L, 2L)]     // 首次出现
	[InlineData("b", 3L, 5L)]     // 从中间开始搜索
	[InlineData("abd", 1L, 4L)]   // 多字节模式（需结束于数据末尾之前，见源码搜索窗口实现）
	[InlineData("z", 1L, -1L)]    // 未找到
	[InlineData("abcabd!", 1L, -1L)] // 模式超出末尾
	public void OracleBlob_FindPosition_finds_pattern(string patternText, long start, long expected)
	{
		var blob = new OracleBlob(Utf8("abcabdXY"));
		Assert.Equal(expected, blob.FindPosition(Utf8(patternText), start));
	}

	[Fact]
	public void OracleBlob_FindPosition_boundaries()
	{
		var blob = new OracleBlob(Utf8("abc"));
		Assert.Equal(0L, blob.FindPosition(Array.Empty<byte>(), 1)); // 空模式固定返回 0
		Assert.Throws<ArgumentOutOfRangeException>(() => blob.FindPosition(Utf8("a"), 0));
		Assert.Throws<ArgumentOutOfRangeException>(() => blob.FindPosition(Utf8("a"), 4)); // start > 数据长度
		var pattern = new OracleBlob(Utf8("b"));
		Assert.Equal(2L, blob.FindPosition(pattern, 1)); // OracleBlob 模式重载
	}

	[Fact]
	public void OracleBlob_GetCharacterStream_decodes_ascii()
	{
		var blob = new OracleBlob(Utf8("hi"));
		using var reader = blob.GetCharacterStream();
		Assert.NotNull(reader);
		Assert.Equal("hi", reader!.ReadToEnd());
		Assert.Null(new OracleBlob().GetCharacterStream()); // 无数据时返回 null
	}

	[Fact]
	public void OracleBlob_TryGetInMemoryBytes_returns_window_copy()
	{
		var blob = new OracleBlob(new byte[] { 0, 1, 2, 3 }, 1, 2);
		Assert.Equal(new byte[] { 1, 2 }, blob.TryGetInMemoryBytes()); // 只复制数据窗口
		Assert.Empty(new OracleBlob().TryGetInMemoryBytes()!);
	}

	// ---------- OracleClob：构造 / GetString / ToString ----------

	[Fact]
	public void OracleClob_constructors_and_getters()
	{
		Assert.Equal(string.Empty, new OracleClob().GetString());
		Assert.Equal(0L, new OracleClob().length());
		var fromBytes = new OracleClob(Utf8("héllo"));
		Assert.Equal("héllo", fromBytes.GetString());
		Assert.Equal("héllo", fromBytes.ToString());
		var fromString = new OracleClob("héllo", null);
		Assert.Equal("héllo", fromString.GetString());
		Assert.Equal(5L, fromString.length()); // length() 按字符（UTF-16 码元）计数
		var withInterceptor = new OracleClob(Utf8("ab"), new object());
		Assert.Equal("ab", withInterceptor.GetString());
	}

	[Fact]
	public void OracleClob_ToString_on_closed_returns_empty()
	{
		var clob = new OracleClob("data", null);
		clob.Free();
		Assert.Equal(string.Empty, clob.ToString()); // 关闭后 ToString 返回空串而非抛异常
	}

	// ---------- OracleClob：GetSubString / GetCharacterStream ----------

	[Theory]
	[InlineData(1L, 0, "")]
	[InlineData(2L, 3, "ell")]
	[InlineData(4L, 10, "lo")] // 请求超出剩余字符时钳制到剩余长度
	public void OracleClob_GetSubString_slices(long pos, int length, string expected)
	{
		var clob = new OracleClob("hello", null);
		Assert.Equal(expected, clob.GetSubString(pos, length));
		Assert.Equal(expected, clob.getSubString(pos, length)); // 小写别名
	}

	[Fact]
	public void OracleClob_GetSubString_byte_constructed_uses_decoded_text()
	{
		var clob = new OracleClob(Utf8("hello"));
		Assert.Equal("ell", clob.GetSubString(2, 3));
		Assert.Equal("lo", clob.GetSubString(4, 10));
	}

	[Fact]
	public void OracleClob_GetSubString_validation()
	{
		var clob = new OracleClob("hello", null);
		Assert.Throws<MySqlException>(() => clob.GetSubString(0, 1));
		Assert.Throws<MySqlException>(() => clob.GetSubString(1, -1));
		clob.Free();
		Assert.Null(clob.GetSubString(1, 1)); // 非Oracle模式：关闭后返回 null
		var oracleClob = new OracleClob("x", null);
		oracleClob.SetOracleMode(true);
		oracleClob.Free();
		Assert.Throws<MySqlException>(() => oracleClob.GetSubString(1, 1)); // Oracle模式：关闭后抛异常
	}

	[Fact]
	public void OracleClob_GetCharacterStream_reads_content()
	{
		var clob = new OracleClob("hello", null);
		using var reader = clob.GetCharacterStream();
		Assert.Equal("hello", reader.ReadToEnd());
		using var slice = clob.GetCharacterStream(2, 3);
		Assert.Equal("ell", slice.ReadToEnd());
		Assert.Throws<MySqlException>(() => clob.GetCharacterStream(1, 6)); // pos+length 超出字符数
		clob.Free();
		Assert.Null(clob.GetCharacterStream()); // 非Oracle模式：关闭后返回 null
		Assert.Null(clob.getCharacterStream()); // 小写别名行为一致
	}

	[Fact]
	public void OracleClob_GetCharacterStream_closed_oracle_mode_throws()
	{
		var clob = new OracleClob("x", null);
		clob.SetOracleMode(true);
		clob.Free();
		Assert.Throws<MySqlException>(() => clob.GetCharacterStream());
	}

	// ---------- OracleClob：Position 搜索 ----------

	[Theory]
	[InlineData("l", 1L, 3L)]
	[InlineData("l", 4L, 4L)]
	[InlineData("z", 1L, -1L)]
	[InlineData("", 2L, 2L)] // 空串在 start 位置匹配
	public void OracleClob_Position_finds_string_ordinally(string search, long start, long expected)
	{
		var clob = new OracleClob("hello", null);
		Assert.Equal(expected, clob.Position(search, start));
		Assert.Equal(expected, clob.position(search, start)); // 小写别名
	}

	[Fact]
	public void OracleClob_Position_clob_overload_and_validation()
	{
		var clob = new OracleClob("hello", null);
		Assert.Equal(3L, clob.Position(new OracleClob("l", null), 1));
		Assert.Throws<MySqlException>(() => clob.Position("l", 0));
		Assert.Throws<MySqlException>(() => clob.Position("l", 7)); // start 超出字符数
	}

	// ---------- OracleClob：SetString / length / truncate ----------

	[Fact]
	public void OracleClob_SetString_appends_to_empty()
	{
		var clob = new OracleClob();
		Assert.Equal(3, clob.SetString(1, "abc"));
		Assert.Equal("abc", clob.GetString());
		Assert.Equal(3L, clob.length());
	}

	[Fact]
	public void OracleClob_SetString_overwrites_byte_constructed()
	{
		var clob = new OracleClob(Utf8("hello")); // 字节数组构造：m_data 已就位，可原地覆盖
		Assert.Equal(1, clob.SetString(2, "E"));
		Assert.Equal("hEllo", clob.GetString());
	}

	[Fact]
	public void OracleClob_SetString_validation()
	{
		var clob = new OracleClob("abc", null);
		Assert.Throws<MySqlException>(() => clob.SetString(0, "x"));
		Assert.Throws<MySqlException>(() => clob.SetString(1, null!, 0, 0)); // 4 参重载拒绝 null 字符串
		var oracleClob = new OracleClob("abc", null);
		oracleClob.SetOracleMode(true);
		Assert.Equal(0, oracleClob.SetString(1, "")); // Oracle 模式：空字符串短路返回 0
		oracleClob.Free();
		Assert.Throws<MySqlException>(() => oracleClob.SetString(1, "x")); // Oracle 模式：关闭后抛异常
	}

	[Fact]
	public void OracleClob_SetString_with_offset_len_writes_fragment()
	{
		var clob = new OracleClob();
		Assert.Equal(1, clob.SetString(1, "XYZ", 1, 1)); // 只写 "Y"
		Assert.Equal("Y", clob.GetString());
	}

	[Fact]
	public void OracleClob_SetString_invalid_utf8_byte_position_throws()
	{
		var clob = new OracleClob(new byte[] { 0x80, 0x41 }); // 0x80 不是合法的 UTF-8 首字节
		Assert.Throws<IOException>(() => clob.SetString(2, "x")); // 换算字节位置时校验 UTF-8
	}

	[Theory]
	[InlineData("hello", 5L)]   // 纯 ASCII：字节数 == 字符数
	[InlineData("héllo", 5L)]   // 含 2 字节字符
	[InlineData("a中b", 3L)]    // 含 3 字节字符
	[InlineData("a😀b", 4L)]    // 含 4 字节字符（代理对计 2 个 UTF-16 码元）
	public void OracleClob_length_walks_utf8_bytes(string text, long expected)
	{
		Assert.Equal(expected, new OracleClob(Utf8(text)).length());
	}

	[Fact]
	public void OracleClob_length_invalid_utf8_throws()
	{
		var clob = new OracleClob(new byte[] { 0x80, 0x41 });
		Assert.Throws<IOException>(() => clob.length());
	}

	[Fact]
	public void OracleClob_truncate_char_data()
	{
		var clob = new OracleClob("hello", null);
		clob.truncate(3);
		Assert.Equal("hel", clob.GetString());
		Assert.Throws<MySqlException>(() => clob.truncate(6)); // 超出字符数
		clob.truncate(0);
		Assert.Equal(string.Empty, clob.GetString());
	}

	[Theory]
	[InlineData("h中l", 2L, "h中")] // 3 字节字符整体保留
	[InlineData("héllo", 2L, "hé")] // 2 字节字符整体保留
	[InlineData("héllo", 5L, "héllo")]
	public void OracleClob_truncate_byte_constructed_keeps_char_boundary(string text, long len, string expected)
	{
		var clob = new OracleClob(Utf8(text));
		clob.truncate(len);
		Assert.Equal(expected, clob.GetString());
	}

	[Fact]
	public void OracleClob_truncate_validation()
	{
		var clob = new OracleClob("hello", null);
		Assert.Throws<ArgumentOutOfRangeException>(() => clob.truncate(-1)); // 非Oracle模式：负数抛 ArgumentOutOfRange
		clob.Free();
		Assert.Throws<ObjectDisposedException>(() => clob.truncate(1)); // 非Oracle模式：关闭后 ObjectDisposedException

		var oracleClob = new OracleClob("hello", null);
		oracleClob.SetOracleMode(true);
		Assert.Throws<MySqlException>(() => oracleClob.truncate(-1));
		oracleClob.Free();
		Assert.Throws<MySqlException>(() => oracleClob.truncate(1)); // Oracle模式：关闭后 MySqlException
	}

	// ---------- OracleClob：字符流 / ASCII 流 ----------

	[Fact]
	public void OracleClob_SetCharacterStream_roundtrip()
	{
		var clob = new OracleClob();
		using (var writer = clob.SetCharacterStream(1))
		{
			writer.Write("hello"); // 经 StreamWriter 按 UTF-8 落盘（默认带 BOM）
		}
		Assert.Equal("\uFEFFhello", clob.GetString()); // StreamWriter 会先写入 UTF-8 BOM（U+FEFF）
		Assert.Equal(6L, clob.length());
		Assert.Throws<MySqlException>(() => new OracleClob().SetCharacterStream(0)); // 位置 < 1
	}

	[Fact]
	public void OracleClob_SetAsciiStream_roundtrip()
	{
		var clob = new OracleClob();
		using (var stream = clob.SetAsciiStream(1))
		{
			stream.Write(new byte[] { 65, 66 }, 0, 2);
		}
		Assert.Equal("AB", clob.GetString());
	}

	[Fact]
	public void OracleClob_GetAsciiStream_encodes_char_data()
	{
		var clob = new OracleClob("héllo", null);
		using var stream = clob.GetAsciiStream();
		Assert.Equal(Utf8("héllo"), ReadAll(stream!)); // 字符数据按解析出的编码转为字节
		Assert.False(stream!.CanWrite); // 只读内存流

		var byteClob = new OracleClob(Utf8("abc"));
		using var rawStream = byteClob.GetAsciiStream();
		Assert.Equal(Utf8("abc"), ReadAll(rawStream!)); // 字节数据路径直接给二进制流
	}

	[Fact]
	public void OracleClob_GetAsciiStream_closed_semantics()
	{
		var clob = new OracleClob("x", null);
		clob.Free();
		Assert.Null(clob.GetAsciiStream()); // 非Oracle模式：关闭后返回 null
		var oracleClob = new OracleClob("x", null);
		oracleClob.SetOracleMode(true);
		oracleClob.Free();
		Assert.Throws<MySqlException>(() => oracleClob.GetAsciiStream());
	}

	[Fact]
	public void OracleClob_TryGetInMemoryBytes_encodes_char_data()
	{
		Assert.Equal(Utf8("héllo"), new OracleClob("héllo", null).TryGetInMemoryBytes());
		Assert.Empty(new OracleClob().TryGetInMemoryBytes()!); // 空 LOB 返回空数组
		var closed = new OracleClob("x", null);
		closed.Free();
		Assert.Throws<MySqlException>(() => closed.TryGetInMemoryBytes());
	}

	[Fact]
	public void OracleClob_oracle_mode_closed_rejects_metadata_calls()
	{
		var clob = new OracleClob("x", null);
		clob.SetOracleMode(true);
		clob.Free();
		Assert.Throws<MySqlException>(() => clob.length());
		Assert.Throws<MySqlException>(() => clob.Position("x", 1));
	}
}
