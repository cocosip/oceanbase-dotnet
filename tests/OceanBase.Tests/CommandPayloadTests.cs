using System;
using System.Buffers.Binary;
using System.Text;
using OceanBase.Protocol.Payloads;
using OceanBase.Protocol.Serialization;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 其余协议载荷的编解码测试（internal 类型，无需服务端）。
///
/// 覆盖预编译语句响应、认证切换/续传、caching_sha2 应答、COM_QUERY、
/// LOCAL INFILE 请求与 REF CURSOR 取数包的解析与组装。
/// </summary>
public sealed class CommandPayloadTests
{
	// ---------- StatementPrepareResponsePayload ----------

	private static byte[] PrepareResponseBytes(uint statementId, uint columns, uint parameters)
	{
		var w = new ByteBufferWriter(32);
		w.Write((byte)0);              // 状态字节，固定 0
		w.Write((int)statementId);     // int32
		w.Write((ushort)columns);      // uint16
		w.Write((ushort)parameters);   // uint16
		w.Write((byte)0);              // 保留
		w.Write((ushort)0);            // 警告数
		return w.ToPayloadData().Span.ToArray();
	}

	[Theory]
	[InlineData(1u, 2u, 3u)]
	[InlineData(0u, 0u, 0u)]
	[InlineData(int.MaxValue, 999u, 777u)]
	public void StatementPrepareResponse_parses_header_fields(uint statementId, uint columns, uint parameters)
	{
		var payload = StatementPrepareResponsePayload.Create(PrepareResponseBytes(statementId, columns, parameters));

		Assert.Equal((int)statementId, payload.StatementId);
		Assert.Equal((int)columns, payload.ColumnCount);
		Assert.Equal((int)parameters, payload.ParameterCount);
	}

	// ---------- StatementPrepareExecuteResponsePayload（OceanBase Oracle 模式 0xA1 应答头）----------

	private static byte[] PrepareExecuteBytes(int statementId, ushort columns, ushort parameters, short warnings, int extendFlag, byte hasResultSet)
	{
		var w = new ByteBufferWriter(32);
		w.Write((byte)0);
		w.Write(statementId);
		w.Write(columns);
		w.Write(parameters);
		w.Write((byte)0);
		w.Write((ushort)warnings);   // Write 无 short 重载，用 ushort 保证 2 字节
		w.Write(extendFlag);
		w.Write(hasResultSet);
		return w.ToPayloadData().Span.ToArray();
	}

	[Fact]
	public void StatementPrepareExecuteResponse_parses_all_fields()
	{
		var payload = StatementPrepareExecuteResponsePayload.Create(PrepareExecuteBytes(7, 3, 2, warnings: 5, extendFlag: 1, hasResultSet: 1));

		Assert.Equal(7, payload.StatementId);
		Assert.Equal(3, payload.ColumnCount);
		Assert.Equal(2, payload.ParameterCount);
		Assert.Equal(5, payload.WarningCount);
		Assert.Equal(1, payload.ExtendFlag);
		Assert.True(payload.HasResultSet);
		Assert.False(payload.IsArrayBindingResult);
	}

	[Fact]
	public void StatementPrepareExecuteResponse_detects_array_binding_result_set()
	{
		// extendFlag 第 2 位表示该结果集是 ArrayBinding 内部结果集，不应暴露给用户
		var payload = StatementPrepareExecuteResponsePayload.Create(PrepareExecuteBytes(1, 0, 0, 0, extendFlag: 2, hasResultSet: 1));

		Assert.True(payload.IsArrayBindingResult);
		Assert.Equal(2, StatementPrepareExecuteResponsePayload.ArrayBindingField);
		Assert.Equal(0, StatementPrepareExecuteResponsePayload.NotReturningResultSet);
		Assert.Equal(1, StatementPrepareExecuteResponsePayload.ReturningResultSetWithoutField);
		Assert.Equal(3, StatementPrepareExecuteResponsePayload.ReturningResultSetWithField);
		Assert.Equal(4, StatementPrepareExecuteResponsePayload.PlOutParameter);
	}

	// ---------- AuthenticationMethodSwitchRequestPayload ----------

	[Fact]
	public void AuthSwitchRequest_parses_plugin_name_and_data()
	{
		var w = new ByteBufferWriter(32);
		w.Write(AuthenticationMethodSwitchRequestPayload.Signature);
		w.Write(Encoding.UTF8.GetBytes("mysql_native_password"));
		w.Write((byte)0);              // 插件名以 NUL 结尾
		w.Write(new byte[] { 0x11, 0x22, 0x33 });

		var payload = AuthenticationMethodSwitchRequestPayload.Create(w.ToPayloadData().Span);

		Assert.Equal("mysql_native_password", payload.Name);
		Assert.Equal(new byte[] { 0x11, 0x22, 0x33 }, payload.Data);
	}

	[Fact]
	public void AuthSwitchRequest_without_name_falls_back_to_old_password()
	{
		// 只有签名一个字节：服务端要求旧版（pre-4.1）密码认证
		var payload = AuthenticationMethodSwitchRequestPayload.Create(new byte[] { AuthenticationMethodSwitchRequestPayload.Signature });

		Assert.Equal("mysql_old_password", payload.Name);
		Assert.Empty(payload.Data);
	}

	// ---------- AuthenticationMoreDataPayload ----------

	[Fact]
	public void AuthMoreData_carries_remaining_bytes()
	{
		var w = new ByteBufferWriter(16);
		w.Write(AuthenticationMoreDataPayload.Signature);
		w.Write(new byte[] { 1, 2, 3, 4 });

		var payload = AuthenticationMoreDataPayload.Create(w.ToPayloadData().Span);

		Assert.Equal(new byte[] { 1, 2, 3, 4 }, payload.Data);
	}

	[Fact]
	public void AuthMoreData_without_data_is_empty()
	{
		var payload = AuthenticationMoreDataPayload.Create(new byte[] { AuthenticationMoreDataPayload.Signature });

		Assert.Empty(payload.Data);
	}

	// ---------- CachingSha2ServerResponsePayload ----------

	[Fact]
	public void CachingSha2_response_recognizes_fast_auth_success()
	{
		var payload = CachingSha2ServerResponsePayload.Create(new byte[] { 1, CachingSha2ServerResponsePayload.SuccessSignature });

		Assert.True(payload.Succeeded);
		Assert.False(payload.FullAuthRequired);
	}

	[Fact]
	public void CachingSha2_response_recognizes_full_auth_request()
	{
		var payload = CachingSha2ServerResponsePayload.Create(new byte[] { 1, CachingSha2ServerResponsePayload.FullAuthRequiredSignature });

		Assert.False(payload.Succeeded);
		Assert.True(payload.FullAuthRequired);
	}

	// ---------- QueryPayload ----------

	[Fact]
	public void QueryPayload_writes_command_word_and_text()
	{
		var payload = QueryPayload.Create(supportsQueryAttributes: false, "SELECT 1"u8);

		var span = payload.Span;
		Assert.Equal(3, span[0]);      // COM_QUERY
		Assert.Equal("SELECT 1", Encoding.UTF8.GetString(span.Slice(1)));
	}

	[Fact]
	public void QueryPayload_with_query_attributes_writes_parameter_count_header()
	{
		var payload = QueryPayload.Create(supportsQueryAttributes: true, "SELECT 1"u8);

		var span = payload.Span;
		Assert.Equal(3, span[0]);
		Assert.Equal(0, span[1]);      // 参数个数为 0
		Assert.Equal(1, span[2]);      // 参数集标志
		Assert.Equal("SELECT 1", Encoding.UTF8.GetString(span.Slice(3)));
	}

	// ---------- LocalInfilePayload ----------

	[Fact]
	public void LocalInfile_parses_file_name()
	{
		var w = new ByteBufferWriter(32);
		w.Write(LocalInfilePayload.Signature);
		w.Write(Encoding.UTF8.GetBytes("/tmp/data.csv"));

		var payload = LocalInfilePayload.Create(w.ToPayloadData().Span);

		Assert.Equal("/tmp/data.csv", payload.FileName);
	}

	// ---------- RefCursorFetchPayloadCreator.WriteFetch ----------

	[Fact]
	public void RefCursorFetch_writes_com_stmt_fetch_frame()
	{
		var w = new ByteBufferWriter(32);
		RefCursorFetchPayloadCreator.WriteFetch(w, cursorId: 0x01020304, rowsToFetch: 100);

		var span = w.ToPayloadData().Span;
		Assert.Equal(19, span.Length); // 1 + 4 + 4 + 2 + 4 + 4
		Assert.Equal(28, span[0]);     // COM_STMT_FETCH
		Assert.Equal(0x01020304, BinaryPrimitives.ReadInt32LittleEndian(span.Slice(1)));
		Assert.Equal(100, BinaryPrimitives.ReadInt32LittleEndian(span.Slice(5)));
		// 其余字段全为 0
		for (int i = 9; i < span.Length; i++)
		{
			Assert.Equal(0, span[i]);
		}
	}
}
