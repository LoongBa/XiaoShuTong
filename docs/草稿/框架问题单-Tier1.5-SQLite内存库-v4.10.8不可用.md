---
title: 框架问题单——Tier 1.5（SQLite :memory: 测试）不可用（v4.10.8 六缺口 + v4.10.10 修复后 G6 状态隔离 + v4.10.14 恢复后 G7/G8 覆盖盲区）
status: 待框架组处理（G1-G5+F1/F2 已由 v4.10.10 修复；G6/G6b 已由 v4.10.13/v4.10.14 修复；G7/G8 为新发现待确认）
date: 2026-09-12
source: XiaoShuTong Tier 1.5 迁移实测 + 框架源码审计 + FreeSql/Microsoft.Data.Sqlite 库级调研
---

# 框架问题单：Tier 1.5（SQLite :memory: 测试）不可用

> **进展**：G1-G5 + F1/F2 六缺口已由框架 **v4.10.10**（提交 `eaa847ec`）修复（NonDisposableFreeSqlWrapper / 共享 scope / 双发现 / ViewSqlMetadataReader / SqliteTypeHandlerRegistrar / SqliteMemoryKeeper），Oracle PASS WITH CONDITIONS，框架实证回归 911/911。XiaoShuTong 恢复官方用法后，发现 **G6（Tier 1.5 状态不隔离）**——见下。

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

### G6（v4.10.10 修复后新发现）：Tier 1.5 SQLite :memory: 状态不隔离——多测试/多运行库污染

**现象**：框架 v4.10.10 修复（G1-G5+F1/F2）后，项目恢复官方 Tier 1.5 配置（`UseFreeSqlEntityDAC(Sqlite, "Data Source=:memory:")` + `SqliteMemoryKeeper`），实测：

- **单测试单运行**：`GetPkStatsServiceTests.ExecuteAsync_Stats_WinRateComputed`（写基表→读聚合）**通过**（视图真实执行，聚合正确）
- **多测试或重复运行**：同一测试**多次运行结果不稳定**——首次通过、后续 `Actual=0`（写基表后读不到）；全类 5 Fact 跑 3 失败（第 2 个起写后读不到）

**根因假设**：`SqliteMemoryKeeper`（共享缓存 + Keeper 保活）在 **Fixture 级单例**下，`:memory:` 库跨测试/跨运行**未重置或状态残留**——首次运行空库写入+读正常；后续运行 `:memory:` 库可能被 Keeper 重建（丢数据）或读到旧状态。

**框架实证局限**：框架 `Tier15SqliteTests` 是**单 Fact**（`Tier15_SqliteMemory_FullChain` 单测试内写 3 条 + 读聚合）——**未验证多测试顺序写入 / 重复运行场景**。

**建议框架侧**：
- `SqliteMemoryKeeper` 在 Fixture/测试集合重建时**重置 :memory: 库**（或提供显式 Reset API）
- 框架 Tier15SqliteTests 扩展为**多 Fact**（多测试顺序写入隔离验证）
- 或文档明示 Tier 1.5 测试需**每个测试独立库**（连接串含测试唯一 Guid）

### G6b（v4.10.13 Reset 契约适配发现）：Reset 后标准 Service 调用（User scope）读旧库

**现象**：v4.10.13（G6 修复，提交 `4387cade`）提供 `ResetTier15SqliteMemoryDbAsync`（移除 IFreeSqlCache 缓存 + 释放 Keeper + 重建表视图）。XiaoShuTong 按契约（每 Fact 首行 Reset）恢复 Tier 1.5 聚合测试，实测：

- Reset 后**新 scope 解析 DAC 写入**成功（无 ObjectDisposed——新库写入正常）
- 但 **`User.Use<GetPkStatsService>()`（标准 Service 调用，User 会话 scope）查询返回 0**——Service 注入的 `IEntityReadOnlyDAC` 持 **Reset 前的旧库引用**（User 会话 scope 的 DAC 缓存了旧 IFreeSql），读不到新 scope 写入的数据

**根因**：Reset 移除 `IFreeSqlCache` 缓存实例（下次 GetOrAdd 重铸新 Guid 内存库）。**User 会话 scope 的 DAC/IFreeSql 在 Reset 后仍是旧实例**——框架测试（Tier15SqliteIsolationTests）用「单 scope 内写+读」（SeedAndReadAsync 同一 scope GetRequiredService），规避了跨 scope 一致性；**标准 `User.Use<TService>()`（NoAop User scope）路径未覆盖**——Reset 后读旧库。

**影响**：框架 G6 契约「每 Fact Reset」与「标准 Service 调用（User scope）」**冲突**——VEntity 聚合测试若经 Service 验证（如 XiaoShuTong GetPkStatsService），Reset 后 Service 读旧库 → 断言失败。

**建议框架侧**：
- Reset 后使 **User 会话 scope 的 DAC 引用同步失效**（或提供"重置后刷新会话 DAC"API）
- 或文档明示：Tier 1.5 测试经 Service 验证时，**Service 调用也需新 scope**（非 `User.Use<T>()` 默认 User scope）
- 或框架测试补充「Reset 后经 Service 查询」用例（覆盖标准消费模式）

### G7（v4.10.14 恢复 Tier 1.5 后新发现）：DateTime UTC Kind 往返丢失（+8h 时区偏移）

**现象**：XiaoShuTong 全套件切 Tier 1.5（SQLite :memory:）后，**7 个**测试断言 DateTime 失败——期望 `2026-09-19T05:11:37.5519156Z`（UTC），实际 `2026-09-19T13:11:37.5519156`（+8h 本地偏移）。涉及：`ListSubscriptionsServiceTests`（TrialEndAt 透传）、`CancelSubscriptionServiceTests`（PeriodEndAt 保留）、`ListMyTasksServiceTests`×2（DeadlineAt 透传 + Overdue 判定）、`ExportRosterCsvServiceTests`×2（CsvExpiresAt 链接过期时间）、`GenerateInviteCodesServiceTests`（30 天过期时间）。

**根因**：System.Data.SQLite（FreeSql.Provider.Sqlite 3.5.311 依赖的 ADO provider，堆栈实证 `System.Data.SQLite.SQLite3.Step`）读写 DateTime **不带 Kind**——`DateTime.UtcNow` 存入后读出 Kind=Unspecified 且被解释为本地时间 → 与 UtcNow 比较偏移 +8h。框架 v4.10.10 G5（`SqliteTypeHandlerRegistrar`）只注册了 **DateOnly/string[]** 的 TypeHandler，**未覆盖 DateTime 的 UTC 往返**（框架官方 Tier15SqliteTests 测试实体仅含 `DateOnly StatDate` + `string[] Tags`，DateTime 未实证）。

**影响**：Tier 1.5 下任何含 DateTime 字段的实体（时间戳/截止/过期语义）断言 `Assert.Equal(utcNow, value)` 失败——Tier 1.5 与生产 PostgreSQL（timestamptz 原生 UTC）行为不一致。

**建议框架侧**：
- `SqliteTypeHandlerRegistrar` 补 DateTime TypeHandler（TEXT ISO8601 `yyyy-MM-ddTHH:mm:ss.fffffffZ` + Kind=Utc 往返），或配置连接串 `DateTimeKind=Utc` / `DateTimeFormat=ISO8601`
- 框架官方 Tier15SqliteTests 补 DateTime 字段读写用例（覆盖 UTC 往返）

### G8（v4.10.14 恢复 Tier 1.5 后新发现）：string[] 数组列查询翻译失败（SQL logic error）

**现象**：`GetNextQuestionServiceTests` 3 个用例失败 `System.Data.SQLite.SQLiteException : SQL logic error`，堆栈指向 `GetNextQuestionService.ExecuteAsync` line 63——`QuestionsDs.EntitySelectAsync(x => x.BankId == ... && x.Status == ... && x.KnowledgePoints.Contains(request.KnowledgePoint))`（`KnowledgePoints` 为 `string[]`，`[Column(DbType = "jsonb")]`）。

**根因**：FreeSql 在 SQLite 下对 `string[]` 列的 `.Contains(x)`（数组包含查询）**无法翻译**（PostgreSQL 用 jsonb `@>` 数组操作，SQLite 无对应）→ 生成非法 SQL → SQL logic error。G5 只解决 string[] **读写**（JSON TEXT 序列化），**未解决 string[] 查询翻译**（Contains/数组操作）。生产 PostgreSQL 正常，Tier 1.5 SQLite 失败。

**影响**：Tier 1.5 下任何对 `string[]`（JSONB 数组列）的 Contains 过滤查询失败——含知识点过滤（GetNextQuestionService 等核心取题逻辑）。

**建议框架侧**：
- 明确 FreeSql SQLite provider 对 string[] Contains 的翻译能力边界：a) 框架文档明示 Tier 1.5 不支持 string[] 列 Contains 查询（查询需内存过滤），或 b) 框架层用 AOP/方言翻译将 `string[] Contains` 转为 SQLite `json_each` 兼容写法
- 官方 Tier15SqliteTests 补 string[] 列 Contains 查询用例（覆盖查询翻译）

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
