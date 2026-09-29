---
title: 框架问题单——SG3 GraphQL 字段名重推导不跟随 SG2 消歧（G11）
status: 已关闭（框架 v4.10.36 修复）
date: 2026-09-27
source: XiaoShuTong V0.6.18 WARN004 全链路排查（2026-09-27）——SG2/SG3 生成器源码反编译 + schema/运行时实证
---

# 框架问题单：SG3 GraphQL 字段名重推导不跟随 SG2 消歧（G11）

> **背景**：G1-G10 问题单已闭环（Tier1.5 系列 + xCodeGen 活态文档系列，见
> `docs/草稿/归档/框架问题单-Tier1.5-SQLite内存库-v4.10.8不可用.md` 与
> `docs/草稿/框架问题单-xCodeGen活态文档生成缺陷-G9-G10.md`）。本问题单为 **2026-09-27 WARN004 消歧排查**
> 新发现 SG2↔SG3 契约缺口，独立主题，转交框架组处理。
>
> **实证链路**：SG2 服务端 schema 消歧生效（54 个 `_Execute` 字段，运行时实测 `createBank_Execute` 存在、
> 裸 `execute` 不存在）+ SG3 生成器源码重推导分析（`ApiServiceClientGenerator.cs:227/997`）+ 消费端产物核对
> （WebH5 ts-client 天然对齐 / AdminWasm 编译产物无 SG3 GraphQL 代理故潜伏）。

## 一、现象

### 触发面：WARN004 + 服务端消歧（正常）

领域层 ≥2 个 `[GenerateController]` 服务的 public 方法重名（XiaoShuTong 共 62 个 Service 方法统一为
`ExecuteAsync`，TKWF 官方模板 `Service.cs.txt` 约定）→ SG2 报 `TKWF_SG2a_WARN004` 并自动消歧 resolver 字段
名为 `{服务短名}_{方法名}`（如 `createBank_Execute`/`bankDetail_Execute`，`Get` 前缀剥除、方法段保留
PascalCase `Execute`）。服务端 schema 正确暴露 54 个 `_Execute` 字段，无静默丢失。

### 缺口：SG3 客户端生成器重推导 `execute`，不跟随服务端消歧名

`ApiServiceClientGenerator.cs:227`（元数据路径）与 `:997`（另一分支）：
```csharp
var graphqlField = TypeNameConvention.ToGraphQLMethodName(methodName);   // "Execute" → "execute"
```
SG3 **本地重新推导** GraphQL 字段名（小驼峰 `execute`），从不接收 SG2 的消歧结果。生成的代理代码把
`method.GraphQLField` 硬编码进 `SendQueryAsync<T>(graphqlField, ...)`（`:777/:788/:799/:808`）。

**推论**：消费端（Blazor `ApiClientType.GraphQL`）经 SG3 代理请求字段 `execute`，而服务端暴露的是
`createBank_Execute` → **HotChocolate 必然 400 "The field `execute` does not exist on the type `Query`"**
（XiaoShuTong 运行时已实测：`createBank_Execute` 字段存在、裸 `execute` 字段不存在）。

## 二、根因（源码定位，待框架确认）

| 环节 | 代码/契约 | 缺失 |
|------|----------|------|
| **SG2 服务端** | `ApiServiceGenerator*.cs`（Resolvers 生成）按冲突检测输出 `{短名}_Execute` 字段；SG2b 产物 `ApiMetadata.g.cs` 的 `ApiMethodInfo` | ✔ 服务端字段名已消歧 |
| **SG2b 元数据** | `ApiMethodInfo { Name; ReturnType; ApiType; ApiParameterInfo[] Parameters; IsExposed }`（结构体，5 字段） | ❌ **不含最终 GraphQL 字段名 / 别名 / 路由**——SG2 的消歧结果未传出 |
| **SG3 客户端** | `ApiServiceClientGenerator.cs:227/:997` `GraphQLField = TypeNameConvention.ToGraphQLMethodName(methodName)` | ❌ **本地重推导**，走 `ToGraphQLMethodName`（"Execute"→"execute"），与 SG2 消歧算法（`EffectiveFieldName`）不一致 |

**根因一句话**：SG2 的字段消歧发生在**服务端 resolver 层**（`ApiServiceGenerator`），而 SG2b→SG3 的唯一契约
`ApiMethodInfo` 不携带该消歧结果，SG3 只能凭方法名（"Execute"）反推小驼峰（"execute"）——两条算法链在
**重名场景下分叉**。

## 三、影响

- **当前（XiaoShuTong）**：WebH5（React/ts-client）从 schema.graphql 生成，天然使用消歧名 → **不受影响**（真实链路走查全 PASS）；
  AdminWasm 编译产物中**无 SG3 GraphQL 代理**（字段名字符串计数全 0，`obj/` 无 generated/ 目录）→ **缺口潜伏未触发**。
- **潜在**：任何消费端启用 SG3 GraphQL 代理通道（或未来 AdminWasm 补 SG3 代理配置）→ 业务调用 400 全挂。
  REST 通道完全不受影响（`GetRestRoute` 走 `PascalToKebabCase`，独立于 GraphQL 消歧）。
- **结构性矛盾**：AC-Kit 官方模板强推 `ExecuteAsync` 标准签名（`Service.cs.txt`、DG-06-U、DG-07），
  遵循模板的项目 **≥2 个服务即必触发 WARN004 + 消歧**——SG3 缺口会命中每个遵循模板的消费端。

## 四、建议框架侧（三选一，按推荐序）

| # | 方案 | 说明 |
|---|------|------|
| 1 | **SG2b 在 `ApiMethodInfo` 携带最终 GraphQL 字段名**（新增 `GraphQLField` 字段，SG2 消歧后写入）；SG3 `ApiServiceClientGenerator` 改为读取该字段（有则用之、无则回退旧重推导） | 契约清晰、单一事实源；兼容旧元数据（回退保底） |
| 2 | **SG3 复刻 SG2 冲突检测算法**（`EffectiveFieldName`/服务短名规则抽公共 `TypeNameConvention` 方法，两端共用） | 免改元数据契约，但算法需双端同步维护 |
| 3 | 仅在文档层注明规避（SG3 消费端走 REST） | 不修根因，不推荐 |

**附加建议**：评估 WARN004 的 `IsMutationMethod` 判定——当前 `Execute` 不匹配 mutation 前缀表 → 全部写操作落入
Query 根（`createBank_Execute` 是 query 而非 mutation），若需 mutation 语义（缓存失效/审计）需调整判定表或命名。

## 五、验收标准（XiaoShuTong 侧）

框架修复 + 部署后，在 XiaoShuTong 验证：
- **主验收**：AdminWasm（`ApiClientType.GraphQL`）真实调用 `ICreateBankService.Execute` → 不再 400，字段名与服务端
  schema（`createBank_Execute`）一致（方案 1）或由公共算法推导一致（方案 2）
- **回归**：WebH5 ts-client 全链路不变（gen-ts-client 从 schema 生成，schema 无变化）
- 建议框架侧补 `ApiServiceClientGeneratorTest` 用例：重名服务（≥2 个 `ExecuteAsync`）→ 生成代理字段名 = 服务端
  schema 字段名（`createBank_Execute`），断言非 `execute`