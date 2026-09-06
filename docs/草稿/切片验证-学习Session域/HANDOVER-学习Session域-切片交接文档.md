# HANDOVER · 学习Session域切片交接文档

> **定位**：设计阶段→实施阶段的承上启下文档（切片验证版）。Agent 阅读本文档后完成自引导加载后续动作——AC-Kit。
> **项目**：小书童（XiaoShuTong）· 学习 Session 域切片
> **范围**：模块 4（学习 Session 域：会话/作答/记忆状态机/复习队列/错题本/每日统计），切片序号 02（前序：切片 01 = 群组管理域）
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

- **R-series**：`docs/草稿/切片验证-学习Session域/R01-需求与功能设计-学习Session.md`
- **S-series**：`docs/草稿/切片验证-学习Session域/S01-场景描述-学习Session.md`
- **DS-series**：`docs/草稿/切片验证-学习Session域/DS01-数据结构设计-学习Session.md`
- **U-series**：`docs/草稿/切片验证-学习Session域/U01-用况与契约-学习Session.md`
- **实施总览**：`docs/草稿/切片验证-学习Session域/学习Session域-切片实施总览.md`

---

## §二 设计规格清单

### UC 跟踪表（Agent 逐项填写）

| UC# | UC 名称 | 子域 | § Entity | § Service | § 测试 | § Schema | § 验证 | 备注 |
|:---:|---------|:----:|:--------:|:---------:|:------:|:--------:|:------:|------|
| 4.1 | 开始学习会话 | Learning | ☐ | ☐ | ☐ | ☐ | ☐ | CROSS + 跨任务域 |
| 4.2 | 提交作答（判题+状态迁移+派生同步） | Learning | ☐ | ☐ | ☐ | ☐ | ☐ | CROSS + 跨判题服务；★核心 |
| 4.3 | 请求提示 | Learning | ☐ | ☐ | ☐ | ☐ | ☐ | 跨判题服务 |
| 4.4 | 复习队列查询 | Learning | ☐ | ☐ | ☐ | ☐ | ☐ | |
| 4.5 | 记忆状态查询 | Learning | ☐ | ☐ | ☐ | ☐ | ☐ | 无 UI，核心层 UC |
| 4.6 | 会话结果 | Learning | ☐ | ☐ | ☐ | ☐ | ☐ | |
| 4.7 | 错题本查询（跨域引用统计域） | Learning | ☐ | ☐ | ☐ | ☐ | ☐ | 路由属统计域 |
| 4.8 | 知识点掌握度聚合 | Learning | ☐ | ☐ | ☐ | ☐ | ☐ | BackgroundJob |

### Entity → DS 映射表

| Entity | 子域 | 关联 DS 文档 | 文件路径 |
|--------|:----:|:------------:|---------|
| StudySessions | Learning | DS01 | `Entities/Learning/StudySessions.cs` |
| Attempts | Learning | DS01 | `Entities/Learning/Attempts.cs` |
| MemoryStates | Learning | DS01 | `Entities/Learning/MemoryStates.cs` |
| DailyStats | Learning | DS01 | `Entities/Learning/DailyStats.cs` |
| KnowledgeMastery | Learning | DS01 | `Entities/Learning/KnowledgeMastery.cs` |
| WrongQuestions | Learning | DS01 | `Entities/Learning/WrongQuestions.cs` |

### Service 跟踪表

| Service 类 | 子域 | 关联 UC | 备注 |
|-----------|:----:|:-------:|------|
| CreateStudySessionService | Learning | 4.1 | [GenerateController] |
| SubmitAttemptService | Learning | 4.2 | [GenerateController] + [Transactional] |
| GetHintService | Learning | 4.3 | [GenerateController] |
| GetReviewQueueService | Learning | 4.4 | [GenerateController] |
| GetMemoryStatesService | Learning | 4.5 | [GenerateController] |
| GetSessionResultService | Learning | 4.6 | [GenerateController] |
| GetWrongQuestionsService | Learning | 4.7 | [GenerateController] |
| KnowledgeMasteryAggregationJob | Learning | 4.8 | BackgroundJob，无 Controller |

### 测试跟踪表

| 测试类 | 关联 Service | 重点 |
|-------|-------------|------|
| CreateStudySessionServiceTests | CreateStudySessionService | 任务校验/幂等复用/CROSS 回滚 |
| SubmitAttemptServiceTests | SubmitAttemptService | **四阶状态机全迁移矩阵**/间隔计算/判题降级/四表一致/幂等 |
| GetHintServiceTests | GetHintService | 提示≤20字/不给答案/状态路由 |
| GetReviewQueueServiceTests | GetReviewQueueService | 到期筛选/逾期置顶/不含答案 |
| GetSessionResultServiceTests | GetSessionResultService | 聚合正确/新增★/卡壳点/空会话 |
| KnowledgeMasteryAggregationJobTests | KnowledgeMasteryAggregationJob | 增量幂等/聚合口径/单用户失败 |

---

## §三 服务契约一览

> 完整表格见 `学习Session域-切片实施总览.md`（8 个 Service，BR-01~47，CROSS 事务 3 处：4.1/4.2/4.8，跨模块依赖 3 项：任务域/题库域/判题服务）

---

## §四 项目约定

| 项 | 值 |
|----|-----|
| 项目名 | XiaoShuTong |
| 切片范围 | 学习 Session 域（模块 4）；切片 02（前序切片 01 = 群组管理域） |
| 子域 | `Learning` |
| 用户类型 | 群主(owner)/学生(student)/家长(parent)；本域均为 student |
| 开发路线 | WebApi（切片验证，全量待定） |
| 测试模式 | 内存 DAC（切片验证） |
| 审计字段命名 | `CreateTime`/`UpdateTime`（存量 DDL 为 CreatedAt/UpdatedAt 或缺失，需迁移/补列） |
| 枚举 ORM 映射 | `[Column(MapType = typeof(string))]`，PascalCase 存字符串（存量 smallint 0-3/小写/中文需迁移映射） |
| 错误码 | 数字域码 30xx（仅 3001/3002 存量）+ SNAKE_CASE 语义名双列（迁移决策） |
| 主键决策 | 系统内部 `long Id`；外部关联/跨系统 `Uid`（uuid 业务键），实体表补 Uid 列 |
| 框架路径 | `$env:TKWF_FRAMEWORK_PATH` |

---

## §五 编码启动序列（切片验证）

| 步骤 | 动作 | Skill / 工具 | 产出 | 完成标志 |
|:----:|------|-------------|------|---------|
| 0 | **跨切片依赖启动检查**：确认切片 01（群组管理域）实体已编码（`Entities/Groups/*.cs` + DataService 产物存在）；缺失则先执行切片 01 编码序列 | - | 依赖就绪 | `.g.cs` 存在 |
| 1 | 读 DS01 -> 写 6 个 Entity.cs | tkwf-entity | `Entities/Learning/*.cs` | 文件存在 |
| 2 | `dotnet build` | - | xCodeGen 生成 DataService/DTO/Conditions | `.g.cs` 存在 |
| 3 | 读 U01 -> 写 8 个 Service.cs | tkwf-service | `Services/Learning/*.cs` | 文件存在 |
| 4 | 读 U01 BR -> 写测试 | tkwf-test | `*ServiceTests.cs` | 文件存在 |
| 5 | `dotnet build` | - | 全项目编译 | 0 error |
| 6 | `dotnet test` | - | 测试执行 | 全部通过 |

> ⚠️ **存量迁移前置确认（路径 B 转化衔接）**：DS01「存量差异标注」①主键类型（uuid → long Id + Uid）已按切片 01 决策确定并写入输出（2026-09-06 用户决策），非阻断项；②审计字段/③枚举存储按迁移脚本处理，不阻断启动。

---

## §六 衔接与继续执行

> **实施 Agent 必读**：本切片执行完毕后，按下述逻辑衔接后续切片。**多切片交接纪律**：每个切片完成且验证通过后，**询问用户是否继续执行下一个切片，得到同意后才继续**；不得静默连跑。

### 6.1 执行完毕询问

| 状态 | 动作 |
|------|------|
| 本切片全部步骤完成 + 测试通过 | 向用户报告完成摘要（Entity/Service/测试数 + 编译测试结果）→ **询问用户：是否继续执行下一个切片（切片 03（题库判题域））？** |
| 用户同意 | 读下一个切片 HANDOVER → 执行其 §五 编码启动序列 |
| 用户暂缓/暂停 | 保存当前状态（本切片 HANDOVER + 实施产物在解决方案中），等待用户指示 |

### 6.2 中断后续跑衔接（从任意切片启动）

若实施中途被中断，之后从**本切片（或任意切片）HANDOVER 启动**，Agent 必须：

| 步骤 | 动作 |
|------|------|
| 1 | **检查解决方案是否存在**：`XiaoShuTong.sln` 不存在 → 按切片 01 §五 步骤 0 创建（仅首次需要） |
| 2 | **检查本切片的前序切片实体是否已编码**（逐项）：<br>| 1 | 切片 01（群组管理域）已实施：`Entities/Groups/*.cs` 存在 | |
| 3 | **前序未编码** → 提醒用户："检测到前序切片尚未实施，需要先完成其编码序列" → 询问是否跳转执行前序 HANDOVER，或由用户决定 |
| 4 | **前序已编码** → 直接执行本切片 §五 编码启动序列 |

> **切片依赖链**：01（群组）→ 02（学习）→ 03（题库判题）→ 04（家校任务）→ 05（可视化激励，依赖 02）→ 06（排行榜搭子，依赖 02/03）→ 07（PK，依赖 03/06）→ 08（家长报告，依赖 02/04）

### 6.3 完成后状态

- 本切片实施产物（Entity/DataService/Service/测试）已在解决方案中，版本已提交
- 后续切片 Agent 读本 HANDOVER §五 步骤 0 即知本切片已实施（`Entities/Learning/*.cs` 存在）

---

## 附录：8 项交接条件自查（DG-01 §2.4）

| # | 条件 | 验证方式 | 结果 |
|:-:|------|---------|:----:|
| 1 | U-series 所有 Use-Case 的契约签名已定义（含 DTO 字段类型） | 审查 | ✅ 8 UC 契约签名 + Req/Res DTO 完整 |
| 2 | 所有 BR-xx 规则已列全，无「待定」「后续补充」 | 审查 | ✅ BR-01~50 全覆盖（含四阶状态机全迁移矩阵 BR-11~21） |
| 3 | 所有 Use-Case 引用的 DataService 已在 DS 中定义 | 交叉核对 | ✅ 6 实体均有对应 DataService；跨模块引用已标注（任务域/题库域/判题服务） |
| 4 | 所有跨模块依赖已标注 | 交叉核对 | ✅ 依赖模块 1（任务）、3（题库）、5（判题）已标注于 UC 头/编排逻辑/实施总览 |
| 5 | 所有 Alternative Flow 至少有一条对应的 BR-xx | 审查 | ✅ 每条替代流均有 BR 对应 |
| 6 | 所有 R-series 约束条件的交叉引用均指向已存在功能点 | 交叉核对 | ✅ R01 约束均在 U01 有 BR |
| 7 | 交叉模块影响扫描已完成 | 审查 | ✅ R01「交叉模块影响扫描」覆盖模块 1/5/6/7/10；待任务域/统计域 R-doc 接收 |
| 8 | 所有 DS 实体的子域标注与 U 文档 UC 头子域一致 | 交叉核对 | ✅ 全部 `Learning` 一致 |

**⚠️ 补充说明**：条件 1~8 在"设计文档"层面全部满足。但学习域待确认项 3 处（见下节）不阻断设计交接，阻断的是实施阶段特定细节——已在 §四/§五 前置确认标注。

---

## 待确认项清单

| # | 项 | 影响 | 建议 |
|:-:|---|------|------|
| 1 | **复习队列视图读取方式**：D02 定义 SQL VIEW，但 DataService 不跨实体 | 数据访问实现 | 由 GetReviewQueueService 直接读视图，或按 MemoryStates 条件过滤内存实现——实施阶段确认 |
| 2 | ~~分页约定不一致（limit vs page）~~ | 前端契约 | **已决策（2026-09-06，对齐 DG-06-附录 存量 API 不一致规则）**：学习域统一 page/size；D03 源文档回改待排期，实施以 U01 为准 |
| 3 | 1001 message 措辞：§2.4 `"未登录或令牌已过期"` vs §3.2 `"未登录/令牌失效"` | 错误文案 | 建议以 §2.4 运行时 message 为准 |

---

> *文档版本：第一阶段 v1.1*
> *编制日期：2026-09-06*
> *同步版本：R01 v1.1 / S01 v1.1 / DS01 v1.1 / U01 v1.1*