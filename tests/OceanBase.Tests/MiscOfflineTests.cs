using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 离线可测的杂项公共/内部 API 测试。
///
/// 覆盖值枚举器（DataTable / DbDataReader / IDataReader 三种来源）、
/// <see cref="MySqlHelper.EscapeString"/>、<see cref="MySqlError"/> 与
/// <see cref="MySqlProvidePasswordContext"/>。
/// </summary>
public sealed class MiscOfflineTests
{
	// ---------- DataRowsValuesEnumerator（DataTable 来源）----------

	[Fact]
	public async Task DataRowsEnumerator_walks_all_rows_and_copies_values()
	{
		var table = new DataTable();
		table.Columns.Add("Id", typeof(int));
		table.Columns.Add("Name", typeof(string));
		table.Rows.Add(1, "one");
		table.Rows.Add(2, "two");

		var type = typeof(MySqlConnection).Assembly.GetType("OceanBase.Core.DataRowsValuesEnumerator", throwOnError: true)!;
		var enumerator = (OceanBase.Core.IValuesEnumerator)type.GetMethod("Create")!.Invoke(null, new object[] { table })!;

		Assert.Equal(2, enumerator.FieldCount);

		var values = new object[2];
		Assert.True(enumerator.MoveNext());
		enumerator.GetValues(values);
		Assert.Equal(1, values[0]);
		Assert.Equal("one", values[1]);

		Assert.True(await enumerator.MoveNextAsync());
		enumerator.GetValues(values);
		Assert.Equal(2, values[0]);
		Assert.Equal("two", values[1]);

		Assert.False(enumerator.MoveNext());
		Assert.False(await enumerator.MoveNextAsync());
	}

	// ---------- DataReaderValuesEnumerator / DbDataReaderValuesEnumerator ----------

	[Fact]
	public async Task ReaderEnumerator_uses_db_reader_path_for_DbDataReader()
	{
		var table = new DataTable();
		table.Columns.Add("A", typeof(int));
		table.Rows.Add(10);
		table.Rows.Add(20);

		using DbDataReader reader = table.CreateDataReader();
		var type = typeof(MySqlConnection).Assembly.GetType("OceanBase.Core.DataReaderValuesEnumerator", throwOnError: true)!;
		var enumerator = (OceanBase.Core.IValuesEnumerator)type.GetMethod("Create")!.Invoke(null, new object[] { reader })!;

		// DbDataReader 走 DbDataReaderValuesEnumerator 专属实现
		Assert.Equal("DbDataReaderValuesEnumerator", enumerator.GetType().Name);
		Assert.Equal(1, enumerator.FieldCount);

		var values = new object[1];
		Assert.True(await enumerator.MoveNextAsync());
		enumerator.GetValues(values);
		Assert.Equal(10, values[0]);
		Assert.True(enumerator.MoveNext());
		Assert.False(enumerator.MoveNext());
	}

	[Fact]
	public async Task ReaderEnumerator_uses_generic_path_for_plain_IDataReader()
	{
		// DataTableReader 是 DbDataReader，这里包一层只暴露 IDataReader 的假实现
		var table = new DataTable();
		table.Columns.Add("A", typeof(int));
		table.Rows.Add(7);
		var inner = table.CreateDataReader();

		var enumerator = (OceanBase.Core.IValuesEnumerator)Activator.CreateInstance(
			typeof(MySqlConnection).Assembly.GetType("OceanBase.Core.DataReaderValuesEnumerator", throwOnError: true)!,
			new WrappingDataReader(inner))!;

		var values = new object[1];
		Assert.True(enumerator.MoveNext());
		enumerator.GetValues(values);
		Assert.Equal(7, values[0]);
		Assert.False(enumerator.MoveNext());
		Assert.False(await enumerator.MoveNextAsync());
	}

	private sealed class WrappingDataReader : IDataReader
	{
		private readonly IDataReader m_inner;

		public WrappingDataReader(IDataReader inner) => m_inner = inner;

		public object this[int i] => m_inner[i];
		public object this[string name] => m_inner[name];
		public int Depth => m_inner.Depth;
		public bool IsClosed => m_inner.IsClosed;
		public int RecordsAffected => m_inner.RecordsAffected;
		public int FieldCount => m_inner.FieldCount;
		public void Close() => m_inner.Close();
		public DataTable GetSchemaTable() => m_inner.GetSchemaTable();
		public bool NextResult() => m_inner.NextResult();
		public bool Read() => m_inner.Read();
		public int GetOrdinal(string name) => m_inner.GetOrdinal(name);
		public string GetName(int i) => m_inner.GetName(i);
		public string GetDataTypeName(int i) => m_inner.GetDataTypeName(i);
		public Type GetFieldType(int i) => m_inner.GetFieldType(i);
		public object GetValue(int i) => m_inner.GetValue(i);
		public int GetValues(object[] values) => m_inner.GetValues(values);
		public bool IsDBNull(int i) => m_inner.IsDBNull(i);
		public bool GetBoolean(int i) => m_inner.GetBoolean(i);
		public byte GetByte(int i) => m_inner.GetByte(i);
		public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => m_inner.GetBytes(i, fieldOffset, buffer, bufferoffset, length);
		public char GetChar(int i) => m_inner.GetChar(i);
		public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => m_inner.GetChars(i, fieldoffset, buffer, bufferoffset, length);
		public System.Data.IDataReader GetData(int i) => m_inner.GetData(i);
		public DateTime GetDateTime(int i) => m_inner.GetDateTime(i);
		public decimal GetDecimal(int i) => m_inner.GetDecimal(i);
		public double GetDouble(int i) => m_inner.GetDouble(i);
		public float GetFloat(int i) => m_inner.GetFloat(i);
		public Guid GetGuid(int i) => m_inner.GetGuid(i);
		public short GetInt16(int i) => m_inner.GetInt16(i);
		public int GetInt32(int i) => m_inner.GetInt32(i);
		public long GetInt64(int i) => m_inner.GetInt64(i);
		public string GetString(int i) => m_inner.GetString(i);
		public void Dispose() => m_inner.Dispose();
	}

	// ---------- MySqlHelper ----------

	[Theory]
	[InlineData("", "")]
	[InlineData("plain", "plain")]
	[InlineData("it's", "it\\'s")]
	[InlineData("say \"hi\"", "say \\\"hi\\\"")]
	[InlineData("back\\slash", "back\\\\slash")]
	[InlineData("'\\\"\\", "\\'\\\\\\\"\\\\")]
	public void EscapeString_escapes_quotes_and_backslashes(string value, string expected)
	{
		Assert.Equal(expected, MySqlHelper.EscapeString(value));
	}

	[Fact]
	public void EscapeString_rejects_null()
	{
		Assert.Throws<ArgumentNullException>(() => MySqlHelper.EscapeString(null!));
	}

	// ---------- MySqlError / MySqlProvidePasswordContext（internal 构造，反射）----------

	[Fact]
	public void MySqlError_exposes_level_code_and_message()
	{
		var constructor = typeof(MySqlError).GetConstructor(BindingFlags, null, new[] { typeof(string), typeof(int), typeof(string) }, null)!;
		var error = (MySqlError)constructor.Invoke(new object[] { "Error", 1064, "You have an error in your SQL syntax" });

		Assert.Equal("Error", error.Level);
#pragma warning disable 618
		Assert.Equal(1064, error.Code);
#pragma warning restore 618
		Assert.Equal(MySqlErrorCode.ParseError, error.ErrorCode);
		Assert.Equal("You have an error in your SQL syntax", error.Message);
	}

	[Fact]
	public void ProvidePasswordContext_carries_connection_info()
	{
		var constructor = typeof(MySqlProvidePasswordContext).GetConstructor(BindingFlags, null, new[] { typeof(string), typeof(int), typeof(string), typeof(string) }, null)!;
		var context = (MySqlProvidePasswordContext)constructor.Invoke(new object[] { "db.example.com", 2881, "root", "appdb" });

		Assert.Equal("db.example.com", context.Server);
		Assert.Equal(2881, context.Port);
		Assert.Equal("root", context.UserId);
		Assert.Equal("appdb", context.Database);
	}

	private const System.Reflection.BindingFlags BindingFlags =
		System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
}
