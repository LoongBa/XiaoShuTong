# DS01-搭子PK竞技 · 数据结构设计

> **所属阶段**：第一阶段（设计层），切片验证（路径 B 转化）
> **前置条件**：R01 + S01 已产出
> **产出物**：搭子 PK 竞技域 3 实体定义（PkMatches/PkPlayers/PkAttempts）→ 输入给 xCodeGen
>
> **转化说明**：本 DS 由 D02 V1.4（§5.1 PkMatches / §5.2 PkPlayers / §5.3 PkAttempts）权威 DDL 转化。存量 DDL 与 TKWF 框架约定的差异在「存量差异标注」节集中说明。
> **子域决策**：本切片使用子域 `Pk`（PK 竞技域内聚）；PkPlayerStats 为视图（不建实体）；跨模块实体（Users/StudyBuddies/Questions）引用不在此定义。
> **版本标注修正**：InviteCode/FinishReason 为 **D02 V1.3** 扩展（非 V3.1.3），DS 存量差异按 V1.3 记。

---

## 实体清单

### PkMatches ── 比赛主表

**子域**：`Pk`
**文件路径**：`Entities/Pk/PkMatches.cs`
**命名空间**：`{App}.Entities.Pk`

**表名**：`PkMatches`

| # | 属性名 | 类型 | 主键 | 外键 | 必填 | 默认值 | 索引 | 排序 | 业务说明 |
|:-:|-------|------|:---:|:---:|:---:|:-----:|:----:|:---:|---------|
| 1 | Id | long | ✅ | — | ✅ | — | PK | — | 自增主键（D02 原为 uuid，见存量差异①） |
| 2 | Uid | Guid | — | — | ✅ | — | UNQ_PkMatches_Uid | — | 外部业务键（uuid，API/DTO 暴露） |
| 3 | Subject | string | — | — | ✅ | — | — | — | 学科（见 Subject 枚举，跨题库域） |
| 4 | BankId | string | — | — | ✅ | — | — | — | 题库业务键 |
| 5 | Topic | string? | — | — | — | — | — | — | 知识点/主题（按知识点自动出题） |
| 6 | QuestionCount | int | — | — | ✅ | 10 | — | — | 题量（5/10/20） |
| 7 | PerQuestionTimeS | int | — | — | ✅ | 30 | — | — | 每题限时（秒） |
| 8 | TotalTimeLimitS | int | — | — | ✅ | 300 | — | — | 总时长上限（10×30s+缓冲） |
| 9 | Mode | string | — | — | ✅ | "Sync" | — | — | 模式（见 PkMode 枚举） |
| 10 | Status | string | — | — | ✅ | "Pending" | IX_PkMatches_Status_CreatedAt | DESC | 状态（见 PkMatchStatus 枚举） |
| 11 | WinnerId | long? | — | →Users | — | — | IX_PkMatches_WinnerId | — | 胜方（NULL=平局） |
| 12 | InviteCode | string | — | — | — | — | IX_PkMatches_InviteCode | — | 4 位对战码（方式 B 加入） |
| 13 | FinishReason | string | — | — | — | — | — | — | 结束原因（见 PkFinishReason 枚举） |
| 14 | CreateTime | DateTime | — | — | ✅ | — | — | — | 创建时间（D02 原 CreatedAt，见存量差异②） |
| 15 | FinishedAt | DateTime? | — | — | — | — | — | — | 结束时间（D02 原 FinishedAt，对齐审计名） |
| 16 | UpdateTime | DateTime | — | — | ✅ | — | — | — | 更新时间（框架审计字段） |

> **软删除**：否（历史对局保留）
> **缺口决策**：finishReason=forfeit/timeout 时 Status **置 Finished**（对齐 PkPlayerStats 视图 `WHERE Status='finished'` 过滤口径，见存量差异④）

### PkPlayers ── 参赛者成绩

**子域**：`Pk`
**文件路径**：`Entities/Pk/PkPlayers.cs`
**命名空间**：`{App}.Entities.Pk`

**表名**：`PkPlayers`

| # | 属性名 | 类型 | 主键 | 外键 | 必填 | 默认值 | 索引 | 排序 | 业务说明 |
|:-:|-------|------|:---:|:---:|:---:|:-----:|:----:|:---:|---------|
| 1 | Id | long | ✅ | — | ✅ | — | PK | — | 自增主键 |
| 2 | Uid | Guid | — | — | ✅ | — | UNQ_PkPlayers_Uid | — | 外部业务键 |
| 3 | MatchId | long | — | →PkMatches | ✅ | — | — | — | 对局归属（含在 UNIQUE） |
| 4 | UserId | long | — | →Users | ✅ | — | IX_PkPlayers_UserId_MatchId | DESC | 参赛者（个人战绩查询） |
| 5 | Score | int | — | — | ✅ | 0 | — | — | 总分（每题 +10） |
| 6 | CorrectCount | int | — | — | ✅ | 0 | — | — | 答对数 |
| 7 | TotalTimeMs | int | — | — | ✅ | 0 | — | — | 总用时（同分比用时） |
| 8 | AiComment | string? | — | — | — | — | — | — | AI 趣味点评（D01 8.4.2） |
| 9 | CreateTime | DateTime | — | — | ✅ | — | — | — | 创建时间（框架审计字段，D02 原无） |
| 10 | UpdateTime | DateTime | — | — | ✅ | — | — | — | 更新时间（框架审计字段） |

> **唯一约束**：UNQ_PkPlayers_MatchId_UserId（每对局每参赛者唯一）
> **软删除**：否

### PkAttempts ── PK 答题明细

**子域**：`Pk`
**文件路径**：`Entities/Pk/PkAttempts.cs`
**命名空间**：`{App}.Entities.Pk`

**表名**：`PkAttempts`

| # | 属性名 | 类型 | 主键 | 外键 | 必填 | 默认值 | 索引 | 排序 | 业务说明 |
|:-:|-------|------|:---:|:---:|:---:|:-----:|:----:|:---:|---------|
| 1 | Id | long | ✅ | — | ✅ | — | PK | — | 自增主键 |
| 2 | Uid | Guid | — | — | ✅ | — | UNQ_PkAttempts_Uid | — | 外部业务键 |
| 3 | MatchId | long | — | →PkMatches | ✅ | — | IX_PkAttempts_MatchId | — | 对局归属 |
| 4 | PlayerId | long | — | →PkPlayers | ✅ | — | — | — | 参赛者成绩记录 |
| 5 | UserId | long | — | →Users | ✅ | — | — | — | 答题用户 |
| 6 | QuestionId | string | — | — | ✅ | — | — | — | 题目业务键 |
| 7 | Answer | string? | — | — | — | — | — | — | 用户答案 |
| 8 | Result | string | — | — | — | — | — | — | 判题结果（见 PkAttemptResult 枚举） |
| 9 | IsCorrect | bool | — | — | — | — | — | — | 答对标记 |
| 10 | TimeCostMs | int | — | — | — | — | — | — | 答题用时（客户端上送） |
| 11 | CreateTime | DateTime | — | — | ✅ | — | — | — | 创建时间（框架审计字段，D02 原无） |
| 12 | UpdateTime | DateTime | — | — | ✅ | — | — | — | 更新时间（框架审计字段） |

> **软删除**：否
> **缺口决策**：补 UNQ_PkAttempts_MatchId_PlayerId_QuestionId（防重复提交，见存量差异③）

## 枚举定义

#### Subject ── 学科

存储格式：`string`（PascalCase；存量小写 subject）
使用场景：PkMatches.Subject

| 枚举值 | 业务含义 |
|:------:|---------|
| Chinese | 语文 |
| Math | 数学 |
| English | 英语 |
| Physics | 物理 |
| Chemistry | 化学 |
| Biology | 生物 |
| History | 历史 |
| Geography | 地理 |
| Politics | 政治 |
| Fun | 趣味 |

#### PkMode ── PK 模式

存储格式：`string`（PascalCase；存量小写 sync/async）
使用场景：PkMatches.Mode

| 枚举值 | 业务含义 | 说明 |
|:------:|---------|------|
| Sync | 同步实时 | WebSocket/SignalR 实时同步（WS 可用时） |
| Async | 异步回合制 | 微信 H5 无 WS 降级（各自计时、合并计分） |

#### PkMatchStatus ── 对局状态

存储格式：`string`（PascalCase；存量小写 pending/ongoing/finished/cancelled）
使用场景：PkMatches.Status

| 枚举值 | 业务含义 |
|:------:|---------|
| Pending | 待加入（等待对手确认） |
| Ongoing | 进行中 |
| Finished | 已结束（含 forfeit/timeout，**本切片决策**） |
| Cancelled | 已取消 |

#### PkFinishReason ── 结束原因

存储格式：`string`（PascalCase；存量小写 score/forfeit/timeout）
使用场景：PkMatches.FinishReason

| 枚举值 | 业务含义 |
|:------:|---------|
| Score | 正常比分 |
| Forfeit | 对手离线 30s 判弃权 |
| Timeout | 超时结束 |

#### PkAttemptResult ── PK 判题结果

存储格式：`string`（PascalCase；D01 判题契约 correct/partial/wrong）
使用场景：PkAttempts.Result

| 枚举值 | 业务含义 | PK 计分 |
|:------:|---------|:------:|
| Correct | 正确 | +10 |
| Partial | 部分正确 | **0 分（本切片决策，D02/D01 未定义 partial 计分）** |
| Wrong | 错误 | 0 分 |

## 实体关系

```
PkMatches (1) ──→ (N) PkPlayers                对局包含参赛者（每对局每参赛者唯一）
PkMatches (1) ──→ (N) PkAttempts               对局包含答题明细
PkPlayers (1) ──→ (N) PkAttempts               参赛者答题记录引用
Users (1) ──→ (N) PkMatches（WinnerId）         胜方
Users (1) ──→ (N) PkPlayers / PkAttempts       参赛者/答题者
```

> 跨模块实体引用（不在本 DS 定义）：`Users`（账户域）、`StudyBuddies`（搭子域，切片 06，仅 accepted 可 PK 的应用层校验）、`Banks`/`Questions`（题库域，切片 03，出题）——FK/业务键引用，编译依赖通过框架 DI 注入。
> **视图**：`PkPlayerStats`（D02 §5.4，聚合视图，不建实体）——胜场/战绩由视图实时计算，胜率在 API 层派生。

## 存量差异标注（D02 DDL → TKWF 框架）

| # | 差异项 | D02 存量 | TKWF 要求（DG-05） | 处理建议 |
|:-:|-------|---------|-------------------|---------|
| ① | **主键类型** | `uuid` | `long Id`（硬性） | **沿用前序切片决策**：内部 `long Id` + 外部 `Uid`（uuid 业务键）——实体表补 Uid 列（唯一索引），API/DTO 暴露 Uid |
| ② | **审计字段** | PkPlayers/PkAttempts 原无审计列；PkMatches 为 CreatedAt/FinishedAt | `CreateTime/UpdateTime` | 本 DS 已补齐/转化；FinishedAt 保留业务语义（对齐审计命名） |
| ③ | **防重复提交约束** | PkAttempts 无 UNIQUE(MatchId,PlayerId,QuestionId) | 框架无强制 | **本切片补充**（防同题重复提交）；存量无此约束 |
| ④ | **forfeit/timeout 的 Status** | D02 未定义（有枚举值无流转） | 框架无强制 | **决策：置 Finished**——对齐 PkPlayerStats 视图 `WHERE Status='finished'` 过滤，保证胜场统计口径（最高风险缺口） |
| ⑤ | **partial 判题计分** | D02/D01 未定义 PK 场景 partial 是否计分 | 框架无强制 | **决策：Partial 计 0 分**（IsCorrect=false）——保证"答对 +10"规则语义清晰（用户诉求确认：partial 不鼓励） |
| ⑥ | **枚举存储** | 小写（sync/pending/score） | PascalCase `"Sync"/"Pending"/"Score"` | 本 DS 已转化；存量数据迁移映射 |
| ⑦ | **InviteCode 版本** | D02 V1.3 新增（非 V3.1.3） | 无强制 | **版本标注**：存量差异按 V1.3 记 |
| ⑧ | **错误码** | 数字域码 20xx | DG-06 默认 SNAKE_CASE | **沿用决策**：保留数字码 + 语义名双列（见 U01 错误码表） |

> **字段覆盖校对（S/UI → DS）**：S01 场景与 UI 展示字段（发起挑战按钮/搭子列表/学科/题量/对战码/对局配置、题目/30s倒计时红条/答对+10即时反馈/自动下一题、胜负/双方得分/AI点评/知识彩蛋/庆祝动画、战绩/待加入列表）——① 对局/对战码 = PkMatches ✅；② 得分/用时/点评 = PkPlayers ✅；③ 题目/判分 = PkAttempts + 判题引擎 ✅；④ 战绩 = PkPlayerStats 视图 ✅；⑤ 知识彩蛋 = 响应态不落库（标注"前端展示"）✅

> *文档版本：第一阶段 v1.0*
> *编制日期：2026-09-06*
> *同步版本：R01 v1.0 / S01 v1.0*
---

## VEntity 设计规格（tkwf-ventity-design 产出，2026-09-07）

> 输入：S01 定稿场景 + UI PK/战绩页字段来源 + R/S 查询需求。结论：**PkPlayerStats（战绩聚合）为 VEntity 适用场景**（跨表聚合报表），但切片验证期以 Service 层聚合实现（内存 DAC 不支持 SQL 视图）；生产建议落正式视图。

### 查询场景全局清单

| 页面 | 查询字段 | 来源表 | 聚合口径 | 跨表 | 判定 |
|------|---------|--------|---------|:----:|------|
| PK 战绩（9.6） | 场次/胜场/胜率 | PkMatches + PkPlayers | 按 UserId 聚合（Status=Finished） | 是（JOIN） | **VEntity 候选 → 生产 vw_pk_player_stats** |
| PK 结果（9.4） | 双方得分/用时/AI点评/胜负 | PkMatches + PkPlayers（单局） | 无聚合（单行） | 是（按局关联） | Service |
| PK 大厅/待加入（9.1/9.2） | 待加入对局列表（Subject/题量/对战码） | PkMatches | 无聚合（列表） | 否 | Service 单表 |
| 对战答题（9.3） | 题目/判分/得分 | PkMatches + PkPlayers + PkAttempts（单局写） | 写入侧 | — | Service（判题引擎跨模块） |

### PkPlayerStatsView ── PK 战绩聚合视图（VEntity 设计规格）

**表名**：`vw_pk_player_stats`（DisableSyncStructure=true）
**子域**：`Pk`（生产域内聚）
**Id 构造方案**：A 业务唯一键透传（UserId）
**视图类型**：普通视图（物化经 DBA 评估）

**引用基表**：Pk.PkMatches（Status='Finished'）+ Pk.PkPlayers
**依赖追踪**：PkMatches.Status/WinnerId/FinishedAt、PkPlayers.MatchId/UserId DDL 变更需核对本视图

**Join 结构**：INNER JOIN（PkPlayers ← PkMatches ON MatchId，WHERE PkMatches.Status='Finished'）
**优化栅栏评估**：含 GROUP BY（聚合）+ WHERE 谓词——无窗口函数/集合操作

**聚合粒度**：COUNT（场次）/ 派生胜场（PkMatches.WinnerId = 玩家）按 UserId 分组

**字段表**（= UI 展示字段超集）：
| 字段 | 类型 | 来源列 | 四层一致性核对 | 可更新列 |
|------|------|--------|--------------|:-------:|
| UserId | long | PkPlayers.UserId | UI 战绩页按人 | 否 |
| TotalMatches | int | COUNT(PkPlayers.MatchId) | 场次 | 否 |
| Wins | int | COUNT(CASE WHEN PkMatches.WinnerId = PkPlayers.UserId THEN 1 END) | 胜场 | 否 |

**数据权限**：行级 WHERE（仅本人/搭子可见）；无列级隐藏
**性能评估**：预估行数 = 用户数 / 索引建议（PkMatches.Status+FinishedAt、PkPlayers.UserId+MatchId）/ EXPLAIN 验证（谓词下推?）
**可维护性**：命名 `vw_` 前缀 / COMMENT ON 文档（战绩口径：Status=Finished 才计入）/ 纳入 git
**查询通道暴露决策**：EQR `User.Query<PkPlayerStatsView>()` / ExposeRestQuery 关（经 Service 包装 REST）/ GraphQL 开

### 切片验证期实现（不建视图）

- 内存 DAC（TestingEntityDAC）不支持 SQL 视图 → `GetPkStatsService` 以 Service 层聚合实现（读 PkMatches+PkPlayers 计算胜场/场次/胜率，口径与视图一致：仅 Status=Finished 计入）
- 生产切换：建 `vw_pk_player_stats` 后 GetPkStatsService 改为 EQR 读取，DTO 不变

### 跨切片消费声明

- 本切片消费：**Judging**（判题引擎五键契约，切片 03）、**Buddy**（StudyBuddies 搭子资格，切片 06）、**Bank**（Questions 出题/知识点，切片 03）
- 本切片生产：PkMatches/PkPlayers/PkAttempts（PK 战绩数据源，供切片 06 排行榜 PkWins 指标消费）
