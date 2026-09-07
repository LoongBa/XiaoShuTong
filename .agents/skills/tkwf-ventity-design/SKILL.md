---
name: tkwf-ventity-design
description: 设计阶段查询/统计场景梳理与 VEntity（只读视图实体）设计。输入 UI 定稿场景（S-v2）+ UI 图字段来源 + R/S 查询需求；从全局梳理查询/统计场景，判定 VEntity 适用性，设计完整 View 设计规格（字段/聚合口径/Id 构造/引用基表/Join/性能/权限/查询通道），产出查询契约要素供 DG-06 引用，供前端对接真实 WebApi。核心纪律：设计阶段纯文档产出，不写实现代码、不写 ViewSql 具体 SQL。
version: 4.9.106
---

# tkwf-ventity-design — 查询统计 VEntity 设计

> 定位：tkwf-design 设计阶段子环节（UI 定稿后、DS 场景层前）。**核心 = 完整的 View 设计**——发挥查询、统计优势，省 Service 编排代码、快速适配前端查询页。
> 执行：主 Agent 在进入 DS 场景层设计前委托本 skill（满足触发门槛时）。
> 分工：本 skill 产出 View 设计规格 + 查询契约要素；**执行（编码）靠 tkwf-entity（ViewSql 落地）/ tkwf-service（查询契约消费）**。

## 1. TASK

以 S-v2 定稿场景 + UI 图字段来源 + R/S 查询需求为输入，从全局（跨模块）梳理查询/统计场景，判定 VEntity 适用性，设计**完整的 View 设计规格**（发挥查询/统计优势），产出查询契约要素。

## 2. EXPECTED OUTCOME

- **查询场景全局清单**（页面→字段→来源表→聚合口径→跨表→敏感列→判定）
- **VEntity 设计规格清单**（完整 View 设计：语义/基表/Join/聚合/字段/权限/性能/可维护性/查询通道——见 §4.2 模板）
- **查询契约要素**（每个 VEntity 的 EQR/ExposeRestQuery/GraphQL Connection 暴露决策）
- **跨切片消费声明**（本切片消费他域哪些实体）
- **查询场景覆盖自检报告**

## 3. REQUIRED TOOLS

Read / Write / Edit（纯文档，无工具依赖）

## 4. MUST DO

### 4.1 执行流程

1. 读本 skill + DG-05（场景层/字段覆盖校对）+ 本 skill 自带精简设计速查（不读编码侧 419 行 VEntity 速查）
2. 读输入：S-v2 定稿场景 + UI 图（字段来源）+ 相关 R/S
3. **查询场景全局收集**：遍历所有 UI 查询/列表/统计/报表页 → 查询字段清单（页面→字段→来源表→聚合口径→跨表→敏感列）
4. **场景分类判定**：按决策表（§5）逐场景判定 → VEntity / 普通 Entity+裁剪 / Service
5. **VEntity 设计规格**：按 §4.2 模板设计完整 View 规格（ViewSql 具体 SQL 留编码阶段）
6. **产出查询契约要素**：每个 VEntity 的 EQR/ExposeRestQuery/GraphQL Connection 暴露决策 + 跨切片消费声明
7. **自检**：§4.3 七项

### 4.2 VEntity 设计规格模板（完整 View 设计）

```markdown
### {名称}View ── {只读视图说明}

**表名**：`vw_{名称}`（DisableSyncStructure=true）
**子域**：{消费域子域}（VEntity 归消费域，非生产域）
**Id 构造方案**：A 业务唯一键透传 / B ROW_NUMBER（仅纯报表）
**视图类型**：普通视图（TKWF VEntity 默认；物化视图经 DBA 评估）

**引用基表**：{域}.{表}（+ {域}.{表}…）
**依赖追踪**：{变更影响的基表清单（基表 DDL 变更需核对本视图）}

**Join 结构**：{类型 INNER/LEFT/… + 数量}
**优化栅栏评估**：{是否含 GROUP BY/DISTINCT/窗口函数/集合操作——影响谓词下推与查询性能}

**聚合粒度**：{聚合函数 SUM/COUNT/AVG + 分组键 + 粒度级别（日/月/自定义）}

**字段表**（= UI 展示字段超集，显式列名，禁 SELECT *）：
| 字段 | 类型 | 聚合列类型 | 来源列 | 四层一致性核对(UI/属性/?fields) | 可更新列标记 |
|------|------|-----------|--------|------------------------------|-------------|

**数据权限**：{列级隐藏 / 行级 WHERE 条件 / security_barrier 评估 / security_invoker 评估}

**性能评估**：预估行数 {N} / 索引建议 {列} / EXPLAIN 验证要点 {谓词下推? 索引扫描?}

**可维护性**：命名 {后缀 View} / COMMENT ON 文档 {要点} / 版本控制 {纳入 git}

**查询通道暴露决策**：EQR `User.Query<{名称}View>()` / ExposeRestQuery {开/关} / GraphQL Connection {开/关}
```

### 4.3 框架约束核对（六维度，设计阶段必须覆盖）

> ⚠️ 以下约束为框架实际执行行为（含文档偏差纠偏，v4.9.106 核实）：

| 维度 | 约束 | 设计阶段要求 |
|------|------|-------------|
| 声明 | `IsView=true` 或 `InlineSelectSql` 非空即视为 View；两者同设时 InlineSelectSql 覆盖 ViewSql | 明确视图形态（正式 View / 轻量 Inline） |
| 声明 | 方言变体键名精确匹配 `DatabaseProvider.ToString()`（SqlServer/MySQL/SQLite/Oracle），否则静默 fallback PG 语法 | 非 PG 环境标注目标方言 + 变体需求 |
| 编译期校验 | VIEW001 **单向**（仅"多余列"：SQL 别名不在 C# 属性），Warning；VIEW002 视图链（vw_ 引用 vw_），Warning；**VIEW003 编译期不触发**（仅运行时 LogWarning，速查 §1.1 表述有误） | 字段表列名与 ViewSql 列别名一致（大小写不敏感） |
| 运行时校验 | `ViewSqlColumnValidator` **双向**（多余+缺失列）→ 抛 DomainException 阻断启动；视图链同样抛错 | 字段表 = ViewSql 列超集（双向完备），不引 vw_ 视图 |
| 运行时 | 视图同步门控 `IsDevelopment` + `EnableAutoViewSync`；生产不自动建视图（DBA 管理） | 标注生产建视图责任方 |
| 生成 | VEntity **会**生成 `{ClassName}Dto.g.cs`（速查 §7.5"不生成 Dto"有误，§1 正确）；跳过 DataService/Conditions | U 契约依赖 IEntityReadOnlyDAC（非 IEntityDAC） |
| 生成 | **StatsDto 门控仅 SUM(/COUNT(/AVG(（Engine.cs:66-69）——仅含 MIN/MAX 不生成 StatsDto**（速查 §8 声称含 MIN/MAX 有误） | 含 MIN/MAX 聚合的视图，标注 StatsDto 不生成（U 契约自行聚合或等框架修复） |
| 生成 | 聚合列**必须写 AS 别名**（无别名 → 属性名退化为大写函数名如 `COUNT`）；函数名与 `(` 之间无空格（`SUM (x)` 不触发门控） | 字段表聚合列标注别名，格式 `SUM(x) AS y` |
| 查询通道 | EQR 默认控制器不依赖 `[GenerateController]`；GraphQL 默认开（`ExposeGraphqlQuery=true`）；VEntity 不支持 REST 端点，REST 查询需通过 Service 方法包装；禁 `IEntityDAC<ViewEntity>`（静态构造器守卫抛错） | 明确每 VEntity 查询通道暴露决策 |
| 性能 | `DefaultLimit=100` / `MaxSearchLimit=100`；轻量 Inline 每次查询重执行（仅开发/测试）；1:1 全属性 Dto 无 SQL 级裁剪（SELECT *） | 预估行数 + 索引建议 + 轻量/正式视图决策 |
| 增量 | `SourceHash` 不含属性定义——改字段不触发重新生成 | 设计定稿字段后标注"改字段需手动重跑 xCodeGen" |

### 4.4 自检（查询场景覆盖）

| # | 检查项 | 标准 |
|:-:|--------|------|
| 1 | UI 查询页全覆盖 | 每个查询/列表/统计页有数据源映射 |
| 2 | 无重复 VEntity | 同一聚合场景不重复声明（跨切片唯一） |
| 3 | 字段一致性 | VEntity 字段 = UI 展示字段超集（显式列名） |
| 4 | 视图链 | 无 vw_ 引用 vw_ |
| 5 | 聚合口径跨切片一致性 | 跨切片复用的口径标注唯一来源（如"执行率"全局唯一） |
| 6 | 查询通道暴露决策 | 每个 VEntity 显式决策 EQR/ExposeRestQuery/GraphQL 开关 |
| 7 | 与字段覆盖校对衔接 | 产出并入 DS 场景层后，字段覆盖校对一次通过 |

> 本自检作为 DG-01 §2.4 交接条件第 3 项"所有 UC 引用的 DataService 已在 DS 中定义"的补充输入（VEntity/EQR 引用需有本清单支撑）。

### 4.5 全局统一约束

- 同一聚合口径跨切片复用（不各域各算各的）
- VEntity 命名：类名后缀 `View`（如 `PaymentLogView`）、表名 `vw_` 前缀
- VEntity 子域归属：归**消费域** `Entities/{消费域SubDomain}/`
- 跨模块视图引用标注（被谁消费）

### 4.6 双触发机制

| 触发点 | 时机 | 职责 |
|--------|------|------|
| 切片内触发（主） | 每切片 S-v2 定稿后、DS 场景层前 | 梳理本切片查询场景 + 标注跨切片消费声明 |
| 切片收官触发（辅） | 全量切片交付前、聚合终检时（DG-01 §三·补） | 全局对齐聚合口径 + 补跨切片 VEntity |

## 5. MUST NOT DO

- ❌ 不写 VEntity 实现代码（Entity.cs/ViewSql 落地 → tkwf-entity）
- ❌ 不写 ViewSql 具体 SQL（PG 引号/方言变体 → tkwf-entity 编码阶段按 VEntity 速查）
- ❌ 不写查询 Service 编排（→ tkwf-service）
- ❌ 不直接产 U 文档（→ DG-06；本 skill 只产查询契约要素）
- ❌ 不修改已定稿的 UI/S-v2（只读输入）
- ❌ 不为单表只读查询滥用 VEntity（应 Entity+[DtoFieldIgnore]+?fields 裁剪）
- ❌ 不为单实体聚合统计用 VEntity（Service+IQueryable 聚合）
- ❌ 不为实时查询/推送数据源用 VEntity（Service+实时通道）
- ❌ 不臆造聚合口径；缺失口径标注 [待确认] 报主 Agent
- ❌ 视图链（ViewSql 引用其他 vw_ 视图）
- ❌ 跳过 UI 字段来源核对（必须基于定稿字段设计）

## 6. CONTEXT

- 规范基础：DG-05（VEntity 规则节，v4.9.106 修订）+ 本 skill 精简设计速查
- 实现衔接：tkwf-entity 规则 15-18（编码侧 VEntity 速查 419 行）/ tkwf-service 规则 20-21
- 产出位置：DS 场景层（并入 DS01）+ 查询契约要素（供 DG-06 引用）
- 触发门槛：含跨表聚合/报表/跨切片消费声明的切片触发；纯 CRUD 切片走原 DG-05 流程
