---
title: 框架问题单——@tkwf/tsclient 前缀启发式误判 query/mutation（G16）
status: 提议（XiaoShuTong V0.7.7 已绕行，待框架组评估）
date: 2026-10-01
source: XiaoShuTong P0 浏览器走查（2026-10-01）——removeBuddy 点击 HTTP 400 "field does not exist on type Mutation"
---

# 框架问题单：@tkwf/tsclient ServiceProxy 前缀启发式与服务端 SG IsMutationMethod 判定不一致（G16）

## 一、现象

WebH5 前端调用 `removeBuddy_Execute`（解除搭子）→ HTTP 400：

```json
{"errors":[{"message":"The field `removeBuddy_Execute` does not exist on the type `Mutation`.","extensions":{"specifiedBy":"https://spec.graphql.org/September2025/#sec-Field-Selections"}}]}
```

前端请求体为 `mutation { removeBuddy_Execute ... }`——但服务端 schema 中该字段在 **Query 根**（非 Mutation）。

## 二、根因（三层，源码实证）

### 第一层（服务端 SG）：`Xxx_Execute` 全进 Query 根

TKWF SG 的 `IsMutationMethod` 把 `Xxx_ExecuteAsync` 判定为**非 mutation**（方法名 `Execute` 不匹配 mutation 前缀表）→ 所有 `*_Execute` resolver 字段进 **GraphQL Query 根**。XiaoShuTong schema.graphql 实证：`removeBuddy_Execute` L44 / `createTask_Execute` L121 均在 Query 类型段；运行时 introspection 确认 Mutation 根仅 8 字段（认证/AI 模型类）。

### 第二层（SDK 客户端）：后缀启发式判 mutation

`@tkwf/tsclient` `dist/service-proxy.js` `createUse()`：

```js
const MUTATION_PREFIXES = ["create", "update", "delete", "add", "remove", "lock", "unlock", "reset"];
function isMutation(name, explicitMutations) {
    if (explicitMutations?.has(name)) return true;
    const lower = name.toLowerCase();
    return MUTATION_PREFIXES.some((p) => lower.startsWith(p));
}
```

`removeBuddy_Execute`（`remove` 前缀）→ 判 mutation → 发 `mutation { removeBuddy_Execute }` → 服务端 Mutation 根无此字段 → **400**。`acceptBuddyInvite_Execute`（`accept` 不在前缀表）→ 判 query → 匹配 Query 根 → **成功**。

### 第三层（SDK 最深）：codegen 已生成正确的 type 标记但 SDK 不消费

消费端 codegen 产物 `ts-client.g.ts` 已生成 `type:'query'|'mutation'` 标记：

```ts
export const Query = { removeBuddy_Execute: { type: 'query' }, ... };
export const Mutation = { loginByPassword: { type: 'mutation' }, ... };
```

**但 SDK 完全不消费该标记**——`ServiceProxy` 不读 `Query`/`Mutation` 常量，也无 `explicitQueries` 反向白名单接入（dist 全量扫零命中）→ 丢弃正确信号，退回粗糙前缀启发式。**这是根因锚点**。

## 三、影响

| 调用 | 前缀 | SDK 判定 | 服务端 | 结果 |
|---|---|---|---|---|
| `removeBuddy_Execute` | remove | mutation | Query 根 | **400 实证**（P0 走查） |
| `createTask_Execute` | create | mutation | Query 根 | **400 预期**（同因） |
| `createStudySession_Execute` | create | mutation | Query 根 | **400 预期**（同因，appStore 每次开学习会话必踩） |
| createBank/createGroup/createParentRelation/createPkMatch | create | mutation | Query 根 | 潜在（当前未调用） |
| 其余 ~40 处（accept/reject/list/get/markMastered 等） | 非前缀 | query | Query 根 | ✅ 正常 |

> ts-client.g.ts Query 常量中命中前缀表的方法共 **7 个**，前端当前实际调用 3 个（已 400/预期 400），其余 4 个一旦被调用同样踩坑。

## 四、建议框架侧（按推荐序）

| # | 方案 | 说明 |
|---|------|------|
| 1 | **`explicitQueries` 反向白名单（最小侵入，推荐）** | 对称于现有 `EXPLICIT_MUTATIONS`，新增 `explicitQueries?: ReadonlySet<string>`；`isMutation(name, explicitMutations, explicitQueries)` 中 `explicitQueries?.has(name) → return false` 优先于前缀启发式；不传时行为不变（向后兼容）。 |
| 2 | **codegen 产物自动驱动（根治）** | 消费端 codegen 已有 `Query`/`Mutation` 常量（含正确 type 标记）——`Tkwf.configure()` 接入 `explicitQueries: new Set(Object.keys(Query))` / `explicitMutations: new Set(Object.keys(Mutation))`，**消灭前缀启发式**（可标记 deprecated 最终移除）。XiaoShuTong `main.tsx` 已接入同源 `selectionMap/variableTypesMap`，扩展同模式即可。 |
| 3 | **`Use()` per-call options 增加 `{type?}` 覆盖** | 最大灵活但把判定责任推给调用方，不推荐。 |

**建议弃用路径**：所有消费端 codegen 产物接入方案 2 后，`MUTATION_PREFIXES` 前缀启发式标记 deprecated 并移除。

## 五、验收标准（XiaoShuTong 侧）

框架修复 + 部署后：
- **主验收**：WebH5 `removeBuddy_Execute`/`createTask_Execute`/`createStudySession_Execute` 走 `Use<T>()` 正常（不再 400），与绕行前 `sdk-bypass.ts` 行为一致
- **回归**：`acceptBuddyInvite_Execute` 等 query 方法不受影响；`loginByContext`/`logout` 等 mutation 方法不受影响（EXPLICIT_MUTATIONS 兼容）
- XiaoShuTong 移除 `src/lib/sdk-bypass.ts` 绕行 helper，3 处调用还原 `Use<T>()` 直调

## 六、XiaoShuTong 侧现状（V0.7.7 绕行）

- **已绕行**（2026-10-01，6bccc1c）：3 处调用（removeBuddy/createTask/createStudySession）改走 `src/lib/sdk-bypass.ts` 的 `executeQuery()`——直接调 `Tkwf.User.getTransport().execute()` 强制 `type:'query'` + 从 codegen `operationSelection`/`operationVariableTypes` 取 selection/variableTypes（对齐 main.tsx Tkwf.configure 同源数据）；typecheck/build 0 错误。
- **待框架修复后**：按 §五 验收 → 移除绕行 helper 还原直调。