# 离线覆盖率测量
#
# 用途：在无法使用 `dotnet test` / VSTest 的受限环境中测量单元测试覆盖率。
# 原理：直接调用 NuGet 缓存里的 coverlet.core 给测试实际加载的 OceanBase.dll 插桩，
#       跑测试宿主收集命中，再用 JsonReporter 出报告并汇总。
#
# 用法：
#   pwsh -File tools/coverage.ps1
#   pwsh -File tools/coverage.ps1 -Threshold 15   # 低于阈值则退出码非 0
#
# 注意：
#   1) 必须插桩「测试输出目录」下的 dll，而不是 src 下的 —— 测试加载的是前者。
#   2) 插桩会临时改写 dll，coverlet 收集后会自行还原；若中途中断，重新 build 即可。
#   3) 无需服务端；但涉及协议/握手/结果集的代码路径必须连库才能覆盖。

param(
    [string]$Root = (Resolve-Path "$PSScriptRoot\..\..").Path,   # 脚本在 tests/coverage/ 下
    [double]$Threshold = 0,
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

# coverlet.core 所在位置（来自 NuGet 缓存）。
# 注意：必须按 [version] 排序，字符串排序会把 "8.0.0" 排在 "10.0.1" 之前，
# 而 8.0.0 没有 build/net10.0 目录。
$coverletDir = Get-ChildItem "$env:USERPROFILE\.nuget\packages\coverlet.collector" -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -as [version] } |
    Sort-Object { [version]$_.Name } -Descending |
    ForEach-Object { Join-Path $_.FullName 'build\net10.0' } |
    Where-Object { Test-Path (Join-Path $_ 'coverlet.core.dll') } |
    Select-Object -First 1

if (-not $coverletDir) {
    Write-Error "找不到 coverlet.core.dll（需要 coverlet.collector 包在 NuGet 缓存中，且含 build/net10.0）"
}

$testOut = Join-Path $Root "tests\OceanBase.Tests\bin\$Configuration\net10.0"
$testExe = Join-Path $testOut 'OceanBase.Tests.exe'
if (-not (Test-Path $testExe)) {
    Write-Error "找不到测试宿主 $testExe，请先构建解决方案"
}

$probeDir = Join-Path $Root '.scratch\coverage-probe'
New-Item -ItemType Directory -Force -Path $probeDir | Out-Null

# 生成一次性测量工具（放在已忽略的 .scratch 下）
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <Nullable>disable</Nullable><ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>coverage-probe</AssemblyName>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.13" />
  </ItemGroup>
</Project>
'@ | Set-Content (Join-Path $probeDir 'p.csproj')
'<Project></Project>' | Set-Content (Join-Path $probeDir 'Directory.Build.props')
'<Project></Project>' | Set-Content (Join-Path $probeDir 'Directory.Packages.props')

@'
using System.Reflection;
using System.Text.Json;

var root = args[0];
var coverletDir = args[1];
var config = args.Length > 2 ? args[2] : "Debug";
var targetDir = Path.Combine(root, "tests", "OceanBase.Tests", "bin", config, "net10.0");
var testExe = Path.Combine(targetDir, "OceanBase.Tests.exe");

AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
{
    var p = Path.Combine(coverletDir, new AssemblyName(e.Name).Name + ".dll");
    return File.Exists(p) ? Assembly.LoadFrom(p) : null;
};
var core = Assembly.LoadFrom(Path.Combine(coverletDir, "coverlet.core.dll"));

object Make(string n, params object?[] a)
{
    var t = core.GetTypes().First(x => x.FullName == n || x.Name == n);
    var c = t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        .First(x => x.GetParameters().Length == a.Length);
    return c.Invoke(a);
}
void SP(object o, string n, object? v)
{
    var p = o.GetType().GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    if (p is null) return;
    var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
    p.SetValue(o, v is null ? null : Convert.ChangeType(v, t));
}

var ilog = core.GetTypes().First(t => t.Name == "ILogger" && t.Namespace == "Coverlet.Core.Abstractions");
var logger = LoggerProxy.Create(ilog);

var par = Make("Coverlet.Core.CoverageParameters");
SP(par, "IncludeFilters", new[] { "[OceanBase]*" });
SP(par, "ExcludeFilters", new[] { "[OceanBase.Tests]*", "[xunit*]*" });
SP(par, "ExcludeAttributes", new[] { "ExcludeFromCodeCoverage" });
SP(par, "UseSourceLink", false); SP(par, "SingleHit", false);
SP(par, "IncludeTestAssembly", false); SP(par, "SkipAutoProps", false); SP(par, "DeterministicReport", false);

var fs = Make("Coverlet.Core.Helpers.FileSystem");
var rh = Make("Coverlet.Core.Helpers.RetryHelper");
var srt = Make("Coverlet.Core.Helpers.SourceRootTranslator", logger, fs);
var peh = Make("Coverlet.Core.Helpers.ProcessExitHandler");
var ih = Make("Coverlet.Core.Helpers.InstrumentationHelper", peh, rh, fs, logger, srt);
var csh = Make("Coverlet.Core.Symbols.CecilSymbolHelper");
var ct = core.GetTypes().First(t => t.Name == "Coverage");
var cov = Activator.CreateInstance(ct, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
    new object?[] { targetDir, par, logger, ih, fs, srt, csh }, null)!;

ct.GetMethod("PrepareModules")!.Invoke(cov, null);

var psi = new System.Diagnostics.ProcessStartInfo(testExe)
{ UseShellExecute = false, RedirectStandardOutput = true, WorkingDirectory = Path.GetDirectoryName(testExe)! };
var proc = System.Diagnostics.Process.Start(psi)!;
var so = proc.StandardOutput.ReadToEnd();
proc.WaitForExit();
foreach (var l in so.Split('\n').Where(l => l.Contains("total:") || l.Contains("失败:") || l.Contains("succeeded:")))
    Console.WriteLine("  " + l.Trim());

var res = ct.GetMethod("GetCoverageResult")!.Invoke(cov, null)!;
var jt = core.GetTypes().First(t => t.Name == "JsonReporter");
var jr = Activator.CreateInstance(jt, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, null, null)!;
var json = (string)jt.GetMethods(BindingFlags.Public | BindingFlags.Instance).First(m => m.Name == "Report")
    .Invoke(jr, new object?[] { res, srt })!;
File.WriteAllText(Path.Combine(root, ".scratch", "coverage.json"), json);

long TL = 0, CL = 0, TB = 0, CB = 0;
var perFile = new Dictionary<string, (long TL, long CL)>();
using (var d = JsonDocument.Parse(json))
    foreach (var mod in d.RootElement.EnumerateObject())
        foreach (var file in mod.Value.EnumerateObject())
        {
            long tl = 0, cl = 0;
            foreach (var cls in file.Value.EnumerateObject())
                foreach (var meth in cls.Value.EnumerateObject())
                {
                    if (meth.Value.TryGetProperty("Lines", out var ls))
                        foreach (var l in ls.EnumerateObject()) { tl++; if (l.Value.GetInt32() > 0) cl++; }
                    if (meth.Value.TryGetProperty("Branches", out var bs))
                        foreach (var b in bs.EnumerateArray())
                        { TB++; if (b.TryGetProperty("Hits", out var h) && h.GetInt32() > 0) CB++; }
                }
            TL += tl; CL += cl;
            var prev = perFile.TryGetValue(file.Name, out var e2) ? e2 : (0L, 0L);
            perFile[file.Name] = (prev.Item1 + tl, prev.Item2 + cl);
        }

Console.WriteLine();
Console.WriteLine($"文件数      {perFile.Count}");
Console.WriteLine($"行覆盖率    {CL}/{TL} = {(TL > 0 ? CL * 100.0 / TL : 0):F2}%");
Console.WriteLine($"分支覆盖率  {CB}/{TB} = {(TB > 0 ? CB * 100.0 / TB : 0):F2}%");
Console.WriteLine();
Console.WriteLine("--- 覆盖率最低的 20 个文件 ---");
foreach (var kv in perFile.OrderBy(k => k.Value.Item1 > 0 ? k.Value.Item2 * 100.0 / k.Value.Item1 : 999).Take(20))
    Console.WriteLine($"  {(kv.Value.Item1 > 0 ? kv.Value.Item2 * 100.0 / kv.Value.Item1 : 0),6:F1}%  {kv.Value.Item2,5}/{kv.Value.Item1,-5} {Path.GetFileName(kv.Key)}");

Console.WriteLine();
Console.WriteLine($"LINE_PCT={(TL > 0 ? CL * 100.0 / TL : 0):F2}");

public class LoggerProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? m, object?[]? a) => null;
    public static object Create(Type t)
    {
        var c = typeof(DispatchProxy).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == "Create" && m.GetGenericArguments().Length == 2)
            .MakeGenericMethod(t, typeof(LoggerProxy));
        return c.Invoke(null, null)!;
    }
}
'@ | Set-Content (Join-Path $probeDir 'Program.cs')

# 用仓库的离线配置（若存在），否则走默认
$cfgArgs = @()
$sandboxCfg = Join-Path $Root '.scratch\nuget.sandbox.config'
if (Test-Path $sandboxCfg) { $cfgArgs = @('--configfile', $sandboxCfg) }

Write-Host "构建测量工具…" -ForegroundColor Cyan
& dotnet build (Join-Path $probeDir 'p.csproj') -m:1 @cfgArgs 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Error "测量工具构建失败" }

Write-Host "插桩 + 跑测试 + 收集覆盖率…" -ForegroundColor Cyan
$out = & (Join-Path $probeDir 'bin\Debug\net10.0\coverage-probe.exe') $Root $coverletDir $Configuration 2>&1
$out | ForEach-Object { $_ }

$line = ($out | Select-String -Pattern '^LINE_PCT=([\d.]+)' | Select-Object -First 1)
if ($line -and $Threshold -gt 0) {
    $pct = [double]$line.Matches[0].Groups[1].Value
    if ($pct -lt $Threshold) {
        Write-Host "行覆盖率 $pct% 低于阈值 $Threshold%" -ForegroundColor Red
        exit 1
    }
    Write-Host "行覆盖率 $pct% 达到阈值 $Threshold%" -ForegroundColor Green
}
