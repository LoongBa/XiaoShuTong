---
title: 框架问题单——Tier 1.5（SQLite :memory: 测试）在 v4.10.8 不可用
status: 待框架组处理
date: 2026-09-12
source: XiaoShuTong Tier 1.5 迁移实测 + 框架源码审计 + FreeSql/Microsoft.Data.Sqlite 库级调研
---

# 框架问题单：Tier 1.5（SQLite :memory: 测试）在 v4.10.8 不可用

## 一、现象

按 ADR60 / v4.10.7 开发方案文档配置：

```csharp
cfg => cfg.UseFreeSqlEntityDAC(FreeSql.DataType.Sqlite, "Data Source=:memory:")
```

测试启动即 `SyncViewsAsync` 抛 `ObjectDisposedException`（`Cannot access a disposed object`）。

尝试消费端兜底（Singleton IFreeSql + 显式 SyncStructure + 方言变体反射水合）后，**基表写入与视图查询不同库**（写入后视图查不到）——`SQLite :memory:` 多连接内存库隔离。

## 二、根因（4 处框架缺口 + 2 处库级行为）

### G1：IFreeSql 注册为 Scoped（包装 Singleton 缓存实例）→ 作用域结束 dispose 共享实例

**证据**：`_Domain.Infrastructure/FreeSql/FreeSqlAppBuilderExtensions.cs:43-62`

```csharp
services.AddSingleton<IFreeSqlCache>(_ => new FreeSqlCache(connStr =>
    new FreeSqlBuilder().UseConnectionString(dataType, connStr).UseAutoSyncStructure(isDevelopment).Build()));
// L51-62
services.AddScoped<IFreeSql>(sp =>
{
    var cache = sp.GetRequiredService<IFreeSqlCache>();
    return cache.GetOrAdd(connStr);   // ← 返回 Singleton 缓存的共享实例
});
```

MS DI 对 Scoped 工厂返回的实例，作用域结束时若实现 `IDisposable` 会调用 `Dispose`（`ServiceProviderEngineScope.CaptureDisposable`）。`IFreeSql` 实现 `IDisposable` → 首个作用域结束即 dispose 共享 IFreeSql → 连接池销毁 → 后续全部 `ObjectDisposedException`。

### G2：SyncTables / SyncViewsAsync 各建独立 scope → 前者释放连锁 G1

**证据**：`_Framework/Domain/DomainHostInitializerBase.cs:553,555,593,697`

```
ServiceProviderBuiltCallbackAsync → SyncTables(sp) → using scope（L593，结束 dispose IFreeSql）
                                   → SyncViewsAsync(sp) → using scope（L697，拿已 dispose 实例）→ ObjectDisposed
```

### G3：ITableStructureSynchronizer 仅发现 BCL [Table]，FreeSql [Table] 实体建表 no-op

**证据**：`_Domain.Infrastructure/FreeSql/FreeSqlTableStructureSynchronizer.cs:64-65`

```csharp
private static bool HasTableAttribute(Type t)
    => t.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.TableAttribute>() != null;
// 仅 BCL [Table]；FreeSql.DataAnnotations.TableAttribute 分支已移除（v4.9.57）
```

本项目实体用 FreeSql [Table] → SyncTables no-op → 视图依赖的基表不存在。

### G4：GenerateCodeSettings["ViewSqlByProvider"] 不序列化进运行时元数据（根本性障碍）

**证据**：
- 编译期提取：`EntityMetadataGenerator.cs:1109-1116`（`m.GenerateCodeSettings["ViewSqlByProvider"] = variants`）
- 序列化缺失：`EntityMetadataGenerator.Generation.cs:78-176`（BuildMetaCode 只序列化 ViewSql/Attributes/Properties，**不含 GenerateCodeSettings**）
- 运行时读取：`DomainHostInitializerBase.cs:717-722`（`view.GenerateCodeSettings.TryGetValue("ViewSqlByProvider", ...)` → **恒 false**）

`SyncViewsAsync` 运行时拿不到方言变体 → fallback 默认 PG ViewSql → SQLite 执行 PG 特有语法（`FILTER`/`::`/`CREATE OR REPLACE`）失败。

### F1（FreeSql 库级）：SQLite 连接每次操作后 Close + Dispose

**证据**：FreeSql `DbConnectionPool.Return()`（`_dataType == DataType.Sqlite` 分支 `obj.Value.Dispose()`）。裸 `:memory:` 每连接独立内存库 → 写入连接 A、查询连接 B 不同库。

### F2（Microsoft.Data.Sqlite 库级）：`:memory:` 与 `Mode=Memory` 标记为非池化

**证据**：`Microsoft.Data.Sqlite.Core/SqliteConnectionFactory.cs`

```csharp
var isNonPooled = connectionOptions.DataSource == ":memory:"
    || connectionOptions.Mode == SqliteOpenMode.Memory
    || connectionOptions.DataSource.Length == 0
    || !connectionOptions.Pooling;
```

裸 `:memory:` 每次 `new SqliteConnection()` + `Open()` 创建全新内存库。共享需 `file:memdb1?mode=memory&cache=shared`（URI 形式，非 `Mode=Memory`）。

### G5（FreeSql 类型映射缺口）：SQLite 对 `DateOnly` / `string[]` 无原生映射

**现象**：Tier 1.5 下实体含 `DateOnly`（如 `DailyStats.StatDate`）与 `string[]`（如 `Banks.Tags`、`Tasks.QuestionIds`）字段——SQLite provider 无原生类型映射，属性被 FreeSql 标记"忽略"→ 列缺失 → 建表/读写异常。

**证据**：`FreeSql.Internal.Utils.TypeHandlers` 为框架内部类型处理器注册点（PG 下由 provider 原生支持；SQLite 下 DateOnly/string[] 需显式注册 TypeHandler 才能映射）。

**消费端出现绕过**（XiaoShuTong 曾暂用，**按新原则不采用**）：

```csharp
static XiaoShuTongDomainTestFixture()
{
    FreeSql.Internal.Utils.TypeHandlers[typeof(DateOnly)] = new DateOnlyTextHandler();
    FreeSql.Internal.Utils.TypeHandlers[typeof(string[])] = new StringArrayJsonHandler();
}
```

**影响**：消费端绕过使用框架内部 API（`FreeSql.Internal`）——脆弱、跨版本易碎；且隐藏了框架对 SQLite 类型映射的缺口。

**建议框架侧**：
- 框架 FreeSql 扩展包（`_Domain.Infrastructure/FreeSql`）为 SQLite 提供 `DateOnly`（TEXT yyyy-MM-dd）与 `string[]`（JSON TEXT）默认 TypeHandler——对齐 PG provider 的原生支持；或
- 文档明示 SQLite Tier 1.5 的类型映射限制 + 提供框架级注册入口（非 `FreeSql.Internal` 私有 API）。

## 三、框架自身未验证

框架 908/908 测试中，SQLite 相关测试（`ViewSyncInitializerTests`/`GraphQLProjectionEndToEndTests` 等）全用**裸 `FreeSqlBuilder`**（绕过 DomainHost 的 `ConfigTestDomainAsync` 链路）。**从未验证 `ConfigTestDomainAsync` + SQLite :memory: 组合**——ADR60/v4.10.7 文档"Tier 1.5 可用"未实证。

## 四、修复建议（框架侧，按优先级）

| 优先级 | 缺口 | 建议修复 |
|:---:|:---:|---------|
| **P0** | G4 | `BuildMetaCode` 补序列化 `GenerateCodeSettings`（至少 `ViewSqlByProvider`）；或改从 `Attributes` 读变体（`DomainGenerateCodeAttribute.ViewSqlSQLite` 等已序列化） |
| **P1** | G1+G2 | `RegisterFreeSqlCore` 的 `IFreeSql` 改 **Singleton**（FreeSql 官方明确"IFreeSql 应声明为 Singleton"）；或非 IDisposable 包装代理防作用域误 dispose；`SyncTables`/`SyncViewsAsync` 共享 scope |
| **P2** | G3 | `HasTableAttribute` 恢复双发现（BCL + FreeSql [Table]），或文档明示双标注 |
| **P2** | F1/F2 | Tier 1.5 文档补充 SQLite 连接串要求：共享缓存 URI（`file:xxx?mode=memory&cache=shared`）+ Keeper 连接保活，或 `UseConnectionFactory` 单例连接 |

## 五、消费端临时绕过（XiaoShuTong 已部分验证）

1. Fixture 手动管道（方式 B）+ **Singleton IFreeSql**（绕过 G1/G2）
2. 显式 `fsql.CodeFirst.SyncStructure(entityTypes)`（绕过 G3）
3. 反射水合 `ViewSqlByProvider`（绕过 G4——单 VEntity 可行，非通用）
4. 共享缓存 URI + Keeper 连接（解决 F1/F2 多连接隔离）

> 注：消费端兜底复杂且 G4 非通用，**不建议长期依赖**——期望框架侧修复后恢复官方 Tier 1.5 用法。

---

## 附录：消费端三兜底方案（XiaoShuTong 已验证部分可行，供框架组参考）

> 目标：绕过 G1-G4 让 Tier 1.5 在框架修复前部分可用。实测：**主流程聚合用例通过**（视图真实执行成功），但多连接内存库隔离（F1/F2）仍失败——需框架修复 + 连接串方案。

### 兜底 1：Singleton IFreeSql（绕过 G1/G2）

```csharp
EntityDACRegistration.ApplyRegistrars(services, options.EntityDACType!, options);
// 覆盖 Scoped IFreeSql → Singleton（作用域结束不再 dispose 共享实例）
services.AddSingleton<IFreeSql>(sp => sp.GetRequiredService<IFreeSqlCache>().GetOrAdd(SqliteConnStr));
```

### 兜底 2：显式 SyncStructure（绕过 G3）

```csharp
var fsql = sp.GetRequiredService<IFreeSql>();
var entityTypes = typeof(XiaoShuTongUserInfo).Assembly.GetTypes()
    .Where(t => t.IsClass && !t.IsAbstract
                && t.GetCustomAttribute<FreeSql.DataAnnotations.TableAttribute>() != null)
    .ToArray();
if (entityTypes.Length > 0)
    fsql.CodeFirst.SyncStructure(entityTypes);
```

### 兜底 3：方言变体反射水合（绕过 G4，单 VEntity 可行）

```csharp
var attr = typeof(PkPlayerStatsView).GetCustomAttribute<DomainGenerateCodeAttribute>();
if (attr is { ViewSqlSQLite.Length: > 0 })
{
    var variants = new Dictionary<string, string> { ["SQLite"] = attr.ViewSqlSQLite };
    foreach (var view in meta.Views)
        if (view.FullName == typeof(PkPlayerStatsView).FullName)
            view.GenerateCodeSettings["ViewSqlByProvider"] = variants;
}
```

### 聚合用例（框架修复后可恢复）

```csharp
// 写基表（PkMatches + PkPlayers）→ Service 读真实视图
private async Task SeedBaseTablesAsync(long userId, int totalMatches, int wins, int draws, int totalScore)
{
    var matchDac = User.GetService<IEntityDAC<PkMatches>>();
    var playerDac = User.GetService<IEntityDAC<PkPlayers>>();
    var perMatchScore = totalMatches == 0 ? 0 : totalScore / totalMatches;
    for (var i = 0; i < totalMatches; i++)
    {
        long matchId = userId * 1000 + i;
        long? winnerId = i < wins ? userId : (i < wins + draws ? null : userId + 1);
        await matchDac.InsertAsync(new PkMatches { UId = $"pk_{userId}_{i}", Subject = Subject.Chinese,
            BankId = "bank", QuestionCount = 5, Mode = PkMode.Sync,
            Status = PkMatchStatus.Finished, WinnerId = winnerId, FinishReason = PkFinishReason.Score }, ...);
        await playerDac.InsertAsync(new PkPlayers { UId = $"pp_{userId}_{i}", MatchId = matchId,
            UserId = userId, Score = perMatchScore }, ...);
    }
}
// 用例：WinRateComputed(4场2胜1平→0.5) / WinRateRounded(3胜4场→0.75) /
//       NoMatches零值 / OnlyOwnMatches他人隔离 / FiltersOwnRow多行取本人(0.62)
```

### 多连接隔离未解（F1/F2）

兜底方案未解决：基表写入（连接 A）与视图查询（连接 B）不同内存库。框架侧需配合 `file:xxx?mode=memory&cache=shared` + Keeper 连接（见主文 F1/F2 建议）。
