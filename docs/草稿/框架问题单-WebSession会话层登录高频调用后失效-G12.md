---
title: 框架问题单——WebSession 会话层登录切换用户失效（进程污染态）（G12）
status: 待框架组处理
date: 2026-09-29
source: XiaoShuTong V0.7.0 浏览器走查（API 级契约实证 + 多进程复现 + 根因收敛，2026-09-29）
---

# 框架问题单：WebSession 会话层登录切换用户失效（进程污染态）（G12）

> **🔧 更新（2026-09-29 晚，框架 v4.10.36）——根因勘误 + 新实证**：
> 1. **原根因「会话 key 时钟回拨竞态抛 `InvalidOperationException("时钟回拨异常")`」需修正**。框架源码
>    `TKWF/Utility/IdGenerator/DefaultIdGenerator.cs`（v4.10.36 实际代码）**已自愈修复**：
>    `if (timestamp < last)` 分支走 `timestamp = WaitNextMillis(last)`（回退自旋等待），**不抛异常**；
> 且源码注释明写「G12 根因定位…修复：改为回退…而抛异常」——XML 注释（G1-G11 时代）已过时，与实际代码不符。
>    **补充澄清（源码确认，防误导）**：`UseWebSession` 实际注册的默认 ID 生成器为
>    **`DefaultIdGenerator`**（`DomainWebOptions.IIdGeneratorType` 默认值，`DomainWebOptions.cs` L37 +
>    `WebAppBuilder.UseWebSession` L190 `TryAddSingleton(IIdGenerator, Options.IIdGeneratorType)`），
>    **非 G12 原文推断的 `DistributedIdGenerator`**——两者同为时间戳方案但实现不同；排查请以
>    `DefaultIdGenerator` 为准（其高并发 `_LastTimestamp` 竞争自愈逻辑见源码注释）。
> 2. **真正必现、确定性核心表现 = 固定 session key 复用**（本更新核心）：进程内**所有登录返回同一个固定
>    SessionKey**（首个 guest key），无视请求 header/cookie、无视用户名——**身份串号级严重性**。详见新问题单
>    **G13**（`框架问题单-WebSession固定sessionKey复用身份串号-G13.md`），本单保留为 G13 的现象/触发背景。
> 3. **GraphQL 可观测性黑洞独立成单**：详见 **G14**（`框架问题单-GraphQL-DomainErrorFilter不记录异常不透detail-G14.md`）。

> **背景**：G1-G11 问题单已闭环（Tier1.5 系列 + xCodeGen 活态文档 + SG3 契约缺口，见
> `docs/草稿/归档/` 与 `docs/草稿/框架问题单-SG3*G11.md`）。本问题单为 **V0.7.0 走查期间发现的
> 框架 WebSession 会话层缺陷**——经根因收敛，已高度指向**会话 key 生成器竞选态**，转交框架组处理。
>
> **实证链路**：多进程复现 + 切换用户精确触发 + 根因收敛（`TKWF.Utility.xml` GuidIdGenerator 注释为决定性证据）+ API 级走查（GraphQL 通道）。

## 一、现象

### 触发面：GraphQL 通道 `loginByContext` 切换用户后全失败（进程污染态）

XiaoShuTong 领域层（TKWF Dll 模式，`UseWebSession` + HotChocolate GraphQL）走查期间实证：

1. **精确触发（非"高频累积"）**：进程干净态下**切换 userName 首次即 INTERNAL_ERROR**
   - 单用户重复登录（xiaoming×5 / owner01×5）成功 → **首次切换 owner01→xiaoming 或反之即失败**
   - **最小 login 成功**（去 authInfo/deviceId）→ 参数无关，纯框架会话操作失败
   - 30-50 次高频 `loginByContext`（3 用户轮流，间隔 1-1.5s）→ 初期 success=true → 某次起全 INTERNAL_ERROR
2. **进程污染态**：一旦切换失败，进程进入**全失败污染态**——之后任意登录（含单用户）全挂，重启才恢复
3. **响应头与 body 分离**：`Set-Cookie`/`X-Session-Key` 头**仍生成**但 body 为 INTERNAL_ERROR——说明响应头写入与 body 生成是分离路径
4. **多进程稳定复现**（PID 41164@4:44 / PID 8136@5:41 / PID 45680 等）：每个进程均按"启动正常 → 切换/调用触发 → 全失败 → 重启恢复"循环
5. **资源正常**：PID 8136 运行 9 分钟内存仅 51.2MB、14 线程、410 句柄——**非内存泄漏/资源耗尽**
6. **新 DLL（6:52，已含 GuidIdGenerator）下仍复现**——修复工具存在但未接线

### 影响面

- **loginByContext（登录）本身失败**——GraphQL 通道所有业务调用前置（会话激活）断裂
- 伴随现象（走查早期记录）：多用户登录曾返回**相同 sessionKey**（session 复用影子）
- 读操作（listBuddies 等）在登录成功后正常——**失效应答集中于会话创建/激活路径**

## 二、根因（已收敛 —— 决定性证据）

> **根因高度指向：会话 key 走 `DistributedIdGenerator`（时间戳方案），未切到框架已造好的 `GuidIdGenerator`（时钟回拨竞态）。**

**决定性证据（`TKWF.Utility.xml` GuidIdGenerator 注释）**：

1. `GuidIdGenerator` 适用注释明确写"如 **WebSession 会话 key**"
2. `GuidIdGenerator` 特征注释明确写"**无假时钟回拨竞态**——（`DistributedIdGenerator`）共享 `_LastTimestamp`/`_Sequence` 字段，高并发下线程捕获旧时间戳后被抢占、期间其他线程推进 `_LastTimestamp`，会导致 `timestamp < last` 误判回拨并抛 `InvalidOperationException("时钟回拨异常")`"
3. `GuidIdGenerator` 注释明确写"适合排查验证（**问题单 G12**）"——**框架组已为 G12 造了修复工具**

**实证矛盾（关键）**：G12 记录的实际 sessionKey = `session:2026092905492000016p4b10`（**含时间戳** `20260929054920` = 2026-09-29 05:49:20）——证明会话 key **实际仍走 `DistributedIdGenerator`（时间戳+序列+随机），未切到 GuidIdGenerator**。

**根因解释全部现象**：
- **切换/高频即 INTERNAL_ERROR**：`DistributedIdGenerator` 共享 `_LastTimestamp`/`_Sequence` 高并发竞选态 → 假时钟回拨 → 抛 `InvalidOperationException("时钟回拨异常")`
- **进程污染态**：`_LastTimestamp` 被推进到异常值后，后续所有 ID 生成（含 sessionKey）持续失败——**进程级静态状态污染，重启才恢复**
- **Set-Cookie/X-Session-Key 头仍生成**：`TKWF.Domain.Web.xml` `SetSessionKeyToResponse` 注释"将 SessionKey 设置到响应中（Cookie + Header）"——响应头写入与 body 生成是分离路径，头先写、body 后抛异常
- **单用户重复登录正常**：间隔足够长，`_LastTimestamp` 单调推进不触发回拨误判
- **新 DLL 6:52 仍复现**：DLL 已含 GuidIdGenerator，但**会话管线未切换过去**

## 三、影响

- **当前（XiaoShuTong）**：真实 WebApi 走查期间反复触发——切换用户/高频验证后需重启 WebApi 恢复；**联调/走查效率受阻**
- **潜在（生产）**：若生产环境登录高频（真实用户登录/刷新，多用户轮换），会话层可能同样失效 → **登录全挂** = 严重生产事故
- **与 ADR80 的关系**：ADR80 是"会话已写但 GraphQL 读不到"（读取域）；本问题是"登录写会话即失败"（写入域）——**不同层次**，需框架组确认是否同根

## 四、复现路径（框架组可复现）

1. 启动 WebApi（Dll 模式 + `UseWebSession` + GraphQL + SQLite 文件库）
2. **方式 A（精确触发）**：单用户登录成功 → 立即切换 userName 再次登录 → 首次切换即 INTERNAL_ERROR
3. **方式 B（高频）**：循环调用 `loginByContext`（3 用户轮流，间隔 1-1.5s）30-50 次 → 初期 success=true → 某次起全 INTERNAL_ERROR（不恢复）
4. 重启进程 → 恢复；**切换失败后进程进入污染态**（任意登录全挂）

## 五、建议框架侧修复（按优先级）

| # | 修复方向 | 说明 |
|---|---------|------|
| **1（首选，一行改动）** | **`WebSessionManager.NewSessionAsync` 的 sessionKey 生成改用 `GuidIdGenerator`** | 框架已造好 GuidIdGenerator（XML 注释已指明"如 WebSession 会话 key"、"无假时钟回拨竞态"、"适合排查验证（问题单 G12）"）——**一行注入改动即可修复** |
| 2（备选） | 修复 `DistributedIdGenerator` 假时钟回拨竞态 | `_LastTimestamp` 比较加容差（允许 `timestamp == last` 或小幅回拨）或改单调时钟（Environment.TickCount64）；避免误判回拨抛异常 |
| 3（补充） | 时钟回拨异常应可恢复 | `InvalidOperationException("时钟回拨异常")` 当前是硬异常抛给调用方；应改为自愈（`_LastTimestamp` 取 max 或递增）而非进程污染 |

**关键验证点**：修复后 sessionKey 格式应**不再含时间戳**（若仍为 `session:20260929...` 说明未切换）。框架组反馈时请确认：**"`WebSessionManager.NewSessionAsync` 的 sessionKey 生成是否已切到 `GuidIdGenerator`？"**

## 六、验收标准（XiaoShuTong 侧）

框架修复 + 部署后，在 XiaoShuTong 验证：
- **主验收**：单用户登录成功 → 立即切换用户名 → 仍 success=true 无 INTERNAL_ERROR；连续 50 次 `loginByContext`（3 用户轮流）全部 success=true
- **无污染态**：切换失败后不应进入全失败态（或失败可自愈恢复）
- **回归**：登录后 query 正常（listBuddies 200）；进程长跑 30 分钟后登录仍正常
- 建议框架侧补测试：GraphQL 通道高频/切换登录 N 次 → 无会话层异常 + 无进程污染

## 七、待补充/钉选问题

- 【用户强调的关键发现】关闭 `UseWebExceptionMiddleware`（`Program.cs` L14 临时改为 false，**已还原 true**）后，切换用户 INTERNAL_ERROR **仍在**（`{"message":"服务器内部错误，请稍后重试。","extensions":{"code":"INTERNAL_ERROR","translationKey":"Error_InternalError"}}`）——`translationKey: Error_InternalError` 是**框架 i18n 错误码**，证明 INTERNAL_ERROR **非该异常中间件吞异常生成**，而是**框架领域/GraphQL 错误管道在 resolver 执行时显式返回**。这条与"会话 key 时钟回拨竞态"共同构成 G12 根因闭环。

## 附：走查期间相关证据记录

- `docs/V0.7.0-候选任务批量闭环-审核报告.md` §八（走查实证 + G12 登记）
- `docs/变更记录.md` 2026-09-29 走查条目（含"框架级发现登记"）
- `F:\TKWF_FRAMEWORK_PATH\build\refs\TKWF.Utility.xml`（GuidIdGenerator 决定性注释）
- `F:\TKWF_FRAMEWORK_PATH\build\refs\TKWF.Domain.Web.xml`（WebSessionManager/SetSessionKeyToResponse 会话管线）
- 关键时间线：切换用户 15/15 全失败；单用户 5/5 成功；切换后进程污染（单用户也全挂，重启恢复）
