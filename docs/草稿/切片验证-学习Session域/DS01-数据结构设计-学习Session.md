# DS01-学习Session · 数据结构设计

> **所属阶段**：第一阶段（设计层），切片验证（路径 B 转化）
> **前置条件**：R01 + S01 已产出
> **产出物**：学习 Session 域 6 实体定义 → 输入给 xCodeGen
>
> **转化说明**：本 DS 由 D02 V1.4（§4.1~4.6）权威 DDL 转化。存量 DDL 与 TKWF 框架约定的差异在「存量差异标注」节集中说明。
> **子域决策**：本域使用自定义子域 `Learning`（对齐 D03"学习域"命名）——6 实体均属学习数据核心域，聚类为 `Entities/Learning/`；UC 头子域保持一致。

---

## 实体清单

### StudySessions ── 学习会话（单次学习批次）

**子域**：`Learning`
**文件路径**：`Entities/Learning/StudySessions.cs`
**命名空间**：`{App}.Entities.Learning`

**表名**：`StudySessions`

| # | 属性名 | 类型 | 主键 | 外键 | 必填 | 默认值 | 索引 | 排序 | 业务说明 |
|:-:|-------|------|:---:|:---:|:---:|:-----:|:----:|:---:|---------|
| 1 | Id | long | ✅ | — | ✅ | — | PK | — | 自增主键（D02 原为 uuid，见存量差异①） |
| 2 | Uid | Guid | — | — | ✅ | — | UNQ_StudySessions_Uid | — | 外部业务键（uuid，API/DTO 暴露） |
| 3 | UserId | long | — | →Users | ✅ | — | IX_StudySessions_UserId_StartedAt | DESC | 学生用户 |
| 4 | Scenario | string | — | — | ✅ | "Memorize" | — | — | 场景（见 LearningScenario 枚举） |
| 5 | BankId | string | — | — | — | — | — | — | 题库业务键（跨模块引用 Banks） |
| 6 | SessionType | string | — | — | ✅ | — | — | — | 会话类型（见 SessionType 枚举） |
| 7 | QuestionCount | int | — | — | ✅ | 0 | — | — | 计划题数 |
| 8 | CorrectCount | int | — | — | ✅ | 0 | — | — | 本次答对题数 |
| 9 | TotalTimeMs | int | — | — | ✅ | 0 | — | — | 累计用时（毫秒） |
| 10 | TaskId | long? | — | →Tasks | — | — | IX_StudySessions_TaskId | — | 群组任务归属（个人背诵 NULL，部分索引 WHERE TaskId IS NOT NULL） |
| 11 | StartedAt | DateTime | — | — | ✅ | — | — | — | 会话开始时间 |
| 12 | EndedAt | DateTime? | — | — | — | — | — | — | 会话结束时间 |
| 13 | CreateTime | DateTime | — | — | ✅ | — | — | — | 创建时间（框架审计字段，D02 无此列，见存量差异②） |
| 14 | UpdateTime | DateTime | — | — | ✅ | — | — | — | 更新时间（框架审计字段） |

> **软删除**：否
> **数据权限**：学生私域数据，DataService 查询默认带 UserId 过滤

### Attempts ── 作答记录（唯一事实源）

**子域**：`Learning`
**文件路径**：`Entities/Learning/Attempts.cs`
**命名空间**：`{App}.Entities.Learning`

**表名**：`Attempts`

| # | 属性名 | 类型 | 主键 | 外键 | 必填 | 默认值 | 索引 | 排序 | 业务说明 |
|:-:|-------|------|:---:|:---:|:---:|:-----:|:----:|:---:|---------|
| 1 | Id | long | ✅ | — | ✅ | — | PK | — | 自增主键 |
| 2 | Uid | Guid | — | — | ✅ | — | UNQ_Attempts_Uid | — | 外部业务键 |
| 3 | UserId | long | — | →Users | ✅ | — | IX_Attempts_UserId_AnsweredAt | DESC | 学生用户 |
| 4 | SessionId | long? | — | →StudySessions | — | — | IX_Attempts_SessionId | — | 学习会话归属 |
| 5 | QuestionId | string | — | — | ✅ | — | IX_Attempts_UserId_QuestionId | — | 题目业务键（Q-ch-7a-0001） |
| 6 | BankId | string | — | — | ✅ | — | — | — | 题库业务键 |
| 7 | Scenario | string | — | — | ✅ | — | — | — | 场景（LearningScenario，由会话派生） |
| 8 | QType | string | — | — | ✅ | — | — | — | 题型键（R1/R2/R3a/R3b/O1...） |
| 9 | PreState | string | — | — | ✅ | — | — | — | 作答前记忆状态（MemoryState） |
| 10 | PostState | string | — | — | ✅ | — | — | — | 作答后记忆状态（MemoryState） |
| 11 | Result | string | — | — | ✅ | — | IX_Attempts_UserId_Result | — | 判题结果（JudgmentResult） |
| 12 | Confidence | double? | — | — | — | — | — | — | 判题置信度（0-1） |
| 13 | HintLevel | string | — | — | ✅ | "None" | — | — | 求助档位（HintLevel 枚举） |
| 14 | TimeCostMs | int? | — | — | — | — | — | — | 作答耗时（服务端计时） |
| 15 | AnsweredAt | DateTime | — | — | ✅ | — | — | — | 作答时间 |
| 16 | CreateTime | DateTime | — | — | ✅ | — | — | — | 创建时间（框架审计字段） |
| 17 | UpdateTime | DateTime | — | — | ✅ | — | — | — | 更新时间（框架审计字段） |

> **软删除**：否（作答历史不删不改，D02 原则 #5）
> **分区建议**：按月分区（AnsweredAt），实施阶段处理
> **写入纪律**：Attempts 是本域唯一事实源写入入口，MemoryStates/DailyStats/WrongQuestions 均由其派生

### MemoryStates ── 记忆状态（状态机运行表）

**子域**：`Learning`
**文件路径**：`Entities/Learning/MemoryStates.cs`
**命名空间**：`{App}.Entities.Learning`

**表名**：`MemoryStates`

| # | 属性名 | 类型 | 主键 | 外键 | 必填 | 默认值 | 索引 | 排序 | 业务说明 |
|:-:|-------|------|:---:|:---:|:---:|:-----:|:----:|:---:|---------|
| 1 | Id | long | ✅ | — | ✅ | — | PK | — | 自增主键 |
| 2 | Uid | Guid | — | — | ✅ | — | UNQ_MemoryStates_Uid | — | 外部业务键 |
| 3 | UserId | long | — | →Users | ✅ | — | IX_MemoryStates_UserId_NextReviewAt | ASC | 学生用户 |
| 4 | QuestionId | string | — | — | ✅ | — | — | — | 题目业务键 |
| 5 | BankId | string | — | — | ✅ | — | — | — | 题库业务键 |
| 6 | State | string | — | — | ✅ | "NotMastered" | IX_MemoryStates_UserId_State | — | 记忆状态（MemoryState） |
| 7 | ConsecutiveCorrect | int | — | — | ✅ | 0 | — | — | 连续独立答对（2 次→★） |
| 8 | HistoryAccuracy | double | — | — | ✅ | 0 | — | — | 近 20 次正确率（间隔系数 0.5~1.5） |
| 9 | EaseFactor | double | — | — | ✅ | 2.5 | — | — | 预留（Phase2 SM-2） |
| 10 | NextReviewAt | DateTime | — | — | ✅ | — | — | — | 下次复习（驱动队列） |
| 11 | LastAttemptId | long? | — | →Attempts | — | — | — | — | 最近一次作答 |
| 12 | LastHintLevel | string | — | — | — | "None" | — | — | 最近求助档位 |
| 13 | CreateTime | DateTime | — | — | ✅ | — | — | — | 创建时间（框架审计字段，D02 原无，见存量差异②） |
| 14 | UpdateTime | DateTime | — | — | ✅ | — | — | — | 更新时间（D02 原 UpdatedAt，改名） |

> **唯一约束**：UNQ_MemoryStates_UserId_QuestionId（一人一题一行）
> **软删除**：否
> **复习队列视图**（不建表，DS 标注供实施参考）：
> ```sql
> -- CV_ReviewQueue（视图，非实体）
> SELECT UserId, QuestionId, State, NextReviewAt
> FROM MemoryStates
> WHERE NextReviewAt <= now() AND State <> 'Proficient'  -- ★ 熟练不强制复习
> ```

### DailyStats ── 每日统计（热力图/连续天数数据源）

**子域**：`Learning`
**文件路径**：`Entities/Learning/DailyStats.cs`
**命名空间**：`{App}.Entities.Learning`

**表名**：`DailyStats`

| # | 属性名 | 类型 | 主键 | 外键 | 必填 | 默认值 | 索引 | 排序 | 业务说明 |
|:-:|-------|------|:---:|:---:|:---:|:-----:|:----:|:---:|---------|
| 1 | Id | long | ✅ | — | ✅ | — | PK | — | 自增主键 |
| 2 | Uid | Guid | — | — | ✅ | — | UNQ_DailyStats_Uid | — | 外部业务键 |
| 3 | UserId | long | — | →Users | ✅ | — | IX_DailyStats_UserId_StatDate | DESC | 学生用户 |
| 4 | StatDate | DateOnly | — | — | ✅ | — | — | — | UTC+8 业务日期 |
| 5 | LearnedCount | int | — | — | ✅ | 0 | — | — | 学习题数 |
| 6 | StarredCount | int | — | — | ✅ | 0 | — | — | 点亮★数（热力图） |
| 7 | ReviewCount | int | — | — | ✅ | 0 | — | — | 复习题数 |
| 8 | Accuracy | double? | — | — | — | — | — | — | 当日正确率 |
| 9 | StudySeconds | int | — | — | ✅ | 0 | — | — | 学习时长 |
| 10 | CreateTime | DateTime | — | — | ✅ | — | — | — | 创建时间（框架审计字段） |
| 11 | UpdateTime | DateTime | — | — | ✅ | — | — | — | 更新时间（框架审计字段） |

> **唯一约束**：UNQ_DailyStats_UserId_StatDate
> **软删除**：否
> **连续天数**：由 StatDate 连续性 SQL 派生（不建 UserStreaks 表）

### KnowledgeMastery ── 知识点掌握度（聚合视图落库）

**子域**：`Learning`
**文件路径**：`Entities/Learning/KnowledgeMastery.cs`
**命名空间**：`{App}.Entities.Learning`

**表名**：`KnowledgeMastery`

| # | 属性名 | 类型 | 主键 | 外键 | 必填 | 默认值 | 索引 | 排序 | 业务说明 |
|:-:|-------|------|:---:|:---:|:---:|:-----:|:----:|:---:|---------|
| 1 | Id | long | ✅ | — | ✅ | — | PK | — | 自增主键 |
| 2 | Uid | Guid | — | — | ✅ | — | UNQ_KnowledgeMastery_Uid | — | 外部业务键 |
| 3 | UserId | long | — | →Users | ✅ | — | IX_KnowledgeMastery_UserId_Subject | ASC | 学生用户 |
| 4 | Subject | string | — | — | ✅ | — | — | — | 学科 |
| 5 | KnowledgePoint | string | — | — | ✅ | — | — | — | 知识点 |
| 6 | State | string | — | — | ✅ | — | — | — | 聚合状态（取题目中位/最差，MemoryState） |
| 7 | Accuracy | double | — | — | ✅ | 0 | — | — | 聚合正确率 |
| 8 | AttemptCount | int | — | — | ✅ | 0 | — | — | 作答次数 |
| 9 | LastReviewedAt | DateTime? | — | — | — | — | — | — | 最近复习时间 |
| 10 | CreateTime | DateTime | — | — | ✅ | — | — | — | 创建时间（框架审计字段） |
| 11 | UpdateTime | DateTime | — | — | ✅ | — | — | — | 更新时间（框架审计字段） |

> **唯一约束**：UNQ_KnowledgeMastery_UserId_Subject_KnowledgePoint
> **软删除**：否
> **写入时机**：Hangfire 每小时批量 / 家长报告按需触发

### WrongQuestions ── 错题本（物化派生表）

**子域**：`Learning`
**文件路径**：`Entities/Learning/WrongQuestions.cs`
**命名空间**：`{App}.Entities.Learning`

**表名**：`WrongQuestions`

| # | 属性名 | 类型 | 主键 | 外键 | 必填 | 默认值 | 索引 | 排序 | 业务说明 |
|:-:|-------|------|:---:|:---:|:---:|:-----:|:----:|:---:|---------|
| 1 | Id | long | ✅ | — | ✅ | — | PK | — | 自增主键 |
| 2 | Uid | Guid | — | — | ✅ | — | UNQ_WrongQuestions_Uid | — | 外部业务键 |
| 3 | UserId | long | — | →Users | ✅ | — | IX_WrongQuestions_UserId_Mastered_LastWrongAt | DESC | 学生用户 |
| 4 | QuestionId | string | — | — | ✅ | — | — | — | 题目业务键 |
| 5 | BankId | string | — | — | ✅ | — | — | — | 题库业务键 |
| 6 | Subject | string | — | — | ✅ | — | IX_WrongQuestions_UserId_Subject | — | 冗余学科字段（跨学科查询免 join） |
| 7 | WrongCount | int | — | — | ✅ | 1 | — | — | 错误次数 |
| 8 | LastWrongAt | DateTime | — | — | ✅ | — | — | — | 最近错误时间 |
| 9 | Mastered | bool | — | — | ✅ | false | — | — | 连续 2 次答对 → true |
| 10 | CreateTime | DateTime | — | — | ✅ | — | — | — | 创建时间（框架审计字段） |
| 11 | UpdateTime | DateTime | — | — | ✅ | — | — | — | 更新时间（框架审计字段） |

> **唯一约束**：UNQ_WrongQuestions_UserId_QuestionId
> **软删除**：否（已掌握保留记录供复习回看）
> **派生来源**：Attempts（Result=Wrong/Partial）归集；Mastered 连续 2 次 Correct 后置 true

## 枚举定义

#### MemoryState ── 四阶记忆状态

存储格式：`string`（PascalCase；存量 DDL 为 smallint 0-3，见存量差异③）
使用场景：MemoryStates.State / Attempts.PreState / Attempts.PostState / KnowledgeMastery.State

| 枚举值 | 业务含义 | 状态色（全端统一） | 说明 |
|:------:|---------|:---:|------|
| NotMastered | 未掌握 | ✕ 灰"再背背" | 答错/未形成记忆 |
| Fuzzy | 模糊 | △ 橙"快熟了" | 求助后才对 |
| Mastered | 掌握 | ○ 绿"很棒" | 独立答对 |
| Proficient | 熟练 | ★ 金"点亮了" | 多次独立答对，长期记忆 |

#### LearningScenario ── 学习场景

存储格式：`string`（PascalCase；存量为小写 memorize/assess/play_pk/play_daily，见存量差异③）
使用场景：StudySessions.Scenario / Attempts.Scenario

| 枚举值 | 业务含义 | 状态机耦合 |
|:------:|---------|-----------|
| Memorize | 背记 | full（正常迁移） |
| Assess | 检验 | feedback_only（答错降级、答对不升级） |
| PlayPk | 搭子 PK | isolated（不影响记忆状态；PK 答题入 PkAttempts 不入 Attempts） |
| PlayDaily | 每日趣味 | isolated |

#### SessionType ── 会话类型

存储格式：`string`（PascalCase；存量 DDL 为中文"分阶/自由/组卷/关卡/PK"，见存量差异③）
使用场景：StudySessions.SessionType

| 枚举值 | 业务含义 | 说明 |
|:------:|---------|------|
| Progressive | 分阶 | 按记忆状态分阶检索 |
| Free | 自由 | 自由背诵 |
| Assembled | 组卷 | 组卷作答 |
| Level | 关卡 | 关卡通关 |
| Pk | PK | 搭子对战（场景隔离） |

#### JudgmentResult ── 判题结果

存储格式：`string`（PascalCase；存量为小写 correct/partial/wrong，见存量差异③）
使用场景：Attempts.Result

| 枚举值 | 业务含义 | 说明 |
|:------:|---------|------|
| Correct | 正确 | 答案正确 |
| Partial | 部分正确 | 关键要点部分命中 |
| Wrong | 错误 | 答案错误 |

#### HintLevel ── 求助档位

存储格式：`string`（PascalCase；存量为小写 none/partial/full，见存量差异③）
使用场景：Attempts.HintLevel / MemoryStates.LastHintLevel

| 枚举值 | 业务含义 | 说明 |
|:------:|---------|------|
| None | 未求助 | 独立作答 |
| Partial | 部分提示 | 点击"请提示我"后作答 |
| Full | 直接看答案 | 不计入记录、不迁移状态 |

## 实体关系

```
Users (1) ──→ (N) StudySessions        学生创建学习会话
StudySessions (1) ──→ (N) Attempts      会话包含作答
Users (1) ──→ (N) Attempts             学生作答
Attempts (1) ──→ (0..1) MemoryStates   最近作答引用（LastAttemptId）
Users (1) ──→ (N) MemoryStates         学生每题的记忆状态（一人一题一行）
Users (1) ──→ (N) DailyStats           学生每日统计（一人一日一行）
Users (1) ──→ (N) KnowledgeMastery     学生知识点掌握度
Users (1) ──→ (N) WrongQuestions       学生错题（一人一题一行）
Tasks (1) ──→ (N) StudySessions        群组任务关联会话（TaskId；个人背诵 NULL）
Banks (1) ──→ (N) Attempts             题库业务键引用（跨模块，Banks 在题库域）
```

> 跨模块实体引用（不在本 DS 定义）：`Users`（账户域）、`Tasks`（任务域）、`Banks`/`Questions`（题库域 D01 §7.6）——均为业务键/外键引用，编译依赖通过框架 DI 注入。

## 存量差异标注（D02 DDL → TKWF 框架）

| # | 差异项 | D02 存量 | TKWF 要求（DG-05） | 处理建议 |
|:-:|-------|---------|-------------------|---------|
| ① | **主键类型** | `uuid` | `long Id`（硬性） | **沿用切片 01 决策（2026-09-06）**：系统内部用 `long Id` 主键/外键；外部关联、跨系统用 `Uid`（uuid 业务键）——实体表补 `Uid` 列（唯一索引），API 参数/响应/DTO 暴露 Uid，内部关联用 long |
| ② | **审计字段** | `CreatedAt/UpdatedAt`（部分表无审计列，如 StudySessions/Attempts/DailyStats） | `CreateTime/UpdateTime`（AutoManagedField 对齐） | 本 DS 已按 CreateTime/UpdateTime 转化并补齐缺失审计列；存量迁移脚本改名/补列 |
| ③ | **枚举存储** | smallint(0-3) 状态 + 小写/中文枚举值（memorize/correct/分阶） | PascalCase 字符串 `"Memorize"/"Correct"/"Progressive"` | 本 DS 已按 PascalCase 转化；存量数据迁移时映射（0→NotMastered...、分阶→Progressive） |
| ④ | **状态符号** | Unicode ✕△○★（文档表示） | 无框架要求 | 启用 `[JsonConverter]` 将枚举映射为 UI 展示符号；文档/UI 用 ✕△○★，存储用枚举名 |
| ⑤ | **错误码** | 数字域码 30xx | DG-06 默认 SNAKE_CASE | **沿用切片 01 决策**：保留数字码 + 语义名双列（见 U01 错误码表） |
| ⑥ | **复习队列** | D02 定义 SQL VIEW ReviewQueue | DataService 不跨实体 | 视图不建实体/DataService；由 GetReviewQueueService 直接读视图（或内存筛选 MemoryStates），见 U01 UC-4.4 |

> **字段覆盖校对（S/UI → DS）**：S01 场景与 UI 页面展示字段（任务名/群主名/截止时间/进度 x/y、题型、输入框/语音、判题反馈、进度点阵、★点亮、记忆状态色、求助提示、卡壳知识点、复习天数/逾期天数、错题知识点/错因/错误次数、正确率大数字、新增★数、下次重点复习列表、每日统计热力图）——任务名/群主名/截止时间/进度来自 Tasks/TaskAssignments（任务域跨模块）；题型来自 Questions.QType（题库域）；其余均能在上述实体找到对应字段 ✅（卡壳知识点 = Attempts.PostState + Questions 知识点映射，跨模块取知识点元数据）

> *文档版本：第一阶段 v1.1*
> *编制日期：2026-09-06*
> *同步版本：R01 v1.1 / S01 v1.1*