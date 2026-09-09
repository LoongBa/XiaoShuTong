# HANDOVER · 排行榜搭子域切片交接文档

> **定位**：设计阶段→实施阶段的承上启下文档（切片验证版）。Agent 阅读本文档后完成自引导加载后续动作——AC-Kit。
> **项目**：小书童（XiaoShuTong）· 排行榜搭子域切片
> **范围**：模块 7（排行榜）+ 模块 8（学习搭子），切片序号 06（前序：01 群组管理域、02 学习Session域、03 题库判题域、04 家校任务闭环域、05 可视化激励域）
> **更新日期**：2026-09-06

---

## 文档流总览

```
R01(需求)→ S01(场景)→ DS01(数据结构)→ U01(用况与契约)

DS01 ──→ Entity.cs（手写）     参考：{AC-Kit}/templates/Entity.cs.txt
   └──→ xCodeGen → DataService（自动生成，MSBuild AfterBuild 触发）

U01 ──→ Service + [GenerateController]（手写） ← SG 自动生成接口，不手写
   └──→ 服务契约一览（并入实施总览） + 本 HANDOVER
```

> ⚠️ **必须执行顺序**：`DS01 → Entity → xCodeGen → DataService/DTO → U01 → Service`
> - 缺少 xCodeGen → DataService/DTO/Conditions 缺失 → Service 无法编译
> - xCodeGen 失败时上报 Sisyphus，不要自行绕过（手写 DataService 与 SG 生成的 Controller 不兼容）

---

## §一 输入文档清单

- **R-series**：`docs/草稿/切片验证-排行榜搭子域/R01-需求与功能设计-排行榜与学习搭子.md`
- **S-series**：`docs/草稿/切片验证-排行榜搭子域/S01-场景描述-排行榜与学习搭子.md`
- **DS-series**：`docs/草稿/切片验证-排行榜搭子域/DS01-数据结构设计-排行榜与学习搭子.md`
- **U-series**：`docs/草稿/切片验证-排行榜搭子域/U01-用况与契约-排行榜与学习搭子.md`
- **实施总览**：`docs/草稿/切片验证-排行榜搭子域/排行榜搭子域-切片实施总览.md`

---

## §二 设计规格清单

### UC 跟踪表（Agent 逐项填写）

| UC# | UC 名称 | 子域 | § Entity | § Service | § 测试 | § Schema | § 验证 | 备注 |
|:---:|---------|:----:|:--------:|:---------:|:------:|:--------:|:------:|------|
| 7.1 | 榜单查询 | Rank | ☐ | ☐ | ☐ | ☐ | ☐ | RankEnabled 门控 |
| 7.2 | 我的排名 | Rank | ☐ | ☐ | ☐ | ☐ | ☐ | |
| 7.3 | 快照每日冻结 | Rank | ☐ | ☐ | ☐ | ☐ | ☐ | BackgroundJob |
| 8.1 | 发起搭子邀请 | Buddy | ☐ | ☐ | ☐ | ☐ | ☐ | 未成年人保护 |
| 8.2 | 同意邀请 | Buddy | ☐ | ☐ | ☐ | ☐ | ☐ | CROSS ≤5 FOR UPDATE |
| 8.3 | 拒绝邀请 | Buddy | ☐ | ☐ | ☐ | ☐ | ☐ | |
| 8.4 | 搭子列表 | Buddy | ☐ | ☐ | ☐ | ☐ | ☐ | UNION 双向 |
| 8.5 | 搭子排名详情 | Buddy | ☐ | ☐ | ☐ | ☐ | ☐ | 仅 accepted |
| 8.6 | 解除搭子 | Buddy | ☐ | ☐ | ☐ | ☐ | ☐ | |

### Entity → DS 映射表

| Entity | 子域 | 关联 DS 文档 | 文件路径 |
|--------|:----:|:------------:|---------|
| RankSnapshots | Rank | DS01 | `Entities/Rank/RankSnapshots.cs` |
| StudyBuddies | Buddy | DS01 | `Entities/Buddy/StudyBuddies.cs` |

### Service 跟踪表

| Service 类 | 子域 | 关联 UC | 备注 |
|-----------|:----:|:-------:|------|
| GetRankingsService | Rank | 7.1 | [GenerateController] |
| GetMyRankingService | Rank | 7.2 | [GenerateController] |
| RankSnapshotFreezeJob | Rank | 7.3 | BackgroundJob，无 Controller |
| InviteBuddyService | Buddy | 8.1 | [GenerateController] |
| AcceptBuddyInviteService | Buddy | 8.2 | [GenerateController] + [Transactional] |
| RejectBuddyInviteService | Buddy | 8.3 | [GenerateController] |
| ListBuddiesService | Buddy | 8.4 | [GenerateController] |
| GetBuddyRankService | Buddy | 8.5 | [GenerateController] |
| RemoveBuddyService | Buddy | 8.6 | [GenerateController] |

### 测试跟踪表

| 测试类 | 关联 Service | 重点 |
|-------|-------------|------|
| GetRankingsServiceTests | GetRankingsService | RankEnabled 门控/战力榜不受影响/趋势箭头/空快照 |
| RankSnapshotFreezeJobTests | RankSnapshotFreezeJob | 指标聚合/幂等 upsert/单范围失败 |
| InviteBuddyServiceTests | InviteBuddyService | ≤5/单日≤10/同源OR/重复拦截/ExpiresAt |
| AcceptBuddyInviteServiceTests | AcceptBuddyInviteService | 双方≤5 FOR UPDATE/CROSS 回滚/状态流转 |
| ListBuddiesServiceTests | ListBuddiesService | UNION 双向/仅 accepted/排名互看不暴露明细 |
| GetBuddyRankServiceTests | GetBuddyRankService | 非搭子 6004/最小化暴露 |
| RemoveBuddyServiceTests | RemoveBuddyService | 置 removed/历史保留/非当事人 |

---

## §三 服务契约一览

> 完整表格见 `排行榜搭子域-切片实施总览.md`（9 个 Service，BR-01~32，CROSS 事务 2 处：7.3/8.2，跨模块依赖 5 项：群组/学习/PK/账户 + PK 反向消费）

---

## §四 项目约定

| 项 | 值 |
|----|-----|
| 项目名 | XiaoShuTong |
| 切片范围 | 排行榜域（模块 7）+ 学习搭子域（模块 8）；切片 06（前序：01~05） |
| 子域 | `Rank`（排行榜）+ `Buddy`（搭子） |
| 用户类型 | 群主(owner)/学生(student)/家长(parent)；本域均为 student |
| 开发路线 | WebApi（切片验证，全量待定） |
| 测试模式 | 内存 DAC（切片验证） |
| 审计字段命名 | `CreateTime`/`UpdateTime`（存量无审计列 → 补列） |
| 枚举 ORM 映射 | `[Column(MapType = typeof(string))]`，PascalCase 存字符串（存量小写 → 迁移映射） |
| 错误码 | 数字域码 60xx（搭子）/ 70xx（排行榜）/ 10xx（全局）+ SNAKE_CASE 双列（迁移决策） |
| 主键决策 | 系统内部 `long Id`；外部关联/跨系统 `Uid`（uuid 业务键），实体表补 Uid 列 |
| API 命名 | JSON 字段 camelCase（D03 原则 #6）；SNAKE_CASE 仅错误码语义名 |
| 框架路径 | `$env:TKWFDeployPath` |

---

## §五 编码启动序列（切片验证）

| 步骤 | 动作 | Skill / 工具 | 产出 | 完成标志 |
|:----:|------|-------------|------|---------|
| 0 | **跨切片依赖启动检查**：确认切片 02（学习）`Entities/Learning/*.cs` + 切片 03（题库判题）`Entities/Bank/*.cs` 已编码；缺失则先执行前序切片编码序列 | - | 依赖就绪 | `.g.cs` 存在 |
| 1 | 读 DS01 -> 写 2 个 Entity.cs | tkwf-entity | `Entities/Rank/*.cs` + `Entities/Buddy/*.cs` | 文件存在 |
| 2 | `dotnet build` | - | xCodeGen 生成 DataService/DTO/Conditions | `.g.cs` 存在 |
| 3 | 读 U01 -> 写 9 个 Service.cs | tkwf-service | `Services/Rank/*.cs` + `Services/Buddy/*.cs` | 文件存在 |
| 4 | 读 U01 BR -> 写测试 | tkwf-test | `*ServiceTests.cs` | 文件存在 |
| 5 | `dotnet build` | - | 全项目编译 | 0 error |
| 6 | `dotnet test` | - | 测试执行 | 全部通过 |

> ⚠️ **存量迁移前置确认（路径 B 转化衔接）**：DS01「存量差异标注」①主键（uuid → long Id + Uid）已按前序切片决策确定；④ volume 指标口径（=DailyStats.LearnedCount 累计）与 ⑤ 榜单权重公式（简单求和）为**本切片决策待确认项**——实施前确认。

---

## §六 衔接与继续执行

> **实施 Agent 必读**：本切片执行完毕后，按下述逻辑衔接后续切片。**多切片交接纪律**：每个切片完成且验证通过后，**询问用户是否继续执行下一个切片，得到同意后才继续**；不得静默连跑。

### 6.1 执行完毕询问

| 状态 | 动作 |
|------|------|
| 本切片全部步骤完成 + 测试通过 | 向用户报告完成摘要（Entity/Service/测试数 + 编译测试结果）→ **询问用户：是否继续执行下一个切片（切片 07（搭子PK竞技域））？** |
| 用户同意 | 读下一个切片 HANDOVER → 执行其 §五 编码启动序列 |
| 用户暂缓/暂停 | 保存当前状态（本切片 HANDOVER + 实施产物在解决方案中），等待用户指示 |

### 6.2 中断后续跑衔接（从任意切片启动）

若实施中途被中断，之后从**本切片（或任意切片）HANDOVER 启动**，Agent 必须：

| 步骤 | 动作 |
|------|------|
| 1 | **检查解决方案是否存在**：`XiaoShuTong.sln` 不存在 → 按切片 01 §五 步骤 0 创建（仅首次需要） |
| 2 | **检查本切片的前序切片实体是否已编码**（逐项）：<br>| 1 | 切片 02（学习Session域）已实施：`Entities/Learning/*.cs` 存在 |
| 2 | 切片 03（题库判题域）已实施：`Entities/Bank/*.cs` 存在 | |
| 3 | **前序未编码** → 提醒用户："检测到前序切片尚未实施，需要先完成其编码序列" → 询问是否跳转执行前序 HANDOVER，或由用户决定 |
| 4 | **前序已编码** → 直接执行本切片 §五 编码启动序列 |

> **切片依赖链**：01（群组）→ 02（学习）→ 03（题库判题）→ 04（家校任务）→ 05（可视化激励，依赖 02）→ 06（排行榜搭子，依赖 02/03）→ 07（PK，依赖 03/06）→ 08（家长报告，依赖 02/04）

### 6.3 完成后状态

- 本切片实施产物（Entity/DataService/Service/测试）已在解决方案中，版本已提交
- 后续切片 Agent 读本 HANDOVER §五 步骤 0 即知本切片已实施（`Entities/Rank+Buddy/*.cs` 存在）

---

## 附录：8 项交接条件自查（DG-01 §2.4）

| # | 条件 | 验证方式 | 结果 |
|:-:|------|---------|:----:|
| 1 | U-series 所有 Use-Case 的契约签名已定义（含 DTO 字段类型） | 审查 | ✅ 9 UC 契约签名 + Req/Res DTO 完整 |
| 2 | 所有 BR-xx 规则已列全，无「待定」「后续补充」 | 审查 | ✅ BR-01~32 全覆盖 |
| 3 | 所有 Use-Case 引用的 DataService 已在 DS 中定义 | 交叉核对 | ✅ 2 实体均有对应 DataService（RankSnapshots/StudyBuddies）；跨模块引用已标注 |
| 4 | 所有跨模块依赖已标注 | 交叉核对 | ✅ 群组/学习/PK/账户 + PK 反向消费（UC 头/编排逻辑/实施总览三处一致） |
| 5 | 所有 Alternative Flow 至少有一条对应的 BR-xx | 审查 | ✅ 每条替代流均有 BR 对应 |
| 6 | 所有 R-series 约束条件的交叉引用均指向已存在功能点 | 交叉核对 | ✅ R01 约束均在 U01 有 BR |
| 7 | 交叉模块影响扫描已完成 | 审查 | ✅ R01「交叉模块影响扫描」覆盖模块 1/4/9 |
| 8 | 所有 DS 实体的子域标注与 U 文档 UC 头子域一致 | 交叉核对 | ✅ `Rank`/`Buddy` 与 UC 头一致 |

**⚠️ 补充说明**：条件 1~8 在"设计文档"层面全部满足。但存在 **2 类非阻断缺口**需实施前确认（见下节）：volume/权重口径、搭子过期清理作业。

---

## 已决策项（本切片新增）

| # | 决策 | 说明 |
|:-:|------|------|
| 1 | 同群组/同年级 = OR 语义 | D02 斜杠表述未明确（实施前确认） |
| 2 | volume = DailyStats.LearnedCount 累计 | D02 未定义来源 |
| 3 | 榜单指标简单求和 | 战力 = streak+volume+pk_wins；战绩 = accuracy+mastery（权重待 PRD） |
| 4 | stars 指标保留无消费 | 枚举保留，本期无榜单使用 |
| 5 | Redis 计数器 TTL=24h | D02 未定义 |
| 6 | 趋势"持平" = rank 相等 → flat | D02 未定义阈值 |
| 7 | RankSnapshots 唯一约束补全 | UNQ 复合（User/Scope/Subject/Metric/Date） |
| 8 | 搭子海报/链接 = 前端合成 | D02 无数据层定义 |

## 待确认项清单

| # | 项 | 影响 | 建议 |
|:-:|---|------|------|
| 1 | **volume 指标口径**：= DailyStats.LearnedCount 累计是否符合业务意图（而非 StudySeconds） | 战力榜数值 | 业务确认；对齐 PRD"背诵量"表述 |
| 2 | **榜单权重公式**：简单求和 vs 加权（如 PK 胜场权重更高） | 榜单公平性 | 向 PRD 补充权重；本期简单求和尚可 |
| 3 | **搭子过期清理作业**：D02 仅"Hangfire 定时清理"无作业定义 | 过期状态流转 | 本期 UC-8.2 幂等拒绝过期；清理作业列入开发方案 |
| 4 | **年级范围数据源**：MVP 无 Schools 表，Grade 范围 = 平台同年级 | 榜单范围 | 对齐 Groups.Grade；多校阶段引入 Schools 后再细分 |

---

> *文档版本：第一阶段 v1.0*
> *编制日期：2026-09-06*
> *同步版本：R01 v1.0 / S01 v1.0 / DS01 v1.0 / U01 v1.0*