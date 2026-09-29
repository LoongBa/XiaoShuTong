---
title: 框架问题单——WebSession 固定 SessionKey 复用（身份串号级）（G13）
status: 待框架组处理
date: 2026-09-29
source: XiaoShuTong 浏览器/API 级走查实证（v4.10.36，2026-09-29）→ G12 根因勘误后的核心必现项
---

# 框架问题单：WebSession 固定 SessionKey 复用（身份串号级）（G13）

> **背景**：G12（`WebSession 会话层登录切换用户失效`）根因勘误后，真正的**必现、确定性**核心表现为
> **固定 SessionKey 复用**——进程内所有登录返回**同一个固定 SessionKey**（首个 guest key），无视请求
> header/cookie、无视用户名。此为**身份串号级**严重缺陷，转交框架组优先处理。

## 一、现象（确定性必现，非间歇）

在 XiaoShuTong WebApi（Development SQLite，`UseWebSession` + `DefaultIdGenerator`，v4.10.36）实证：

1. **进程重启后首登生成固定 key**：首个登录/请求创建 guest 后，该 guest 的 SessionKey（如
   `session:202609290929470005sbd2kl`，时间戳=首客创建时刻）成为**全进程生命周期内恒定SessionKey**。
2. **后续所有登录返回同一固定 key**：任意用户名（owner01 / xiaoming / parent01）、任意请求 key
   （无 header / 自定义 header `X-Session-Key: custom-*` / 真实 Set-Cookie 新 key 回带）——**一律返回同一个
   固定 SessionKey**（`session:202609290929470005sbd2kl`）。
3. **响应头与 body 分离（关键矛盾）**：`Set-Cookie` / `X-Session-Key` 响应头每次请求**重新生成**（如
   9:41:27 / 9:42:59 / 9:44:24…），而 `loginByContext` body 的 `sessionKey` **恒定固定**（9:29:47）。
   → 中间件每次建**当次 guest（新 key）**并正确回写，但 **resolver 层 `User.SessionKey` 恒定首客**。
4. **身份串号**：多个不同用户共享同一 SessionKey——任意一次拿到该 key 即等同于全部用户（会话可互相顶替）。

## 二、根因收敛（高置信，待框架组确认注入点）

- **body** 的固定 `sessionKey` 来自 `CreateLoginPayload().SessionKey = User.SessionKey`（`AuthController.cs` L321）。
- resolver 的 `User` 经 `IDomainUserAccessor<TUserInfo>`（Web 下 `WebDomainUserAccessor` → 读
  `HttpContext.Items[DomainUserKey]`）获取；`UseWebSession` 的会话解析委托
  （`WebAppBuilder.InvokeSessionStepAsync`）每请求新建 guest（新 key）并回写 Items/响应头。
- **矛盾**：中间件 Items 里是当次 guest（新 key），resolver 却拿到固定首客 → **resolver 服务的 DomainUser
  与当次 Items 的 user 不是同一实例**。
- **高置信落点**：`DomainServiceCollectionExtensions` 的**服务工厂从 `DomainUserContext.CurrentAopUser`
  （AsyncLocal）取 DomainUser**；`DomainUser.Use<T>()` 虽 try/finally 设置/恢复（L252-261），但**若 AsyncLocal
  跨请求页线程被首客冻结泄漏，则所有后续请求的服务解析恒得到首客 DomainUser（SessionKey=首客固定）**。
  → 高度指向**首客 DomainUser 在 AsyncLocal/线程池间被冻结复用**。
- 已排除：`DefaultIdGenerator` 非碰撞根因（每次 NewId 时间戳+随机，key 唯一；且中间件每次 Set-Cookie 新 key 佐证）。

## 三、影响

- **身份隔离完全破坏**：任意用户登录命中同一 SessionKey——会话可被其他用户顶替，认证语义失效。
- **新建会话永不生效**：login 后返回旧固定 key，客户端无法建立独立会话（多端/多用户无法同时在线）。
- 与 G12「切换用户/高频登录失效」同源（登录写会话路径异常），本单为**必现**形态。

## 四、复现路径（框架组可复现）

1. 启动 WebApi（`UseWebSession` + GraphQL + 任意 DB，MOCK/惯性登录均可）。
2. 任意用户 loginByContext 成功，记录返回的 SessionKey = K。
3. 再次 loginByContext（**任意**用户名 / **显式带不同** `X-Session-Key` header）：
   - **断言失败**：响应 body `sessionKey` 仍 = K（应每次新 key）。
   - 同时观察响应头 `Set-Cookie`/`X-Session-Key` 为不同新 key（与 body 分离）。

## 五、建议框架侧修复（参考，非唯一方案）

| # | 修复方向 | 说明 |
|---|---------|------|
| 1 | **排查 `DomainUserContext.CurrentAopUser`（AsyncLocal）跨请求冻结** | 确认 `Use<T>()`/AOP 工厂在请求 async flow 末尾彻底清除/不泄漏首客 user；确保 resolver 服务每次解析到当次 Items user |
| 2 | **resolver 的 DomainUser 应取自当次 `HttpContext.Items[DomainUserKey]`** | `WebDomainUserAccessor`/AOP 注入确保与中间件当次 guest 同实例；禁止解析到进程级缓存的固定实例 |
| 3 | **login 写会话路径修复** | `LoginAsUserAsync → UpdateAndActiveSessionAsync` 用 `this.SessionKey`（可能被冻结为首客）更新，需确保基于当次 session |

**关键验证点**：修复后连续 3 次 loginByContext（不同用户/不同请求 key）返回的 `sessionKey` **应各不相同**，
且与响应头 Set-Cookie 一致。

## 六、验收标准（XiaoShuTong 侧）

- 主验收：3 次不同用户 loginByContext → 3 个**不同** SessionKey；响应 body sessionKey 与 Set-Cookie 头一致。
- 身份隔离：用户 A 登录的 SessionKey 与用户 B 不同，互不顶替。
- 回归：login 后 query 正常；多用户可独立会话。
- 建议框架侧补测试：GraphQL 通道连续 3 次不同用户 login → SessionKey 各不相同。

## 附：相关证据记录

- G12 问题单（根因勘误后保留为现象/触发背景）：`docs/草稿/框架问题单-WebSession会话层登录高频调用后失效-G12.md`
- 框架源码：`F:\LoongBa_Git\_TKWF`（`DomainUserContext` AsyncLocal、`DomainServiceCollectionExtensions` 工厂、
  `WebAppBuilder.InvokeSessionStepAsync`、`DefaultIdGenerator`）
- 走查实证：`docs/V0.7.0-候选任务批量闭环-审核报告.md` §八
