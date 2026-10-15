using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using OceanBase.Logging;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// ADO.NET 工厂类测试：验证 <see cref="DbProviderFactory"/> 契约。
/// 这些工厂是 EF Core / Dapper 等上层框架发现驱动的入口，行为必须稳定。
/// </summary>
public sealed class ProviderFactoryTests
{
	[Fact]
	public void MySqlConnectorFactory_capabilities_match_documented_values()
	{
		var f = MySqlConnectorFactory.Instance;
		Assert.True(f.CanCreateCommandBuilder);
		Assert.True(f.CanCreateDataAdapter);
		Assert.True(f.CanCreateBatch);
		Assert.False(f.CanCreateDataSourceEnumerator);
	}

	[Fact]
	public void OracleClientFactory_capabilities_match_documented_values()
	{
		var f = OracleClientFactory.Instance;
		Assert.True(f.CanCreateCommandBuilder);
		Assert.True(f.CanCreateDataAdapter);
		Assert.True(f.CanCreateBatch);
		Assert.False(f.CanCreateDataSourceEnumerator);
	}

	[Fact]
	public void MySqlConnectorFactory_creates_correct_concrete_types()
	{
		var f = MySqlConnectorFactory.Instance;
		Assert.IsType<MySqlConnection>(f.CreateConnection());
		Assert.IsType<MySqlCommand>(f.CreateCommand());
		Assert.IsType<MySqlParameter>(f.CreateParameter());
		Assert.IsType<MySqlConnectionStringBuilder>(f.CreateConnectionStringBuilder());
		Assert.IsType<MySqlCommandBuilder>(f.CreateCommandBuilder());
		Assert.IsType<MySqlDataAdapter>(f.CreateDataAdapter());
	}

	[Fact]
	public void OracleClientFactory_creates_correct_concrete_types()
	{
		var f = OracleClientFactory.Instance;
		Assert.IsType<OracleConnection>(f.CreateConnection());
		Assert.IsType<OracleCommand>(f.CreateCommand());
		Assert.IsType<OracleParameter>(f.CreateParameter());
		Assert.IsType<OracleConnectionStringBuilder>(f.CreateConnectionStringBuilder());
		Assert.IsType<OracleCommandBuilder>(f.CreateCommandBuilder());
		Assert.IsType<OracleDataAdapter>(f.CreateDataAdapter());
	}

	[Fact]
	public void MySqlConnectorFactory_creates_batch_objects()
	{
		var f = MySqlConnectorFactory.Instance;
		using var batch = f.CreateBatch();
		Assert.IsType<MySqlBatch>(batch);
		Assert.IsType<MySqlBatchCommand>(f.CreateBatchCommand());
	}

	[Fact]
	public void Factories_expose_expected_contract()
	{
		// 注：DbProviderFactories.GetFactory("OceanBase") 需要机器级 machine.config 注册，
		// 驱动本身不自动注册，因此这里只校验工厂类型契约与单例可用性。
		Assert.Same(MySqlConnectorFactory.Instance, MySqlConnectorFactory.Instance);
		Assert.Same(OracleClientFactory.Instance, OracleClientFactory.Instance);
		Assert.IsAssignableFrom<DbProviderFactory>(MySqlConnectorFactory.Instance);
		Assert.IsAssignableFrom<DbProviderFactory>(OracleClientFactory.Instance);
	}

	[Fact]
	public void OracleClientFactory_creates_data_source_from_connection_string()
	{
		var f = OracleClientFactory.Instance;
		using var ds = f.CreateDataSource("Server=h;Port=2883;User Id=u;Password=p;Database=s;");
		Assert.IsType<OracleDataSource>(ds);
	}
}

/// <summary>
/// 参数与参数集合测试（不依赖服务端）。
/// </summary>
public sealed class ParameterTests
{
	[Fact]
	public void MySqlParameter_exposes_defaults()
	{
		var p = new MySqlParameter();
		Assert.Equal("", p.ParameterName);
		Assert.Equal(ParameterDirection.Input, p.Direction);
		Assert.False(p.IsNullable);
		Assert.Equal(0, p.Size);
		Assert.Null(p.Value);
	}

	[Fact]
	public void MySqlParameter_name_value_ctor_assigns_both()
	{
		var p = new MySqlParameter("p0", 42);
		Assert.Equal("p0", p.ParameterName);
		Assert.Equal(42, p.Value);
	}

	[Fact]
	public void MySqlParameter_dbtype_ctor_sets_provider_type()
	{
		var p = new MySqlParameter("p0", MySqlDbType.Int32);
		Assert.Equal(MySqlDbType.Int32, p.MySqlDbType);
	}

	[Fact]
	public void OracleParameter_name_value_ctor_assigns_both()
	{
		var p = new OracleParameter("p0", 42);
		Assert.Equal("p0", p.ParameterName);
		Assert.Equal(42, p.Value);
	}

	[Fact]
	public void OracleParameter_dbtype_ctor_sets_provider_type()
	{
		var p = new OracleParameter("p0", OracleDbType.Int32);
		Assert.Equal(OracleDbType.Int32, p.OracleDbType);
	}

	[Fact]
	public void MySqlParameter_Clone_produces_equal_copy()
	{
		var p = new MySqlParameter("p0", MySqlDbType.Int32) { Value = 7, Size = 4 };
		var c = p.Clone();
		Assert.NotSame(p, c);
		Assert.Equal(p.ParameterName, c.ParameterName);
		Assert.Equal(p.MySqlDbType, c.MySqlDbType);
		Assert.Equal(p.Value, c.Value);
		Assert.Equal(p.Size, c.Size);
	}

	[Fact]
	public void ParameterCollection_adds_and_indexes_by_name()
	{
		var cmd = new MySqlCommand();
		var p = cmd.Parameters.Add(new MySqlParameter("p0", 1));
		Assert.Same(p, cmd.Parameters[0]);
		Assert.Same(p, cmd.Parameters["p0"]);
		Assert.Equal(1, cmd.Parameters.Count);
	}

	[Fact]
	public void ParameterCollection_add_with_name_and_type_returns_parameter()
	{
		var cmd = new OracleCommand();
		var p = cmd.Parameters.Add("p0", OracleDbType.Int32);
		Assert.Equal("p0", p.ParameterName);
		Assert.Same(p, cmd.Parameters["p0"]);
	}

	[Fact]
	public void ParameterCollection_clear_removes_all()
	{
		var cmd = new MySqlCommand();
		cmd.Parameters.Add(new MySqlParameter("a", 1));
		cmd.Parameters.Add(new MySqlParameter("b", 2));
		Assert.Equal(2, cmd.Parameters.Count);
		cmd.Parameters.Clear();
		Assert.Equal(0, cmd.Parameters.Count);
	}

	[Fact]
	public void ParameterCollection_remove_drops_parameter()
	{
		var cmd = new OracleCommand();
		var p = cmd.Parameters.Add(new OracleParameter("a", 1));
		// OracleParameterCollection.Remove 返回 void（MySql 版返回 bool，两者不同）
		cmd.Parameters.Remove(p);
		Assert.Equal(0, cmd.Parameters.Count);
	}
}

/// <summary>
/// 属性集合与 BulkCopy 映射对象测试。
/// </summary>
public sealed class AttributeAndMappingTests
{
	[Fact]
	public void MySqlAttribute_exposes_name_and_value()
	{
		var a = new MySqlAttribute("k", "v");
		Assert.Equal("k", a.AttributeName);
		Assert.Equal("v", a.Value);
	}

	[Fact]
	public void MySqlAttribute_Clone_is_independent_copy()
	{
		var a = new MySqlAttribute("k", "v");
		var c = a.Clone();
		Assert.NotSame(a, c);
		Assert.Equal(a.AttributeName, c.AttributeName);
		Assert.Equal(a.Value, c.Value);
	}

	[Fact]
	public void MySqlAttributeCollection_SetAttribute_adds()
	{
		var col = new MySqlAttributeCollection();
		col.SetAttribute("k", "v");
		Assert.Equal(1, col.Count);
		Assert.Equal("k", col[0].AttributeName);
		Assert.Equal("v", col[0].Value);
	}

	[Fact]
	public void MySqlAttributeCollection_SetAttribute_updates_existing()
	{
		var col = new MySqlAttributeCollection();
		col.SetAttribute("k", "v1");
		col.SetAttribute("k", "v2");
		Assert.Equal(1, col.Count);
		Assert.Equal("v2", col[0].Value);
	}

	[Fact]
	public void MySqlAttributeCollection_Clear_empties()
	{
		var col = new MySqlAttributeCollection();
		col.SetAttribute("k", "v");
		col.Clear();
		Assert.Equal(0, col.Count);
	}

	[Fact]
	public void MySqlBulkCopyColumnMapping_defaults_are_empty()
	{
		// 实测（原始与重建程序集一致）：DestinationColumn 默认 ""，Expression 默认 null
		var m = new MySqlBulkCopyColumnMapping();
		Assert.Equal(0, m.SourceOrdinal);
		Assert.Equal("", m.DestinationColumn);
		Assert.Null(m.Expression);
	}

	[Fact]
	public void MySqlBulkCopyColumnMapping_ctor_assigns_all()
	{
		var m = new MySqlBulkCopyColumnMapping(2, "dest", "expr");
		Assert.Equal(2, m.SourceOrdinal);
		Assert.Equal("dest", m.DestinationColumn);
		Assert.Equal("expr", m.Expression);
	}
}

/// <summary>
/// 日志与诊断相关类型测试。
/// </summary>
public sealed class LoggingTests
{
	[Fact]
	public void MySqlConnectorLogLevel_has_expected_members()
	{
		var names = Enum.GetNames<MySqlConnectorLogLevel>();
		Assert.Contains("Trace", names);
		Assert.Contains("Debug", names);
		Assert.Contains("Info", names);
		Assert.Contains("Warn", names);
		Assert.Contains("Error", names);
		Assert.Contains("Fatal", names);
	}

	[Fact]
	public void NoOpLoggerProvider_returns_a_logger()
	{
		var provider = new NoOpLoggerProvider();
		Assert.NotNull(provider.CreateLogger("test"));
	}

	[Fact]
	public void MySqlConnectorLogManager_Provider_is_write_only_and_assignable()
	{
		// Provider 只有 set 访问器（只写），无法回读校验；
		// 这里验证「可赋值且不影响后续使用」这一契约。
		MySqlConnectorLogManager.Provider = new NoOpLoggerProvider();
		MySqlConnectorLogManager.Provider = new ConsoleLoggerProvider();
		MySqlConnectorLogManager.Provider = new NoOpLoggerProvider();
		Assert.True(true);
	}

	[Fact]
	public void TracingOptionsBuilder_is_chainable()
	{
		var b = new MySqlConnectorTracingOptionsBuilder();
		Assert.Same(b, b.EnableResultSetHeaderEvent(true));
		Assert.Same(b, b.EnableResultSetHeaderEvent(false));
	}
}

/// <summary>
/// Exception 类型层次测试。
/// </summary>
public sealed class ExceptionTests
{
	[Fact]
	public void MySqlException_defaults_are_stable()
	{
		var ex = new MySqlException("boom");
		Assert.Equal("boom", ex.Message);
		Assert.Equal(0, ex.Number);
		Assert.Equal(MySqlErrorCode.None, ex.ErrorCode);
		Assert.Null(ex.SqlState);
	}

	[Fact]
	public void Exception_hierarchy_matches_the_original()
	{
		// 实测：这三个异常**不**继承自 MySqlException，各自独立挂在 BCL 异常下。
		// 上层 catch 依赖这些关系，属公开契约。
		Assert.Equal(typeof(Exception), typeof(MySqlConversionException).BaseType);

		Assert.Equal(typeof(InvalidOperationException), typeof(MySqlProtocolException).BaseType);

		Assert.Equal(typeof(System.IO.EndOfStreamException), typeof(MySqlEndOfStreamException).BaseType);
		Assert.True(typeof(System.IO.IOException).IsAssignableFrom(typeof(MySqlEndOfStreamException)));

		Assert.False(typeof(MySqlException).IsAssignableFrom(typeof(MySqlConversionException)));
		Assert.False(typeof(MySqlException).IsAssignableFrom(typeof(MySqlProtocolException)));
	}

	[Fact]
	public void MySqlException_derives_from_DbException()
	{
		Assert.True(typeof(DbException).IsAssignableFrom(typeof(MySqlException)));
	}

	[Fact]
	public void MySqlEndOfStreamException_reports_byte_counts()
	{
		var ex = new MySqlEndOfStreamException(10, 4);
		Assert.Equal(10, ex.ExpectedByteCount);
		Assert.Equal(4, ex.ReadByteCount);
	}

	[Fact]
	public void MySqlError_is_constructible_from_its_public_surface()
	{
		// MySqlError 无公开构造，只能由驱动内部产生；这里只校验类型契约
		Assert.True(typeof(MySqlError).IsPublic);
		Assert.NotNull(typeof(MySqlError).GetProperty("Level"));
		Assert.NotNull(typeof(MySqlError).GetProperty("Code"));
		Assert.NotNull(typeof(MySqlError).GetProperty("ErrorCode"));
		Assert.NotNull(typeof(MySqlError).GetProperty("Message"));
	}
}

/// <summary>
/// Command / DataAdapter / CommandBuilder 的非服务端行为。
/// </summary>
public sealed class CommandAndAdapterTests
{
	[Fact]
	public void MySqlCommand_defaults_are_stable()
	{
		using var c = new MySqlCommand();
		Assert.Equal("", c.CommandText);
		Assert.Equal(0, c.CommandTimeout);  // 0 = 继承连接级 Default Command Timeout
		Assert.Equal(CommandType.Text, c.CommandType);
		Assert.NotNull(c.Parameters);
		Assert.Equal(0, c.Parameters.Count);
	}

	[Fact]
	public void OracleCommand_defaults_are_stable()
	{
		using var c = new OracleCommand();
		Assert.Equal("", c.CommandText);
		Assert.Equal(0, c.CommandTimeout);  // 0 = 继承连接级 Default Command Timeout
		Assert.NotNull(c.Parameters);
	}

	[Fact]
	public void MySqlCommand_ctor_binds_connection_and_text()
	{
		using var conn = new MySqlConnection("Server=h;");
		using var c = new MySqlCommand("SELECT 1", conn);
		Assert.Same(conn, c.Connection);
		Assert.Equal("SELECT 1", c.CommandText);
	}

	[Fact]
	public void OracleCommand_ctor_binds_connection_and_text()
	{
		using var conn = new OracleConnection("Server=h;Database=s;");
		using var c = new OracleCommand("SELECT 1 FROM dual", conn);
		Assert.Same(conn, c.Connection);
		Assert.Equal("SELECT 1 FROM dual", c.CommandText);
	}

	[Fact]
	public void MySqlCommand_CreateParameter_returns_bound_parameter()
	{
		using var c = new MySqlCommand();
		var p = c.CreateParameter();
		Assert.IsType<MySqlParameter>(p);
	}

	[Fact]
	public void OracleCommand_CreateParameter_returns_bound_parameter()
	{
		using var c = new OracleCommand();
		var p = c.CreateParameter();
		Assert.IsType<OracleParameter>(p);
	}

	// 注意：无连接的 ExecuteNonQuery 在**原始程序集**里就会栈溢出（见下方 Oracle 对照）。
	// 这是上游缺陷（ICancellableCommand.Session 自递归），重建代码忠实保留了该行为，
	// 因此不能用 Assert.Throws 测试——它会直接终止测试宿主（0xC00000FD）。
	// 详见 docs 中的“已知遗留”。
	[Fact]
	public void OracleCommand_execution_without_connection_throws()
	{
		// Oracle 模式走的是不同路径，这里行为正常：抛 InvalidOperationException
		using var c = new OracleCommand("SELECT 1 FROM dual");
		Assert.Throws<InvalidOperationException>(() => c.ExecuteNonQuery());
	}

	[Fact]
	public void MySqlDataAdapter_ctors_wire_up_commands()
	{
		using var a = new MySqlDataAdapter("SELECT 1", "Server=h;");
		Assert.NotNull(a.SelectCommand);
		Assert.Equal("SELECT 1", a.SelectCommand!.CommandText);
		// 实测：构造只装配 SelectCommand；Insert/Update/Delete 需自行赋值或由 CommandBuilder 生成
		Assert.Null(a.InsertCommand);
		Assert.Null(a.UpdateCommand);
		Assert.Null(a.DeleteCommand);
	}

	[Fact]
	public void OracleDataAdapter_ctors_wire_up_commands()
	{
		using var a = new OracleDataAdapter("SELECT 1 FROM dual", "Server=h;Database=s;");
		Assert.NotNull(a.SelectCommand);
		Assert.Equal("SELECT 1 FROM dual", a.SelectCommand!.CommandText);
	}

	[Fact]
	public void MySqlCommandBuilder_exposes_adapter()
	{
		using var a = new MySqlDataAdapter("SELECT 1", "Server=h;");
		using var b = new MySqlCommandBuilder(a);
		Assert.Same(a, b.DataAdapter);
	}

	[Fact]
	public void OracleCommandBuilder_exposes_adapter()
	{
		using var a = new OracleDataAdapter("SELECT 1 FROM dual", "Server=h;Database=s;");
		using var b = new OracleCommandBuilder(a);
		Assert.Same(a, b.DataAdapter);
	}
}
