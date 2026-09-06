# 搭子PK竞技域 · 切片实施总览

> **项目**：小书童（XiaoShuTong）
> **编制依据**：U01-搭子PK竞技 全部 UC
> **用途**：AC 阶段 Agent 的 Service 实现总纲（切片验证版）
> **范围**：搭子 PK 竞技域（模块 9）
> **子域**：`Pk`

> **框架自动生成原则**：只需用 tkwf-entity 编写 Entity、用 tkwf-service 编写 Service，其余全部自动生成——Entity → xCodeGen → DataService/DTO/Conditions，Service → `[GenerateController]` → SG → Controller/契约接口/Api/Wasm 客户端与 ts 客户端。

## Service 一览

| UC# | Service 类 | 子域 | 方法签名 | DataService 依赖(自动生成) | BR 范围 | 事务 | 模式 |
|:---:|-----------|:----:|---------|---------------------------|:-------:|:----:|:----:|
| 9.1 | `CreatePkMatchService` | Pk | `ExecuteAsync(CreatePkMatchReqDto) → CreatePkMatchResDto` | `IPkMatchesDataService`, `IPkPlayersDataService`, `IStudyBuddiesDataService`(跨), `IBanksDataService`(跨) | BR-01~05 | CROSS | B |
| 9.2 | `JoinPkMatchService` | Pk | `ExecuteAsync(JoinPkMatchReqDto) → JoinPkMatchResDto` | `IPkMatchesDataService`, `IPkPlayersDataService`, `IStudyBuddiesDataService`(跨) | BR-06~10 | SINGLE | B |
| 9.3 | `SubmitPkAnswerService` | Pk | `ExecuteAsync(SubmitPkAnswerReqDto) → SubmitPkAnswerResDto` | `IPkMatchesDataService`, `IPkPlayersDataService`, `IPkAttemptsDataService`, 判题引擎(跨) | BR-11~16 | SINGLE | B |
| 9.4 | `GetPkResultService` | Pk | `ExecuteAsync(GetPkResultReqDto) → GetPkResultResDto` | `IPkMatchesDataService`, `IPkPlayersDataService`, AI点评(跨) | BR-17~20 | SINGLE | B |
| 9.5 | `PkForfeitDetectionJob` | Pk | `ExecuteAsync(CancellationToken)` | `IPkMatchesDataService`, `IPkPlayersDataService` | BR-21~23 | CROSS | C |
| 9.6 | `GetPkStatsService` | Pk | `ExecuteAsync(GetPkStatsReqDto) → GetPkStatsResDto` | `IPkPlayerStatsViewService` | BR-24~27 | SINGLE | A |

> **模式说明**：A = 单 DataService；B = 多 DataService 编排(含跨模块)；C = BackgroundJob(调度器触发,无 DTO,不进本表)

## 各 Service 测试要点

| Service 类 | 重点验证 | 边界条件 | 注意 |
|-----------|---------|---------|------|
| `CreatePkMatchService` | 搭子资格校验、对局+参赛者原子、InviteCode 生成 | 非搭子/非法题库/题量越界 | CROSS 回滚 |
| `JoinPkMatchService` | 对局状态/人数/对战码匹配、搭子资格 | 已结束/满员/非搭子 | 方式 B |
| `SubmitPkAnswerService` | 判题引擎五键契约、计分规则（Correct+10/Partial 0）、防重复、timeCostMs 客户端上送 | 判题降级/超时 | 场景隔离（不入 Attempts） |
| `GetPkResultService` | 胜负判定、合规（无对比榜）、AI 点评兜底 | 对局不存在/AI 失败 | knowledgeEggs 响应态 |
| `PkForfeitDetectionJob` | 30s 弃权、30s 内重连取消、幂等 | 断线重连/单对局失败 | 事件+定时双驱动 |
| `GetPkStatsService` | 视图聚合、胜率 API 层计算、RLS | 无记录零值 | 视图不建实体 |

## 存量差异与迁移决策（实施前置确认）

| # | 项 | 状态 | 影响 |
|:-:|---|:----:|------|
| ① | 主键 uuid → long Id | **沿用决策**：内部 long + 外部 Uid(uuid) | 实体表补 Uid 列（唯一索引）；API/DTO 暴露 Uid |
| ② | 审计字段 | PkPlayers/PkAttempts 原无 → 补齐；PkMatches CreatedAt/FinishedAt 转化 | 迁移脚本补列/改名 |
| ③ | **PkAttempts 防重复 UNIQUE** | 本切片补充 UNQ(MatchId,PlayerId,QuestionId) | 存量无此约束 |
| ④ | **forfeit/timeout Status=Finished** | 本切片决策（对齐视图过滤口径） | 最高风险口径 |
| ⑤ | **partial 判题计 0 分** | 本切片决策（D02/D01 未定义） | 计分语义 |
| ⑥ | 枚举小写 → PascalCase | 本切片已转化（sync→Sync、pending→Pending） | 存量数据映射 |
| ⑦ | 错误码 20xx + SNAKE_CASE 双列 | 本切片已采用 | DG-06 附录迁移规则 |

## 跨模块依赖清单

| 依赖模块 | 引用内容 | 用途 | UC |
|---------|---------|------|:---:|
| 搭子域（切片 06） | StudyBuddies | accepted 资格校验（PK 前置） | 9.1/9.2 |
| 题库域（切片 03） | Banks/Questions | 出题范围/题目标识 | 9.1/9.3 |
| 判题服务（切片 03） | JudgingEngineService | 五键契约即时判分 | 9.3 |
| AI 点评（判题服务） | LLM 趣味点评 | 结果页点评 | 9.4 |
| 排行榜域（切片 06） | RankSnapshots | PK 胜场计入战力榜（反向消费） | 9.4 跨 |

> **跨模块依赖标注**：本切片（PK 域）消费搭子资格（切片 06 StudyBuddies）+ 判题引擎（切片 03）；本切片产出 PK 胜场数据（PkPlayerStats 视图）被排行榜域（切片 06）消费——双向闭环。U01 UC 头 + 编排逻辑 + 本表三处一致标注。

---

> *文档版本：第一阶段 v1.0*
> *编制日期：2026-09-06*
> *同步版本：U01 v1.0*