---
title: 框架问题单——EntityUpdateBatchAsync 对游离实体不可用（UpdateRange 要求已跟踪，文档未明示）（G15）
status: 提议（待框架组确认）
date: 2026-09-30
source: XiaoShuTong V0.7.3 Job 类 N+1 修复（2026-09-30）——BankHintBackfillJob 写侧批量改造实证
---

# 框架问题单：EntityUpdateBatchAsync 对游离实体不可用（UpdateRange 要求已跟踪，文档未明示）（G15）

> **背景**：G11-G14 问题单见 `docs/草稿/`（G11 SG3 消歧缺口 / G12 会话失效勘误 / G13 固定 sessionKey / G14 DomainErrorFilter）。本问题单为 **2026-09-30 V0.7.3 Job N+1 修复** 新发现：`DomainDataServiceBase.EntityUpdateBatchAsync` 对 `EntitySelectAsync` 返回的**游离实体**批量更新抛"未跟踪"异常，与 API 参考文档描述不符。
>
> **实证链路**：`BankHintBackfillJob` 写侧改造（foreach 逐行 `EntityUpdateAsync` → 批量）——`EntityUpdateBatchAsync(toUpdate, ct)` 运行时抛 `System.Exception: 不可更新，数据未被跟踪，应该先查询 或者 Attach`（FreeSql `DbSet.CanUpdate`）；改用 `EntityUpdateColumnsBatchAsync`（SetSource 路径）后正常。

## 一、现象

### 期望行为（API 参考文档）

`_TKWF\docs\AC-Kit\references\DataService-API-参考.md` 列出的 `DomainDataServiceBase<TEntity>` 方法：

| 方法 | 返回 | 说明 |
|:---|:---|:---|
| `EntityUpdateAsync(...)` | `Task` | 更新 |
| `EntityUpdateBatchAsync(...)` | `Task` | 批量更新 |
| `EntityUpdateColumnsBatchAsync(...)` | `Task<int>` | 指定列批量更新 |

文档对 `EntityUpdateBatchAsync` 未标注"要求实体已 attach/被跟踪"的前置条件——按常规理解应与 `EntityUpdateAsync` 同级可用（对查询返回的实体做批量更新）。

### 实际行为（FreeSql 运行时）

```text
System.Exception : 不可更新，数据未被跟踪，应该先查询 或者 Attach：(1, be96530aba5941dabac119bed2797293, Q-46001a, ...)
   at FreeSql.DbSet`1.CanUpdate(TEntity data, Boolean isThrow)
   at FreeSql.DbSet`1.CanUpdate(IEnumerable`1 data, Boolean isThrow)
   at FreeSql.DbSet`1.UpdateRangePriv(IEnumerable`1 data, Boolean isCheck)
   at FreeSql.DbSet`1.UpdateRange(IEnumerable`1 data)
   at FreeSql.BaseRepository`1.UpdateAsync(IEnumerable`1 entities, CancellationToken cancellationToken)
   at TKW.Framework.Domain.FreeSql.FreeSqlEntityDAC`1.UpdateBatchAsync(IEnumerable`1 entities, CancellationToken ct)
   at TKW.Framework.Domain.DomainDataServiceBase`2.InternalUpdateBatchAsync(IEnumerable`1 entities, CancellationToken ct)
```

`EntitySelectAsync` 返回的实体是**分离式查询产物（游离实体，未挂载到仓储跟踪上下文）**——逐行 `EntityUpdateAsync`（内部 `IBaseRepository.UpdateAsync(entity)` 按主键 Where 定位）不需要跟踪即可工作，但批量 `UpdateBatchAsync`（FreeSql `UpdateRange`）要求实体已 attach。

## 二、根因（源码定位，待框架确认）

| 环节 | 代码/实现 | 问题 |
|------|----------|------|
| **DataService 基类** | `DomainDataServiceBase.InternalUpdateBatchAsync` → `dac.UpdateBatchAsync(entityArray, ct)` | 直接委托 DAC，无 attach/游离实体适配 |
| **FreeSql DAC** | `FreeSqlEntityDAC.UpdateBatchAsync` → `GetRepo().UpdateAsync(entityList, ct)`（V4.9.84 修复恢复 GetRepo 路径） | FreeSql `IBaseRepository.UpdateAsync(IEnumerable)` 内部走 `UpdateRange` → `CanUpdate` 校验跟踪状态，游离实体抛"不可更新，数据未被跟踪" |
| **对照 UpdateColumnsBatchAsync** | `FreeSqlEntityDAC.UpdateColumnsBatchAsync` → `Orm.Update<TEntity>().SetSource(source).UpdateColumns(objectExpr).ExecuteAffrowsAsync` | **SetSource 路径支持游离实体列表**（不要求 attach）——实测可用 |
| **文档** | `DataService-API-参考.md` 批量更新行 | ❌ 未标注"要求已跟踪实体"前置条件 |

**根因一句话**：`EntityUpdateBatchAsync` 底层走 FreeSql `UpdateRange`（要求实体已 attach），与 `EntitySelectAsync`（返回游离实体）组合不可用；而 `EntityUpdateColumnsBatchAsync` 走 `SetSource`（支持游离实体）——两条批量写路径对**实体跟踪状态的要求不一致**，且 API 文档未明示该差异。

## 三、影响

- **当前（XiaoShuTong）**：V0.7.3 `BankHintBackfillJob` 已改用 `EntityUpdateColumnsBatchAsync`（仅更新 Hint 列，SetSource 路径可用）——**已规避，未阻断**。Job 场景"整实体批量更新"（含审计字段刷写）无法用 `EntityUpdateBatchAsync`。
- **潜在**：任何消费方按"查询 → 批量更新"常规模式使用 `EntityUpdateBatchAsync`（如 Job 批量处理、管理端批量编辑）→ 运行时抛"未跟踪"异常；`EntitySelectAsync` 是 DataService 最常用查询入口，该组合是自然预期。
- **结构性矛盾**：`EntityUpdateBatchAsync` 与 `EntityUpdateAsync` 的可用实体来源语义不一致（后者游离子可用、前者要求 attach），API 参考文档将它们并列列出且未注明差异——遵循文档的项目**大概率踩坑**。

## 四、建议框架侧（三选一，按推荐序）

| # | 方案 | 说明 |
|---|------|------|
| 1 | **FreeSql DAC `UpdateBatchAsync` 改用 SetSource 路径**（对齐 `UpdateColumnsBatchAsync` 的 `Orm.Update<TEntity>().SetSource(source)` 全实体更新），消除两条批量路径的 attach 要求差异 | 根因修复；`SetSource` 已在本项目实测支持游离实体列表；对齐逐行 `EntityUpdateAsync` 的"查询后可更新"语义 |
| 2 | **API 文档补明前置条件**（`EntityUpdateBatchAsync` 标注"要求实体已 attach/被跟踪；游离实体请用 `EntityUpdateColumnsBatchAsync` 或逐行 `EntityUpdateAsync`"） | 最小改动；但无法阻止误用，且"为什么逐行可用批量不可用"的认知裂缝仍在 |
| 3 | **DataService 基类 `InternalUpdateBatchAsync` 做游离实体检测/适配**（如先 attach 再批量，或检测到游离实体自动转 SetSource） | 对消费方透明；但 attach 语义需谨慎（可能与 UoW/多租户冲突） |

**附加建议**：评估 `EntityCreateBatchAsync`（`InsertBatchAsync` → `GetRepo().InsertAsync`）是否也有类似跟踪要求——`InsertAsync(批量)` 通常不要求 attach（新建实体），但建议一并文档化。

## 五、验收标准（XiaoShuTong 侧）

框架修复 + 部署后，在 XiaoShuTong 验证：
- **主验收**：`BankHintBackfillJob` 改回 `EntityUpdateBatchAsync(toUpdate, ct)`（方案 1 下应正常）→ 测试 `ExecuteAsync_EmptyHint_BackfillsFromCard` 全绿
- **回归**：`EntityUpdateColumnsBatchAsync` 路径（现用）不回归；逐行 `EntityUpdateAsync` 不回归
- 建议框架侧补用例：`EntitySelectAsync` 返回实体 → `EntityUpdateBatchAsync` 批量更新 → 断言成功（FreeSql SQLite :memory: + MySQL 双库）
