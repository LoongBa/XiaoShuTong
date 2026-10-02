---
title: 框架问题单——G18 修复（v4.10.48）引入 WASM 兼容缺口：MapGraphQLError 构造 AuthenticationException 触发 System.Net.Security stub PlatformNotSupported，会话过期链再次中断（BUG007）
status: ⚠️ 待框架组确认（XiaoShuTong 已取证 + 实证；应用侧绕行方案 §六 供参考，等待框架组修复方向）
date: 2026-10-02
source: XiaoShuTong AdminWasm G18 验收走查（2026-10-02）——按 G18 §五 验收移除 App.razor OnServiceError 绕行、重建消费 4.10.48 后，注入无效 SessionKey 访问 dashboard，页面显示「加载群组失败: SystemNetSecurity_PlatformNotSupported」且弹窗不触发
updated: 2026-10-02
---

# 框架问题单：G18 修复在 WASM 端构造 AuthenticationException 触发 System.Net.Security stub（BUG007）

## 一、现象

AdminWasm（Blazor WASM，net10.0，ApiClient + GraphQL 传输）升级 **v4.10.48**（消费 G18 修复 DLL）后，**按 G18 问题单 §五 验收：移除 App.razor OnServiceError 兼容绕行、还原纯 OnAuthRequired**。重建 + 重启后，注入无效 SessionKey 访问 `/owner/dashboard`：

- **服务端**：正确返回 `AuthenticationException: 用户未登录或会话已过期` + `code=AUTH_REQUIRED`（WebApi 日志实证，`DomainErrorFilter` 记录两次 `listGroups_Execute` AUTH_REQUIRED）
- **客户端**：页面消息框显示 **`加载群组失败: SystemNetSecurity_PlatformNotSupported`**，且 **OnAuthRequired 弹窗不触发**（modalVisible=false 实证）
- 后果：G18 修复在**桌面/服务器端正确**（框架测试 1177/1177 全绿），但 **WASM 端会话过期引导链再次中断**——且这次连 OnServiceError 都不会收到可识别的「会话已过期」消息（异常类型已变为 PlatformNotSupportedException）

## 二、根因（源码 + WASM 程序集实证）

### 1. v4.10.48 修复后 AUTH_REQUIRED 首次走 `new AuthenticationException(msg)` 分支

`_Domain.Api\ApiClient\Runtime\GraphQLClient.cs` L426（v4.10.48 新增 `TryGetExtensionString` 修复 G18 后，`code` 正确读到 `"AUTH_REQUIRED"`）：

```csharp
private static Exception MapGraphQLError(GraphQLError error)
{
    var code = TryGetExtensionString(error.Extensions, "code");   // v4.10.48 修复：正确读到 "AUTH_REQUIRED"
    ...
    return code switch
    {
        DomainException.ErrorCodes.AuthRequired => new AuthenticationException(msg),   // ← 首次在此构造
        ...
    };
}
```

**关键对比**：
- **v4.10.46（G18 未修）**：`code` 恒 null → 恒落 `_ => new DomainException(msg)` → **从不构造 AuthenticationException** → WASM 不触碰 System.Net.Security stub → 会话过期消息经 OnServiceError 兜底弹窗正常 ✅
- **v4.10.48（G18 修复）**：`code="AUTH_REQUIRED"` 正确映射 → **首次在 WASM 构造 `AuthenticationException`** → 触发 stub → 抛 `PlatformNotSupportedException` ❌

### 2. WASM 程序集证据（铁证）

| 证据 | 内容 |
|---|---|
| AdminWasm 网络清单 | WASM 加载 `System.Net.Security.jfw2oy4ger.wasm`（stub 版）✅，但**从未请求 `System.Security.Authentication.wasm`** ❌ |
| `TKWF.Domain.ApiClient.dll` 引用 | `GetReferencedAssemblies()` 列出 `System.Net.Security 10.0.0.0`（AuthenticationException 类型 TypeForward 至该程序集） |
| .NET WASM 平台行为 | `System.Net.Security` 在浏览器环境为 stub 实现——所有 API/类型构造调用抛 `PlatformNotSupportedException("SystemNetSecurity_PlatformNotSupported")`（资源键消息，正与实测 UI 消息完全一致） |

### 3. 异常传播路径

```
GraphQLClient.SendAsync → MapGraphQLError → new AuthenticationException(msg)  ← WASM stub 在此抛 PlatformNotSupportedException
   ↓ inner = PlatformNotSupportedException（非 AuthenticationException）
   ↓ L249 scope / L278 静态事件分支：`inner is AuthenticationException` 判定失败 → 不触发 OnAuthRequired
   ↓ 落 else → OnServiceError（已被应用移除，G18 验收）→ 无人消费
   ↓ LoadGroupsAsync catch → Message.Error("加载群组失败: SystemNetSecurity_PlatformNotSupported")
```

## 三、影响范围

- **Blazor WASM 消费端（net8+/net9+/net10+）**：凡依赖 `OnAuthRequired` 的会话过期引导链全部中断（弹窗/跳转登录不触发）；`IsAuthRequiredError()` 判定失效（`DomainClientUser.cs` L322 依赖 `ex.IsAuthRequiredError()` → `InnerException is AuthenticationException`）
- **影响层级**：P0（会话过期用户体验链路——G18 修复的验收目标被 WASM 端抵消）
- **服务器/桌面端**：不受影响（System.Net.Security 完整实现，AuthenticationException 可正常构造）

## 四、建议修复方向（供框架组决策）

### 方案 1（推荐）：WASM 兼容异常映射——AUTH_REQUIRED 分支不依赖 System.Net.Security 类型

框架在 `_Framework\Core`（或 ApiClient 自有程序集，WASM 全平台可用）定义自有异常类型（如 `AuthRequiredException : Exception`），或**复用既有 `DomainException` + ErrorCode 标记**：

```csharp
// GraphQLClient.MapGraphQLError
DomainException.ErrorCodes.AuthRequired => new DomainException(msg, DomainException.ErrorCodes.AuthRequired),
```

并同步调整下游判定：
- `IsAuthRequiredError()`（`DomainExceptionExtensions.cs`）：由 `InnerException is AuthenticationException` 改为同时识别 `DomainException.ErrorCode == AuthRequired`
- `DomainClientUser.cs` L322 / `RestClient.cs` L185 / 事件分支 `inner is AuthenticationException` → 统一走 `ex.IsAuthRequiredError()` 扩展判定（不直接 `is` 判型）

**优点**：跨平台一致（WASM/桌面/服务器同语义），消除对 System.Net.Security 的隐式依赖；`DomainException` 在 TKWF.Core，WASM 已加载（无 stub）
**注意**：AuthFailed → UserLogonException 是否也在 WASM stub 需同步核查（`System.Security.Authentication` 程序集缺失清单佐证，大概率同受影响）——**已核查闭环（2026-10-02）**：`UserLogonException` 定义于 `TKWF.Core`（`_Framework\Core\UserLogonException.cs`，编译后 `TKWF.Core.dll` 反射实证 `TKW.Framework.Domain.Exceptions.UserLogonException`），**WASM 已加载 `TKWF.Core` 程序集（无 stub）**，故 **AUTH_FAILED 分支不受 BUG007 影响，无需一并修复**；仅 `AuthenticationException`（AUTH_REQUIRED 分支，TypeForward 至 System.Net.Security stub）受影响

### 方案 2：条件编译 / 运行时判定（侵入小但两套路径，不推荐长期）

```csharp
DomainException.ErrorCodes.AuthRequired => OperatingSystem.IsBrowser()
    ? new DomainException(msg, DomainException.ErrorCodes.AuthRequired)
    : new AuthenticationException(msg),
```

**缺点**：桌面/WASM 行为分叉，测试矩阵扩大；后续框架演进易漏改

## 五、XiaoShuTong 验收建议（框架组修复后）

1. 重建 AdminWasm（消费新 DLL）→ 注入无效 SessionKey 访问 `/owner/dashboard`
2. 断言：OnAuthRequired 正常触发 → 弹窗「会话已过期」→ 确认 → `/user/login?returnUrl=...` → 登录 → 回跳
3. 桌面/服务器回归：框架测试 1177 全绿保持

## 六、应用侧临时绕行（框架修复前，XiaoShuTong 已具备的兼容手段）

若需临时恢复弹窗：App.razor 恢复 OnServiceError 订阅，检测 `PlatformNotSupportedException` 消息特征（`SystemNetSecurity_PlatformNotSupported`）或任何非 AuthenticationException 的会话过期场景 → 转 OnAuthRequired 弹窗。**注意**：此绕行在 G18 修复后覆盖的是 BUG007 新异常形态，与 G18 时代绕行（消息「Session expired」）不同，需按 §七 触发场景同步更新。

（XiaoShuTong 当前策略：等待框架组修复方向，不擅自落地绕行——按纪律「框架缺陷描述清楚转框架组解决」。如框架组确认方案 1，应用侧零改动即可恢复。）

## 七、触发场景与验证证据

- **复现步骤**：AdminWasm（4.10.48 DLL）→ 清除 sessionStorage → 访问 `/owner/dashboard` → 页面消息「加载群组失败: SystemNetSecurity_PlatformNotSupported」+ 无弹窗
- **服务端证据**：`webapi.out.log` 两条 `fail: DomainErrorFilter ... AuthenticationException: 用户未登录或会话已过期`（AUTH_REQUIRED 正确）
- **客户端证据**：WASM 网络清单无 `System.Security.Authentication`；UI 消息 `SystemNetSecurity_PlatformNotSupported`（stub 资源键）
- **适用边界**：仅 Blazor WASM（浏览器）触发；Blazor Server / MAUI / Console / 桌面测试不触发
