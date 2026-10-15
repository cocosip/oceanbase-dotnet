using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OceanBase.Core;
using OceanBase.Logging;
using OceanBase.Protocol;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;

namespace OceanBase;

/// <summary>
/// <para><see cref="T:OceanBase.MySqlBulkCopy" /> lets you efficiently load a MySQL Server table with data from another source.
/// It is similar to the <a href="https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlbulkcopy">SqlBulkCopy</a> class
/// for SQL Server.</para>
/// <para>Due to <a href="https://mysqlconnector.net/troubleshooting/load-data-local-infile/">security features</a>
/// in MySQL Server, the connection string <em>must</em> have <c>AllowLoadLocalInfile=true</c> in order
/// to use this class.</para>
/// <para>For data that is in CSV or TSV format, use <see cref="T:OceanBase.MySqlBulkLoader" /> to bulk load the file.</para>
/// <para>Example code:</para>
/// <code>
/// // NOTE: to copy data between tables in the same database, use INSERT ... SELECT
/// // https://dev.mysql.com/doc/refman/8.0/en/insert-select.html
/// var dataTable = GetDataTableFromExternalSource();
///
/// // open the connection
/// await using var connection = new MySqlConnection("...;AllowLoadLocalInfile=True");
/// await connection.OpenAsync();
///
/// // bulk copy the data
/// var bulkCopy = new MySqlBulkCopy(connection);
/// bulkCopy.DestinationTableName = "some_table_name";
/// var result = await bulkCopy.WriteToServerAsync(dataTable);
///
/// // check for problems
/// if (result.Warnings.Count != 0) { /* handle potential data loss warnings */ }
/// </code>
/// </summary>
/// <remarks><para><strong>Note:</strong> This API is a unique feature of MySqlConnector; you must
/// <a href="https://mysqlconnector.net/overview/installing/">switch to MySqlConnector</a> in order to use it.</para>
/// <para>This API is experimental and may change in the future.</para>
/// </remarks>
public sealed class MySqlBulkCopy
{
	private static readonly char[] s_specialCharacters = new char[3] { '\t', '\\', '\n' };

	private static readonly UTF8Encoding s_utf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

	private readonly MySqlConnection m_connection;

	private readonly MySqlTransaction? m_transaction;

	private readonly ILogger m_logger;

	private int m_rowsCopied;

	private IValuesEnumerator? m_valuesEnumerator;

	private bool m_wasAborted;

	/// <summary>
	/// A <see cref="T:OceanBase.MySqlBulkLoaderConflictOption" /> value that specifies how conflicts are resolved (default <see cref="F:OceanBase.MySqlBulkLoaderConflictOption.None" />).
	/// </summary>
	public MySqlBulkLoaderConflictOption ConflictOption { get; set; }

	/// <summary>
	/// The number of seconds for the operation to complete before it times out, or <c>0</c> for no timeout.
	/// </summary>
	public int BulkCopyTimeout { get; set; }

	/// <summary>
	/// The name of the table to insert rows into.
	/// </summary>
	/// <remarks>This name needs to be quoted if it contains special characters.</remarks>
	public string? DestinationTableName { get; set; }

	/// <summary>
	/// If non-zero, this specifies the number of rows to be processed before generating a notification event.
	/// </summary>
	public int NotifyAfter { get; set; }

	/// <summary>
	/// A collection of <see cref="T:OceanBase.MySqlBulkCopyColumnMapping" /> objects. If the columns being copied from the
	/// data source line up one-to-one with the columns in the destination table then populating this collection is
	/// unnecessary. Otherwise, this should be filled with a collection of <see cref="T:OceanBase.MySqlBulkCopyColumnMapping" /> objects
	/// specifying how source columns are to be mapped onto destination columns. If one column mapping is specified,
	/// then all must be specified.
	/// </summary>
	public List<MySqlBulkCopyColumnMapping> ColumnMappings { get; }

	/// <summary>
	/// Returns the number of rows that were copied (after <c>WriteToServer(Async)</c> finishes).
	/// </summary>
	[Obsolete("Use the MySqlBulkCopyResult.RowsInserted property returned by WriteToServer.")]
	public int RowsCopied => m_rowsCopied;

	/// <summary>
	/// This event is raised every time that the number of rows specified by the <see cref="P:OceanBase.MySqlBulkCopy.NotifyAfter" /> property have been processed.
	/// </summary>
	/// <remarks>
	/// <para>Receipt of a RowsCopied event does not imply that any rows have been sent to the server or committed.</para>
	/// <para>The <see cref="P:OceanBase.MySqlRowsCopiedEventArgs.Abort" /> property can be set to <c>true</c> by the event handler to abort the copy.</para>
	/// </remarks>
	public event MySqlRowsCopiedEventHandler? MySqlRowsCopied;

	/// <summary>
	/// Initializes a <see cref="T:OceanBase.MySqlBulkCopy" /> object with the specified connection, and optionally the active transaction.
	/// </summary>
	/// <param name="connection">The <see cref="T:OceanBase.MySqlConnection" /> to use.</param>
	/// <param name="transaction">(Optional) The <see cref="T:OceanBase.MySqlTransaction" /> to use.</param>
	public MySqlBulkCopy(MySqlConnection connection, MySqlTransaction? transaction = null)
	{
		ExceptionExtensions.ThrowIfNull(connection, "connection");
		m_connection = connection;
		m_transaction = transaction;
		m_logger = m_connection.LoggingConfiguration.BulkCopyLogger;
		ColumnMappings = new List<MySqlBulkCopyColumnMapping>();
	}

	/// <summary>
	/// Copies all rows in the supplied <see cref="T:System.Data.DataTable" /> to the destination table specified by the
	/// <see cref="P:OceanBase.MySqlBulkCopy.DestinationTableName" /> property of the <see cref="T:OceanBase.MySqlBulkCopy" /> object.
	/// </summary>
	/// <param name="dataTable">The <see cref="T:System.Data.DataTable" /> to copy.</param>
	/// <returns>A <see cref="T:OceanBase.MySqlBulkCopyResult" /> with the result of the bulk copy operation.</returns>
	public MySqlBulkCopyResult WriteToServer(DataTable dataTable)
	{
		ExceptionExtensions.ThrowIfNull(dataTable, "dataTable");
		m_valuesEnumerator = DataRowsValuesEnumerator.Create(dataTable);
		return WriteToServerAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Asynchronously copies all rows in the supplied <see cref="T:System.Data.DataTable" /> to the destination table specified by the
	/// <see cref="P:OceanBase.MySqlBulkCopy.DestinationTableName" /> property of the <see cref="T:OceanBase.MySqlBulkCopy" /> object.
	/// </summary>
	/// <param name="dataTable">The <see cref="T:System.Data.DataTable" /> to copy.</param>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:OceanBase.MySqlBulkCopyResult" /> with the result of the bulk copy operation.</returns>
	public async ValueTask<MySqlBulkCopyResult> WriteToServerAsync(DataTable dataTable, CancellationToken cancellationToken = default(CancellationToken))
	{
		ExceptionExtensions.ThrowIfNull(dataTable, "dataTable");
		m_valuesEnumerator = DataRowsValuesEnumerator.Create(dataTable);
		return await WriteToServerAsync(IOBehavior.Asynchronous, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	/// <summary>
	/// Copies all rows in the supplied sequence of <see cref="T:System.Data.DataRow" /> objects to the destination table specified by the
	/// <see cref="P:OceanBase.MySqlBulkCopy.DestinationTableName" /> property of the <see cref="T:OceanBase.MySqlBulkCopy" /> object. The number of columns
	/// to be read from the <see cref="T:System.Data.DataRow" /> objects must be specified in advance.
	/// </summary>
	/// <param name="dataRows">The collection of <see cref="T:System.Data.DataRow" /> objects.</param>
	/// <param name="columnCount">The number of columns to copy (in each row).</param>
	/// <returns>A <see cref="T:OceanBase.MySqlBulkCopyResult" /> with the result of the bulk copy operation.</returns>
	public MySqlBulkCopyResult WriteToServer(IEnumerable<DataRow> dataRows, int columnCount)
	{
		ExceptionExtensions.ThrowIfNull(dataRows, "dataRows");
		m_valuesEnumerator = new DataRowsValuesEnumerator(dataRows, columnCount);
		return WriteToServerAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Asynchronously copies all rows in the supplied sequence of <see cref="T:System.Data.DataRow" /> objects to the destination table specified by the
	/// <see cref="P:OceanBase.MySqlBulkCopy.DestinationTableName" /> property of the <see cref="T:OceanBase.MySqlBulkCopy" /> object. The number of columns
	/// to be read from the <see cref="T:System.Data.DataRow" /> objects must be specified in advance.
	/// </summary>
	/// <param name="dataRows">The collection of <see cref="T:System.Data.DataRow" /> objects.</param>
	/// <param name="columnCount">The number of columns to copy (in each row).</param>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:OceanBase.MySqlBulkCopyResult" /> with the result of the bulk copy operation.</returns>
	public async ValueTask<MySqlBulkCopyResult> WriteToServerAsync(IEnumerable<DataRow> dataRows, int columnCount, CancellationToken cancellationToken = default(CancellationToken))
	{
		ExceptionExtensions.ThrowIfNull(dataRows, "dataRows");
		m_valuesEnumerator = new DataRowsValuesEnumerator(dataRows, columnCount);
		return await WriteToServerAsync(IOBehavior.Asynchronous, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	/// <summary>
	/// Copies all rows in the supplied <see cref="T:System.Data.IDataReader" /> to the destination table specified by the
	/// <see cref="P:OceanBase.MySqlBulkCopy.DestinationTableName" /> property of the <see cref="T:OceanBase.MySqlBulkCopy" /> object.
	/// </summary>
	/// <param name="dataReader">The <see cref="T:System.Data.IDataReader" /> to copy from.</param>
	/// <returns>A <see cref="T:OceanBase.MySqlBulkCopyResult" /> with the result of the bulk copy operation.</returns>
	public MySqlBulkCopyResult WriteToServer(IDataReader dataReader)
	{
		ExceptionExtensions.ThrowIfNull(dataReader, "dataReader");
		m_valuesEnumerator = DataReaderValuesEnumerator.Create(dataReader);
		return WriteToServerAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Asynchronously copies all rows in the supplied <see cref="T:System.Data.IDataReader" /> to the destination table specified by the
	/// <see cref="P:OceanBase.MySqlBulkCopy.DestinationTableName" /> property of the <see cref="T:OceanBase.MySqlBulkCopy" /> object.
	/// </summary>
	/// <param name="dataReader">The <see cref="T:System.Data.IDataReader" /> to copy from.</param>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:OceanBase.MySqlBulkCopyResult" /> with the result of the bulk copy operation.</returns>
	public async ValueTask<MySqlBulkCopyResult> WriteToServerAsync(IDataReader dataReader, CancellationToken cancellationToken = default(CancellationToken))
	{
		ExceptionExtensions.ThrowIfNull(dataReader, "dataReader");
		m_valuesEnumerator = DataReaderValuesEnumerator.Create(dataReader);
		return await WriteToServerAsync(IOBehavior.Asynchronous, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async ValueTask<MySqlBulkCopyResult> WriteToServerAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		string tableName = DestinationTableName ?? throw new InvalidOperationException("DestinationTableName must be set before calling WriteToServer");
		m_wasAborted = false;
		Log.StartingBulkCopy(m_logger, tableName);
		MySqlBulkLoader bulkLoader = new MySqlBulkLoader(m_connection)
		{
			CharacterSet = "utf8mb4",
			EscapeCharacter = '\\',
			FieldQuotationCharacter = '\0',
			FieldTerminator = "\t",
			LinePrefix = null,
			LineTerminator = "\n",
			Local = true,
			NumberOfLinesToSkip = 0,
			Source = this,
			TableName = tableName,
			Timeout = BulkCopyTimeout,
			ConflictOption = ConflictOption
		};
		bool closeConnection = false;
		if (m_connection.State != ConnectionState.Open)
		{
			m_connection.Open();
			closeConnection = true;
		}
		List<MySqlBulkCopyColumnMapping> columnMappings = new List<MySqlBulkCopyColumnMapping>(ColumnMappings);
		bool addDefaultMappings = columnMappings.Count == 0;
		using (MySqlCommand cmd = new MySqlCommand("select * from " + tableName + ";", m_connection, m_transaction))
		{
			using MySqlDataReader mySqlDataReader = await cmd.ExecuteReaderAsync(CommandBehavior.SchemaOnly, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			ReadOnlyCollection<DbColumn> columnSchema = mySqlDataReader.GetColumnSchema();
			for (int i = 0; i < columnSchema.Count; i++)
			{
				string name = mySqlDataReader.GetName(i);
				string dataTypeName = columnSchema[i].DataTypeName;
				if (dataTypeName == "BIT")
				{
					AddColumnMapping(m_logger, columnMappings, addDefaultMappings, i, name, $"@`\ue002\bcol{i}`", "%COL% = CAST(%VAR% AS UNSIGNED)");
					continue;
				}
				Type dataType = columnSchema[i].DataType;
				bool flag = dataType == typeof(byte[]) || dataTypeName == "VECTOR";
				if (!flag)
				{
					bool flag2 = dataType == typeof(Guid);
					if (flag2)
					{
						MySqlGuidFormat guidFormat = m_connection.GuidFormat;
						bool flag3 = (uint)(guidFormat - 4) <= 2u;
						flag2 = flag3;
					}
					flag = flag2;
				}
				if (flag)
				{
					AddColumnMapping(m_logger, columnMappings, addDefaultMappings, i, name, $"@`\ue002\bcol{i}`", "%COL% = UNHEX(%VAR%)");
				}
				else if (addDefaultMappings)
				{
					if (columnSchema[i].DataTypeName == "YEAR")
					{
						throw new NotSupportedException("'YEAR' columns are not supported by MySqlBulkCopy.");
					}
					Log.AddingDefaultColumnMapping(m_logger, i, name);
					columnMappings.Add(new MySqlBulkCopyColumnMapping(i, name));
				}
			}
		}
		int j;
		for (j = 0; j < m_valuesEnumerator.FieldCount; j++)
		{
			MySqlBulkCopyColumnMapping mySqlBulkCopyColumnMapping = columnMappings.FirstOrDefault((MySqlBulkCopyColumnMapping x) => x.SourceOrdinal == j);
			if (mySqlBulkCopyColumnMapping == null)
			{
				Log.IgnoringColumn(m_logger, j);
				bulkLoader.Columns.Add("@`\ue002\bignore`");
				continue;
			}
			if (mySqlBulkCopyColumnMapping.DestinationColumn.Length == 0)
			{
				throw new InvalidOperationException($"MySqlBulkCopyColumnMapping.DestinationName is not set for SourceOrdinal {mySqlBulkCopyColumnMapping.SourceOrdinal}");
			}
			if (mySqlBulkCopyColumnMapping.DestinationColumn[0] == '@' && mySqlBulkCopyColumnMapping.Expression != null)
			{
				bulkLoader.Columns.Add(mySqlBulkCopyColumnMapping.DestinationColumn);
			}
			else
			{
				bulkLoader.Columns.Add(QuoteIdentifier(mySqlBulkCopyColumnMapping.DestinationColumn));
			}
			if (mySqlBulkCopyColumnMapping.Expression != null)
			{
				bulkLoader.Expressions.Add(mySqlBulkCopyColumnMapping.Expression);
			}
		}
		foreach (MySqlBulkCopyColumnMapping item in columnMappings)
		{
			if (item.SourceOrdinal < 0 || item.SourceOrdinal >= m_valuesEnumerator.FieldCount)
			{
				throw new InvalidOperationException($"SourceOrdinal {item.SourceOrdinal} is an invalid value");
			}
		}
		List<MySqlError> errors = new List<MySqlError>();
		MySqlInfoMessageEventHandler infoMessageHandler = delegate(object s, MySqlInfoMessageEventArgs e)
		{
			errors.AddRange(e.Errors);
		};
		m_connection.InfoMessage += infoMessageHandler;
		int num;
		try
		{
			num = await bulkLoader.LoadAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			m_connection.InfoMessage -= infoMessageHandler;
		}
		if (closeConnection)
		{
			m_connection.Close();
		}
		Log.FinishedBulkCopy(m_logger, tableName);
		if (!m_wasAborted && num != m_rowsCopied && ConflictOption == MySqlBulkLoaderConflictOption.None)
		{
			Log.BulkCopyFailed(m_logger, tableName, m_rowsCopied, num);
			throw new MySqlException(MySqlErrorCode.BulkCopyFailed, string.Format("{0} row{1} copied to {2} but only {3} {4} inserted.", m_rowsCopied, (m_rowsCopied == 1) ? " was" : "s were", tableName, num, (num == 1) ? "was" : "were"));
		}
		return new MySqlBulkCopyResult(errors, num);
		static void AddColumnMapping(ILogger logger, List<MySqlBulkCopyColumnMapping> list, bool flag4, int destinationOrdinal, string destinationColumn, string variableName, string expression)
		{
			expression = expression.Replace("%COL%", "`" + destinationColumn + "`").Replace("%VAR%", variableName);
			MySqlBulkCopyColumnMapping mySqlBulkCopyColumnMapping2 = list.FirstOrDefault((MySqlBulkCopyColumnMapping x) => destinationColumn.Equals(x.DestinationColumn, StringComparison.OrdinalIgnoreCase));
			if (mySqlBulkCopyColumnMapping2 != null)
			{
				if (mySqlBulkCopyColumnMapping2.Expression != null)
				{
					Log.ColumnMappingAlreadyHasExpression(logger, mySqlBulkCopyColumnMapping2.SourceOrdinal, destinationColumn, mySqlBulkCopyColumnMapping2.Expression);
				}
				else
				{
					Log.SettingExpressionToMapColumn(logger, mySqlBulkCopyColumnMapping2.SourceOrdinal, destinationColumn, expression);
					list.Remove(mySqlBulkCopyColumnMapping2);
					list.Add(new MySqlBulkCopyColumnMapping(mySqlBulkCopyColumnMapping2.SourceOrdinal, variableName, expression));
				}
			}
			else if (flag4)
			{
				Log.AddingDefaultColumnMapping(logger, destinationOrdinal, destinationColumn);
				list.Add(new MySqlBulkCopyColumnMapping(destinationOrdinal, variableName, expression));
			}
		}
		static string QuoteIdentifier(string identifier)
		{
			return "`" + identifier.Replace("`", "``") + "`";
		}
	}

	internal async Task SendDataReaderAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		byte[] buffer = ArrayPool<byte>.Shared.Rent(1048576);
		int outputIndex = 0;
		m_rowsCopied = 0;
		MySqlRowsCopiedEventArgs eventArgs = null;
		if (NotifyAfter > 0 && this.MySqlRowsCopied != null)
		{
			eventArgs = new MySqlRowsCopiedEventArgs();
		}
		try
		{
			object[] values = new object[m_valuesEnumerator.FieldCount];
			Encoder utf8Encoder = null;
			while ((ioBehavior != IOBehavior.Asynchronous) ? m_valuesEnumerator.MoveNext() : (await m_valuesEnumerator.MoveNextAsync().ConfigureAwait(continueOnCapturedContext: false)))
			{
				m_valuesEnumerator.GetValues(values);
				for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
				{
					if (valueIndex > 0)
					{
						buffer[outputIndex++] = 9;
					}
					int inputIndex = 0;
					int bytesWritten = 0;
					while (true)
					{
						if (outputIndex < 1048575)
						{
							MySqlConnection connection = m_connection;
							object value = values[valueIndex];
							Span<byte> span = buffer.AsSpan(0, 1048575);
							int num = outputIndex;
							if (WriteValue(connection, value, ref inputIndex, ref utf8Encoder, span.Slice(num, span.Length - num), out bytesWritten))
							{
								break;
							}
						}
						PayloadData payload = new PayloadData(new ArraySegment<byte>(buffer, 0, outputIndex + bytesWritten));
						await m_connection.Session.SendReplyAsync(payload, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
						outputIndex = 0;
						bytesWritten = 0;
					}
					outputIndex += bytesWritten;
				}
				buffer[outputIndex++] = 10;
				m_rowsCopied++;
				if (eventArgs != null && m_rowsCopied % NotifyAfter == 0)
				{
					eventArgs.RowsCopied = m_rowsCopied;
					this.MySqlRowsCopied(this, eventArgs);
					if (eventArgs.Abort)
					{
						break;
					}
				}
			}
			if (outputIndex != 0 && ((!(eventArgs?.Abort)) ?? true))
			{
				PayloadData payload2 = new PayloadData(new ArraySegment<byte>(buffer, 0, outputIndex));
				await m_connection.Session.SendReplyAsync(payload2, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
			m_wasAborted = eventArgs?.Abort ?? false;
		}
		static bool WriteBytes(ReadOnlySpan<byte> readOnlySpan2, ref int reference2, Span<byte> output, out int reference)
		{
			ReadOnlySpan<byte> readOnlySpan = "0123456789ABCDEF"u8;
			reference = 0;
			while (reference2 < readOnlySpan2.Length && output.Length > 2)
			{
				byte b = readOnlySpan2[reference2];
				output[0] = readOnlySpan[(b >> 4) & 0xF];
				output[1] = readOnlySpan[b & 0xF];
				output = output.Slice(2, output.Length - 2);
				reference += 2;
				reference2++;
			}
			return reference2 == readOnlySpan2.Length;
		}
		static bool WriteString(string value2, ref Encoder? utf8Encoder2, Span<byte> output, out int reference)
		{
			int inputIndex2 = 0;
			if (WriteSubstring(value2, ref inputIndex2, ref utf8Encoder2, output, out reference))
			{
				return true;
			}
			reference = 0;
			return false;
		}
		static bool WriteSubstring(string text, ref int reference2, ref Encoder? reference3, Span<byte> output, out int reference)
		{
			reference = 0;
			while (reference2 < text.Length)
			{
				if (Array.IndexOf(s_specialCharacters, text[reference2]) != -1)
				{
					if (output.Length <= 2)
					{
						return false;
					}
					output[0] = 92;
					output[1] = (byte)text[reference2];
					output = output.Slice(2, output.Length - 2);
					reference += 2;
					reference2++;
				}
				else
				{
					int num2 = text.IndexOfAny(s_specialCharacters, reference2);
					if (num2 == -1)
					{
						num2 = text.Length;
					}
					if (reference3 == null)
					{
						reference3 = s_utf8Encoding.GetEncoder();
					}
					if (output.Length < 4 && reference3.GetByteCount(text.AsSpan(reference2, Math.Min(2, num2 - reference2)), flush: false) > output.Length)
					{
						return false;
					}
					reference3.Convert(text.AsSpan(reference2, num2 - reference2), output, num2 == text.Length, out var charsUsed, out var bytesUsed, out var completed);
					reference += bytesUsed;
					int num3 = bytesUsed;
					output = output.Slice(num3, output.Length - num3);
					reference2 += charsUsed;
					if (!completed)
					{
						return false;
					}
				}
			}
			return true;
		}
		static bool WriteValue(MySqlConnection mySqlConnection, object obj, ref int inputIndex2, ref Encoder? utf8Encoder2, Span<byte> output, out int reference)
		{
			if (output.Length == 0)
			{
				reference = 0;
				return false;
			}
			if (obj == null || obj == DBNull.Value)
			{
				ReadOnlySpan<byte> readOnlySpan = "\\N"u8;
				if (output.Length < readOnlySpan.Length)
				{
					reference = 0;
					return false;
				}
				readOnlySpan.CopyTo(output);
				reference = readOnlySpan.Length;
				return true;
			}
			if (obj is string value2)
			{
				return WriteSubstring(value2, ref inputIndex2, ref utf8Encoder2, output, out reference);
			}
			if (obj is char c)
			{
				return WriteString(c.ToString(), ref utf8Encoder2, output, out reference);
			}
			if (obj is byte value3)
			{
				return Utf8Formatter.TryFormat(value3, output, out reference);
			}
			if (obj is sbyte value4)
			{
				return Utf8Formatter.TryFormat(value4, output, out reference);
			}
			if (obj is short value5)
			{
				return Utf8Formatter.TryFormat(value5, output, out reference);
			}
			if (obj is ushort value6)
			{
				return Utf8Formatter.TryFormat(value6, output, out reference);
			}
			if (obj is int value7)
			{
				return Utf8Formatter.TryFormat(value7, output, out reference);
			}
			if (obj is uint value8)
			{
				return Utf8Formatter.TryFormat(value8, output, out reference);
			}
			if (obj is long value9)
			{
				return Utf8Formatter.TryFormat(value9, output, out reference);
			}
			if (obj is ulong value10)
			{
				return Utf8Formatter.TryFormat(value10, output, out reference);
			}
			if (obj is decimal value11)
			{
				return Utf8Formatter.TryFormat(value11, output, out reference);
			}
			if ((obj is byte[] || obj is ReadOnlyMemory<byte> || obj is Memory<byte> || obj is ArraySegment<byte> || obj is MySqlGeometry || obj is float[] || obj is ReadOnlyMemory<float> || obj is Memory<float>) ? true : false)
			{
				ReadOnlySpan<byte> value12 = ((obj is byte[] array) ? ((ReadOnlySpan<byte>)array.AsSpan()) : ((obj is ArraySegment<byte> segment) ? ((ReadOnlySpan<byte>)segment.AsSpan()) : ((obj is Memory<byte> memory) ? ((ReadOnlySpan<byte>)memory.Span) : ((obj is MySqlGeometry mySqlGeometry) ? mySqlGeometry.ValueSpan : ((obj is float[] array2) ? MySqlParameter.ConvertFloatsToBytes(array2.AsSpan()) : ((obj is Memory<float> memory2) ? MySqlParameter.ConvertFloatsToBytes(memory2.Span) : ((!(obj is ReadOnlyMemory<float> readOnlyMemory)) ? ((ReadOnlyMemory<byte>)obj).Span : MySqlParameter.ConvertFloatsToBytes(readOnlyMemory.Span))))))));
				return WriteBytes(value12, ref inputIndex2, output, out reference);
			}
			if (obj is bool flag)
			{
				if (output.Length < 1)
				{
					reference = 0;
					return false;
				}
				output[0] = (byte)(flag ? 49 : 48);
				reference = 1;
				return true;
			}
			if (obj is float num2)
			{
				return WriteString(num2.ToString("R", CultureInfo.InvariantCulture), ref utf8Encoder2, output, out reference);
			}
			if (obj is double num3)
			{
				return WriteString(num3.ToString("R", CultureInfo.InvariantCulture), ref utf8Encoder2, output, out reference);
			}
			if (obj is MySqlDateTime mySqlDateTime)
			{
				if (mySqlDateTime.IsValidDateTime)
				{
					return WriteString(mySqlDateTime.GetDateTime().ToString("yyyy'-'MM'-'dd' 'HH':'mm':'ss'.'ffffff", CultureInfo.InvariantCulture), ref utf8Encoder2, output, out reference);
				}
				return WriteString("0000-00-00", ref utf8Encoder2, output, out reference);
			}
			if (obj is DateTime dateTime)
			{
				if (mySqlConnection.DateTimeKind == DateTimeKind.Utc && dateTime.Kind == DateTimeKind.Local)
				{
					throw new MySqlException("DateTime.Kind must not be Local when DateTimeKind setting is Utc");
				}
				if (mySqlConnection.DateTimeKind == DateTimeKind.Local && dateTime.Kind == DateTimeKind.Utc)
				{
					throw new MySqlException("DateTime.Kind must not be Utc when DateTimeKind setting is Local");
				}
				return WriteString(dateTime.ToString("yyyy'-'MM'-'dd' 'HH':'mm':'ss'.'ffffff", CultureInfo.InvariantCulture), ref utf8Encoder2, output, out reference);
			}
			if (obj is DateTimeOffset { UtcDateTime: var utcDateTime })
			{
				return WriteString(utcDateTime.ToString("yyyy'-'MM'-'dd' 'HH':'mm':'ss'.'ffffff", CultureInfo.InvariantCulture), ref utf8Encoder2, output, out reference);
			}
			if (obj is TimeSpan timeSpan)
			{
				bool flag2 = false;
				if (timeSpan.Ticks < 0)
				{
					flag2 = true;
					timeSpan = TimeSpan.FromTicks(-timeSpan.Ticks);
				}
				return WriteString(FormattableString.Invariant(FormattableStringFactory.Create("{0}{1}:{2:mm':'ss'.'ffffff}", flag2 ? "-" : "", timeSpan.Days * 24 + timeSpan.Hours, timeSpan)), ref utf8Encoder2, output, out reference);
			}
			if (obj is Guid value13)
			{
				MySqlGuidFormat guidFormat = mySqlConnection.GuidFormat;
				if ((uint)(guidFormat - 4) <= 2u)
				{
					byte[] array3 = value13.ToByteArray();
					if (mySqlConnection.GuidFormat != MySqlGuidFormat.LittleEndianBinary16)
					{
						Utility.SwapBytes(array3, 0, 3);
						Utility.SwapBytes(array3, 1, 2);
						Utility.SwapBytes(array3, 4, 5);
						Utility.SwapBytes(array3, 6, 7);
						if (mySqlConnection.GuidFormat == MySqlGuidFormat.TimeSwapBinary16)
						{
							Utility.SwapBytes(array3, 0, 4);
							Utility.SwapBytes(array3, 1, 5);
							Utility.SwapBytes(array3, 2, 6);
							Utility.SwapBytes(array3, 3, 7);
							Utility.SwapBytes(array3, 0, 2);
							Utility.SwapBytes(array3, 1, 3);
						}
					}
					return WriteBytes(array3, ref inputIndex2, output, out reference);
				}
				bool flag3 = mySqlConnection.GuidFormat == MySqlGuidFormat.Char32;
				return Utf8Formatter.TryFormat(value13, output, out reference, flag3 ? 'N' : 'D');
			}
			if (obj is Enum obj2)
			{
				return WriteString(obj2.ToString("d"), ref utf8Encoder2, output, out reference);
			}
			if (obj is BigInteger bigInteger)
			{
				return WriteString(bigInteger.ToString(CultureInfo.InvariantCulture), ref utf8Encoder2, output, out reference);
			}
			if (obj is MySqlDecimal mySqlDecimal)
			{
				return WriteString(mySqlDecimal.ToString(), ref utf8Encoder2, output, out reference);
			}
			throw new NotSupportedException($"Type {obj.GetType().Name} not currently supported. Value: {obj}");
		}
	}
}
