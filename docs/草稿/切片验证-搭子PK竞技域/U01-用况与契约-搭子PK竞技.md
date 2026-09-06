# U01-搭子PK竞技 · 用况与契约

> **所属阶段**：第一阶段（设计层），切片验证（路径 B 转化）
> **前置条件**：DS01 + 定稿场景 S01 已产出
> **产出物**：搭子 PK 竞技域 Use-Case + 契约定义 → 输入给 Agentic Coding
>
> **转化说明**：由 D03 V1.3（§八 PK 竞技 API 8.1~8.7）API 契约拆分为 UC。**错误码决策**：保留 D03 数字域错误码（20xx PK / 60xx 搭子 / 10xx 全局）+ SNAKE_CASE 语义名双列（DG-06 附录「既有错误码体系迁移」）。
> **命名决策（对齐 D03 原则 #6）**：API JSON 字段一律 camelCase；SNAKE_CASE 仅用于错误码语义名（D03 未提供语义名，由本 U01 推导标注）。
> **跨模块依赖**：学习搭子（accepted 资格）、题库（出题）、AI 判题（判分）、排行榜（胜场）。

---

## 缺口决策声明（存量源未定义 → 本切片决策）

> 依据 DG-06 文档头缺口决策声明规则（切片验证第三轮固化，2026-09-06）。

| # | 缺口项 | 源状态 | 本切片决策 | 影响 UC |
|:-:|--------|-------|-----------|:-------:|
| 1 | forfeit/timeout 时的 Status | D02 有枚举无流转 | **置 Finished**（对齐 PkPlayerStats 视图过滤口径） | 9.5/9.6 |
| 2 | partial（部分正确）PK 计分 | D02/D01 未定义 | **Partial 计 0 分**（IsCorrect=false，判题契约 result=partial 不入 CorrectCount） | 9.3 |
| 3 | 掉线 30s 判定机制 | D03 仅语义（forfeit），无作业定义 | 事件驱动计时（对局 ongoing 中检测断线 → 30s 未重连置弃权）——Hangfire 兜底扫描 | 9.5 |
| 4 | InviteCode 有效期/复用 | D02/D03 未定义 | **对局 pending 期间有效**；对局结束失效；新对局生成新码 | 9.1/9.2 |
| 5 | AI 点评生成失败降级 | D02 未定义 | LLM 失败 → AiComment 留空 + 响应兜底文案"本次对战很精彩！" | 9.4 |
| 6 | PkAttempts 防重复 | D02 无 UNIQUE | **补 UNQ(MatchId,PlayerId,QuestionId)**（DS01 差异③） | 9.3 |
| 7 | 30s 轮询降级出处 | D03 未写间隔（仅 ADR-005 有） | **轮询间隔 30s**（出处 ADR-005，非 D03） | 9.3 |
| 8 | 战绩胜率 | PkPlayerStats 视图无胜率列 | **胜率 API 层计算**（Wins/TotalMatches） | 9.6 |
| 9 | 知识彩蛋落库 | D03 响应含 knowledgeEggs，D02 无表 | **响应态展示，不落库**（前端展示，结果页一次性） | 9.4 |

---

# 一、本域 UC 总览

| UC# | UC 名称 | 类型 | Service | 子域 | 事务 |
|:---:|---------|:----:|---------|:----:|:----:|
| 9.1 | 发起 PK（方式 A） | UI | CreatePkMatchService | Pk | CROSS |
| 9.2 | 加入 PK（方式 B） | UI | JoinPkMatchService | Pk | SINGLE |
| 9.3 | 对战答题 | UI | SubmitPkAnswerService | Pk | SINGLE |
| 9.4 | PK 结果 + AI 点评 | UI | GetPkResultService | Pk | SINGLE |
| 9.5 | 掉线弃权检测 | Job | PkForfeitDetectionJob | Pk | CROSS |
| 9.6 | PK 战绩 | UI | GetPkStatsService | Pk | SINGLE |

---

## UC-9.1：发起 PK（方式 A：选对手）

**对应场景**：S01-9.1 → 步骤 1~5
**对应 UI 图**：pk-lobby#1~5
**子域**：`Pk`
**事务范围**：CROSS（创建 PkMatches + PkPlayers + 校验搭子资格跨模块）
**事务策略说明**：对局与发起方参赛记录必须同生共死——对局已建但参赛记录缺失会造成结果无法归属
**故障容忍度**：Required
**权限要求**：`student`
**关联 R 功能点**：R01-9.1
**API 路由**：`POST /api/v1/pk/matches`

### [场景层]

#### Precondition
1. 已登录且角色为学生
2. 与对手互为 accepted 搭子（搭子 = PK 资格）

#### Main Flow
1. 学生选对手 + 学科/题库范围 + 题量（默认 10）→ 发起
2. 系统校验搭子资格 → 创建对局（Pending）+ 发起方参赛记录 → 生成 4 位对战码
3. 分享对战码给对手 → 等待确认

#### Alternative Flow
- 1a. 非搭子 → 6004/2005（BR-01）
- 1b. 题库不存在/参数非法 → 1501/1002（BR-02）
- 1c. 搭子已满（不影响 PK，但校验路径）→ 提示（BR-03）

#### Postcondition
- 数据：PkMatches（Pending）+ PkPlayers（发起方）已写入；InviteCode 生成
- 页面：对局待确认态 + 4 位对战码

### [契约层]

#### Service 类声明

位置：`Domain/Services/Pk/`

```csharp
[GenerateController]
[Transactional]
public class CreatePkMatchService : DomainServiceBase<TUserInfo>
{
    public async Task<CreatePkMatchResDto> ExecuteAsync(CreatePkMatchReqDto request) { throw new NotImplementedException(); }
}
```

#### Request DTO

位置：`Domain/Dtos/Pk/Request/`

| 属性 | 类型 | 必填 | 默认值 | 说明 |
|------|------|:---:|:-----:|------|
| OpponentUserId | string | ✅ | — | 对手 Uid（accepted 搭子） |
| BankId | string | ✅ | — | 题库业务键 |
| Topic | string? | — | — | 知识点/主题 |
| QuestionCount | int | — | 10 | 5/10/20 |
| Mode | string | — | "Sync" | Sync/Async |

#### Response DTO

位置：`Domain/Dtos/Pk/Response/`

| 属性 | 类型 | 说明 |
|------|------|------|
| MatchUid | string | 对局外部键 |
| InviteCode | string | 4 位对战码 |

#### 业务规则

| BR# | 规则说明 | 测试场景 |
|:---:|---------|---------|
| BR-01 | 与对手必须互为 accepted 搭子 → 6004/2005 | 非搭子 -> 2005 |
| BR-02 | 题库存在 + 参数合法 → 1501/1002 | 非法 bankId -> 1501 |
| BR-03 | 题量可选 5/10/20，默认 10 | 题量 15 -> 1002 |
| BR-04 | 创建对局即生成 InviteCode（4 位） | 创建 -> 返回 4 位码 |
| BR-05 | 对局 Pending + 发起方 PkPlayers 原子创建 | 任一步失败 -> 回滚 |

#### 错误码定义

| 错误码 | 语义名 | HTTP | 业务含义 | 触发条件 |
|:------:|:------:|:----:|---------|:--------:|
| 2005 | NOT_BUDDY_PK | 403 | 仅搭子（好友）之间可以 PK | BR-01 |
| 1501 | BANK_NOT_FOUND | 404 | 题库不存在 | BR-02 |
| 1002 | PARAM_INVALID | 400 | 参数错误 | BR-03 |
| 1001 | UNAUTHORIZED | 401 | 未登录/令牌失效 | Precondition |

#### 依赖的 DataService

```csharp
IPkMatchesDataService             // 对局创建（本域）
IPkPlayersDataService             // 参赛记录（本域）
IStudyBuddiesDataService          // 搭子资格校验（跨模块，搭子域）
IBanksDataService                 // 题库校验（跨模块，题库域）
```

#### Service 编排逻辑

| 步骤 | 容忍度 | 操作描述 | 涉及 DataService | 对应规则 |
|:----:|:------:|---------|:----------------:|:--------:|
| 1 | Required | 校验与对手互为 accepted 搭子 | `IStudyBuddiesDataService` | BR-01 |
| 2 | Required | 校验题库 + 参数 | `IBanksDataService` | BR-02/BR-03 |
| 3 | Required | 创建对局（Pending + InviteCode） | `IPkMatchesDataService` | BR-04 |
| 4 | Required | 创建发起方参赛记录 | `IPkPlayersDataService` | BR-05 |

---

## UC-9.2：加入 PK（方式 B：对战码）

**对应场景**：S01-9.2 → 步骤 1~3
**子域**：`Pk`
**事务范围**：SINGLE
**故障容忍度**：Required
**权限要求**：`student`
**关联 R 功能点**：R01-9.2
**API 路由**：`POST /api/v1/pk/matches/{matchId}/join`

### [场景层]

#### Precondition
1. 已登录且角色为学生
2. 与发起者互为 accepted 搭子

#### Main Flow
1. 学生输入 4 位对战码 / 点挑战确认
2. 系统校验对局 + 搭子关系 → 加入成功 → 返回对局配置

#### Alternative Flow
- 1a. 对局不存在 → 2001（BR-06）
- 1b. 对局已结束/不可加入 → 2002（BR-07）
- 1c. 参赛人数已满 → 2003（BR-08）
- 1d. 非搭子 → 2005（BR-09）

#### Postcondition
- 数据：PkPlayers（加入方）已写入；对局可开局
- 页面：就绪态 → 进入对战

### [契约层]

#### Service 类声明

```csharp
[GenerateController]
public class JoinPkMatchService : DomainServiceBase<TUserInfo>
{
    public async Task<JoinPkMatchResDto> ExecuteAsync(JoinPkMatchReqDto request) { throw new NotImplementedException(); }
}
```

#### Request DTO

| 属性 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| MatchUid | string | ✅ | 对局外部键 |
| InviteCode | string | ✅ | 4 位对战码 |

#### Response DTO

| 属性 | 类型 | 说明 |
|------|------|------|
| MatchUid | string | 对局外部键 |
| Mode | string | Sync/Async |
| QuestionCount | int | 题量 |
| PerQuestionTimeS | int | 每题时长（30） |

#### 业务规则

| BR# | 规则说明 | 测试场景 |
|:---:|---------|---------|
| BR-06 | 对局不存在 → 2001 | 非法 matchId -> 2001 |
| BR-07 | 对局已结束/不可加入 → 2002 | Finished 对局 -> 2002 |
| BR-08 | 参赛人数已满 → 2003 | 已满员 -> 2003 |
| BR-09 | 与发起者互为 accepted 搭子 → 2005 | 非搭子 -> 2005 |
| BR-10 | InviteCode 匹配校验 | 码不符 -> 2001/2003 |

#### 错误码定义

| 错误码 | 语义名 | HTTP | 业务含义 | 触发条件 |
|:------:|:------:|:----:|---------|:--------:|
| 2001 | MATCH_NOT_FOUND | 404 | 比赛不存在 | BR-06/BR-10 |
| 2002 | MATCH_CLOSED | 400 | 比赛已结束/不可加入 | BR-07 |
| 2003 | MATCH_FULL | 409 | 参赛人数已满 | BR-08 |
| 2005 | NOT_BUDDY_PK | 403 | 仅搭子（好友）之间可以 PK | BR-09 |
| 1001 | UNAUTHORIZED | 401 | 未登录/令牌失效 | Precondition |

#### 依赖的 DataService

```csharp
IPkMatchesDataService             // 对局查询/状态（本域）
IPkPlayersDataService             // 加入方参赛记录（本域）
IStudyBuddiesDataService          // 搭子资格（跨模块）
```

#### Service 编排逻辑

| 步骤 | 容忍度 | 操作描述 | 涉及 DataService | 对应规则 |
|:----:|:------:|---------|:----------------:|:--------:|
| 1 | Required | 查对局 + 对战码匹配 | `IPkMatchesDataService` | BR-06/BR-10 |
| 2 | Required | 校验状态（可加入）+ 人数 | `IPkMatchesDataService` | BR-07/BR-08 |
| 3 | Required | 校验与发起者互为 accepted 搭子 | `IStudyBuddiesDataService` | BR-09 |
| 4 | Required | 写入加入方参赛记录 | `IPkPlayersDataService` | — |

---

## UC-9.3：对战答题

**对应场景**：S01-9.3 → 步骤 1~4
**对应 UI 图**：pk-battle#1
**子域**：`Pk`
**事务范围**：SINGLE（写 PkAttempts + 更新 PkPlayers.Score）
**故障容忍度**：Required（判题降级除外）
**权限要求**：`student`（参赛者）
**关联 R 功能点**：R01-9.3
**API 路由**：`POST /api/v1/pk/matches/{matchId}/answer`

### [场景层]

#### Precondition
1. 已登录且为参赛者；对局 ongoing
2. 已取到当前题（同题同序）

#### Main Flow
1. 系统取当前题（同题同序）→ 30s 倒计时
2. 学生作答提交 → 判题引擎即时判定 → 答对 +10
3. 自动下一题；重复至题量结束

#### Alternative Flow
- 1a. 非参赛者 → 2004（BR-11）
- 2a. 答错/超时 → 0 分（BR-12）
- 2b. partial 判题 → 0 分（BR-13）
- 2c. 判题失败降级 → 规则判定 + 提示（BR-14）

#### Postcondition
- 数据：PkAttempts 写入；PkPlayers.Score/CorrectCount/TotalTimeMs 更新
- 页面：判题反馈（+10/0 分）→ 下一题

### [契约层]

#### Service 类声明

```csharp
[GenerateController]
public class SubmitPkAnswerService : DomainServiceBase<TUserInfo>
{
    public async Task<SubmitPkAnswerResDto> ExecuteAsync(SubmitPkAnswerReqDto request) { throw new NotImplementedException(); }
}
```

#### Request DTO

| 属性 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| MatchUid | string | ✅ | 对局外部键 |
| QuestionId | string | ✅ | 题目业务键 |
| UserAnswer | string | ✅ | 用户答案 |
| TimeCostMs | int | ✅ | **客户端上送**（PK 以用时决胜） |

#### Response DTO

| 属性 | 类型 | 说明 |
|------|------|------|
| IsCorrect | bool | 是否答对（含第四题） |
| Result | string | Correct/Partial/Wrong |
| Confidence | double? | 判题置信度 |
| Score | int | 本题得分（10/0） |

#### 业务规则

| BR# | 规则说明 | 测试场景 |
|:---:|---------|---------|
| BR-11 | 非参赛者 → 2004 | 非参赛 -> 2004 |
| BR-12 | 答对 +10；答错/超时 0 分 | Correct -> Score=10 |
| BR-13 | Partial 计 0 分（IsCorrect=false） | Partial -> Score=0 |
| BR-14 | 判题失败降级规则判定 + 提示 | LLM 超时 -> 本地规则 |
| BR-15 | 同题防重复（UNQ Match+Player+Question） | 重复提交 -> 幂等 |
| BR-16 | timeCostMs 客户端上送（不与主判题服务端计时混淆） | 客户端上送 -> 落库 |

#### 错误码定义

| 错误码 | 语义名 | HTTP | 业务含义 | 触发条件 |
|:------:|:------:|:----:|---------|:--------:|
| 2004 | NOT_PARTICIPANT | 403 | 非参赛者 | BR-11 |
| 1001 | UNAUTHORIZED | 401 | 未登录/令牌失效 | Precondition |
| 1006 | INTERNAL_ERROR | 500 | 判题服务不可用（已降级） | BR-14 |

#### 依赖的 DataService

```csharp
IPkMatchesDataService             // 对局状态校验（本域）
IPkPlayersDataService             // 分数更新（本域）
IPkAttemptsDataService            // 答题明细（本域）
// 判题引擎（跨模块，切片 03 JudgingEngineService 五键契约）
```

#### Service 编排逻辑

| 步骤 | 容忍度 | 操作描述 | 涉及 DataService | 对应规则 |
|:----:|:------:|---------|:----------------:|:--------:|
| 1 | Required | 校验参赛者 + 对局 ongoing | `IPkMatchesDataService` | BR-11 |
| 2 | Required | 防重复提交（UNQ） | `IPkAttemptsDataService` | BR-15 |
| 3 | Required | 调判题引擎（五键契约） | 判题引擎（跨模块） | BR-14 |
| 4 | Required | 按规则记分（Correct+10，Partial/错误 0） | — | BR-12/BR-13 |
| 5 | Required | 写 PkAttempts + 更新 PkPlayers 分数/用时 | `IPkAttemptsDataService`/`IPkPlayersDataService` | BR-16 |

> **跨模块依赖标注**：判题引擎为本切片消费方（切片 03 Callee UC-J.1），本 UC 仅调五键契约，判题逻辑不重复实现。

---

## UC-9.4：PK 结果 + AI 点评

**对应场景**：S01-9.4 → 步骤 1~4
**对应 UI 图**：pk-result#1
**子域**：`Pk`
**事务范围**：SINGLE
**故障容忍度**：Required
**权限要求**：`student`（参赛者）
**关联 R 功能点**：R01-9.5
**API 路由**：`GET /api/v1/pk/matches/{matchId}/result`

### [场景层]

#### Precondition
1. 已登录且为参赛者；对局已 finished

#### Main Flow
1. 系统判定胜负（WinnerId，NULL=平局）+ FinishReason 写入
2. 展示胜负 + 双方得分 + AI 趣味点评 + 知识彩蛋
3. 胜方庆祝动画；胜场计入战力榜（后台）

#### Alternative Flow
- 1a. 对局不存在 → 2001（BR-17）
- 1b. AI 点评生成失败 → AiComment 留空 + 兜底文案（BR-18）

#### Postcondition
- 数据：已 finished（结果由 9.5 判定写库后返回）
- 页面：结果页展示

### [契约层]

#### Service 类声明

```csharp
[GenerateController]
public class GetPkResultService : DomainServiceBase<TUserInfo>
{
    public async Task<GetPkResultResDto> ExecuteAsync(GetPkResultReqDto request) { throw new NotImplementedException(); }
}
```

#### Request DTO

| 属性 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| MatchUid | string | ✅ | 对局外部键 |

#### Response DTO

| 属性 | 类型 | 说明 |
|------|------|------|
| Status | string | Finished |
| WinnerId | string? | 胜方（NULL=平局） |
| FinishReason | string | Score/Forfeit/Timeout |
| WinReason | string? | 胜负说明（如"同分比用时"） |
| Players | PkPlayerResultDto[] | UserId/Nickname/Score/TotalTimeMs/AiComment |
| KnowledgeEggs | KnowledgeEggDto[] | 知识彩蛋（响应态不落库） |

#### 业务规则

| BR# | 规则说明 | 测试场景 |
|:---:|---------|---------|
| BR-17 | 对局不存在 → 2001 | 非法 matchId -> 2001 |
| BR-18 | AI 点评失败 → AiComment 留空 + 兜底文案 | LLM 失败 -> 兜底文案 |
| BR-19 | **合规：结果只展示★数/用时，不出现正确率对比榜** | 响应 -> 无对比榜字段 |
| BR-20 | 胜负由 WinnerId 判定（NULL=平局） | 同分 -> winnerId null |

#### 错误码定义

| 错误码 | 语义名 | HTTP | 业务含义 | 触发条件 |
|:------:|:------:|:----:|---------|:--------:|
| 2001 | MATCH_NOT_FOUND | 404 | 比赛不存在 | BR-17 |
| 1001 | UNAUTHORIZED | 401 | 未登录/令牌失效 | Precondition |

#### 依赖的 DataService

```csharp
IPkMatchesDataService             // 对局结果（本域）
IPkPlayersDataService             // 双方得分/点评（本域）
// AI 点评服务（跨模块，判题服务 LLM）
```

#### Service 编排逻辑

| 步骤 | 容忍度 | 操作描述 | 涉及 DataService | 对应规则 |
|:----:|:------:|---------|:----------------:|:--------:|
| 1 | Required | 查对局结果（WinnerId/FinishReason） | `IPkMatchesDataService` | BR-17/BR-20 |
| 2 | Required | 组装双方得分/用时/点评 | `IPkPlayersDataService` | BR-19 |
| 3 | Required | AI 点评失败兜底 | AI 点评服务（跨模块） | BR-18 |

---

## UC-9.5：掉线弃权检测（BackgroundJob）

**调度规则**：事件触发（检测到断线起 30s 计时）+ Hangfire 定时扫描兜底（每 30s）
**事务范围**：CROSS（更新对局状态 + 计分结算）
**故障容忍度**：Optional（单对局失败记录，不中断）
**关联 R 功能点**：R01-9.4

### Precondition（数据状态条件）
- 对局 ongoing；一方断线（WS 断开 / 轮询超时）

### Main Flow（系统流程，无用户操作）
1. 检测到玩家断线 → 启动 30s 弃权倒计时
2. 30s 未重连 → 判弃权（FinishReason=Forfeit，对方获胜）
3. 更新对局 Status=Finished + PkPlayers 结算

### Alternative Flow（数据异常路径）
- 1a. 断线方 30s 内重连 → 取消计时，进度保留（BR-21）
- 2a. 单对局处理失败 → 记录日志继续（BR-22）

### Postcondition
- 数据：PkMatches（Finished + Forfeit + WinnerId）更新；PkPlayers 结算

### [契约层]

#### Service 类声明

位置：`Domain/Services/Pk/`

```csharp
// 不标注 [GenerateController]（调度器触发）
// 不标注 [AuthorityFilter]（系统内部任务）
public class PkForfeitDetectionJob : DomainServiceBase<TUserInfo>
{
    public async Task ExecuteAsync(CancellationToken ct) { throw new NotImplementedException(); }
}
```

#### 业务规则

| BR# | 规则说明 | 测试场景 |
|:---:|---------|---------|
| BR-21 | 30s 内重连 → 取消弃权计时，进度保留 | 20s 重连 -> 对局继续 |
| BR-22 | 单对局失败不中断整批（幂等） | 处理异常 -> 跳过继续 |
| BR-23 | 30s 未重连 → FinishReason=Forfeit，对方胜 | 超 30s -> Forfeit + 对方 Win |

#### 错误码定义

| 错误码 | 语义名 | HTTP | 业务含义 | 触发条件 |
|:------:|:------:|:----:|---------|:--------:|
| 1006 | INTERNAL_ERROR | 500 | 批次处理失败（仅日志/告警） | BR-22 |

#### 依赖的 DataService

```csharp
IPkMatchesDataService             // 对局状态/弃权写库（本域）
IPkPlayersDataService             // 结算（本域）
```

#### 编排逻辑

| 步骤 | 容忍度 | 操作描述 | 涉及 DataService | 对应规则 |
|:----:|:------:|---------|:----------------:|:--------:|
| 1 | Required | 扫描断线倒计时中的对局 | `IPkMatchesDataService` | — |
| 2 | Required | 30s 内重连则取消；超时判弃权 | `IPkMatchesDataService` | BR-21/BR-23 |
| 3 | Required | 更新 Status=Finished + WinnerId + 结算 | `IPkMatchesDataService`/`IPkPlayersDataService` | BR-22 |

> **调度方式**：事件驱动 + Hangfire 定时兜底，不生成 Controller、不进服务契约一览表（模式列标注 `C`）。

---

## UC-9.6：PK 战绩

**对应场景**：S01-9.1 → 步骤 2（大厅"我的战绩"）
**子域**：`Pk`
**事务范围**：SINGLE
**故障容忍度**：Required
**权限要求**：`student`
**关联 R 功能点**：R01-9.6
**API 路由**：`GET /api/v1/pk/stats?userId=uuid`

### [场景层]

#### Precondition
1. 已登录且角色为学生

#### Main Flow
1. 学生查看 PK 大厅"我的战绩" → 系统按 PkPlayerStats 视图聚合返回

#### Alternative Flow
- 1a. 无对战记录 → 战绩零值（BR-24）

#### Postcondition
- 数据：无写入
- 页面：战绩展示（胜/负/平 + 胜率 + 累计分）

### [契约层]

#### Service 类声明

```csharp
[GenerateController]
public class GetPkStatsService : DomainServiceBase<TUserInfo>
{
    public async Task<GetPkStatsResDto> ExecuteAsync(GetPkStatsReqDto request) { throw new NotImplementedException(); }
}
```

#### Request DTO

| 属性 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| UserUid | string? | — | 用户（默认当前用户） |

#### Response DTO

| 属性 | 类型 | 说明 |
|------|------|------|
| TotalMatches | int | 总场数 |
| Wins | int | 胜 |
| Draws | int | 平 |
| WinRate | double | 胜率（API 层计算 Wins/TotalMatches） |
| TotalScore | int | 累计分 |

#### 业务规则

| BR# | 规则说明 | 测试场景 |
|:---:|---------|---------|
| BR-24 | 无记录正常返回零值 | 无对战 -> 全 0 |
| BR-25 | 仅当前用户（RLS） | 无法查他人战绩 |
| BR-26 | 胜率 API 层计算（视图无该列） | Wins=8/Total=12 -> 0.67 |
| BR-27 | 战绩不展示正确率对比榜（合规） | 响应 -> 无对比字段 |

#### 错误码定义

| 错误码 | 语义名 | HTTP | 业务含义 | 触发条件 |
|:------:|:------:|:----:|---------|:--------:|
| 1001 | UNAUTHORIZED | 401 | 未登录/令牌失效 | Precondition |

#### 依赖的 DataService

```csharp
IPkPlayerStatsViewService         // 战绩视图读取（本域，视图不建实体）
```

#### Service 编排逻辑

| 步骤 | 容忍度 | 操作描述 | 涉及 DataService | 对应规则 |
|:----:|:------:|---------|:----------------:|:--------:|
| 1 | Required | 读 PkPlayerStats 视图聚合 | `IPkPlayerStatsViewService` | BR-24/BR-25 |
| 2 | Required | 计算胜率（API 层） | — | BR-26 |
| 3 | Required | 组装响应（合规过滤） | — | BR-27 |

---

# 二、D03/D02 契约缺口附录（不伪造，显式决策）

| 缺口项 | 源状态 | 本切片决策 |
|--------|-------|-----------|
| forfeit/timeout 的 Status | D02 未定义 | 置 Finished（对齐视图过滤） |
| partial 判题 PK 计分 | D02/D01 未定义 | 计 0 分 |
| 掉线 30s 判定机制 | D03 仅语义无作业 | 事件驱动 + Hangfire 兜底 |
| InviteCode 有效期 | D02/D03 未定义 | Pending 期间有效，结束失效 |
| AI 点评失败降级 | D02 未定义 | AiComment 留空 + 兜底文案 |
| PkAttempts 防重复 | D02 无 UNIQUE | 补 UNQ(MatchId,PlayerId,QuestionId) |
| 30s 轮询降级出处 | 仅 ADR-005 | 轮询间隔 30s（出处标 ADR-005） |
| 战绩胜率 | 视图无胜率列 | API 层计算 |
| 知识彩蛋落库 | D02 无表 | 响应态不落库 |
| 对战码重复使用 | D03 未说明 | 对局结束即失效；新对局新码（复用 PkMatches 唯一性由对局隔离） |

---

# 三、横切关注点标注

| 关注点 | 实现方式 | 本域 UC 标注 |
|--------|---------|-------------|
| 认证鉴权 | `[AuthorityFilter]` + 角色枚举 | 全部 UI UC 权限要求 student；UC-9.5 Job 不标注 |
| 事务管理 | `[Transactional]` AOP | UC-9.1 CROSS（对局+参赛者原子）；UC-9.5 CROSS；其余 SINGLE |
| 数据权限（RLS） | DataService 注入当前用户 | 参赛者校验（BR-11）；战绩仅本人（BR-25） |
| 幂等性 | 业务级幂等 | PkAttempts 防重复（BR-15）；弃权处理幂等（BR-22） |
| 实时性 | SignalR + 轮询降级 | 对战同步（UC-9.3 标注实时通道；30s 轮询出处 ADR-005） |
| 限流 | Redis 计数 | 判题接口每题 ≥2s（PK 答题走判题引擎） |

---

> *文档版本：第一阶段 v1.0*
> *编制日期：2026-09-06*
> *同步版本：R01 v1.0 / S01 v1.0 / DS01 v1.0*