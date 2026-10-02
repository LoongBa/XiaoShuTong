# P1「Execute 全量归 Query 根」评估方案（草稿）

> 状态：草稿待 Oracle 评审
> 日期：2026-10-03
> 来源：工作计划 P1「Execute 全量归 Query 根」（平台管理系统-迭代计划 §0.3 遗留）+ G16 问题单

## 一、问题陈述

XiaoShuTong 领域层所有 `[GenerateController]` Service 方法统一为 `ExecuteAsync`（TKWF 官方模板约定）。GraphQL 层 SG2 检测到 Query/Mutation 根下 resolver 字段名 `Execute` 全面重名 → 自动消歧为 `{服务短名}_Execute`。当前 schema 暴露 **54 个 `_Execute` 字段**，且**全部在 Query 根**（运行时 introspection 实证；Mutation 根仅 8 字段——认证/AI 模型类）。

「Execute 全量归 Query 根」的本质：`TypeNameConvention.IsMutationMethod`（框架统一判定，SG2 服务端 + SG3 客户端共用）依据方法名前缀判定 Query/Mutation，**仅认** `Create/Update/Delete/Add/Remove/Lock/Unlock/Reset/Login/Logout/Change` 11 个前缀——**`Execute` 不在表内** → 所有 `Xxx_Execute` 方法被判为 Query（无论读写）。

## 二、为什么当前是"事实状态"而非"缺陷"

1. **写操作经 Query 根执行**：GraphQL 语义上 Mutation 根是"写操作官方入口"，但 Query 根执行写操作**功能可用**（HTTP GET/POST 均可触发，仅语义不纯）。WebH5（ts-client 走 codegen 生成的 `type` 标记）与 AdminWasm（SG3 代理）均按 schema 生成消费，运行时链路（V0.6.3~V0.6.7、V0.6.18、V0.7.x 走查）全 PASS。
2. **消歧机制正确**：`{服务短名}_Execute` 命名是 SG2 自动消歧产物（WARN004 告警 + 自动重命名），54 个字段互不冲突，消费端 codegen 天然使用消歧名。
3. **唯一真实缺陷（G16，已绕行）**：`@tkwf/tsclient` 客户端 **SDK 前缀启发式**（MUTATION_PREFIXES 含 create/remove 等）与框架 `IsMutationMethod`（服务端）**不一致**——SDK 把 `remove*/create*` 方法误判为 mutation → 发 `mutation { ..._Execute }` → 服务端 Mutation 根无此字段 → **HTTP 400**。该问题根因在 SDK 不消费 codegen 已生成的 `type:'query'` 标记（G16 三层根因），XiaoShuTong 已用 `executeQuery()` 绕行（3 处）。

## 三、方案选项

### 方案 A：框架判定表加 `Execute` 前缀（使写操作归 Mutation 根）

调整 `TypeNameConvention.IsMutationMethod` 增加 `Execute` 前缀 → 全部 54 个 `Xxx_Execute` 从 Query 根迁至 Mutation 根。

**影响**：
- **破坏性契约变更**：schema.graphql 54 字段整体迁移 + 消费端（WebH5 schema/codegen/ts-client.g.ts + AdminWasm SG3 代理）全量重新生成
- **读方法误伤**：`Execute` 是**统一方法名模板**（所有 Service 方法都叫 ExecuteAsync），读操作（GetXxx/ListXxx 的 Execute）也会被误判为 Mutation——**方向性错误**（读操作归 Mutation 语义更差）
- 需框架组改 TypeNameConvention + AdminWasm `ApiClientType.Rest` 不受影响（REST 路由 `/create-bank/execute` 不变）
- 风险：框架统一判定表影响**所有 TKWF 消费项目**（DMP-Lite 等），非 XiaoShuTong 单点可控

**结论：不推荐**——`Execute` 前缀命中会无差别迁移读+写全部方法，语义恶化；且属框架级破坏性变更，收益（GraphQL 语义纯度）与代价（全链路重建+全生态影响）不匹配。

### 方案 B：维持现状 + 根治 G16（推荐）

不调整 `IsMutationMethod`（Execute 归 Query 根是**稳定事实**，消歧正确、消费端走查全 PASS），而是**根治 G16 客户端前缀启发式缺陷**（SDK 消费 codegen `type` 标记 / explicitQueries 反向白名单——G16 问题单 §四 方案 1/2）。

**优点**：
- 不产生契约破坏性变更（54 字段不动、双消费端零重建）
- G16 根治后 removeBuddy/createTask/createStudySession 3 处绕行可还原直调（XiaoShuTong 待办）
- 框架判定表保持稳定（`Execute` 不加入），通过**客户端正确路由**解决 400 问题——服务端 Query 根执行写操作虽语义不纯但功能正确，不阻塞业务
- 影响面：仅 SDK 消费端 codegen 接线（Tkwf.configure 传入 explicitQueries/explicitMutations），XiaoShuTong main.tsx 已接入同源 selectionMap/variableTypesMap，扩展同模式即可

**结论：推荐**——最小侵入、无契约破坏、顺带闭环 G16 遗留。

### 方案 C：应用侧 Service 方法改名（如 ExecuteAsync → CreateXxxAsync）

理论可让写方法名命中既有 mutation 前缀，但：
- 破坏性变更 ×2（方法改名 + schema 重建 + 双消费端 + REST 路由 `/xxx/execute` 全变）
- 与 TKWF 官方模板 `ExecuteAsync` 约定冲突（76 个 Service 逐个体检）
- 平台计划 §0.3 已明确"不批量改名 53+ 服务（破坏性变更 + 与官方模板冲突 + 牵动 REST 路由）"

**结论：不推荐**（历史已定论）。

## 四、Oracle 评审结果（2026-10-03，附条件通过——方案 B）

### 评审结论

**附条件通过（方案 B）**。目标符合性 ✅（真问题是 G16 不是 Execute 归 Query 根；方案 A/C 否决论证成立，方案 A 还有「双向恶化」——Execute 统一模板加入前缀表会把读方法一并迁 Mutation 根，语义更差）。最优解验证 ✅（5 个"必须 Mutation 根"未来场景逐项排查：同请求多写串行 / GET 缓存 / 根类型拦截 / Subscription / Federation——当前全部不触发；唯一未来可能的 Subscription 有 WebSocket/SignalR 替代路径）。

### M1（操作项 — 通过条件）

方案 B 附条件通过，闭环条件在方案 §五基础上**追加 2 项**：
1. 框架 G16 修复部署（SDK 暴露 `explicitQueries`/`explicitMutations` 接口或 codegen 自动驱动）
2. XiaoShuTong 升级 `@tkwf/tsclient` 至修复版（package.json + lock + typecheck）
3. 移除 `src/lib/sdk-bypass.ts`，3 处调用（removeBuddy/createTask/createStudySession）还原 `Use<T>()` 直调
4. **（追加）剩余 4 个潜在 400 方法**（createBank/createGroup/createParentRelation/createPkMatch）在 G16 修复后验证直调正常，或确认永不被前端调用
5. 验收（按 G16 问题单 §五 主验收 + 回归）

### M2（本地可先行项 — 不依赖框架组）

1. **防御性盘点剩余 4 个潜在 400 方法的调用计划**：G16 问题单 §三明确「ts-client.g.ts Query 常量中命中前缀表的方法共 7 个，前端当前实际调用 3 个，其余 4 个一旦被调用同样踩坑」。XiaoShuTong 应主动确认这 4 个方法是否在近期迭代会被前端调用——若会，提前走 `sdk-bypass.ts` 绕行；若不会，登记工作计划避免误踩。
2. 其余（还原直调、移除绕行）**纯等待框架 G16 根治**，无其他本地先行项（patch node_modules 不被推荐）。

### C1（修正项 — 论证缺口）

方案 §五闭环条件遗漏 2 项，已补：
- **SDK 版本升级步骤**：闭环条件显式包含「升级 `@tkwf/tsclient` 至修复版 + typecheck 0 错误」（部署≠应用接入）。
- **剩余 4 方法验证**：闭环条件纳入第 4 项（见 M1 #4）。

### C2（修正项 — 事实澄清）

- 方案 §一「54 个 `_Execute` 字段」与 schema.graphql 实证 grep `_Execute` 命中 61 次差异——多出 7 次为 description/注释提及。统一表述「**54 字段 + 7 处描述提及 = 61 命中**」。
- 方案 §三方案 B「main.tsx 已接入同源 selectionMap/variableTypesMap，扩展同模式即可」为论证简化：`selectionMap`/`variableTypesMap`（selection/variableTypes 查表）与 `explicitQueries`/`explicitMutations`（query/mutation 路由判定）是**不同配置项**——前者已接入不代表后者已可用，后者依赖 SDK 新增接口。改为「main.tsx 已接入同源 codegen 产物查表模式，**框架暴露 explicitQueries 接口后**可同模式扩展接入」。

### 远期 Watch out（Oracle 提示，登记备查）

- **Subscription 远期场景**：若未来引入 GraphQL Subscription 做实时通知，方案 B 的「写操作在 Query 根」会成为阻塞——届时重开 ADR 评估方案 D（按方法粒度特性标记，如 `[GraphQLMutation]`）。当前不处理，登记 P2+ 备查。
- **`IsMutationMethod` 框架级稳定性**：方案 B 依赖「框架不改判定表」——建议在 G16 问题单或新 ADR 登记「XiaoShuTong 依赖 Execute 不进 mutation 前缀表」作为契约假设。

## 五、决策建议（Oracle 已评审：附条件通过 — 方案 B）

- **推进方案 B**：Working Plan P1「Execute 全量归 Query 根」项改为「维持现状（事实状态，功能正确）+ G16 客户端根治」，闭环条件 = **M1 五项**（框架修复部署 + SDK 升级 + 移除绕行还原直调 + 剩余 4 方法验证 + 验收）
- 不动框架 IsMutationMethod、不改应用 Service 命名、不产生契约破坏
- 本地先行：按 **M2** 盘点 4 个潜在 400 方法，登记工作计划

## 六、待办（含 Oracle 闭环）

- [x] Oracle 评审（2026-10-03，附条件通过，M1/M2/C1/C2 已应用）
- [ ] 按评审结论更新工作计划 P1 项 + 平台计划 §0.3 遗留状态
- [ ] **M2 本地先行**：盘点 4 个潜在 400 方法（createBank/createGroup/createParentRelation/createPkMatch）调用计划 → 登记工作计划
- [ ] 远期备查：Subscription 场景（P2+）+ IsMutationMethod 契约假设登记
- [ ] G16 框架根治后验收（依赖框架组，M1 #1-5）