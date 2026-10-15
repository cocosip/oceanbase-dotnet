using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OceanBase.Logging;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Core;

internal sealed class CachedProcedure
{
	private static readonly IReadOnlyDictionary<string, string> s_typeMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		{ "BOOL", "TINYINT" },
		{ "BOOLEAN", "TINYINT" },
		{ "INTEGER", "INT" },
		{ "NUMERIC", "DECIMAL" },
		{ "FIXED", "DECIMAL" },
		{ "REAL", "DOUBLE" },
		{ "DOUBLE PRECISION", "DOUBLE" },
		{ "NVARCHAR", "VARCHAR" },
		{ "CHARACTER VARYING", "VARCHAR" },
		{ "NATIONAL VARCHAR", "VARCHAR" },
		{ "NCHAR", "CHAR" },
		{ "CHARACTER", "CHAR" },
		{ "NATIONAL CHAR", "CHAR" },
		{ "CHAR BYTE", "BINARY" }
	};

	private static readonly Regex s_cStyleComments = new Regex("/\\*.*?\\*/", RegexOptions.Singleline);

	private static readonly Regex s_singleLineComments = new Regex("(^|\\s)--.*?$", RegexOptions.Multiline);

	private static readonly Regex s_multipleSpaces = new Regex("\\s+");

	private static readonly Regex s_numericTypes = new Regex("(DECIMAL|DEC|FIXED|NUMERIC|FLOAT|DOUBLE PRECISION|DOUBLE|REAL)\\s*\\([0-9]+(,\\s*[0-9]+)\\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex s_enumOrSet = new Regex("(ENUM|SET)\\s*\\([^)]+\\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex s_parameterName = new Regex("^(?:`((?:[\\u0001-\\u005F\\u0061-\\uFFFF]+|``)+)`|([A-Za-z0-9$_\\u0080-\\uFFFF]+)) (.*)$");

	private static readonly Regex s_characterSet = new Regex(" (CHARSET|CHARACTER SET) [A-Za-z0-9_]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex s_collate = new Regex(" (COLLATE) [A-Za-z0-9_]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex s_length = new Regex("\\s*\\(\\s*([0-9]+)\\s*(?:,\\s*[0-9]+\\s*)?\\)");

	private readonly string m_schema;

	private readonly string m_component;

	public IReadOnlyList<CachedParameter> Parameters { get; }

	public bool HasOverloadVariants { get; }

	public bool SupportsAccurateParameterInfo { get; }

	private string FullyQualified => $"`{m_schema}`.`{m_component}`";

	public static async Task<CachedProcedure?> FillAsync(IOBehavior ioBehavior, MySqlConnection connection, string schema, string component, ILogger logger, CancellationToken cancellationToken)
	{
		if (!connection.Session.ProcAccessDenied && (connection.Session.ServerVersion.IsMariaDb || connection.Session.ServerVersion.Version < ServerVersions.RemovesMySqlProcTable))
		{
			try
			{
				using MySqlCommand cmd = connection.CreateCommand();
				cmd.Transaction = connection.CurrentTransaction;
				cmd.CommandText = "SELECT param_list, returns FROM mysql.proc WHERE db = @schema AND name = @component";
				cmd.Parameters.AddWithValue("@schema", schema);
				cmd.Parameters.AddWithValue("@component", component);
				using MySqlDataReader reader = await cmd.ExecuteReaderNoResetTimeoutAsync(CommandBehavior.Default, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (!(await reader.ReadAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)))
				{
					return null;
				}
				byte[] bytes = (byte[])reader.GetValue(0);
				string parametersSql = Encoding.UTF8.GetString(bytes);
				object value = reader.GetValue(1);
				string text2 = default(string);
				if (!(value is string text))
				{
					if (value != null)
					{
						text2 = Encoding.UTF8.GetString((byte[])value);
					}
					else
					{
						global::_003CPrivateImplementationDetails_003E.ThrowSwitchExpressionException(value);
					}
				}
				else
				{
					text2 = text;
				}
				string text3 = text2;
				List<CachedParameter> list = ParseParameters(parametersSql, connection.GuidFormat);
				if (text3.Length != 0)
				{
					bool unsigned;
					int length;
					string dataType = ParseDataType(text3, out unsigned, out length);
					list.Insert(0, CreateCachedParameter(0, null, "", dataType, unsigned, length, connection.GuidFormat, text3));
				}
				return new CachedProcedure(schema, component, list);
			}
			catch (MySqlException ex)
			{
				Log.FailedToRetrieveProcedureMetadata(logger, ex, connection.Session.Id, schema, component, ex.Message);
				if (ex.ErrorCode == MySqlErrorCode.TableAccessDenied)
				{
					connection.Session.ProcAccessDenied = true;
				}
			}
		}
		if (connection.Session.ServerVersion.Version < ServerVersions.SupportsProcedureCache)
		{
			Log.ServerDoesNotSupportCachedProcedures(logger, connection.Session.Id, connection.Session.ServerVersion.OriginalString);
			return null;
		}
		List<CachedParameter> parameters = new List<CachedParameter>();
		int routineCount;
		using (MySqlCommand cmd = connection.CreateCommand())
		{
			cmd.Transaction = connection.CurrentTransaction;
			cmd.CommandText = "SELECT COUNT(*)\n\t\t\t\tFROM information_schema.routines\n\t\t\t\tWHERE ROUTINE_SCHEMA = @schema AND ROUTINE_NAME = @component;\n\t\t\t\tSELECT ORDINAL_POSITION, PARAMETER_MODE, PARAMETER_NAME, DTD_IDENTIFIER\n\t\t\t\tFROM information_schema.parameters\n\t\t\t\tWHERE SPECIFIC_SCHEMA = @schema AND SPECIFIC_NAME = @component\n\t\t\t\tORDER BY ORDINAL_POSITION";
			cmd.Parameters.AddWithValue("@schema", schema);
			cmd.Parameters.AddWithValue("@component", component);
			using MySqlDataReader reader = await cmd.ExecuteReaderNoResetTimeoutAsync(CommandBehavior.Default, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await reader.ReadAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			routineCount = reader.GetInt32(0);
			await reader.NextResultAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			while (await reader.ReadAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
			{
				bool unsigned2;
				int length2;
				string dataType2 = ParseDataType(reader.GetString(3), out unsigned2, out length2);
				parameters.Add(new CachedParameter(reader.GetInt32(0), (!reader.IsDBNull(1)) ? reader.GetString(1) : null, (!reader.IsDBNull(2)) ? reader.GetString(2) : "", dataType2, unsigned2, length2, connection.GuidFormat));
			}
		}
		Log.ProcedureHasRoutineCount(logger, schema, component, routineCount, parameters.Count);
		return (routineCount == 0) ? null : new CachedProcedure(schema, component, parameters);
	}

	public static async Task<CachedProcedure?> FillAsync(IOBehavior ioBehavior, OracleConnection connection, string? schema, string component, string? packageName, ILogger logger, CancellationToken cancellationToken)
	{
		if (connection.Session.ServerVersion.Version < ServerVersions.SupportsProcedureCache)
		{
			Log.ServerDoesNotSupportCachedProcedures(logger, connection.Session.Id, connection.Session.ServerVersion.OriginalString);
			return null;
		}
		try
		{
			List<CachedParameter> parameters = new List<CachedParameter>();
			int routineCount = 0;
			bool hasOverload = false;
			HashSet<string?> overloads = new HashSet<string>();
			using (OracleCommand cmd = connection.CreateCommand())
			{
				cmd.Transaction = connection.CurrentTransaction;
				if (!string.IsNullOrEmpty(schema))
				{
					cmd.Parameters.AddWithValue("@schema", schema);
				}
				else if (!string.IsNullOrEmpty(packageName))
				{
					if (!packageName.Equals("DBMS_LOB", StringComparison.OrdinalIgnoreCase))
					{
						cmd.Parameters.AddWithValue("@schema", "SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')");
					}
					else
					{
						cmd.Parameters.AddWithValue("@schema", "SYS");
					}
				}
				else
				{
					cmd.Parameters.AddWithValue("@schema", "SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')");
				}
				cmd.Parameters.AddWithValue("@component", component);
				if (!string.IsNullOrEmpty(packageName))
				{
					cmd.Parameters.AddWithValue("@package", packageName);
				}
				else
				{
					cmd.Parameters.AddWithValue("@package", DBNull.Value);
				}
				string commandText = "SELECT ARGUMENT_NAME, IN_OUT, DATA_TYPE, DATA_PRECISION, DATA_SCALE, POSITION, DATA_LENGTH, OVERLOAD, TYPE_NAME, TYPE_OWNER FROM SYS.ALL_ARGUMENTS WHERE POSITION != 0 AND OBJECT_NAME = @component AND (PACKAGE_NAME = @package OR (@package IS NULL AND PACKAGE_NAME IS NULL)) AND OWNER = @schema ORDER BY POSITION, OVERLOAD";
				cmd.CommandText = commandText;
				List<(int Index, string? TypeName)> extTypeInfos = new List<(int, string)>();
				using OracleDataReader reader = await cmd.ExecuteReaderNoResetTimeoutAsync(CommandBehavior.Default, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				string name;
				string direction;
				string text;
				int ordinalPosition;
				int length;
				string text2;
				string udtTypeName;
				for (; await reader.ReadAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false); parameters.Add(new CachedParameter(ordinalPosition, direction, name, text, unsigned: false, length, text2, udtTypeName)))
				{
					name = ((!reader.IsDBNull(0)) ? reader.GetString(0) : "");
					direction = ((!reader.IsDBNull(1)) ? reader.GetString(1) : null);
					text = ((!reader.IsDBNull(2)) ? reader.GetString(2) : "OBJECT");
					if (!reader.IsDBNull(3))
					{
						reader.GetInt32(3);
					}
					if (!reader.IsDBNull(4))
					{
						reader.GetInt32(4);
					}
					ordinalPosition = ((!reader.IsDBNull(5)) ? reader.GetInt32(5) : 0);
					length = ((!reader.IsDBNull(6)) ? reader.GetInt32(6) : 0);
					text2 = ((!reader.IsDBNull(7)) ? reader.GetString(7) : null);
					string text3 = ((!reader.IsDBNull(8)) ? reader.GetString(8) : null);
					if (!reader.IsDBNull(9))
					{
						reader.GetString(9);
					}
					if (!string.IsNullOrEmpty(text2))
					{
						hasOverload = true;
						overloads.Add(text2);
					}
					else
					{
						overloads.Add(null);
					}
					udtTypeName = null;
					if (text == "EXT" && !string.IsNullOrEmpty(text3))
					{
						extTypeInfos.Add((parameters.Count, text3));
						udtTypeName = text3;
						continue;
					}
					switch (text)
					{
					case "OBJECT":
					case "VARRAY":
					case "COLLECTION":
						if (!string.IsNullOrEmpty(text3))
						{
							udtTypeName = text3;
							continue;
						}
						break;
					}
					if (!TypeMapper.Instance.TryGetOracleDbType(text, unsigned: false, length, out var _))
					{
						string text4 = ((!string.IsNullOrEmpty(text3)) ? text3 : text);
						if (!string.IsNullOrEmpty(text4))
						{
							extTypeInfos.Add((parameters.Count, text4));
							text = "EXT";
							udtTypeName = text4;
						}
					}
				}
				reader.Close();
				if (extTypeInfos.Count > 0)
				{
					foreach (var (index, value) in extTypeInfos)
					{
						using OracleCommand typeCmd = connection.CreateCommand();
						typeCmd.Transaction = connection.CurrentTransaction;
						typeCmd.CommandText = "SELECT TYPECODE FROM ALL_TYPES WHERE OWNER = @schema AND TYPE_NAME = @typename";
						typeCmd.Parameters.AddWithValue("@schema", schema ?? "SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')");
						typeCmd.Parameters.AddWithValue("@typename", value);
						object obj = await typeCmd.ExecuteScalarAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
						CachedParameter cachedParameter = parameters[index];
						string dataType = ((obj != null && obj != DBNull.Value) ? obj.ToString() : "ref cursor");
						string direction2 = ((cachedParameter.Direction == ParameterDirection.ReturnValue) ? null : ((cachedParameter.Direction == ParameterDirection.Input) ? "in" : ((cachedParameter.Direction == ParameterDirection.Output) ? "out" : "inout")));
						parameters[index] = new CachedParameter(cachedParameter.Position, direction2, cachedParameter.Name, dataType, unsigned: false, cachedParameter.Length, cachedParameter.Overload, cachedParameter.UdtTypeName);
					}
				}
			}
			string component2 = (string.IsNullOrEmpty(packageName) ? component : (packageName + "." + component));
			Log.ProcedureHasRoutineCount(logger, schema ?? string.Empty, component2, routineCount, parameters.Count);
			bool supportsAccurateParameterInfo = connection.Session.SupportsAccurateParameterInfo;
			bool hasOverloadVariants = hasOverload && overloads.Count > 1;
			return new CachedProcedure(schema ?? string.Empty, component2, parameters, hasOverloadVariants, supportsAccurateParameterInfo);
		}
		catch (MySqlException ex)
		{
			Log.FailedToRetrieveProcedureMetadata(logger, ex, connection.Session.Id, schema ?? string.Empty, component, ex.Message);
			if (ex.ErrorCode == MySqlErrorCode.TableAccessDenied)
			{
				connection.Session.ProcAccessDenied = true;
			}
		}
		return null;
	}

	/// <summary>
	/// Queries <c>SYS.ALL_PROCEDURES</c> to confirm whether a stored procedure/function actually exists
	/// when <c>ALL_ARGUMENTS</c> returns zero rows. This distinguishes "no-arg procedure" from "not found".
	/// </summary>
	/// <remarks>Only used by <see cref="M:OceanBase.OracleCommandBuilder.DeriveParameters(OceanBase.OracleCommand)" />; not on the normal execution path.</remarks>
	internal static async Task<bool> CheckProcedureExistsAsync(IOBehavior ioBehavior, OracleConnection connection, string schema, string component, string? packageName, ILogger logger, CancellationToken cancellationToken)
	{
		_ = 1;
		try
		{
			using OracleCommand cmd = connection.CreateCommand();
			cmd.Transaction = connection.CurrentTransaction;
			cmd.CommandText = "SELECT COUNT(*)\n\t\t\t\tFROM SYS.ALL_PROCEDURES\n\t\t\t\tWHERE OWNER = @schema\n\t\t\t\t  AND (\n\t\t\t\t    (@package IS NULL AND OBJECT_NAME = @component AND PROCEDURE_NAME IS NULL)\n\t\t\t\t    OR\n\t\t\t\t    (@package IS NOT NULL AND OBJECT_NAME = @package AND PROCEDURE_NAME = @component)\n\t\t\t\t  )";
			cmd.Parameters.AddWithValue("@schema", schema);
			cmd.Parameters.AddWithValue("@component", component);
			if (!string.IsNullOrEmpty(packageName))
			{
				cmd.Parameters.AddWithValue("@package", packageName);
			}
			else
			{
				cmd.Parameters.AddWithValue("@package", DBNull.Value);
			}
			using OracleDataReader reader = await cmd.ExecuteReaderNoResetTimeoutAsync(CommandBehavior.Default, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return await reader.ReadAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false) && reader.GetInt32(0) > 0;
		}
		catch (MySqlException ex)
		{
			Log.FailedToRetrieveProcedureMetadata(logger, ex, connection.Session.Id, schema, component, ex.Message);
			return false;
		}
	}

	private CachedProcedure(string schema, string component, IReadOnlyList<CachedParameter> parameters, bool hasOverloadVariants = false, bool supportsAccurateParameterInfo = false)
	{
		m_schema = schema;
		m_component = component;
		Parameters = parameters;
		HasOverloadVariants = hasOverloadVariants;
		SupportsAccurateParameterInfo = supportsAccurateParameterInfo;
	}

	internal MySqlParameterCollection AlignParamsWithDb(MySqlParameterCollection? parameterCollection)
	{
		MySqlParameterCollection mySqlParameterCollection = new MySqlParameterCollection();
		MySqlParameter mySqlParameter = parameterCollection?.FirstOrDefault((MySqlParameter x) => x.Direction == ParameterDirection.ReturnValue);
		foreach (CachedParameter parameter in Parameters)
		{
			MySqlParameter mySqlParameter2;
			if (parameter.Direction == ParameterDirection.ReturnValue)
			{
				mySqlParameter2 = mySqlParameter ?? throw new InvalidOperationException("Attempt to call stored function " + FullyQualified + " without specifying a return parameter");
			}
			else
			{
				int num = parameterCollection?.NormalizedIndexOf(parameter.Name) ?? (-1);
				if (num < 0)
				{
					throw new ArgumentException("Parameter '" + parameter.Name + "' not found in the collection.");
				}
				mySqlParameter2 = parameterCollection[num];
			}
			if (!mySqlParameter2.HasSetDirection)
			{
				mySqlParameter2.Direction = parameter.Direction;
			}
			if (!mySqlParameter2.HasSetDbType)
			{
				mySqlParameter2.MySqlDbType = parameter.MySqlDbType;
			}
			bool flag = mySqlParameter2.MySqlDbType == MySqlDbType.Guid;
			if (flag)
			{
				ParameterDirection direction = parameter.Direction;
				bool flag2 = (uint)(direction - 2) <= 1u;
				flag = flag2;
			}
			if (flag)
			{
				mySqlParameter2.Size = parameter.Length;
			}
			mySqlParameterCollection.Add(mySqlParameter2);
		}
		return mySqlParameterCollection;
	}

	internal OracleParameterCollection AlignParamsWithDb(OracleParameterCollection? parameterCollection)
	{
		OracleParameterCollection oracleParameterCollection = new OracleParameterCollection();
		OracleParameter oracleParameter = parameterCollection?.FirstOrDefault((OracleParameter x) => x.Direction == ParameterDirection.ReturnValue);
		foreach (CachedParameter parameter in Parameters)
		{
			OracleParameter oracleParameter2;
			if (parameter.Direction == ParameterDirection.ReturnValue)
			{
				oracleParameter2 = oracleParameter ?? throw new InvalidOperationException("Attempt to call stored function " + FullyQualified + " without specifying a return parameter");
			}
			else
			{
				int num = parameterCollection?.NormalizedIndexOf(parameter.Name) ?? (-1);
				if (num < 0)
				{
					throw new ArgumentException("Parameter '" + parameter.Name + "' not found in the collection.");
				}
				oracleParameter2 = parameterCollection[num];
			}
			if (!oracleParameter2.HasSetDirection)
			{
				oracleParameter2.Direction = parameter.Direction;
			}
			if (!oracleParameter2.HasSetDbType)
			{
				oracleParameter2.OracleDbType = parameter.OracleDbType;
			}
			oracleParameterCollection.Add(oracleParameter2);
		}
		return oracleParameterCollection;
	}

	/// <summary>
	/// Oracle 存储过程参数对齐（按 ALL_ARGUMENTS 反查结果）。
	/// 与 <see cref="M:OceanBase.Core.CachedProcedure.AlignParamsWithDb(OceanBase.OracleParameterCollection)" /> 的差异：
	///   1) 支持按 OVERLOAD 分组，先用本地参数个数过滤候选重载；
	///   2) 候选唯一时按 POSITION 顺序映射到本地参数（不依赖名字），并强制覆盖
	///      <see cref="P:OceanBase.OracleParameter.OracleDbType" /> 和 <see cref="P:OceanBase.OracleParameter.Direction" />，
	///      以纠正调用方误登记的 OUT 类型（参见 pe_overload/pl_overload 抓包分析）；
	///   3) 失败时静默保持原集合不动，绝不抛异常，避免破坏既有调用场景；
	///   4) 不处理 ReturnValue 形参（POSITION=0），交给调用方自行处理。
	/// 入参 <paramref name="parameterCollection" /> 来自调用方原始绑定，
	/// 返回的是同一个集合（就地修改），便于上游继续使用其引用。
	/// </summary>
	internal OracleParameterCollection? AlignOracleStoredProcedureParameters(OracleParameterCollection? parameterCollection)
	{
		if (parameterCollection == null || parameterCollection.Count == 0)
		{
			return parameterCollection;
		}
		if (Parameters.Count == 0)
		{
			return parameterCollection;
		}
		List<OracleParameter> list = new List<OracleParameter>(parameterCollection.Count);
		for (int i = 0; i < parameterCollection.Count; i++)
		{
			OracleParameter oracleParameter = parameterCollection[i];
			if (oracleParameter.Direction != ParameterDirection.ReturnValue)
			{
				list.Add(oracleParameter);
			}
		}
		if (list.Count == 0)
		{
			return parameterCollection;
		}
		Dictionary<string, List<CachedParameter>> dictionary = new Dictionary<string, List<CachedParameter>>(StringComparer.Ordinal);
		foreach (CachedParameter parameter in Parameters)
		{
			if (parameter.Position > 0)
			{
				string key = parameter.Overload ?? "";
				if (!dictionary.TryGetValue(key, out var value))
				{
					value = (dictionary[key] = new List<CachedParameter>());
				}
				value.Add(parameter);
			}
		}
		if (dictionary.Count == 0)
		{
			return parameterCollection;
		}
		List<List<CachedParameter>> list3 = new List<List<CachedParameter>>();
		foreach (KeyValuePair<string, List<CachedParameter>> item in dictionary)
		{
			if (item.Value.Count == list.Count)
			{
				list3.Add(item.Value);
			}
		}
		if (list3.Count == 0)
		{
			return parameterCollection;
		}
		List<CachedParameter> list4;
		bool flag;
		if (list3.Count == 1)
		{
			list4 = list3[0];
			flag = false;
		}
		else
		{
			int num = int.MinValue;
			List<CachedParameter> list5 = null;
			bool flag2 = false;
			foreach (List<CachedParameter> item2 in list3)
			{
				List<CachedParameter> list6 = item2.OrderBy((CachedParameter x) => x.Position).ToList();
				int num2 = 0;
				for (int num3 = 0; num3 < list6.Count; num3++)
				{
					OracleParameter oracleParameter2 = list[num3];
					CachedParameter cachedParameter = list6[num3];
					if (oracleParameter2.HasSetDbType && oracleParameter2.OracleDbType == cachedParameter.OracleDbType)
					{
						num2++;
					}
				}
				if (num2 > num)
				{
					num = num2;
					list5 = item2;
					flag2 = false;
				}
				else if (num2 == num)
				{
					flag2 = true;
				}
			}
			list4 = (flag2 ? null : list5);
			if (list4 == null)
			{
				return parameterCollection;
			}
			flag = true;
		}
		List<CachedParameter> list7 = list4.OrderBy((CachedParameter x) => x.Position).ToList();
		for (int num4 = 0; num4 < list7.Count; num4++)
		{
			CachedParameter cachedParameter2 = list7[num4];
			OracleParameter oracleParameter3 = list[num4];
			if (!oracleParameter3.HasSetDirection)
			{
				oracleParameter3.Direction = cachedParameter2.Direction;
			}
			if (flag || !oracleParameter3.HasSetDbType)
			{
				oracleParameter3.OracleDbType = cachedParameter2.OracleDbType;
			}
		}
		return parameterCollection;
	}

	internal static List<CachedParameter> ParseParameters(string parametersSql, MySqlGuidFormat guidFormat)
	{
		parametersSql = s_cStyleComments.Replace(parametersSql, "");
		parametersSql = s_singleLineComments.Replace(parametersSql, "");
		parametersSql = s_multipleSpaces.Replace(parametersSql, " ");
		if (string.IsNullOrWhiteSpace(parametersSql))
		{
			return new List<CachedParameter>();
		}
		parametersSql = s_numericTypes.Replace(parametersSql, "$1");
		parametersSql = s_enumOrSet.Replace(parametersSql, "$1");
		string[] array = parametersSql.Split(',');
		List<CachedParameter> list = new List<CachedParameter>(array.Length);
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].Trim();
			string originalSql = text;
			string direction = "IN";
			if (text.StartsWith("INOUT ", StringComparison.OrdinalIgnoreCase))
			{
				direction = "INOUT";
				string text2 = text;
				text = text2.Substring(6, text2.Length - 6);
			}
			else if (text.StartsWith("OUT ", StringComparison.OrdinalIgnoreCase))
			{
				direction = "OUT";
				string text2 = text;
				text = text2.Substring(4, text2.Length - 4);
			}
			else if (text.StartsWith("IN ", StringComparison.OrdinalIgnoreCase))
			{
				direction = "IN";
				string text2 = text;
				text = text2.Substring(3, text2.Length - 3);
			}
			Match match = s_parameterName.Match(text);
			string name = (match.Groups[1].Success ? match.Groups[1].Value.Replace("``", "`") : match.Groups[2].Value);
			bool unsigned;
			int length;
			string dataType = ParseDataType(match.Groups[3].Value, out unsigned, out length);
			list.Add(CreateCachedParameter(i + 1, direction, name, dataType, unsigned, length, guidFormat, originalSql));
		}
		return list;
	}

	internal static string ParseDataType(string sql, out bool unsigned, out int length)
	{
		sql = s_characterSet.Replace(sql, "");
		sql = s_collate.Replace(sql, "");
		sql = s_enumOrSet.Replace(sql, "$1");
		length = 0;
		Match match = s_length.Match(sql);
		if (match.Success)
		{
			length = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
			sql = s_length.Replace(sql, "");
		}
		string[] array = sql.Trim().Split(' ');
		if ((array.Length < 2 || !s_typeMapping.TryGetValue(array[0] + " " + array[1], out string value)) && s_typeMapping.TryGetValue(array[0], out value) && array[0].StartsWith("BOOL", StringComparison.OrdinalIgnoreCase))
		{
			length = 1;
		}
		unsigned = array.Contains<string>("UNSIGNED", StringComparer.OrdinalIgnoreCase);
		return value ?? array[0];
	}

	private static CachedParameter CreateCachedParameter(int ordinal, string? direction, string name, string dataType, bool unsigned, int length, MySqlGuidFormat guidFormat, string originalSql)
	{
		try
		{
			return new CachedParameter(ordinal, direction, name, dataType, unsigned, length, guidFormat);
		}
		catch (NullReferenceException innerException)
		{
			throw new MySqlException("Failed to parse stored procedure parameter '" + originalSql + "'; extracted data type was " + dataType, innerException);
		}
	}
}
