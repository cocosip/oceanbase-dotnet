using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// API 对齐校验（硬门槛）。
///
/// 用 MetadataLoadContext 同时加载「原始 OceanBase-ManagedDataAccess.dll」与「重建的 OceanBase.dll」，
/// 逐项比对公开 API 面（类型 + 成员签名），差异必须为 0。
///
/// 基准 DLL 属**开发期校验资产**，不进版本库；获取方式（按顺序尝试）：
///   1) 环境变量 <c>OCEANBASE_BASELINE_DIR</c> 指向的目录；
///   2) 仓库根目录 <c>.scratch/baseline/</c>。
/// 两者都没有时**跳过**测试，不作为构建/CI 前置条件。
/// </summary>
public sealed class PublicApiParityTests
{
	/// <summary>
	/// 已知且已确认可接受的差异（白名单）。每一项都必须有书面理由。
	/// 新增差异时不要直接加白名单，先确认是否为真实缺陷。
	/// </summary>
	private static readonly HashSet<string> s_acceptedDifferences = new(StringComparer.Ordinal)
	{
		// 原始程序集是强名称（PublicKeyToken=d33d3e53aa5f8c92），我们无原私钥、不冒充签名者。
		// 这不影响公开 API 面，仅影响程序集标识。
	};

	[Theory]
	[InlineData("netstandard2.0")]
	[InlineData("netstandard2.1")]
	[InlineData("net8.0")]
	[InlineData("net9.0")]
	[InlineData("net10.0")]
	public void Rebuilt_assembly_exposes_the_same_public_api_as_the_original(string targetFramework)
	{
		var baselinePath = BaselineLocator.Find(targetFramework);
		Assert.SkipUnless(baselinePath is not null, BaselineLocator.SkipReason);

		var rebuiltPath = ResolveRebuiltAssembly(targetFramework);

		var original = LoadApiSurface(baselinePath!);
		var rebuilt = LoadApiSurface(rebuiltPath);

		// 防空转：若两侧都为空，下面的 Except 比对会“通过”但毫无意义。
		Assert.True(original.Count > 100, $"[{targetFramework}] 原始程序集 API 面过小（{original.Count} 项），校验可能失效。");
		Assert.True(rebuilt.Count > 100, $"[{targetFramework}] 重建程序集 API 面过小（{rebuilt.Count} 项），校验可能失效。");

		var missing = original.Except(rebuilt).OrderBy(x => x, StringComparer.Ordinal).ToList();
		var added = rebuilt.Except(original).OrderBy(x => x, StringComparer.Ordinal).ToList();

		missing = missing.Where(x => !s_acceptedDifferences.Contains(x)).ToList();
		added = added.Where(x => !s_acceptedDifferences.Contains(x)).ToList();

		var report = new StringBuilder();
		report.AppendLine($"目标框架: {targetFramework}");
		report.AppendLine($"原始 API 项: {original.Count}");
		report.AppendLine($"重建 API 项: {rebuilt.Count}");
		report.AppendLine();
		if (missing.Count > 0)
		{
			report.AppendLine($"== 缺失（重建程序集里没有，共 {missing.Count} 项）==");
			foreach (var m in missing.Take(100))
				report.AppendLine("  - " + m);
		}
		if (added.Count > 0)
		{
			report.AppendLine($"== 新增（原始程序集里没有，共 {added.Count} 项）==");
			foreach (var a in added.Take(100))
				report.AppendLine("  + " + a);
		}

		Assert.True(missing.Count == 0 && added.Count == 0, report.ToString());
	}

	/// <summary>
	/// 定位某个 TFM 的重建程序集：
	/// - 当前运行 TFM：直接用已加载的程序集；
	/// - 其它 TFM：从构建输出目录读取（相对测试程序集定位到仓库根）。
	/// </summary>
	private static string ResolveRebuiltAssembly(string targetFramework)
	{
		var current = typeof(MySqlConnection).Assembly;
		var currentTfm = AppContext.TargetFrameworkName?.Split(',')[0].Replace(".NETCoreApp,Version=v", "net").Replace(".", "");
		if (NormalizeTfm(currentTfm) == targetFramework)
			return current.Location;

		// AppContext.BaseDirectory = <repo>/tests/OceanBase.Tests/bin/<Config>/net10.0/
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		var config = dir.Parent!.Name;
		var repoRoot = dir.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
		var candidate = Path.Combine(repoRoot, "src", "OceanBase", "bin", config, targetFramework, "OceanBase.dll");

		Assert.True(File.Exists(candidate),
			$"找不到 {targetFramework} 的重建程序集：{candidate}。请先执行 dotnet build OceanBase.slnx。");
		return candidate;
	}

	private static string NormalizeTfm(string? tfm) => tfm switch
	{
		"net80" => "net8.0",
		"net90" => "net9.0",
		"net100" => "net10.0",
		_ => tfm ?? "",
	};

	/// <summary>加载程序集并枚举其公开 API 面（类型与公开成员签名）。</summary>
	private static HashSet<string> LoadApiSurface(string assemblyPath)
	{
		var dir = Path.GetDirectoryName(assemblyPath)!;

		// 解析依赖：应用目录（含 NuGet 还原的依赖）+ 程序集所在目录 + 核心库目录。
		// 核心库目录必须包含，否则 MetadataLoadContext 报 "Could not find core assembly"。
		var paths = new List<string>();
		paths.AddRange(Directory.GetFiles(AppContext.BaseDirectory, "*.dll"));
		paths.AddRange(Directory.GetFiles(dir, "*.dll"));
		paths.AddRange(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"));
		var resolver = new PathAssemblyResolver(paths.Distinct(StringComparer.OrdinalIgnoreCase));

		using var mlc = new MetadataLoadContext(resolver, "System.Private.CoreLib");
		var asm = mlc.LoadFromAssemblyPath(assemblyPath);

		var surface = new HashSet<string>(StringComparer.Ordinal);
		foreach (var type in asm.GetExportedTypes())
			CollectType(type, surface);
		return surface;
	}

	private static void CollectType(Type type, HashSet<string> surface)
	{
		// 只比对公开 API；编译器生成类型（含 <>) 不属于 API 契约
		if (type.Name.Contains('<') || type.Name.Contains('>'))
			return;

		surface.Add("T:" + TypeName(type));

		const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

		foreach (var ctor in type.GetConstructors(flags))
			surface.Add($"M:{TypeName(type)}.#ctor({ParamList(ctor.GetParameters())})");

		foreach (var method in type.GetMethods(flags))
		{
			if (method.IsSpecialName && !method.Name.StartsWith("op_", StringComparison.Ordinal))
				continue;
			surface.Add($"M:{TypeName(type)}.{method.Name}({ParamList(method.GetParameters())}):{TypeName(method.ReturnType)}");
		}

		foreach (var prop in type.GetProperties(flags))
			surface.Add($"P:{TypeName(type)}.{prop.Name}:{TypeName(prop.PropertyType)}");

		foreach (var field in type.GetFields(flags))
		{
			if (field.IsSpecialName)
				continue;
			surface.Add($"F:{TypeName(type)}.{field.Name}:{TypeName(field.FieldType)}");
		}

		foreach (var evt in type.GetEvents(flags))
			surface.Add($"E:{TypeName(type)}.{evt.Name}:{TypeName(evt.EventHandlerType!)}");

		foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
			CollectType(nested, surface);
	}

	private static string ParamList(ParameterInfo[] parameters)
		=> string.Join(",", parameters.Select(p => TypeName(p.ParameterType)));

	/// <summary>归一化类型名，屏蔽程序集身份差异（如泛型参数、数组、byref）。</summary>
	private static string TypeName(Type type)
	{
		if (type.IsByRef)
			return TypeName(type.GetElementType()!) + "&";
		if (type.IsArray)
			return TypeName(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
		if (type.IsGenericParameter)
			return "!" + type.GenericParameterPosition;
		if (type.IsGenericType)
		{
			var def = type.GetGenericTypeDefinition();
			var name = (def.FullName ?? def.Name);
			var tick = name.IndexOf('`');
			if (tick >= 0)
				name = name.Substring(0, tick);
			var args = string.Join(",", type.GetGenericArguments().Select(TypeName));
			return name + "<" + args + ">";
		}
		return type.FullName ?? type.Name;
	}
}

/// <summary>
/// 基准程序集定位器。基准属开发期校验资产，不进版本库；
/// 找不到时相关测试跳过，不作为构建/CI 前置条件。
/// </summary>
internal static class BaselineLocator
{
	public const string SkipReason =
		"未提供基准程序集（开发期校验资产，不入库）。如需运行对齐校验，" +
		"请把官方 OceanBase-ManagedDataAccess.dll 放到 %OCEANBASE_BASELINE_DIR% 或 <repo>/.scratch/baseline/ 下。";

	private const string FileName = "OceanBase-ManagedDataAccess.dll";

	/// <summary>查找指定 TFM 的基准程序集；找不到返回 null。</summary>
	public static string? Find(string targetFramework)
	{
		foreach (var dir in CandidateDirectories())
		{
			if (!Directory.Exists(dir))
				continue;

			// 优先 TFM 子目录，其次目录本身（兼容只放一份 net10.0 的情况）
			foreach (var candidate in new[]
			{
				Path.Combine(dir, targetFramework, FileName),
				Path.Combine(dir, FileName),
			})
			{
				if (File.Exists(candidate))
					return candidate;
			}
		}
		return null;
	}

	private static IEnumerable<string> CandidateDirectories()
	{
		var env = Environment.GetEnvironmentVariable("OCEANBASE_BASELINE_DIR");
		if (!string.IsNullOrWhiteSpace(env))
			yield return env;

		// AppContext.BaseDirectory = <repo>/tests/OceanBase.Tests/bin/<Config>/<tfm>/
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		var repoRoot = dir.Parent?.Parent?.Parent?.Parent?.Parent?.FullName;
		if (repoRoot is not null)
			yield return Path.Combine(repoRoot, ".scratch", "baseline");
	}
}
