# HANDOVER · 可视化激励域切片交接文档

> **定位**：设计阶段→实施阶段的承上启下文档（切片验证版）。Agent 阅读本文档后完成自引导加载后续动作——AC-Kit。
> **项目**：小书童（XiaoShuTong）· 可视化激励域切片
> **范围**：模块 6（可视化激励：热力图/连续天数/报告/错题本/分享战报/学习路线图），切片序号 05（前序：01 群组管理域、02 学习Session域、03 题库判题域、04 家校任务闭环域）
> **更新日期**：2026-09-06

---

## 文档流总览

```
R01(需求)→ S01(场景)→ DS01(数据结构)→ U01(用况与契约)

DS01 ──→ 无新增实体（复用切片 02 Learning 实体 + 切片 02 DataService）
   └──→ 仅查询 DTO 由本域定义（DTO 不建表）

U01 ──→ Service + [GenerateController]（手写） ← SG 自动生成接口，不手写
   └──→ 服务契约一览（并入实施总览） + 本 HANDOVER
```

> ⚠️ **必须执行顺序**：`切片02 DS01 实体已存在 → U01 Service 编写 → xCodeGen（复用） → 测试`
> - **本域无新增 Entity**：直接复用 `Entities/Learning/*.cs`（切片 02）——不要重新创建 Entity
> - 若切片 02 尚未编码实施，则本域 Service 依赖的 `ILearning*DataService` 不存在——需先完成切片 02 的 Entity + xCodeGen

---

## §一 输入文档清单

- **R-series**：`docs/草稿/切片验证-可视化激励域/R01-需求与功能设计-可视化激励.md`
- **S-series**：`docs/草稿/切片验证-可视化激励域/S01-场景描述-可视化激励.md`
- **DS-series**：`docs/草稿/切片验证-可视化激励域/DS01-数据结构设计-可视化激励.md`（复用声明）
- **U-series**：`docs/草稿/切片验证-可视化激励域/U01-用况与契约-可视化激励.md`
- **实施总览**：`docs/草稿/切片验证-可视化激励域/可视化激励域-切片实施总览.md`

---

## §二 设计规格清单

### UC 跟踪表（Agent 逐项填写）

| UC# | UC 名称 | 子域 | § Entity | § Service | § 测试 | § Schema | § 验证 | 备注 |
|:---:|---------|:----:|:--------:|:---------:|:------:|:--------:|:------:|------|
| 6.1 | 查看记忆热力图 | Stats | 复用 | ☐ | ☐ | — | ☐ | 跨学习域读 DailyStats |
| 6.2 | 查看连续天数 | Stats | 复用 | ☐ | ☐ | — | ☐ | |
| 6.3 | 查看学习报告 | Stats | 复用 | ☐ | ☐ | — | ☐ | |
| 6.4 | 查看错题本 | Stats | 复用 | ☐ | ☐ | — | ☐ | 只读 |
| 6.5 | 分享战报 | — | — | — | — | — | — | 前端模拟，无 Service |

### Entity → DS 映射表

| Entity | 子域 | 关联 DS 文档 | 文件路径 | 状态 |
|--------|:----:|:------------:|---------|:----:|
| （无新增实体） | — | 复用切片 02 DS01 | `Entities/Learning/*.cs`（已有） | **复用** |
| （查询 DTO 全部复用 {Entity}Dto，无手写 DTO） | Stats | 本切片 DS01 §二 | `Entities/DTOs/Learning/*.g.cs`（自动生成） | DTO 最小化：HeatmapDayDto/TrendPointDto/WrongQuestionItemDto 均已替换 |

### Service 跟踪表

| Service 类 | 子域 | 关联 UC | 备注 |
|-----------|:----:|:-------:|------|
| GetHeatmapService | Stats | 6.1 | [GenerateController] |
| GetStreakService | Stats | 6.2 | [GenerateController] |
| GetPeriodReportService | Stats | 6.3 | [GenerateController] |
| GetWrongQuestionsService | Stats | 6.4 | [GenerateController] |

### 测试跟踪表

| 测试类 | 关联 Service | 重点 |
|-------|-------------|------|
| GetHeatmapServiceTests | GetHeatmapService | 按日聚合/参数/RLS/空数据 |
| GetStreakServiceTests | GetStreakService | 连续性派生/断更清零/最长保留 |
| GetPeriodReportServiceTests | GetPeriodReportService | 周期聚合/合规无排名/4001 |
| GetWrongQuestionsServiceTests | GetWrongQuestionsService | 分组过滤/分页/空态/只读 |

---

## §三 服务契约一览

> 完整表格见 `可视化激励域-切片实施总览.md`（4 个 Service + 1 前端模拟，BR-01~18，全部 SINGLE，跨模块依赖 2 项：学习域/题库域）

---

## §四 项目约定

| 项 | 值 |
|----|-----|
| 项目名 | XiaoShuTong |
| 切片范围 | 可视化激励域（模块 6）；切片 05（前序：01 群组管理、02 学习Session、03 题库判题、04 家校任务闭环） |
| 子域 | `Stats`（查询编排）；实体归属 `Learning`（复用切片 02） |
| 用户类型 | 群主(owner)/学生(student)/家长(parent)；本域均为 student |
| 开发路线 | WebApi（切片验证，全量待定） |
| 测试模式 | 内存 DAC（切片验证） |
| 审计字段命名 | 复用切片 02（CreateTime/UpdateTime） |
| 枚举 ORM 映射 | 复用切片 02（PascalCase） |
| 错误码 | 数字域码 4001（统计）+ 10xx 全局 + SNAKE_CASE 双列；4002 [Proposed] |
| 主键决策 | 复用切片 02（内部 long + 外部 Uid） |
| API 命名 | JSON 字段 camelCase（D03 原则 #6） |
| 框架路径 | `$env:TKWFDeployPath` |

---

## §五 编码启动序列（切片验证）

| 步骤 | 动作 | Skill / 工具 | 产出 | 完成标志 |
|:----:|------|-------------|------|---------|
| 0 | 确认切片 02 实体已编码（Entities/Learning/*.cs + DataService 已生成） | tkwf-entity（前序） | 复用 | 文件存在 |
| 1 | 写本域查询 DTO（DTOs/Stats/） | tkwf-entity | `Dtos/Stats/*.cs` | 文件存在 |
| 2 | 读 U01 -> 写 4 个 Service.cs | tkwf-service | `Services/Stats/*.cs` | 文件存在 |
| 3 | 读 U01 BR -> 写测试 | tkwf-test | `*ServiceTests.cs` | 文件存在 |
| 4 | `dotnet build` | - | 全项目编译 | 0 error |
| 5 | `dotnet test` | - | 测试执行 | 全部通过 |

> ⚠️ **前置依赖**：本域 Service 依赖 `ILearning*DataService`（切片 02 xCodeGen 产物）——若切片 02 未编码，需先执行切片 02 编码序列（HANDOVER-学习Session域 §五）。

---

## §六 衔接与继续执行

> **实施 Agent 必读**：本切片执行完毕后，按下述逻辑衔接后续切片。**多切片交接纪律**：每个切片完成且验证通过后，**询问用户是否继续执行下一个切片，得到同意后才继续**；不得静默连跑。

### 6.1 执行完毕询问

| 状态 | 动作 |
|------|------|
| 本切片全部步骤完成 + 测试通过 | 向用户报告完成摘要（Entity/Service/测试数 + 编译测试结果）→ **询问用户：是否继续执行下一个切片（切片 06（排行榜搭子域））？** |
| 用户同意 | 读下一个切片 HANDOVER → 执行其 §五 编码启动序列 |
| 用户暂缓/暂停 | 保存当前状态（本切片 HANDOVER + 实施产物在解决方案中），等待用户指示 |

### 6.2 中断后续跑衔接（从任意切片启动）

若实施中途被中断，之后从**本切片（或任意切片）HANDOVER 启动**，Agent 必须：

| 步骤 | 动作 |
|------|------|
| 1 | **检查解决方案是否存在**：`XiaoShuTong.sln` 不存在 → 按切片 01 §五 步骤 0 创建（仅首次需要） |
| 2 | **检查本切片的前序切片实体是否已编码**（逐项）：<br>| 1 | 切片 02（学习Session域）已实施：`Entities/Learning/*.cs` + `DataServices/ILearning*DataService.g.cs` 存在（本域复用其实体） | |
| 3 | **前序未编码** → 提醒用户："检测到前序切片尚未实施，需要先完成其编码序列" → 询问是否跳转执行前序 HANDOVER，或由用户决定 |
| 4 | **前序已编码** → 直接执行本切片 §五 编码启动序列 |

> **切片依赖链**：01（群组）→ 02（学习）→ 03（题库判题）→ 04（家校任务）→ 05（可视化激励，依赖 02）→ 06（排行榜搭子，依赖 02/03）→ 07（PK，依赖 03/06）→ 08（家长报告，依赖 02/04）

### 6.3 完成后状态

- 本切片实施产物（Entity/DataService/Service/测试）已在解决方案中，版本已提交
- 后续切片 Agent 读本 HANDOVER §五 步骤 0 即知本切片已实施（`Entities/Stats/*.cs` 存在）

---

## 附录：8 项交接条件自查（DG-01 §2.4）

| # | 条件 | 验证方式 | 结果 |
|:-:|------|---------|:----:|
| 1 | U-series 所有 Use-Case 的契约签名已定义（含 DTO 字段类型） | 审查 | ✅ 4 UC 契约签名 + Req/Res DTO 完整（6.5 前端模拟注明） |
| 2 | 所有 BR-xx 规则已列全，无「待定」「后续补充」 | 审查 | ✅ BR-01~18 全覆盖 |
| 3 | 所有 Use-Case 引用的 DataService 已在 DS 中定义 | 交叉核对 | ✅ 实体复用切片 02（ILearning*DataService）；题库 Questions 已标跨模块 |
| 4 | 所有跨模块依赖已标注 | 交叉核对 | ✅ 学习域/题库域（UC 头/编排逻辑/实施总览三处一致） |
| 5 | 所有 Alternative Flow 至少有一条对应的 BR-xx | 审查 | ✅ 每条替代流均有 BR 对应 |
| 6 | 所有 R-series 约束条件的交叉引用均指向已存在功能点 | 交叉核对 | ✅ R01 约束均在 U01 有 BR |
| 7 | 交叉模块影响扫描已完成 | 审查 | ✅ R01「交叉模块影响扫描」覆盖模块 3/4/7/10 |
| 8 | 所有 DS 实体的子域标注与 U 文档 UC 头子域一致 | 交叉核对 | ✅ 查询子域 `Stats` 一致；实体复用 `Learning` 有显式声明 |

**⚠️ 补充说明**：条件 1~8 在"设计文档"层面全部满足。但存在 **2 类非阻断缺口**需实施前确认（见下节）：学习路线图 UI 缺失、分享战报前端模拟边界。

---

## 已决策项（本切片新增）

| # | 决策 | 说明 |
|:-:|------|------|
| 1 | 实体复用切片 02 | 本域纯消费展示，不新建实体/DataService（DS01 复用声明） |
| 2 | 时间范围预设前端换算 | D03 仅 start/end，预设（本周/本月/本季度/今年）前端转区间 |
| 3 | 分享战报前端模拟 | MVP 无后端 API（D03 无分享端点）；真实图片生成 Phase 2 |
| 4 | 学生端报告不含 vsLastWeek | D03 §7.3 无此字段（家长域 §11.6 有）——待扩展 |
| 5 | 错题重练复用学习域会话 | 不新建重练端点 |
| 6 | 错题分页统一 page/size | 对齐切片 02 决策 |
| 7 | 4001 语义名 NO_STATS_DATA | U01 推导（D03 仅中文） |
| 8 | 学习路线图只录功能点 | UI 文档缺失，本期不实现 UC |

## 待确认项清单

| # | 项 | 影响 | 建议 |
|:-:|---|------|------|
| 1 | **学习路线图 UI 规格缺失**：V2.2 无此页（仅 PRD 有"单元列表+完成率进度条+解锁状态"） | 页面/路由/状态待 UI 补 | 向 UI 提规格；或本期砍掉（功能点保留） |
| 2 | **分享战报前端模拟边界**：4 风格视觉/各分享方式反馈态/弹窗关闭方式均未定义 | 前端组件 | 按 PRD L195 最小实现；视觉风格待 UI 细化 |
| 3 | **错题空态文案不一致**："没有错题，记得很牢！"（页面级）vs "说明都会了"（三步式模板） | 文案 | 建议三步式模板为准（对齐 §4 规范） |
| 4 | **4002 周期非法 [Proposed]**：新增统计域错误码需 D03 V1.4 收录 | API 契约 | 业务确认后回改 D03 |

---

> *文档版本：第一阶段 v1.0*
> *编制日期：2026-09-06*
> *同步版本：R01 v1.0 / S01 v1.0 / DS01 v1.0 / U01 v1.0*