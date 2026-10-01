---
title: 框架问题单——SG 生成 LoginAsAsync 的 GraphQL 字段名 loginByContextAsync 与 schema loginByContext 不一致（G17）
status: ✅ 已修复（框架侧 v4.10.46；XiaoShuTong 升级后需按 §五 验收）
date: 2026-10-01（2026-10-02 核实升级后未修复；2026-10-02 框架组修复完成 v4.10.46）
source: XiaoShuTong P0 浏览器走查（2026-10-01）——AdminWasm 登录页 DomainClientUser.LoginAsAsync 每次登录 HTTP 400 "field not exist"
---

# 框架问题单：TKWF.Domain.ApiClient.SG 生成的 LoginAsAsync 发送 loginByContextAsync，schema 权威字段为 loginByContext（G17）

## 一、现象

AdminWasm（Blazor WASM，`TKWFRole=ApiClient`）登录时 `DomainClientUser.LoginAsAsync(...)` 发出 GraphQL mutation **`loginByContextAsync`** → HTTP 400：

```json
{"errors":[{"message":"The field `loginByContextAsync` does not exist on the type `Mutation`.","extensions":{"specifiedBy":"https://spec.graphql.org/September2025/#sec-Field-Selections"}}]}
```

schema 实际权威字段为 **`loginByContext`**（`Mutation` 根），二者不一致导致登录请求全军覆没。

## 二、根因（三方证据比对）

| 证据源 | 字段名 | 说明 |
|---|---|---|
| `src/XiaoShuTong.WebApi/schema.graphql` L166 | `loginByContext(input: LoginContextInput)` | 服务端 SG2 生成的权威 schema，走查实证登录成功需用此字段 |
| 框架 XML 注释 `F:\TKWF_FRAMEWORK_PATH\build\refs\TKWF.Domain.ApiClient.xml` L135 / L292 | `loginByContext` | 框架自身文档注释也是 `loginByContext` |
| AdminWasm 运行期实际请求（浏览器网络面板 + 服务端 400 响应） | `loginByContextAsync` | SG 生成代理（`TKWF.Domain.ApiClient.SG` V4.8.5）发出的字段名多了 `Async` 后缀 |

### 推断

`LoginAsAsync` 的 GraphQL 字段名推导路径把 **CLR 方法名 `LoginByContextAsync` 直接去掉 `ByContext`？或错误拼接 `Async` 后缀**，产出 `loginByContextAsync`；而服务端 SG2 消歧规则对同源方法生成 `loginByContext`（无 Async、驼峰）。两端字段名推导规则不一致——客户端侧由 `TKWF.Domain.ApiClient.SG` 生成，服务端侧由 SG2 生成，未共享同一命名规则实现。

> 补充：schema.graphql 中其它认证 mutation（`loginByPassword` / `logout` / `changePasswordSecure`）均为无 `Async` 形式，佐证服务端规则"去 Async + 驼峰"；客户端生成器未对齐。

## 二·五、2026-10-02 核实：升级 4.10.36+（G11）后**未自动修复**（refs 4.10.45 实证）

### 核实结论

**G17 在升级到 4.10.36+ 后依旧存在**——当前 refs（`F:\TKWF_FRAMEWORK_PATH\build\refs\`）实际版本 **4.10.45**（FileVersion/Pascal 版本号四层证据），AdminWasm.dll 字符串探测**仅含 `loginByContextAsync`、无独立 `loginByContext`** → SG3 代理依旧发错字段名 → 登录依旧 400。

### 四层证据链（根因精确化）

| 层 | 来源 | 字段名 | 说明 |
|---|---|---|---|
| 运行时权威 | `schema.graphql` L166 | `loginByContext` | **无 Async**——HotChocolate 命名约定从方法名 `LoginByContextAsync` **裁剪 Async 后缀**（`loginByPassword`/`logout`/`changePasswordSecure` 同理，佐证系统性命名约定非个案） |
| SG2 契约 | `WebApi/obj/.../ApiServiceGenerator/ApiMetadata.g.cs` | `loginByContextAsync` | **带 Async**——SG2 写入契约 `GraphQLField` 时未应用 HotChocolate Async 裁剪规则 |
| 服务端 resolver | `WebApi/obj/.../ApiServiceGenerator/AuthMutation.g.cs` | 方法名 `LoginByContextAsync` | 运行时经 HotChocolate 命名约定裁剪 → 实际注册 `loginByContext`（无 Async） |
| 客户端代理 | `AdminWasm/bin/.../XiaoShuTong.AdminWasm.dll` 字符串探测 | `loginByContextAsync` | SG3 忠实消费 SG2 契约字段名 → 发错名 → **400** |

### 根因修正（原 §二 推断不完整）

- 原判定侧重"客户端生成器未对齐"；**实际根因在服务端 SG2 契约本身**：SG2 生成 `ApiMetadata.GraphQLField` 时对 AuthController（框架内置控制器）方法直接取方法名 camelCase（`loginByContextAsync`），**未应用与 HotChocolate 运行时一致的 Async 后缀裁剪**（`loginByContext`），契约值与运行时实际注册字段不一致。
- **关键演化**：G11（v4.10.36）将 SG3 从"本地推导"改为"优先消费 SG2 契约 `GraphQLField`"（解决业务 Service `_Execute` 消歧名 ✓ 已验证）——但 AuthController 的契约值本身就是错的，G11 的契约消费机制**反而把错误固化**：升级后客户端从"独立推导（也错）"变为"消费错误契约（照错）"，**字段名依旧 `loginByContextAsync`，400 依旧发生**。
- **佐证（REST vs GraphQL 分叉）**：REST 端点 `/auth/login-by-context-async`（**带 Async**，`AuthRestEndpoints.g.cs` 实证）——REST 层无此命名约束故正常；GraphQL 层被 HotChrome 裁剪为 `loginByContext`。**只有 SG2 契约夹在中间既不对齐 GraphQL 运行时、又不走 REST 命名**，两层从此分叉。
- 修复方向收敛：**SG2 生成 `ApiMetadata.GraphQLField` 时须对 AuthController 方法应用与服务端运行时一致的 Async 裁剪**（或让 G11 的 `ComputeGraphQLFieldDisambiguation`/`ResolveResolverMethodName` 单一权威一并覆盖框架内置控制器，而非仅业务 Service）。

## 三、影响

| 场景 | 表现 | 严重度 |
|---|---|---|
| AdminWasm 登录页（`User/Login/Login.razor`） | `LoginAsAsync` 400 → 登录失败 | **P0**（登录是管理端唯一入口） |
| 任何 ApiClient 消费端调用 `DomainClientUser.LoginAsAsync` | 同 400 | 潜在（其余消费端若走 LoginAs 路径同踩） |

**XiaoShuTong 走查绕行**：未绕过 `LoginAsAsync`，改为页面层手写 `loginByContext` mutation（与 schema 对齐）并手动写 `sessionStorage["TKWF_SessionKey"]` 注入会话，规避生成代理缺陷（详见走查会话记录）。登录成功实证 `loginByContext` 字段正确。

## 四、建议框架侧（按推荐序，2026-10-02 按根因修正更新）

> ⚠️ 核实结论（§二·五）：根因在 **SG2 写 ApiMetadata 契约时未对 AuthController 方法应用 HotChocolate Async 裁剪**，SG3 忠实消费错误契约。修复须从**契约源头**对齐运行时命名，而非客户端侧。

| # | 方案 | 说明 |
|---|------|------|
| 1 | **SG2 生成 `GraphQLField` 契约时应用 Async 裁剪（根治）** | `ApiServiceGenerator`（SG2）写 `ApiMetadata.GraphQLField` 时，对方法名应用与服务端运行时 HotChocolate 一致的命名规则（**裁剪 `Async` 后缀 + camelCase**，如 `LoginByContextAsync → loginByContext`）；或让 G11 已建的 `ComputeGraphQLFieldDisambiguation`/`ResolveResolverMethodName` 单一权威**覆盖框架内置控制器**（AuthController/PingService），而非仅业务 Service。SG3 消费修正后契约即自动正确——**改动点收敛到服务端 SG2 一处，客户端零改动**。 |
| 2 | **契约校验防再犯（护栏）** | SG2/SG3 生成时对比运行时实际注册字段（schema 导出或 HotChocolate 命名约定），`GraphQLField` 与运行时不符即告警/报错，防此类静默不匹配（G17 属 G11 契约消费机制引入前就存在、升级后未察觉的静默缺陷）。 |
| 3 | ~~客户端生成器映射表~~（已否） | 原方案主攻客户端侧，与根因（契约源头错误）不符——客户端只是忠实消费错误契约，改客户端治标不治本，仅作临时应急。 |

## 五、验收标准（XiaoShuTong 侧）

框架修复 + 部署后：
- **主验收**：AdminWasm `User.Use<DomainClientUser>().LoginAsAsync(...)` 直调成功登录（不再 400），可移除走查用的手写 `loginByContext` 登录注入脚本
- **回归**：`loginByPassword` / `logout` / `changePasswordSecure` 不受影响
- **实证**：`AdminWasm.dll` 字符串探测不再出现 `loginByContextAsync`（仅 `loginByContext`）

## 六、XiaoShuTong 侧现状（V0.7.7 走查）

- **已绕行**（2026-10-01，走查会话）：登录改手写 `loginByContext` mutation + sessionStorage 注入，不走 `LoginAsAsync`；后续每次 WebApi 重启后重登走同路径。
- **待框架修复后**：按 §五 验收 → 还原 `LoginAsAsync` 直调，删除绕行脚本。

## 七、框架组修复完成答复（2026-10-02，v4.10.46）

> **状态：✅ 已修复（框架侧 v4.10.46）**。根因 = 契约源头（SG2 内置控制器 `GraphQLField` 未对齐 HC 运行时 Async 裁剪），非消费端问题。升级 4.10.46 后按 §五 验收。

### 修复实现

- **`TypeNameConvention` 新增 `StripAsyncSuffix`（唯一 Async 剥离权威）**，`ToGraphQLMethodName` 统一先剥 Async（对齐 HotChocolate 运行时命名约定）
- 内置控制器契约字段名：`loginByContextAsync` → **`loginByContext`**（与 schema 一致）；业务服务幂等零变化（其 `ExtractMethodInfo` 路径本已剥 Async）
- `ServiceMethodExtractor.StripAsyncSuffix` 委托同源（消除重复实现，全链路单点）
- Oracle1 方案审核通过（候选 A：单一权威；否决 B 消歧分叉 / C 破坏 REST 路由）

### 验证（框架侧）

- 全量 **1170/1170 用例全绿**（含新增内置控制器契约断言 `FrameworkContractGraphQLFieldAsyncTests` ×2）
- slnx Release 0 警告 0 错误（编译层）
- REST 路由 `/auth/login-by-context-async` / ResolverName / 输出类型名 **零变化**（零破坏性变更）

### XiaoShuTong 侧验收动作（升级 4.10.46 后）

1. **主验收**：还原 `User.Use<DomainClientUser>().LoginAsAsync(...)` 直调 → 登录成功（不再 400），删除走查绕行脚本（手写 `loginByContext` mutation + sessionStorage 注入）
2. **回归**：`loginByPassword` / `logout` / `changePasswordSecure` 不受影响
3. **实证**：`AdminWasm.dll` 字符串探测不再出现 `loginByContextAsync`（仅 `loginByContext`）
4. **注意**：本次修复只对齐契约到运行时，**服务端 schema 字段名不变**（`loginByContext`）——无需调整既有 schema 消费

### XiaoShuTong 侧验收状态（2026-10-02，主 Agent 亲测）

**编译层 ✅ 全部通过**（无需代码改动——Login.razor.cs 本就是 `LoginAsAsync` 直调，走查绕行是浏览器会话层手法无脚本残留）：

| 项 | 结果 |
|---|---|
| refs 版本 | `TKWF.Domain.ApiClient.dll` = **4.10.46.0**（框架组已部署） |
| ApiMetadata 契约（`.TKWF/` + `obj/` 双副本） | `GraphQLField = "loginByContext"`（无 Async，10-02 03:08 重建）✅ |
| AdminWasm 重建（4.10.46 refs） | 0 错误 |
| DLL 字符串精确探测（大小写敏感） | camelCase `loginByContextAsync` = **0**、`loginByContext`（独立）= 0（字段名经契约常量传递非内联）；PascalCase `LoginByContextAsync` = 3（CLR 方法名，正常）✅ |
| Domain build | 0 警告 0 错误（契约刷新）✅ |
| 测试基线 | **477 通过 / 0 失败 / 2 跳过**（无回归）✅ |

> ⚠️ 探测注意：PowerShell `-match` 不区分大小写会误报（PascalCase 方法名 `LoginByContextAsync` 也能命中小写模式）——须用 `[regex]::Matches` 大小写敏感精确计数。

**运行时验收 ⏳ 待服务在线**：主验收项（`LoginAsAsync` 直调登录成功）需 WebApi(5020)+AdminWasm(5000) 运行后浏览器实测；届时一并补 AI 模型页走查 + BankDetail 运行时验证（框架组编译完成、服务重启后）。