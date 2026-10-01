---
title: 框架问题单——SG 生成 LoginAsAsync 的 GraphQL 字段名 loginByContextAsync 与 schema loginByContext 不一致（G17）
status: 提议（XiaoShuTong V0.7.7 走查已绕行，待框架组评估）
date: 2026-10-01
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

## 三、影响

| 场景 | 表现 | 严重度 |
|---|---|---|
| AdminWasm 登录页（`User/Login/Login.razor`） | `LoginAsAsync` 400 → 登录失败 | **P0**（登录是管理端唯一入口） |
| 任何 ApiClient 消费端调用 `DomainClientUser.LoginAsAsync` | 同 400 | 潜在（其余消费端若走 LoginAs 路径同踩） |

**XiaoShuTong 走查绕行**：未绕过 `LoginAsAsync`，改为页面层手写 `loginByContext` mutation（与 schema 对齐）并手动写 `sessionStorage["TKWF_SessionKey"]` 注入会话，规避生成代理缺陷（详见走查会话记录）。登录成功实证 `loginByContext` 字段正确。

## 四、建议框架侧（按推荐序）

| # | 方案 | 说明 |
|---|------|------|
| 1 | **客户端生成器对齐服务端命名规则（根治）** | `TKWF.Domain.ApiClient.SG` 对认证类方法生成 GraphQL 字段名时复用服务端 SG2 的命名规则（去 `Async` 后缀 + 首字母小写 + 驼峰），或直接读取服务端 schema/ApiMetadata 中的权威字段名，而非独立推导。 |
| 2 | **方法名->字段名映射表** | 客户端 `LoginAs` 系列（`LoginAsAsync`/`LoginByContextAsync`/`LoginByPasswordAsync` 等）在生成器中显式映射到 schema 既有字段名，规避推导歧义。 |
| 3 | **生成后契约校验** | 生成的客户端代理在构建期对比服务端 schema，字段名不一致即告警/报错，防此类静默不匹配上线。 |

## 五、验收标准（XiaoShuTong 侧）

框架修复 + 部署后：
- **主验收**：AdminWasm `User.Use<DomainClientUser>().LoginAsAsync(...)` 直调成功登录（不再 400），可移除走查用的手写 `loginByContext` 登录注入脚本
- **回归**：`loginByPassword` / `logout` / `changePasswordSecure` 不受影响

## 六、XiaoShuTong 侧现状（V0.7.7 走查）

- **已绕行**（2026-10-01，走查会话）：登录改手写 `loginByContext` mutation + sessionStorage 注入，不走 `LoginAsAsync`；后续每次 WebApi 重启后重登走同路径。
- **待框架修复后**：按 §五 验收 → 还原 `LoginAsAsync` 直调，删除绕行脚本。