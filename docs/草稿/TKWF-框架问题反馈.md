---
title: TKWF 框架问题反馈（XiaoShuTong 实盘发现）
version: v0.1
summary: 从 XiaoShuTong 项目实战中发现的 TKWF 框架问题/隐患/文档偏差，供框架组修正
status: 待框架组评审
date: 2026-09-11
source: XiaoShuTong VEntity 落地 + 框架源码审计（F:\LoongBa_Git\_TKWF）
---

# TKWF 框架问题反馈

> 反馈来源：XiaoShuTong 项目 `PkPlayerStatsView`（VEntity）落地过程中实测发现 + 框架源码三路并行审计。
> 涉及框架能力：VEntity 测试（InMemory DAC）、`IEntityDAC<T>`/`IEntityReadOnlyDAC<T>` 注册机制、tkwf-* skill 文档准确性。
> 优先级：P0 = 影响正确性（测试与生产行为不一致）；P1 = 文档误导开发者踩坑；P2 = 可维护性。

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

## 修复优先级摘要

| 优先级 | 项 | 修复 | 收益 |
|:---:|:---:|------|------|
| P0 | P0-1 | `SetEntityDAC` 工厂注册共享实例 | 一次性修复双实例/读写隔离/框架内部组件静默失效 |
| P0 | P0-2 | 修正 SKILL.md §7.3 示例 + 全仓 `RootServiceProvider` 检索 | 阻断官方文档引导的陷阱 |
| P0 | P0-3 | VEntity InMemory 测试填充通道（依赖 P0-1） | VEntity 集成测试在 InMemory 可行 |
| P1 | P1-1 | 4 处文档改「方法级守卫」+ 测试 DAC 补守卫 | 消除文档漂移 + 测试信任假象 |
| P1 | P1-2 | `SetEntityDAC` 重复注册检测 | 防静默分歧 |
| P1 | P1-3 | 实例同一性回归测试 + README 更新 | 防回归 + 文档准确 |