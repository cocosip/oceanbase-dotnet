using System;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OceanBase.Core;
using OceanBase.Protocol.Serialization;

namespace OceanBase;

public sealed class OracleCommandBuilder : DbCommandBuilder
{
	public new OracleDataAdapter? DataAdapter
	{
		get
		{
			return (OracleDataAdapter)base.DataAdapter;
		}
		set
		{
			base.DataAdapter = value;
		}
	}

	public static void DeriveParameters(OracleCommand command)
	{
		DeriveParametersAsync(IOBehavior.Synchronous, command, CancellationToken.None).GetAwaiter().GetResult();
	}

	public static Task DeriveParametersAsync(OracleCommand command)
	{
		return DeriveParametersAsync(command?.Connection?.AsyncIOBehavior ?? IOBehavior.Asynchronous, command, CancellationToken.None);
	}

	public static Task DeriveParametersAsync(OracleCommand command, CancellationToken cancellationToken)
	{
		return DeriveParametersAsync(command?.Connection?.AsyncIOBehavior ?? IOBehavior.Asynchronous, command, cancellationToken);
	}

	private static async Task DeriveParametersAsync(IOBehavior ioBehavior, OracleCommand command, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(command, "command");
		if (command.CommandType != CommandType.StoredProcedure)
		{
			throw new ArgumentException($"OracleCommand.CommandType must be StoredProcedure not {command.CommandType}", "command");
		}
		if (string.IsNullOrWhiteSpace(command.CommandText))
		{
			throw new ArgumentException("OracleCommand.CommandText must be set to a stored procedure name", "command");
		}
		OracleConnection? connection = command.Connection;
		if (connection == null || connection.State != ConnectionState.Open)
		{
			throw new ArgumentException("OracleCommand.Connection must be an open connection.", "command");
		}
		if (command.Connection.Session.ServerVersion.Version < ServerVersions.SupportsProcedureCache)
		{
			throw new NotSupportedException("Oracle Server " + command.Connection.Session.ServerVersion.OriginalString + " doesn't support procedure metadata.");
		}
		CachedProcedure cachedProcedure = await command.Connection.GetCachedProcedure(command.CommandText, revalidateMissing: true, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (cachedProcedure == null)
		{
			NormalizedSchema normalizedSchema = NormalizedSchema.MustNormalize(command.CommandText, command.Connection.Database, isOracleMode: true);
			throw new MySqlException($"Procedure or function '{normalizedSchema.Component}' cannot be found in database '{normalizedSchema.Schema}'.");
		}
		if (cachedProcedure.Parameters.Count == 0)
		{
			NormalizedSchema normalized = NormalizedSchema.MustNormalize(command.CommandText, command.Connection.Database, isOracleMode: true);
			if (!(await CachedProcedure.CheckProcedureExistsAsync(ioBehavior, command.Connection, normalized.Schema, normalized.Component, normalized.PackageName, ((ICommand)command).Logger, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)))
			{
				throw new MySqlException($"OBE-06564:Procedure or function '{normalized.Component}' cannot be found in schema '{normalized.Schema}'.");
			}
		}
		command.Parameters.Clear();
		foreach (CachedParameter parameter in cachedProcedure.Parameters)
		{
			OracleParameter oracleParameter = command.Parameters.Add(":" + parameter.Name, parameter.OracleDbType);
			oracleParameter.Direction = parameter.Direction;
			oracleParameter.Size = parameter.Length;
			if (!string.IsNullOrEmpty(parameter.UdtTypeName))
			{
				oracleParameter.UdtTypeName = parameter.UdtTypeName;
			}
		}
	}

	public OracleCommandBuilder()
	{
		GC.SuppressFinalize(this);
		QuotePrefix = "\"";
		QuoteSuffix = "\"";
	}

	public OracleCommandBuilder(OracleDataAdapter dataAdapter)
		: this()
	{
		DataAdapter = dataAdapter;
	}

	public new OracleCommand GetDeleteCommand()
	{
		return (OracleCommand)base.GetDeleteCommand();
	}

	public new OracleCommand GetInsertCommand()
	{
		return (OracleCommand)base.GetInsertCommand();
	}

	public new OracleCommand GetUpdateCommand()
	{
		return (OracleCommand)base.GetUpdateCommand();
	}

	protected override void ApplyParameterInfo(DbParameter parameter, DataRow row, StatementType statementType, bool whereClause)
	{
		((OracleParameter)parameter).OracleDbType = (OracleDbType)row[SchemaTableColumn.ProviderType];
	}

	protected override string GetParameterName(int parameterOrdinal)
	{
		IFormatProvider invariantCulture = CultureInfo.InvariantCulture;
		DefaultInterpolatedStringHandler handler = new DefaultInterpolatedStringHandler(2, 1, invariantCulture);
		handler.AppendLiteral(":p");
		handler.AppendFormatted(parameterOrdinal);
		return string.Create(invariantCulture, ref handler);
	}

	protected override string GetParameterName(string parameterName)
	{
		return ":" + parameterName;
	}

	protected override string GetParameterPlaceholder(int parameterOrdinal)
	{
		return GetParameterName(parameterOrdinal);
	}

	protected override void SetRowUpdatingHandler(DbDataAdapter adapter)
	{
		if (!(adapter is OracleDataAdapter oracleDataAdapter))
		{
			throw new ArgumentException("adapter needs to be a OracleDataAdapter", "adapter");
		}
		if (adapter == DataAdapter)
		{
			oracleDataAdapter.RowUpdating -= RowUpdatingHandler;
		}
		else
		{
			oracleDataAdapter.RowUpdating += RowUpdatingHandler;
		}
	}

	public override string QuoteIdentifier(string unquotedIdentifier)
	{
		if (string.IsNullOrEmpty(unquotedIdentifier))
		{
			return unquotedIdentifier;
		}
		return QuotePrefix + unquotedIdentifier.Replace(QuotePrefix, QuotePrefix + QuotePrefix) + QuoteSuffix;
	}

	public override string UnquoteIdentifier(string quotedIdentifier)
	{
		if (string.IsNullOrEmpty(quotedIdentifier))
		{
			return quotedIdentifier;
		}
		if (quotedIdentifier.Length >= 2 && quotedIdentifier[0] == '"')
		{
			string text = quotedIdentifier;
			if (text[text.Length - 1] == '"')
			{
				string text2 = quotedIdentifier;
				quotedIdentifier = text2.Substring(1, text2.Length - 1 - 1);
			}
		}
		return quotedIdentifier.Replace("\"\"", "\"");
	}

	private void RowUpdatingHandler(object sender, OracleRowUpdatingEventArgs e)
	{
		RowUpdatingHandler(e);
	}
}
