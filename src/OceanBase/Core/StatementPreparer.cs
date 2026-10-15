using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Core;

internal sealed class StatementPreparer(string commandText, DbParameterCollection? parameters, StatementPreparerOptions options)
{
	private sealed class ParameterSqlParser(StatementPreparer preparer, ByteBufferWriter writer) : SqlParser(preparer)
	{
		private int m_currentParameterIndex;

		private int m_lastIndex;

		public bool IsComplete { get; private set; }

		private ByteBufferWriter Writer { get; } = writer;

		protected override void OnNamedParameter(int index, int length)
		{
			int parameterIndex = base.Preparer.GetParameterIndex(base.Preparer.CommandText.Substring(index, length));
			if (parameterIndex != -1)
			{
				DoAppendParameter(parameterIndex, index, length);
			}
		}

		protected override void OnPositionalParameter(int index)
		{
			DoAppendParameter(m_currentParameterIndex, index, 1);
			m_currentParameterIndex++;
		}

		private void DoAppendParameter(int parameterIndex, int textIndex, int textLength)
		{
			Writer.Write(base.Preparer.CommandText, m_lastIndex, textIndex - m_lastIndex);
			DbParameter inputParameter = base.Preparer.GetInputParameter(parameterIndex);
			if (!(inputParameter is MySqlParameter mySqlParameter))
			{
				if (!(inputParameter is OracleParameter oracleParameter))
				{
					throw new NotSupportedException("Unsupported parameter type: " + inputParameter.GetType().Name);
				}
				oracleParameter.AppendSqlString(Writer, base.Preparer.Options, base.Preparer.m_session);
			}
			else
			{
				mySqlParameter.AppendSqlString(Writer, base.Preparer.Options);
			}
			m_lastIndex = textIndex + textLength;
		}

		protected override void OnParsed(FinalParseStates states)
		{
			Writer.Write(base.Preparer.CommandText, m_lastIndex, base.Preparer.CommandText.Length - m_lastIndex);
			if ((states & FinalParseStates.NeedsNewline) == FinalParseStates.NeedsNewline)
			{
				Writer.Write((byte)10);
			}
			if ((states & FinalParseStates.NeedsSemicolon) == FinalParseStates.NeedsSemicolon && (base.Preparer.Options & StatementPreparerOptions.AppendSemicolon) == StatementPreparerOptions.AppendSemicolon)
			{
				Writer.Write((byte)59);
			}
			IsComplete = (states & FinalParseStates.Complete) == FinalParseStates.Complete;
		}
	}

	private sealed class PreparedCommandSqlParser(StatementPreparer preparer, List<ParsedStatement> statements, List<int> statementStartEndIndexes, ByteBufferWriter writer) : SqlParser(preparer)
	{
		private int m_currentParameterIndex;

		private int m_lastIndex;

		private bool m_isOracleAnonymousBlock;

		private List<ParsedStatement> Statements { get; } = statements;

		private List<int> StatementStartEndIndexes { get; } = statementStartEndIndexes;

		private ByteBufferWriter Writer { get; } = writer;

		protected override void OnBeforeParse(string sql)
		{
			m_isOracleAnonymousBlock = base.Preparer.m_isOracleMode && StartsWithOracleAnonymousBlock(sql);
		}

		protected override bool IsStatementDelimiter(string sql, int index)
		{
			return !m_isOracleAnonymousBlock;
		}

		protected override void OnStatementBegin(int index)
		{
			Statements.Add(new ParsedStatement());
			StatementStartEndIndexes.Add(Writer.Position);
			Writer.Write((byte)22);
			m_lastIndex = index;
		}

		protected override void OnNamedParameter(int index, int length)
		{
			string parameterName = base.Preparer.CommandText.Substring(index, length);
			DoAppendParameter(parameterName, -1, index, length);
		}

		protected override void OnPositionalParameter(int index)
		{
			DoAppendParameter(null, m_currentParameterIndex, index, 1);
			m_currentParameterIndex++;
		}

		private void DoAppendParameter(string? parameterName, int parameterIndex, int textIndex, int textLength)
		{
			Writer.Write(base.Preparer.CommandText, m_lastIndex, textIndex - m_lastIndex);
			m_lastIndex = textIndex + textLength;
			Writer.Write((byte)63);
			List<ParsedStatement> statements = Statements;
			statements[statements.Count - 1].ParameterNames.Add(parameterName);
			string item = null;
			if (parameterName != null)
			{
				item = ((!base.Preparer.m_isOracleMode) ? MySqlParameter.NormalizeParameterName(parameterName) : OracleParameter.NormalizeParameterName(parameterName));
			}
			List<ParsedStatement> statements2 = Statements;
			statements2[statements2.Count - 1].NormalizedParameterNames.Add(item);
			List<ParsedStatement> statements3 = Statements;
			statements3[statements3.Count - 1].ParameterIndexes.Add(parameterIndex);
		}

		protected override void OnStatementEnd(int index)
		{
			Writer.Write(base.Preparer.CommandText, m_lastIndex, index - m_lastIndex);
			m_lastIndex = index;
			StatementStartEndIndexes.Add(Writer.Position);
		}
	}

	private readonly bool m_isOracleMode = parameters is OracleParameterCollection;

	private readonly ServerSession? m_session;

	public StatementPreparerOptions Options { get; } = options;

	internal bool IsOracleMode => m_isOracleMode;

	private string CommandText { get; } = commandText;

	internal StatementPreparer(string commandText, DbParameterCollection? parameters, StatementPreparerOptions options, bool? isOracleMode = null, ServerSession? session = null)
		: this(commandText, parameters, options)
	{
		m_isOracleMode = isOracleMode ?? (parameters is OracleParameterCollection);
		m_session = session;
	}

	public ParsedStatements SplitStatements()
	{
		List<ParsedStatement> list = new List<ParsedStatement>();
		List<int> list2 = new List<int>();
		Encoding textEncoding = Encoding.UTF8;
		ServerSession? session = m_session;
		if (session != null && session.UseOracleMode && m_session.CharacterEncoding != null)
		{
			textEncoding = m_session.CharacterEncoding;
		}
		ByteBufferWriter byteBufferWriter = new ByteBufferWriter(CommandText.Length + 1, textEncoding);
		new PreparedCommandSqlParser(this, list, list2, byteBufferWriter).Parse(CommandText);
		for (int i = 0; i < list.Count; i++)
		{
			ParsedStatement parsedStatement = list[i];
			ArraySegment<byte> arraySegment = byteBufferWriter.ArraySegment;
			int num = list2[i * 2];
			parsedStatement.StatementBytes = arraySegment.Slice(num, list2[i * 2 + 1] - num);
		}
		return new ParsedStatements(list, byteBufferWriter.ToPayloadData());
	}

	public bool ParseAndBindParameters(ByteBufferWriter writer)
	{
		if (!string.IsNullOrWhiteSpace(CommandText))
		{
			ParameterSqlParser parameterSqlParser = new ParameterSqlParser(this, writer);
			parameterSqlParser.Parse(CommandText);
			return parameterSqlParser.IsComplete;
		}
		return true;
	}

	private int GetParameterIndex(string name)
	{
		DbParameterCollection dbParameterCollection = parameters;
		int num = ((dbParameterCollection is MySqlParameterCollection mySqlParameterCollection) ? mySqlParameterCollection.NormalizedIndexOf(name) : ((!(dbParameterCollection is OracleParameterCollection oracleParameterCollection)) ? (-1) : oracleParameterCollection.NormalizedIndexOf(name)));
		if (num == -1 && (Options & StatementPreparerOptions.AllowUserVariables) == 0)
		{
			throw new MySqlException("Parameter '" + name + "' must be defined. To use this as a variable, set 'Allow User Variables=true' in the connection string.");
		}
		return num;
	}

	private DbParameter GetInputParameter(int index)
	{
		if (index >= (parameters?.Count ?? 0))
		{
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(57, 3);
			defaultInterpolatedStringHandler.AppendLiteral("Parameter index ");
			defaultInterpolatedStringHandler.AppendFormatted(index);
			defaultInterpolatedStringHandler.AppendLiteral(" is invalid when only ");
			defaultInterpolatedStringHandler.AppendFormatted(parameters?.Count ?? 0);
			defaultInterpolatedStringHandler.AppendLiteral(" parameter");
			DbParameterCollection? dbParameterCollection = parameters;
			defaultInterpolatedStringHandler.AppendFormatted((dbParameterCollection != null && dbParameterCollection.Count == 1) ? " is" : "s are");
			defaultInterpolatedStringHandler.AppendLiteral(" defined.");
			throw new MySqlException(defaultInterpolatedStringHandler.ToStringAndClear());
		}
		DbParameter dbParameter = parameters[index];
		if (dbParameter.Direction != ParameterDirection.Input && (Options & StatementPreparerOptions.AllowOutputParameters) == 0)
		{
			throw new MySqlException("Only ParameterDirection.Input is supported when CommandType is Text (parameter name: " + dbParameter.ParameterName + ")");
		}
		return dbParameter;
	}

	private static bool StartsWithOracleAnonymousBlock(string sql)
	{
		int i = 0;
		while (true)
		{
			if (i < sql.Length && char.IsWhiteSpace(sql[i]))
			{
				i++;
				continue;
			}
			if (i >= sql.Length)
			{
				return false;
			}
			if (sql[i] == '-' && i + 2 < sql.Length && sql[i + 1] == '-' && sql[i + 2] == ' ')
			{
				for (i += 3; i < sql.Length && sql[i] != '\n'; i++)
				{
				}
				continue;
			}
			if (sql[i] == '#')
			{
				for (i++; i < sql.Length && sql[i] != '\n'; i++)
				{
				}
				continue;
			}
			if (sql[i] != '/' || i + 1 >= sql.Length || sql[i + 1] != '*')
			{
				break;
			}
			for (i += 2; i + 1 < sql.Length && (sql[i] != '*' || sql[i + 1] != '/'); i++)
			{
			}
			i = Math.Min(sql.Length, i + 2);
		}
		int num = i;
		for (; i < sql.Length && (char.IsLetter(sql[i]) || sql[i] == '_'); i++)
		{
		}
		if (i == num)
		{
			return false;
		}
		string text = sql.AsSpan(num, i - num).ToString();
		if (!text.Equals("BEGIN", StringComparison.OrdinalIgnoreCase))
		{
			return text.Equals("DECLARE", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}
}
