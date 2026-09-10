---
title: 小书童 - P0 查询视图（VEntity）设计规格
version: v0.1
summary: 群主看板/榜单/家长总览/周期报告/PK 战绩 5 个跨表聚合查询的 VEntity 设计（替代 C# 内存聚合 + 手写 POCO）
status: 设计评审中
date: 2026-09-11
source: Services 查询模式审查 + 平台管理系统需求方案 v0.4 + 各 Service 实现口径
---

# P0 VEntity 设计规格（查询视图）

## 〇、设计背景

### 0.1 动机

全仓 79 个 Service 审查确认：跨表聚合 + 内存聚合（GroupBy/Sum/Average/Count）+ N+1 循环查询共 40+ 处，全部走 **Entity 全量拉取 → C# 内存聚合 → 手写 POCO Dto**。按 Dto 最小化决策树：「跨表 JOIN / SQL 聚合 / 报表 / 看板 → ✅ 声明 VEntity」，本设计将 P0 五类高频跨表聚合下沉为 SQL 视图，由 `User.Query<T>()`（EQR）直接消费。

### 0.2 框架约束（设计必须满足）

| 约束 | 要求 |
|------|------|
| 三件套 | `IsView=true` + `ViewSql` + `[Table("vw_...", DisableSyncStructure=true)]` |
| `long Id` | 编译期强制；业务唯一键优先（方案 A），纯报表用 ROW_NUMBER（方案 B） |
| 四层一致性 | 列别名=C# 属性名（VIEW001 双向校验）；类型匹配（SUM→decimal/long） |
| PG 引号 | ViewSql 表名+列名全双引号（防 42703） |
| StatsDto 门控 | 仅 SUM(/COUNT(/AVG( 生成 StatsDto；MIN/MAX 不触发 |
| 视图链禁令 | 只引用基表，禁止 FROM 其他 vw_ |
| 查询通道 | 仅 GraphQL（默认开）；REST 需 Service 包装；禁 IEntityDAC<ViewEntity> |
| 生产视图 | 开发环境自动同步；生产 DBA 手动建 |
| 子域归属 | VEntity 归**消费域**（谁消费归谁） |

### 0.3 全局统一口径（跨切片唯一来源）

| 口径 | 定义 | 来源 |
|------|------|------|
| 执行率 | Completed 分配数 / 全部分配数（分母含 Overdue） | 任务-BR-17 |
| 平均进度 | 全部分配 Progress 均值 | GetOwnerDashboardService |
| 胜率 | 胜场 / Finished 总场次 | Pk-BR-26 |
| 战绩指标 | accuracy+mastery 简单求和 | Rank-BR-12 |
| 战力指标 | streak+volume+pk_wins 简单求和 | Rank-BR-11 |
| 正确率（个人） | 有记录日正确率均值 | 激励-BR-11 |

---

## 一、vw_owner_dashboard ── 群主看板聚合

**表名**：`vw_owner_dashboard`
**子域**：TaskManagement（消费域）
**Id 构造**：方案 B ROW_NUMBER（看板为聚合报表，无业务唯一键）
**视图类型**：普通视图（VEntity）

**引用基表**：
- TaskManagement.Tasks（任务）
- TaskManagement.TaskAssignments（分配）
- GroupManagement.Groups（群组，关联 OwnerId/GroupId）
- GroupManagement.GroupMembers（成员）
- Learning.KnowledgeMastery（掌握度）

**Join 结构**：INNER Tasks→TaskAssignments（TaskId）；INNER GroupMembers→KnowledgeMastery（UserId）；LEFT Groups（Owner 过滤）
**优化栅栏**：GROUP BY + 聚合函数（谓词下推受限，需按 GroupId 过滤）

**聚合粒度**：按 GroupId 聚合

**字段表**（= 看板展示字段超集）：

| 字段 | 类型 | 来源列/聚合 | 备注 |
|------|------|-----------|------|
| Id | long | ROW_NUMBER() OVER (ORDER BY g.Id) | Id 构造 |
| GroupId | long | Groups.Id | 过滤键 |
| OwnerId | long | Groups.OwnerId | 行级权限（RLS：仅 Owner） |
| TaskCount | long | COUNT(t.Id) | 任务数 |
| AssignmentCount | long | COUNT(a.Id) | 分配数 |
| CompletedCount | long | COUNT(a.Id) FILTER (WHERE a.Status='Completed') | 已完成 |
| OverdueCount | long | COUNT(a.Id) FILTER (WHERE a.Status='Overdue') | 逾期 |
| AvgProgress | decimal | AVG(a.Progress) | 平均进度 |
| MemberCount | long | COUNT(DISTINCT gm.UserId) | 成员数 |
| WeakPointCount | long | COUNT(DISTINCT km.KnowledgePoint) | 薄弱知识点数 |

**规范 DDL 要点**（方言中立）：
```
GROUP BY g.Id → 每群组一行
任务级/成员级计数经 Join 后可能重复——需 COUNT(DISTINCT) 消重
```
> ⚠️ **聚合口径歧义**：TaskCount（COUNT 任务）+ AssignmentCount（COUNT 分配）+ CompletedCount（COUNT 已完成分配）三者在 Join 后需确认计数归属（见 Oracle 评审点 1）。

**数据权限**：行级 WHERE `OwnerId = 当前用户`（RLS 策略注入）
**性能评估**：按 GroupId 索引；GROUP BY 无法谓词下推——**看板高频场景建议在 Service 层加 GroupId 参数过滤**（见 Oracle 评审点 2）
**查询通道**：EQR `User.Query<OwnerDashboardView>()` + GraphQL Connection 开；REST 关（Service 包装）

---

## 二、vw_ranking_board ── 榜单聚合

**表名**：`vw_ranking_board`
**子域**：Rank（消费域）
**Id 构造**：方案 B ROW_NUMBER（榜单为排序报表）
**视图类型**：普通视图

**引用基表**：
- Rank.RankSnapshots（快照，单表）

**Join 结构**：无 Join（单表聚合）
**优化栅栏**：GROUP BY UserId + SUM/聚合

**聚合粒度**：按 (ScopeType, ScopeId, Subject, SnapshotDate, UserId, BoardType) 聚合

**字段表**：

| 字段 | 类型 | 来源列/聚合 | 备注 |
|------|------|-----------|------|
| Id | long | ROW_NUMBER() OVER (...)` | Id 构造 |
| ScopeType | string | RankSnapshots.ScopeType | 过滤键 |
| ScopeId | string | RankSnapshots.ScopeId | 过滤键 |
| Subject | string | RankSnapshots.Subject | 过滤键 |
| SnapshotDate | DateOnly | RankSnapshots.SnapshotDate | 过滤键 |
| UserId | long | RankSnapshots.UserId | 分组键 |
| BoardType | string | CASE MetricType 映射 | Combat/Performance |
| MetricValue | decimal | SUM(MetricValue) | 组合指标求和 |
| Rank | long | MIN(Rank) | 排名取最小 |

**规范 DDL 要点**：
```
BoardType 映射（方言中立，编码时译 CASE WHEN）：
  Combat      = Streak + Volume + PkWins 三指标 SUM
  Performance = Accuracy + Mastery 两指标 SUM
```
> ⚠️ **动态指标集问题**：现有实现按 BoardType 动态选择指标集（Combat=3 指标 / Performance=2 指标）。视图需把指标集**编码为 BoardType 列**（CASE），否则无法静态表达（见 Oracle 评审点 3）。

**数据权限**：行级 ScopeId 过滤（群组范围需 Groups.RankEnabled 门控——该门控在 Service 层做，视图不含）
**性能评估**：按 (ScopeType, ScopeId, Subject, SnapshotDate, MetricType) 索引
**查询通道**：EQR + GraphQL 开；REST 关

---

## 三、vw_parent_dashboard ── 家长成长总览

**表名**：`vw_parent_dashboard`
**子域**：Parent（消费域）
**Id 构造**：方案 B ROW_NUMBER
**视图类型**：普通视图

**引用基表**：
- Learning.DailyStats（每日统计）
- Learning.KnowledgeMastery（掌握度）
- TaskManagement.TaskAssignments（任务分配）

**Join 结构**：INNER DailyStats→KnowledgeMastery（UserId）；LEFT TaskAssignments（UserId）
**优化栅栏**：GROUP BY UserId + SUM/AVG + 周窗口

**聚合粒度**：按 (UserId, 周窗口) 聚合

**字段表**：

| 字段 | 类型 | 来源列/聚合 | 备注 |
|------|------|-----------|------|
| Id | long | ROW_NUMBER() | Id 构造 |
| StudentId | long | DailyStats.UserId | 过滤键 |
| WeekLearnedCount | long | SUM(LearnedCount)（当周） | 本周学习量 |
| WeekAccuracy | decimal | AVG(Accuracy)（当周有记录日） | 本周正确率 |
| Subject | string | KnowledgeMastery.Subject | 分组键 |
| SubjectAccuracy | decimal | AVG(Accuracy) | 学科正确率 |
| StreakDays | long | 派生（日期连续性，视图无法 SQL 化） | ⚠️ 见下 |

> ⚠️ **StreakDays（坚持天数）**：现由 `StreakCalculator.CalcCurrentStreak`（C# 日期连续性算法）计算——**无法在 SQL 视图表达**（需要窗口函数遍历连续日期）。设计决策：视图不含 StreakDays，仍由 Service 计算（见 Oracle 评审点 4）。

**规范 DDL 要点**：
```
周窗口：StatDate >= 本周一 AND <= 今日（动态，Service 传参）
订阅门控：未订阅仅前 2 项预览（今日完成 + 坚持天数）——门控在 Service 层，视图仅出完整聚合
```
> ⚠️ **动态作用域问题**：视图聚合是"全量按 UserId"，周窗口/订阅门控均为运行时参数——视图无法静态固化周范围。设计决策：**视图按 UserId 全量聚合，周过滤由 Service 查询参数下推**（见 Oracle 评审点 5）。

**数据权限**：行级 StudentId 过滤（家长→孩子授权链在 Service 层，视图含 StudentId 列）
**性能评估**：按 UserId 索引；周过滤谓词下推需 Service 传 StatDate 范围
**查询通道**：EQR + GraphQL 开；REST 关

---

## 四、vw_period_report ── 周期学习报告

**表名**：`vw_period_report`
**子域**：Stats（消费域）
**Id 构造**：方案 B ROW_NUMBER
**视图类型**：普通视图

**引用基表**：
- Learning.DailyStats（每日统计）
- Learning.KnowledgeMastery（掌握度）

**Join 结构**：INNER DailyStats→KnowledgeMastery（UserId）
**优化栅栏**：GROUP BY UserId + SUM/AVG

**聚合粒度**：按 (UserId, 周期) 聚合

**字段表**：

| 字段 | 类型 | 来源列/聚合 | 备注 |
|------|------|-----------|------|
| Id | long | ROW_NUMBER() | Id 构造 |
| UserId | long | DailyStats.UserId | 过滤键 |
| LearnedCount | long | SUM(LearnedCount) | 学习量 |
| StarredCount | long | SUM(StarredCount) | ★数 |
| Accuracy | decimal | AVG(Accuracy)（有记录日） | 个人正确率 |
| WeakPointCount | long | COUNT(DISTINCT KnowledgePoint WHERE Accuracy<0.6) | 薄弱点数 |

> ⚠️ **动态区间问题**：周期（Week=近7天 / Month=近30天）是前端动态换算的**动态区间**——视图无法静态固化。设计决策同 §三：**视图按 UserId 全量聚合，区间过滤由 Service 查询参数下推**（见 Oracle 评审点 6）。

> ⚠️ **薄弱点 Top5**：现有实现按 Accuracy 升序取 5 条——视图聚合后需排序取前 N，GraphQL Connection 分页可满足（order + first=5）。

**数据权限**：行级 UserId = 当前用户（RLS）
**性能评估**：按 UserId 索引
**查询通道**：EQR + GraphQL 开；REST 关

---

## 五、vw_pk_player_stats ── PK 战绩

**表名**：`vw_pk_player_stats`
**子域**：Pk（消费域）
**Id 构造**：方案 A 业务唯一键（UserId 透传）
**视图类型**：普通视图（注释已认领：GetPkStatsService 标注"PkPlayerStats 视图（VEntity 设计规格）"）

**引用基表**：
- Pk.PkPlayers（参赛记录）
- Pk.PkMatches（对局）

**Join 结构**：INNER PkPlayers→PkMatches（MatchId），仅 Finished 对局
**优化栅栏**：GROUP BY UserId + COUNT/SUM

**聚合粒度**：按 UserId 聚合（全部 Finished 对局）

**字段表**：

| 字段 | 类型 | 来源列/聚合 | 备注 |
|------|------|-----------|------|
| Id | long | PkPlayers.UserId（透传） | Id 构造（方案 A） |
| UserId | long | PkPlayers.UserId | 分组键 + RLS |
| TotalMatches | long | COUNT(p.Id) | 总场次（仅 Finished） |
| Wins | long | COUNT(*) FILTER (WHERE m.WinnerId = p.UserId) | 胜场 |
| Draws | long | COUNT(*) FILTER (WHERE m.WinnerId IS NULL) | 平场 |
| TotalScore | decimal | SUM(p.Score) | 累计分 |

> **胜率**：BR-26 定义"胜率 API 层计算"——视图出 Wins/TotalMatches，胜率由 Service 除法计算（视图不含除法避免 decimal 精度歧义，见 Oracle 评审点 7）。

**规范 DDL 要点**：
```
仅 Finished 计入：WHERE m.Status = 'Finished'（对齐注释"口径与视图一致：仅 Finished 计入"）
```
**数据权限**：行级 UserId = 当前用户（RLS，BR-25）
**性能评估**：按 PkPlayers.UserId 索引；PkMatches.Status 索引
**查询通道**：EQR + GraphQL 开；REST 关

---

## 六、查询通道暴露决策汇总

| 视图 | EQR | GraphQL Connection | REST | 消费方 |
|------|:---:|:---:|:---:|--------|
| vw_owner_dashboard | ✅ | ✅ | 关（Service 包装） | 群主看板 |
| vw_ranking_board | ✅ | ✅ | 关（Service 包装） | 榜单页 |
| vw_parent_dashboard | ✅ | ✅ | 关（Service 包装） | 家长总览 |
| vw_period_report | ✅ | ✅ | 关（Service 包装） | 学习报告 |
| vw_pk_player_stats | ✅ | ✅ | 关（Service 包装） | PK 战绩页 |

---

## 七、待 Oracle 评审决策点（7 项）

| # | 决策点 | 现状 | 设计倾向 |
|:-:|--------|------|---------|
| 1 | **聚合口径歧义**（vw_owner_dashboard） | Join 后任务/分配/成员计数可能重复 | COUNT(DISTINCT) 消重 or 拆分多视图 |
| 2 | **看板谓词下推** | GROUP BY 无法下推，看板高频查询 | Service 传 GroupId 参数 or 接受全量 |
| 3 | **榜单动态指标集** | BoardType 动态选指标 | CASE WHEN 编码 BoardType 列 |
| 4 | **StreakDays 无法 SQL 化** | C# 日期连续性算法 | 视图不含，Service 保留 |
| 5 | **家长报告动态作用域** | 订阅门控 + 周窗口运行时参数 | 视图全量按 UserId，Service 过滤 |
| 6 | **周期报告动态区间** | Week/Month 动态换算 | 视图全量按 UserId，Service 过滤 |
| 7 | **胜率计算位置** | BR-26"API 层计算" | 视图出分子分母，Service 除法 |

---

## 八、变更记录

| 日期 | 版本 | 变更 |
|------|------|------|
| 2026-09-11 | v0.1 | 初始 P0 设计（5 视图规格 + 7 评审点），待 Oracle 评审 |
| 2026-09-11 | v0.2 | Oracle 评审落地：**裁决仅 #5 vw_pk_player_stats 进入实现**；#1/#3/#4 否决（Cartesian product 污染 SUM/AVG + 日期维度塌缩）；#2 延后（单表无收益）；补充第 8 缺陷（SUM/AVG 被 JOIN 重数膨胀）；替代方案 = 修复 4 个 Service 的 N+1 |

---

## 九、Oracle 评审结论（v0.2 落地）

### 9.1 裁决汇总

| # | 视图 | 裁决 | 核心理由 | 替代方案 |
|:-:|------|:---:|---------|---------|
| 5 | vw_pk_player_stats | ✅ **进入实现** | 多对一 JOIN 无 fan-out + 固定 grain + 无动态参数 + BR-26 一致 + Id 方案 A 稳定 | — |
| 2 | vw_ranking_board | ⏸ 延后 | 单表无 JOIN，VEntity 不减少查询次数；趋势仍需 Service 查两次 | 修复 LatestSnapshotDateAsync N+1 |
| 1 | vw_owner_dashboard | ❌ 否决 | **Cartesian product** 污染 AVG/SUM（5×10×20×50=10,000 行/群组） | 修 2 处 N+1（foreach→IN 查询） |
| 3 | vw_parent_dashboard | ❌ 否决 | 日期塌缩（GROUP BY UserId 后无法按周过滤）+ 订阅门控 + StreakDays 不可 SQL | 加 DailyStats 30 天窗口过滤 |
| 4 | vw_period_report | ❌ 否决 | 日期塌缩（架构性不可能）；现有查询已高效（7~30 行） | 无需改动 |

### 9.2 关键架构原则（Oracle 确立）

```
✅ 视图处理：静态过滤 + 固定 grain 聚合（WHERE Status='Finished' + GROUP BY UserId）
❌ 视图不能：影响聚合维度的动态参数（日期区间改变 SUM 参与行）
❌ 视图不能：跨行引用（趋势 = 今日 vs 昨日，需 Service 查两次）
❌ 视图不能：外部状态门控（订阅状态、RankEnabled 开关）
❌ 视图不能：混合不同粒度聚合维度（日级 × 知识点级 × 任务级 → Cartesian product）
```

### 9.3 第 8 个设计缺陷（评审新发现）

**SUM/AVG 被 JOIN 重数膨胀**（非仅 COUNT）：
- vw_period_report 示例：DailyStats（30 行/月）INNER JOIN KnowledgeMastery（50 知识点）ON UserId → 1,500 行/用户
- `SUM(LearnedCount)` 膨胀 50 倍；`AVG(Accuracy)` 被错误加权；仅 `COUNT(DISTINCT)` 正确
- 根因：一个 GROUP BY 混合不同粒度（日级/知识点级/任务级），对 UserId 均 one-to-many
- 正确做法：相关子查询（每维度独立 `SELECT SUM(...) WHERE UserId = g.UserId`），但 SQL 过度复杂，不如保留 Service 编排

### 9.4 谓词下推修正

设计 v0.1 判断"GROUP BY 无法谓词下推"**有误**——PostgreSQL 视图是宏展开，对 **GROUP BY 列**的 WHERE 可下推（如 GroupId）。真正无法下推的是对聚合结果的 HAVING，非本场景。

### 9.5 合规审查（无阻断项）

- C-03 正确率排名禁令：全部视图合规（#2 accuracy 是战绩组合指标非独立排名；#1 WeakPointCount 是群组级计数非学生排名；#3 SubjectAccuracy 是个人学科正确率）
- C-04 RLS：视图仅提供过滤列，实际 WHERE 注入在 Service 层 IGlobalQueryFilter

### 9.6 实施建议（评审确立）

1. **立即实现** #5 vw_pk_player_stats（tkwf-entity → tkwf-service）
2. **并行修复** 4 个 Service 的 N+1（每个 < 30 分钟）：
   - GetOwnerDashboardService：2 处 foreach → IN 查询
   - GetRankingsService：LatestSnapshotDateAsync → MAX 查询
   - GetDashboardReportService：DailyStats 加 30 天窗口
   - GetPeriodReportService：已高效，无需改动
3. **未来重评估触发**：PkPlayers/PkMatches > 10 万行且 PK 战绩成瓶颈 / 批量多用户 PK 战绩查询（搭子互看）
