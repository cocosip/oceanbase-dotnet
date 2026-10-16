using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text;
using OceanBase.Protocol.Payloads;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 公共外围类型的离线行为测试（无需服务端）。
///
/// 覆盖简单命令载荷（PING/QUIT/RESET/INIT DB）、ADO.NET 事件参数类、
/// <see cref="MySqlBulkCopy"/> 与 <see cref="MySqlBulkLoader"/> 的构造与属性面、
/// 以及 <see cref="ConnectionOpenedContext"/>。
/// </summary>
public sealed class OfflineSurfaceTests
{
	private const BindingFlags NonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;

	// ---------- 简单命令载荷 ----------

	[Fact]
	public void Ping_quit_and_reset_payloads_use_command_words()
	{
		Assert.Equal(new byte[] { 14 }, PingPayload.Instance.Span.ToArray());
		Assert.Equal(new byte[] { 1 }, QuitPayload.Instance.Span.ToArray());
		Assert.Equal(new byte[] { 31 }, ResetConnectionPayload.Instance.Span.ToArray());
		Assert.Empty(EmptyPayload.Instance.Span.ToArray());
	}

	[Fact]
	public void InitDatabasePayload_writes_command_word_and_name()
	{
		var payload = InitDatabasePayload.Create("appdb");

		Assert.Equal(new byte[] { 2, (byte)'a', (byte)'p', (byte)'p', (byte)'d', (byte)'b' }, payload.Span.ToArray());
	}

	// ---------- 事件参数类 ----------

	[Fact]
	public void RowsCopiedEventArgs_tracks_rows_and_abort()
	{
		var constructor = typeof(MySqlRowsCopiedEventArgs).GetConstructor(NonPublicInstance, null, Type.EmptyTypes, null)!;
		var eventArgs = (MySqlRowsCopiedEventArgs)constructor.Invoke(Array.Empty<object>());

		Assert.False(eventArgs.Abort);
		Assert.Equal(0, eventArgs.RowsCopied);

		eventArgs.Abort = true;

		// RowsCopied 由驱动内部更新
		typeof(MySqlRowsCopiedEventArgs).GetProperty("RowsCopied")!.SetValue(eventArgs, 42L);
		Assert.True(eventArgs.Abort);
		Assert.Equal(42L, eventArgs.RowsCopied);
	}

	[Fact]
	public void InfoMessageEventArgs_carries_errors()
	{
		var errorConstructor = typeof(MySqlError).GetConstructor(NonPublicInstance, null, new[] { typeof(string), typeof(int), typeof(string) }, null)!;
		var error = (MySqlError)errorConstructor.Invoke(new object[] { "Warning", 1050, "table exists" });

		var constructor = typeof(MySqlInfoMessageEventArgs).GetConstructor(NonPublicInstance, null, new[] { typeof(IReadOnlyList<MySqlError>) }, null)!;
		var eventArgs = (MySqlInfoMessageEventArgs)constructor.Invoke(new object[] { new[] { error } });

		Assert.Same(error, Assert.Single(eventArgs.Errors));
	}

	private static (DataRow Row, DataTableMapping Mapping) CreateRowAndMapping()
	{
		var table = new DataTable();
		table.Columns.Add("Id", typeof(int));
		table.Rows.Add(1);
		return (table.Rows[0], new DataTableMapping("Table", "t"));
	}

	[Fact]
	public void MySqlRowUpdatedEventArgs_exposes_typed_command()
	{
		var (row, mapping) = CreateRowAndMapping();
		using var command = new MySqlCommand("UPDATE t SET a = 1");

		var eventArgs = new MySqlRowUpdatedEventArgs(row, command, StatementType.Update, mapping);

		Assert.Same(command, eventArgs.Command);
		Assert.Equal(StatementType.Update, eventArgs.StatementType);
	}

	[Fact]
	public void OracleRowUpdatedEventArgs_exposes_typed_command()
	{
		var (row, mapping) = CreateRowAndMapping();
		using var command = new OracleCommand("UPDATE t SET a = 1");

		var eventArgs = new OracleRowUpdatedEventArgs(row, command, StatementType.Update, mapping);

		Assert.Same(command, eventArgs.Command);
	}

	[Fact]
	public void MySqlRowUpdatingEventArgs_carries_row_and_command()
	{
		var (row, mapping) = CreateRowAndMapping();
		using var command = new MySqlCommand("DELETE FROM t WHERE id = 1");

		var eventArgs = new MySqlRowUpdatingEventArgs(row, command, StatementType.Delete, mapping);

		Assert.Same(command, ((DbCommand)eventArgs.Command!));
		Assert.Equal(StatementType.Delete, eventArgs.StatementType);
	}

	[Fact]
	public void OracleRowUpdatingEventArgs_carries_row_and_command()
	{
		var (row, mapping) = CreateRowAndMapping();
		using var command = new OracleCommand("DELETE FROM t WHERE id = 1");

		var eventArgs = new OracleRowUpdatingEventArgs(row, command, StatementType.Delete, mapping);

		Assert.Same(command, ((DbCommand)eventArgs.Command!));
		Assert.Equal(StatementType.Delete, eventArgs.StatementType);
	}

	// ---------- ConnectionOpenedContext ----------

	[Fact]
	public void ConnectionOpenedContext_carries_connection_and_conditions()
	{
		using var connection = new MySqlConnection("Server=h");
		var constructor = typeof(ConnectionOpenedContext).GetConstructor(
			NonPublicInstance, null, new[] { typeof(DbConnection), typeof(ConnectionOpenedConditions) }, null)!;
		var context = (ConnectionOpenedContext)constructor.Invoke(new object[] { connection, ConnectionOpenedConditions.Reset });

		Assert.Same(connection, context.Connection);
		Assert.Equal(ConnectionOpenedConditions.Reset, context.Conditions);
	}

	// ---------- MySqlBulkCopy 构造与离线校验 ----------

	[Fact]
	public void BulkCopy_constructor_initializes_mappings_and_rejects_null()
	{
		using var connection = new MySqlConnection("Server=h");

		var bulkCopy = new MySqlBulkCopy(connection);
		Assert.Empty(bulkCopy.ColumnMappings);
		Assert.Equal(0, bulkCopy.RowsCopied);
		Assert.Equal(0, bulkCopy.BulkCopyTimeout);
		Assert.Equal(MySqlBulkLoaderConflictOption.None, bulkCopy.ConflictOption);
		bulkCopy.DestinationTableName = "t";
		bulkCopy.BulkCopyTimeout = 30;
		bulkCopy.NotifyAfter = 100;
		bulkCopy.ConflictOption = MySqlBulkLoaderConflictOption.Replace;
		Assert.Equal("t", bulkCopy.DestinationTableName);
		Assert.Equal(30, bulkCopy.BulkCopyTimeout);
		Assert.Equal(100, bulkCopy.NotifyAfter);
		Assert.Equal(MySqlBulkLoaderConflictOption.Replace, bulkCopy.ConflictOption);

		Assert.Throws<ArgumentNullException>(() => new MySqlBulkCopy(null!));
	}

	[Fact]
	public void BulkCopy_writetoserver_validates_arguments_before_contacting_server()
	{
		using var connection = new MySqlConnection("Server=h");
		var bulkCopy = new MySqlBulkCopy(connection);

		Assert.Throws<ArgumentNullException>(() => bulkCopy.WriteToServer((DataTable)null!));

		// 未设置目标表名 → 在触碰网络前就失败
		var table = new DataTable();
		table.Columns.Add("Id", typeof(int));
		Assert.Throws<InvalidOperationException>(() => bulkCopy.WriteToServer(table));
	}

	[Fact]
	public void BulkCopyResult_carries_warnings_and_row_count()
	{
		var errorConstructor = typeof(MySqlError).GetConstructor(NonPublicInstance, null, new[] { typeof(string), typeof(int), typeof(string) }, null)!;
		var error = (MySqlError)errorConstructor.Invoke(new object[] { "Note", 1592, "unsafe statement" });

		var constructor = typeof(MySqlBulkCopyResult).GetConstructor(
			NonPublicInstance, null, new[] { typeof(IReadOnlyList<MySqlError>), typeof(int) }, null)!;
		var result = (MySqlBulkCopyResult)constructor.Invoke(new object[] { new[] { error }, 7 });

		Assert.Same(error, Assert.Single(result.Warnings));
		Assert.Equal(7, result.RowsInserted);
	}

	// ---------- MySqlBulkLoader 属性面 ----------

	[Fact]
	public void BulkLoader_initializes_collections_and_exposes_properties()
	{
		using var connection = new MySqlConnection("Server=h");
		var loader = new MySqlBulkLoader(connection);

		Assert.Same(connection, loader.Connection);
		Assert.Empty(loader.Columns);
		Assert.Empty(loader.Expressions);
		Assert.True(loader.Local);   // 文档注明默认 true
		Assert.Equal(0, loader.NumberOfLinesToSkip);
		Assert.Equal(0, loader.Timeout);

		loader.Local = false;
		loader.NumberOfLinesToSkip = 1;
		loader.Priority = MySqlBulkLoaderPriority.Low;
		loader.ConflictOption = MySqlBulkLoaderConflictOption.Ignore;
		loader.CharacterSet = "utf8mb4";
		loader.FieldTerminator = ",";
		loader.LineTerminator = "\n";
		loader.TableName = "t";
		loader.FileName = "/tmp/data.csv";
		loader.Columns.Add("a");
		loader.Expressions.Add("b + 1");

		Assert.False(loader.Local);
		Assert.Equal(1, loader.NumberOfLinesToSkip);
		Assert.Equal(MySqlBulkLoaderPriority.Low, loader.Priority);
		Assert.Equal(MySqlBulkLoaderConflictOption.Ignore, loader.ConflictOption);
		Assert.Equal("utf8mb4", loader.CharacterSet);
		Assert.Equal(",", loader.FieldTerminator);
		Assert.Equal("\n", loader.LineTerminator);
		Assert.Equal("t", loader.TableName);
		Assert.Equal("/tmp/data.csv", loader.FileName);
		Assert.Equal(new[] { "a" }, loader.Columns);
		Assert.Equal(new[] { "b + 1" }, loader.Expressions);
	}
}
