using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OceanBase.Core;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 批处理 API（<see cref="MySqlBatch"/> / <see cref="MySqlBatchCommand"/> /
/// <see cref="MySqlBatchCommandCollection"/>）的离线行为测试。
///
/// 批处理是 ADO.NET 的实验性接口，其校验顺序与错误信息属于可观测契约；
/// 这里不连库，只驱动参数校验与集合操作路径。
/// </summary>
public sealed class BatchTests
{
	// ---------- 构造与属性 ----------

	[Fact]
	public void MySqlBatch_defaults_are_stable()
	{
		using var batch = new MySqlBatch();
		Assert.Null(batch.Connection);
		Assert.Null(batch.Transaction);
		Assert.NotNull(batch.BatchCommands);
		Assert.Equal(0, batch.BatchCommands.Count);
		Assert.Equal(0, batch.Timeout);
	}

	[Fact]
	public void MySqlBatch_ctor_binds_connection()
	{
		using var connection = new MySqlConnection("Server=h;");
		using var batch = new MySqlBatch(connection);
		Assert.Same(connection, batch.Connection);
	}

	[Fact]
	public void MySqlBatch_DbConnection_roundtrips()
	{
		// DbConnection 是 protected 成员，用反射验证其读写与 Connection 属性联动
		using var connection = new MySqlConnection("Server=h;");
		using var batch = new MySqlBatch();
		var property = typeof(DbBatch).GetProperty("DbConnection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
		property.SetValue(batch, connection);
		Assert.Same(connection, property.GetValue(batch));
		Assert.Same(connection, batch.Connection);
	}

	[Fact]
	public void MySqlBatch_Timeout_setter_clears_effective_timeout()
	{
		using var batch = new MySqlBatch();
		var cancellable = (ICancellableCommand)batch;
		cancellable.EffectiveCommandTimeout = 123;

		batch.Timeout = 30;
		Assert.Equal(30, batch.Timeout);
		// 显式设置 Timeout 会清掉缓存的生效超时
		Assert.Null(cancellable.EffectiveCommandTimeout);
	}

	[Fact]
	public void MySqlBatch_CreateBatchCommand_returns_mysql_command()
	{
		using var batch = new MySqlBatch();
		var command = batch.CreateBatchCommand();
		Assert.IsType<MySqlBatchCommand>(command);
	}

	[Fact]
	public void MySqlBatch_Cancel_without_connection_is_safe()
	{
		using var batch = new MySqlBatch();
		batch.Cancel();
	}

	[Fact]
	public void MySqlBatch_IsTimedOut_defaults_to_false()
	{
		using var batch = new MySqlBatch();
		Assert.False(((ICancellableCommand)batch).IsTimedOut);
	}

	[Fact]
	public void MySqlBatch_Session_is_not_implemented()
	{
		// ICancellableCommand.Session 在批处理上未实现（批处理按命令逐个执行）
		using var batch = new MySqlBatch();
		Assert.Throws<NotImplementedException>(() => ((ICancellableCommand)batch).Session);
	}

	[Fact]
	public void MySqlBatch_RegisterCancel_with_non_cancellable_token_is_noop()
	{
		using var batch = new MySqlBatch();
		// 不可取消的 token 直接返回默认注册，不注册任何回调
		var registration = ((ICancellableCommand)batch).RegisterCancel(CancellationToken.None);
		Assert.Equal(default(CancellationTokenRegistration), registration);
	}

	[Fact]
	public void MySqlBatch_RegisterCancel_with_cancellable_token_returns_registration()
	{
		using var batch = new MySqlBatch();
		using var source = new CancellationTokenSource();
		var registration = ((ICancellableCommand)batch).RegisterCancel(source.Token);
		Assert.NotEqual(default(CancellationTokenRegistration), registration);
		// 触发取消：批处理会调用 Cancel（无连接时为空操作，不应抛错）
		source.Cancel();
		registration.Dispose();
	}

	// ---------- 校验顺序 ----------
	//
	// 重要（忠实复刻的上游行为）：MySqlBatch.Execute* 会先调用
	// ICancellableCommandExtensions.ResetCommandTimeout，而后者在
	// EffectiveCommandTimeout != int.MaxValue 时会读取 ICancellableCommand.Session ——
	// MySqlBatch 未实现 Session（抛 NotImplementedException），因此**未连库**时
	// Execute* 抛的是 NotImplementedException，而不是参数校验异常。
	// 把 EffectiveCommandTimeout 预置为 int.MaxValue 可跳过该分支，从而验证真正的参数校验。

	private static MySqlBatch CreateBatchWithTimeoutShortCircuited(MySqlConnection? connection)
	{
		var batch = new MySqlBatch(connection);
		((ICancellableCommand)batch).EffectiveCommandTimeout = int.MaxValue;
		return batch;
	}

	[Fact]
	public void MySqlBatch_execute_without_connection_throws_NotImplemented_from_Session()
	{
		using var batch = new MySqlBatch();
		Assert.Throws<NotImplementedException>(() => batch.ExecuteNonQuery());
	}

	[Fact]
	public void MySqlBatch_execute_after_dispose_throws_NotImplemented_from_Session()
	{
		using var connection = new MySqlConnection("Server=h;");
		var batch = new MySqlBatch(connection);
		batch.Dispose();
		Assert.Throws<NotImplementedException>(() => batch.ExecuteNonQuery());
	}

	[Fact]
	public void MySqlBatch_validation_reports_missing_connection()
	{
		// 跳过 Session 分支后，暴露真正的参数校验：连接为 null
		using var batch = CreateBatchWithTimeoutShortCircuited(null);
		var ex = Assert.Throws<InvalidOperationException>(() => batch.ExecuteNonQuery());
		Assert.Contains("Connection property must be non-null", ex.Message);
	}

	[Fact]
	public void MySqlBatch_validation_reports_disposed_batch()
	{
		using var connection = new MySqlConnection("Server=h;");
		var batch = CreateBatchWithTimeoutShortCircuited(connection);
		batch.Dispose();
		Assert.Throws<ObjectDisposedException>(() => batch.ExecuteNonQuery());
	}

	[Fact]
	public void MySqlBatch_validation_reports_closed_connection()
	{
		using var connection = new MySqlConnection("Server=h;");
		using var batch = CreateBatchWithTimeoutShortCircuited(connection);
		var ex = Assert.Throws<InvalidOperationException>(() => batch.ExecuteNonQuery());
		Assert.Contains("Connection must be Open", ex.Message);
	}

	[Fact]
	public void MySqlBatch_Prepare_without_connection_throws()
	{
		// Prepare 不走 ResetCommandTimeout，直接命中 NeedsPrepare 的校验
		using var batch = new MySqlBatch();
		Assert.Throws<InvalidOperationException>(() => batch.Prepare());
	}

	[Fact]
	public void MySqlBatch_Prepare_after_dispose_throws_ObjectDisposed()
	{
		using var connection = new MySqlConnection("Server=h;");
		var batch = new MySqlBatch(connection);
		batch.Dispose();
		Assert.Throws<ObjectDisposedException>(() => batch.Prepare());
	}

	[Fact]
	public void MySqlBatch_Prepare_with_closed_connection_throws()
	{
		using var connection = new MySqlConnection("Server=h;");
		using var batch = new MySqlBatch(connection);
		var ex = Assert.Throws<InvalidOperationException>(() => batch.Prepare());
		Assert.Contains("Connection must be Open", ex.Message);
	}

	[Fact]
	public void MySqlBatch_ExecuteReader_without_connection_throws_NotImplemented()
	{
		using var batch = new MySqlBatch();
		Assert.Throws<NotImplementedException>(() => batch.ExecuteReader());
	}

	[Fact]
	public void MySqlBatch_ExecuteScalar_without_connection_throws_NotImplemented()
	{
		using var batch = new MySqlBatch();
		Assert.Throws<NotImplementedException>(() => batch.ExecuteScalar());
	}

	[Fact]
	public void MySqlBatch_CreateDbBatchCommand_via_base_returns_expected_type()
	{
		using var batch = new MySqlBatch();
		var command = ((DbBatch)batch).CreateBatchCommand();
		Assert.IsType<MySqlBatchCommand>(command);
	}

	// ---------- MySqlBatchCommand ----------

	[Fact]
	public void MySqlBatchCommand_defaults_are_stable()
	{
		var command = new MySqlBatchCommand();
		Assert.Equal("", command.CommandText);
		Assert.Equal(CommandType.Text, command.CommandType);
		Assert.Equal(0, command.RecordsAffected);
		Assert.True(command.CanCreateParameter);
	}

	[Fact]
	public void MySqlBatchCommand_ctor_sets_text()
	{
		var command = new MySqlBatchCommand("SELECT 1");
		Assert.Equal("SELECT 1", command.CommandText);
	}

	[Fact]
	public void MySqlBatchCommand_null_text_becomes_empty()
	{
		var command = new MySqlBatchCommand(null);
		Assert.Equal("", command.CommandText);
	}

	[Fact]
	public void MySqlBatchCommand_CreateParameter_returns_MySqlParameter()
	{
		var command = new MySqlBatchCommand();
		Assert.IsType<MySqlParameter>(command.CreateParameter());
	}

	[Fact]
	public void MySqlBatchCommand_Parameters_lazily_created_and_cached()
	{
		var command = new MySqlBatchCommand();
		var first = command.Parameters;
		Assert.NotNull(first);
		Assert.Same(first, command.Parameters);
		Assert.Equal(0, first.Count);
	}

	[Fact]
	public void MySqlBatchCommand_parameters_can_be_added()
	{
		var command = new MySqlBatchCommand("INSERT INTO t VALUES(@p)");
		command.Parameters.Add(new MySqlParameter("@p", 1));
		Assert.Equal(1, command.Parameters.Count);
	}

	[Fact]
	public void MySqlBatchCommand_DbParameterCollection_matches_typed_collection()
	{
		// DbParameterCollection 是 protected 成员
		var command = new MySqlBatchCommand();
		var property = typeof(DbBatchCommand).GetProperty("DbParameterCollection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
		Assert.Same(command.Parameters, property.GetValue(command));
	}

	[Fact]
	public void MySqlBatchCommand_interfaces_report_expected_values()
	{
		var command = new MySqlBatchCommand();
		var mysqlCommand = (IMySqlCommand)command;
		Assert.False(((ICommand)command).AllowUserVariables);
		Assert.Null(mysqlCommand.RawParameters);
		Assert.Null(((ICommand)command).RawAttributes);
		Assert.Null(mysqlCommand.Connection);
		Assert.Equal(0L, ((ICommand)command).LastInsertedId);
		Assert.Null(((ICommand)command).TryGetPreparedStatements());
	}

	[Fact]
	public void MySqlBatchCommand_SetLastInsertedId_is_reflected()
	{
		var command = new MySqlBatchCommand();
		((ICommand)command).SetLastInsertedId(42);
		Assert.Equal(42L, ((ICommand)command).LastInsertedId);
	}

	[Fact]
	public void MySqlBatchCommand_OutParameters_roundtrips_through_interfaces()
	{
		var command = new MySqlBatchCommand();
		var mysqlCommand = (IMySqlCommand)command;
		Assert.Null(mysqlCommand.OutParameters);

		var parameters = new MySqlParameterCollection();
		((ICommand)command).OutParameters = parameters;
		Assert.Same(parameters, mysqlCommand.OutParameters);
	}

	[Fact]
	public void MySqlBatchCommand_ReturnParameter_roundtrips_through_interfaces()
	{
		var command = new MySqlBatchCommand();
		var mysqlCommand = (IMySqlCommand)command;
		Assert.Null(mysqlCommand.ReturnParameter);

		var parameter = new MySqlParameter("@ret", 1);
		((ICommand)command).ReturnParameter = parameter;
		Assert.Same(parameter, mysqlCommand.ReturnParameter);
	}

	// ---------- MySqlBatchCommandCollection ----------

	private static MySqlBatchCommandCollection CreateCollection()
	{
		using var batch = new MySqlBatch();
		return batch.BatchCommands;
	}

	[Fact]
	public void BatchCommandCollection_starts_empty_and_writable()
	{
		var collection = CreateCollection();
		Assert.Equal(0, collection.Count);
		Assert.False(collection.IsReadOnly);
	}

	[Fact]
	public void BatchCommandCollection_Add_and_indexer()
	{
		var collection = CreateCollection();
		var command = new MySqlBatchCommand("SELECT 1");
		collection.Add(command);
		Assert.Equal(1, collection.Count);
		Assert.Same(command, collection[0]);
	}

	[Fact]
	public void BatchCommandCollection_Add_wrong_type_throws()
	{
		var collection = CreateCollection();
		Assert.Throws<InvalidCastException>(() => collection.Add(new OtherBatchCommand()));
	}

	[Fact]
	public void BatchCommandCollection_Insert_and_RemoveAt()
	{
		var collection = CreateCollection();
		var a = new MySqlBatchCommand("A");
		var b = new MySqlBatchCommand("B");
		collection.Add(a);
		collection.Insert(0, b);
		Assert.Same(b, collection[0]);
		Assert.Same(a, collection[1]);

		collection.RemoveAt(0);
		Assert.Equal(1, collection.Count);
		Assert.Same(a, collection[0]);
	}

	[Fact]
	public void BatchCommandCollection_Contains_and_IndexOf()
	{
		var collection = CreateCollection();
		var command = new MySqlBatchCommand("A");
		collection.Add(command);
		Assert.True(collection.Contains(command));
		Assert.Equal(0, collection.IndexOf(command));

		var other = new MySqlBatchCommand("B");
		Assert.False(collection.Contains(other));
		Assert.Equal(-1, collection.IndexOf(other));
	}

	[Fact]
	public void BatchCommandCollection_Remove_returns_whether_removed()
	{
		var collection = CreateCollection();
		var command = new MySqlBatchCommand("A");
		collection.Add(command);
		Assert.True(collection.Remove(command));
		Assert.False(collection.Remove(command));
		Assert.Equal(0, collection.Count);
	}

	[Fact]
	public void BatchCommandCollection_Clear_empties()
	{
		var collection = CreateCollection();
		collection.Add(new MySqlBatchCommand("A"));
		collection.Add(new MySqlBatchCommand("B"));
		collection.Clear();
		Assert.Equal(0, collection.Count);
	}

	[Fact]
	public void BatchCommandCollection_indexer_set_replaces()
	{
		var collection = CreateCollection();
		collection.Add(new MySqlBatchCommand("A"));
		var replacement = new MySqlBatchCommand("B");
		collection[0] = replacement;
		Assert.Same(replacement, collection[0]);
	}

	[Fact]
	public void BatchCommandCollection_GetEnumerator_yields_commands()
	{
		var collection = CreateCollection();
		collection.Add(new MySqlBatchCommand("A"));
		collection.Add(new MySqlBatchCommand("B"));
		Assert.Equal(new[] { "A", "B" }, collection.Select(c => c.CommandText).ToArray());
	}

	[Fact]
	public void BatchCommandCollection_CopyTo_is_not_implemented()
	{
		var collection = CreateCollection();
		Assert.Throws<NotImplementedException>(() => collection.CopyTo(new DbBatchCommand[1], 0));
	}

	/// <summary>非 MySql 的 DbBatchCommand，用于验证集合的强制类型转换。</summary>
	private sealed class OtherBatchCommand : DbBatchCommand
	{
		public override string CommandText { get; set; } = "";

		public override CommandType CommandType { get; set; }

		public override int RecordsAffected => 0;

		protected override DbParameterCollection DbParameterCollection => throw new NotImplementedException();
	}
}

/// <summary>
/// 事务类型（<see cref="MySqlTransaction"/> / <see cref="OracleTransaction"/>）的离线契约测试。
/// 事务的实际提交/回滚需要服务端，这里只验证未开启事务时的校验与释放语义。
/// </summary>
public sealed class TransactionTests
{
	[Fact]
	public void MySqlTransaction_derives_from_DbTransaction()
	{
		Assert.True(typeof(DbTransaction).IsAssignableFrom(typeof(MySqlTransaction)));
		Assert.True(typeof(DbTransaction).IsAssignableFrom(typeof(OracleTransaction)));
	}

	[Fact]
	public void MySqlTransaction_exposes_connection_and_isolation_level()
	{
		// Connection 用 DeclaredOnly 取本类型声明的那个（否则与继承的 DbTransaction.Connection 歧义）
		const System.Reflection.BindingFlags Declared = System.Reflection.BindingFlags.Public
			| System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly;

		var mysqlConnection = typeof(MySqlTransaction).GetProperty("Connection", Declared);
		Assert.NotNull(mysqlConnection);
		Assert.Equal(typeof(MySqlConnection), mysqlConnection!.PropertyType);

		var oracleConnection = typeof(OracleTransaction).GetProperty("Connection", Declared);
		Assert.NotNull(oracleConnection);
		Assert.Equal(typeof(OracleConnection), oracleConnection!.PropertyType);

		Assert.NotNull(typeof(MySqlTransaction).GetProperty("IsolationLevel"));
		Assert.NotNull(typeof(OracleTransaction).GetProperty("IsolationLevel"));
	}

	[Fact]
	public void MySqlTransaction_types_are_sealed()
	{
		Assert.True(typeof(MySqlTransaction).IsSealed);
		Assert.True(typeof(OracleTransaction).IsSealed);
	}

	[Fact]
	public void MySqlTransaction_supports_savepoint_api()
	{
		// Release/Rollback(name)/Save 及其异步版本属于公开契约
		var type = typeof(MySqlTransaction);
		Assert.NotNull(type.GetMethod("Save", new[] { typeof(string) }));
		Assert.NotNull(type.GetMethod("Release", new[] { typeof(string) }));
		Assert.NotNull(type.GetMethod("Rollback", new[] { typeof(string) }));
		Assert.NotNull(type.GetMethod("SaveAsync", new[] { typeof(string), typeof(CancellationToken) }));
		Assert.NotNull(type.GetMethod("ReleaseAsync", new[] { typeof(string), typeof(CancellationToken) }));
		Assert.NotNull(type.GetMethod("RollbackAsync", new[] { typeof(string), typeof(CancellationToken) }));
	}
}

/// <summary>
/// 零散公开类型的契约测试（无法离线触发其运行时行为，但类型形状属公开 API）。
/// </summary>
public sealed class MiscSurfaceTests
{
	[Fact]
	public void ConnectionOpenedContext_exposes_connection_and_conditions()
	{
		Assert.NotNull(typeof(ConnectionOpenedContext).GetProperty("Connection"));
		Assert.NotNull(typeof(ConnectionOpenedContext).GetProperty("Conditions"));
		Assert.Equal(typeof(DbConnection), typeof(ConnectionOpenedContext).GetProperty("Connection")!.PropertyType);
	}

	[Fact]
	public void ConnectionOpenedConditions_is_a_flags_enum()
	{
		Assert.True(typeof(ConnectionOpenedConditions).IsEnum);
		Assert.NotNull(typeof(ConnectionOpenedConditions).GetCustomAttributes(typeof(FlagsAttribute), false).FirstOrDefault());
	}

	[Fact]
	public void EventArgs_types_expose_expected_members()
	{
		Assert.NotNull(typeof(MySqlRowsCopiedEventArgs).GetProperty("RowsCopied"));
		Assert.NotNull(typeof(MySqlRowsCopiedEventArgs).GetProperty("Abort"));
		Assert.NotNull(typeof(MySqlInfoMessageEventArgs).GetProperty("Errors"));
	}

	[Fact]
	public void ProvidePasswordContext_exposes_expected_members()
	{
		// 注意：该类型**没有** Password 属性 —— 密码是由回调返回的，
		// 上下文只提供定位连接所需的信息。
		var type = typeof(MySqlProvidePasswordContext);
		Assert.NotNull(type.GetProperty("Server"));
		Assert.NotNull(type.GetProperty("Port"));
		Assert.NotNull(type.GetProperty("UserId"));
		Assert.NotNull(type.GetProperty("Database"));
		Assert.Null(type.GetProperty("Password"));
	}

	[Fact]
	public void MySqlBulkCopyResult_exposes_expected_members()
	{
		Assert.NotNull(typeof(MySqlBulkCopyResult).GetProperty("RowsInserted"));
		Assert.NotNull(typeof(MySqlBulkCopyResult).GetProperty("Warnings"));
	}

	[Fact]
	public void MySqlError_exposes_expected_members()
	{
		Assert.NotNull(typeof(MySqlError).GetProperty("Level"));
		Assert.NotNull(typeof(MySqlError).GetProperty("Code"));
		Assert.NotNull(typeof(MySqlError).GetProperty("ErrorCode"));
		Assert.NotNull(typeof(MySqlError).GetProperty("Message"));
	}
}

/// <summary>
/// <see cref="MySqlHelper"/> 的离线行为测试。
/// </summary>
public sealed class MySqlHelperTests
{
	[Theory]
	[InlineData("plain", "plain")]
	[InlineData("it's", "it\\'s")]
	[InlineData("say \"hi\"", "say \\\"hi\\\"")]
	[InlineData("back\\slash", "back\\\\slash")]
	[InlineData("a'b\"c\\d", "a\\'b\\\"c\\\\d")]
	[InlineData("", "")]
	public void EscapeString_escapes_quotes_and_backslashes(string input, string expected)
	{
		Assert.Equal(expected, MySqlHelper.EscapeString(input));
	}

	[Fact]
	public void EscapeString_returns_same_instance_when_nothing_to_escape()
	{
		// 无特殊字符时不分配新字符串（直接返回入参）
		var value = "nothing special";
		Assert.Same(value, MySqlHelper.EscapeString(value));
	}

	[Fact]
	public void EscapeString_handles_multiple_escapes()
	{
		Assert.Equal("\\'\\'\\'", MySqlHelper.EscapeString("'''"));
	}

	[Fact]
	public void EscapeString_rejects_null()
	{
		Assert.Throws<ArgumentNullException>(() => MySqlHelper.EscapeString(null!));
	}

	[Fact]
	public void ClearConnectionPools_is_callable()
	{
		// 已废弃但仍可用：无连接池时应当是空操作
#pragma warning disable CS0618
		MySqlHelper.ClearConnectionPools();
#pragma warning restore CS0618
		Assert.True(true);
	}
}
