# HANDOVER · 搭子PK竞技域切片交接文档

> **定位**：设计阶段→实施阶段的承上启下文档（切片验证版）。Agent 阅读本文档后完成自引导加载后续动作——AC-Kit。
> **项目**：小书童（XiaoShuTong）· 搭子 PK 竞技域切片
> **范围**：模块 9（搭子 PK 竞技：PK 大厅/对战/结果），切片序号 07（前序：01 群组管理域、02 学习Session域、03 题库判题域、04 家校任务闭环域、05 可视化激励域、06 排行榜搭子域）
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

- **R-series**：`docs/草稿/切片验证-搭子PK竞技域/R01-需求与功能设计-搭子PK竞技.md`
- **S-series**：`docs/草稿/切片验证-搭子PK竞技域/S01-场景描述-搭子PK竞技.md`
- **DS-series**：`docs/草稿/切片验证-搭子PK竞技域/DS01-数据结构设计-搭子PK竞技.md`
- **U-series**：`docs/草稿/切片验证-搭子PK竞技域/U01-用况与契约-搭子PK竞技.md`
- **实施总览**：`docs/草稿/切片验证-搭子PK竞技域/搭子PK竞技域-切片实施总览.md`

---

## §二 设计规格清单

### UC 跟踪表（Agent 逐项填写）

| UC# | UC 名称 | 子域 | § Entity | § Service | § 测试 | § Schema | § 验证 | 备注 |
|:---:|---------|:----:|:--------:|:---------:|:------:|:--------:|:------:|------|
| 9.1 | 发起 PK（方式 A） | Pk | ☐ | ☐ | ☐ | ☐ | ☐ | CROSS + 搭子资格 |
| 9.2 | 加入 PK（方式 B） | Pk | ☐ | ☐ | ☐ | ☐ | ☐ | |
| 9.3 | 对战答题 | Pk | ☐ | ☐ | ☐ | ☐ | ☐ | 判题引擎跨模块 |
| 9.4 | PK 结果 + AI 点评 | Pk | ☐ | ☐ | ☐ | ☐ | ☐ | 合规红线 |
| 9.5 | 掉线弃权检测 | Pk | ☐ | ☐ | ☐ | ☐ | ☐ | BackgroundJob |
| 9.6 | PK 战绩 | Pk | ☐ | ☐ | ☐ | ☐ | ☐ | 视图读取 |

### Entity → DS 映射表

| Entity | 子域 | 关联 DS 文档 | 文件路径 |
|--------|:----:|:------------:|---------|
| PkMatches | Pk | DS01 | `Entities/Pk/PkMatches.cs` |
| PkPlayers | Pk | DS01 | `Entities/Pk/PkPlayers.cs` |
| PkAttempts | Pk | DS01 | `Entities/Pk/PkAttempts.cs` |

> **视图（不建实体）**：PkPlayerStats（D02 §5.4）——由 `IPkPlayerStatsViewService` 读取，胜率 API 层派生。

### Service 跟踪表

| Service 类 | 子域 | 关联 UC | 备注 |
|-----------|:----:|:-------:|------|
| CreatePkMatchService | Pk | 9.1 | [GenerateController] + [Transactional] |
| JoinPkMatchService | Pk | 9.2 | [GenerateController] |
| SubmitPkAnswerService | Pk | 9.3 | [GenerateController] |
| GetPkResultService | Pk | 9.4 | [GenerateController] |
| PkForfeitDetectionJob | Pk | 9.5 | BackgroundJob，无 Controller |
| GetPkStatsService | Pk | 9.6 | [GenerateController] |

### 测试跟踪表

| 测试类 | 关联 Service | 重点 |
|-------|-------------|------|
| CreatePkMatchServiceTests | CreatePkMatchService | 搭子资格/原子创建/InviteCode/CROSS 回滚 |
| JoinPkMatchServiceTests | JoinPkMatchService | 状态/人数/对战码/搭子资格 |
| SubmitPkAnswerServiceTests | SubmitPkAnswerService | **计分规则（Correct+10/Partial 0）**/防重复/降级 |
| GetPkResultServiceTests | GetPkResultService | 胜负判定/合规无对比榜/AI 点评兜底 |
| PkForfeitDetectionJobTests | PkForfeitDetectionJob | 30s 弃权/重连取消/幂等 |
| GetPkStatsServiceTests | GetPkStatsService | 视图聚合/胜率/RLS |

---

## §三 服务契约一览

> 完整表格见 `搭子PK竞技域-切片实施总览.md`（6 个 Service，BR-01~27，CROSS 事务 2 处：9.1/9.5，跨模块依赖 5 项：搭子/题库/判题/AI点评/排行榜双向）

---

## §四 项目约定

| 项 | 值 |
|----|-----|
| 项目名 | XiaoShuTong |
| 切片范围 | 搭子 PK 竞技域（模块 9）；切片 07（前序：01~06） |
| 子域 | `Pk` |
| 用户类型 | 群主(owner)/学生(student)/家长(parent)；本域均为 student |
| 开发路线 | WebApi（切片验证，全量待定） |
| 测试模式 | 内存 DAC（切片验证） |
| 审计字段命名 | `CreateTime`/`UpdateTime`（PkPlayers/PkAttempts 补列；PkMatches CreatedAt 转化） |
| 枚举 ORM 映射 | `[Column(MapType = typeof(string))]`，PascalCase 存字符串（存量小写 → 迁移映射） |
| 错误码 | 数字域码 20xx（PK）/ 60xx（搭子）/ 10xx（全局）+ SNAKE_CASE 双列（迁移决策） |
| 主键决策 | 系统内部 `long Id`；外部关联/跨系统 `Uid`（uuid 业务键），实体表补 Uid 列 |
| API 命名 | JSON 字段 camelCase（D03 原则 #6）；SNAKE_CASE 仅错误码语义名 |
| 实时性 | SignalR 同步 + 微信 H5 降级 30s 轮询（出处 ADR-005，非 D03） |
| 框架路径 | `$env:TKWF_FRAMEWORK_PATH` |

---

## §五 编码启动序列（切片验证）

| 步骤 | 动作 | Skill / 工具 | 产出 | 完成标志 |
|:----:|------|-------------|------|---------|
| 0 | **跨切片依赖启动检查**：确认切片 03 判题引擎（JudgingEngineService）与切片 06 搭子（StudyBuddies）已编码（Entity + DataService 产物存在） | 前序切片 | 依赖就绪 | `.g.cs` 存在 |
| 1 | 读 DS01 -> 写 3 个 Entity.cs | tkwf-entity | `Entities/Pk/*.cs` | 文件存在 |
| 2 | `dotnet build` | - | xCodeGen 生成 DataService/DTO/Conditions | `.g.cs` 存在 |
| 3 | 读 U01 -> 写 6 个 Service.cs | tkwf-service | `Services/Pk/*.cs` | 文件存在 |
| 4 | 读 U01 BR -> 写测试 | tkwf-test | `*ServiceTests.cs` | 文件存在 |
| 5 | `dotnet build` | - | 全项目编译 | 0 error |
| 6 | `dotnet test` | - | 测试执行 | 全部通过 |

> ⚠️ **存量迁移前置确认（路径 B 转化衔接）**：DS01「存量差异标注」①主键（uuid → long Id + Uid）已按前序切片决策确定；**③ PkAttempts 防重复 UNIQUE**、**④ forfeit/timeout Status=Finished**、**⑤ partial 计 0 分** 为本切片决策待实施前确认（尤其 ④ 影响 PkPlayerStats 胜场统计口径）。

---

## 附录：8 项交接条件自查（DG-01 §2.4）

| # | 条件 | 验证方式 | 结果 |
|:-:|------|---------|:----:|
| 1 | U-series 所有 Use-Case 的契约签名已定义（含 DTO 字段类型） | 审查 | ✅ 6 UC 契约签名 + Req/Res DTO 完整 |
| 2 | 所有 BR-xx 规则已列全，无「待定」「后续补充」 | 审查 | ✅ BR-01~27 全覆盖 |
| 3 | 所有 Use-Case 引用的 DataService 已在 DS 中定义 | 交叉核对 | ✅ 3 实体均有对应 DataService（PkMatches/PkPlayers/PkAttempts）+ PkPlayerStats 视图；跨模块引用已标注 |
| 4 | 所有跨模块依赖已标注 | 交叉核对 | ✅ 搭子/题库/判题/AI点评/排行榜双向（UC 头/编排逻辑/实施总览三处一致） |
| 5 | 所有 Alternative Flow 至少有一条对应的 BR-xx | 审查 | ✅ 每条替代流均有 BR 对应 |
| 6 | 所有 R-series 约束条件的交叉引用均指向已存在功能点 | 交叉核对 | ✅ R01 约束均在 U01 有 BR |
| 7 | 交叉模块影响扫描已完成 | 审查 | ✅ R01「交叉模块影响扫描」覆盖模块 3/4/5/7/8 |
| 8 | 所有 DS 实体的子域标注与 U 文档 UC 头子域一致 | 交叉核对 | ✅ 全部 `Pk` 一致 |

**⚠️ 补充说明**：条件 1~8 在"设计文档"层面全部满足。但存在 3 类非阻断缺口需实施前确认（见下节）：partial 计分口径、forfeit Status 取值、AI 点评失败降级。

---

## 已决策项（本切片新增）

| # | 决策 | 说明 |
|:-:|------|------|
| 1 | forfeit/timeout Status=Finished | 对齐 PkPlayerStats 视图过滤口径（胜场统计正确性） |
| 2 | Partial 判题计 0 分 | D02/D01 未定义 PK 场景 partial 计分；保证"答对 +10"语义清晰 |
| 3 | 掉线 30s 判定 = 事件驱动 + Hangfire 兜底 | D03 仅语义无作业；30s 轮询兼作断线检测（出处 ADR-005） |
| 4 | InviteCode Pending 期间有效 | 对局结束失效；新对局新码 |
| 5 | AI 点评失败 AiComment 留空 + 兜底文案 | 防止结果页空白 |
| 6 | PkAttempts 防重复 UNIQUE | 防同题重复提交（幂等） |
| 7 | knowledgeEggs 响应态不落库 | D02 无承载表；前端一次性展示 |

## 待确认项清单

| # | 项 | 影响 | 建议 |
|:-:|---|------|------|
| 1 | **Partial 判题 PK 计分**：计 0 分是否合适（vs 计 5 分或按比例） | 比分公平性 | 业务确认；倾向 0 分（对齐"答对 +10"二分语义） |
| 2 | **forfeit/timeout Status 口径**：置 Finished 是否正确 | 胜场统计 | 业务确认后需同步 D02 补丁说明 |
| 3 | **PK 页面 UI 规格**：V2.2 无 PK 页（归档 §9 仅文字）；线框/状态枚举为本文档补充定义 | 前端 | 向 UI 设计补片 |
| 4 | **AI 趣味点评 LLM 成本**：按局计费 + 失败兜底 | 成本/体验 | 缓存模板兜底 + 试运行观察 |

---

> *文档版本：第一阶段 v1.0*
> *编制日期：2026-09-06*
> *同步版本：R01 v1.0 / S01 v1.0 / DS01 v1.0 / U01 v1.0*