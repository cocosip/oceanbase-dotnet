using System;
using System.Data;
using System.Text;
using OceanBase.Core;
using OceanBase.Protocol.Serialization;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// SQL 解析器与语句拆分测试（internal 类型，无需服务端）。
///
/// <see cref="SqlParser"/> 是一个手写状态机，负责把 SQL 文本拆成多条语句并
/// 识别命名/位置参数；引号、注释、反引号、Oracle q-quote 等边界都靠它保证
/// 不把字符串里的分号当成语句边界。
/// </summary>
public sealed class SqlParserTests
{
	private static string Bind(string sql, MySqlParameterCollection parameters, StatementPreparerOptions options = StatementPreparerOptions.None, ByteBufferWriter? writer = null)
	{
		writer ??= new ByteBufferWriter(sql.Length + 1);
		new StatementPreparer(sql, parameters, options).ParseAndBindParameters(writer);
		return Encoding.UTF8.GetString(writer.ToPayloadData().Span);
	}

	// ---------- SplitStatements ----------

	[Fact]
	public void SplitStatements_splits_on_semicolons()
	{
		var parsed = new StatementPreparer("SELECT 1; SELECT 2", new MySqlParameterCollection(), StatementPreparerOptions.None).SplitStatements();
		Assert.Equal(2, parsed.Statements.Count);
		// 每条语句带 0x16 前缀（预处理语句标记），分号本身不属于语句
		Assert.Equal("SELECT 1", Text(parsed.Statements[0]));
		Assert.Equal("SELECT 2", Text(parsed.Statements[1]));
	}

	[Fact]
	public void SplitStatements_records_named_parameters()
	{
		var parsed = new StatementPreparer("SELECT @a, :b", new MySqlParameterCollection(), StatementPreparerOptions.None).SplitStatements();
		var statement = parsed.Statements[0];
		Assert.Equal(new string?[] { "@a", ":b" }, statement.ParameterNames);
		Assert.Equal(new string?[] { "a", ":b" }, statement.NormalizedParameterNames);
		Assert.All(statement.ParameterIndexes, i => Assert.Equal(-1, i));
		Assert.Equal("SELECT ?, ?", Text(statement));
	}

	[Fact]
	public void SplitStatements_records_positional_parameter_indexes()
	{
		var parsed = new StatementPreparer("SELECT ?, ?", new MySqlParameterCollection(), StatementPreparerOptions.None).SplitStatements();
		var statement = parsed.Statements[0];
		Assert.Equal(new string?[] { null, null }, statement.ParameterNames);
		Assert.Equal(new[] { 0, 1 }, statement.ParameterIndexes);
	}

	[Fact]
	public void SplitStatements_records_parameters_across_statements()
	{
		var parsed = new StatementPreparer("SELECT @a; SELECT ?", new MySqlParameterCollection(), StatementPreparerOptions.None).SplitStatements();
		Assert.Equal(2, parsed.Statements.Count);
		Assert.Equal(new[] { "@a" }, parsed.Statements[0].ParameterNames);
		Assert.Equal(new[] { 0 }, parsed.Statements[1].ParameterIndexes);
	}

	[Theory]
	[InlineData("SELECT 'a;b'")]       // 单引号字符串
	[InlineData("SELECT \"a;b\"")]     // 双引号字符串
	[InlineData("SELECT `a;b`")]       // 反引号标识符
	[InlineData("SELECT 'a;b'; SELECT 2")] // 引号内的分号不拆，引号外的拆
	public void SplitStatements_ignores_semicolons_inside_quotes(string sql)
	{
		var parsed = new StatementPreparer(sql, new MySqlParameterCollection(), StatementPreparerOptions.None).SplitStatements();
		if (sql.EndsWith("SELECT 2", StringComparison.Ordinal))
		{
			Assert.Equal(2, parsed.Statements.Count);
			Assert.Equal("SELECT 'a;b'", Text(parsed.Statements[0]));
		}
		else
		{
			Assert.Single(parsed.Statements);
		}
	}

	[Theory]
	[InlineData("SELECT 1 -- cmt;\n; SELECT 2")]    // 行注释（-- 后带空格），注释内分号不拆
	[InlineData("SELECT 1 # cmt;\n; SELECT 2")]     // # 注释
	[InlineData("SELECT 1 /* cmt; */ ; SELECT 2")]  // 块注释
	public void SplitStatements_ignores_semicolons_in_comments(string sql)
	{
		var parsed = new StatementPreparer(sql, new MySqlParameterCollection(), StatementPreparerOptions.None).SplitStatements();
		Assert.Equal(2, parsed.Statements.Count);
	}

	[Fact]
	public void SplitStatements_escaped_quote_does_not_end_string()
	{
		// 'It''s' 与反斜杠转义都不会提前终止字符串
		var parsed = new StatementPreparer("SELECT 'It''s;ok', `a``b;c`", new MySqlParameterCollection(), StatementPreparerOptions.None).SplitStatements();
		Assert.Single(parsed.Statements);
	}

	[Fact]
	public void SplitStatements_backslash_escaped_quote_continues_string()
	{
		var parsed = new StatementPreparer("SELECT 'a\\';b'", new MySqlParameterCollection(), StatementPreparerOptions.None).SplitStatements();
		Assert.Single(parsed.Statements);
	}

	[Fact]
	public void SplitStatements_oracle_anonymous_block_is_not_split()
	{
		var sql = "BEGIN IF x = 1 THEN NULL; END IF; INSERT INTO t VALUES(1); END";
		var oracle = new StatementPreparer(sql, new OracleParameterCollection(), StatementPreparerOptions.None, isOracleMode: true).SplitStatements();
		Assert.Single(oracle.Statements);

		var mysql = new StatementPreparer(sql, new MySqlParameterCollection(), StatementPreparerOptions.None).SplitStatements();
		Assert.True(mysql.Statements.Count > 1, "MySQL 模式下块内分号应拆分多条语句");
	}

	[Theory]
	[InlineData("  BEGIN NULL; END")]
	[InlineData("-- 前置注释\nBEGIN NULL; END")]
	[InlineData("# 注释\nDECLARE x INT; BEGIN NULL; END")]
	[InlineData("/* 头 */ BEGIN NULL; END")]
	public void SplitStatements_oracle_anonymous_block_after_comments(string sql)
	{
		var parsed = new StatementPreparer(sql, new OracleParameterCollection(), StatementPreparerOptions.None, isOracleMode: true).SplitStatements();
		Assert.Single(parsed.Statements);
	}

	[Fact]
	public void SplitStatements_empty_sql_yields_no_statements()
	{
		var parsed = new StatementPreparer("", new MySqlParameterCollection(), StatementPreparerOptions.None).SplitStatements();
		Assert.Empty(parsed.Statements);
	}

	private static string Text(ParsedStatement statement) => Encoding.UTF8.GetString(statement.StatementBytes.AsSpan(1));

	// ---------- ParseAndBindParameters ----------

	[Fact]
	public void ParseAndBindParameters_substitutes_named_parameters()
	{
		var parameters = new MySqlParameterCollection();
		parameters.AddWithValue("@name", "value");
		parameters.AddWithValue("@id", 42);
		Assert.Equal("SELECT 'value', 42", Bind("SELECT @name, @id", parameters));
	}

	[Fact]
	public void ParseAndBindParameters_substitutes_positional_parameters_in_order()
	{
		var parameters = new MySqlParameterCollection();
		parameters.AddWithValue(null, 1);
		parameters.AddWithValue(null, 2);
		Assert.Equal("SELECT 1, 2", Bind("SELECT ?, ?", parameters));
	}

	[Fact]
	public void ParseAndBindParameters_escapes_string_values()
	{
		var parameters = new MySqlParameterCollection();
		parameters.AddWithValue("@s", "a'b\\c");
		Assert.Equal("SELECT 'a''b\\\\c'", Bind("SELECT @s", parameters));
	}

	[Fact]
	public void ParseAndBindParameters_escapes_differently_with_no_backslash_escapes()
	{
		var parameters = new MySqlParameterCollection();
		parameters.AddWithValue("@s", "a'b\\c");
		Assert.Equal("SELECT 'a''b\\c'", Bind("SELECT @s", parameters, StatementPreparerOptions.NoBackslashEscapes));
	}

	[Fact]
	public void ParseAndBindParameters_null_becomes_null_literal()
	{
		var parameters = new MySqlParameterCollection();
		parameters.AddWithValue("@v", null);
		Assert.Equal("SELECT NULL", Bind("SELECT @v", parameters));
	}

	[Fact]
	public void ParseAndBindParameters_user_variable_requires_option()
	{
		var parameters = new MySqlParameterCollection();
		var exception = Assert.Throws<MySqlException>(() => Bind("SELECT @unknown", parameters));
		Assert.Contains("must be defined", exception.Message);

		// 打开 AllowUserVariables 后原样保留
		Assert.Equal("SELECT @unknown", Bind("SELECT @unknown", parameters, StatementPreparerOptions.AllowUserVariables));
	}

	[Fact]
	public void ParseAndBindParameters_positional_index_out_of_range_throws()
	{
		var parameters = new MySqlParameterCollection();
		parameters.AddWithValue(null, 1);
		var exception = Assert.Throws<MySqlException>(() => Bind("SELECT ?, ?", parameters));
		Assert.Contains("Parameter index 1 is invalid", exception.Message);
	}

	[Fact]
	public void ParseAndBindParameters_output_parameter_requires_option()
	{
		var parameters = new MySqlParameterCollection();
		var p = parameters.Add("@p", MySqlDbType.Int32);
		p.Direction = ParameterDirection.Output;
		Assert.Throws<MySqlException>(() => Bind("SELECT @p", parameters));

		// 打开选项后允许绑定；未赋值的输出参数写成 NULL
		Assert.Equal("SELECT NULL", Bind("SELECT @p", parameters, StatementPreparerOptions.AllowOutputParameters));
	}

	[Fact]
	public void ParseAndBindParameters_unterminated_string_is_incomplete()
	{
		var writer = new ByteBufferWriter(32);
		var complete = new StatementPreparer("SELECT 'abc", new MySqlParameterCollection(), StatementPreparerOptions.None).ParseAndBindParameters(writer);
		Assert.False(complete, "未闭合的字符串应报告语句不完整");

		complete = new StatementPreparer("SELECT 1", new MySqlParameterCollection(), StatementPreparerOptions.None).ParseAndBindParameters(writer);
		Assert.True(complete);
	}

	[Fact]
	public void ParseAndBindParameters_trailing_line_comment_appends_newline()
	{
		var parameters = new MySqlParameterCollection();
		var result = Bind("SELECT 1 -- trailing", parameters);
		Assert.EndsWith("\n", result, StringComparison.Ordinal);
	}

	[Fact]
	public void ParseAndBindParameters_appends_semicolon_when_requested()
	{
		var parameters = new MySqlParameterCollection();
		Assert.Equal("SELECT 1;", Bind("SELECT 1", parameters, StatementPreparerOptions.AppendSemicolon));
		Assert.Equal("SELECT 1", Bind("SELECT 1", parameters));
	}

	[Fact]
	public void ParseAndBindParameters_empty_command_text_binds_nothing()
	{
		var writer = new ByteBufferWriter(4);
		Assert.True(new StatementPreparer("  ", new MySqlParameterCollection(), StatementPreparerOptions.None).ParseAndBindParameters(writer));
		Assert.Equal(0, writer.Position);
	}

	// ---------- Oracle 模式差异 ----------

	[Fact]
	public void ParseAndBindParameters_oracle_q_quote_string_with_apostrophe()
	{
		// q'#it;'s#'：Oracle q-quote 分隔符内的单引号和分号属于字符串字面量
		const string sql = "SELECT q'#it;'s#'; SELECT 2";

		var oracle = new StatementPreparer(sql, new OracleParameterCollection(), StatementPreparerOptions.None, isOracleMode: true).SplitStatements();
		Assert.Equal(2, oracle.Statements.Count);

		// MySQL 模式没有 q-quote 语法：# 把后半段吞成行注释，只剩一条语句
		var mysql = new StatementPreparer(sql, new MySqlParameterCollection(), StatementPreparerOptions.None).SplitStatements();
		Assert.Single(mysql.Statements);
	}

	[Fact]
	public void ParseAndBindParameters_oracle_named_parameter_uses_colon_prefix()
	{
		var parameters = new OracleParameterCollection();
		var p = parameters.Add(":name", OracleDbType.Varchar2);
		p.Value = "v";
		var writer = new ByteBufferWriter(32);
		Assert.True(new StatementPreparer("SELECT :name FROM dual", parameters, StatementPreparerOptions.None, isOracleMode: true).ParseAndBindParameters(writer));
		Assert.Equal("SELECT 'v' FROM dual", Encoding.UTF8.GetString(writer.ToPayloadData().Span));
	}
}
