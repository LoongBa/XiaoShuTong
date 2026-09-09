# HANDOVER · 家长报告订阅域切片交接文档

> **定位**：设计阶段→实施阶段的承上启下文档（切片验证版）。Agent 阅读本文档后完成自引导加载后续动作——AC-Kit。
> **项目**：小书童（XiaoShuTong）· 家长报告订阅域切片
> **范围**：模块 10（家长成长报告与订阅：成长总览/进度趋势/薄弱点/付费订阅），切片序号 08（**收官切片**；前序：01 群组管理域、02 学习Session域、03 题库判题域、04 家校任务闭环域、05 可视化激励域、06 排行榜搭子域、07 搭子PK竞技域）
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

- **R-series**：`docs/草稿/切片验证-家长报告订阅域/R01-需求与功能设计-家长成长报告.md`
- **S-series**：`docs/草稿/切片验证-家长报告订阅域/S01-场景描述-家长成长报告.md`
- **DS-series**：`docs/草稿/切片验证-家长报告订阅域/DS01-数据结构设计-家长成长报告.md`
- **U-series**：`docs/草稿/切片验证-家长报告订阅域/U01-用况与契约-家长成长报告.md`
- **实施总览**：`docs/草稿/切片验证-家长报告订阅域/家长报告订阅域-切片实施总览.md`

---

## §二 设计规格清单

### UC 跟踪表（Agent 逐项填写）

| UC# | UC 名称 | 子域 | § Entity | § Service | § 测试 | § Schema | § 验证 | 备注 |
|:---:|---------|:----:|:--------:|:---------:|:------:|:--------:|:------:|------|
| 10.1 | 家长关联孩子 | Parent | ☐ | ☐ | ☐ | ☐ | ☐ | |
| 10.2 | 我的孩子列表 | Parent | ☐ | ☐ | ☐ | ☐ | ☐ | |
| 10.3 | 开通试用 | Parent | ☐ | ☐ | ☐ | ☐ | ☐ | 授权链 8002 |
| 10.4 | 订阅（支付回调） | Parent | ☐ | ☐ | ☐ | ☐ | ☐ | Callee |
| 10.5 | 我的订阅列表 | Parent | ☐ | ☐ | ☐ | ☐ | ☐ | |
| 10.6 | 取消续费 | Parent | ☐ | ☐ | ☐ | ☐ | ☐ | |
| 10.7 | 成长总览 | Parent | ☐ | ☐ | ☐ | ☐ | ☐ | 订阅门控 |
| 10.8 | 进度趋势 | Parent | ☐ | ☐ | ☐ | ☐ | ☐ | 合规红线 |
| 10.9 | 薄弱知识点 | Parent | ☐ | ☐ | ☐ | ☐ | ☐ | 按需聚合 |

### Entity → DS 映射表

| Entity | 子域 | 关联 DS 文档 | 文件路径 |
|--------|:----:|:------------:|---------|
| ParentStudentRelations | Parent | DS01 | `Entities/Parent/ParentStudentRelations.cs` |
| Subscriptions | Parent | DS01 | `Entities/Parent/Subscriptions.cs` |

### Service 跟踪表

| Service 类 | 子域 | 关联 UC | 备注 |
|-----------|:----:|:-------:|------|
| CreateParentRelationService | Parent | 10.1 | [GenerateController] |
| ListChildrenService | Parent | 10.2 | [GenerateController] |
| StartTrialService | Parent | 10.3 | [GenerateController] |
| ActivateSubscriptionService | Parent | 10.4 | Callee（支付回调，无 Controller） |
| ListSubscriptionsService | Parent | 10.5 | [GenerateController] |
| CancelSubscriptionService | Parent | 10.6 | [GenerateController] |
| GetDashboardReportService | Parent | 10.7 | [GenerateController] |
| GetProgressReportService | Parent | 10.8 | [GenerateController] |
| GetWeaknessReportService | Parent | 10.9 | [GenerateController] |

### 测试跟踪表

| 测试类 | 关联 Service | 重点 |
|-------|-------------|------|
| StartTrialServiceTests | StartTrialService | 授权链 8002/试用 7 天/幂等 |
| ActivateSubscriptionServiceTests | ActivateSubscriptionService | 回调激活/月年周期/幂等 |
| GetDashboardReportServiceTests | GetDashboardReportService | **订阅门控（前2项+locked）**/链式聚合/合规 |
| GetProgressReportServiceTests | GetProgressReportService | 相对进步计算/合规无排名/4001 |
| GetWeaknessReportServiceTests | GetWeaknessReportService | 订阅门控/按需聚合/状态映射 |
| CancelSubscriptionServiceTests | CancelSubscriptionService | 置 cancelled/权益保留/非本人 |

---

## §三 服务契约一览

> 完整表格见 `家长报告订阅域-切片实施总览.md`（9 个 Service，BR-01~28，CROSS 事务 1 处：10.4，跨模块依赖 4 项：账户/学习/任务/微信支付）

---

## §四 项目约定

| 项 | 值 |
|----|-----|
| 项目名 | XiaoShuTong |
| 切片范围 | 家长成长报告与订阅域（模块 10）；切片 08（**收官**；前序：01~07） |
| 子域 | `Parent` |
| 用户类型 | 群主(owner)/学生(student)/家长(parent)；本域均为 parent |
| 开发路线 | WebApi（切片验证，全量待定） |
| 测试模式 | 内存 DAC（切片验证） |
| 审计字段命名 | `CreateTime`/`UpdateTime`（存量 CreatedAt 转化 + 补齐） |
| 枚举 ORM 映射 | `[Column(MapType = typeof(string))]`，PascalCase 存字符串（存量小写 → 迁移映射） |
| 错误码 | 数字域码 80xx（订阅）/ 40xx（统计）/ 10xx（全局）+ SNAKE_CASE 双列（迁移决策） |
| 主键决策 | 系统内部 `long Id`；外部关联/跨系统 `Uid`（uuid 业务键），实体表补 Uid 列 |
| API 命名 | JSON 字段 camelCase（D03 原则 #6）；SNAKE_CASE 仅错误码语义名 |
| 授权链 | 订阅/报告前应用层校验 ParentStudentRelations（8002） |
| 框架路径 | `$env:TKWFDeployPath` |

---

## §五 编码启动序列（切片验证）

| 步骤 | 动作 | Skill / 工具 | 产出 | 完成标志 |
|:----:|------|-------------|------|---------|
| 0 | **跨切片依赖启动检查**：确认学习域（切片 02 Entity + DataService 产物）与任务域（切片 04）已编码 | 前序切片 | 依赖就绪 | `DataServices/*.g.cs` 存在 |
| 1 | 读 DS01 -> 写 2 个 Entity.cs | tkwf-entity | `Entities/Parent/*.cs` | 文件存在 |
| 2 | `dotnet build` | - | xCodeGen 生成 DataService/DTO/Conditions | `.g.cs` 存在 |
| 3 | 读 U01 -> 写 9 个 Service.cs | tkwf-service | `Services/Parent/*.cs` | 文件存在 |
| 4 | 读 U01 BR -> 写测试 | tkwf-test | `*ServiceTests.cs` | 文件存在 |
| 5 | `dotnet build` | - | 全项目编译 | 0 error |
| 6 | `dotnet test` | - | 测试执行 | 全部通过 |

> ⚠️ **前置依赖**：本域报告 UC 依赖 `ILearning*DataService`（学习域）+ `ITaskAssignmentsDataService`（任务域）——前序切片需先编码。

---

## §六 衔接与继续执行

> **实施 Agent 必读**：本切片执行完毕后，按下述逻辑衔接后续切片。**多切片交接纪律**：每个切片完成且验证通过后，**询问用户是否继续执行下一个切片，得到同意后才继续**；不得静默连跑。

### 6.1 执行完毕询问

| 状态 | 动作 |
|------|------|
| 本切片全部步骤完成 + 测试通过 | 向用户报告：**全部 8 切片实施完成（（收官切片——全部 8 切片实施完成））** → 执行「切片收官聚合检查」（DG-01 三·补）→ 进入整体验收 |
| 用户同意 | 读下一个切片 HANDOVER → 执行其 §五 编码启动序列 |
| 用户暂缓/暂停 | 保存当前状态（本切片 HANDOVER + 实施产物在解决方案中），等待用户指示 |

### 6.2 中断后续跑衔接（从任意切片启动）

若实施中途被中断，之后从**本切片（或任意切片）HANDOVER 启动**，Agent 必须：

| 步骤 | 动作 |
|------|------|
| 1 | **检查解决方案是否存在**：`XiaoShuTong.sln` 不存在 → 按切片 01 §五 步骤 0 创建（仅首次需要） |
| 2 | **检查本切片的前序切片实体是否已编码**（逐项）：<br>| 1 | 切片 02（学习Session域）已实施：`Entities/Learning/*.cs` 存在 |
| 2 | 切片 04（家校任务闭环域）已实施：`Entities/Tasks/*.cs` 存在 | |
| 3 | **前序未编码** → 提醒用户："检测到前序切片尚未实施，需要先完成其编码序列" → 询问是否跳转执行前序 HANDOVER，或由用户决定 |
| 4 | **前序已编码** → 直接执行本切片 §五 编码启动序列 |

> **切片依赖链**：01（群组）→ 02（学习）→ 03（题库判题）→ 04（家校任务）→ 05（可视化激励，依赖 02）→ 06（排行榜搭子，依赖 02/03）→ 07（PK，依赖 03/06）→ 08（家长报告，依赖 02/04）

### 6.3 完成后状态

- 本切片实施产物（Entity/DataService/Service/测试）已在解决方案中，版本已提交
- 后续切片 Agent 读本 HANDOVER §五 步骤 0 即知本切片已实施（`Entities/Parent/*.cs` 存在）

---

## 附录：8 项交接条件自查（DG-01 §2.4）

| # | 条件 | 验证方式 | 结果 |
|:-:|------|---------|:----:|
| 1 | U-series 所有 Use-Case 的契约签名已定义（含 DTO 字段类型） | 审查 | ✅ 9 UC 契约签名 + Req/Res DTO 完整（10.5 全量无分页注明） |
| 2 | 所有 BR-xx 规则已列全，无「待定」「后续补充」 | 审查 | ✅ BR-01~28 全覆盖 |
| 3 | 所有 Use-Case 引用的 DataService 已在 DS 中定义 | 交叉核对 | ✅ 2 实体均有对应 DataService（ParentStudentRelations/Subscriptions）；消费实体跨模块已标注 |
| 4 | 所有跨模块依赖已标注 | 交叉核对 | ✅ 账户/学习/任务/微信支付（UC 头/编排逻辑/实施总览三处一致） |
| 5 | 所有 Alternative Flow 至少有一条对应的 BR-xx | 审查 | ✅ 每条替代流均有 BR 对应 |
| 6 | 所有 R-series 约束条件的交叉引用均指向已存在功能点 | 交叉核对 | ✅ R01 约束均在 U01 有 BR |
| 7 | 交叉模块影响扫描已完成 | 审查 | ✅ R01「交叉模块影响扫描」覆盖模块 1/4/6 |
| 8 | 所有 DS 实体的子域标注与 U 文档 UC 头子域一致 | 交叉核对 | ✅ 全部 `Parent` 一致 |

**⚠️ 补充说明**：条件 1~8 在"设计文档"层面全部满足。但存在 3 类非阻断缺口需实施前确认（见下节）：vsLastWeek 天数字段、篇目下钻 API、订阅到期扫描作业。

---

## 已决策项（本切片新增）

| # | 决策 | 说明 |
|:-:|------|------|
| 1 | vsLastWeek 用既有两字段 | learnedDelta + weaknessShift（D03）；坚持天数在 dashboard 展示；向 D03 提议补充天数字段 |
| 2 | 篇目下钻=前端跳转学生端 | D03 无下钻子接口；本期标注缺口 |
| 3 | 家长域列表全量返回 | 孩子/订阅/薄弱点数据量小，不分页；标注契约缺口 |
| 4 | 空态三步式补充 | 家长无数据三步式（发生了什么/为什么/下一步） |
| 5 | 1001 message 以 §2.4 为准 | 运行时"未登录或令牌已过期" |
| 6 | 报告无数据返回 4001 | 对齐统计域惯例（D03 §11 未声明） |

## 待确认项清单

| # | 项 | 影响 | 建议 |
|:-:|---|------|------|
| 1 | **vsLastWeek 坚持天数字段** | 相对进步完整性 | 向 D03 提议 §11.6 补充 `streakDelta` 字段 |
| 2 | **篇目下钻 API** | 薄弱点下钻交互 | 前端跳学生端篇目 或 D03 补充子接口 |
| 3 | **订阅到期扫描作业** | 到期自动置 expired | 本期由报告访问时惰性判定（请求时校验 TrialEndAt/PeriodEndAt）；批量扫描作业列入开发方案 |
| 4 | **订阅 UI 独立页面**（开通弹窗/支付/管理） | V2.2 无页面规格 | 前端按归档 §11 时序实现；向 UI 提补规格 |
| 5 | **微信支付集成** | 回调契约 | 实施阶段对接支付平台回调签名验证 |

---

> *文档版本：第一阶段 v1.0*
> *编制日期：2026-09-06*
> *同步版本：R01 v1.0 / S01 v1.0 / DS01 v1.0 / U01 v1.0*