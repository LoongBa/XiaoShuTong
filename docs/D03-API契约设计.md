---
title: API 契约设计
version: V1.3
last_updated: 2026-09-05
status: 活跃
superseded_by:
---

# API 契约设计（D03）

> 本文档定义小书童前端（React + shadcn/ui，H5/PC）与后端（ASP.NET Core Web API）之间的 RESTful API 契约：认证、题库、学习、群组任务、统计、PK、搭子社交、排行榜、家长订阅与报告、判题反馈、用户数据、内测邀请**十二大域**。
> 关联文档：`docs/D01-统一题库与多学科学习平台设计.md`（框架 V1.2.1）、`docs/D02-学习数据与记忆跟踪设计.md`（数据层 V1.4）、`docs/1-需求分析/V1/“小书童（背书搭子）”产品需求分析和设计-V3.md`（需求 V3.1.3）、`docs/adr/ADR-007-技术栈分端调整-React统一.md`

---

## 一、总体设计原则

| # | 原则 | 说明 |
|---|------|------|
| 1 | **RESTful** | 资源式路径 + HTTP 方法；仅 JSON 交换 |
| 2 | **统一响应** | 所有接口返回 `{code, message, data}` 三字段 |
| 3 | **错误码分级** | 全局错误码（10xx）+ 域错误码（题库 15xx / PK 20xx / 学习 30xx / 统计 40xx / 群组 50xx / 内测邀请 52xx / 任务 51xx / 搭子 60xx / 排行榜 70xx / 订阅 80xx / 判题反馈 90xx） |
| 4 | **版本控制** | URL 前缀 `/api/v1/`，破坏性变更升 v2 |
| 5 | **鉴权统一** | 除登录/激活外全部需携带 `Authorization: Bearer <token>` |
| 6 | **命名规范（对齐 .NET）** | DB 表/列、Entity/DTO 一律大写驼峰（D02 V1.4）；**API JSON 字段由框架（System.Text.Json）自动序列化为 camelCase**——本文档示例一律展示 camelCase（`userId`/`questionId`），不写 snake_case |
| 7 | **判题契约对齐** | 判题相关接口输出 D01 统一契约 `result/confidence/matchedKeywords/missingKeywords/hint` |
| 8 | **防爬对齐 V3** | 题库内容"背一题取一题"，不返回明文列表 |
| 9 | **限流** | 单用户 60 次/分，单 IP 限流（V3 非功能需求） |

---

## 二、认证与鉴权

### 2.1 微信授权登录

```
POST /api/v1/auth/wechat-login
```

**请求**：
```json
{
  "code": "微信登录 code",
  "nickname": "可选",
  "avatarUrl": "可选"
}
```

**响应**：
```json
{
  "code": 0,
  "message": "success",
  "data": {
    "accessToken": "JWT",
    "refreshToken": "JWT",
    "expiresIn": 7200,
    "userId": "uuid",
    "needBindPhone": false
  }
}
```

**流程**：微信 code → 后端换 openid → 查已有用户（uuid）→ 无则创建 → 签发 JWT（含 userId/role）。**内测期：新用户激活走一次性邀请码（§6.12），此处仅完成微信身份建立。**

### 2.2 手机号绑定（可选，内测后启用）

```
POST /api/v1/auth/bind-phone
```

**请求**：`{ "phone": "138****1234", "smsCode": "123456" }`
**响应**：`{ "code": 0, "data": { "bound": true } }`

> 可选绑定（V3 决策 1）；手机号存储脱敏（138****1234）。**内测期：手机号不绑定账户**（仅作一次性邀请码名单定向，见 §6.12），本接口内测后启用。

### 2.3 令牌刷新

```
POST /api/v1/auth/refresh
```
**请求**：`{ "refreshToken": "..." }` → **响应**：新 accessToken + refreshToken。

### 2.4 鉴权中间件

- 所有 `/api/v1/*` 接口（除 auth、§6.12 激活）校验 Bearer token
- 无效/过期 → `{code: 1001, message: "未登录或令牌已过期"}`
- 未成年人（<14 岁）接口额外校验监护人同意标记

---

## 三、统一响应与错误码

### 3.1 统一响应结构

```json
{
  "code": 0,
  "message": "success",
  "data": {}
}
```

### 3.2 全局错误码（10xx）

| code | 含义 |
|------|------|
| 0 | 成功 |
| 1001 | 未登录 / 令牌失效 |
| 1002 | 参数错误（附带字段校验信息） |
| 1003 | 资源不存在（题库/题目/用户/群组） |
| 1004 | 无权限（私域题库非 owner） |
| 1005 | 请求过于频繁（限流） |
| 1006 | 内部错误（判题服务不可用等） |

### 3.3 域错误码

| 域 | code | 含义 |
|----|------|------|
| 题库 | 1501 | 题库不存在 |
| | 1502 | 题目不属于该题库 |
| PK | 2001 | 比赛不存在 |
| | 2002 | 比赛已结束/不可加入 |
| | 2003 | 参赛人数已满 |
| | 2004 | 非参赛者 |
| | 2005 | 仅搭子（好友）之间可以 PK |
| 学习 | 3001 | 会话不存在 |
| | 3002 | 答案格式错误 |
| 统计 | 4001 | 无统计数据 |
| 群组 | 5001 | 群组不存在 |
| | 5002 | 重复加入/已在群组 |
| | 5003 | 非群组成员/无权限 |
| 内测邀请 | 5201 | 内测邀请码无效/已使用/已过期 |
| | 5202 | 一次性邀请码无效/已使用/已过期 |
| | 5203 | 手机号后四位不匹配 |
| | 5204 | 名单批次不存在/未就绪 |
| | 5205 | 内测未开放 |
| 任务 | 5101 | 任务不存在 |
| | 5102 | 任务已截止/关闭 |
| 搭子 | 6001 | 搭子邀请不存在 |
| | 6002 | 邀请已过期/已处理 |
| | 6003 | 搭子数量已达上限（5 个） |
| | 6004 | 非搭子关系（无法互看排名/发起 PK） |
| | 6005 | 单日发出邀请超限（≤10 次） |
| | 6006 | 仅限同群组/同年级用户（未成年人保护） |
| 排行榜 | 7001 | 该群组战绩榜已关闭（RankEnabled=false） |
| 订阅 | 8001 | 未订阅/无权限查看完整报告 |
| | 8002 | 家长-孩子未建立授权关系 |
| | 8003 | 试用已过期 |
| 判题反馈 | 9001 | 反馈记录不存在 |

---

## 四、题库域 API

### 4.1 题库列表

```
GET /api/v1/banks?subject=chinese&purpose=memorize&page=1&size=20
```

**响应**：
```json
{
  "code": 0,
  "data": {
    "items": [
      {
        "bankId": "chinese-7to9-pep",
        "subject": "chinese",
        "name": "七-九年级-统编教材",
        "version": "V1.0",
        "topicCount": 12,
        "questionCount": 435
      }
    ],
    "total": 1
  }
}
```

### 4.2 题库详情

```
GET /api/v1/banks/{bankId}
```
返回 bank.v1.json 元数据 + 知识点层级（topic 树）。

### 4.3 创建题库（私域）

```
POST /api/v1/banks
```
**请求**：`{ "name", "subject", "privacy": "private", "description" }`
**响应**：新建 bankId + 元数据。

### 4.4 导入题目（txt/JSON）

```
POST /api/v1/banks/{bankId}/import
Content-Type: multipart/form-data
```
**字段**：`file`（.txt 或 .json）、`topic`（目标章节）
**响应**：`{ "imported": 120, "failed": 3, "failures": ["行 45: 格式错误"] }`

### 4.5 取题（防爬：背一题取一题）

```
GET /api/v1/questions/next?bankId=xxx&type=R1&knowledgePoint=观沧海&sessionId=uuid
```

**响应**：
```json
{
  "code": 0,
  "data": {
    "questionId": "Q-ch-7a-0001",
    "type": "R1",
    "content": { "question": "日月之行" },
    "knowledgePoint": "观沧海",
    "knowledgeCardId": "KC-ch-观沧海"
  }
}
```

> **不含答案**！答案与 keywords 仅在下述判题接口服务端侧校验。`sessionId` 关联当前学习会话（D02 StudySessions），用于按状态机出题（到期复习优先 → 未掌握优先 → 新题）。**任务会话**（session 带 taskId）时，出题范围为 `Tasks.QuestionIds`（jsonb）内按同一状态机规则排序。

### 4.6 知识卡片

```
GET /api/v1/questions/{questionId}/card
```
**响应**：知识卡片字段（authorCard/wordCard/eventCard 等，按题库模板）。

---

## 五、学习域 API

### 5.1 开始学习会话

```
POST /api/v1/study/sessions
```

**请求**：
```json
{
  "scenario": "memorize",
  "bankId": "chinese-7to9-pep",
  "sessionType": "分阶",
  "taskId": "uuid"
}
```
- `taskId`：**可选**。群组任务执行时必传——服务端创建 StudySessions（TaskId 关联）+ TaskAssignments 状态推进（in_progress/sessionId），Attempts 经 SessionId→TaskId 链式归属
- 个人自愿背书：taskId 省略（NULL）

**响应**：`{ "sessionId": "uuid", "questionCount": 20 }`

### 5.2 提交作答（核心：判题接口）

```
POST /api/v1/study/attempts
```

**请求**：
```json
{
  "sessionId": "uuid",
  "questionId": "Q-ch-7a-0001",
  "userAnswer": "若出其中",
  "hintLevel": "none"
}
```

**Attempts 落库字段派生说明**（对齐 D02 V1.4 §4.1 Attempts 表 NOT NULL 约束）：

| 落库字段 | 派生来源 |
|---------|---------|
| `questionId` / `qtype` / `bankId` | 由 `questionId` 服务端查题目获得（客户端不上送，防篡改） |
| `scenario` | 由 `sessionId` 会话获得（session 创建时定为 memorize/assess/play_pk/play_daily） |
| `preState` / `postState` | 判题后状态机迁移结果（D01 §10.2 迁移矩阵） |
| `result` / `confidence` | 判题引擎输出（判题契约前两项） |
| `hintLevel` | 请求原样透传 |
| `timeCostMs` | 服务端自请求到达至判题完成计时，由中间件写入（无需客户端上送） |

**响应**（D01 判题契约，camelCase）：
```json
{
  "code": 0,
  "data": {
    "result": "correct",
    "confidence": 0.98,
    "matchedKeywords": ["若出其中"],
    "missingKeywords": [],
    "hint": "",
    "preState": 1,
    "postState": 2,
    "nextReviewAt": "2026-09-05T08:00:00Z"
  }
}
```

**状态值映射**（`preState`/`postState`）：`0=✕ 未掌握`、`1=△ 模糊`、`2=○ 掌握`、`3=★ 熟练`（与 D02 V1.4 一致）。前端据此渲染四阶记忆状态。

> 服务端流程：鉴权 → 查题目（校验归属）→ 判题路由（rule/keyword/LLM）→ 更新 MemoryStates → 写 Attempts → 同步 DailyStats/WrongQuestions/TaskAssignments.Progress → 返回迁移结果。**答案永不下发客户端**。`hint` 字段在 hintLevel=none 且答对时返回空串；hintLevel=partial 时必给提示。
> **任务执行**：任务会话的每次作答同步推进 TaskAssignments.Progress；全部完成 → Status=completed。

### 5.3 请求提示（背景钩子）

```
POST /api/v1/study/hint
```
**请求**：`{ "questionId": "Q-ch-7a-0001", "difficultySlot": "S2" }`（可空，按状态路由）
**响应**：
```json
{
  "code": 0,
  "data": {
    "hint": "月亮这封信使要把你的心送到哪？——往西边",
    "difficultySlot": "S2",
    "hintSource": "记忆钩子"
  }
}
```

### 5.4 复习队列

```
GET /api/v1/study/review-queue?date=2026-09-05&limit=30
```
**响应**：到期题目列表（questionId/state/nextReviewAt），不含答案。

### 5.5 记忆状态

```
GET /api/v1/study/memory-states?bankId=xxx&state=1&page=1
```
**响应**：题目 id + 状态 + 正确率 + nextReviewAt。

### 5.6 会话结果（单次背诵总结）

```
GET /api/v1/study/sessions/{sessionId}/result
```
**响应**：
```json
{
  "code": 0,
  "data": {
    "correctCount": 8,
    "totalCount": 10,
    "newStarCount": 3,
    "blockedPoints": [
      { "questionId": "Q-ch-7a-0002", "knowledgePoint": "滕王阁序·落霞句", "state": 0 },
      { "questionId": "Q-ch-7a-0003", "knowledgePoint": "蜀道难·开头", "state": 1 }
    ]
  }
}
```
> 对齐会话结果页："这次背对了 8/10" + "新增 3 颗★" + "下次重点复习"列表。

---

## 六、群组与任务域 API

> 对齐《产品战略 V1》群组任务场景（V1.4 淡化"班"）+ D02 V1.4 Groups/GroupMembers/ParentStudentRelations/Tasks/TaskAssignments。群主（原老师/班主任）= 群组 Owner。

### 6.1 创建群组（群主，凭内测邀请码激活）

```
POST /api/v1/groups
```
**请求**：
```json
{
  "betaCode": "内测邀请码",
  "name": "七(3)班",
  "subject": "history",
  "grade": "七年级"
}
```
**响应**：`{ "groupId": "uuid" }`
> `betaCode` 为平台发放的内测邀请码（D02 §4.16 BetaInviteCodes）：校验未用/未过期（5201）→ 创建 Groups（OwnerId=当前登录群主）→ 码标记 used。一个内测码只能激活一个群组。

### 6.2 我的群组列表

```
GET /api/v1/groups
```
**响应**：`{ "items": [{ "groupId", "name", "subject", "grade", "memberCount", "executionRate", "rankEnabled" }] }`

### 6.3 群组详情 / 成员管理（群主）

```
GET    /api/v1/groups/{groupId}
GET    /api/v1/groups/{groupId}/members
DELETE /api/v1/groups/{groupId}/members/{userId}
```
- members 响应含 role/nickname/joinedAt/（学生）任务完成度
- 移除成员：二次确认语义由前端承载；不删学习数据

### 6.4 群组排名开关（群主，战绩榜控制）

```
PATCH /api/v1/groups/{groupId}/rank-enabled
```
**请求**：`{ "rankEnabled": false }`（默认 true）
**响应**：`{ "groupId", "rankEnabled": false }`
> 关闭后该群成员端战绩榜入口隐藏、榜单不展示（战力榜不受影响）。

### 6.5 布置任务（群主）

```
POST /api/v1/tasks
```
**请求**：
```json
{
  "groupId": "uuid",
  "bankId": "chinese-7to9-pep",
  "title": "背诵《人文地理·第一章》",
  "description": "本周任务",
  "questionIds": ["Q-his-0001", "Q-his-0002"],
  "questionCount": 10,
  "scenario": "memorize",
  "sessionType": "分阶",
  "deadlineAt": "2026-09-07T18:00:00+08:00"
}
```
**响应**：`{ "taskId": "uuid" }`
> 发布后自动为群组每个学生创建 TaskAssignments（pending）。

### 6.6 任务列表 / 详情（群主 + 成员视图）

```
GET /api/v1/tasks?groupId=uuid&status=active
GET /api/v1/tasks/{taskId}
```
**详情响应**（群主视角，含全群执行进度）：
```json
{
  "code": 0,
  "data": {
    "taskId": "uuid",
    "title": "背诵《人文地理·第一章》",
    "deadlineAt": "2026-09-07T18:00:00+08:00",
    "status": "active",
    "questionCount": 10,
    "assignments": [
      { "userId": "uuid", "nickname": "小明", "status": "completed", "progress": 100, "completedAt": "..." },
      { "userId": "uuid", "nickname": "小红", "status": "in_progress", "progress": 40 }
    ],
    "stuckPoints": ["王安石变法", "五代十国"]
  }
}
```

### 6.7 群主执行看板

```
GET /api/v1/owner/dashboard?groupId=uuid
```
**响应**：
```json
{
  "code": 0,
  "data": {
    "todayExecutionRate": 0.78,
    "avgProgress": 0.42,
    "overdueCount": 2,
    "weakPointsTop5": ["王安石变法", "五代十国", "郑和下西洋", "贞观之治", "科举制"],
    "taskList": [
      { "taskId": "uuid", "title": "背诵《人文地理》", "completionRate": 0.78, "status": "normal" },
      { "taskId": "uuid", "title": "背诵《蜀道难》", "completionRate": 0.42, "status": "overdue" }
    ]
  }
}
```

### 6.8 家长关联孩子

```
POST /api/v1/parents/relations
```
**请求**：`{ "studentId": "uuid", "relation": "parent" }`
**响应**：`{ "relationId": "uuid" }`
> 关联后家长方可订阅与查看该孩子报告；须校验孩子角色为学生。

### 6.9 我的孩子列表

```
GET /api/v1/parents/children
```
**响应**：`{ "items": [{ "studentId", "nickname", "className", "hasSubscription": false }] }`

---

## 六·补、内测邀请域 API（V1.3 新增）

> 对齐 PRD V3.1.3 内测邀请机制 + D02 V1.4 §四·补3（BetaInviteCodes / OneTimeInviteCodes / RosterImports）。核心链路：**平台发放内测码 → 群主激活建群 → 导入名单（Agent 整理）→ 生成一次性码 → 导出 CSV → 成员凭码+微信激活**。

### 6.10 名单导入（群主：批量粘贴 / 文件）

```
POST /api/v1/groups/{groupId}/roster/import
```
**请求**（multipart/form-data 或 JSON）：
```json
{
  "method": "paste",
  "rawText": "13800001111\n13800002222\n13800003333"
}
```
（file 方式：上传 .txt/.csv，字段 `file`）

**响应**：
```json
{
  "code": 0,
  "data": {
    "importId": "uuid",
    "status": "processing",
    "sourceCount": 30
  }
}
```
> 导入后异步触发 **Agent 整理**（去重/手机号格式校验/非法剔除），落 RosterImports（SourceCount/CleanedCount/DuplicateCount/InvalidCount）。

### 6.11 名单整理预览（轮询至 ready）

```
GET /api/v1/groups/{groupId}/roster/{importId}
```
**响应**：
```json
{
  "code": 0,
  "data": {
    "importId": "uuid",
    "status": "ready",
    "sourceCount": 30,
    "cleanedCount": 28,
    "duplicateCount": 1,
    "invalidCount": 1,
    "preview": ["1380***1111", "1380***2222"]
  }
}
```
> 手机号**脱敏展示**（前缀+后四位）；未就绪（processing）时前端骨架屏轮询，就绪后可确认生成。

### 6.12 生成一次性邀请码（群主确认后）

```
POST /api/v1/groups/{groupId}/roster/{importId}/generate
```
**请求**：`{ "confirm": true }`
**响应**：`{ "generatedCount": 28 }`
> 为名单每个有效手机号生成一个一次性邀请码（OneTimeInviteCodes：Code 8 位 + PhoneLast4 后四位 + ExpiresAt 默认 30 天）；同一名单内码↔手机号唯一。

### 6.13 导出 CSV（群主下载分发）

```
GET /api/v1/groups/{groupId}/roster/{importId}/export
```
**响应**：
```json
{
  "code": 0,
  "data": {
    "csvFileUrl": "https://oss.xxx/roster-export/xxx.csv",
    "expiresAt": "2026-09-12T00:00:00+08:00",
    "columns": ["phone_last4", "invite_code"]
  }
}
```
> CSV 仅含**手机号后四位 + 一次性邀请码**（不含 openid/完整手机号/学习数据）；OSS 存 7 天过期（对齐 D02 V1.4 §4.18 / D04 生命周期）。

### 6.14 成员激活（一次性码 + 微信登录）

```
POST /api/v1/beta/activate
```
**请求**：
```json
{
  "oneTimeCode": "AB12CD34",
  "phoneLast4": "1111"
}
```
**响应**：
```json
{
  "code": 0,
  "data": {
    "groupId": "uuid",
    "groupName": "七(3)班",
    "role": "student",
    "memberId": "uuid"
  }
}
```
> **流程**：微信已登录（OpenId 已建 Users 行）→ 校验码未用/未过期（5202）→ 校验手机号后四位与名单一致（5203）→ 绑定微信 ID（OneTimeInviteCodes.BoundUserId = 当前 UserId）→ 写入 GroupMembers（Role 由群主名单预设 student/parent，InviteCodeId 溯源）→ 码标记 used。**账户级手机号补绑定留后续**（内测后 bind-phone）。
> 幂等：同一码重复激活返回原结果；码已使用 → 5202。

---

## 七、统计域 API

### 7.1 记忆热力图

```
GET /api/v1/stats/heatmap?start=2026-08-01&end=2026-09-04
```
**响应**：
```json
{
  "code": 0,
  "data": {
    "days": [
      { "date": "2026-08-15", "starredCount": 12, "learnedCount": 30 }
    ]
  }
}
```

### 7.2 连续天数

```
GET /api/v1/stats/streak
```
**响应**：`{ "currentStreak": 5, "longestStreak": 12 }`

### 7.3 学习报告（周/月）

```
GET /api/v1/stats/report?period=week
```
**响应**：
```json
{
  "code": 0,
  "data": {
    "learnedCount": 210,
    "accuracy": 0.87,
    "starredCount": 45,
    "weakPoints": [{ "knowledgePoint": "宋朝人物", "accuracy": 0.55 }]
  }
}
```

### 7.4 错题本

```
GET /api/v1/stats/wrong-questions?mastered=false&page=1
```
**响应**：错题列表（questionId/题目摘要/bankId/subject/wrongCount/lastWrongAt），含"重新练习"入口。支持按 subject 过滤（跨学科错题分类）。连续答对 2 次 → mastered=true 移入"已掌握"分组。

---

## 八、PK 竞技 API（限定搭子 + InviteCode + FinishReason）

### 8.1 发起 PK（仅限 accepted 搭子）

```
POST /api/v1/pk/matches
```

**请求**：
```json
{
  "opponentUserId": "uuid",
  "bankId": "chinese-7to9-pep",
  "topic": "唐朝历史",
  "questionCount": 10,
  "mode": "sync"
}
```
- `opponentUserId` 必须为**互为 accepted 的搭子**（否则 6004/2005）
- 服务端校验后创建 PkMatches（Status=pending）+ 生成 `inviteCode`（4 位）

**响应**：`{ "matchId": "uuid", "inviteCode": "ABC1" }`

### 8.2 加入 PK（输入对战码 / 接受挑战）

```
POST /api/v1/pk/matches/{matchId}/join
```
**请求**：`{ "inviteCode": "ABC1" }`
**响应**：`{ "matchId", "mode": "sync", "questionCount": 10, "perQuestionTimeS": 30 }`
> 加入者亦须与发起者为 accepted 搭子（未成年人保护 + 搭子 PK 限定）。

### 8.3 取当前题（同步逐题 / 异步整组）

```
GET /api/v1/pk/matches/{matchId}/question?index=0
```
**响应**：`{ "questionId", "content": { "question" }, "index": 0, "timeLeftS": 30 }`（不含答案）

### 8.4 提交 PK 答案

```
POST /api/v1/pk/matches/{matchId}/answer
```
**请求**：`{ "questionId", "userAnswer", "timeCostMs" }`
**响应**：`{ "isCorrect": true, "result": "correct", "confidence": 0.95, "score": 10 }`

> PK 答题走判题引擎，输出与 D01 判题契约一致（含 confidence）；PK 数据 state_coupling=isolated，不入 Attempts/MemoryStates。**`timeCostMs` 由客户端上送**（PK 需以用时决胜），与 5.2 主判题接口的"服务端中间件计数"不同。

### 8.5 PK 结果 + AI 点评

```
GET /api/v1/pk/matches/{matchId}/result
```
**响应**：
```json
{
  "code": 0,
  "data": {
    "status": "finished",
    "winnerId": "uuid",
    "finishReason": "score",
    "winReason": "同分比用时，小红更快",
    "players": [
      { "userId": "uuid", "nickname": "小明", "score": 70, "totalTimeMs": 210000, "aiComment": "唐朝满分选手，宋朝断网……" }
    ],
    "knowledgeEggs": [
      { "questionId": "Q-his-0003", "egg": "王安石变法小科普……" }
    ]
  }
}
```
> `finishReason`：score（正常比分）/ forfeit（对手离线 30s 判弃权）/ timeout（超时结束）。胜负由 `PkMatches.WinnerId` 判定（NULL=平局）。

### 8.6 PK 战绩

```
GET /api/v1/pk/stats?userId=uuid
```
**响应**：`{ "totalMatches": 12, "wins": 8, "draws": 1, "winRate": 0.67, "totalScore": 860 }`

### 8.7 实时同步（WebSocket/SignalR）

```
WS /api/v1/pk/live?matchId=xxx&token=...
```
- 同步模式：双方实时状态推送（当前题/倒计时/对方进度）
- 微信 H5 不支持 WS 时自动降级异步模式（D01 8.4.1），HTTP 轮询 `/api/v1/pk/matches/{id}/status`

---

## 九、搭子社交域 API

> 对齐 V3.1 决策 #7（双向同意制）+ D02 V1.4 StudyBuddies（Status: pending/accepted/rejected/removed/expired）。

### 9.1 发起搭子邀请

```
POST /api/v1/buddies/invite
```
**请求**：`{ "inviteeUserId": "uuid" }`
**响应**：`{ "inviteId": "uuid", "expiresAt": "2026-09-12T00:00:00+08:00" }`
> 校验：同群组/同年级（6006）、单日 ≤10 次（6005）、accepted ≤5（6003）、非重复邀请。`ExpiresAt` = +7 天。

### 9.2 同意 / 拒绝邀请

```
POST /api/v1/buddies/{inviteId}/accept
POST /api/v1/buddies/{inviteId}/reject
```
**响应**：`{ "buddyId": "uuid" }`（accept）；`{ "code": 0 }`（reject）
> accept 时事务内校验双方 accepted 数量 ≤5（FOR UPDATE 防并发超限）。

### 9.3 搭子列表（含排名互看）

```
GET /api/v1/buddies
```
**响应**：
```json
{
  "code": 0,
  "data": {
    "items": [
      {
        "buddyId": "uuid",
        "userId": "uuid",
        "nickname": "小红",
        "avatarUrl": "",
        "streakDays": 12,
        "rank": { "scopeType": "group", "scopeId": "uuid", "rank": 3, "metricValue": 12, "trend": "up" }
      }
    ]
  }
}
```
> `rank` = 对方最新 RankSnapshots 快照（仅排名与数值，不暴露答题明细）；`trend`：up/down/flat → ↑绿/↓红/→灰。

### 9.4 解除搭子

```
DELETE /api/v1/buddies/{buddyId}
```
**响应**：`{ "code": 0 }`（Status → removed，历史 PK/答题保留）

### 9.5 查看搭子排名详情

```
GET /api/v1/buddies/{buddyId}/rank?scopeType=group&scopeId=uuid&subject=all
```
**响应**：`{ "rank": 3, "metricValue": 12, "trend": "up", "snapshotDate": "2026-09-05" }`

---

## 十、排行榜域 API

> 对齐 V3.1 决策 #6 + D02 V1.4 RankSnapshots（每日冻结快照）。数据由 Hangfire 每日 0:00(UTC+8) 批量写入，接口只读。

### 10.1 榜单查询

```
GET /api/v1/rankings?scopeType=group&scopeId=uuid&subject=all&metric=combat&date=2026-09-05&limit=50
```

| 参数 | 说明 |
|------|------|
| `scopeType` | group（群组）/ grade（年级，scopeId 传年级 key） |
| `scopeId` | 群组 id 或年级 key |
| `subject` | all/语文/数学/英语/物理/化学/生物/历史/地理/政治（排行榜学科筛选） |
| `metric` | `combat`（战力榜：streak+volume+pkWins）/ `performance`（战绩榜：accuracy+mastery） |
| `date` | 缺省 = 最新快照日期 |

**响应**：
```json
{
  "code": 0,
  "data": {
    "snapshotDate": "2026-09-05",
    "rankEnabled": true,
    "items": [
      { "rank": 1, "userId": "uuid", "nickname": "小明", "avatarUrl": "", "value": 45, "trend": "up", "isMe": false }
    ]
  }
}
```
> 战绩榜（metric=performance）时服务端校验 `Groups.RankEnabled`，false → 7001 且 `rankEnabled=false`（前端隐藏入口）。

### 10.2 我的排名 + 趋势（个人中心/首页）

```
GET /api/v1/rankings/me?scopeType=group&scopeId=uuid&subject=all&metric=combat
```
**响应**：`{ "rank": 15, "value": 20, "trend": "up", "rankChange": 3, "rankEnabled": true }`
> `rankChange` = 今日 rank − 昨日 rank（正=上升，由两份冻结快照对比，不回溯）。

### 10.3 群组开关读取（成员端）

```
GET /api/v1/groups/{groupId}/rank-enabled
```
**响应**：`{ "groupId": "uuid", "rankEnabled": true }`

---

## 十一、家长订阅与报告域 API

> 对齐 V3.1 决策 #8 + D02 V1.4 Subscriptions/ParentStudentRelations。付费锚点：家长为"知情权"付费（10 元/月 / 100 元/年），学生/群主端零付费门槛。

### 11.1 开通试用（7 天）

```
POST /api/v1/subscriptions/trial
```
**请求**：`{ "studentId": "uuid" }`
**响应**：`{ "subscriptionId": "uuid", "status": "trialing", "trialEndAt": "2026-09-12T00:00:00+08:00" }`
> 须已在 ParentStudentRelations 授权（8002）；按 (parent, student) 对独立（多孩子各自试用）。

### 11.2 订阅（微信支付成功回调）

```
POST /api/v1/subscriptions
```
**请求**：`{ "studentId": "uuid", "plan": "month" | "year" }`
**响应**：`{ "subscriptionId", "status": "active", "plan": "month", "periodEndAt": "2026-10-05T00:00:00+08:00" }`

### 11.3 我的订阅列表（多孩子切换）

```
GET /api/v1/subscriptions
```
**响应**：`{ "items": [{ "subscriptionId", "studentId", "studentNickname", "plan", "status", "trialEndAt", "periodEndAt" }] }`

### 11.4 取消续费 / 退订

```
DELETE /api/v1/subscriptions/{subscriptionId}
```
**响应**：`{ "code": 0 }`（Status → cancelled，权益至周期末）

### 11.5 成长总览（付费报告）

```
GET /api/v1/parent/report/dashboard?studentId=uuid
```
**响应**：
```json
{
  "code": 0,
  "data": {
    "subscription": { "status": "trialing", "trialEndAt": "..." },
    "todayCompleted": true,
    "streakDays": 38,
    "weekProgress": { "done": 5, "total": 10 },
    "subjectsMastery": [
      { "subject": "chinese", "stars": 2, "status": "mastered" },
      { "subject": "history", "status": "consolidating" }
    ]
  }
}
```
> 未订阅：data 返回前 2 项预览 + `locked: true`；完整内容 → 8001。**报告只呈现相对进步，不显示群组正确率排名（合规红线）**。

### 11.6 进度趋势（周/月切换）

```
GET /api/v1/parent/report/progress?studentId=uuid&period=week
```
**响应**：`{ "trend": [{ "date": "2026-09-05", "accuracy": 0.87, "learnedCount": 30 }], "vsLastWeek": { "learnedDelta": 3, "weaknessShift": "唐朝→五代十国" } }`

### 11.7 薄弱知识点（可下钻篇目）

```
GET /api/v1/parent/report/weakness?studentId=uuid
```
**响应**：`{ "weakPoints": [{ "subject": "history", "knowledgePoint": "五代十国", "accuracy": 0.55, "state": 1 }] }`

---

## 十二、判题反馈域 API

> 对齐 PRD §2.1"判错了"+ D02 V1.4 JudgmentFeedback。

### 12.1 提交判错反馈

```
POST /api/v1/judgment/feedback
```
**请求**：`{ "attemptId": "uuid", "feedbackType": "wrong_judgment" }`
**响应**：`{ "feedbackId": "uuid", "status": "pending" }`
> 反馈进入人工复核队列，计入判题质量统计。

---

## 十三、用户数据 API

### 13.1 数据导出

```
GET /api/v1/user/data/export
```
**响应**：JSON 打包全部学习数据（Attempts/MemoryStates/DailyStats/Pk*/StudyBuddies/RankSnapshots/Subscriptions/JudgmentFeedback/GroupMembers），供下载。

### 13.2 账号注销

```
POST /api/v1/user/data/delete
```
**响应**：`{ "code": 0, "data": { "scheduled": true, "purgeAt": "2026-10-04T00:00:00Z" } }`

> 30 天宽限期后级联删除（V3 决策：注销 30 天全清）。

---

## 十四、限流与安全

| 措施 | 配置 |
|------|------|
| 单用户限流 | 60 次/分（Redis 计数） |
| 单 IP 限流 | 100 次/分 |
| 判题接口 | 每题 ≥2s 间隔（防刷） |
| 搭子邀请 | 单日 ≤10 次（Redis `buddy_invite_quota:{userId}:{date}`） |
| 名单导入 | 群主单日 ≤20 次导入/生成（防批量滥用，内测期） |
| 一次性码激活 | 单用户 ≤10 次/分（防爆破） |
| HTTPS/TLS | TLS 1.3 全链路 |
| 存储加密 | AES-256 + 手机号脱敏 |
| RLS | 每表 UserId 行级安全（D02 §九） |
| JSON 命名 | System.Text.Json camelCase 策略（Entity PascalCase → 传输 camelCase，全端统一） |

---

## 十五、与 D01/D02/V3 衔接

| API | D01 对齐 | D02 V1.4 落库 | V3.1 对齐 |
|-----|---------|---------------|----------|
| POST /study/attempts | 判题路由 + Prompt 矩阵 | Attempts + MemoryStates + DailyStats + WrongQuestions | 判题契约/hintLevel/任务进度联动 |
| POST /study/hint | 背景钩子提示生成 | - | 引导容错/请提示我 |
| GET /review-queue | 状态机调度 | MemoryStates 视图（ReviewQueue） | 艾宾浩斯复习 |
| 群组/任务域 | - | Groups/GroupMembers/ParentStudentRelations/Tasks/TaskAssignments | 家校互动主链路（群组化） |
| **内测邀请域** | - | **BetaInviteCodes/OneTimeInviteCodes/RosterImports** | **内测邀请机制（平台发码→名单导入→一次性码→CSV→微信激活）** |
| 搭子域 | - | StudyBuddies（Status 状态机 + ExpiresAt） | 双向同意制/互看排名/未成年人保护 |
| 排行榜域 | - | RankSnapshots（每日冻结 + Groups.RankEnabled） | 战力榜/战绩榜/趋势箭头/群组开关 |
| 订阅/报告域 | - | Subscriptions + ParentStudentRelations | 家长付费/多孩子独立计费/相对进步 |
| PK 系列 | 8.4 PK 双模式 | PkMatches（+InviteCode/FinishReason）/PkPlayers/PkAttempts | 仅搭子 PK/胜场计入战力榜 |
| 判题反馈 | - | JudgmentFeedback | "判错了"人工复核 |
| 统计系列 | 场景-状态机耦合 | DailyStats/KnowledgeMastery/WrongQuestions | 热力图/连续天数/错题本/周月报 |
| auth | - | - | 微信登录/手机号可选绑定（内测后） |

---

## 十六、落地顺序

| 阶段 | 内容 |
|------|------|
| A1 | auth + 题库域（列表/详情/取题/导入） |
| A2 | 学习域（sessions/attempts/hint/review-queue + 会话结果） |
| A3 | 群组与任务域（群组/任务/看板/家长关联） |
| A4 | **内测邀请域（激活建群/名单导入/Agent 整理/生成一次性码/CSV 导出/成员激活）** |
| A5 | 统计域（heatmap/streak/report/wrong） |
| A6 | 搭子域 + 排行榜域（RankSnapshots 每日批处理先行） |
| A7 | 订阅与家长报告域 + 判题反馈域 |
| A8 | PK 域（matches/answer/result/stats + WS） |
| A9 | 用户数据（export/delete）+ RLS 全量启用 |

---

## 变更记录

| 日期 | 版本 | 变更内容 | 关联ADR |
|------|------|---------|---------|
| 2026-09-05 | V1.3 | **群组化 + 内测邀请域**：① 班级→群组全量术语/路径（/classes→/groups、/teacher→/owner，群主=Owner）；② 新增内测邀请域 5 API（激活建群凭内测码 / 名单导入 / Agent 整理预览 / 生成一次性码 / CSV 导出 / 成员微信激活）；③ 新增错误码 52xx（内测码/一次性码/手机号校验/名单批次/内测开关）；④ 登录说明补内测期绑定边界（手机号不绑账户）；⑤ 限流补名单导入/激活防爆破；⑥ 衔接表/落地顺序同步（A4 内测邀请域） | ADR-007 |
| 2026-09-05 | V1.2 | **对齐 V3.1.2 + D02 V1.3**：① 技术栈头部修正为 React + shadcn/ui（ADR-007）；② 全部 JSON 示例转 camelCase（对齐 .NET PascalCase→框架自动转换命名规范）；③ 新增班级与任务域、搭子社交域、排行榜域、家长订阅与报告域、判题反馈域；④ PK 域限定搭子 + 补 InviteCode/FinishReason；⑤ 学习域补 taskId + 会话结果接口；⑥ 错误码按域扩展 | ADR-007 |
| 2026-09-04 | V1.1 | 四文档自审修复：PK 答题响应补 confidence（对齐 D01 判题契约）；判题响应补状态值映射说明（0-3→✕△○★）；取题接口补 sessionId 参数（按状态机出题）；错题本响应补 subject 字段；判题 hint 字段行为说明（none+correct→空串） | - |
| 2026-09-04 | V1.0 | 初始版：六大 API 域契约 + 错误码 + 安全 | - |
