using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OceanBase.Protocol.Serialization;

namespace OceanBase;

/// <summary>
/// <para><see cref="T:OceanBase.MySqlBulkLoader" /> lets you efficiently load a MySQL Server Table with data from a CSV or TSV file or <see cref="T:System.IO.Stream" />.</para>
/// <para>Example code:</para>
/// <code>
/// await using var connection = new MySqlConnection("...;AllowLoadLocalInfile=True");
/// await connection.OpenAsync();
/// var bulkLoader = new MySqlBulkLoader(connection)
/// {
/// 	FileName = @"C:\Path\To\file.csv",
/// 	TableName = "destination",
/// 	CharacterSet = "UTF8",
/// 	NumberOfLinesToSkip = 1,
/// 	FieldTerminator = ",",
/// 	FieldQuotationCharacter = '"',
/// 	FieldQuotationOptional = true,
/// 	Local = true,
/// }
/// var rowCount = await bulkLoader.LoadAsync();
/// </code>
/// </summary>
/// <remarks>Due to <a href="https://mysqlconnector.net/troubleshooting/load-data-local-infile/">security features</a>
/// in MySQL Server, the connection string <strong>must</strong> have <c>AllowLoadLocalInfile=true</c> in order to use a local source.
/// </remarks>
public sealed class MySqlBulkLoader
{
	internal const string SourcePrefix = ":SOURCE:";

	private static readonly object s_lock = new object();

	private static readonly Dictionary<string, object> s_sources = new Dictionary<string, object>();

	/// <summary>
	/// (Optional) The character set of the source data. By default, the database's character set is used.
	/// </summary>
	public string? CharacterSet { get; set; }

	/// <summary>
	/// (Optional) A list of the column names in the destination table that should be filled with data from the input file.
	/// </summary>
	public List<string> Columns { get; }

	/// <summary>
	/// A <see cref="T:OceanBase.MySqlBulkLoaderConflictOption" /> value that specifies how conflicts are resolved (default <see cref="F:OceanBase.MySqlBulkLoaderConflictOption.None" />).
	/// </summary>
	public MySqlBulkLoaderConflictOption ConflictOption { get; set; }

	/// <summary>
	/// The <see cref="T:OceanBase.MySqlConnection" /> to use.
	/// </summary>
	public MySqlConnection Connection { get; set; }

	/// <summary>
	/// (Optional) The character used to escape instances of <see cref="P:OceanBase.MySqlBulkLoader.FieldQuotationCharacter" /> within field values.
	/// </summary>
	public char EscapeCharacter { get; set; }

	/// <summary>
	/// (Optional) A list of expressions used to set field values from the columns in the source data.
	/// </summary>
	public List<string> Expressions { get; }

	/// <summary>
	/// (Optional) The character used to enclose fields in the source data.
	/// </summary>
	public char FieldQuotationCharacter { get; set; }

	/// <summary>
	/// Whether quoting fields is optional (default <c>false</c>).
	/// </summary>
	public bool FieldQuotationOptional { get; set; }

	/// <summary>
	/// (Optional) The string fields are terminated with.
	/// </summary>
	public string? FieldTerminator { get; set; }

	/// <summary>
	/// The name of the local (if <see cref="P:OceanBase.MySqlBulkLoader.Local" /> is <c>true</c>) or remote (otherwise) file to load.
	/// Either this or <see cref="P:OceanBase.MySqlBulkLoader.SourceStream" /> must be set.
	/// </summary>
	public string? FileName { get; set; }

	/// <summary>
	/// (Optional) A prefix in each line that should be skipped when loading.
	/// </summary>
	public string? LinePrefix { get; set; }

	/// <summary>
	/// (Optional) The string lines are terminated with.
	/// </summary>
	public string? LineTerminator { get; set; }

	/// <summary>
	/// Whether a local file is being used (default <c>true</c>).
	/// </summary>
	public bool Local { get; set; }

	/// <summary>
	/// The number of lines to skip at the beginning of the file (default <c>0</c>).
	/// </summary>
	public int NumberOfLinesToSkip { get; set; }

	/// <summary>
	/// A <see cref="T:OceanBase.MySqlBulkLoaderPriority" /> giving the priority to load with (default <see cref="F:OceanBase.MySqlBulkLoaderPriority.None" />).
	/// </summary>
	public MySqlBulkLoaderPriority Priority { get; set; }

	/// <summary>
	/// A <see cref="T:System.IO.Stream" /> containing the data to load. Either this or <see cref="P:OceanBase.MySqlBulkLoader.FileName" /> must be set.
	/// The <see cref="P:OceanBase.MySqlBulkLoader.Local" /> property must be <c>true</c> if this is set.
	/// </summary>
	public Stream? SourceStream
	{
		get
		{
			return Source as Stream;
		}
		set
		{
			Source = value;
		}
	}

	/// <summary>
	/// The name of the table to load into. If this is a reserved word or contains spaces, it must be quoted.
	/// </summary>
	public string? TableName { get; set; }

	/// <summary>
	/// The timeout (in milliseconds) to use.
	/// </summary>
	public int Timeout { get; set; }

	internal object? Source { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="T:OceanBase.MySqlBulkLoader" /> class with the specified <see cref="T:OceanBase.MySqlConnection" />.
	/// </summary>
	/// <param name="connection">The <see cref="T:OceanBase.MySqlConnection" /> to use.</param>
	public MySqlBulkLoader(MySqlConnection connection)
	{
		Connection = connection;
		Local = true;
		Columns = new List<string>();
		Expressions = new List<string>();
	}

	/// <summary>
	/// Loads all data in the source file or stream into the destination table.
	/// </summary>
	/// <returns>The number of rows inserted.</returns>
	public int Load()
	{
		return LoadAsync(IOBehavior.Synchronous, CancellationToken.None).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Asynchronously loads all data in the source file or stream into the destination table.
	/// </summary>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> that will be completed with the number of rows inserted.</returns>
	public Task<int> LoadAsync()
	{
		return LoadAsync(IOBehavior.Asynchronous, CancellationToken.None).AsTask();
	}

	/// <summary>
	/// Asynchronously loads all data in the source file or stream into the destination table.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> that will be completed with the number of rows inserted.</returns>
	public Task<int> LoadAsync(CancellationToken cancellationToken)
	{
		return LoadAsync(IOBehavior.Asynchronous, cancellationToken).AsTask();
	}

	internal async ValueTask<int> LoadAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (Connection == null)
		{
			throw new InvalidOperationException("Connection not set");
		}
		if (string.IsNullOrWhiteSpace(TableName))
		{
			throw new InvalidOperationException("TableName is required.");
		}
		if (!string.IsNullOrWhiteSpace(FileName) && Source != null)
		{
			throw new InvalidOperationException("Exactly one of FileName or SourceStream must be set.");
		}
		if (!string.IsNullOrWhiteSpace(FileName))
		{
			if (Local)
			{
				string text = GenerateSourceFileName();
				AddSource(text, CreateFileStream(FileName));
				FileName = text;
			}
		}
		else
		{
			if (!Local)
			{
				throw new InvalidOperationException("Local must be true to use SourceStream.");
			}
			FileName = GenerateSourceFileName();
			AddSource(FileName, Source);
		}
		bool closeConnection = false;
		if (Connection.State != ConnectionState.Open)
		{
			closeConnection = true;
			Connection.Open();
		}
		bool closeStream = SourceStream != null;
		try
		{
			if (Local && !Connection.AllowLoadLocalInfile)
			{
				throw new NotSupportedException("To use MySqlBulkLoader.Local=true, set AllowLoadLocalInfile=true in the connection string. See https://mysqlconnector.net/load-data");
			}
			using MySqlCommand cmd = new MySqlCommand(CreateSql(), Connection, Connection.CurrentTransaction)
			{
				AllowUserVariables = true,
				CommandTimeout = Timeout
			};
			int result = await cmd.ExecuteNonQueryAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			closeStream = false;
			return result;
		}
		finally
		{
			if (closeStream && TryGetAndRemoveSource(FileName, out object source))
			{
				((IDisposable)source).Dispose();
			}
			if (closeConnection)
			{
				Connection.Close();
			}
		}
		static void AddSource(string name, object value)
		{
			lock (s_lock)
			{
				s_sources.Add(name, value);
			}
		}
	}

	private string CreateSql()
	{
		StringBuilder stringBuilder = new StringBuilder("LOAD DATA ");
		StringBuilder stringBuilder2 = stringBuilder;
		stringBuilder2.Append(Priority switch
		{
			MySqlBulkLoaderPriority.Low => "LOW_PRIORITY ", 
			MySqlBulkLoaderPriority.Concurrent => "CONCURRENT ", 
			_ => "", 
		});
		if (Local)
		{
			stringBuilder.Append("LOCAL ");
		}
		stringBuilder.Append("INFILE '" + MySqlHelper.EscapeString(FileName) + "' ");
		stringBuilder2 = stringBuilder;
		stringBuilder2.Append(ConflictOption switch
		{
			MySqlBulkLoaderConflictOption.Replace => "REPLACE ", 
			MySqlBulkLoaderConflictOption.Ignore => "IGNORE ", 
			_ => "", 
		});
		stringBuilder.Append("INTO TABLE " + TableName + " ");
		if (CharacterSet != null)
		{
			stringBuilder.Append("CHARACTER SET " + CharacterSet + " ");
		}
		string text = ((FieldTerminator == null) ? "" : ("TERMINATED BY '" + MySqlHelper.EscapeString(FieldTerminator) + "' "));
		string text2 = ((FieldQuotationCharacter == '\0') ? "" : ((FieldQuotationOptional ? "OPTIONALLY " : "") + "ENCLOSED BY '" + MySqlHelper.EscapeString(FieldQuotationCharacter.ToString()) + "' "));
		string text3 = ((EscapeCharacter == '\0') ? "" : ("ESCAPED BY '" + MySqlHelper.EscapeString(EscapeCharacter.ToString()) + "' "));
		if (text.Length + text2.Length + text3.Length > 0)
		{
			stringBuilder.Append("FIELDS " + text + text2 + text3);
		}
		string text4 = ((LineTerminator == null) ? "" : ("TERMINATED BY '" + MySqlHelper.EscapeString(LineTerminator) + "' "));
		string text5 = ((LinePrefix == null) ? "" : ("STARTING BY '" + MySqlHelper.EscapeString(LinePrefix) + "' "));
		if (text4.Length + text5.Length > 0)
		{
			stringBuilder.Append("LINES " + text4 + text5);
		}
		stringBuilder.AppendFormat(CultureInfo.InvariantCulture, "IGNORE {0} LINES ", NumberOfLinesToSkip);
		if (Columns.Count > 0)
		{
			stringBuilder.Append("(" + string.Join(",", Columns) + ") ");
		}
		if (Expressions.Count > 0)
		{
			stringBuilder.Append("SET " + string.Join(",", Expressions));
		}
		stringBuilder.Append(';');
		return stringBuilder.ToString();
	}

	private static FileStream CreateFileStream(string fileName)
	{
		try
		{
			return File.OpenRead(fileName);
		}
		catch (Exception innerException)
		{
			throw new MySqlException("Could not access file \"" + fileName + "\"", innerException);
		}
	}

	internal static object GetAndRemoveSource(string sourceKey)
	{
		lock (s_lock)
		{
			object result = s_sources[sourceKey];
			s_sources.Remove(sourceKey);
			return result;
		}
	}

	internal static bool TryGetAndRemoveSource(string sourceKey, [NotNullWhen(true)] out object? source)
	{
		lock (s_lock)
		{
			if (s_sources.TryGetValue(sourceKey, out source))
			{
				return s_sources.Remove(sourceKey);
			}
		}
		return false;
	}

	private static string GenerateSourceFileName()
	{
		return ":SOURCE:" + Guid.NewGuid().ToString("N");
	}
}
