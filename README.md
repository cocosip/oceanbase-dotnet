# Sharp.OceanBase.Data

OceanBase 的 .NET ADO.NET 数据驱动，同时支持 **MySQL 模式** 与 **Oracle 模式**。

本包是对官方 `OceanBase.ManagedDataAccess` 1.3.1 的**源码重建**（source-rebuilt）版本，
公开 API 与原程序集**逐项对齐**（2115 个 API 项，差异为 0，由 `PublicApiParityTests` 自动校验）。

| 包 | 说明 |
|---|---|
| **`Sharp.OceanBase.Data`** | 驱动本体（本仓库 `src/OceanBase`） |
| `Sharp.OceanBase.Data.DependencyInjection` | 依赖注入集成（`src/OceanBase.DependencyInjection`） |

## 安装

```bash
dotnet add package Sharp.OceanBase.Data
```

> ⚠ **与官方包互斥**：本包与原命名空间同为 `OceanBase`。若同时安装官方
> `OceanBase.ManagedDataAccess`，会出现 `CS0433` 类型冲突。**二者只能装其一。**

## 快速开始

### MySQL 模式

连接方式与 MySqlConnector 一致。经 ODP 连接时用户名为三段式 `用户名@租户名#集群名`。

```csharp
using OceanBase;

var builder = new MySqlConnectionStringBuilder
{
    Server = "host",
    Port = 2881,              // 直连 OBServer 通常 2881，经 ODP 通常 2883
    Database = "database_name",
    UserID = "username@tenant_name#cluster_name",
    Password = "password",
};

await using var connection = new MySqlConnection(builder.ConnectionString);
await connection.OpenAsync();

await using var command = connection.CreateCommand();
command.CommandText = "SELECT * FROM orders WHERE order_id = @OrderId;";
command.Parameters.AddWithValue("@OrderId", orderId);

await using var reader = await command.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    var id = reader.GetInt32("order_id");
    // ...
}
```

### Oracle 模式

API 与 Oracle ODP.NET 类似。**`Database`（schema）为必填项**，否则 `Open()` 抛 `ArgumentException`。

```csharp
using OceanBase;

var connectionString =
    "Data Source=host:2883;User Id=username@tenant_name#cluster_name;Password=password;Database=schema_name;";

using var conn = new OracleConnection(connectionString);
conn.Open();

using var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT 1 FROM DUAL";
Console.WriteLine(cmd.ExecuteScalar());
```

### 使用 DataSource（推荐，自带连接池）

```csharp
await using var dataSource = new MySqlDataSourceBuilder(connectionString).Build();
await using var connection = await dataSource.OpenConnectionAsync();
```

## 依赖注入

```csharp
using OceanBase.DependencyInjection;

builder.Services.AddOceanBase(builder.Configuration);              // 读取 ConnectionStrings:OceanBase
builder.Services.AddOceanBaseConnection();                         // 注册 scoped MySqlConnection

// Oracle 模式
builder.Services.AddOceanBaseOracle(builder.Configuration);        // 读取 ConnectionStrings:OceanBaseOracle
builder.Services.AddOceanBaseOracleConnection();
```

## 连接字符串要点

| 项 | 说明 |
|---|---|
| MySQL 模式 | `Server=host;Port=port;Database=db;Uid=user@tenant#cluster;Pwd=pw;` |
| Oracle 模式 | `Data Source=host:port;User Id=user@tenant#cluster;Password=pw;Database=schema;` |
| 端口 | 直连 OBServer 通常 `2881`，经 ODP 通常 `2883`（builder 默认 `3306`） |
| `Data Source` | 仅支持 `host` 或 `host:port`，不支持追加 `/servicename` |
| `Database` | Oracle 模式**必填** |
| 字符集 | `Character Set=gbk`（别名 `CharSet` / `CharacterSet`） |

与原版 MySqlConnector 不同的默认值：`SslMode=None`、`Connection Reset=False`。

## 目标框架

`netstandard2.0` / `netstandard2.1` / `net8.0` / `net9.0` / `net10.0`。

每个 TFM 引用对应主版本的 `Microsoft.Extensions.*` 包（netstandard 与 net8.0 用 8.x、net9.0 用 9.x、net10.0 用 10.x），
版本统一在 `Directory.Packages.props` 中按 TFM 分组管理。

> netstandard 目标使用与原程序集一致的一套独立源码（`src/OceanBase/Frameworks/netstandard2.0|2.1`），
> 因为原包在 netstandard 上的 API 形态与现代 TFM 不同（例如 `FrozenDictionary`、`PeriodicTimer`、`X509CertificateLoader` 等）。
> 五套产物均通过公开 API 逐项对齐校验（每个 TFM 约 2115 项，差异为 0）。

## 构建与测试

```bash
dotnet restore OceanBase.slnx -m:1
dotnet build   OceanBase.slnx -m:1
```

项目启用了 `TreatWarningsAsErrors`，五个 TFM 均为 **0 警告 / 0 错误**。

测试使用 **xunit.v3 + Microsoft.Testing.Platform**（测试项目是可执行程序）：

```bash
dotnet run --project tests/OceanBase.Tests
dotnet run --project tests/OceanBase.DependencyInjection.Tests
```

> `-m:1` 用于规避某些受限环境下 MSBuild 并行节点通信失败的问题；常规环境可省略。
> 若 `dotnet test` 不可用（VSTest 被环境限制），请用上面的 `dotnet run --project`。

### 对齐校验（可选）

`tests/OceanBase.Tests` 中有一组**对齐校验**测试，会把重建产物与官方原始程序集逐项比对：

| 测试 | 内容 |
|---|---|
| `PublicApiParityTests` | 5 个 TFM 的公开 API 面（类型 + 成员签名），差异必须为 0 |
| `BehaviorParityTests` | 真正执行两个程序集，差分比对连接串解析、枚举、值类型等纯逻辑行为 |

原始程序集属**开发期校验资产，不进版本库**。要运行这组测试，请把官方的
`OceanBase-ManagedDataAccess.dll` 放到下面任一位置（按 TFM 分子目录，或直接放一份 net10.0）：

```
%OCEANBASE_BASELINE_DIR%/<tfm>/OceanBase-ManagedDataAccess.dll
<repo>/.scratch/baseline/<tfm>/OceanBase-ManagedDataAccess.dll
```

两者都不存在时，这组测试会**自动跳过**（不失败），因此不影响常规构建与 CI。

### 单元测试

除对齐校验外，还有不依赖基准程序集的常规单元测试，覆盖**公开 API 与 internal 类型**：

| 测试文件 | 覆盖内容 |
|---|---|
| `ConnectionStringBuilderTests` | 两种模式的全部关键字与别名、默认值、大小写/空白容忍、往返、非法取值 |
| `ValueTypeTests` | `MySqlDateTime`/`MySqlDecimal`/`MySqlGeometry`、Oracle 时间戳与间隔类型、枚举成员数与取值 |
| `AdoNetSurfaceTests` | Provider 工厂、参数集合、属性集合、BulkCopy 映射、日志类型、异常层次、Command/DataAdapter |
| `SerializationTests` | `ByteBufferWriter`/`ByteArrayReader` 的字节序、length-encoded 变长编码、字符串编解码 |
| `ColumnReaderTests` | 24 个可直接调用的列读取器（整数/布尔/YEAR 边界值、GUID 三种字节布局、DECIMAL） |
| `AuthenticationTests` | MySQL Secure Password Authentication（SHA-1）与 OceanBase SHA-256 scramble 变体、字符集字节宽度 |
| `IdentifierAndTimerTests` | `NormalizedSchema` 标识符规范化（引号转义、Oracle 大写语义、三段式包名）、`TimerQueue` 簿记 |
| `OracleUdtAndPayloadTests` | `OracleRefCursor.Null`、DbColumn 契约、`OkPayload` 前置条件、`ChangeUserPayload` 编码 |
| `InternalTypeTests` | CRC32、列类型映射、Oracle UDT 十六进制、`Utility` 查找与扩容 |
| `DriverSmokeTests` | 连接串解析、DataSource 工厂、连接继承关系 |

> internal 类型通过主程序集 `Properties/AssemblyInfo.cs` 中的
> `InternalsVisibleTo("OceanBase.Tests")` 访问；个别 `private static` 解析方法用反射调用
> （`ReadOnlySpan<T>` 是 ref struct，无法装箱进 `MethodInfo.Invoke`，故用 `CreateDelegate` 强类型调用）。

### 覆盖率

受限环境（`dotnet test` / VSTest 被拦截）下可用自带脚本测量：

```bash
pwsh -File tests/coverage/coverage.ps1
pwsh -File tests/coverage/coverage.ps1 -Threshold 12   # 低于阈值退出码非 0
```

原理是直接调用 NuGet 缓存中的 `coverlet.core` 对**测试实际加载的** `OceanBase.dll` 插桩，
跑测试宿主收集命中，再用 `JsonReporter` 出报告。

> 当前实测：**行 12.72% / 分支 11.74%**（测试 326 个：驱动 319 + DI 7）。
> 上限受限于「无服务端」：`Core/`（会话/握手/结果集，9449 行）、LOB（1714 行）、
> 事务与批量（1354 行）必须连真实数据库才能产生覆盖；
> `Logging/Log.cs` 的 2152 行是 149 个源生成日志方法，需真实会话事件触发。
> 可离线覆盖的部分约 2 万行，即使全部测到，整体上限也约 60%。

## 离线 / 受限环境

仓库根的 `NuGet.config` 只声明 nuget.org，保持通用。若你的环境无法访问外网
（或无法写用户级包目录），可自建一份本地配置覆盖：

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <config>
    <add key="globalPackagesFolder" value=".packages" />
  </config>
  <fallbackPackageFolders>
    <add key="user-cache" value="C:\Users\<你>\.nuget\packages" />
  </fallbackPackageFolders>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

然后构建时指定：

```bash
dotnet build OceanBase.slnx -m:1 --configfile .scratch/nuget.sandbox.config
```

## 许可

MIT。本项目基于 `OceanBase.ManagedDataAccess` 1.3.1（其上游为 MySqlConnector，
Copyright 2016–2025 Bradley Grainger，MIT 许可）进行源码重建。
详见 [LICENSE](LICENSE) 与 [NOTICE](NOTICE)。
