---
title: TKWF 框架问题反馈（XiaoShuTong 实盘发现）
version: v0.3
summary: 从 XiaoShuTong 项目实战中发现的 TKWF 框架问题/隐患/文档偏差，供框架组修正
status: 待框架组评审
date: 2026-09-11
source: XiaoShuTong VEntity 落地 + 框架源码审计（F:\LoongBa_Git\_TKWF）
---

# TKWF 框架问题反馈

> 反馈来源：XiaoShuTong 项目 `PkPlayerStatsView`（VEntity）落地过程中实测发现 + 框架源码三路并行审计。
> 涉及框架能力：VEntity 测试（InMemory DAC）、`IEntityDAC<T>`/`IEntityReadOnlyDAC<T>` 注册机制、tkwf-* skill 文档准确性。
> 优先级：P0 = 影响正确性（测试与生产行为不一致）；P1 = 文档误导开发者踩坑；P2 = 可维护性。
> **v0.3（2026-09-11）**：框架组回复「DLL 至 4.10.6 后 P0-3 因 P0-1 天然解锁」——经源码审计 + 部署运行时探针**证伪**（P0-1 与 P1-1 正交，守卫仍拦截 `IEntityDAC<TView>.InsertAsync`）；另发现部署版本混合（Testing.xUnit.dll 仍 4.10.5）。详见「N-3 论证伪」。

---

## P0-1：`SetEntityDAC` 双接口注册为独立 scoped 实例

### 现象

`IEntityDAC<T>` 与 `IEntityReadOnlyDAC<T>` 对同一实现类型分别 `AddScoped`，DI 按服务类型缓存 → **两个独立实例**。InMemory（`TestingEntityDAC`）下：

```
User.GetService<IEntityDAC<T>>()          → TestingEntityDAC 实例 A（独立 _store_A）
User.GetService<IEntityReadOnlyDAC<T>>()  → TestingEntityDAC 实例 B（独立 _store_B）
```

向 A 插入数据 → B 查询为 0 行。**已实测复现**（XiaoShuTong `GetPkStatsServiceTests` 自检 A 通过、自检 B 空）。

### 根因

```csharp
// F:\LoongBa_Git\_TKWF\_Framework\Domain\Interfaces\EntityDACExtensions.cs:21-22
services.AddScoped(typeof(IEntityDAC<>), dacImplementationType);
services.AddScoped(typeof(IEntityReadOnlyDAC<>), dacImplementationType);
```

DI 无机制感知「这两个注册同源」，各自 `ActivatorUtilities` 构造实例。读写链路：
- 查询：`DomainUser.Query<T>()` → `IEntityReadOnlyDAC<T>`（DomainUser.cs:325）
- 写入：`DomainDataServiceBase` → `IEntityDAC<T>`（DomainDataServiceBase.cs:23,152）

### 影响

| 维度 | 影响 |
|------|------|
| 测试可信度 | InMemory 下「写入后查询为空」→ 测试与生产行为不一致，测试通过不代表生产正确 |
| 框架内部组件 | `InboxDbVersionStore.cs:48-49` / `OutboxSender.cs:106-107` / `InboxProcessor.cs:78-79` 同 scope 解析两接口 → InMemory 下读永远看不到写（版本防乱序失效、Outbox 事件永不发送，**静默无告警**） |
| 开发者认知 | README 声称「同一实现」，实际「同一实现类型的两个独立实例」——文档误导 |

### 框架组已知证据

`_Tests\Domain.BackgroundJobs.Tests\TestInfrastructure.cs:103-108` 已有手工 workaround，注释明确：
```csharp
// JobRecord 内存共享存储（覆盖 TestingEntityDAC<> 开放泛型——双接口经单例 store 共享同一数据源）
services.AddScoped<IEntityReadOnlyDAC<JobRecord>>(sp =>
    (IEntityReadOnlyDAC<JobRecord>)sp.GetRequiredService<IEntityDAC<JobRecord>>());
```
框架组已用手工规避，但未修根因。

### 建议修复

**方案 A（推荐）**：`SetEntityDAC` 改工厂注册，两接口共享同一实例：

```csharp
public static IServiceCollection SetEntityDAC(
    this IServiceCollection services, Type dacImplementationType)
{
    if (!dacImplementationType.IsGenericTypeDefinition)
        throw new ArgumentException("...", nameof(dacImplementationType));

    // 先注册实现类型（scoped），两接口转发到同一实例
    services.AddScoped(dacImplementationType);
    services.AddScoped(typeof(IEntityDAC<>), sp =>
    {
        var implType = typeof(IEntityDAC<>).MakeGenericType(...);
        return sp.GetRequiredService(dacImplementationType);
    });
    services.AddScoped(typeof(IEntityReadOnlyDAC<>), sp => ...同实例...);
    return services;
}
```

> 注意：需处理开放泛型 `AddScoped(typeof(X), ...)` 与实例转发的组合。生产（FreeSql）行为不变（共享实例 + 共享 DB）；InMemory 两接口指向同一 `_store`，读写链路天然打通。

**方案 B（最小）**：文档明示「InMemory DAC 下两接口为独立实例，测试须用同一接口读写」+ `TestingEntityDAC` 类注释加醒目警告。

**方案 C**：新增 `TestingEntityDAC.SeedAsync(IEnumerable<TEntity>)` 测试填充 API + `ConfigTestDomainAsync` 支持 VEntity 种子（见 P1-3）。

---

## P0-2：tkwf-test SKILL.md §7.3 VEntity 测试示例无法编译，引导踩坑

### 现象

`docs/AC-Kit/skills/tkwf-test/SKILL.md:139-147` 示例：

```csharp
var dac = this.User.RootServiceProvider
    .GetRequiredService<IEntityReadOnlyDAC<PaymentLogStatView>>();
await dac.InsertAsync(new PaymentLogStatView { ... });
```

**两处硬错误**：
1. `this.User.RootServiceProvider` —— `DomainXunitTestBase` 无此成员（真实成员：`Fixture`/`TestLoggerFactory`/`Output`/`User`）
2. `dac.InsertAsync(...)` —— `IEntityReadOnlyDAC<T>` **无 `InsertAsync`**（真实方法仅 Query/FirstOrDefault/ToList/Count）

### 影响

开发者按文档写 → 编译失败 → 最自然修正为 `IEntityDAC<T>`（有 InsertAsync）→ 但写入实例与 `User.Query<T>()`（示例 :150）读取实例不同 → 精确复现 P0-1。

**这是官方文档主动将开发者引向 P0-1 陷阱。**

### 建议修复

- 修正示例：测试填充用 `IEntityDAC<TView>`（有 InsertAsync）+ 查询用同一 `IEntityDAC<TView>`（P0-1 修复后两接口同源可自由切换）
- 或按 P0-1 方案 C 提供专用填充 API
- 全仓检索 `RootServiceProvider`（文档 4 处 + TestingEntityDAC.cs XML doc 注释），统一修正

---

## P0-3：VEntity 在 InMemory 测试下无数据填充通道

### 现象

VEntity 无 DataService（ADR14 D7），测试无法经 `EntityCreateAsync` 填充；`SyncViewsAsync` 跳过 InMemory DAC；`IEntityReadOnlyDAC<T>` 无写方法；`IEntityDAC<TView>` 有 InsertAsync 但因 P0-1 与 EQR 查询不同源。

**结论：按官方文档，InMemory 下 VEntity 数据既无法经 DataService 填充，也无法经只读 DAC 写入，也没有同源查询通道——VEntity 集成测试在 InMemory 环境实际不可行。**

### 建议修复

1. **P0-1 方案 A 落地后**：测试填充 `IEntityDAC<TView>` + 查询 `User.Query<TView>()` 天然同源（同 scope 共享实例）
2. **或**：`TestingEntityDAC` 增加 View 特化支持——`SeedAsync` 直接写 `_store`，与 EQR 查询同实例
3. **文档**：tkwf-test §7 明确 InMemory VEntity 测试的正确姿势

---

## P1-1：View 守卫文档漂移（4 处称「静态构造器守卫」，实际已改方法级）

### 现象

以下文档仍称 View 实体经 `IEntityDAC` 解析会触发「静态构造器守卫抛错」，但当前代码已改为**方法级**守卫（仅写方法调用时抛）：

| 文档 | 位置 | 当前实际 |
|------|------|---------|
| D06-领域数据服务与数据存取设计.md | :121-123 | `FreeSqlEntityDAC.GuardAgainstViewEntity()`（:57-62）仅写方法调用，`Query` 不守卫 |
| tkwf-ventity-design SKILL.md | :104 | 同上 |
| VEntity跨表查询升级-开发方案.md | :181 | 同上 |
| ADR-扩展跨表查询VEntity化.md | :56 | 同上 |

### 影响

开发者以为「`IEntityDAC<View>` 解析即抛错」→ 实际不抛（只有调用写方法才抛）→ 文档与行为不符。

### 建议修复

4 处文档统一改为「方法级守卫：`IEntityDAC<ViewEntity>` 的写操作（Insert/Update/Delete）调用时抛 `InvalidOperationException`」；测试 DAC（`TestingEntityDAC`/`MockDbEntityDAC`）补对称 Guard（当前完全无守卫 → 测试可信度问题：测试环境 VEntity 可写、生产不可写）。

---

## P1-2：`IEntityReadOnlyDAC<>` 与 `IEntityDAC<>` 后注册覆盖导致静默分歧

### 现象

`SetEntityDAC` 之后若消费者追加 `services.AddScoped<IEntityDAC<T>>(factory)`（DI「后注册优先」），`IEntityDAC<T>` 被覆盖但 `IEntityReadOnlyDAC<T>` 仍指向原实现 → 两接口指向**不同实现类**（比不同实例更严重），且无任何告警。

### 建议修复

`SetEntityDAC` 检测是否已注册 `IEntityDAC<>`/`IEntityReadOnlyDAC<>` 并抛异常/警告；文档明示「SetEntityDAC 之后不应再注册这两接口」。

---

## P1-3：无实例同一性回归测试 + 相关 README 陈旧

- `CfgStrongContractTests.cs:231` 仅断言 `IsType<FreeSqlEntityDAC<T>>`，从未断言两接口实例 `ReferenceEquals` 同一——P0-1 行为完全未受测试保护。
- `_Domain.Testing\xUnit\README.md`（2026-07-29）「断裂链 1」（DataService 丢弃 DAC 返回值）**已在 V4.9.44 修复**（DomainDataServiceBase.cs:152 `entity = await dac.InsertAsync(...)`），README 未同步。
- 建议：新增实例同一性回归测试（修复后必绿）+ 更新 README。

---

## 补充：XiaoShuTong 侧已确认的联调环境说明

- 项目侧 `.agents/skills/tkwf-*` 与 `%TKWFDeployPath%\docs\AC-Kit\skills\*` 为**同一文件（junction/hardlink，FileId 一致）**——`-LinkRefs` 本地联调模式。**项目侧无法独立定制 skill**，skill 修正需在框架源码 `_TKWF` 侧进行（单一来源）。若产品线有「项目侧 skill 覆盖」需求，需框架侧评估链接策略或提供覆盖优先级机制。

---

## V4.10.6 反馈验收（2026-09-11 框架组已修复项）

框架组已在 **v4.10.6**（提交 `1af81e56`）落地本反馈的 P0-1/P0-2/P1-1/P1-2/P1-3，回归 907/907 全绿：

| 原项 | 框架组修复 | 验收状态 |
|:---:|-----------|:---:|
| P0-1 | `EntityReadOnlyDACForwarder<T>` 桥接：`IEntityReadOnlyDAC<>` 转发到同 scope `IEntityDAC<>` 实例（Microsoft DI 开放泛型工厂无法拿闭合类型参数，forwarder 标准方案） | ✅ 运行时已生效（Forwarder 解析 + 读写同源回归测试） |
| P0-2 | SKILL §7.3 示例 `RootServiceProvider`→`User.GetService<T>`（RootServiceProvider 全仓 15 处/7 文件替换归零） | ⚠️⚠️ **部分修复，见下** |
| P1-1 | 5 处文档改方法级守卫 + `TestingEntityDAC`/`MockDbEntityDAC` 补对称 View 守卫 | ✅ 已生效（写 View 抛异常） |
| P1-2 | G06 §5.3 自定义 DAC 注册改 `SetEntityDAC` | ✅ 已修 |
| P1-3 | README 断裂链 1/问题 1 标注已修（V4.9.44） | ✅ 已修 |

**XiaoShuTong 适配**：`GetPkStatsServiceTests` 已移除过时 workaround（强转 `TestingEntityDAC`），改为 V4.10.6 修复后的降级路径验证；全套件 343 通过 0 失败。

---

## 新发现（V4.10.6 引入，待框架组下一轮）

### N-1：P0-2 与 P1-1 相互矛盾——SKILL §7.3 示例仍不可用

**现象**：v4.10.6 修正后的 SKILL.md §7.3 示例：

```csharp
var dac = this.User.GetService<IEntityDAC<PaymentLogStatView>>();
await dac.InsertAsync(new PaymentLogStatView { ... });
```

但**同一版本**的 P1-1 守卫（`TestingEntityDAC.cs`/`MockDbEntityDAC.cs`）对 View 实体的写操作抛 `InvalidOperationException`（"View Entity 不支持写操作"）。两个改动**互相冲突**：

```
§7.3 示例：IEntityDAC<View>.InsertAsync 填充  → 被 P1-1 守卫拦截（抛异常）
```

**实测复现**：XiaoShuTong `GetPkStatsServiceTests` 按修正后示例编写 → 运行抛 `TestingEntityDAC<PkPlayerStatsView>: View Entity 不支持写操作`。

**根因**：P0-2 只把 `RootServiceProvider` 换成 `GetService<T>`，未同步意识到 P1-1 守卫使 `IEntityDAC<View>` 写入不可行——两个修复独立落地，未交叉验证。

**建议**：
- SKILL §7.3 示例改为**真实 FreeSql 集成测试**（视图由 `SyncViewsAsync` 创建，数据写基表后查视图）——与框架自身 VEntity 测试模式一致
- 或明确文档：InMemory 下 VEntity 不可填充（`IEntityDAC<View>` 写被守卫、`IEntityReadOnlyDAC<View>` 无写方法），VEntity 数据验证需 FreeSql 视图真实创建
- 补 P0-2/P1-1 交叉回归：任一改动都走一遍 §7.3 示例

### N-2：VEntity InMemory 数据填充通道仍缺失（P0-3 悬而未决）

**现象**：P0-1 修复（只读 DAC 转发写 DAC）后，VEntity 数据仍**无法经任何 DAC 通道填充**：

| 通道 | 状态 | 原因 |
|------|:---:|------|
| `IEntityDAC<ViewEntity>.InsertAsync` | ❌ 守卫拦截 | P1-1 新增 View 写守卫 |
| `IEntityReadOnlyDAC<ViewEntity>` | ❌ 无写方法 | 接口仅 Query/FirstOrDefault/ToList/Count |
| `User.Query<ViewEntity>` | ✅ 只读可用 | EQR 查询通道（但无数据可读） |
| `EntityCreateAsync`（经 DataService） | ❌ VEntity 无 DataService | ADR14 D7 |

**影响**：VEntity 的 InMemory Contract 测试（本仓库已实现第一个 VEntity `PkPlayerStatsView`）只能验证降级/空值路径，**聚合正确性**（仅 Finished 计入、Count/Sum 口径）必须在真实 FreeSql 环境验证。本机无 DB（`appsettings.Test.json` 连接串为空）→ 聚合正确性无自动化保障，仅靠编译期 `TKW_SG1a_VIEW001` 列校验。

**建议**（任选其一，供框架组决策）：
- **方案 A**：`TestingEntityDAC` 提供 `SeedAsync(IEnumerable<TEntity>)` 便捷方法（绕过写守卫，直接写 `_store`，专用测试通道，与生产守卫语义不冲突）
- **方案 B**：提供 `IEntityReadOnlyDAC<ViewEntity>` 的测试特化实现（实现类带 Seed，注册仅覆盖测试 scope）
- **方案 C**：文档明示「VEntity InMemory 测试仅覆盖只读降级路径，聚合正确性需 FreeSql 集成测试」，并考虑 `ConfigTestDomainAsync` 增加 `EnableAutoViewSync` 真实视图联动（本机需 DB）

---

## N-3：框架组「P0-3 因 P0-1 天然解锁」论断 — 经审计证伪（v0.3 新增）

### 框架组回复（待验证论断）

> 「DLL 更新到 v4.10.6 后，P0-1 读写隔离、P0-2 文档陷阱、P1-1 守卫不对称、P1-2 注册分裂、P1-3 README 滞后全部消除；**P0-3（VEntity InMemory 填充）因 P0-1 修复天然解锁**（`IEntityDAC<TView>` 填充 + `User.Query<TView>()` 同源）。」

### 验证结论：**P0-3 论断不成立**（P0-1 与 P1-1 正交）

**源码审计**（`_TKWF` v4.10.6）：
- `TestingEntityDAC.GuardAgainstViewEntity()`（L68）被 **6 个写方法全部调用**（InsertAsync L77 / InsertBatchAsync L87 / DeleteAsync L101 / UpdateAsync L107 / UpdateBatchAsync L114 / UpdateColumnsBatchAsync L125）；`MockDbEntityDAC` 同样（L60 定义，6 调用点）
- `DatasetSeedLoader.LoadIntoAsync/LoadFromSeedAsync` 调用 `dac.InsertAsync` → **Seed 通道也被守卫拦截**，无绕过
- 框架自身 VEntity 测试（GraphQLProjectionEndToEndTests / Plan1EndToEndTests）**全部用 `fsql.Insert(...)` 原生写入**，不经过 `IEntityDAC<T>`——框架无 VEntity InMemory 填充先例
- P0-1 回归测试 `SetEntityDAC_DualInterfaces_ReadWriteSameSource` 用 `TestEntity`（**普通实体**，仅 `IDomainEntity`），**不涉及 View 行为**

**部署运行时探针**（决定性证据）：
```
PkPlayerStatsView implements IDomainViewEntity: True
IEntityDAC<PkPlayerStatsView>.InsertAsync → THREW InvalidOperationException
  "TestingEntityDAC<PkPlayerStatsView>: View Entity 不支持写操作"
```

**逻辑推演**：
- P0-1（Forwarder 桥接）= 解决「`IEntityReadOnlyDAC<T>` 与 `IEntityDAC<T>` 同 scope 共享实例」→ 读写同源
- P1-1（GuardAgainstViewEntity）= 解决「测试可写 View、生产不可写」信任假象 → **View 实体禁止任何写入**
- 两者正交：P0-1 使读写同源，但**写操作本身被 P1-1 守卫拦截** → `IEntityDAC<TView>.InsertAsync` 填充路径仍被阻断
- v4.10.6 开发方案 L143 的论断（写于 P1-1 加入之前/未考虑交互）不成立

**附带发现**：部署 `TKWF.Domain.dll` 已 v4.10.6（含 Forwarder，09:26），但 `TKWF.Domain.Testing.xUnit.dll` **仍 v4.10.5**（08:49）——**部署版本混合**，需框架组同步补齐。

### 建议框架组

1. **成立性确认**：P0-3 需**两个独立修复**（P0-1 读写同源 ✅ 已修 + View 专用填充通道 ❌ 未提供），非 P0-1 自然结果
2. **填充通道方案**（任选）：
   - `TestingEntityDAC` 增加 `SeedViewAsync(IEnumerable<TEntity>)`（写 `_store` 直通，与生产守卫语义不冲突——测试专用）
   - 或提供 `IEntityViewDAC<T>` 测试接口（带 Seed）
   - 或文档明示「VEntity InMemory 测试需 FreeSql 原生 Insert（对齐框架自身测试模式）」+ 修正 v4.10.6 开发方案 L143
3. **修正 SKILL §7.3 示例**（N-1 延续）：当前 `IEntityDAC<PaymentLogStatView>.InsertAsync` 示例仍被守卫拦截
4. **部署同步**：Testing.xUnit.dll 升至 v4.10.6

---

## 修复优先级摘要

| 优先级 | 项 | 修复 | 收益 |
|:---:|:---:|------|------|
| P0 | P0-1 | `SetEntityDAC` 工厂注册共享实例 | 一次性修复双实例/读写隔离/框架内部组件静默失效 |
| P0 | P0-2 | 修正 SKILL.md §7.3 示例 + 全仓 `RootServiceProvider` 检索 | 阻断官方文档引导的陷阱 |
| P0 | P0-3 | VEntity InMemory 测试填充通道（依赖 P0-1） | VEntity 集成测试在 InMemory 可行 |
| P1 | P1-1 | 4 处文档改「方法级守卫」+ 测试 DAC 补守卫 | 消除文档漂移 + 测试信任假象 |
| P1 | P1-2 | `SetEntityDAC` 重复注册检测 | 防静默分歧 |
| P1 | P1-3 | 实例同一性回归测试 + README 更新 | 防回归 + 文档准确 |
| **P0** | **N-1** | SKILL §7.3 示例与 P1-1 守卫矛盾（示例仍不可用） | 消除官方文档错误引导 |
| **P0** | **N-2** | VEntity InMemory 填充通道缺失（P0-3 未落地） | VEntity 聚合正确性可自动化验证 |
| **P0** | **N-3** | 「P0-3 因 P0-1 天然解锁」论断证伪——P0-1/P1-1 正交，需 View 专用 Seed 通道 | 防止错误论断误导消费端 |
| **P1** | **N-4** | 部署版本混合：Testing.xUnit.dll 仍 4.10.5 | 部署一致性 |