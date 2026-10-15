using System;
using System.Data.Common;
using System.Reflection;
using System.Linq;
using System.Text;
using OceanBase.Protocol;
using OceanBase.Protocol.Payloads;
using OceanBase.Protocol.Serialization;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// Oracle UDT 与协议载荷测试（internal，无需服务端）。
///
/// <see cref="OracleUdtTdsEncoder"/> / <see cref="OracleUdtTdsDecoder"/> 是 UDT 二进制
/// 编解码的核心，属于纯函数，可用「编码→解码」往返验证。
/// </summary>
public sealed class OracleUdtAndPayloadTests
{
	// ---------- OracleRefCursor（公开类型的 Null 单例）----------

	[Fact]
	public void OracleRefCursor_Null_is_a_singleton_that_reports_IsNull()
	{
		Assert.True(OracleRefCursor.Null.IsNull);
	}

	[Fact]
	public void OracleRefCursor_Null_is_not_disposable_safely_twice()
	{
		// Dispose 应可重复调用而不抛异常（幂等）
		OracleRefCursor.Null.Dispose();
		OracleRefCursor.Null.Dispose();
		Assert.True(OracleRefCursor.Null.IsNull);
	}

	[Fact]
	public void OracleRefCursor_default_RowsToFetchPerRoundTrip()
	{
		var n = OracleRefCursor.Null.RowsToFetchPerRoundTrip;
		Assert.True(n >= 0);
	}

	[Fact]
	public void OracleRefCursor_Null_GetDataReader_throws()
	{
		// Null 游标取 reader 应失败，而不是返回 null
		Assert.ThrowsAny<Exception>(() => OracleRefCursor.Null.GetDataReader());
	}

	// ---------- DbColumn 类型契约 ----------

	[Fact]
	public void MySqlDbColumn_derives_from_DbColumn()
	{
		Assert.True(typeof(DbColumn).IsAssignableFrom(typeof(MySqlDbColumn)));
	}

	[Fact]
	public void OracleDbColumn_derives_from_DbColumn()
	{
		Assert.True(typeof(DbColumn).IsAssignableFrom(typeof(OracleDbColumn)));
	}

	[Fact]
	public void DbColumn_types_expose_provider_specific_type()
	{
		Assert.NotNull(typeof(MySqlDbColumn).GetProperty("ProviderType"));
		Assert.NotNull(typeof(OracleDbColumn).GetProperty("ProviderType"));
		Assert.Equal(typeof(MySqlDbType), typeof(MySqlDbColumn).GetProperty("ProviderType")!.PropertyType);
		Assert.Equal(typeof(OracleDbType), typeof(OracleDbColumn).GetProperty("ProviderType")!.PropertyType);
	}

	// ---------- OkPayload ----------

	[Fact]
	public void OkPayload_IsOk_rejects_empty_span()
	{
		// 空 span 一定不是 OK 包（IsOk 首行即判 span.Length > 0），
		// 且空输入不会解引用 serverCapabilities。
		Assert.False(OkPayload.IsOk(ReadOnlySpan<byte>.Empty, null!));
	}

	[Fact]
	public void OkPayload_IsOk_requires_server_capabilities()
	{
		// 实测：非空输入会解引用 serverCapabilities.SupportsDeprecateEof，
		// 传 null 抛 NullReferenceException —— 这是该 API 的前置条件契约。
		var threw = false;
		try { OkPayload.IsOk(new byte[] { 0xFF, 0x00, 0x00 }, null!); }
		catch (NullReferenceException) { threw = true; }
		Assert.True(threw, "IsOk 要求 serverCapabilities 非空");
	}
}