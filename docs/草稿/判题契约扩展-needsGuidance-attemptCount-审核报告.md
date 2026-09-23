---
title: 判题契约扩展 - 审核报告
version: 草稿 v0.1
summary: needsGuidance / attemptCount / maxAttempts / showAnswer / isDegraded 契约扩展步骤 0-5 全面审计
date: 2026-09-23
status: 活跃
auditor: Sisyphus（Oracle 风格证据审计）
---

# 判题契约扩展 - 审核报告（草稿 v0.1）

> 审计对象：`docs/草稿/判题契约扩展-needsGuidance-attemptCount-方案.md` 步骤 0-5 全部实施
> 审计方式：源码定向核验（file:line）+ 全套件 `dotnet test` 复跑 + WebH5 `pnpm typecheck` 复跑 + git log 提交核验

## 一、审核范围

- 特性：判题契约扩展（needsGuidance / attemptCount / maxAttempts / showAnswer / isDegraded）
- 开发方案：`docs/草稿/判题契约扩展-needsGuidance-attemptCount-方案.md`（草稿 v0.1）
- 实施步骤：步骤 0（JudgingEngineService 接入）→ 步骤 5（useAttemptFlow hook），步骤 6 为本报告
- 审核日期：2026-09-23
- 审核对象文件：
  - `src/XiaoShuTong.Domain/Services/Learning/SubmitAttemptService.cs`（475 行）
  - `src/XiaoShuTong.Domain/Entities/Learning/Attempts.cs`（AnswerHash + 唯一索引）
  - `tests/XiaoShuTong.Tests/Learning/SubmitAttemptServiceTests.cs`（8 分支用例）
  - `src/XiaoShuTong.WebH5/src/hooks/use-attempt-flow.ts`（165 行，新建未接线）
  - `src/XiaoShuTong.WebH5/src/gql/ts-client.g.ts`（生成物，只读核验字段选择串）
  - `.TKWF/DOMAIN_MAP.md` / `.TKWF/GraphQL_Api.md`（活态文档同步状态）

## 二、审核结论

**总评：附条件通过**

- **通过部分**：步骤 0-5 全部实施完成且与方案 §三/§五/§六/§八 契约一致；后端全套件复跑 **353 通过 / 0 失败 / 2 跳过**（与基线完全一致，8/8 分支用例全绿）；WebH5 `tsc --noEmit` **exit 0**；生成物链路（codegen 提交 `11370e8`）与 tkwf-skill 路由均验证无绕过。
- **附条件（不阻断，延期项）**：
  1. `useAttemptFlow` 已建但**未接入任何页面**——`task.tsx` 仍走 `MOCK_QUESTIONS` + zustand 本地判题，特性尚无法在 UI 端到端走通；
  2. `sessionUid` 无活源——`createStudySession_Execute` 在 `src/` 全代码库**零调用**；
  3. hook 决策分支无前端单测。
- **理由**：以上均为**后续迭代延期项**（会话生命周期 + UI 编排改造），非本次契约扩展实施范围内的缺陷；契约层（后端 Service/DTO/测试/生成物）100% 达标，故不判「不通过」；因端到端 UI 可操性暂缺而不足以判「完全通过」，定级**附条件通过**。

## 三、需求符合度

### 3.1 方案 §一 目标 → 实施

| 目标 | 实现状态 | 证据 |
|------|:---:|------|
| `SubmitAttemptResDto` 扩展 5 字段，前端作答流由服务端契约驱动 | ✅ 完成 | DTO 5 字段落地 `SubmitAttemptService.cs:461-474`；ts-client 字段选择串含全部 5 字段 `ts-client.g.ts:149` |
| 服务端单点决策 needsGuidance/showAnswer，前端不猜 | ✅ 完成 | `SubmitAttemptService.cs:227-228` 决策；hook 不重推（`use-attempt-flow.ts:122-148` 仅消费） |

### 3.2 方案 §三 缺口 G1-G4 → 实施

| 缺口 | 方案要求 | 实现状态 | 证据 |
|------|---------|:---:|------|
| G1 无 `needsGuidance` | Partial/Wrong 且未达上限 → true | ✅ 完成 | `SubmitAttemptService.cs:228` `needsGuidance = (Partial\|Wrong) && !showAnswer`；hook 引导分支 `use-attempt-flow.ts:132-139` |
| G2 无 `attemptCount`/`maxAttempts` | 服务端统计轮次 + 上限 2 | ✅ 完成 | `MaxAttempts=2` 常量 `SubmitAttemptService.cs:33`；统计 `:114` `attemptCount = prevAttempts.Count + 1`；DTO `:243-244` |
| G3 无 `showAnswer` | 达上限或已看答案 → true，前端展示后必进下一题 | ✅ 完成 | 达上限 `:227`；已看答案（Full）`BR-16` 早退分支 `:117-136`（`ShowAnswer=true` `:133`）；hook 优先分支 `:124-131` |
| G4 `isDegraded` 未透出 | 判题降级标记透出 | ✅ 完成 | `:108` 接收 `verdict.IsDegraded`；`:246` 透出；hook 原始响应透出 `use-attempt-flow.ts:82-83` |

**需求覆盖率**：5/5 字段 + 4/4 缺口 + 8/8 测试用例 = **17/17 全达标**。

### 3.3 方案 §八 测试计划 T1-T8 → 实施（实际测试方法名 + 复跑结果）

| 用例 | 场景 | 实际测试方法（文件行） | 复跑结果 |
|:-:|------|------|:---:|
| T1 | correct 无引导，postState 升级 | `ExecuteAsync_Correct_NoGuidance_StateUpgrades`（`SubmitAttemptServiceTests.cs:470`） | ✅ 通过 |
| T2 | partial 第 1 次 → needsGuidance=true | `ExecuteAsync_PartialFirstAttempt_NeedsGuidance`（`:453`） | ✅ 通过 |
| T3 | partial 达上限 → showAnswer=true | `ExecuteAsync_PartialReachesLimit_ShowAnswer`（`:491`） | ✅ 通过 |
| T4 | wrong 第 1 次 → needsGuidance=true、hint 非空 | `ExecuteAsync_WrongFirstAttempt_GuidanceWithHint`（`:515`） | ✅ 通过 |
| T5 | wrong 达上限 → showAnswer=true | `ExecuteAsync_WrongRetryReachesLimit_ShowAnswer`（`:534`） | ✅ 通过 |
| T6 | hintLevel=Full → showAnswer=true | `ExecuteAsync_FullHint_ShowAnswer`（`:557`） | ✅ 通过 |
| T7 | 幂等命中 → attemptCount 不重复累加 | `ExecuteAsync_SameQuestionTwice_Idempotent`（`:383`，按 §6.1 同答案幂等 + 改答案重试更新） | ✅ 通过 |
| T8 | LLM 降级 → isDegraded=true 透出 | `ExecuteAsync_NoAiModel_IsDegradedTrue`（`:574`） | ✅ 通过 |

**测试覆盖率**：8/8 方案用例全落地且全绿（见 §六 复跑证据）。

## 四、架构符合度

| 架构约束 | 遵守情况 | 证据 |
|---------|:---:|------|
| 服务端单点决策、前端不猜（§4.2 关键规则） | ✅ | 决策仅在后端 `:227-228`；hook 注释明示"不做任何再推导、不重新判断对错" `use-attempt-flow.ts:40-41`，决策顺序 showAnswer→needsGuidance→celebrate `:122-148` 与方案 §4.3 伪码一致 |
| 生成物不手改（ts-client.g.ts 只读） | ✅ | 字段选择串由 codegen 生成：提交 `11370e8`「schema.graphql/ts-client.g.ts/ts-client.mock.g.ts 含 5 新字段 + GraphQL_Api.md 刷新」；hook 仅 `import type ... from '@/gql/ts-client.g'` |
| tkwf-service / tkwf-test / tkwf-tsclient skill 路由 | ✅ | Service 符合 DomainServiceBase + `User.Use<T>()` 懒加载 + DTO 同文件内联；测试为 Contract（`[Trait("Category","Contract")]` `:16`，Tier 1.5 InMemory）；前端消费走 `Tkwf.User.Use<SubmitAttempt_ExecuteService>()` `use-attempt-flow.ts:105` |
| AnswerHash 幂等语义（§6.1 决策） | ✅ | 幂等键 `SessionId+QuestionId+AnswerHash` `:71-76`；唯一索引 `Attempts.cs:20` `idx_attempts_idem`；活态文档同步 `.TKWF/DOMAIN_MAP.md:97` |
| showAnswer 优先防死循环（决策表 #4/#8 兜底） | ✅ | 后端 `:227`（partial/wrong 达上限均 showAnswer）；hook `:124` showAnswer 为第一分支 |
| DTO 与方案 §5.1 逐字段一致 | ✅ | `:461-474` 五个新字段名/类型/默认值（`MaxAttempts=2`）与方案 §5.1 完全一致；GraphQL 契约 15 字段刷新 `.TKWF/GraphQL_Api.md:501` |
| 判题引擎五键契约接入（步骤 0） | ✅ | `:88-100` `User.Use<JudgingEngineService>().JudgeAsync(..., PreferLlm = true)`，提交 `d3c1a56` |

**架构符合度结论**：9/9 约束全遵守，无违反项。

## 五、代码质量

| 检查项 | 结果 | 备注/证据 |
|--------|:---:|------|
| 类型安全（前端 hook） | ✅ | `use-attempt-flow.ts` 全文件 **0 处** `as any` / `@ts-ignore` / `@ts-expect-error`（Grep 核验：仅 `routeTree.gen.ts` 生成路由文件含 `as any`，非审计对象） |
| 类型安全（后端 DTO） | ✅ | `SubmitAttemptResDto` 为 `sealed record`，全部 `init` 属性，`Result/PreState/PostState` 非空默认 |
| 异常处理 | ✅ | hook 域级失败 `success===false` 分支 `:116-120` 产 `error` 动作；RPC 抛错 `DomainClientError.code` 捕获 `:150-156`；`finally` 复位 `isSubmitting` `:157-159`；无空 catch |
| 业务规则引用可溯源 | ✅ | 每条 BR 引用（BR-16/21/27/29 等）均在方案 §4.2/§六 或 `.TKWF/Business.md` 可溯源；决策表 #7 语义由 BR-16 承载（见 §八 待改进项 #5 说明） |
| 测试覆盖 | ✅ | 8/8 方案用例 + 既有 17 条 SubmitAttempt 测试全绿 |
| 常量位置 | ✅ | `MaxAttempts = 2` 置于 `SubmitAttemptService` 内 `private const` `:33`（方案 §六 常量建议原样落实） |

## 六、测试情况

### 自动化测试（fresh 复跑，2026-09-23）

| 模块 | 用例数 | 通过 | 失败 | 跳过 | 说明 |
|------|--------|------|------|------|------|
| XiaoShuTong.Tests 全套件 | 355 | **353** | **0** | **2** | `dotnet test tests/XiaoShuTong.Tests/`（`TKWFDeployPath=F:\TKWF_FRAMEWORK_PATH`），**与基线 353/0/2 完全一致，无增量失败**；2 跳过为框架模板样例（`MyDataService_Query` / `MyService_ShouldDoSomething`），预存无关 |
| 判题契约扩展新增分支用例 | 8 | 8 | 0 | 0 | T1-T8 见 §3.3，均为 Contract 用例 |
| WebH5 typecheck | - | exit 0 | - | - | `pnpm typecheck`（`tsc --noEmit`）0 错误，hook 类型与 ts-client.g.ts 生成类型严格对齐 |

### 遗留缺陷

见 §八 待改进项（均为延期项/低危项，无阻断性缺陷）。

## 七、ADR 执行情况

| ADR | 决策内容 | 是否落实 | 偏差 |
|-----|---------|:---:|------|
| **N/A** | 本特性**未创建 ADR**——依据 `Agents_Use_TKWF.md §3`：ADR 门禁仅适用于架构决策（技术选型/模块拆分/契约破坏性变更/外部依赖变更）；本次为**单字段契约扩展（5 个响应字段透出）+ 前端 hook 消费**，属 bugfix/contract-extension tier，不触发 ADR 门禁 | ✅ | 无 |

> **补充说明**：特性内的两个关键决策均有记录——① 幂等键细化（AnswerHash，§6.1）记入方案正文；② **Jev（TypeSafe Jev）经评估后拒绝采用**（输出契约 Noul/Choice/Score 无法承载五键判题契约、判题 LLM 低频不构成 TPM 瓶颈、语义粒度不足），决策记录见方案 §9.2，无需 ADR（评估后不采纳，无架构变更落地）。两决策在本次审计中均核验与实施一致。

## 八、待改进项

| # | 项 | 描述 | 严重度 | 对策 |
|:-:|----|------|:---:|------|
| 1 | **task.tsx 未接线 useAttemptFlow** | hook 已建但全代码库 0 处 import（Grep 核验：`useAttemptFlow` 仅存在于自身文件）；`task.tsx:21` 仍用 `MOCK_QUESTIONS` + `:90` zustand `useAppStore` 本地判题——特性**尚无法在 UI 端到端走通** | 中 | 后续迭代接 `createStudySession_Execute` + `task.tsx` 改造（替换 mock 判题为 hook 三分支驱动），属已知延期项 |
| 2 | **sessionUid 无活源** | `createStudySession_Execute` 在 `src/` 全代码库**零调用**（仅生成物 `ts-client.g.ts:34/98` 与 mock 定义存在），hook 的 `sessionUid` 参数无真实会话来源 | 中 | 同 #1：会话生命周期接线后获得活源 |
| 3 | **hook 决策分支无前端单测** | `use-attempt-flow.ts` 三个动作分支（answer-sheet/guidance/celebrate/error）无 vitest/jest 用例 | 低 | 后续补单测；当前由 typecheck（0 错误）+ 后端 Contract 测试兜底 |
| 4 | **幂等返回与 hook 三分支的边缘语义** | 同答案重发命中幂等（`BuildFromExistingAsync` `:362-385` 返回 `NeedsGuidance=false/ShowAnswer=false`）时，Wrong 结果会落入 hook `celebrate` 分支（`use-attempt-flow.ts:140-148`）——语义上"无新信息"应停留而非庆祝 | 低 | UI `isSubmitting` 已防双击（`:91/104`），可达性低；接线时 hook 增加"幂等/无变化 → 停留"分支或服务端幂等返回携带真实决策值 |
| 5 | **决策表 #7 实现方式与方案 §六 伪码差异（信息性）** | 方案 §六 `showAnswer = ... \|\| hintLevel == HintLevel.Full`；实际实现为 **BR-16 早退分支**（`hintLevel==Full` 时 `:117-136` 直接返回 `ShowAnswer=true`，且**不迁移状态、不入 Attempts**）——功能等价且语义更强，**非缺陷** | 信息 | 无需处理；若需与伪码逐字对齐可后续注释注明，但不建议改 |
| 6 | **代码注释引用"决策表 #9"在方案 §4.2 无对应行** | `SubmitAttemptService.cs:152` 注释"方案决策表 #9 附加"（Play 场景不引导），但方案 §4.2 决策表仅 1-8 行，Play 语义未入表 | 低 | 方案 §4.2 决策表补 Play 行（或注释改为"BR-21 Play 场景"），保证文档-代码引用闭环 |
| 7 | **BR-29 hint ≤20 字截断** | ✅ 已核验存在：`TruncateHint(question.Hint, 20)` `:238` + `:398-403`（超长截断至 20 字）——记录为**已满足**，非缺陷 | - | 无 |

## 九、审核结论

- **是否可进入下一阶段**：是 —— 步骤 0-5 实施达标、全套件 353/0/2 复跑全绿、typecheck 0 错误、无缺陷级偏差；可提交（push）步骤 3+5+6 改动并推进后续迭代（push 与 tag 按 `Agents_Use_TKWF.md §4` 征求用户同意后由主 Agent 统一执行）。
- **遗留问题是否阻断**：否 —— §八 全部为**延期项/低危项**（UI 接线、会话源、hook 单测、边缘语义），非契约层缺陷，不阻断提交与后续迭代。
- **下一步建议**：
  1. 提交当前工作区改动（`SubmitAttemptService.cs` / `SubmitAttemptServiceTests.cs` / 方案 md / 变更记录 md / `use-attempt-flow.ts`）；
  2. 后续迭代（可并入下个版本）：`createStudySession_Execute` 接线产出 `sessionUid` → `task.tsx` 改造接入 `useAttemptFlow` 替换 mock 判题 → 补 hook 单测；
  3. 收尾时按进度同步纪律核对四类状态文档（变更记录已由步骤 6 登记，其余随接线迭代联动）。
