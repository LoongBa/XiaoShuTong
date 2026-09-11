---
title: 小书童 - 审核报告（AdminWasm-V0.1.7-框架 v4.10.8 适配与 Tier 1.5 迁移尝试）
version: V0.1.7
summary: 框架 v4.10.8 适配（SeedViewAsync/TestingEntityDAC 退役后测试基建迁移）+ Tier 1.5 SQLite 迁移尝试（发现框架缺口回退）+ ViewSqlSQLite 方言就绪
date: 2026-09-12
---

# 小书童 - 审核报告（AdminWasm-V0.1.7-框架 v4.10.8 适配与 Tier 1.5 迁移尝试）

## 一、审核范围

| 提交 | 内容 |
|------|------|
| `7e365c3` | 移除 SeedViewAsync/TestingEntityDAC 残留（框架 v4.10.7 退役，用户裁定彻底移除） |
| `ade93f4` | 框架 v4.10.8 适配：PkPlayerStatsView 补 ViewSqlSQLite 方言 + 测试依赖补齐 + Tier 1.5 迁移尝试（发现框架缺口回退 Tier 1）+ .gitignore 修正 |

## 二、审核结论

**✅ 通过（有条件）**。框架 v4.10.8 适配正确（SeedViewAsync 退役合规处理），测试基建迁移 MockDbEntityDAC 完成，ViewSqlSQLite 方言就绪。**框架缺口**（Tier 1.5 SQLite :memory: ObjectDisposed）已准确识别并记录，回退 Tier 1 基线无损。

## 三、需求符合度

| 需求 | 符合度 | 说明 |
|------|:---:|------|
| SeedViewAsync 退役后测试合规 | ✅ | GetPkStatsServiceTests 移除 SeedViewAsync/TestingEntityDAC 全部残留，保留 BR-24 降级用例 |
| MockDbEntityDAC 迁移 | ✅ | LlmGatewayTests 特判改 MockDbEntityDAC（对齐 SKILL §4.2 规则 8） |
| ViewSqlSQLite 方言 | ✅ | PkPlayerStatsView 补 SQLite 变体（FILTER→CASE WHEN，列别名与 C# 属性一致，VIEW001 校验通过） |
| 测试依赖 | ✅ | FreeSql.Provider.Sqlite（CPM 3.5.311）+ TKWF.Domain.Testing.Mock 引用 |
| 聚合验证恢复 | ⏸ | 待框架 Tier 1.5 修复（见遗留缺陷） |

## 四、架构符合度

| 检查项 | 结果 |
|--------|:---:|
| VEntity 方言机制（ADR53） | ✅ 遵守——ViewSqlSQLite 变体经 `ViewSqlByProvider` 运行时选择 |
| 测试分层（ADR60 Tier 1/1.5/2） | ✅ 回退 Tier 1（MockDbEntityDAC）正确；Tier 1.5 尝试符合框架推荐但遇框架缺口 |
| 测试数据隔离 | ✅ SQLite :memory: 全 Collection 共享 DB，Id 唯一模式（userId*1000+i）已设计 |
| 生成物管控 | ✅ .gitignore 补 `src/**/.xCodeGen/Generated/`（误跟踪修正） |

## 五、代码质量

| 项 | 评价 |
|----|------|
| ViewSqlSQLite 方言 | 语义等价（COUNT FILTER→SUM CASE WHEN，两库 COUNT/SUM NULL 传播一致），注释完整 |
| Fixture 回退 | 保留 Tier 1.5 配置思路注释 + 框架缺口说明，便于框架修复后快速启用 |
| 依赖管理 | CPM 集中（Directory.Packages.props 3.5.311 与 FreeSql 系列一致）；Mock 引用带条件注释 |
| 无残留 | 全仓 0 处 SeedViewAsync/TestingEntityDAC 引用（grep 验证） |

## 六、测试情况

### 自动化测试

**全套件：341 通过 / 0 失败 / 2 跳过**（Tier 1 MockDbEntityDAC 基线，与迁移前一致）

- GetPkStatsServiceTests：BR-24 零值降级用例保留（Tier 1 下 VEntity 只读空集）
- LlmGatewayTests：MockDbEntityDAC.Clear() 隔离正常（补 Mock 引用后编译通过）
- 编译：全解决方案 0 错误 0 警告

### 遗留缺陷（框架缺口，非本项目缺陷）

**Tier 1.5（ConfigTestDomainAsync + SQLite :memory:）在框架 v4.10.8 实际不可用**：

```
现象：SyncViewsAsync 执行 ViewSql 时抛 ObjectDisposedException
      （FreeSql SQLite 连接池 :memory: 不保活，SyncStructure 后连接释放）
根因：FreeSql 默认连接池对 :memory: 内存库——连接归还/释放后库销毁；
      框架 ConfigTestDomainAsync 链路（SyncTables→SyncViewsAsync 用同一 IFreeSql）
      与裸 FreeSqlBuilder（框架自身测试模式，单例常驻连接）生命周期不同
证据：框架 _Tests 全用裸 FreeSqlBuilder（绕过 DomainHost），908/908 测试从未验证
      ConfigTestDomainAsync + SQLite :memory: 组合——ADR60/v4.10.7 文档"可用"未实证
影响：VEntity 聚合正确性（仅 Finished 计入、Count/Sum、胜率）无法在 InMemory 下自动化验证
```

**缓解**：回退 Tier 1 基线无损（341 通过）；`PkPlayerStatsView.ViewSqlSQLite` 已就绪，框架修复后改 Fixture 一行即可启用。

## 七、ADR 执行情况

| ADR | 执行 |
|-----|:---:|
| ADR53（方言变体） | ✅ ViewSqlSQLite 变体正确声明 |
| ADR60（测试 DAC 收敛） | ✅ 遵守 Tier 分层；Tier 1.5 尝试符合推荐但发现框架实现缺口 |
| ADR55（方言翻译尽力而为） | ✅ 手写方言变体（未依赖自动翻译） |

## 八、待改进项

| 优先级 | 项 | 说明 |
|:---:|------|------|
| P0 | 框架 Tier 1.5 修复（已记录变更记录） | ConfigTestDomainAsync + SQLite :memory: ObjectDisposed——需框架组处理 FreeSql SQLite 连接池 :memory: 保活，或文档明示仅支持裸 FreeSqlBuilder |
| P1 | 聚合验证恢复 | 框架修复后：Fixture 改回 UseFreeSqlEntityDAC(Sqlite,:memory:) + GetPkStatsServiceTests 恢复 5 聚合用例（基表写入方案已设计） |
| P2 | Mock 引用自动注入 | 框架 targets 未自动注入 TKWF.Domain.Testing.Mock（默认 DAC 已是 MockDbEntityDAC）——建议框架组补注入，消除项目侧显式 Reference |

## 九、审核结论

**✅ 通过（有条件）**——框架 v4.10.8 适配正确完整，测试基线无损；Tier 1.5 迁移因框架缺口回退但方言/依赖/方案全部就绪，框架修复后可一行启用。框架缺口（Tier 1.5 ObjectDisposed + Mock 未自动注入）已记录变更记录，待框架组处理。
