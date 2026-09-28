---
title: 框架问题单——WebSession 会话层登录高频调用后失效（进程重启恢复）（G12）
status: 待框架组处理
date: 2026-09-29
source: XiaoShuTong V0.7.0 浏览器走查（API 级契约实证 + 多进程复现，2026-09-29）
---

# 框架问题单：WebSession 会话层登录高频调用后失效（G12）

> **背景**：G1-G11 问题单已闭环（Tier1.5 系列 + xCodeGen 活态文档 + SG3 契约缺口，见
> `docs/草稿/归档/` 与 `docs/草稿/框架问题单-SG3*G11.md`）。本问题单为 **V0.7.0 走查期间发现的
> 框架 WebSession 会话层缺陷**——独立主题，转交框架组处理。
>
> **实证链路**：多进程复现（每个 WebApi 进程重启后初期登录正常 → 短暂运行后 `loginByContext` 全用户
> INTERNAL_ERROR → 进程重启恢复）+ API 级走查（GraphQL 通道）。

## 一、现象

### 触发面：GraphQL 通道 `loginByContext` 高频调用后全失败

XiaoShuTong 领域层（TKWF Dll 模式，`UseWebSession` + HotChocolate GraphQL）走查期间实证：

1. **多进程稳定复现**（3 个进程：PID 41164@4:44 / PID 8136@5:41 / 及 5:24 进程）：
   - 进程**刚启动**：`loginByContext` 返回 `success=true + sessionKey`（正常）
   - **运行数分钟后**（期间有多次登录/查询调用）：`loginByContext` **全部用户返回 INTERNAL_ERROR**
     （`{"errors":[{"message":"服务器内部错误","code":"INTERNAL_ERROR"}]}`）——纯框架操作失败
   - **进程重启**：恢复登录正常——每个进程均按此循环
2. **不随时间恢复**：等待 5-10 秒重试仍失败；连测 6+ 次全失败；3 秒间隔 3 次全失败
3. **资源正常**：PID 8136 运行 9 分钟内存仅 51.2MB、14 线程、410 句柄——**非内存泄漏/资源耗尽**
4. **重启瞬间可登录**：新进程（5:49）登录成功一次（`session:2026092905492000016p4b10`），随后又失效

### 影响面

- **loginByContext（登录）本身失败**——GraphQL 通道所有业务调用前置（会话激活）断裂
- 伴随现象（走查早期记录）：多用户登录曾返回**相同 sessionKey**（session 复用影子）
- 读操作（listBuddies 等）在登录成功后正常——**失效应答集中于会话创建/激活路径**

## 二、根因假设（待框架确认）

| 假设 | 论证 |
|------|------|
| **A（主）**：WebSessionManager 会话存储（HybridCache/SessionStore）在**高频创建会话**后进入异常态 | 登录每次 `NewSessionAsync` 写会话；走查高频调用了数十次登录——写入累积后存储层异常；重启清空恢复；内存正常排除 OOM |
| **B（辅）**：GraphQL 通道会话激活与 REST 通道路径差异 | V0.6.6 ADR80 同类（GraphQL 会话激活 Session not found）——本问题在**登录应答层**更前置（连 sessionKey 都不返回） |
| **C（排除）**：XiaoShuTong 业务代码 | 登录是纯框架操作（白名单映射固定）；业务 Service 未参与登录路径 |

## 三、影响

- **当前（XiaoShuTong）**：真实 WebApi 走查期间反复触发——每次高频验证后需重启 WebApi 恢复；**联调/走查效率受阻**
- **潜在（生产）**：若生产环境登录高频（真实用户登录/刷新），会话层可能同样失效 → **登录全挂** = 严重生产事故
- **与 ADR80 的关系**：ADR80 是"会话已写但 GraphQL 读不到"（读取域）；本问题是"登录写会话即失败"（写入域）——**不同层次**，需框架组确认是否同根

## 四、复现路径（框架组可复现）

1. 启动 WebApi（Dll 模式 + `UseWebSession` + GraphQL + SQLite 文件库）
2. 循环调用 `loginByContext`（任意白名单用户，间隔 1-1.5s）30-50 次
3. 观察：初期 success=true → 某次起全 INTERNAL_ERROR（不恢复）
4. 重启进程 → 恢复

## 五、建议框架侧排查（按序）

| # | 排查方向 | 说明 |
|---|---------|------|
| 1 | **WebSessionManager.NewSessionAsync 写入路径** | 会话存储（HybridCache 局部/分布式）在高频写下的异常；换 .NET 版本/缓存后端验证 |
| 2 | **GraphQL 通道会话激活** | 登录 mutation → 会话创建 → 后续携带：与 REST 对照（REST 通道是否同失效） |
| 3 | **会话数/过期清理** | 会话是否无限累积（虽内存正常，但存储层哈希/索引可能膨胀） |
| 4 | **日志/异常栈** | 当前 INTERNAL_ERROR 被异常中间件吞掉——框架组可在内部日志看到真实异常（XiaoShuTong 侧 UseWebExceptionMiddleware 包裹） |

## 六、验收标准（XiaoShuTong 侧）

框架修复 + 部署后，在 XiaoShuTong 验证：
- **主验收**：连续 50 次 `loginByContext`（3 用户轮流）全部 success=true，无 INTERNAL_ERROR
- **回归**：登录后 query 正常（listBuddies 200）；进程长跑 30 分钟后登录仍正常
- 建议框架侧补测试：GraphQL 通道高频登录 N 次 → 无会话层异常

## 附：走查期间相关证据记录

- `docs/V0.7.0-候选任务批量闭环-审核报告.md` §八（走查实证 + G12 登记）
- `docs/变更记录.md` 2026-09-29 走查条目（含"框架级发现登记"）
- 关键时间线：5:24 进程初期 inviteBuddy 等写操作正常 → 5:41 进程（PID 8136）9 分钟登录全失败