# 学习Session域 · 切片实施总览

> **项目**：小书童（XiaoShuTong）
> **编制依据**：U01-学习Session 全部 UC
> **用途**：AC 阶段 Agent 的 Service 实现总纲（切片验证版）
> **范围**：学习 Session 域（模块 4：会话/作答/记忆状态/复习队列/错题本/每日统计）

> **框架自动生成原则**：只需用 tkwf-entity 编写 Entity、用 tkwf-service 编写 Service，其余全部自动生成——Entity → xCodeGen → DataService/DTO/Conditions，Service → `[GenerateController]` → SG → Controller/契约接口/Api/Wasm 客户端与 ts 客户端。

## Service 一览

| UC# | Service 类 | 子域 | 方法签名 | DataService 依赖(自动生成) | BR 范围 | 事务 | 模式 |
|:---:|-----------|:----:|---------|---------------------------|:-------:|:----:|:----:|
| 4.1 | `CreateStudySessionService` | Learning | `ExecuteAsync(CreateStudySessionReqDto) → CreateStudySessionResDto` | `IStudySessionsDataService`, `IMemoryStatesDataService`, `ITasksDataService`(跨), `ITaskAssignmentsDataService`(跨), `IBanksDataService`(跨) | BR-01~05 | CROSS | B |
| 4.2 | `SubmitAttemptService` | Learning | `ExecuteAsync(SubmitAttemptReqDto) → SubmitAttemptResDto` | `IStudySessionsDataService`, `IAttemptsDataService`, `IMemoryStatesDataService`, `IDailyStatsDataService`, `IWrongQuestionsDataService`, `IQuestionsDataService`(跨), `ITaskAssignmentsDataService`(跨) | BR-06~27 | CROSS | B |
| 4.3 | `GetHintService` | Learning | `ExecuteAsync(GetHintReqDto) → GetHintResDto` | `IQuestionsDataService`(跨), `IMemoryStatesDataService` | BR-28~30 | SINGLE | B |
| 4.4 | `GetReviewQueueService` | Learning | `ExecuteAsync(GetReviewQueueReqDto) → GetReviewQueueResDto` | `IMemoryStatesDataService` | BR-31~34 | SINGLE | A |
| 4.5 | `GetMemoryStatesService` | Learning | `ExecuteAsync(GetMemoryStatesReqDto) → GetMemoryStatesResDto` | `IMemoryStatesDataService` | BR-35/36 | SINGLE | A |
| 4.6 | `GetSessionResultService` | Learning | `ExecuteAsync(GetSessionResultReqDto) → GetSessionResultResDto` | `IStudySessionsDataService`, `IAttemptsDataService`, `IQuestionsDataService`(跨) | BR-37~40 | SINGLE | B |
| 4.7 | `GetWrongQuestionsService` | Learning | `ExecuteAsync(GetWrongQuestionsReqDto) → GetWrongQuestionsResDto` | `IWrongQuestionsDataService` | BR-41~43 | SINGLE | A |
| 4.8 | `KnowledgeMasteryAggregationJob` | Learning | `ExecuteAsync(CancellationToken)` | `IAttemptsDataService`, `IKnowledgeMasteryDataService` | BR-47~50 | CROSS | C |

> **模式说明**：A = 单 DataService(顺序写入)；B = 多 DataService 编排(含跨模块)；C = BackgroundJob(调度器触发,无 DTO,不进本表)

## 各 Service 测试要点

| Service 类 | 重点验证 | 边界条件 | 注意 |
|-----------|---------|---------|------|
| `CreateStudySessionService` | 会话创建 + 任务状态推进原子性、任务未截止校验 | 任务不存在/已截止、题库不存在、重复发起同任务 | 幂等复用进行中会话 |
| `SubmitAttemptService` | **四阶状态机全迁移矩阵**（BR-11~21）、间隔计算、判题降级、派生同步四表一致性 | 首次新题答错 30min、★ 答错降一级、连续 2 次升级、求助后答对 | 判题服务跨模块（D01 §9）；防刷 ≥2s；幂等防重复 |
| `GetHintService` | 提示 ≤20 字、不给答案、难度档按状态路由 | 提示服务不可用降级 | 从知识卡片提取，不从答案提取 |
| `GetReviewQueueService` | 到期筛选（≤Date 且非 ★）、逾期置顶、不含答案 | 空队列正常返回 | 视图 ReviewQueue 由 Service 层读取 |
| `GetMemoryStatesService` | 仅本人数据（RLS）、组合过滤 | 空条件全量分页 | 纯读 |
| `GetSessionResultService` | 聚合答对/总题数、新增★数、卡壳知识点 | 空会话不产生结果页 | 知识点映射跨模块取题 |
| `GetWrongQuestionsService` | 仅本人错题、Mastered 分组、学科过滤 | 空错题正常返回 | 数据由 UC-4.2 写入（重练复用会话流程） |
| `KnowledgeMasteryAggregationJob` | 增量扫描幂等、聚合口径（中位/最差/均值）、upsert | 无增量跳过、单用户失败不中断 | 每小时 + 家长报告按需触发 |

## 存量差异与迁移决策（实施前置确认）

| # | 项 | 状态 | 影响 |
|:-:|---|:----:|------|
| ① | 主键 uuid → long Id | **已决策**：内部 long + 外部 Uid(uuid) | 实体表补 Uid 列（唯一索引）；API/DTO 暴露 Uid |
| ② | CreatedAt/UpdatedAt → CreateTime/UpdateTime | 本切片已转化（部分表补齐审计列） | 迁移脚本改名/补列 |
| ③ | 枚举 smallint/小写/中文 → PascalCase 字符串 | 本切片已转化（0-3→MemoryState；memorize→Memorize；分阶→Progressive） | 存量数据映射 |
| ④ | 错误码 30xx 数字码 + SNAKE_CASE 双列 | 本切片已采用（仅 3001/3002 存量，未新增码值） | DG-06 附录迁移规则 |
| ⑤ | 状态符号 ✕△○★ | 文档/UI 展示符号，存储用枚举名 | JsonConverter 映射 |

## 跨模块依赖清单

| 依赖模块 | 引用内容 | 用途 | UC |
|---------|---------|------|:---:|
| 任务域 | Tasks / TaskAssignments | 任务校验 + 进度回传 | 4.1 / 4.2 |
| 题库域（D01 §7.6） | Banks / Questions | 题库校验 + 取题 + 知识点映射 | 4.1 / 4.2 / 4.3 / 4.6 |
| 判题服务（D01 §9） | 判题引擎（五键契约） | 本地关键词 + LLM 语义判题 + 提示生成 | 4.2 / 4.3 |

> **判题服务跨模块标注（验证点 #2）**：判题依赖在 U01 UC-4.2/4.3 的 UC 头 + 编排逻辑 + 本表三处一致标注。判题逻辑由判题服务模块（D07）实现，本域仅消费。

> *文档版本：第一阶段 v1.1*
> *编制日期：2026-09-06*
> *同步版本：U01 v1.1*