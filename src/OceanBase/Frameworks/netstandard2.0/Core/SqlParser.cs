using System;

namespace OceanBase.Core;

internal abstract class SqlParser(StatementPreparer preparer)
{
	[Flags]
	protected enum FinalParseStates
	{
		None = 0,
		/// <summary>
		/// The statement is complete (apart from potentially needing a semicolon or newline).
		/// </summary>
		Complete = 1,
		/// <summary>
		/// The statement needs a newline (e.g., to terminate a final comment).
		/// </summary>
		NeedsNewline = 2,
		/// <summary>
		/// The statement needs a semicolon (if another statement is going to be concatenated to it).
		/// </summary>
		NeedsSemicolon = 4
	}

	private enum State
	{
		Beginning,
		Statement,
		SingleQuotedString,
		SingleQuotedStringBackslash,
		SingleQuotedStringSingleQuote,
		DoubleQuotedString,
		DoubleQuotedStringBackslash,
		DoubleQuotedStringDoubleQuote,
		BacktickQuotedString,
		BacktickQuotedStringBacktick,
		EndOfLineComment,
		Hyphen,
		SecondHyphen,
		ForwardSlash,
		CStyleComment,
		CStyleCommentAsterisk,
		QuestionMark,
		AtSign,
		NamedParameter,
		OracleQQuoteString,
		OracleQQuoteCloseDelimiter
	}

	protected StatementPreparer Preparer { get; } = preparer;

	/// <summary>
	/// Returns <c>true</c> if a semicolon at <paramref name="index" /> should be treated as a statement delimiter.
	/// Derived parsers can override this to support dialects where semicolons may appear within a single logical statement
	/// (e.g., Oracle anonymous PL blocks).
	/// </summary>
	protected virtual bool IsStatementDelimiter(string sql, int index)
	{
		return true;
	}

	public void Parse(string sql)
	{
		ExceptionExtensions.ThrowIfNull(sql, "sql");
		OnBeforeParse(sql);
		int num = -1;
		bool flag = (Preparer.Options & StatementPreparerOptions.NoBackslashEscapes) == StatementPreparerOptions.NoBackslashEscapes;
		bool isOracleMode = Preparer.IsOracleMode;
		char closeDelimiter = '\0';
		State state = State.Beginning;
		State state2 = State.Beginning;
		bool flag2 = false;
		for (int i = 0; i < sql.Length; i++)
		{
			char c = sql[i];
			switch (state)
			{
			case State.EndOfLineComment:
				if (c == '\n')
				{
					state = state2;
				}
				break;
			case State.CStyleComment:
				if (c == '*')
				{
					state = State.CStyleCommentAsterisk;
				}
				break;
			case State.CStyleCommentAsterisk:
				state = ((c == '/') ? state2 : State.CStyleComment);
				break;
			case State.OracleQQuoteString:
				if (c == closeDelimiter)
				{
					state = State.OracleQQuoteCloseDelimiter;
				}
				break;
			case State.OracleQQuoteCloseDelimiter:
				state = ((c == '\'') ? State.Statement : ((c != closeDelimiter) ? State.OracleQQuoteString : State.OracleQQuoteCloseDelimiter));
				break;
			case State.SingleQuotedString:
				switch (c)
				{
				case '\'':
					state = State.SingleQuotedStringSingleQuote;
					break;
				case '\\':
					if (!flag)
					{
						state = State.SingleQuotedStringBackslash;
					}
					break;
				}
				break;
			case State.SingleQuotedStringBackslash:
				state = State.SingleQuotedString;
				break;
			case State.DoubleQuotedString:
				switch (c)
				{
				case '"':
					state = State.DoubleQuotedStringDoubleQuote;
					break;
				case '\\':
					if (!flag)
					{
						state = State.DoubleQuotedStringBackslash;
					}
					break;
				}
				break;
			case State.DoubleQuotedStringBackslash:
				state = State.DoubleQuotedString;
				break;
			case State.BacktickQuotedString:
				if (c == '`')
				{
					state = State.BacktickQuotedStringBacktick;
				}
				break;
			case State.SingleQuotedStringSingleQuote:
				if (c == '\'')
				{
					state = State.SingleQuotedString;
					break;
				}
				if (flag2)
				{
					OnNamedParameter(num, i - num);
				}
				if (c == ';' && IsStatementDelimiter(sql, i))
				{
					OnStatementEnd(i);
					state = State.Beginning;
				}
				else
				{
					state = State.Statement;
				}
				break;
			case State.DoubleQuotedStringDoubleQuote:
				if (c == '"')
				{
					state = State.DoubleQuotedString;
					break;
				}
				if (flag2)
				{
					OnNamedParameter(num, i - num);
				}
				if (c == ';' && IsStatementDelimiter(sql, i))
				{
					OnStatementEnd(i);
					state = State.Beginning;
				}
				else
				{
					state = State.Statement;
				}
				break;
			case State.BacktickQuotedStringBacktick:
				if (c == '`')
				{
					state = State.BacktickQuotedString;
					break;
				}
				if (flag2)
				{
					OnNamedParameter(num, i - num);
				}
				if (c == ';' && IsStatementDelimiter(sql, i))
				{
					OnStatementEnd(i);
					state = State.Beginning;
				}
				else
				{
					state = State.Statement;
				}
				break;
			case State.SecondHyphen:
				state = ((c != ' ') ? State.Statement : State.EndOfLineComment);
				break;
			case State.Hyphen:
				state = ((c != '-') ? State.Statement : State.SecondHyphen);
				break;
			case State.ForwardSlash:
				state = ((c != '*') ? State.Statement : State.CStyleComment);
				break;
			case State.QuestionMark:
				if (IsVariableName(c))
				{
					state = State.NamedParameter;
					break;
				}
				OnPositionalParameter(num);
				if (c == ';' && IsStatementDelimiter(sql, i))
				{
					OnStatementEnd(i);
					state = State.Beginning;
				}
				else
				{
					state = State.Statement;
				}
				break;
			case State.AtSign:
				if (IsVariableName(c))
				{
					state = State.NamedParameter;
					break;
				}
				switch (c)
				{
				case '`':
					state = State.BacktickQuotedString;
					flag2 = true;
					break;
				case '"':
					state = State.DoubleQuotedString;
					flag2 = true;
					break;
				case '\'':
					state = State.SingleQuotedString;
					flag2 = true;
					break;
				default:
					state = State.Statement;
					break;
				}
				break;
			case State.NamedParameter:
				if (!IsVariableName(c))
				{
					OnNamedParameter(num, i - num);
					if (c == ';' && IsStatementDelimiter(sql, i))
					{
						OnStatementEnd(i);
						state = State.Beginning;
					}
					else
					{
						state = State.Statement;
					}
				}
				break;
			default:
				throw new InvalidOperationException($"Unexpected state: {state}");
			case State.Beginning:
			case State.Statement:
				if (c == '-' && i < sql.Length - 2 && sql[i + 1] == '-' && sql[i + 2] == ' ')
				{
					state2 = state;
					state = State.Hyphen;
					break;
				}
				if (c == '/' && i < sql.Length - 1 && sql[i + 1] == '*')
				{
					state2 = state;
					state = State.ForwardSlash;
					break;
				}
				switch (c)
				{
				case '\'':
					state = State.SingleQuotedString;
					break;
				case '"':
					state = State.DoubleQuotedString;
					break;
				case '`':
					state = State.BacktickQuotedString;
					break;
				default:
					{
						if (isOracleMode && (c == 'q' || c == 'Q') && i + 2 < sql.Length && sql[i + 1] == '\'' && TryGetOracleQQuoteCloseDelimiter(sql[i + 2], out closeDelimiter))
						{
							if (state == State.Beginning)
							{
								state = State.Statement;
								OnStatementBegin(i);
							}
							state = State.OracleQQuoteString;
							i += 2;
							break;
						}
						switch (c)
						{
						case '?':
							state = State.QuestionMark;
							num = i;
							goto end_IL_0402;
						case '@':
							state = State.AtSign;
							num = i;
							goto end_IL_0402;
						case ':':
							state = State.AtSign;
							num = i;
							goto end_IL_0402;
						case '#':
							state2 = state;
							state = State.EndOfLineComment;
							goto end_IL_0402;
						case ';':
							if (!IsStatementDelimiter(sql, i))
							{
								break;
							}
							if (state != State.Beginning)
							{
								OnStatementEnd(i);
							}
							state = State.Beginning;
							goto end_IL_0402;
						}
						if (!IsWhitespace(c) && state == State.Beginning)
						{
							state = State.Statement;
							OnStatementBegin(i);
						}
						break;
					}
					end_IL_0402:
					break;
				}
				break;
			}
		}
		FinalParseStates finalParseStates = FinalParseStates.None;
		if (state == State.NamedParameter)
		{
			OnNamedParameter(num, sql.Length - num);
			state = State.Statement;
		}
		else if (state == State.QuestionMark)
		{
			OnPositionalParameter(num);
			state = State.Statement;
		}
		else if (state == State.EndOfLineComment)
		{
			finalParseStates |= FinalParseStates.NeedsNewline;
			state = state2;
		}
		else if ((state == State.SingleQuotedStringSingleQuote || state == State.DoubleQuotedStringDoubleQuote || state == State.BacktickQuotedStringBacktick) ? true : false)
		{
			state = State.Statement;
		}
		else
		{
			switch (state)
			{
			case State.OracleQQuoteCloseDelimiter:
				state = State.Statement;
				break;
			case State.OracleQQuoteString:
				state = State.Statement;
				break;
			}
		}
		if (state == State.Statement)
		{
			OnStatementEnd(sql.Length);
			finalParseStates |= FinalParseStates.NeedsSemicolon;
			state = State.Beginning;
		}
		if (state == State.Beginning)
		{
			finalParseStates |= FinalParseStates.Complete;
		}
		OnParsed(finalParseStates);
	}

	protected virtual void OnBeforeParse(string sql)
	{
	}

	protected virtual void OnStatementBegin(int index)
	{
	}

	protected virtual void OnPositionalParameter(int index)
	{
	}

	protected virtual void OnNamedParameter(int index, int length)
	{
	}

	protected virtual void OnStatementEnd(int index)
	{
	}

	protected virtual void OnParsed(FinalParseStates states)
	{
	}

	private static bool IsWhitespace(char ch)
	{
		switch (ch)
		{
		case '\t':
		case '\n':
		case '\r':
		case ' ':
			return true;
		default:
			return false;
		}
	}

	private static bool IsVariableName(char ch)
	{
		if (ch >= 'a')
		{
			if (ch <= 'z' || ch >= '\u0080')
			{
				goto IL_003b;
			}
		}
		else if (ch >= 'A')
		{
			if (ch <= 'Z' || ch == '_')
			{
				goto IL_003b;
			}
		}
		else if (ch >= '0')
		{
			if (ch <= '9')
			{
				goto IL_003b;
			}
		}
		else if (ch == '$' || ch == '.')
		{
			goto IL_003b;
		}
		return false;
		IL_003b:
		return true;
	}

	private static bool TryGetOracleQQuoteCloseDelimiter(char openDelimiter, out char closeDelimiter)
	{
		switch (openDelimiter)
		{
		case '[':
			closeDelimiter = ']';
			return true;
		case '{':
			closeDelimiter = '}';
			return true;
		case '<':
			closeDelimiter = '>';
			return true;
		case '(':
			closeDelimiter = ')';
			return true;
		default:
			if (openDelimiter == '\'' || openDelimiter == ' ' || openDelimiter == '\t' || openDelimiter == '\r' || openDelimiter == '\n')
			{
				closeDelimiter = '\0';
				return false;
			}
			closeDelimiter = openDelimiter;
			return true;
		}
	}
}
