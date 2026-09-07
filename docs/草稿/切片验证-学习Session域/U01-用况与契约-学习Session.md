# U01-学习Session · 用况与契约

> **所属阶段**：第一阶段（设计层），切片验证（路径 B 转化）
> **前置条件**：DS01 + 定稿场景 S01 已产出
> **产出物**：学习 Session 域 Use-Case + 契约定义 → 输入给 Agentic Coding
>
> **转化说明**：由 D03 V1.3（§五 学习域 5.1~5.6 + §7.4 错题本跨域引用）API 契约拆分为 UC。**错误码决策**：保留 D03 数字域错误码 + SNAKE_CASE 语义名双列（DG-06 附录「既有错误码体系迁移」场景验证）。
> **跨模块依赖**：判题服务（D01 §9 判题引擎）——在 UC 头 + 编排逻辑 + 实施总览三处一致标注。
> **分页决策（DG-06-附录 2026-09-06 更新）**：D03 存量在学习域内分页不一致（5.4 复习队列用 `limit`、5.5 记忆状态用 `page`）。本域**显式决策：统一 `page` + `size`（响应 `{items, total}`）**——UC-4.4 复习队列由 D03 原 `limit` 迁移为 `page`/`size`；D03 源文档回改待排期，实施以本 U01 为准。

---

# 一、本域 UC 总览

| UC# | UC 名称 | 类型 | Service | 子域 | 事务 |
|:---:|---------|:----:|---------|:----:|:----:|
| 4.1 | 开始学习会话 | UI | CreateStudySessionService | Learning | CROSS |
| 4.2 | 提交作答（判题+状态迁移+派生同步） | UI | SubmitAttemptService | Learning | CROSS |
| 4.3 | 请求提示（背景钩子） | UI | GetHintService | Learning | SINGLE |
| 4.4 | 复习队列查询 | UI | GetReviewQueueService | Learning | SINGLE |
| 4.5 | 记忆状态查询 | UI | GetMemoryStatesService | Learning | SINGLE |
| 4.6 | 会话结果 | UI | GetSessionResultService | Learning | SINGLE |
| 4.7 | 错题本查询（跨域引用统计域） | UI | GetWrongQuestionsService | Learning | SINGLE |
| 4.8 | 知识点掌握度聚合（BackgroundJob） | Job | KnowledgeMasteryAggregationJob | Learning | CROSS |

---

## UC-4.1：开始学习会话

**对应场景**：S01-4.1 → 步骤 1~2
**对应 UI 图**：task#1~2
**子域**：`Learning`
**事务范围**：CROSS（写 StudySessions + 推进 TaskAssignments 跨模块）
**事务策略说明**：任务会话须在创建会话的同事务内推进任务执行状态（in_progress + SessionId 关联），任一步失败全部回滚，防止会话已建但任务状态未更新
**故障容忍度**：Required
**权限要求**：`student`
**关联 R 功能点**：R01-4.1
**API 路由**：`POST /api/v1/study/sessions`

---

### [场景层] — 给 UI/验收看

#### Precondition（进入条件）
1. 用户已登录且角色为学生（微信授权 [SDKStep] 已完成）
2. 从任务卡 / 复习队列卡 / 自由背诵入口发起

#### Main Flow（主流程）
1. 学生点击任务卡 → 系统创建会话（Scenario=Memorize + 关联任务）→ 装载第 1 题，答题框聚焦
2. 自由背诵入口 → 系统创建会话（Scenario=Memorize，无任务关联）
3. 复习队列入口 → 系统创建复习会话（排序：到期复习优先 → 未掌握优先 → 新题）

#### Alternative Flow（替代流程）
- 1a. 任务不存在 / 已截止 → 提示「任务不存在/已截止」→ 停留当前页（BR-02，5101/5102）
- 1b. 题库不存在 → 提示「题库不存在」→ 停留当前页（BR-03，1501）
- 1c. 同一任务会话重复发起 → 续用现有会话（进度保留），不重复创建（BR-05）

#### Postcondition（完成后状态）
- 数据：StudySessions 已写入；任务会话同步 TaskAssignments.Status=in_progress、SessionId 关联（跨模块）
- 页面：进入作答中态（第 1 题展示）

---

### [契约层] — 给 Agentic Coding

> ⚠️ **编排逻辑格式要求**：纯业务语言，不写具体框架 API、异常捕获、DI 解析。

#### Service 类声明

位置：`Domain/Services/Learning/`

```csharp
[GenerateController]
[Transactional]
public class CreateStudySessionService : DomainServiceBase<TUserInfo>
{
    public async Task<CreateStudySessionResDto> ExecuteAsync(CreateStudySessionReqDto request)
    {
        // 编排逻辑见下方
        throw new NotImplementedException();
    }
}
```

#### Request DTO

位置：`Domain/Dtos/Learning/Request/`

| 属性 | 类型 | 必填 | 默认值 | 来源 | 说明 |
|------|------|:---:|:-----:|------|------|
| Scenario | string | ✅ | — | 本UC定义 | Memorize/Assess/PlayPk/PlayDaily |
| BankId | string | ✅ | — | 本UC定义 | 题库业务键 |
| SessionType | string | ✅ | — | 本UC定义 | Progressive/Free/Assembled/Level/Pk |
| TaskId | string(uuid)? | — | — | 本UC定义 | 群组任务关联（任务会话必传） |
| QuestionCount | int | — | 20 | 本UC定义 | 10/20/30/50 |

#### Response DTO

位置：`Domain/Dtos/Learning/Response/`

| 属性 | 类型 | 来源 | 说明 |
|------|------|------|------|
| SessionUid | string | 本UC定义 | 会话外部键（uuid） |
| QuestionCount | int | 本UC定义 | 本次题数 |

#### 响应示例

```json
{
  "code": 0,
  "data": { "sessionUid": "a3f1...", "questionCount": 20 }
}
```

#### 业务规则（每个规则即为一个测试用例）

| BR# | 规则说明 | 测试场景（输入 -> 预期结果） |
|:---:|---------|---------|
| BR-01 | Scenario/SessionType 必须为合法枚举值 | 传非法枚举 -> 1002 参数错误 |
| BR-02 | 任务会话的 taskId 必须指向存在且未截止的任务 | 任务已截止 -> 5102；任务不存在 -> 5101 |
| BR-03 | 题库必须存在（bankId 校验，跨模块） | 非法 bankId -> 1501 |
| BR-04 | 题量可选 10/20/30/50，默认 20；混合比 30% 新题 + 70% 复习 | 题量 15 -> 1002；题量 10 -> 成功 |
| BR-05 | 同一任务续做幂等：已存在进行中会话则复用，不重复建 | 二次发起同任务 -> 返回原 SessionUid |

#### 错误码定义

| 错误码 | 语义名 | HTTP | 业务含义 | 触发条件 |
|:------:|:------:|:----:|---------|:--------:|
| 1001 | UNAUTHORIZED | 401 | 未登录/令牌失效 | Precondition |
| 1002 | PARAM_INVALID | 400 | 参数错误（枚举/题量） | BR-01/BR-04 |
| 1501 | BANK_NOT_FOUND | 404 | 题库不存在 | BR-03 |
| 5101 | TASK_NOT_FOUND | 404 | 任务不存在 | BR-02 |
| 5102 | TASK_CLOSED | 409 | 任务已截止/关闭 | BR-02 |

#### 依赖的 DataService

```csharp
// DataService（自动生成）
IStudySessionsDataService         // 创建会话（本域）
IMemoryStatesDataService          // 复习队列装载（到期优先排序，本域）

// BusinessService（跨模块）
ITasksDataService                 // 任务校验（任务域）
ITaskAssignmentsDataService       // 任务进度推进（任务域）
IBanksDataService                 // 题库校验（题库域 D01 §7.6）
```

#### Service 编排逻辑

| 步骤 | 容忍度 | 操作描述 | 涉及 DataService | 对应规则 |
|:----:|:------:|---------|:----------------:|:--------:|
| 1 | Required | 校验枚举值合法 | — | BR-01 |
| 2 | Required | 任务会话校验任务存在且未截止（跨模块） | `ITasksDataService` | BR-02 |
| 3 | Required | 校验题库存在（跨模块） | `IBanksDataService` | BR-03 |
| 4 | Required | 幂等检查：同任务已存在进行中会话 → 复用 | `IStudySessionsDataService` | BR-05 |
| 5 | Required | 按到期优先→未掌握→新题顺序装载题集（题量/混合比） | `IMemoryStatesDataService` | BR-04 |
| 6 | Required | 创建会话（Scenario/SessionType/BankId/TaskId） | `IStudySessionsDataService` | — |
| 7 | Optional | 任务会话推进 TaskAssignments：Status=in_progress + SessionId（失败回滚整事务） | `ITaskAssignmentsDataService` | BR-02/BR-05 |

> Optional 标注说明：步骤 7 虽为 Optional（非任务会话时跳过），但其失败按事务策略必须回滚——事务由 `[Transactional]` 保证。

---

## UC-4.2：提交作答（判题 + 状态迁移 + 派生同步）★核心

**对应场景**：S01-4.1 → 步骤 3~8
**对应 UI 图**：task#3~13
**子域**：`Learning`
**事务范围**：CROSS（Attempts + MemoryStates + DailyStats + WrongQuestions + TaskAssignments.Progress + 跨模块判题）
**事务策略说明**：作答记录、记忆状态、每日统计、错题本必须同生共死——事实源单一原则（D02 原则 #3）要求派生数据与 Attempts 一致，防止不一致漂移
**故障容忍度**：Required（判题降级除外——判题服务不可用时降级本地规则，作答仍完成）
**权限要求**：`student`
**关联 R 功能点**：R01-4.2 / 4.4 / 4.6 / 4.7
**API 路由**：`POST /api/v1/study/attempts`
**跨模块依赖**：判题服务（D01 §9 判题引擎）——本 UC 编排逻辑调用判题契约（五键：result/confidence/matched/missing/hint）

---

### [场景层]

#### Precondition
1. 用户已登录且角色为学生
2. 会话存在且属于当前用户
3. 已装载当前题目

#### Main Flow
1. 学生输入/语音输入答案 → 提交 → 系统"判题中…"（≤1s 快判，禁重复提交）
2. 判题服务判定（本地关键词 ≥85% 正确 → LLM 兜底；LLM 超时降级本地规则）
3. 按判题结果 × hintLevel 迁移记忆状态（见 BR-11~21 状态机矩阵）
4. 同一事务写入 Attempts + 同步派生 DailyStats/WrongQuestions/TaskAssignments.Progress
5. 页面反馈：正确（绿+★动画）/ 错误（红+正确句+【再看一遍/下一题】）/ 部分（黄△）
6. 完成最后一题 → 会话结束 → 结果页（对应 UC-4.6）

#### Alternative Flow
- 1a. 会话不存在/不属于当前用户 → 3001（BR-06）
- 1b. 答案格式非法 → 3002（BR-07）
- 1c. 提交间隔 <2s（防刷）→ 1005（BR-08）
- 2a. LLM 超时(>3s)/失败 → 降级本地规则引擎 + 提示用户（BR-09）
- 2b. 题目不存在/不属该题库 → 1502（BR-26）
- 6a. 重复提交同一题（幂等）→ 返回已有结果（BR-27）

#### Postcondition
- 数据：Attempts 写入；MemoryStates 更新（状态/间隔/连续答对数）；DailyStats 当日累加；WrongQuestions 归集/置顶；任务进度推进
- 页面：正确/错误/部分反馈态；最后一题完成 → 结果页

---

### [契约层]

#### Service 类声明

位置：`Domain/Services/Learning/`

```csharp
[GenerateController]
[Transactional]
public class SubmitAttemptService : DomainServiceBase<TUserInfo>
{
    public async Task<SubmitAttemptResDto> ExecuteAsync(SubmitAttemptReqDto request)
    {
        // 编排逻辑见下方
        throw new NotImplementedException();
    }
}
```

#### Request DTO

位置：`Domain/Dtos/Learning/Request/`

| 属性 | 类型 | 必填 | 来源 | 说明 |
|------|------|:---:|------|------|
| SessionUid | string | ✅ | 本UC定义 | 会话外部键 |
| QuestionId | string | ✅ | 本UC定义 | 题目业务键 |
| UserAnswer | string | ✅ | 本UC定义 | 用户作答文本 |
| HintLevel | string | ✅ | 本UC定义 | None/Partial/Full |

#### Response DTO

位置：`Domain/Dtos/Learning/Response/`

| 属性 | 类型 | 来源 | 说明 |
|------|------|------|------|
| Result | string | 判题服务 | Correct/Partial/Wrong |
| Confidence | double? | 判题服务 | 判题置信度 |
| MatchedKeywords | string[] | 判题服务 | 命中关键词 |
| MissingKeywords | string[] | 判题服务 | 缺失关键词 |
| Hint | string | 判题服务 | 引导线索（hintLevel=Partial 时必给） |
| PreState | string | 本UC定义 | 迁移前状态（MemoryState） |
| PostState | string | 本UC定义 | 迁移后状态（MemoryState） |
| NextReviewAt | DateTime | 本UC定义 | 下次复习时间 |

#### 响应示例

```json
{
  "code": 0,
  "data": {
    "result": "Correct",
    "confidence": 0.98,
    "matchedKeywords": ["若出其中"],
    "missingKeywords": [],
    "hint": "",
    "preState": "Fuzzy",
    "postState": "Mastered",
    "nextReviewAt": "2026-09-06T08:00:00Z"
  }
}
```

#### 业务规则（每个规则即为一个测试用例）

> **四阶记忆状态机完整迁移 BR（本域核心，验证点 #1）**——对齐 D01 §10.2 迁移矩阵 + D02 §4.1/4.2。

| BR# | 规则说明 | 测试场景（输入 -> 预期结果） |
|:---:|---------|---------|
| BR-06 | 会话必须存在且属于当前用户 | 他人会话 -> 3001 |
| BR-07 | 答案文本必须合法（非空、格式正确） | 空答案 -> 3002 |
| BR-08 | 每题提交间隔 ≥2s（Redis 防刷） | 1.5s 内二次提交 -> 1005 |
| BR-09 | 判题服务 LLM 超时(>3s)/失败 → 降级本地规则引擎，作答仍完成 | 模拟 LLM 超时 -> 本地规则判定 + 响应 |
| BR-10 | 判题结果三分：Correct/Partial/Wrong（关键词 ≥85%→Correct；60-85%→Partial；<60%→LLM；confidence ≥0.85→Correct/0.5-0.85→Partial/<0.5→Wrong） | 关键词命中 90% -> Correct |
| BR-11 | 独立答对（None+Correct）：✕→△、△→○、○→○；ConsecutiveCorrect +1 | 当前 △ 独立答对 -> PostState=Mastered |
| BR-12 | 熟练升级：○ 连续 2 次独立答对（跨会话累计） → ★ | ConsecutiveCorrect=1 再独立答对 -> Proficient |
| BR-13 | 求助后答对（Partial+Correct）：→ △（Fuzzy）；ConsecutiveCorrect 清零 | 当前 ○ 求助后答对 -> PostState=Fuzzy |
| BR-14 | 独立答错（None+Wrong）：○→△、★→○、△→✕；ConsecutiveCorrect 清零 | 当前 Proficient 答错 -> PostState=Mastered |
| BR-15 | 求助后答错（Partial+Wrong）：→ ✕（或停留 △）；ConsecutiveCorrect 清零 | 当前 △ 求助后答错 -> PostState=NotMastered |
| BR-16 | 直接看答案（Full）：不迁移状态、不计入作答记录 | hintLevel=Full -> 状态不变、无 Attempts |
| BR-17 | 间隔计算：NextReviewAt = 状态基础间隔 × HistoryAccuracy 系数（0.5~1.5）；基础间隔 ✕30min/△12h/○3d/★7d | 正确率 0.8 -> 间隔 ×1.0 区间内 |
| BR-18 | 首次背诵新题答错 → 30 分钟复习（非 12h） | 新题 ✕ 答错 -> NextReviewAt ≈ +30min |
| BR-19 | ★ 熟练停留：间隔递增至 30 天；★ 不强制复习（队列过滤） | Proficient 独立答对 -> 状态停留、间隔增大 |
| BR-20 | Assess 场景（feedback_only）：答错降级、答对不升级 | Assess 场景独立答对 -> 状态不升级 |
| BR-21 | Play 场景（isolated）：不影响 MemoryStates（PK 答题入 PkAttempts，非本域） | PlayPk 作答 -> 不写 MemoryStates |
| BR-22 | 同步派生：DailyStats 当日累加（LearnedCount/StarredCount/ReviewCount/Accuracy/StudySeconds） | 答对 1 题 -> LearnedCount+1、StarredCount 按 ★ 点亮 |
| BR-23 | 同步派生：WrongQuestions 归集（Result=Wrong/Partial）；连续 2 次 Correct（跨会话）→ Mastered=true | 答错 -> 错题 WardCount+1；连续 2 次对 -> Mastered |
| BR-24 | 同步派生：TaskAssignments.Progress 更新；全部完成 → Status=Completed | 完成全部题 -> Progress=100、Status=Completed |
| BR-25 | 作答后必须返回状态迁移结果（PreState/PostState/NextReviewAt），供前端渲染状态变化 | 响应含 preState/postState -> 前端显示状态色 |
| BR-26 | 题目必须存在且属于会话题库 | 非法 QuestionId -> 1502 |
| BR-27 | 幂等：同一 SessionUid + QuestionId 已记录则返回已有结果，不重复写 | 重复提交同题 -> 返回首次结果 |

#### 错误码定义

| 错误码 | 语义名 | HTTP | 业务含义 | 触发条件 |
|:------:|:------:|:----:|---------|:--------:|
| 3001 | SESSION_NOT_FOUND | 404 | 会话不存在 | BR-06 |
| 3002 | ANSWER_FORMAT_INVALID | 400 | 答案格式错误 | BR-07 |
| 1001 | UNAUTHORIZED | 401 | 未登录/令牌失效 | Precondition |
| 1005 | RATE_LIMIT_EXCEEDED | 429 | 提交过于频繁 | BR-08 |
| 1502 | QUESTION_NOT_IN_BANK | 400 | 题目不存在/不属该题库 | BR-26 |
| 1006 | INTERNAL_ERROR | 500 | 判题服务不可用（已降级） | BR-09 |

#### 依赖的 DataService

```csharp
// DataService（自动生成）
IStudySessionsDataService         // 会话校验（本域）
IAttemptsDataService              // 作答记录写入（本域）
IMemoryStatesDataService          // 状态迁移（本域）
IDailyStatsDataService            // 每日统计（本域）
IWrongQuestionsDataService        // 错题本（本域）
IQuestionsDataService             // 题目元数据（跨模块，题库域 D01 §7.6）
ITaskAssignmentsDataService       // 任务进度（跨模块，任务域）

// BusinessService（跨模块，判题服务）
// 判题服务（D01 §9）——本地关键词 + LLM 语义判题，返回统一五键契约
```

#### Service 编排逻辑

| 步骤 | 容忍度 | 操作描述 | 涉及 DataService | 对应规则 |
|:----:|:------:|---------|:----------------:|:--------:|
| 1 | Required | 校验会话存在且属于当前用户 | `IStudySessionsDataService` | BR-06 |
| 2 | Required | 校验答案格式 + 判题限流（Redis ≥2s） | — | BR-07/BR-08 |
| 3 | Required | 查题目（题库域 D01 §7.6 Questions 索引表，背一题取一题） | `IQuestionsDataService`(跨模块) | BR-26 |
| 4 | Required | 调用判题服务：keyword 前置 → LLM 兜底 → 五键契约返回；LLM 超时(>3s)降级本地规则 | 判题服务（跨模块） | BR-09/BR-10 |
| 5 | Required | 按 hintLevel × result 迁移记忆状态（全矩阵见 BR-11~21） | `IMemoryStatesDataService` | BR-11~21 |
| 6 | Required | 计算 NextReviewAt（间隔 × 正确率系数） | `IMemoryStatesDataService` | BR-17/BR-18 |
| 7 | Required | 写入 Attempts（Pre/PostState、Result、HintLevel） | `IAttemptsDataService` | — |
| 8 | Required | 同步 DailyStats（当日累加） | `IDailyStatsDataService` | BR-22 |
| 9 | Required | 同步 WrongQuestions（归集/连续 2 次 Mastered） | `IWrongQuestionsDataService` | BR-23 |
| 10 | Optional | 任务会话推进 TaskAssignments.Progress（非任务会话跳过） | `ITaskAssignmentsDataService` | BR-24 |
| 11 | Required | 返回迁移结果（PreState/PostState/NextReviewAt） | — | BR-25 |

> **跨模块依赖标注（验证点 #2）**：判题服务依赖在 UC 头、本编排逻辑第 4 步、实施总览三处一致标注为跨模块（D01 §9 判题引擎）。判题逻辑本域不实现，仅消费五键契约。

---

## UC-4.3：请求提示（背景钩子）

**对应场景**：S01-4.1 → 步骤 5~5b
**对应 UI 图**：task#7~9
**子域**：`Learning`
**事务范围**：SINGLE
**故障容忍度**：Required
**权限要求**：`student`
**关联 R 功能点**：R01-4.3
**API 路由**：`POST /api/v1/study/hint`

### [场景层]

#### Precondition
1. 已登录且角色为学生
2. 当前处于作答中状态且题目已加载

#### Main Flow
1. 学生点击"想不起来？" → 系统按题目状态路由提示难度档（S1 首字 → S2 意象 → S3 逻辑）
2. 系统返回 ≤20 字提示 + 难度档位 + 提示来源
3. 学生可反复求助；求助后作答 → 走 UC-4.2（HintLevel=Partial）

#### Alternative Flow
- 1a. 题目不存在 → 1502（BR-28）
- 2a. 提示生成服务不可用 → 返回本地兜底提示（BR-29）

#### Postcondition
- 数据：无写入（hint 不入 Attempts）
- 页面：HintBubble 展示提示

### [契约层]

#### Service 类声明

位置：`Domain/Services/Learning/`

```csharp
[GenerateController]
public class GetHintService : DomainServiceBase<TUserInfo>
{
    public async Task<GetHintResDto> ExecuteAsync(GetHintReqDto request) { throw new NotImplementedException(); }
}
```

#### Request DTO

| 属性 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| QuestionId | string | ✅ | 题目业务键 |
| DifficultySlot | string? | — | S1/S2/S3（可空，按状态路由） |

#### Response DTO

| 属性 | 类型 | 说明 |
|------|------|------|
| Hint | string | 提示文本（≤20 字线索） |
| DifficultySlot | string | S1/S2/S3 |
| HintSource | string | 提示来源（记忆钩子） |

#### 业务规则

| BR# | 规则说明 | 测试场景 |
|:---:|---------|---------|
| BR-28 | 题目必须存在 → 1502 | 非法 QuestionId -> 1502 |
| BR-29 | 提示 ≤20 字，仅首字/意象/逻辑线索，严禁直接给答案 | 提示超过 20 字 -> 截断；包含答案 -> 拒绝 |
| BR-30 | 难度档可空时按题目记忆状态路由（状态越低提示越深） | 未掌握题 -> 路由至 S3 |

#### 错误码定义

| 错误码 | 语义名 | HTTP | 业务含义 | 触发条件 |
|:------:|:------:|:----:|---------|:--------:|
| 1502 | QUESTION_NOT_IN_BANK | 400 | 题目不存在 | BR-28 |
| 1001 | UNAUTHORIZED | 401 | 未登录/令牌失效 | Precondition |
| 1002 | PARAM_INVALID | 400 | 参数错误 | BR-29/BR-30 |

#### 依赖的 DataService

```csharp
IQuestionsDataService              // 题目元数据（跨模块，题库域）
IMemoryStatesDataService           // 状态路由（本域）
```

#### Service 编排逻辑

| 步骤 | 容忍度 | 操作描述 | 涉及 DataService | 对应规则 |
|:----:|:------:|---------|:----------------:|:--------:|
| 1 | Required | 校验题目存在 | `IQuestionsDataService` | BR-28 |
| 2 | Required | 读取题目记忆状态，未指定难度档则按状态路由 | `IMemoryStatesDataService` | BR-30 |
| 3 | Required | 生成提示（背景钩子 Prompt，≤20 字，从知识卡片提取不从答案提取） | 判题服务（跨模块） | BR-29 |

---

## UC-4.4：复习队列查询

**对应场景**：S01-4.3 → 步骤 1~2a
**对应 UI 图**：review#1
**子域**：`Learning`
**事务范围**：SINGLE
**故障容忍度**：Required
**权限要求**：`student`
**关联 R 功能点**：R01-4.5
**API 路由**：`GET /api/v1/study/review-queue?date=2026-09-05&page=1&size=20`

### [场景层]

#### Precondition
1. 已登录且角色为学生

#### Main Flow
1. 学生进入首页复习区块 → 系统按日期筛选到期题目（NextReviewAt ≤ 指定日期）
2. 系统按状态色排序返回（逾期置顶红标；△ 优先于 ○）
3. 学生点击卡片 → 进入复习会话（复用 UC-4.1/4.2 流程）

#### Alternative Flow
- 1a. 无到期题目 → 复习区块整体隐藏，不占位（BR-31）
- 1b. 查询参数非法（date 格式错误 / 分页参数越界）→ 1002 参数错误（BR-35）

#### Postcondition
- 数据：无写入
- 页面：到期卡片列表（questionId/state/nextReviewAt，不含答案）

### [契约层]

#### Service 类声明

```csharp
[GenerateController]
public class GetReviewQueueService : DomainServiceBase<TUserInfo>
{
    public async Task<GetReviewQueueResDto> ExecuteAsync(GetReviewQueueReqDto request) { throw new NotImplementedException(); }
}
```

#### Request DTO

| 属性 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| Date | DateTime | ✅ | 查询日期 |
| PageIndex | int | — | 页码（页大小默认 20，分页决策统一 page/size） |
| PageSize | int | — | 每页条数（替代 D03 原 limit，上限 100） |

#### Response DTO

| 属性 | 类型 | 说明 |
|------|------|------|
| Items | MemoryStatesDto[] | QuestionId/State/NextReviewAt（DTO 最小化：复用自动生成 MemoryStatesDto，不含答案正文） |
| OverdueCount | int | 逾期数 |

#### 业务规则

| BR# | 规则说明 | 测试场景 |
|:---:|---------|---------|
| BR-31 | 空队列正常返回（非错误）；前端隐藏复习区块 | 无到期项 -> Items 为空 + 正常码 0 |
| BR-32 | 队列 = NextReviewAt ≤ 查询日期 且 State ≠ Proficient | 含 ★ 题 -> 不出现 |
| BR-33 | 逾期（NextReviewAt < 今日）置顶，△ 优先于 ○ 排序 | 构造逾期+到期 -> 逾期在前 |
| BR-34 | 响应不含答案正文（防渗透） | 响应 JSON -> 无答案字段 |
| BR-35 | 参数校验：date 必填且格式合法、PageSize 1~100 | date 非法 -> 1002；PageSize=0 -> 1002 |

#### 错误码定义

| 错误码 | 语义名 | HTTP | 业务含义 | 触发条件 |
|:------:|:------:|:----:|---------|:--------:|
| 1001 | UNAUTHORIZED | 401 | 未登录/令牌失效 | Precondition |
| 1002 | PARAM_INVALID | 400 | 参数错误 | — |

#### 依赖的 DataService

```csharp
IMemoryStatesDataService           // 队列筛选（本域；视图 ReviewQueue 由 Service 层直接读取）
```

#### Service 编排逻辑

| 步骤 | 容忍度 | 操作描述 | 涉及 DataService | 对应规则 |
|:----:|:------:|---------|:----------------:|:--------:|
| 1 | Required | 按 NextReviewAt ≤ Date 且 State ≠ Proficient 筛选当前用户到期项 | `IMemoryStatesDataService` | BR-32 |
| 2 | Required | 逾期置顶 + 状态色排序 | `IMemoryStatesDataService` | BR-33 |
| 3 | Required | 截断 limit、剔除答案字段 | — | BR-31/BR-34 |

---

## UC-4.5：记忆状态查询

**对应场景**：无 UI 场景（核心层 UC，供学习路线图/进度页数据消费）
**子域**：`Learning`
**事务范围**：SINGLE
**故障容忍度**：Required
**权限要求**：`student`
**关联 R 功能点**：R01-4.4
**API 路由**：`GET /api/v1/study/memory-states?bankId=xxx&state=1&page=1`

### [场景层]

#### Precondition
1. 已登录且角色为学生

#### Main Flow
1. 学生/前端请求题目记忆状态（按题库或状态过滤，分页）
2. 系统返回题目 id + 状态 + 正确率 + nextReviewAt

#### Alternative Flow
- 1a. 无匹配记录 → 返回空列表（正常）
- 1b. 分页参数非法（page<1 / size 越界）→ 1002 参数错误（BR-38）

#### Postcondition
- 数据：无写入
- 页面：状态列表

### [契约层]

#### Service 类声明

```csharp
[GenerateController]
public class GetMemoryStatesService : DomainServiceBase<TUserInfo>
{
    public async Task<GetMemoryStatesResDto> ExecuteAsync(GetMemoryStatesReqDto request) { throw new NotImplementedException(); }
}
```

#### Request DTO

| 属性 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| BankId | string? | — | 题库过滤 |
| State | string? | — | 状态过滤（NotMastered/Fuzzy/Mastered/Proficient） |
| PageIndex | int | — | 页码 |
| PageSize | int | — | 每页数 |

#### Response DTO

| 属性 | 类型 | 说明 |
|------|------|------|
| Items | MemoryStatesDto[] | QuestionId/State/HistoryAccuracy/NextReviewAt（DTO 最小化：复用自动生成 MemoryStatesDto） |

#### 业务规则

| BR# | 规则说明 | 测试场景 |
|:---:|---------|---------|
| BR-36 | 仅返回当前用户的状态（RLS） | 无法查询他人状态 |
| BR-37 | 过滤条件组合（bankId/state）可空 | 空条件 -> 全量分页 |
| BR-38 | 分页参数校验：page≥1、size 1~100 | page=0 -> 1002 |

#### 错误码定义

| 错误码 | 语义名 | HTTP | 业务含义 | 触发条件 |
|:------:|:------:|:----:|---------|:--------:|
| 1001 | UNAUTHORIZED | 401 | 未登录/令牌失效 | Precondition |
| 1002 | PARAM_INVALID | 400 | 参数错误 | — |

#### 依赖的 DataService

```csharp
IMemoryStatesDataService           // 状态查询（本域）
```

#### Service 编排逻辑

| 步骤 | 容忍度 | 操作描述 | 涉及 DataService | 对应规则 |
|:----:|:------:|---------|:----------------:|:--------:|
| 1 | Required | 按 UserId + 可选过滤条件分页查询 | `IMemoryStatesDataService` | BR-36/BR-37 |

---

## UC-4.6：会话结果

**对应场景**：S01-4.2 → 步骤 1~3a
**对应 UI 图**：result#1
**子域**：`Learning`
**事务范围**：SINGLE
**故障容忍度**：Required
**权限要求**：`student`
**关联 R 功能点**：R01-4.2 / 4.4
**API 路由**：`GET /api/v1/study/sessions/{sessionId}/result`

### [场景层]

#### Precondition
1. 已登录且角色为学生
2. 会话存在且属于当前用户

#### Main Flow
1. 会话完成（最后一题作答完毕）→ 系统聚合会话内作答统计
2. 返回：答对数/总题数/新增 ★ 数/下次重点复习列表（✕△ 题目 + 知识点）
3. 页面展示庆祝动画 + 大数字正确率（非百分比焦虑）+ 卡壳知识点

#### Alternative Flow
- 1a. 会话不存在/不属于当前用户 → 3001（BR-39）
- 1b. 会话未结束（空会话）→ 直接返回进行中状态（BR-40）

#### Postcondition
- 数据：无写入
- 页面：结果页展示

### [契约层]

#### Service 类声明

```csharp
[GenerateController]
public class GetSessionResultService : DomainServiceBase<TUserInfo>
{
    public async Task<GetSessionResultResDto> ExecuteAsync(GetSessionResultReqDto request) { throw new NotImplementedException(); }
}
```

#### Request DTO

| 属性 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| SessionUid | string | ✅ | 会话外部键 |

#### Response DTO

| 属性 | 类型 | 说明 |
|------|------|------|
| CorrectCount | int | 答对数 |
| TotalCount | int | 总题数 |
| NewStarCount | int | 新增 ★ 数 |
| BlockedPoints | BlockedPointDto[] | QuestionId/KnowledgePoint/State（下次重点复习） |

#### 业务规则

| BR# | 规则说明 | 测试场景 |
|:---:|---------|---------|
| BR-39 | 会话必须存在且属于当前用户 → 3001 | 他人会话 -> 3001 |
| BR-40 | 未作答退出（空会话）不产生结果页 | 空会话 -> 返回空统计，前端回首页 |
| BR-41 | NewStarCount = 本次会话中 PostState=Proficient 且 PreState<Proficient 的次数 | 1 题升至 ★ -> NewStarCount=1 |
| BR-42 | BlockedPoints = 会话内 State ∈ {NotMastered, Fuzzy} 的题目 + 知识点 | 含 ✕ 题 -> 列入重点复习 |

#### 错误码定义

| 错误码 | 语义名 | HTTP | 业务含义 | 触发条件 |
|:------:|:------:|:----:|---------|:--------:|
| 3001 | SESSION_NOT_FOUND | 404 | 会话不存在 | BR-39 |
| 1001 | UNAUTHORIZED | 401 | 未登录/令牌失效 | Precondition |
| 1002 | PARAM_INVALID | 400 | 参数错误 | — |

#### 依赖的 DataService

```csharp
IStudySessionsDataService           // 会话校验（本域）
IAttemptsDataService                // 作答聚合（本域）
IQuestionsDataService               // 知识点映射（跨模块，题库域 D01 §7.6）
```

#### Service 编排逻辑

| 步骤 | 容忍度 | 操作描述 | 涉及 DataService | 对应规则 |
|:----:|:------:|---------|:----------------:|:--------:|
| 1 | Required | 校验会话存在且属于当前用户 | `IStudySessionsDataService` | BR-39 |
| 2 | Required | 聚合会话内作答（CorrectCount/TotalCount） | `IAttemptsDataService` | BR-40 |
| 3 | Required | 计算 NewStarCount（升 ★ 次数） | `IAttemptsDataService` | BR-41 |
| 4 | Required | 提取卡壳知识点（✕/△ + 知识点映射） | `IAttemptsDataService` / `IQuestionsDataService`(跨模块) | BR-42 |

---

## UC-4.7：错题本查询（跨域引用统计域）

**对应场景**：S01-4.4 → 步骤 1~3
**对应 UI 图**：wrong#1
**子域**：`Learning`
**事务范围**：SINGLE
**故障容忍度**：Required
**权限要求**：`student`
**关联 R 功能点**：R01-4.6
**API 路由**：`GET /api/v1/stats/wrong-questions?mastered=false&page=1`（统计域路由，本域 Service 提供数据）

### [场景层]

#### Precondition
1. 已登录且角色为学生

#### Main Flow
1. 学生进入错题本 → 系统返回错题列表（知识点/错因/错误次数，支持分组与学科过滤）
2. 学生点"重练" → 进入复习会话（复用 UC-4.1/4.2）
3. 连续答对 2 次 → 题目转"已掌握"分组（Mastered 置位发生在 UC-4.2 BR-23）；可"移回错题本"

#### Alternative Flow
- 1a. 错题本空 → 空态"没有错题，记得很牢！"（BR-43）
- 1b. 查询参数非法（mastered/subject 校验失败）→ 1002 参数错误（BR-46）

#### Postcondition
- 数据：无写入
- 页面：错题列表/空态

### [契约层]

#### Service 类声明

```csharp
[GenerateController]
public class GetWrongQuestionsService : DomainServiceBase<TUserInfo>
{
    public async Task<GetWrongQuestionsResDto> ExecuteAsync(GetWrongQuestionsReqDto request) { throw new NotImplementedException(); }
}
```

#### Request DTO

| 属性 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| Mastered | bool | — | 分组过滤（false=待掌握，true=已掌握） |
| Subject | string? | — | 学科过滤 |
| PageIndex | int | — | 页码 |

#### Response DTO

| 属性 | 类型 | 说明 |
|------|------|------|
| Items | WrongQuestionsDto[] | QuestionId/知识点/错因(WrongCount)/LastWrongAt/Subject（DTO 最小化：复用 WrongQuestionsDto + KnowledgePoint 计算字段） |

#### 业务规则

| BR# | 规则说明 | 测试场景 |
|:---:|---------|---------|
| BR-43 | 空错题本正常返回空列表（非错误，前端展示正面空态） | 无错题 -> Items 为空 + 码 0 |
| BR-44 | 仅返回当前用户错题（RLS） | 无法查询他人错题 |
| BR-45 | Mastered 分组过滤 + 学科过滤可空 | 只查语文 -> 仅语文错题 |
| BR-46 | 参数校验：mastered 必须为布尔、subject 必须合法 | mastered=abc -> 1002 |

#### 错误码定义

| 错误码 | 语义名 | HTTP | 业务含义 | 触发条件 |
|:------:|:------:|:----:|---------|:--------:|
| 1001 | UNAUTHORIZED | 401 | 未登录/令牌失效 | Precondition |
| 1002 | PARAM_INVALID | 400 | 参数错误 | — |

#### 依赖的 DataService

```csharp
IWrongQuestionsDataService           // 错题查询（本域；数据由 UC-4.2 写入）
```

#### Service 编排逻辑

| 步骤 | 容忍度 | 操作描述 | 涉及 DataService | 对应规则 |
|:----:|:------:|---------|:----------------:|:--------:|
| 1 | Required | 按 UserId + Mastered + Subject 分页查询 | `IWrongQuestionsDataService` | BR-41~43 |

> **说明**：错题写入与"连续 2 次 → 已掌握"置位发生在 UC-4.2（BR-23）；本 UC 仅负责列表查询与重练入口。

---

## UC-4.8：知识点掌握度聚合（BackgroundJob）

**调度规则**：Hangfire 每小时批量（家长报告触发时按需执行）
**事务范围**：CROSS（扫描 Attempts + 批量聚合写入 KnowledgeMastery）
**故障容忍度**：Optional（单用户失败记录日志，不中断整批）
**关联 R 功能点**：R01-4.8

### Precondition（数据状态条件）
- Attempts 存在增量作答记录（上次聚合时间之后）

### Main Flow（系统流程，无用户操作）
1. 扫描上次聚合后的增量作答（按 UserId 分组）
2. 按 Subject × KnowledgePoint 聚合：State 取中位/最差、Accuracy 均值、AttemptCount 累计、LastReviewedAt 取最大
3. Upsert KnowledgeMastery（UserId+Subject+KnowledgePoint 唯一）

### Alternative Flow（数据异常路径）
- 1a. 无增量作答 → 跳过本次执行（BR-47）
- 2a. 单用户聚合失败 → 记录日志，继续下一用户（BR-48）

### Postcondition
- 数据：KnowledgeMastery 已 upsert（幂等，不重复累计）

### [契约层]

#### Service 类声明

位置：`Domain/Services/Learning/`

```csharp
// 不标注 [GenerateController]（无对外接口，调度器触发）
// 不标注 [AuthorityFilter]（系统内部任务）
// [Transactional]
public class KnowledgeMasteryAggregationJob : DomainServiceBase<TUserInfo>
{
    public async Task ExecuteAsync(CancellationToken ct) { throw new NotImplementedException(); }
}
```

#### 业务规则

| BR# | 规则说明 | 测试场景（输入 -> 预期结果） |
|:---:|---------|---------|
| BR-47 | 无增量作答则跳过（幂等） | 无新增 Attempts -> 不产生写入 |
| BR-48 | 单用户失败不影响整批（Optional） | 某用户数据异常 -> 该用户跳过，其余完成 |
| BR-49 | 聚合口径：State 取中位/最差、Accuracy 均值、AttemptCount 增量累加 | 2 次作答正确率 0.5/1.0 -> Accuracy=0.75 |
| BR-50 | 按需触发：家长报告打开时触发刷新 | 家长查报告 -> 触发本 Job |

#### 错误码定义

| 错误码 | 语义名 | HTTP | 业务含义 | 触发条件 |
|:------:|:------:|:----:|---------|:--------:|
| 1006 | INTERNAL_ERROR | 500 | 批次处理失败（仅日志/告警） | BR-48 |

#### 依赖的 DataService

```csharp
IAttemptsDataService              // 增量作答扫描（本域）
IKnowledgeMasteryDataService      // 聚合 upsert（本域）
```

#### 编排逻辑

| 步骤 | 容忍度 | 操作描述 | 涉及 DataService | 对应规则 |
|:----:|:------:|---------|:----------------:|:--------:|
| 1 | Required | 扫描增量作答（上次聚合后），空则返回 | `IAttemptsDataService` | BR-47 |
| 2 | Optional | 逐用户聚合 Subject×KnowledgePoint | `IAttemptsDataService` | BR-49 |
| 3 | Required | Upsert KnowledgeMastery | `IKnowledgeMasteryDataService` | BR-49/BR-50 |

> **调度方式**：由外部调度器（Hangfire）按「调度规则」调用，不生成 Controller、不进服务契约一览表（DG-07 §三 模式列标注 `C`）。

---

# 二、横切关注点标注

| 关注点 | 实现方式 | 本域 UC 标注 |
|--------|---------|-------------|
| 认证鉴权 | `[AuthorityFilter]` + 角色枚举 | UC-4.1~4.7 权限要求 `student`；UC-4.8 Job 不标注（系统内部） |
| 事务管理 | `[Transactional]` AOP | UC-4.1/4.2/4.8 CROSS（标注）；其余 SINGLE（DataService 自带） |
| 数据权限（RLS） | DataService 注入当前用户 | 全部查询 UC 默认按 UserId 过滤（BR-36/42） |
| 幂等性 | 业务级幂等 | 作答防重复（BR-27）；任务续做复用（BR-05）；Job 幂等（BR-47） |
| 限流 | Redis 计数 | 判题接口每题 ≥2s（BR-08）；通用单用户 60 次/分 |
| 缓存 | `[ContentCacheFilter]` | 复习队列/记忆状态读多写少，可标注缓存 TTL（实施阶段） |

---

> *文档版本：第一阶段 v1.1*
> *编制日期：2026-09-06*
> *同步版本：R01 v1.1 / S01 v1.1 / DS01 v1.1 / U01 v1.1*
> *修订：v1.1 分页决策落地（对齐全域 page/size）+ 边缘 BR 补齐（BR-35/38/46）*