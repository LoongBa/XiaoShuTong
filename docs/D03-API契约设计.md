---
title: API 契约设计
version: V1.1
last_updated: 2026-09-04
status: 活跃
superseded_by:
---

# API 契约设计（D03）

> 本文档定义小书童前端（Vue3/Uni-app）与后端（ASP.NET Core）之间的 RESTful API 契约：认证、题库、学习、统计、PK、用户数据六大域。
> 关联文档：`docs/D01-统一题库与多学科学习平台设计.md`（框架 V1.2）、`docs/D02-学习数据与记忆跟踪设计.md`（数据层 V1.0）、`docs/1-需求分析/V1/“小书童（背书搭子）”产品需求分析和设计-V3.md`（需求）

---

## 一、总体设计原则

| # | 原则 | 说明 |
|---|------|------|
| 1 | **RESTful** | 资源式路径 + HTTP 方法；仅 JSON 交换 |
| 2 | **统一响应** | 所有接口返回 `{code, message, data}` 三字段 |
| 3 | **错误码分级** | 全局错误码（10xx）+ 域错误码（PK 20xx/学习 30xx/统计 40xx） |
| 4 | **版本控制** | URL 前缀 `/api/v1/`，破坏性变更升 v2 |
| 5 | **鉴权统一** | 除登录外全部需携带 `Authorization: Bearer <token>` |
| 6 | **判题契约对齐** | 判题相关接口输出 D01 统一契约 `result/confidence/matched/missing/hint` |
| 7 | **防爬对齐 V3** | 题库内容"背一题取一题"，不返回明文列表 |
| 8 | **限流** | 单用户 60 次/分，单 IP 限流（V3 非功能需求） |

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
  "avatar_url": "可选"
}
```

**响应**：
```json
{
  "code": 0,
  "message": "success",
  "data": {
    "access_token": "JWT",
    "refresh_token": "JWT",
    "expires_in": 7200,
    "user_id": "uuid",
    "need_bind_phone": false
  }
}
```

**流程**：微信 code → 后端换 openid → 查已有用户（uuid）→ 无则创建 → 签发 JWT（含 user_id/role）。

### 2.2 手机号绑定（可选）

```
POST /api/v1/auth/bind-phone
```

**请求**：`{ "phone": "138****1234", "sms_code": "123456" }`
**响应**：`{ "code": 0, "data": { "bound": true } }`

> 可选绑定（V3 决策 1）；手机号存储脱敏（138****1234）。

### 2.3 令牌刷新

```
POST /api/v1/auth/refresh
```
**请求**：`{ "refresh_token": "..." }` → **响应**：新 access_token + refresh_token。

### 2.4 鉴权中间件

- 所有 `/api/v1/*` 接口（除 auth 外）校验 Bearer token
- 无效/过期 → `{code: 1001, message: "未登录或令牌已过期"}`
- 未成年人（<14 岁）接口额外校验监护人同意标记

---

## 三、统一响应与错误码

### 3.1 统一响应结构

```json
{
  "code": 0,
  "message": "success",
  "data": {}        // 可为对象/数组/null
}
```

### 3.2 全局错误码（10xx）

| code | 含义 |
|------|------|
| 0 | 成功 |
| 1001 | 未登录 / 令牌失效 |
| 1002 | 参数错误（附带字段校验信息） |
| 1003 | 资源不存在（题库/题目/用户） |
| 1004 | 无权限（私域题库非 owner） |
| 1005 | 请求过于频繁（限流） |
| 1006 | 内部错误（判题服务不可用等） |

### 3.3 域错误码

| 域 | code | 含义 |
|----|------|------|
| PK | 2001 | 比赛不存在 |
| | 2002 | 比赛已结束/不可加入 |
| | 2003 | 参赛人数已满 |
| | 2004 | 非参赛者 |
| 学习 | 3001 | 会话不存在 |
| | 3002 | 题目不属于该题库 |
| | 3003 | 答案格式错误 |
| 统计 | 4001 | 无统计数据 |

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
        "bank_id": "chinese-7to9-pep",
        "subject": "chinese",
        "name": "七-九年级-统编教材",
        "version": "V1.0",
        "topic_count": 12,
        "question_count": 435
      }
    ],
    "total": 1
  }
}
```

### 4.2 题库详情

```
GET /api/v1/banks/{bank_id}
```
返回 bank.v1.json 元数据 + 知识点层级（topic 树）。

### 4.3 创建题库（私域）

```
POST /api/v1/banks
```
**请求**：`{ "name", "subject", "privacy": "private", "description" }`
**响应**：新建 bank_id + 元数据。

### 4.4 导入题目（txt/JSON）

```
POST /api/v1/banks/{bank_id}/import
Content-Type: multipart/form-data
```
**字段**：`file`（.txt 或 .json）、`topic`（目标章节）
**响应**：`{ "imported": 120, "failed": 3, "failures": ["行 45: 格式错误"] }`

### 4.5 取题（防爬：背一题取一题）

```
GET /api/v1/questions/next?bank_id=xxx&type=R1&knowledge_point=观沧海&session_id=uuid
```
**响应**：
```json
{
  "code": 0,
  "data": {
    "question_id": "Q-ch-7a-0001",
    "type": "R1",
    "content": { "question": "日月之行" },
    "knowledge_point": "观沧海",
    "knowledge_card_id": "KC-ch-观沧海"
  }
}
```

> **不含答案**！答案与 keywords 仅在下述判题接口服务端侧校验。`session_id` 关联当前学习会话（D02 study_sessions），用于按状态机出题（到期复习优先 → 未掌握优先 → 新题）。知识卡片走单独接口。

### 4.6 知识卡片

```
GET /api/v1/questions/{question_id}/card
```
**响应**：知识卡片字段（author_card/word_card/event_card 等，按题库模板）。

---

## 五、学习域 API

### 5.1 开始学习会话

```
POST /api/v1/study/sessions
```
**请求**：`{ "scenario": "memorize", "bank_id": "chinese-7to9-pep", "session_type": "分阶" }`
**响应**：`{ "session_id": "uuid", "question_count": 20 }`

### 5.2 提交作答（核心：判题接口）

```
POST /api/v1/study/attempts
```

**请求**：
```json
{
  "session_id": "uuid",
  "question_id": "Q-ch-7a-0001",
  "user_answer": "若出其中",
  "hint_level": "none"
}
```

**响应**（D01 判题契约）：
```json
{
  "code": 0,
  "data": {
    "result": "correct",
    "confidence": 0.98,
    "matched_keywords": ["若出其中"],
    "missing_keywords": [],
    "hint": "",
    "pre_state": 1,
    "post_state": 2,
    "next_review_at": "2026-09-05T08:00:00Z"
  }
}
```

**状态值映射**（`pre_state`/`post_state`）：`0=✕ 未掌握`、`1=△ 模糊`、`2=○ 掌握`、`3=★ 熟练`（与 D02 4.1 一致）。前端据此渲染四阶记忆状态。

> 服务端流程：鉴权 → 查题目（校验归属）→ 判题路由（rule/keyword/LLM）→ 更新 memory_states → 写 attempts → 返回迁移结果。**答案永不下发客户端**。`hint` 字段在 hint_level=none 且答对时返回空串；hint_level=partial 时必给提示。

### 5.3 请求提示（背景钩子）

```
POST /api/v1/study/hint
```
**请求**：`{ "question_id": "Q-ch-7a-0001", "difficulty_slot": "S2" }`（可空，按状态路由）
**响应**：
```json
{
  "code": 0,
  "data": {
    "hint": "月亮这封信使要把你的心送到哪？——往西边",
    "difficulty_slot": "S2",
    "hint_source": "记忆钩子"
  }
}
```

### 5.4 复习队列

```
GET /api/v1/study/review-queue?date=2026-09-05&limit=30
```
**响应**：到期题目列表（question_id/state/next_review_at），不含答案。

### 5.5 记忆状态

```
GET /api/v1/study/memory-states?bank_id=xxx&state=△&page=1
```
**响应**：题目 id + 状态 + 正确率 + next_review_at。

---

## 六、统计域 API

### 6.1 记忆热力图

```
GET /api/v1/stats/heatmap?start=2026-08-01&end=2026-09-04
```
**响应**：
```json
{
  "code": 0,
  "data": {
    "days": [
      { "date": "2026-08-15", "starred_count": 12, "learned_count": 30 }
    ]
  }
}
```

### 6.2 连续天数

```
GET /api/v1/stats/streak
```
**响应**：`{ "current_streak": 5, "longest_streak": 12 }`

### 6.3 学习报告（周/月）

```
GET /api/v1/stats/report?period=week
```
**响应**：
```json
{
  "code": 0,
  "data": {
    "learned_count": 210,
    "accuracy": 0.87,
    "starred_count": 45,
    "weak_points": [{ "knowledge_point": "宋朝人物", "accuracy": 0.55 }]
  }
}
```

### 6.4 错题本

```
GET /api/v1/stats/wrong-questions?mastered=false&page=1
```
**响应**：错题列表（question_id/题目摘要/bank_id/subject/wrong_count/last_wrong_at），含"重新练习"入口。支持按 subject 过滤（跨学科错题分类）。

---

## 七、PK 竞技 API

### 7.1 发起 PK

```
POST /api/v1/pk/matches
```
**请求**：
```json
{
  "bank_id": "chinese-7to9-pep",
  "topic": "唐朝历史",
  "question_count": 10,
  "mode": "async"
}
```
**响应**：`{ "match_id": "uuid", "invite_code": "ABC123" }`

### 7.2 加入 PK

```
POST /api/v1/pk/matches/{match_id}/join
```
**响应**：`{ "match_id", "mode": "async", "question_count": 10, "per_question_time_s": 30 }`

### 7.3 取当前题（同步模式逐题 / 异步模式整组）

```
GET /api/v1/pk/matches/{match_id}/question?index=0
```
**响应**：`{ "question_id", "content": { "question" }, "index": 0, "time_left_s": 30 }`（不含答案）

### 7.4 提交 PK 答案

```
POST /api/v1/pk/matches/{match_id}/answer
```
**请求**：`{ "question_id", "user_answer", "time_cost_ms" }`
**响应**：`{ "is_correct": true, "result": "correct", "confidence": 0.95, "score": 10 }`

> PK 答题走判题引擎，输出与 D01 判题契约一致（含 confidence）；PK 数据 state_coupling=isolated，不入 attempts/memory_states。

### 7.5 PK 结果 + AI 点评

```
GET /api/v1/pk/matches/{match_id}/result
```
**响应**：
```json
{
  "code": 0,
  "data": {
    "status": "finished",
    "winner_id": "uuid",
    "win_reason": "同分比用时，小红更快",
    "players": [
      { "user_id": "uuid", "nickname": "小明", "score": 70, "total_time_ms": 210000, "ai_comment": "唐朝满分选手，宋朝断网……" }
    ],
    "knowledge_eggs": [
      { "question_id": "Q-his-0003", "egg": "王安石变法小科普……" }
    ]
  }
}
```

### 7.6 PK 战绩

```
GET /api/v1/pk/stats?user_id=uuid
```
**响应**：`{ "total_matches": 12, "wins": 8, "draws": 1, "win_rate": 0.67, "total_score": 860 }`

### 7.7 实时同步（WebSocket/SignalR）

```
WS /api/v1/pk/live?match_id=xxx&token=...
```
- 同步模式：双方实时状态推送（当前题/倒计时/对方进度）
- 微信 H5 不支持 WS 时自动降级异步模式（D01 8.4.1），HTTP 轮询 `/api/v1/pk/matches/{id}/status`

---

## 八、用户数据 API

### 8.1 数据导出

```
GET /api/v1/user/data/export
```
**响应**：JSON 打包全部学习数据（attempts/memory_states/daily_stats/pk_*），供下载。

### 8.2 账号注销

```
POST /api/v1/user/data/delete
```
**响应**：`{ "code": 0, "data": { "scheduled": true, "purge_at": "2026-10-04T00:00:00Z" } }`

> 30 天宽限期后级联删除（V3 决策：注销 30 天全清）。

---

## 九、限流与安全

| 措施 | 配置 |
|------|------|
| 单用户限流 | 60 次/分（Redis 计数） |
| 单 IP 限流 | 100 次/分 |
| 判题接口 | 每题 ≥2s 间隔（防刷） |
| HTTPS/TLS | TLS 1.3 全链路 |
| 存储加密 | AES-256 + 手机号脱敏 |
| RLS | 每表 user_id 行级安全（D02 九节） |

---

## 十、与 D01/D02/V3 衔接

| API | D01 对齐 | D02 落库 | V3 对齐 |
|-----|---------|---------|---------|
| POST /study/attempts | 判题路由 + Prompt 矩阵 | attempts + memory_states | 判题契约/hint_level |
| POST /study/hint | 背景钩子提示生成 | - | 引导容错/请提示我 |
| GET /review-queue | 状态机调度 | memory_states 视图 | 艾宾浩斯复习 |
| PK 系列 | 8.4 PK 双模式 | pk_matches/players/attempts | 趣味学习/非分数排名（PK 数据不进全局排名） |
| 统计系列 | 场景-状态机耦合 | daily_stats/knowledge_mastery | 热力图/连续天数/离线报告 |
| auth | - | - | 微信登录/手机号可选绑定 |

---

## 十一、落地顺序

| 阶段 | 内容 |
|------|------|
| A1 | auth + 题库域（列表/详情/取题/导入） |
| A2 | 学习域（sessions/attempts/hint/review-queue） |
| A3 | 统计域（heatmap/streak/report/wrong） |
| A4 | PK 域（matches/answer/result/stats + WS） |
| A5 | 用户数据（export/delete）+ RLS 全量启用 |

---

## 变更记录

| 日期 | 版本 | 变更内容 | 关联ADR |
|------|------|---------|---------|
| 2026-09-04 | V1.0 | 初始版：六大 API 域契约 + 错误码 + 安全 | - |
| 2026-09-04 | V1.1 | 四文档自审修复：PK 答题响应补 confidence（对齐 D01 判题契约）；判题响应补状态值映射说明（0-3→✕△○★）；取题接口补 session_id 参数（按状态机出题）；错题本响应补 subject 字段；判题 hint 字段行为说明（none+correct→空串） | - |
