# HANDOVER · 群组管理域切片交接文档

> **定位**：设计阶段→实施阶段的承上启下文档（切片验证版）。Agent 阅读本文档后完成自引导加载后续动作——AC-Kit。
> **项目**：小书童（XiaoShuTong）· 群组管理域切片
> **范围**：模块 6（群组管理 + 内测邀请），仅验证路径 B 转化衔接
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

- **R-series**：`docs/草稿/切片验证-群组管理域/R01-需求与功能设计-群组管理.md`
- **S-series**：`docs/草稿/切片验证-群组管理域/S01-场景描述-群组管理.md`
- **DS-series**：`docs/草稿/切片验证-群组管理域/DS01-数据结构设计-群组管理.md`
- **U-series**：`docs/草稿/切片验证-群组管理域/U01-用况与契约-群组管理.md`
- **实施总览**：`docs/草稿/切片验证-群组管理域/群组管理域-切片实施总览.md`

---

## §二 设计规格清单

### UC 跟踪表（Agent 逐项填写）

| UC# | UC 名称 | 子域 | § Entity | § Service | § 测试 | § Schema | § 验证 | 备注 |
|:---:|---------|:----:|:--------:|:---------:|:------:|:--------:|:------:|------|
| 6.1 | 激活建群 | Groups | ☐ | ☐ | ☐ | ☐ | ☐ | CROSS |
| 6.2 | 群组列表 | Groups | ☐ | ☐ | ☐ | ☐ | ☐ | |
| 6.3 | 详情与成员管理 | Groups | ☐ | ☐ | ☐ | ☐ | ☐ | |
| 6.4 | 排名开关 | Groups | ☐ | ☐ | ☐ | ☐ | ☐ | |
| 6.5 | 导入名单 | Groups | ☐ | ☐ | ☐ | ☐ | ☐ | |
| 6.6 | Agent 整理 | Groups | ☐ | ☐ | ☐ | ☐ | ☐ | BackgroundJob |
| 6.7 | 整理预览 | Groups | ☐ | ☐ | ☐ | ☐ | ☐ | |
| 6.8 | 生成一次性码 | Groups | ☐ | ☐ | ☐ | ☐ | ☐ | CROSS |
| 6.9 | 导出 CSV | Groups | ☐ | ☐ | ☐ | ☐ | ☐ | |
| 6.10 | 成员激活 | Groups | ☐ | ☐ | ☐ | ☐ | ☐ | CROSS |

### Entity → DS 映射表

| Entity | 子域 | 关联 DS 文档 | 文件路径 |
|--------|:----:|:------------:|---------|
| Groups | Groups | DS01 | `Entities/Groups/Groups.cs` |
| GroupMembers | Groups | DS01 | `Entities/Groups/GroupMembers.cs` |
| BetaInviteCodes | Groups | DS01 | `Entities/Groups/BetaInviteCodes.cs` |
| OneTimeInviteCodes | Groups | DS01 | `Entities/Groups/OneTimeInviteCodes.cs` |
| RosterImports | Groups | DS01 | `Entities/Groups/RosterImports.cs` |

### Service 跟踪表

| Service 类 | 子域 | 关联 UC | 备注 |
|-----------|:----:|:-------:|------|
| CreateGroupService | Groups | 6.1 | [GenerateController] |
| ListGroupsService | Groups | 6.2 | [GenerateController] |
| ManageGroupMembersService | Groups | 6.3 | [GenerateController] |
| SetRankEnabledService | Groups | 6.4 | [GenerateController] |
| ImportRosterService | Groups | 6.5 | [GenerateController] |
| RosterCleanupJob | Groups | 6.6 | BackgroundJob，无 Controller |
| GetRosterPreviewService | Groups | 6.7 | [GenerateController] |
| GenerateInviteCodesService | Groups | 6.8 | [GenerateController] |
| ExportRosterCsvService | Groups | 6.9 | [GenerateController] |
| ActivateMemberService | Groups | 6.10 | [GenerateController] |

### 测试跟踪表

| 测试类 | 关联 Service | 重点 |
|-------|-------------|------|
| CreateGroupServiceTests | CreateGroupService | 码校验/一码一群组/CROSS 回滚 |
| ManageGroupMembersServiceTests | ManageGroupMembersService | 权限/不删学习数据 |
| ActivateMemberServiceTests | ActivateMemberService | 幂等/后四位校验/CROSS 回滚 |
| GenerateInviteCodesServiceTests | GenerateInviteCodesService | 每人一码/唯一/30 天 |
| RosterCleanupJobTests | RosterCleanupJob | 去重/非法剔除/幂等 |

---

## §三 服务契约一览

> 完整表格见 `群组管理域-切片实施总览.md`（10 个 Service，BR-01~31，CROSS 事务 4 处：6.1/6.8/6.10 + 6.6 为 Job 模式 C）

---

## §四 项目约定

| 项 | 值 |
|----|-----|
| 项目名 | XiaoShuTong |
| 切片范围 | 群组管理域 + 内测邀请域（模块 6） |
| 子域 | `Groups` |
| 用户类型 | 群主(owner)/学生(student)/家长(parent) |
| 开发路线 | 经 §五 实施偏好确认 2 填写（**建议 `WebApiBlazorWasm`**：WebApi 供 React 主前端 + Wasm 平台管理端；切片验证时暂用 WebApi） |
| 测试模式 | 内存 DAC（切片验证） |
| 审计字段命名 | `CreateTime`/`UpdateTime`（存量 DDL 为 CreatedAt/UpdatedAt，需迁移） |
| 枚举 ORM 映射 | `[Column(MapType = typeof(string))]`，PascalCase 存字符串（存量小写需迁移） |
| 错误码 | 数字域码 52xx/50xx + SNAKE_CASE 语义名双列（迁移决策） |
| 框架路径 | `$env:TKWFDeployPath` |

---

## §五 编码启动序列（切片验证）

> ⚠️ **主键决策（已定）**：系统内部用 `long Id` 主键/外键；外部关联、跨系统用 `Uid`（uuid 业务键）。实体表补 `Uid` 列（唯一索引），API 参数/响应/DTO 暴露 Uid——见 DS01「存量差异标注」①。

### 实施偏好确认（创建解决方案前必问）

> ⚠️ **实施 Agent 必读**：进入 §五 编码启动序列前（即创建解决方案之前），必须先向用户确认以下偏好——**不得静默采用默认值**。

| # | 询问项 | 选项 | 默认 |
|:-:|--------|------|:----:|
| 1 | **测试编写节奏**：编写领域（Domain）的同时**同步创建测试**，还是在领域全部完成后**一次性创建测试**？ | A) 同步创建（每 Entity/Service 即写对应测试）<br>B) 一次性创建（Domain 全部完成后统一写测试） | **B) 一次性创建** |
| 2 | **解决方案模式（create-new-solution.ps1 的 `-Mode` 参数）**：项目包含哪些端？ | `WebApiBlazorWasm`（WebApi + Blazor Wasm 双项目——**XiaoShuTong 建议值**：WebApi 供 React 主前端 + Wasm 平台管理端）<br>`WebApi`（仅后端 API）<br>`BlazorWeb`（Blazor Web）<br>`DomainOnly`（仅领域层） | 用户确认后填写，**不写死**（建议 `-Mode WebApiBlazorWasm`） |

**测试编写节奏影响**：
- **A 同步创建**：步骤 1~4 中每完成一个 Entity/Service 即写对应 `*ServiceTests.cs`——测试随写随验，适合测试驱动（TDD）节奏
- **B 一次性创建（默认）**：先完成 步骤 1~3（Entity → xCodeGen → Service）全部领域代码，再在 步骤 4 一次性写全部测试——适合先跑通编译、再统一补测试验证

**确认后按所选偏好执行**；若用户未明确选择，**默认采用 B（一次性创建）+ `-Mode WebApi`**（模式确认后填入步骤 0 命令，不写死）。

> **实施 skill 位置**：tkwf-entity / tkwf-service / tkwf-test 的 SKILL.md 位于 `$env:TKWFDeployPath\docs\AC-Kit\skills\{skill名}\SKILL.md`——按步骤 Skill 列加载对应 skill 执行，不得手写绕过。

| 步骤 | 动作 | Skill / 工具 | 产出 | 完成标志 |
|:----:|------|-------------|------|---------|
| 0 | **按需创建解决方案**：检查 `XiaoShuTong.sln` 是否存在——不存在则运行 `$env:TKWFDeployPath\docs\AC-Kit\scripts\create-new-solution.ps1 -Name XiaoShuTong -Mode {实施偏好确认 2 的确认值}`（建议 `WebApiBlazorWasm`）；**已存在则跳过创建，直接复用** | create-new-solution.ps1 | 解决方案 + Domain/WebApi(/Wasm)/Tests 项目骨架 | `.sln` 存在 |
| 1 | 读 DS01 -> 写 5 个 Entity.cs（**用真实 Entity 原子替换 FakeEntity 全家桶**：写首个 `{Entity}.cs`（标注 `[DomainGenerateCode]`）同时移除 `Entities\FakeEntity*` + `Entities\DTOs\FakeEntityDto*` + `Entities\Conditions\FakeEntity*` + `DataServices\FakeEntityDataService*`，或运行 `create-new-solution.ps1 -CleanupFakeEntity`；⚠️ 勿只删 FakeEntity.cs 留空实体状态——无 `[DomainGenerateCode]` 实体时 SG1 不生成 ProjectMetaContext，Domain 编译失败） | tkwf-entity | `Entities/Groups/*.cs` | 文件存在 |
| 2 | `dotnet build` | - | xCodeGen 生成 DataService/DTO/Conditions | `.g.cs` 存在 |
| 3 | 读 U01 -> 写 10 个 Service.cs | tkwf-service | `Services/Groups/*.cs` | 文件存在 |
| 4 | 读 U01 BR -> 写测试（按已确认的测试编写节奏） | tkwf-test | `*ServiceTests.cs` | 文件存在 |
| 5 | `dotnet build` | - | 全项目编译 | 0 error |
| 6 | `dotnet test` | - | 测试执行 | 全部通过 |

---

## §六 衔接与继续执行

> **实施 Agent 必读**：本切片执行完毕后，按下述逻辑衔接后续切片。**多切片交接纪律**：每个切片完成且验证通过后，**询问用户是否继续执行下一个切片，得到同意后才继续**；不得静默连跑。

### 6.1 执行完毕询问

| 状态 | 动作 |
|------|------|
| 本切片全部步骤完成 + 测试通过 | 向用户报告完成摘要（Entity/Service/测试数 + 编译测试结果）→ **询问用户：是否继续执行下一个切片（切片 02 学习Session域）？** |
| 用户同意 | 读切片 02 HANDOVER → 执行其 §五 编码启动序列 |
| 用户暂缓/暂停 | 保存当前状态（本切片 HANDOVER + 实施产物在解决方案中），等待用户指示 |

### 6.2 中断后续跑衔接（从任意切片启动）

若实施中途被中断，之后从**任意一个切片 HANDOVER 启动**，Agent 必须：

| 步骤 | 动作 |
|------|------|
| 1 | **检查解决方案是否存在**：`XiaoShuTong.sln` 不存在 → 按 §五 步骤 0 创建（仅首次需要） |
| 2 | **检查本切片的前序切片实体是否已编码**：查解决方案中 `Entities/{前序域}/*.cs` + `DataServices/*.g.cs` 是否存在 |
| 3 | **前序未编码** → 提醒用户："检测到前序切片 {前序域} 尚未实施，需要先完成其编码序列" → 询问是否跳转执行前序 HANDOVER，或由用户决定 |
| 4 | **前序已编码** → 直接执行本切片 §五 编码启动序列 |

> **切片依赖链**：01（群组）→ 02（学习）→ 03（题库判题）→ 04（家校任务）→ 05（可视化激励，依赖 02）→ 06（排行榜搭子，依赖 02/03）→ 07（PK，依赖 03/06）→ 08（家长报告，依赖 02/04）

### 6.3 完成后状态

- 本切片实施产物（Entity/DataService/Service/测试）已在解决方案中，版本已提交
- 后续切片 Agent 读本 HANDOVER §五 步骤 0 即知本切片已实施（`Entities/Groups/*.cs` 存在）

---

## 附录：8 项交接条件自查（DG-01 §2.4）

| # | 条件 | 验证方式 | 结果 |
|:-:|------|---------|:----:|
| 1 | U-series 所有 Use-Case 的契约签名已定义（含 DTO 字段类型） | 审查 | ✅ 10 UC 契约签名 + Req/Res DTO 完整 |
| 2 | 所有 BR-xx 规则已列全，无「待定」「后续补充」 | 审查 | ✅ BR-01~31 全覆盖 |
| 3 | 所有 Use-Case 引用的 DataService 已在 DS 中定义 | 交叉核对 | ✅ 5 实体均有对应 DataService |
| 4 | 所有跨模块依赖已标注 | 交叉核对 | ✅ 依赖模块 1（账户/认证）已标注 |
| 5 | 所有 Alternative Flow 至少有一条对应的 BR-xx | 审查 | ✅ 每条替代流均有 BR 对应 |
| 6 | 所有 R-series 约束条件的交叉引用均指向已存在功能点 | 交叉核对 | ✅ R01 约束均在 U01 有 BR |
| 7 | 交叉模块影响扫描已完成 | 审查 | ✅ 本切片影响模块 1/7（排行榜：RankEnabled）已标注 |
| 8 | 所有 DS 实体的子域标注与 U 文档 UC 头子域一致 | 交叉核对 | ✅ 全部 `Groups` 一致 |

**⚠️ 补充说明**：条件 1~8 在"设计文档"层面全部满足。但**存量 DDL 与 TKWF 框架的 3 处差异（主键/审计字段/枚举存储）**不阻断设计交接，阻断的是实施阶段 Entity 编写——已在 §五 前置确认标注。

> *文档版本：第一阶段 v1.1*
> *编制日期：2026-09-06*
