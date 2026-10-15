using System;
using System.CodeDom.Compiler;
using System.Text.RegularExpressions;

namespace OceanBase.Core;

internal sealed class NormalizedSchema
{
	private const string ReQuoted = "`((?:[^`]|``)+)`";

	private const string ReUnQuoted = "([^\\.`]+)";

	private const string ReEither = "(?:`((?:[^`]|``)+)`|([^\\.`]+))";

	private const string ReName = "^\\s*(?:`((?:[^`]|``)+)`|([^\\.`]+))\\s*(?:\\.\\s*(?:`((?:[^`]|``)+)`|([^\\.`]+))\\s*)?$";

	private const string ReQuotedOracle = "\"((?:[^\"]|\"\")+)\"";

	private const string ReUnQuotedOracle = "([^\\.\"]+)";

	private const string ReEitherOracle = "(?:\"((?:[^\"]|\"\")+)\"|([^\\.\"]+))";

	private const string ReNameOracle = "^\\s*(?:\"((?:[^\"]|\"\")+)\"|([^\\.\"]+))\\s*(?:\\.\\s*(?:\"((?:[^\"]|\"\")+)\"|([^\\.\"]+))\\s*(?:\\.\\s*(?:\"((?:[^\"]|\"\")+)\"|([^\\.\"]+))\\s*)?)?$";

	private const string ReQuotedUnified = "([`\"])((?:(?!\\1).|\\1\\1)*)\\1";

	private const string ReUnQuotedUnified = "([^\\.`\"]+)";

	private const string ReEitherUnified = "(?:([`\"])((?:(?!\\1).|\\1\\1)*)\\1|([^\\.`\"]+))";

	private const string ReNameUnified = "^\\s*(?:([`\"])((?:(?!\\1).|\\1\\1)*)\\1|([^\\.`\"]+))\\s*(?:\\.\\s*(?:([`\"])((?:(?!\\1).|\\1\\1)*)\\1|([^\\.`\"]+))\\s*(?:\\.\\s*(?:([`\"])((?:(?!\\1).|\\1\\1)*)\\1|([^\\.`\"]+))\\s*)?)?$";

	public string? Schema { get; }

	public string? Component { get; }

	public string? PackageName { get; }

	public bool IsOracleMode { get; private set; }

	public string FullyQualified
	{
		get
		{
			if (string.IsNullOrEmpty(PackageName))
			{
				return $"`{Schema}`.`{Component}`";
			}
			return $"`{Schema}`.`{PackageName}`.`{Component}`";
		}
	}

	/// <remarks>
	/// Pattern:<br />
	/// <code>^\\s*(?:`((?:[^`]|``)+)`|([^\\.`]+))\\s*(?:\\.\\s*(?:`((?:[^`]|``)+)`|([^\\.`]+))\\s*)?$</code><br />
	/// Explanation:<br />
	/// <code>
	/// ○ Match if at the beginning of the string.<br />
	/// ○ Match a whitespace character greedily any number of times.<br />
	/// ○ Match with 2 alternative expressions.<br />
	///     ○ Match a sequence of expressions.<br />
	///         ○ Match '`'.<br />
	///         ○ 1st capture group.<br />
	///             ○ Loop greedily at least once.<br />
	///                 ○ Match with 2 alternative expressions.<br />
	///                     ○ Match any character other than '`'.<br />
	///                     ○ Match the string "``".<br />
	///         ○ Match '`'.<br />
	///     ○ 2nd capture group.<br />
	///         ○ Match a character in the set [^.`] greedily at least once.<br />
	/// ○ Match a whitespace character greedily any number of times.<br />
	/// ○ Optional (greedy).<br />
	///     ○ Match '.'.<br />
	///     ○ Match a whitespace character greedily any number of times.<br />
	///     ○ Match with 2 alternative expressions.<br />
	///         ○ Match a sequence of expressions.<br />
	///             ○ Match '`'.<br />
	///             ○ 3rd capture group.<br />
	///                 ○ Loop greedily at least once.<br />
	///                     ○ Match with 2 alternative expressions.<br />
	///                         ○ Match any character other than '`'.<br />
	///                         ○ Match the string "``".<br />
	///             ○ Match '`'.<br />
	///         ○ 4th capture group.<br />
	///             ○ Match a character in the set [^.`] greedily at least once.<br />
	///     ○ Match a whitespace character greedily any number of times.<br />
	/// ○ Match if at the end of the string or if before an ending newline.<br />
	/// </code>
	/// </remarks>
	private static Regex NameRegex() => new Regex("^\\s*(?:`((?:[^`]|``)+)`|([^\\.`]+))\\s*(?:\\.\\s*(?:`((?:[^`]|``)+)`|([^\\.`]+))\\s*)?$", RegexOptions.Compiled);

	/// <remarks>
	/// Pattern:<br />
	/// <code>^\\s*(?:"((?:[^"]|"")+)"|([^\\."]+))\\s*(?:\\.\\s*(?:"((?:[^"]|"")+)"|([^\\."]+))\\s*(?:\\.\\s*(?:"((?:[^"]|"")+)"|([^\\."]+))\\s*)?)?$</code><br />
	/// Explanation:<br />
	/// <code>
	/// ○ Match if at the beginning of the string.<br />
	/// ○ Match a whitespace character greedily any number of times.<br />
	/// ○ Match with 2 alternative expressions.<br />
	///     ○ Match a sequence of expressions.<br />
	///         ○ Match '"'.<br />
	///         ○ 1st capture group.<br />
	///             ○ Loop greedily at least once.<br />
	///                 ○ Match with 2 alternative expressions.<br />
	///                     ○ Match any character other than '"'.<br />
	///                     ○ Match the string "\"\"".<br />
	///         ○ Match '"'.<br />
	///     ○ 2nd capture group.<br />
	///         ○ Match a character in the set [^".] greedily at least once.<br />
	/// ○ Match a whitespace character greedily any number of times.<br />
	/// ○ Optional (greedy).<br />
	///     ○ Match '.'.<br />
	///     ○ Match a whitespace character greedily any number of times.<br />
	///     ○ Match with 2 alternative expressions.<br />
	///         ○ Match a sequence of expressions.<br />
	///             ○ Match '"'.<br />
	///             ○ 3rd capture group.<br />
	///                 ○ Loop greedily at least once.<br />
	///                     ○ Match with 2 alternative expressions.<br />
	///                         ○ Match any character other than '"'.<br />
	///                         ○ Match the string "\"\"".<br />
	///             ○ Match '"'.<br />
	///         ○ 4th capture group.<br />
	///             ○ Match a character in the set [^".] greedily at least once.<br />
	///     ○ Match a whitespace character greedily any number of times.<br />
	///     ○ Optional (greedy).<br />
	///         ○ Match '.'.<br />
	///         ○ Match a whitespace character greedily any number of times.<br />
	///         ○ Match with 2 alternative expressions.<br />
	///             ○ Match a sequence of expressions.<br />
	///                 ○ Match '"'.<br />
	///                 ○ 5th capture group.<br />
	///                     ○ Loop greedily at least once.<br />
	///                         ○ Match with 2 alternative expressions.<br />
	///                             ○ Match any character other than '"'.<br />
	///                             ○ Match the string "\"\"".<br />
	///                 ○ Match '"'.<br />
	///             ○ 6th capture group.<br />
	///                 ○ Match a character in the set [^".] greedily at least once.<br />
	///         ○ Match a whitespace character greedily any number of times.<br />
	/// ○ Match if at the end of the string or if before an ending newline.<br />
	/// </code>
	/// </remarks>
	private static Regex NameRegexOracle() => new Regex("^\\s*(?:\"((?:[^\"]|\"\")+)\"|([^\\.\"]+))\\s*(?:\\.\\s*(?:\"((?:[^\"]|\"\")+)\"|([^\\.\"]+))\\s*(?:\\.\\s*(?:\"((?:[^\"]|\"\")+)\"|([^\\.\"]+))\\s*)?)?$", RegexOptions.Compiled);

	/// <remarks>
	/// Pattern:<br />
	/// <code>^\\s*(?:([`"])((?:(?!\\1).|\\1\\1)*)\\1|([^\\.`"]+))\\s*(?:\\.\\s*(?:([`"])((?:(?!\\1).|\\1\\1)*)\\1|([^\\.`"]+))\\s*(?:\\.\\s*(?:([`"])((?:(?!\\1).|\\1\\1)*)\\1|([^\\.`"]+))\\s*)?)?$</code><br />
	/// Explanation:<br />
	/// <code>
	/// ○ Match if at the beginning of the string.<br />
	/// ○ Match a whitespace character greedily any number of times.<br />
	/// ○ Match with 2 alternative expressions.<br />
	///     ○ Match a sequence of expressions.<br />
	///         ○ 1st capture group.<br />
	///             ○ Match a character in the set ["`].<br />
	///         ○ 2nd capture group.<br />
	///             ○ Loop greedily any number of times.<br />
	///                 ○ Match with 2 alternative expressions.<br />
	///                     ○ Match a sequence of expressions.<br />
	///                         ○ Zero-width negative lookahead.<br />
	///                             ○ Match the same text as matched by the 1st capture group.<br />
	///                         ○ Match any character other than '\n'.<br />
	///                     ○ Match a sequence of expressions.<br />
	///                         ○ Match the same text as matched by the 1st capture group.<br />
	///                         ○ Match the same text as matched by the 1st capture group.<br />
	///         ○ Match the same text as matched by the 1st capture group.<br />
	///     ○ 3rd capture group.<br />
	///         ○ Match a character in the set [^".`] greedily at least once.<br />
	/// ○ Match a whitespace character greedily any number of times.<br />
	/// ○ Optional (greedy).<br />
	///     ○ Match '.'.<br />
	///     ○ Match a whitespace character greedily any number of times.<br />
	///     ○ Match with 2 alternative expressions.<br />
	///         ○ Match a sequence of expressions.<br />
	///             ○ 4th capture group.<br />
	///                 ○ Match a character in the set ["`].<br />
	///             ○ 5th capture group.<br />
	///                 ○ Loop greedily any number of times.<br />
	///                     ○ Match with 2 alternative expressions.<br />
	///                         ○ Match a sequence of expressions.<br />
	///                             ○ Zero-width negative lookahead.<br />
	///                                 ○ Match the same text as matched by the 1st capture group.<br />
	///                             ○ Match any character other than '\n'.<br />
	///                         ○ Match a sequence of expressions.<br />
	///                             ○ Match the same text as matched by the 1st capture group.<br />
	///                             ○ Match the same text as matched by the 1st capture group.<br />
	///             ○ Match the same text as matched by the 1st capture group.<br />
	///         ○ 6th capture group.<br />
	///             ○ Match a character in the set [^".`] greedily at least once.<br />
	///     ○ Match a whitespace character greedily any number of times.<br />
	///     ○ Optional (greedy).<br />
	///         ○ Match '.'.<br />
	///         ○ Match a whitespace character greedily any number of times.<br />
	///         ○ Match with 2 alternative expressions.<br />
	///             ○ Match a sequence of expressions.<br />
	///                 ○ 7th capture group.<br />
	///                     ○ Match a character in the set ["`].<br />
	///                 ○ 8th capture group.<br />
	///                     ○ Loop greedily any number of times.<br />
	///                         ○ Match with 2 alternative expressions.<br />
	///                             ○ Match a sequence of expressions.<br />
	///                                 ○ Zero-width negative lookahead.<br />
	///                                     ○ Match the same text as matched by the 1st capture group.<br />
	///                                 ○ Match any character other than '\n'.<br />
	///                             ○ Match a sequence of expressions.<br />
	///                                 ○ Match the same text as matched by the 1st capture group.<br />
	///                                 ○ Match the same text as matched by the 1st capture group.<br />
	///                 ○ Match the same text as matched by the 1st capture group.<br />
	///             ○ 9th capture group.<br />
	///                 ○ Match a character in the set [^".`] greedily at least once.<br />
	///         ○ Match a whitespace character greedily any number of times.<br />
	/// ○ Match if at the end of the string or if before an ending newline.<br />
	/// </code>
	/// </remarks>
	private static Regex NameRegexUnified() => new Regex("^\\s*(?:([`\"])((?:(?!\\1).|\\1\\1)*)\\1|([^\\.`\"]+))\\s*(?:\\.\\s*(?:([`\"])((?:(?!\\1).|\\1\\1)*)\\1|([^\\.`\"]+))\\s*(?:\\.\\s*(?:([`\"])((?:(?!\\1).|\\1\\1)*)\\1|([^\\.`\"]+))\\s*)?)?$", RegexOptions.Compiled);

	public static NormalizedSchema MustNormalize(string name, string? defaultSchema = null, bool isOracleMode = false)
	{
		NormalizedSchema normalizedSchema = new NormalizedSchema(name, isOracleMode, defaultSchema);
		if (normalizedSchema != null)
		{
			if (normalizedSchema.Component != null)
			{
				if (normalizedSchema.Schema == null)
				{
					throw new ArgumentException("Could not determine schema", "defaultSchema");
				}
				return normalizedSchema;
			}
			throw new ArgumentException("Could not determine function/procedure name", "name");
		}
		global::_003CPrivateImplementationDetails_003E.ThrowSwitchExpressionException(normalizedSchema);
		NormalizedSchema result = default(NormalizedSchema);
		return result;
	}

	public NormalizedSchema(string name, bool isOracleMode, string? defaultSchema = null)
	{
		IsOracleMode = isOracleMode;
		if (!isOracleMode)
		{
			Match match = NameRegex().Match(name);
			if (match.Success)
			{
				if (match.Groups[3].Success)
				{
					Component = match.Groups[3].Value.Replace("``", "`").Trim();
				}
				else if (match.Groups[4].Success)
				{
					Component = match.Groups[4].Value.Trim();
				}
				string text = "";
				if (match.Groups[1].Success)
				{
					text = match.Groups[1].Value.Replace("``", "`").Trim();
				}
				else if (match.Groups[2].Success)
				{
					text = match.Groups[2].Value.Trim();
				}
				if (Component == null)
				{
					Component = text.Trim();
				}
				else
				{
					Schema = text.Trim();
				}
				if (Schema == null)
				{
					Schema = defaultSchema;
				}
			}
			return;
		}
		Match match2 = NameRegexOracle().Match(name);
		if (!match2.Success)
		{
			return;
		}
		string text2 = GetOraclePart(match2, 1, 2);
		string text3 = GetOraclePart(match2, 3, 4);
		string text4 = GetOraclePart(match2, 5, 6);
		string text5 = (string.IsNullOrWhiteSpace(defaultSchema) ? null : defaultSchema.Trim().ToUpperInvariant());
		if (text2 == null)
		{
			return;
		}
		if (text3 == null)
		{
			Schema = text5;
			Component = text2;
			PackageName = null;
		}
		else if (text4 == null)
		{
			if (text5 != null)
			{
				Schema = text5;
				PackageName = text2;
				Component = text3;
			}
			else
			{
				Schema = text2;
				PackageName = null;
				Component = text3;
			}
		}
		else
		{
			Schema = text2;
			PackageName = text3;
			Component = text4;
		}
		static string? GetOraclePart(Match match3, int quotedGroupIndex, int unquotedGroupIndex)
		{
			if (match3.Groups[quotedGroupIndex].Success)
			{
				return match3.Groups[quotedGroupIndex].Value.Replace("\"\"", "\"").Trim();
			}
			if (match3.Groups[unquotedGroupIndex].Success)
			{
				return match3.Groups[unquotedGroupIndex].Value.Trim().ToUpperInvariant();
			}
			return null;
		}
	}
}
