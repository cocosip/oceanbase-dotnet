using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OceanBase.Core;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;

namespace OceanBase;

/// <summary>
/// <see cref="T:OceanBase.OracleCommand" /> represents a SQL statement or stored procedure name
/// to execute against a MySQL database.
/// </summary>
public sealed class OracleCommand : DbCommand, IOracleCommand, ICommand, ICancellableCommand, ICloneable
{
	private static readonly Regex s_cStyleComments = new Regex("/\\*.*?\\*/", RegexOptions.Singleline);

	private static readonly Regex s_singleLineComments = new Regex("(^|\\s)--\\s.*?$", RegexOptions.Multiline);

	private static readonly Regex s_hashComments = new Regex("#.*?$", RegexOptions.Multiline);

	private readonly int m_commandId;

	private string m_commandText = "";

	private OracleConnection? m_connection;

	private CommandType m_commandType = CommandType.Text;

	private bool m_isDisposed;

	private OracleParameterCollection? m_parameterCollection;

	private MySqlAttributeCollection? m_attributeCollection;

	private int? m_commandTimeout;

	private CommandBehavior m_commandBehavior;

	private int m_arrayBindCount;

	private Action? m_cancelAction;

	private Action? m_cancelForCommandTimeoutAction;

	private uint m_cancelTimerId;

	private bool m_commandTimedOut;

	private HashSet<string>? m_preparedStatementsToClose;

	/// <summary>
	/// Gets or sets whether parameters are bound by name.
	/// </summary>
	/// <remarks>
	/// This is a compatibility property for ODP.NET consumers (e.g., SqlSugar's Oracle provider sets it to <c>true</c>).
	/// OceanBase.ManagedDataAccess binds Oracle-style parameters by name; setting this property currently does not change behavior.
	/// </remarks>
	public bool BindByName { get; set; } = true;

	/// <summary>
	/// Gets or sets the initial fetch size for LONG columns.
	/// </summary>
	/// <remarks>
	/// This is a compatibility property for ODP.NET consumers. LONG prefetch size is not currently used by the driver; the value is stored only.
	/// </remarks>
	public int InitialLONGFetchSize { get; set; } = -1;

	/// <summary>
	/// Gets or sets the number of rows to bind for Oracle-style ArrayBinding.
	/// A value of <c>0</c> disables ArrayBinding for this command.
	/// </summary>
	public int ArrayBindCount
	{
		get
		{
			return m_arrayBindCount;
		}
		set
		{
			if (value < 0)
			{
				throw new ArgumentOutOfRangeException("value", "ArrayBindCount must be greater than or equal to 0.");
			}
			m_arrayBindCount = value;
		}
	}

	/// <summary>
	/// Gets the per-row affected row counts returned by ArrayBinding execution.
	/// </summary>
	public long[]? ArrayBindRowsAffected { get; internal set; }

	/// <summary>
	/// The collection of <see cref="T:OceanBase.OracleParameter" /> objects for this command.
	/// </summary>
	public new OracleParameterCollection Parameters => m_parameterCollection ?? (m_parameterCollection = new OracleParameterCollection());

	OracleParameterCollection? IOracleCommand.RawParameters => m_parameterCollection;

	DbParameterCollection? ICommand.RawParameters => ((IOracleCommand)this).RawParameters;

	/// <summary>
	/// The collection of <see cref="T:OceanBase.MySqlAttribute" /> objects for this command.
	/// </summary>
	public MySqlAttributeCollection Attributes => m_attributeCollection ?? (m_attributeCollection = new MySqlAttributeCollection());

	MySqlAttributeCollection? ICommand.RawAttributes => m_attributeCollection;

	bool ICommand.AllowUserVariables => AllowUserVariables;

	internal bool AllowUserVariables { get; set; }

	internal bool NoActivity { get; set; }

	internal bool IsRefCursorFetchCommand { get; set; }

	internal int RefCursorRowsToFetchPerRoundTrip { get; set; }

	internal bool EnableStoredProcedureRefCursorResultSets { get; set; }

	internal bool CloseRefCursorResultSetAfterScalar { get; set; }

	/// <summary>
	/// Gets or sets the command text to execute.
	/// </summary>
	/// <remarks>If <see cref="P:OceanBase.OracleCommand.CommandType" /> is <see cref="F:System.Data.CommandType.Text" />, this is one or more SQL statements to execute.
	/// If <see cref="P:OceanBase.OracleCommand.CommandType" /> is <see cref="F:System.Data.CommandType.StoredProcedure" />, this is the name of the stored procedure; any
	/// special characters in the stored procedure name must be quoted or escaped.</remarks>
	public override string CommandText
	{
		get
		{
			return m_commandText;
		}
		[param: AllowNull]
		set
		{
			if (Connection?.ActiveCommandId == m_commandId)
			{
				throw new InvalidOperationException("Cannot set OracleCommand.CommandText when there is an open DataReader for this command; it must be closed first.");
			}
			TryCloseTrackedPreparedStatement(m_commandText);
			m_commandText = value ?? "";
		}
	}

	public bool IsPrepared => ((ICommand)this).TryGetPreparedStatements() != null;

	public new OracleTransaction? Transaction { get; set; }

	public new OracleConnection? Connection
	{
		get
		{
			return m_connection;
		}
		set
		{
			if (m_connection?.ActiveCommandId == m_commandId)
			{
				throw new InvalidOperationException("Cannot set MySqlCommand.Connection when there is an open DataReader for this command; it must be closed first.");
			}
			m_connection = value;
		}
	}

	/// <inheritdoc />
	public override int CommandTimeout
	{
		get
		{
			return Math.Min(m_commandTimeout ?? Connection?.DefaultCommandTimeout ?? 0, 2147483);
		}
		set
		{
			if (value < 0)
			{
				throw new ArgumentOutOfRangeException("value");
			}
			m_commandTimeout = value;
			((ICancellableCommand)this).EffectiveCommandTimeout = null;
		}
	}

	/// <inheritdoc />
	public override CommandType CommandType
	{
		get
		{
			return m_commandType;
		}
		set
		{
			if (value != CommandType.Text && value != CommandType.StoredProcedure)
			{
				throw new ArgumentException("CommandType must be Text or StoredProcedure.", "value");
			}
			if (m_commandType != value)
			{
				TryCloseTrackedPreparedStatement(CommandText);
			}
			m_commandType = value;
		}
	}

	/// <inheritdoc />
	public override bool DesignTimeVisible { get; set; }

	/// <inheritdoc />
	public override UpdateRowSource UpdatedRowSource { get; set; }

	/// <summary>
	/// Holds the first automatically-generated ID for a value inserted in an <c>AUTO_INCREMENT</c> column in the last statement.
	/// </summary>
	/// <remarks>
	/// See <a href="https://dev.mysql.com/doc/refman/8.0/en/information-functions.html#function_last-insert-id"><c>LAST_INSERT_ID()</c></a> for more information.
	/// </remarks>
	public long LastInsertedId { get; private set; }

	long ICommand.LastInsertedId => LastInsertedId;

	protected override DbConnection? DbConnection
	{
		get
		{
			return Connection;
		}
		set
		{
			Connection = (OracleConnection)value;
		}
	}

	protected override DbParameterCollection DbParameterCollection => Parameters;

	protected override DbTransaction? DbTransaction
	{
		get
		{
			return Transaction;
		}
		set
		{
			Transaction = (OracleTransaction)value;
		}
	}

	bool ICancellableCommand.IsTimedOut => Volatile.Read(ref m_commandTimedOut);

	int ICancellableCommand.CommandId => m_commandId;

	int? ICancellableCommand.EffectiveCommandTimeout { get; set; }

	int ICancellableCommand.CancelAttemptCount { get; set; }

	ICancellableCommand ICommand.CancellableCommand => this;

	private IOBehavior AsyncIOBehavior => Connection?.AsyncIOBehavior ?? IOBehavior.Asynchronous;

	CommandBehavior ICommand.CommandBehavior => m_commandBehavior;

	OracleParameterCollection? IOracleCommand.OutParameters { get; set; }

	DbParameterCollection? ICommand.OutParameters
	{
		get
		{
			return ((IOracleCommand)this).OutParameters;
		}
		set
		{
			((IOracleCommand)this).OutParameters = (OracleParameterCollection)value;
		}
	}

	OracleParameter? IOracleCommand.ReturnParameter { get; set; }

	DbParameter? ICommand.ReturnParameter
	{
		get
		{
			return ((IOracleCommand)this).ReturnParameter;
		}
		set
		{
			((IOracleCommand)this).ReturnParameter = (OracleParameter)value;
		}
	}

	ILogger ICommand.Logger => Connection.LoggingConfiguration.CommandLogger;

	OracleConnection? IOracleCommand.Connection => Connection;

	DbConnection? ICommand.Connection => ((IOracleCommand)this).Connection;

	ServerSession? ICancellableCommand.Session => Connection?.Session;

	/// <summary>
	/// Initializes a new instance of the <see cref="T:OceanBase.OracleCommand" /> class.
	/// </summary>
	public OracleCommand()
		: this(null, null, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="T:OceanBase.OracleCommand" /> class, setting <see cref="P:OceanBase.OracleCommand.CommandText" /> to <paramref name="commandText" />.
	/// </summary>
	/// <param name="commandText">The text to assign to <see cref="P:OceanBase.OracleCommand.CommandText" />.</param>
	public OracleCommand(string? commandText)
		: this(commandText, null, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="T:OceanBase.OracleCommand" /> class with the specified <see cref="T:OceanBase.OracleConnection" /> and <see cref="T:OceanBase.OracleTransaction" />.
	/// </summary>
	/// <param name="connection">The <see cref="T:OceanBase.OracleConnection" /> to use.</param>
	/// <param name="transaction">The active <see cref="T:OceanBase.OracleTransaction" />, if any.</param>
	public OracleCommand(OracleConnection? connection, OracleTransaction? transaction)
		: this(null, connection, transaction)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="T:OceanBase.OracleCommand" /> class with the specified command text and <see cref="T:OceanBase.OracleConnection" />.
	/// </summary>
	/// <param name="commandText">The text to assign to <see cref="P:OceanBase.OracleCommand.CommandText" />.</param>
	/// <param name="connection">The <see cref="T:OceanBase.OracleConnection" /> to use.</param>
	public OracleCommand(string? commandText, OracleConnection? connection)
		: this(commandText, connection, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="T:OceanBase.OracleCommand" /> class with the specified command text,<see cref="T:OceanBase.OracleConnection" />, and <see cref="T:OceanBase.OracleTransaction" />.
	/// </summary>
	/// <param name="commandText">The text to assign to <see cref="P:OceanBase.OracleCommand.CommandText" />.</param>
	/// <param name="connection">The <see cref="T:OceanBase.OracleConnection" /> to use.</param>
	/// <param name="transaction">The active <see cref="T:OceanBase.OracleTransaction" />, if any.</param>
	public OracleCommand(string? commandText, OracleConnection? connection, OracleTransaction? transaction)
	{
		GC.SuppressFinalize(this);
		m_commandId = ICancellableCommandExtensions.GetNextId();
		CommandText = commandText ?? "";
		Connection = connection;
		Transaction = transaction;
		CommandType = CommandType.Text;
	}

	private OracleCommand(OracleCommand other)
		: this(other.CommandText, other.Connection, other.Transaction)
	{
		GC.SuppressFinalize(this);
		m_commandTimeout = other.m_commandTimeout;
		((ICancellableCommand)this).EffectiveCommandTimeout = null;
		CommandType = other.CommandType;
		DesignTimeVisible = other.DesignTimeVisible;
		UpdatedRowSource = other.UpdatedRowSource;
		m_arrayBindCount = other.m_arrayBindCount;
		ArrayBindRowsAffected = other.ArrayBindRowsAffected;
		m_parameterCollection = other.CloneRawParameters();
		m_attributeCollection = other.CloneRawAttributes();
	}

	public new OracleParameter CreateParameter()
	{
		return (OracleParameter)base.CreateParameter();
	}

	/// <inheritdoc />
	public override void Cancel()
	{
		Connection?.Cancel(this, m_commandId, isCancel: true);
	}

	/// <summary>
	/// Executes this command on the associated <see cref="T:OceanBase.MySqlConnection" />.
	/// </summary>
	/// <returns>The number of rows affected.</returns>
	/// <remarks>For UPDATE, INSERT, and DELETE statements, the return value is the number of rows affected by the command.
	/// For stored procedures, the return value is the number of rows affected by the last statement in the stored procedure,
	/// or zero if the last statement is a SELECT. For all other types of statements, the return value is -1.</remarks>
	public override int ExecuteNonQuery()
	{
		return ExecuteNonQueryAsync(IOBehavior.Synchronous, CancellationToken.None).Result;
	}

	public override object? ExecuteScalar()
	{
		return ExecuteScalarAsync(IOBehavior.Synchronous, CancellationToken.None).Result;
	}

	public new OracleDataReader ExecuteReader()
	{
		return ExecuteReaderAsync(CommandBehavior.Default, IOBehavior.Synchronous, default(CancellationToken)).Result;
	}

	public new OracleDataReader ExecuteReader(CommandBehavior commandBehavior)
	{
		return ExecuteReaderAsync(commandBehavior, IOBehavior.Synchronous, default(CancellationToken)).GetAwaiter().GetResult();
	}

	/// <inheritdoc />
	public override void Prepare()
	{
		if (!NeedsPrepare(out Exception exception))
		{
			if (exception != null)
			{
				throw exception;
			}
			TrackPreparedStatementForDispose();
		}
		else
		{
			Connection.Session.PrepareAsync(this, IOBehavior.Synchronous, default(CancellationToken)).GetAwaiter().GetResult();
			TrackPreparedStatementForDispose();
		}
	}

	public Task PrepareAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return PrepareAsync(AsyncIOBehavior, cancellationToken);
	}

	internal OracleParameterCollection? CloneRawParameters()
	{
		if (m_parameterCollection == null)
		{
			return null;
		}
		OracleParameterCollection oracleParameterCollection = new OracleParameterCollection();
		foreach (OracleParameter item in (IEnumerable<OracleParameter>)m_parameterCollection)
		{
			oracleParameterCollection.Add(item.Clone());
		}
		return oracleParameterCollection;
	}

	private MySqlAttributeCollection? CloneRawAttributes()
	{
		if (m_attributeCollection == null)
		{
			return null;
		}
		MySqlAttributeCollection mySqlAttributeCollection = new MySqlAttributeCollection();
		foreach (MySqlAttribute item in m_attributeCollection)
		{
			mySqlAttributeCollection.Add(new MySqlAttribute(item.AttributeName, item.Value));
		}
		return mySqlAttributeCollection;
	}

	private Task PrepareAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (!NeedsPrepare(out Exception exception))
		{
			if (exception == null)
			{
				TrackPreparedStatementForDispose();
			}
			if (exception != null)
			{
				return Task.FromException(exception);
			}
			return Task.CompletedTask;
		}
		return DoPrepareAsync(ioBehavior, cancellationToken);
	}

	private async Task DoPrepareAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		await Connection.Session.PrepareAsync(this, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		TrackPreparedStatementForDispose();
	}

	private bool NeedsPrepare(out Exception? exception)
	{
		exception = null;
		if (Connection == null)
		{
			exception = new InvalidOperationException("Connection property must be non-null.");
		}
		else if (Connection.State != ConnectionState.Open)
		{
			exception = new InvalidOperationException($"Connection must be Open; current state is {Connection.State}");
		}
		else if (string.IsNullOrWhiteSpace(CommandText))
		{
			exception = new InvalidOperationException("CommandText must be specified");
		}
		else if (Connection?.HasActiveReader ?? false)
		{
			exception = new InvalidOperationException("Cannot call Prepare when there is an open DataReader for this command's connection; it must be closed first.");
		}
		if (exception != null || Connection.IgnorePrepare)
		{
			return false;
		}
		CommandType commandType = CommandType;
		if (commandType != CommandType.StoredProcedure && commandType != CommandType.Text)
		{
			exception = new NotSupportedException("Only CommandType.Text and CommandType.StoredProcedure are currently supported by OracleCommand.Prepare.");
			return false;
		}
		return Connection.Session.TryGetPreparedStatement(CommandText) == null;
	}

	void ICommand.SetLastInsertedId(long lastInsertedId)
	{
		LastInsertedId = lastInsertedId;
	}

	protected override DbParameter CreateDbParameter()
	{
		return new OracleParameter();
	}

	protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
	{
		return ExecuteReaderAsync(behavior, IOBehavior.Synchronous, CancellationToken.None).Result;
	}

	/// <summary>
	/// Executes this command asynchronously on the associated <see cref="T:OceanBase.MySqlConnection" />.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>For UPDATE, INSERT, and DELETE statements, the return value is the number of rows affected by the command.
	/// For stored procedures, the return value is the number of rows affected by the last statement in the stored procedure,
	/// or zero if the last statement is a SELECT. For all other types of statements, the return value is -1.</remarks>
	public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
	{
		return ExecuteNonQueryAsync(AsyncIOBehavior, cancellationToken).AsTask();
	}

	internal async ValueTask<int> ExecuteNonQueryAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		Volatile.Write(ref m_commandTimedOut, value: false);
		this.ResetCommandTimeout();
		EnableStoredProcedureRefCursorResultSets = false;
		using (((ICancellableCommand)this).RegisterCancel(cancellationToken))
		{
			if (RequiresNationalCharacterArrayBindingFallback())
			{
				return await ExecuteNationalCharacterArrayBindingFallbackAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			using OracleDataReader reader = await ExecuteReaderNoResetTimeoutAsync(CommandBehavior.Default, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			while (await reader.ReadAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false) || await reader.NextResultAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
			{
			}
			return reader.RecordsAffected;
		}
	}

	private bool RequiresNationalCharacterArrayBindingFallback()
	{
		if (ArrayBindCount > 0)
		{
			Encoding encoding = Connection?.Session.CharacterEncoding;
			if (encoding != null && !encoding.Equals(Encoding.UTF8) && m_parameterCollection != null)
			{
				for (int i = 0; i < m_parameterCollection.Count; i++)
				{
					OracleDbType oracleDbType = m_parameterCollection[i].OracleDbType;
					if ((oracleDbType == OracleDbType.NChar || oracleDbType == OracleDbType.NVarchar2) ? true : false)
					{
						return true;
					}
				}
				return false;
			}
		}
		return false;
	}

	private async ValueTask<int> ExecuteNationalCharacterArrayBindingFallbackAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		SingleCommandPayloadCreator.ValidateOracleArrayBindingParameters(new CommandFacade(this));
		ValidateArrayBindingCharacterValues();
		long[] rowsAffected = (ArrayBindRowsAffected = new long[ArrayBindCount]);
		int totalRowsAffected = 0;
		for (int rowIndex = 0; rowIndex < ArrayBindCount; rowIndex++)
		{
			using OracleCommand scalarCommand = new OracleCommand(CommandText, Connection, Transaction)
			{
				BindByName = BindByName,
				CommandTimeout = CommandTimeout,
				CommandType = CommandType
			};
			for (int i = 0; i < m_parameterCollection.Count; i++)
			{
				OracleParameter oracleParameter = m_parameterCollection[i];
				OracleParameter oracleParameter2 = oracleParameter.Clone();
				object value = ((Array)oracleParameter.Value).GetValue(rowIndex);
				oracleParameter2.Value = ((value == null || value == DBNull.Value) ? DBNull.Value : value);
				scalarCommand.Parameters.Add(oracleParameter2);
			}
			try
			{
				int num = await scalarCommand.ExecuteNonQueryAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				rowsAffected[rowIndex] = num;
				totalRowsAffected += num;
			}
			catch
			{
				rowsAffected[rowIndex] = 0L;
				throw;
			}
		}
		return totalRowsAffected;
	}

	private void ValidateArrayBindingCharacterValues()
	{
		Encoding encoding = Connection.Session.CharacterEncoding ?? Encoding.UTF8;
		for (int i = 0; i < m_parameterCollection.Count; i++)
		{
			OracleParameter oracleParameter = m_parameterCollection[i];
			OracleDbType oracleDbType = oracleParameter.OracleDbType;
			bool flag = ((oracleDbType == OracleDbType.Char || oracleDbType == OracleDbType.Varchar2) ? true : false);
			if (!flag || !(oracleParameter.Value is Array array))
			{
				continue;
			}
			for (int j = 0; j < array.Length; j++)
			{
				object value = array.GetValue(j);
				if (value != null && value != DBNull.Value)
				{
					encoding.GetByteCount(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
				}
			}
		}
	}

	public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
	{
		return ExecuteScalarAsync(AsyncIOBehavior, cancellationToken).AsTask();
	}

	internal async ValueTask<object?> ExecuteScalarAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		Volatile.Write(ref m_commandTimedOut, value: false);
		this.ResetCommandTimeout();
		EnableStoredProcedureRefCursorResultSets = ShouldEnableStoredProcedureRefCursorResultSets();
		using (((ICancellableCommand)this).RegisterCancel(cancellationToken))
		{
			if (EnableStoredProcedureRefCursorResultSets)
			{
				return await ExecuteScalarWithStoredProcedureRefCursorResultSetsAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			return await ExecuteScalarAndDrainAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private async ValueTask<object?> ExecuteScalarAndDrainAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		bool hasSetResult = false;
		object result = null;
		using OracleDataReader reader = await ExecuteReaderNoResetTimeoutAsync(CommandBehavior.Default, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		do
		{
			bool flag = await reader.ReadAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!hasSetResult)
			{
				if (flag)
				{
					result = reader.GetValue(0);
				}
				hasSetResult = true;
			}
		}
		while (await reader.NextResultAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		return result;
	}

	private async ValueTask<object?> ExecuteScalarWithStoredProcedureRefCursorResultSetsAsync(IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		int previousRowsToFetchPerRoundTrip = RefCursorRowsToFetchPerRoundTrip;
		bool previousCloseAfterScalar = CloseRefCursorResultSetAfterScalar;
		RefCursorRowsToFetchPerRoundTrip = 1;
		CloseRefCursorResultSetAfterScalar = true;
		try
		{
			using OracleDataReader reader = await ExecuteReaderNoResetTimeoutAsync(CommandBehavior.Default, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return (await reader.ReadAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) ? reader.GetValue(0) : null;
		}
		finally
		{
			RefCursorRowsToFetchPerRoundTrip = previousRowsToFetchPerRoundTrip;
			CloseRefCursorResultSetAfterScalar = previousCloseAfterScalar;
		}
	}

	public new Task<OracleDataReader> ExecuteReaderAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return ExecuteReaderAsync(CommandBehavior.Default, AsyncIOBehavior, cancellationToken).AsTask();
	}

	public new Task<OracleDataReader> ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken = default(CancellationToken))
	{
		return ExecuteReaderAsync(behavior, AsyncIOBehavior, cancellationToken).AsTask();
	}

	protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
	{
		return await ExecuteReaderAsync(behavior, AsyncIOBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	internal async ValueTask<OracleDataReader> ExecuteReaderAsync(CommandBehavior behavior, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		Volatile.Write(ref m_commandTimedOut, value: false);
		this.ResetCommandTimeout();
		EnableStoredProcedureRefCursorResultSets = ShouldEnableStoredProcedureRefCursorResultSets();
		using (((ICancellableCommand)this).RegisterCancel(cancellationToken))
		{
			return await ExecuteReaderNoResetTimeoutAsync(behavior, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private bool ShouldEnableStoredProcedureRefCursorResultSets()
	{
		bool? bIsDefineInSelect = null;
		bool? bIsDmlReturn = null;
		if (CommandType != CommandType.StoredProcedure)
		{
			if (CommandType == CommandType.Text)
			{
				return ResultSet.GetSqlStatementType(CommandText, ref bIsDefineInSelect, ref bIsDmlReturn) == SqlStatementType.PLSQL;
			}
			return false;
		}
		return true;
	}

	internal ValueTask<OracleDataReader> ExecuteReaderNoResetTimeoutAsync(CommandBehavior behavior, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (Connection != null && Connection.CurrentTransaction != null && Transaction == null)
		{
			Transaction = Connection.CurrentTransaction;
		}
		if (!IsValid(out Exception exception))
		{
			return ValueTaskExtensions.FromException<OracleDataReader>(exception);
		}
		Activity activity = (NoActivity ? null : Connection.Session.StartActivity("Execute", "db.statement", CommandText));
		m_commandBehavior = behavior;
		return CommandExecutor.ExecuteOracleReaderAsync(new CommandListPosition(this), SingleCommandPayloadCreator.Instance, behavior, activity, ioBehavior, cancellationToken);
	}

	public OracleCommand Clone()
	{
		return new OracleCommand(this);
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			TryCloseTrackedPreparedStatementsOnDispose();
		}
		m_isDisposed = true;
		base.Dispose(disposing);
	}

	public Task DisposeAsync()
	{
		Dispose();
		return Task.CompletedTask;
	}

	/// <summary>
	/// Registers <see cref="M:OceanBase.OracleCommand.Cancel" /> as a callback with <paramref name="cancellationToken" /> if cancellation is supported.
	/// </summary>
	/// <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" />.</param>
	/// <returns>An object that must be disposed to revoke the cancellation registration.</returns>
	/// <remarks>This method is more efficient than calling <code>token.Register(Command.Cancel)</code> because it avoids
	/// unnecessary allocations.</remarks>
	CancellationTokenRegistration ICancellableCommand.RegisterCancel(CancellationToken cancellationToken)
	{
		if (!cancellationToken.CanBeCanceled)
		{
			return default(CancellationTokenRegistration);
		}
		if (m_cancelAction == null)
		{
			m_cancelAction = Cancel;
		}
		return cancellationToken.Register(m_cancelAction);
	}

	void ICancellableCommand.SetTimeout(int milliseconds)
	{
		if (m_cancelTimerId != 0)
		{
			TimerQueue.Instance.Remove(m_cancelTimerId);
		}
		if (milliseconds != int.MaxValue)
		{
			if (m_cancelForCommandTimeoutAction == null)
			{
				m_cancelForCommandTimeoutAction = CancelCommandForTimeout;
			}
			m_cancelTimerId = TimerQueue.Instance.Add(milliseconds, m_cancelForCommandTimeoutAction);
		}
	}

	private void CancelCommandForTimeout()
	{
		Volatile.Write(ref m_commandTimedOut, value: true);
		Connection?.Cancel(this, m_commandId, isCancel: false);
	}

	private bool IsValid([NotNullWhen(false)] out Exception? exception)
	{
		exception = null;
		if (m_isDisposed)
		{
			exception = new ObjectDisposedException(GetType().Name);
		}
		else if (Connection == null)
		{
			exception = new InvalidOperationException("Connection property must be non-null.");
		}
		else
		{
			ConnectionState state = Connection.State;
			if (state != ConnectionState.Open && state != ConnectionState.Connecting)
			{
				exception = new InvalidOperationException($"Connection must be Open; current state is {Connection.State}");
			}
			else if (!Connection.IgnoreCommandTransaction && Transaction != Connection.CurrentTransaction)
			{
				exception = new InvalidOperationException("The transaction associated with this command is not the connection's active transaction; see https://mysqlconnector.net/trans");
			}
			else if (string.IsNullOrWhiteSpace(CommandText))
			{
				exception = new InvalidOperationException("CommandText must be specified");
			}
			else if (string.IsNullOrWhiteSpace(StripComments(CommandText)))
			{
				exception = new InvalidOperationException("Invalid SQL statement.");
			}
		}
		return exception == null;
	}

	private void TrackPreparedStatementForDispose()
	{
		OracleConnection? connection = Connection;
		if (connection != null && connection.AutoClosePreparedStatements && !string.IsNullOrWhiteSpace(CommandText) && IsPrepared && !ShouldUseProcedureScopedPreparedStatementLifecycle())
		{
			(m_preparedStatementsToClose ?? (m_preparedStatementsToClose = new HashSet<string>(StringComparer.Ordinal))).Add(CommandText);
		}
	}

	private bool ShouldUseProcedureScopedPreparedStatementLifecycle()
	{
		if (CommandType == CommandType.StoredProcedure)
		{
			return true;
		}
		if (CommandType != CommandType.Text)
		{
			return false;
		}
		bool? bIsDefineInSelect = null;
		bool? bIsDmlReturn = null;
		return ResultSet.GetSqlStatementType(CommandText, ref bIsDefineInSelect, ref bIsDmlReturn) == SqlStatementType.PLSQL;
	}

	private void TryCloseTrackedPreparedStatementsOnDispose()
	{
		HashSet<string> preparedStatementsToClose = m_preparedStatementsToClose;
		if (preparedStatementsToClose == null || preparedStatementsToClose.Count <= 0)
		{
			return;
		}
		OracleConnection connection = Connection;
		if (connection == null || connection.State != ConnectionState.Open)
		{
			return;
		}
		ServerSession session = connection.Session;
		if (session == null || connection.ActiveCommandId == m_commandId)
		{
			return;
		}
		foreach (string item in preparedStatementsToClose)
		{
			session.TryClosePreparedStatementsNoThrow(item);
		}
		preparedStatementsToClose.Clear();
	}

	private void TryCloseTrackedPreparedStatement(string? commandText)
	{
		if (string.IsNullOrWhiteSpace(commandText))
		{
			return;
		}
		HashSet<string> preparedStatementsToClose = m_preparedStatementsToClose;
		if (preparedStatementsToClose == null || preparedStatementsToClose.Count <= 0 || !preparedStatementsToClose.Remove(commandText))
		{
			return;
		}
		OracleConnection connection = Connection;
		if (connection == null || connection.State != ConnectionState.Open)
		{
			return;
		}
		ServerSession session = connection.Session;
		if (session != null)
		{
			if (connection.ActiveCommandId == m_commandId)
			{
				preparedStatementsToClose.Add(commandText);
			}
			else
			{
				session.TryClosePreparedStatementsNoThrow(commandText);
			}
		}
	}

	/// <summary>
	/// Strips SQL comments from the command text.
	/// Removes -- single-line comments, /* */ multi-line comments, and # single-line comments.
	/// </summary>
	private static string StripComments(string sql)
	{
		if (string.IsNullOrEmpty(sql))
		{
			return sql;
		}
		sql = s_cStyleComments.Replace(sql, "");
		sql = s_singleLineComments.Replace(sql, "");
		sql = s_hashComments.Replace(sql, "");
		return sql;
	}

	PreparedStatements? ICommand.TryGetPreparedStatements()
	{
		if (string.IsNullOrWhiteSpace(CommandText) || Connection == null || Connection.State != ConnectionState.Open)
		{
			return null;
		}
		return Connection.Session.TryGetPreparedStatement(CommandText);
	}
}
