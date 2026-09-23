# Agents_Use_TKWF — XiaoShuTong 开发规则

> 本仓库的**开发规则**。所有 Agent（AI）与人工开发者在本仓库内执行任何开发、文档、版本操作时，必须遵守本文件。
> 本文件由 OpenCode 自动加载（见 `.opencode/opencode.json` 的 instructions 配置）。

---

## 1. 仓库定位

XiaoShuTong 基于 **TKW.Framework 框架**（简称 TKWF）。

- **TKWF 框架**：`F:\TKWF_FRAMEWORK_PATH`
- **引用模式**：Dll（默认）/ NuGet，详见 `.TKWF/TKWF-Rules.md §1`
- **解决方案**：仅 `XiaoShuTong.slnx`（Dll 模式，4 项目）。（勘误：历史文档记载的 `XiaoShuTong.Dev.slnx` 源码联调方案**不存在**，如未来恢复 ProjectRef/源码联调需另行决策，见 `PROJECT_STRUCTURE.md` 勘误备忘。）

## 2. 版本体系

- **产品版本**：MinVer 自动管理，`git tag` 即版本确认。前缀 `V0.`（如 `V0.1.0`）——MinVer 前缀匹配不区分大小写，仓库 tag 惯例大写 `V`；子产品线用项目前缀（`AdminWasm-V0.x.x` / `WebH5-V0.x.x`），见 `docs/AGENTS.md` 核心规则。
- **文档版本**：文件名中**不出现版本号**。

### 版本语义（SemVer，MinVer 三级号 X.Y.Z）

| 位 | 名称 | 触发条件 | 示例 |
|:---:|------|---------|------|
| **X** | 主版本 | 阶段性发布 / 架构重构 / 产品方向调整——**必须申请**（征求同意后才可升级） | V0.x.x → V1.0.0 |
| **Y** | 次版本 | 较大调整 / 一般迭代（新功能、子系统上线、平台级迭代） | V0.1.x → V0.2.0 |
| **Z** | 子版本 | 修复 bug、微小调整（默认值修正、编译修复、契约小改、内容复核） | V0.1.0 → V0.1.1 |

> MinVer 自动递增 **Z**（子版本）；升级 **Y**（次版本）或 **X**（主版本）需显式 tag 并**申请同意**（见 §4）。三线独立版本线互不影响（领域 `V0.x.x` / AdminWasm `AdminWasm-V0.x.x` / WebH5 `WebH5-V0.x.x`）。

## 3. 迭代开发流程

0. **架构变更门禁**：涉及架构决策（技术选型、模块拆分、契约破坏性变更、跨域设计）→ **先提 ADR**（`docs/模板/ADR模板.md`，记录 问题→选项→论证→结论）→ 讨论、审核通过（状态=活跃，登记 `docs/adr/README.md` 总表）后**才允许进入迭代**；纯功能/内容迭代无需 ADR

   **ADR 三问必填**（缺任一小节视为不完整，不予批准，详见 `%TKWFDeployPath%/AC-Kit/guides/ADR编写指南.md`）：
   - **目的与目标**——这个决策要达成什么？读者应在 3 句话内明白目标状态
   - **问题**——为了解决什么问题？必须含问题现象、触发场景、现有方案不足
   - **使用场景**——用于什么场景？列出具体场景，标注不适用边界

   **需要写 ADR 的场景**（不可只记在开发方案里）：
   - 技术栈/架构风格切换、模块拆分合并
   - 关键设计取舍（如 VEntity vs 手写 Dto）
   - 外部依赖变更（新增/替换 NuGet 包、框架版本升级）
   - 跨模块契约/协议变更（DTO 破坏性变更、接口语义变化）

   **不需要写 ADR 的场景**（记在开发方案里即可）：
   - 单文件 bug 修复、仅影响内部实现的重构、测试调整
   - 纯内容迭代（题库素材/复核等，走进度同步纪律）

   **生命周期**：ADR 是永久记录，**不可删除**。即使决策后续被推翻，也在原 ADR 标注"已废弃"并引用新 ADR，而非删除原文件。

1. 新版本开始 → 编写 `{系统名称}/02-迭代开发/V{主版本}/v{version}-开发方案.md`
2. **方案 Oracle 评审**：开发方案定稿后 → 交 Oracle 评审（目标符合性 + 约束/最优解验证）→ 确认后**才进入实施**；评审意见闭环（采纳/驳回需明确）。**评审统一用 `oracle`**（框架 v4.10.9 经验：`momus` 只能评审 `/.plan` 目录文档，对开发方案评审不可用，勿浪费时间重试）
3. 按方案执行开发
4. 开发完成 → 审查代码 → 编写/更新 `{version}-审核报告.md`
5. 有内容变更 → 同步追加到 `变更记录.md`
6. **推送（push）但不打 tag** → **tag 必须征求同意**（见 §4）

### 提交纪律

- **不频繁提交**：每个逻辑单元（feature/fix/docs）完成后才提交，一次迭代宜收敛为少量提交（通常 1-4 个），避免逐补丁高频提交。
- **提交语义完整**：同一主题的探索性/失败尝试改动应合并为单条有意义的提交，而非保留中间过程。
- **禁止提交调试噪音**：无关的临时修改、未验证的半成品不入提交。

### 构建纪律

- **关闭编译服务器用 `dotnet build-server shutdown`**：需要停止编译服务器（VBCSCompiler/MSBuild 节点）时，使用 `dotnet build-server shutdown` 优雅关闭，**不得强杀进程**——强杀可能导致锁文件残留、增量构建缓存损坏、后续编译异常或需手工清理 obj/bin 才能恢复。

### 进度同步纪律（通用）

任何功能/内容迭代完成后，**检查并更新受影响的状态文档**，防止状态过期（四类文档联动，具体触发范围随迭代内容而定）：

| 文档 | 触发条件 | 说明 |
|------|---------|------|
| `docs/变更记录.md` | 任何内容/代码/文档变更 | 增量追加条目（日期/版本/变更内容/关联 ADR），不覆盖历史 |
| 对应 README / 状态清单 | 该子产品的进度/待办变化 | 如 `题库/各册更新情况清单.md` + `题库/README.md`（素材/复核完成四联同步）、`docs/平台管理系统-迭代计划.md`（页面建成状态） |
| `docs/AGENTS.md` | 题库素材/复核状态、项目总览变化 | 同步"题库素材与版本状态"节及路由表 |
| `PROJECT_STRUCTURE.md` | 目录结构变化（新增/移动项目、文件） | 活文档，结构变化必须同步，见 §8 索引 |
| 子产品 `AGENTS.md` | 该子产品的规范/架构变化 | AdminWasm / WebH5 / tests / 题库 各自维护

## 4. Tag 纪律

- **任何 `git tag` 操作（创建/推送）必须事先征求用户同意**。tag = 产品版本发布确认（触发 MinVer 版本号）。
- 日常开发、迭代完成 → 只 `push` 提交，**不自动打 tag**。

## 5. TKWF 框架依赖

- 框架同步：`pwsh %TKWFDeployPath%\build\build-deployment.ps1 -Destination F:\TKWF_FRAMEWORK_PATH -LinkRefs`

增量开发路由：`.TKWF/TKWF-Rules.md`（需求分层、Skill 路由、红线）

框架引用路径统一使用 `$(TKWFDeployPath)build\refs\`，不得硬编码源码树路径

## 6. TKWF 开发流程和基本规则

> 基于 TKW.Framework（简称 TKWF）的开发流程和基本规则。

### 6.1 核心流程

```
编写 Entity → dotnet build 编译通过 → xCodeGen 生成 Dto/DataService/Conditions
         → 编写 Service → dotnet build 编译通过 → SG1 生成 Controller/契约接口
         → WebApi 项目 → SG2 自动生成 Api 接口（GraphQL/REST）
         → 表现层项目 → Wasm SG3 自动生成 Api 调用（GraphQL/REST）
                       → 或 自动生成 ts-domain-client 调用的 ts 强类型
```

### 6.1.1 类型生命周期与 POCO 管控

> **自动注入上下文**：以下规则约束所有类型定义决策，Agent 编写 Entity/Service 前必须知晓。

**类型生成链路**：

| 输入（手写） | 自动生成 | 暴露方式 |
|---|---|---|
| Entity（`[DomainGenerateCode]`） | `{Entity}Dto` / `{Entity}DataService` / `Conditions` | Service 方法（默认 `IsGraphQLQueryable=false`） |
| VEntity（`IsView=true` + `ViewSql`） | `{ViewEntity}Dto` / 只读 DataService | GraphQL 连接（默认 `IsGraphQLQueryable=true`） |
| Service（`[GenerateController]`） | Controller / REST 端点 / GraphQL resolver / ts-client 代理 | REST + GraphQL |

**POCO 管控规则**：

- 查询返回**复用** `{Entity}Dto` / `{ViewEntity}Dto` + 字段裁剪（`?fields` / GraphQL selection / `DynamicSelector`），不为字段子集新建 Dto
- 跨表聚合/报表 → 声明 **VEntity**（`IsView=true` + `ViewSql`），不手写只读 Dto
- 多实体组合 → Response Dto **仅外层壳**，内部引用 `{Entity}Dto`，不扁平复制字段
- 写操作 → Request Dto 与 Service **同文件内联**
- 计算字段（非 SQL 列）→ `[DtoField(IsComputed=true)]` 扩展 VEntity Dto，Service 赋值，不新建 POCO
- 新建 POCO 需论证：参见 Dto最小化速查 §2 决策树（仅 2 个正当理由：Request Dto / 独立响应形状）

> 详细决策树与组合模式见 `%TKWFDeployPath%\docs\G00-TKWF.Domain-领域自治框架-V4-使用指南.md` §类型生命周期与 POCO 管控。

### 6.2 建议使用的 Skill

| 任务                              | 建议 Skill      |
| --------------------------------- | --------------- |
| 物化/维护业务规则（Business.md）  | `tkwf-business` |
| 编写 Entity                       | `tkwf-entity`   |
| 编写 Service                      | `tkwf-service`  |
| 编写测试                          | `tkwf-test`     |
| 编写 ts-domain-client ts 强类型   | `tkwf-tsclient` |

> 使用对应 Skill 确保框架的代码生成、守卫规则、命名约定等 TKWF 特有机制正确执行，不经过 Skill 直接编写会导致与框架不兼容。

### 6.3 代码纪律

- ❌ 禁止手写 `.g.cs` / DataService / Conditions / Controller 接口 / DI 注册
- ❌ 禁止读 `*.g.cs` / `*.biz.cs` / `DataServices\*.cs` 生成源码——读活态文档（`.TKWF/{域}/`）
- ❌ 禁止降低守卫规则 Severity、删除 `[GeneratedBySg]`、修改 `// <auto-generated>`、吞掉规则异常
- 数据访问必须经 `DomainUser.Use<T>()`，写入必须处于 TransactionScope
- 修改 csproj 后必须验证两种解决方案均编译通过

> **引导**：如项目有特殊代码纪律（如前端特有规则、ts-domain-client 规范、测试规范），追加在此节末尾。

### 6.4 tkwf-business 使用时机（编写/更新 Business.md）

> 依据 `ADR05-Business.md物化与tkwf-business-skill设计.md`。Business.md 是**手写物化文件**（xCodeGen 只生成骨架），唯一权威来源是需求/设计文档，Agent 不得从源码反推规则。

**必须执行 `tkwf-business` 的时机**（任一满足即触发）：

| 时机 | 说明 | 触发场景 |
|------|------|---------|
| **首次编写该域 Service 前** | 编写 `tkwf-service` 前检查 `.TKWF/{Domain}/Business.md`，若仍为骨架占位符 → 先执行 `tkwf-business` 物化 | 新域开发、新 UC 实施 |
| **业务需求变更时** | 需求变更涉及业务规则/状态流转/枚举业务含义 → 增量更新 Business.md | 新 UC、改规则、加约束 |
| **审核发现 BR 缺失/有误时** | 审核报告中指出 BR 与实现不符 → 修正 Business.md 并记 LOG.md | 审核反馈 |

**输出**：物化的 `.TKWF/{Domain}/Business.md`（5 段结构：业务全景 / BR 表 / 状态流转 / 枚举含义 / 跨实体约束）+ `LOG.md` 变更记录。

**红线**：
- ❌ 不写方法级实现细节——只写跨实体约束 / 业务不变量
- ❌ 不包含 DTO 字段 / API 签名（归 DOMAIN_MAP.md / DataService_API.md / Domain_Api.md）
- ❌ **增量更新不覆盖**——已有 BR 保留，仅追加/修改变更部分
- ❌ 不臆造规则——每条 BR 必须可从需求/设计文档溯源；溯不到则标记"待补充"并报告主 Agent

## 7. 强制 Skill 路由（违反即失败）

> 本路由表内联自 `.TKWF/TKWF-Rules.md §三`，**禁止跳过**。任何领域开发任务必须按表格加载对应 Skill，禁止使用通用 Agent 分类或手动编写绕过。

| 任务                           | 必须使用的 Skill    | 关键参数                                      | 禁止行为                                         |
| ------------------------------ | ------------------- | --------------------------------------------- | ------------------------------------------------ |
| 物化/维护业务规则              | `tkwf-business`     | `{Domain}=域`                                 | 禁止臆造规则、禁止覆盖已有 BR                    |
| 新增/改实体（Entity/VEntity）  | `tkwf-entity`       | `{Domain}=域` `{Entity}=实体名`               | 禁止手动写 Entity 字段/注解、禁止用通用 category |
| 数据服务/条件/业务规则/Service | `tkwf-service`      | `{Domain}=域` `{Entity}=...`                  | 禁止手动写 Service、禁止用通用 category          |
| 领域测试                       | `tkwf-test`         | `{Domain}=域` `{UC}=用况` `{ServiceName}=...` | 禁止手动写测试                                   |
| 设计文档（需求→方案）          | `tkwf-design`       | `{Domain}=域`                                 | 设计阶段禁止写代码                               |
| 查询/统计梳理（VEntity 设计）  | `tkwf-ventity-design` | `{Domain}=域` `{切片}=切片名`（触发门槛：含跨表聚合/报表/跨切片消费；UI 定稿后、DS 场景层前） | 设计阶段纯文档产出，不写 ViewSql 实现        |

**前置检查**：接到领域开发任务时，第一步先加载并阅读对应 Skill 全文，按其规范执行，不得凭记忆或通用做法替代。

### 7.1 委托 subagent 的 prompt 必须内联 Skill 的「读取禁区」（强制）

> **违反即失败**。委托 subagent 执行任一 tkwf-skill 任务时，主 Agent 的 prompt **必须包含「禁止读取代码文件」约束**，否则 subagent 可能直接读 `.g.cs` / `DataServices\*.cs` / `Entities\*.cs` 等生成源码（500+ 行/文件，冗余且易误导），导致文档失效、绕过生成规则。

**统一约束块**（所有 tkwf-skill 通用，委托 prompt 必含）：

```
## 读取禁区（硬性）
- ❌ 禁止读任何生成源码：*.g.cs / *.biz.cs / DataServices\*.cs / Entities\*.cs / DTOs\*.g.cs
- ❌ 禁止读取产物以反推结构——方法签名、字段结构一律以活态文档为准：
    .TKWF/{Domain}/DOMAIN_MAP.md . /DataService_API.md / Domain_Api.md / Business.md
- ❌ 禁止绕过 Skill 手写生成物（Controller / DI 注册 / .g.cs 成员 / gql 字符串）
- ✅ 文档缺失或过时 → 报告主 Agent 补文档，禁止自行读生成源码替代
```

**Skill 特有禁区**：从对应 SKILL.md 的「## 5. MUST NOT DO」节原样摘录附在约束块后（如 tkwf-entity 的"不手动实现 IDomainEntity/IDomainViewEntity"、tkwf-service 的"不臆造业务规则"、tkwf-test 的"不 Mock DataService"）。

**委托 prompt 模板**：

```
任务：{使用 tkwf-{skill} 的完整任务描述}
必读文档：{阶段相关活态文档路径，如 .TKWF/{Domain}/DataService_API.md}
{读取禁区约束块}
{对应 Skill 的 MUST NOT DO 原样摘录}
输出要求：{特定 skill 的 EXPECTED OUTCOME}
```

## 8. 指南索引

### 活文档（根目录）

| 文件                   | 查阅时机                          | 更新时机               |
| ---------------------- | --------------------------------- | ---------------------- |
| `PROJECT_STRUCTURE.md` | 了解仓库目录结构、新增/移动项目时 | 目录结构变化时同步更新 |

### 增量开发路由

| 文件                  | 查阅时机                   |
| --------------------- | -------------------------- |
| `.TKWF/TKWF-Rules.md` | 需求分层、Skill 路由、红线 |

### 框架静态规则（`%TKWFDeployPath%/AC-Kit/guides/`）

| 指南                    | 查阅时机               |
| ----------------------- | ---------------------- |
| `文档操作指南.md`       | 创建/修改文档时        |
| `ADR编写指南.md`        | 记录架构决策时         |
| `SG架构规则.md`         | 涉及源代码生成器时     |
| `生成代码防绕过规则.md` | 审查生成代码时         |
| `预存问题修复指南.md`   | 全量编译修复预存问题时 |
| `Agent-TKWF使用行为守则.md` | 使用 TKWF 框架遇到红线/事务/AOP/工作流问题时 |