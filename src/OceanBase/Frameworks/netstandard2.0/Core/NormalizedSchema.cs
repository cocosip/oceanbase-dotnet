using System;
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

	private static readonly Regex s_nameRegexMySql = new Regex("^\\s*(?:`((?:[^`]|``)+)`|([^\\.`]+))\\s*(?:\\.\\s*(?:`((?:[^`]|``)+)`|([^\\.`]+))\\s*)?$", RegexOptions.Compiled);

	private static readonly Regex s_nameRegexOracle = new Regex("^\\s*(?:\"((?:[^\"]|\"\")+)\"|([^\\.\"]+))\\s*(?:\\.\\s*(?:\"((?:[^\"]|\"\")+)\"|([^\\.\"]+))\\s*(?:\\.\\s*(?:\"((?:[^\"]|\"\")+)\"|([^\\.\"]+))\\s*)?)?$", RegexOptions.Compiled);

	private static readonly Regex s_nameRegexUnified = new Regex("^\\s*(?:([`\"])((?:(?!\\1).|\\1\\1)*)\\1|([^\\.`\"]+))\\s*(?:\\.\\s*(?:([`\"])((?:(?!\\1).|\\1\\1)*)\\1|([^\\.`\"]+))\\s*(?:\\.\\s*(?:([`\"])((?:(?!\\1).|\\1\\1)*)\\1|([^\\.`\"]+))\\s*)?)?$", RegexOptions.Compiled);

	public string? Schema { get; }

	public string? Component { get; }

	public string? PackageName { get; }

	public bool IsOracleMode { get; private set; }

	public string FullyQualified
	{
		get
		{
			if (!string.IsNullOrEmpty(PackageName))
			{
				return "`" + Schema + "`.`" + PackageName + "`.`" + Component + "`";
			}
			return "`" + Schema + "`.`" + Component + "`";
		}
	}

	private static Regex NameRegex()
	{
		return s_nameRegexMySql;
	}

	private static Regex NameRegexOracle()
	{
		return s_nameRegexOracle;
	}

	private static Regex NameRegexUnified()
	{
		return s_nameRegexUnified;
	}

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
		global::_003CPrivateImplementationDetails_003E.ThrowInvalidOperationException();
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
