using System;
using OceanBase.Core;
using OceanBase.Utilities;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 标识符规范化测试（internal，无需服务端）。
///
/// <see cref="NormalizedSchema"/> 把用户写的 schema 名（可能带反引号/双引号、
/// 可能是 schema.table 或 Oracle 的 schema.package.proc）规范化成驱动内部形式。
///
/// 全部断言依据对原始程序集的实测行为编写，注意三点反直觉之处：
///   1. 单段名（无 "."）**必须**提供 defaultSchema，否则抛 ArgumentException；
///   2. Oracle 模式下未加引号的标识符会被**转成大写**；
///   3. FullyQualified 返回的是**反引号包裹**形式（`` `db`.`table` ``）。
/// </summary>
public sealed class NormalizedSchemaTests
{
	// ---------- MySQL 模式 ----------

	[Theory]
	[InlineData("db.table", "db", "table")]
	[InlineData("`db`.`table`", "db", "table")]
	[InlineData("db.`my table`", "db", "my table")]
	[InlineData("  db . table  ", "db", "table")]
	public void MySql_schema_qualified_name(string input, string expectedSchema, string expectedComponent)
	{
		var s = NormalizedSchema.MustNormalize(input);
		Assert.Equal(expectedSchema, s.Schema);
		Assert.Equal(expectedComponent, s.Component);
	}

	[Theory]
	[InlineData("mytable", "mytable")]
	[InlineData("`mytable`", "mytable")]
	[InlineData("`my table`", "my table")]
	[InlineData("`my``table`", "my`table")]        // 反引号转义：`` → `
	public void MySql_single_part_uses_default_schema(string input, string expectedComponent)
	{
		var s = NormalizedSchema.MustNormalize(input, defaultSchema: "mydb");
		Assert.Equal("mydb", s.Schema);
		Assert.Equal(expectedComponent, s.Component);
	}

	[Fact]
	public void MySql_single_part_without_default_schema_throws()
	{
		// 实测：单段名无法推断 schema，必须由调用方给出 defaultSchema
		Assert.Throws<ArgumentException>(() => NormalizedSchema.MustNormalize("mytable"));
	}

	[Fact]
	public void MySql_explicit_schema_overrides_default()
	{
		var s = NormalizedSchema.MustNormalize("other.table", defaultSchema: "mydb");
		Assert.Equal("other", s.Schema);
		Assert.Equal("table", s.Component);
	}

	// ---------- Oracle 模式 ----------

	[Fact]
	public void Oracle_mode_sets_flag()
	{
		var s = NormalizedSchema.MustNormalize("myschema.mytable", isOracleMode: true);
		Assert.True(s.IsOracleMode);
	}

	[Fact]
	public void MySql_mode_leaves_flag_false()
	{
		Assert.False(NormalizedSchema.MustNormalize("db.table").IsOracleMode);
	}

	[Fact]
	public void Oracle_unquoted_identifiers_are_upper_cased()
	{
		// Oracle 的未加引号标识符语义上是大写
		var s = NormalizedSchema.MustNormalize("schema.table", isOracleMode: true);
		Assert.Equal("SCHEMA", s.Schema);
		Assert.Equal("TABLE", s.Component);
	}

	[Fact]
	public void Oracle_quoted_identifiers_preserve_case()
	{
		var s = NormalizedSchema.MustNormalize("\"S\".\"T\"", isOracleMode: true);
		Assert.Equal("S", s.Schema);
		Assert.Equal("T", s.Component);
	}

	[Fact]
	public void Oracle_double_quote_escape_is_unescaped()
	{
		// Oracle 用 "" 表示一个字面双引号
		var s = NormalizedSchema.MustNormalize("\"S\".\"my\"\"table\"", isOracleMode: true);
		Assert.Equal("S", s.Schema);
		Assert.Equal("my\"table", s.Component);
	}

	[Fact]
	public void Oracle_three_part_name_captures_package()
	{
		// Oracle 的 schema.package.proc
		var s = NormalizedSchema.MustNormalize("s.p.proc", isOracleMode: true);
		Assert.Equal("S", s.Schema);
		Assert.Equal("P", s.PackageName);
		Assert.Equal("PROC", s.Component);
	}

	[Fact]
	public void Oracle_two_part_name_has_no_package()
	{
		var s = NormalizedSchema.MustNormalize("s.t", isOracleMode: true);
		Assert.Null(s.PackageName);
	}

	// ---------- FullyQualified：反引号包裹 ----------

	[Fact]
	public void FullyQualified_wraps_in_backticks()
	{
		// 实测：返回 `schema`.`component`（不是 schema.component）
		Assert.Equal("`db`.`table`", NormalizedSchema.MustNormalize("db.table").FullyQualified);
	}

	[Fact]
	public void FullyQualified_includes_package_when_present()
	{
		Assert.Equal("`S`.`P`.`PROC`", NormalizedSchema.MustNormalize("s.p.proc", isOracleMode: true).FullyQualified);
	}

	// ---------- 非法输入 ----------

	[Theory]
	[InlineData("")]
	[InlineData("a.b.c.d")]                        // MySQL 模式只允许两段
	public void MustNormalize_rejects_invalid_names(string input)
	{
		Assert.Throws<ArgumentException>(() => NormalizedSchema.MustNormalize(input));
	}

	[Fact]
	public void MustNormalize_rejects_four_part_even_in_oracle_mode()
	{
		Assert.Throws<ArgumentException>(() => NormalizedSchema.MustNormalize("a.b.c.d", isOracleMode: true));
	}

	[Fact]
	public void Constructor_and_MustNormalize_agree()
	{
		var a = NormalizedSchema.MustNormalize("db.table");
		var b = new NormalizedSchema("db.table", isOracleMode: false);
		Assert.Equal(a.Schema, b.Schema);
		Assert.Equal(a.Component, b.Component);
		Assert.Equal(a.FullyQualified, b.FullyQualified);
	}

	[Fact]
	public void Constructor_accepts_explicit_single_part_name()
	{
		// 构造函数可显式传 defaultSchema
		var s = new NormalizedSchema("mytable", isOracleMode: false, defaultSchema: "mydb");
		Assert.Equal("mydb", s.Schema);
		Assert.Equal("mytable", s.Component);
	}
}

/// <summary>
/// 定时器队列测试（internal，无需服务端）。
///
/// <see cref="TimerQueue"/> 是连接空闲超时 / keep-alive 的计时基础设施，
/// 以最小堆按到期时间排序；Add/Remove 的簿记正确性直接影响连接生命周期。
/// </summary>
public sealed class TimerQueueTests
{
	[Fact]
	public void TimerQueue_is_a_singleton()
	{
		Assert.Same(TimerQueue.Instance, TimerQueue.Instance);
	}

	[Fact]
	public void Add_returns_increasing_ids()
	{
		var q = TimerQueue.Instance;
		var id1 = q.Add(60000, static () => { });
		var id2 = q.Add(60000, static () => { });
		try
		{
			Assert.NotEqual(id1, id2);
			Assert.True(id2 > id1, "ID 应单调递增");
		}
		finally
		{
			q.Remove(id1);
			q.Remove(id2);
		}
	}

	[Fact]
	public void Remove_returns_false_for_unknown_id()
	{
		Assert.False(TimerQueue.Instance.Remove(uint.MaxValue));
	}

	[Fact]
	public void Remove_returns_true_for_existing_id()
	{
		var q = TimerQueue.Instance;
		var id = q.Add(60000, static () => { });
		Assert.True(q.Remove(id));
	}

	[Fact]
	public void Remove_is_not_idempotent()
	{
		var q = TimerQueue.Instance;
		var id = q.Add(60000, static () => { });
		Assert.True(q.Remove(id));
		Assert.False(q.Remove(id), "重复移除应返回 false");
	}

	[Fact]
	public void Add_accepts_zero_and_large_delays()
	{
		var q = TimerQueue.Instance;
		var a = q.Add(0, static () => { });
		var b = q.Add(int.MaxValue, static () => { });
		Assert.True(q.Remove(a));
		Assert.True(q.Remove(b));
	}

	[Fact]
	public void Many_timers_can_coexist_and_all_be_removed()
	{
		var q = TimerQueue.Instance;
		var ids = new uint[50];
		try
		{
			for (var i = 0; i < ids.Length; i++)
				ids[i] = q.Add(60000 + i, static () => { });

			foreach (var id in ids)
				Assert.True(q.Remove(id), $"移除 {id} 失败（堆结构可能错乱）");
		}
		finally
		{
			foreach (var id in ids)
				q.Remove(id);
		}
	}
}
