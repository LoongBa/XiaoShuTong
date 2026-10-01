---
title: 框架问题单——GraphQLClient.MapGraphQLError 读 extensions.code 恒 null（STJ JsonElement as string），业务码映射全失效（G18）
status: 提议（XiaoShuTong V0.7.7 AdminWasm 走查实证；应用侧已兼容绕行，待框架组评估）
date: 2026-10-02
source: XiaoShuTong AdminWasm 会话过期弹窗走查（2026-10-02）——注入无效 SessionKey 后请求 listGroups，服务端返回 AUTH_REQUIRED，但客户端 OnAuthRequired 不触发（Session expired 走了 OnServiceError）
---

# 框架问题单：GraphQLClient.MapGraphQLError 无法读取 extensions.code（G18）

## 一、现象

AdminWasm（Blazor WASM，ApiClient + GraphQL 传输）会话过期后请求任意受保护接口：
- **服务端**：返回 `extensions.code = "AUTH_REQUIRED"`（HTTP 200 + GraphQL errors，直连 Invoke-WebRequest 实证）
- **客户端**：`OnAuthRequired` 事件**不触发**，`Session expired` 错误走了 `OnServiceError`（console 实证 `warn: TKWF.WasmAuth[1001] [GraphQL] DomainException: Session expired, please log in again.` 及异常链）
- 后果：应用层 `domainUser.OnAuthRequired` 弹窗/跳转（App.razor 已实现）**永不触发**；WasmAuthState.Register 框架默认跳转 handler 同样不触发

## 二、根因（源码实证）

### 客户端错误映射（`_Domain.Api\ApiClient\Runtime\GraphQLClient.cs` L419-434）

```csharp
private static Exception MapGraphQLError(GraphQLError error)
{
    var code = error.Extensions?.GetValueOrDefault("code") as string;  // ← 恒 null
    var msg = error.Message ?? "未知 GraphQL 错误";
    return code switch
    {
        DomainException.ErrorCodes.AuthRequired => new AuthenticationException(msg),   // AUTH_REQUIRED 分支永不命中
        DomainException.ErrorCodes.AuthFailed    => new UserLogonException(...),
        DomainException.ErrorCodes.Forbidden     => new UnauthorizedAccessException(msg),
        ...
        _ => new DomainException(msg),   // ← 永远落此分支：inner = DomainException
    };
}
```

**`error.Extensions` 类型为 `Dictionary<string, object?>`**（GraphQLClient.cs L466）——System.Text.Json 反序列化 `object` 目标时值类型为 **`JsonElement`**（struct），`as string` 对 `JsonElement` 装箱对象**恒返回 null** → `code` 恒 null → switch 恒落 default → **所有业务码映射全部失效**（AUTH_REQUIRED/AUTH_FAILED/FORBIDDEN/NOT_FOUND/VALIDATION_ERROR 等）。

### 下游判定链（因 inner 类型错误而全部失效）

| 层 | 判定 | 期望 inner | 实际 inner | 结果 |
|---|---|---|---|---|
| `DomainExceptionExtensions.IsAuthRequiredError` | `InnerException is AuthenticationException` | AuthenticationException | DomainException | **false** |
| `DomainClientUser.HandleErrorAsync`（L321-327） | IsAuthRequiredError → OnAuthRequired | 触发 | 走 else OnServiceError | **弹窗/跳转失效** |
| `GraphQLClient` 静态 OnAuthRequired（L279-285，无 scope 时） | `inner is AuthenticationException` | 触发 | 不触发 | **框架默认跳转失效** |

### 附带影响

`detail` 透传（L242 `GetValueOrDefault("detail") as string`）同样恒 null——G14（v4.10.37，服务端写 extensions.detail）的客户端读取路径实际也是失效的（G14 验收只验证了服务端写入，未验证 WASM 客户端读取）。

## 三、影响

| 场景 | 表现 | 严重度 |
|---|---|---|
| WASM 客户端会话过期 | OnAuthRequired 不触发 → 无弹窗/无跳转；用户停留在"请求静默失败"状态 | **P0**（会话过期引导链全断） |
| 登录失败（AUTH_FAILED）客户端识别 | IsAuthFailedError 恒 false → 无法按错误类型提示 | 中 |
| FORBIDDEN / NOT_FOUND 等客户端识别 | 同样失效，统一按普通 DomainError 处理 | 中 |

## 四、建议框架侧（按推荐序）

| # | 方案 | 说明 |
|---|------|------|
| 1 | **用 `JsonElement` 安全读取（根治）** | `MapGraphQLError` 改 `error.Extensions?.GetValueOrDefault("code")` 返回 `object?` → `if (o is JsonElement je && je.ValueKind == JsonValueKind.String) code = je.GetString();`（或 `o?.ToString()` 兜底）——兼容 STJ JsonElement 与字符串两种形态；`detail` 读取（L242）同步修复。 |
| 2 | **单元测试护栏** | 框架侧补 `MapGraphQLError` 反序列化测试：构造 `{"extensions":{"code":"AUTH_REQUIRED"}}` 响应 → 断言返回 `AuthenticationException`（当前必然失败，回归防护）。 |
| 3 | 统一错误读取 helper | 抽 `TryGetExtensionString` 公共方法，`code`/`detail` 统一走它。 |

## 五、验收标准（XiaoShuTong 侧）

框架修复 + 部署后：
- **主验收**：注入无效 SessionKey → 请求受保护接口 → `domainUser.OnAuthRequired` 触发 → App.razor 弹窗显示；点「重新登录」→ 跳 `/user/login?returnUrl=...` → 登录 → 回跳原页
- **回归**：`loginByPassword`/其他业务错误走 OnServiceError 类型/文案不回归
- 可移除应用侧 §六 兼容绕行

## 六、XiaoShuTong 侧现状（V0.7.7，兼容绕行）

- **已绕行**（2026-10-02）：App.razor 订阅 `domainUser.OnServiceError`，检测 DomainException 消息含会话过期特征（`Session expired` / `请先登录` / `会话已过期`）→ 与 OnAuthRequired 相同弹窗处理；两事件并存互不冲突（OnAuthRequired 正常触发时按原路径，OnServiceError 兜底）。
- **待框架修复后**：按 §五 验收 → 移除 OnServiceError 兜底分支，还原纯 OnAuthRequired。