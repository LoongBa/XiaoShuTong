---
title: 小书童 - 审核报告（AdminWasm-V0.1.8-Tier 1.5 恢复与回归修复）
version: V0.1.8
summary: Tier 1.5（SQLite :memory: 真实视图）恢复（框架 v4.10.14 G6b 修复后）+ 21 个 UId 种子缺陷修复 + G8 内存过滤 + GetDashboardReport 周起点修复 + 框架缺口 G7/G7b 记录
date: 2026-09-13
---

# 小书童 - 审核报告（AdminWasm-V0.1.8-Tier 1.5 恢复与回归修复）

## 一、审核范围

| 提交 | 内容 |
|------|------|
| `b3e1e7b` | Tier 1.5（SQLite :memory: 真实视图）恢复：Fixture 切官方 `UseFreeSqlEntityDAC(Sqlite, "Data Source=:memory:")` + 集合 `DisableParallelization=true`（ADR66）；新建 `XiaoShuTongTestBase`（每 Fact 前 `ResetTier15SqliteMemoryDbAsync`）；66 个测试类继承迁移；GetPkStatsServiceTests 恢复 5 Fact 真实视图聚合；21 个测试种子补 UId |
| `e7e7c62` | 变更记录补 AdminWasm-V0.1.8 条目 |
| `116e8fe` | G8 修复（GetNextQuestionService 知识点过滤移内存层）+ GetDashboardReportService `weekStart` 周一起点修复（周日边界） |
| `5bc70d2` | 框架问题单补 G7b（DateTime? nullable 盲区） |
| `dcdc867` | 变更记录补 09-13 回归修复条目 |

## 二、审核结论

**✅ 通过（有条件）**。Tier 1.5 真实视图聚合测试按官方契约（ADR66 / SKILL §7.1.1）完整恢复——Fixture 配置、串行集合、每 Fact Reset、5 Fact 聚合断言全部符合框架 v4.10.14 G6b 修复后的标准用法。21 个 UId 测试种子缺陷（MockDb 无唯一约束掩盖）清零。G8 按框架文档明示边界在项目侧改内存过滤（不绕过）。**剩余 7 个失败为框架缺口 G7b（DateTime? nullable TypeHandler 未注册）**，已探针实证并记录问题单，待框架组修复（见遗留缺陷）。

## 三、需求符合度

| 需求 | 符合度 | 说明 |
|------|:---:|------|
| Tier 1.5 聚合测试恢复（SKILL §7.1.1 / ADR66） | ✅ | Fixture 官方配置 + 集合 `DisableParallelization=true` + 每 Fact 前 Reset（原地清空，引用不失效） |
| Pk 聚合 5 Fact（仅 Finished 计入/RLS/胜率 API 层/舍入） | ✅ | 真实视图执行，写基表→Service 读聚合全链路；隔离（OnlyOwnMatches）/零值（NoMatches）/舍入（0.62 银行家舍入）全覆盖 |
| 21 个 UId 种子缺陷修复 | ✅ | 实体未实现 `IEntityTracked`（框架不自动生成 UId），测试种子显式补 `UidGenerator.NewId()`——与生产 Service 一致 |
| G8 string[] Contains 边界处置 | ✅ | 按框架文档明示边界，`GetNextQuestionService` 知识点过滤移内存层（先查全量再 `Where`）；全项目审计仅此 1 处需改 |
| GetDashboardReport 周起点 | ✅ | `weekStart` 改周一起点公式 `today-((DayOfWeek+6)%7)`（原周日退化漏周六） |

## 四、架构符合度

| 检查项 | 结果 |
|--------|:---:|
| ADR66（Tier 1.5 测试库隔离契约） | ✅ 每 Fact 前 `Host.ResetTier15SqliteMemoryDbAsync()`（v4.10.14 原地清空，非重铸新库——会话 scope 引用保持有效） |
| 测试分层（ADR60 Tier 1/1.5/2） | ✅ 全套件迁 Tier 1.5（SQLite :memory:），G7b 缺口外全绿 |
| 测试基类 | ✅ 新建 `XiaoShuTongTestBase`（中间层，override `OnInitializeAsync` 挂 Reset）——66 类统一继承，ast_grep 批量替换无误 |
| POCO 管控 | ✅ 查询复用 `{Entity}Dto`/VEntity Dto，未新建 POCO；G8 内存过滤不引入新类型 |
| 生成物管控 | ✅ 无生成源码改动；测试文件为主 |

## 五、代码质量

| 项 | 评价 |
|----|------|
| Fixture 配置 | 官方 `UseFreeSqlEntityDAC(FreeSql.DataType.Sqlite, "Data Source=:memory:")` 单点用法（无消费端兜底），符合"不绕过框架"原则 |
| XiaoShuTongTestBase | `OnInitializeAsync` 虚方法挂载（非遮蔽基类 IAsyncLifetime），xUnit 保证每 Fact 前执行；`Fixture.Host != null` 防御 |
| Pk 5 Fact 断言 | 数据独立（userId 段隔离）、口径注释完整（BR-24/25/26）、银行家舍入 0.62 精确 |
| UId 补种 | 18 处实体初始化器 + 5 处 using，风格与 Pk 域既有种子一致 |
| G8 内存过滤 | 注释说明方言边界（G8）+ 量级评估（单题库 <1000）；生产 PG 与 Tier 1.5 语义一致 |
| 周起点公式 | 注释含根因（周日 DayOfWeek=0 退化）+ 推导（周日→-6，周一→0，周六→-5） |
| 无残留 | 探针测试已删（工作区干净）；无 Skip 测试 |

## 六、测试情况

### 自动化测试

**全套件：338 通过 / 7 失败 / 2 跳过**（Tier 1.5 SQLite :memory:，净增 4 清零）

| 阶段 | 通过 | 失败 | 说明 |
|------|:---:|:---:|------|
| 迁移后首跑 | 315 | 30 | 21 UId 约束 + 3 G8 方言 + 6 G7 DateTime（初判） |
| UId 修复后 | 335 | 10 | 21 UId 清零；G7×7 + G8×3 |
| 框架 v4.10.18 + 项目修复 | 338 | 7 | G8×3 + GetDashboardReport×1 清零；**剩 7 = G7b（DateTime? nullable）** |

- GetPkStatsServiceTests：5 Fact 全通过（视图真实执行 + 每 Fact 隔离）
- G8：GetNextQuestionServiceTests 3 用例全通过（内存过滤）
- GetDashboardReportServiceTests：全通过（周起点修复，周日实证）
- 编译：全解决方案 0 错误 0 警告；lsp_diagnostics 干净

### 遗留缺陷（框架缺口，非本项目缺陷）

**G7b：DateTimeUtcHandler 未覆盖 `DateTime?`（nullable）**——7 个测试仍 +8h：

```
现象：v4.10.18（G7 修复已含）下 nullable DateTime? 字段往返仍 +8h（Kind=Unspecified）
     探针实证：期望 2026-09-19T22:33:24.5813690Z / 实际 2026-09-20T06:33:24.5813690
根因：SqliteTypeHandlerRegistrar.EnsureRegistered() 只注册 TypeHandlers[typeof(DateTime)]，
     未注册 typeof(DateTime?)——FreeSql 对 Nullable<DateTime> 属性按 typeof(DateTime?) 查字典
     不命中 → 走 System.Data.SQLite 原生路径（Kind 丢失 +8h）
证据：框架官方 G7 回归测试实体 Tier15DateTimeEntity.CreatedAt 是非空 DateTime（命中 handler）；
     nullable 场景框架未覆盖
影响：XiaoShuTong 7 用例（ListSubscriptions×1 / CancelSubscription×1 / ListMyTasks×2 /
     ExportRosterCsv×2 / GenerateInviteCodes×1）
建议：EnsureRegistered() 补 TypeHandlers[typeof(DateTime?)] = new DateTimeUtcHandler()；
     官方 G7_DateTimeUtcRoundTrip 加 nullable 变体
```

**缓解**：7 个失败全部为 G7b 单一框架缺口（DateTime? TypeHandler 注册缺失），项目侧无绕过方案（按用户原则不绕过）；已记录框架问题单（`docs/草稿/框架问题单-Tier1.5-SQLite内存库-v4.10.8不可用.md` G7b 节），待框架组修复后验证清零。

## 七、ADR 执行情况

| ADR | 执行 |
|-----|:---:|
| ADR66（Tier 1.5 每 Fact 重置契约） | ✅ Reset 原地清空（v4.10.14 G6b）——会话 scope 引用不失效，标准 `User.Use<TService>()` 路径兼容 |
| ADR60（测试 DAC 收敛） | ✅ 全套件迁 Tier 1.5；VEntity 聚合经真实视图验证 |
| ADR53（方言变体） | ✅ ViewSqlSQLite 已就绪（V0.1.7），本次真实执行 |
| 生成代码防绕过 | ✅ 无手写 Controller/DataService/.g.cs；UId 显式设置与生产一致（非绕过框架审计） |

## 八、待改进项

| 优先级 | 项 | 说明 |
|:---:|------|------|
| P0 | G7b 框架缺口（DateTime? TypeHandler） | `TypeHandlers[typeof(DateTime?)]` 注册缺失——已记录问题单，待框架组修复（v4.10.19+）后 7 用例清零 |
| P1 | 全套件全绿验收 | 框架修复 G7b 后全套件目标 345 通过 / 0 失败 / 2 跳过；届时补 tag（子版本 +1，需征求同意） |

## 九、审核结论

**✅ 通过（有条件）**——Tier 1.5 真实视图聚合按官方契约完整恢复（Fixture/串行/Reset/5 Fact 全合规），21 个 UId 种子缺陷与 G8 边界（内存过滤）、周起点缺陷（周日边界）全部正确修复，338 通过基线稳定。**条件**：剩余 7 个失败为框架缺口 G7b（DateTime? nullable TypeHandler 未注册），项目侧不绕过，已记录问题单待框架组修复后验证清零并补 tag。
