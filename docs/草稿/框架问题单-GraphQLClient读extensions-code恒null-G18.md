---
title: 框架问题单——GraphQLClient.MapGraphQLError 读 extensions.code 恒 null（STJ JsonElement as string），业务码映射全失效（G18）
status: ✅ 已修复（框架 v4.10.48 根治 + 测试护栏已落地；XiaoShuTong 需重建 WASM 消费新 DLL 后按 §五 验收、移除 §六 绕行）
date: 2026-10-02
source: XiaoShuTong AdminWasm 会话过期弹窗走查（2026-10-02）——注入无效 SessionKey 后请求 listGroups，服务端返回 AUTH_REQUIRED，但客户端 OnAuthRequired 不触发（Session expired 走了 OnServiceError）
updated: 2026-10-02（框架侧修复核实回写，见 §七）
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

## 七、框架侧修复核实（2026-10-02 回写，G18/BUG006）

### 7.1 框架源码根治（v4.10.48，commit `046b92cd`，2026-10-02 06:10:50）

- `GraphQLClient.cs` 新增私有静态方法 `TryGetExtensionString(Dictionary<string, object?>?, string key)`——统一兼容 **string** 与 **STJ 装箱 JsonElement**（`ValueKind == JsonValueKind.String` → `GetString()`）两种形态；非字符串值保守返回 null（不字符串化，避免误匹配）。
- `MapGraphQLError` 改为 `var code = TryGetExtensionString(error.Extensions, "code");`——AUTH_REQUIRED/AUTH_FAILED/FORBIDDEN/NOT_FOUND/VALIDATION_ERROR 映射恢复。
- `detail` 透传（原 L242）同步改走 `TryGetExtensionString(..., "detail")`——G14 服务端写 extensions.detail 的客户端读取路径一并修复。
- 适用性边界（helper 注释已标注）：仅适用于 `code`/`detail` 等字符串契约 key；`messageArgs`（数组）等非 string 扩展值须独立解析，复用会静默 null。

### 7.2 单元测试护栏（问题单 §四 方案 2 已落地）

`_TKWF\_Tests\Domain.ApiService.HotChocolate.Tests\GraphQLClientTests.cs` 新增 4 用例：
- `SendQueryAsync_ExtensionsCodeAuthRequired_FiresOnAuthRequired`——`{"extensions":{"code":"AUTH_REQUIRED","detail":"<dev-stack-trace>"}}` → OnAuthRequired 触发 + 内层 `AuthenticationException` + `IsAuthRequiredError()` 识别 + detail 透传断言。
- `SendQueryAsync_ExtensionsCodeAuthFailed_FiresOnAuthFailed`——AUTH_FAILED → OnAuthFailed + 内层 `UserLogonException`。
- `SendQueryAsync_NoCode_FallsBackToDomainException`——无 code 回退默认分支（内层 DomainException，OnServiceError）。
- `SendQueryAsync_ExtensionsCode_MapsToCorrectExceptionType`（Theory）——全 ErrorCodes 码→异常类型映射断言（防常量漂移/switch 误改）。

### 7.3 部署 DLL 实证（XiaoShuTong 实际消费产物）

- XiaoShuTong 为 **DLL 模式**（`TkwfReferenceMode=Dll`，AdminWasm 经 `$(TKWFDeployPath)build\refs\` 引用 `TKWF.Domain.ApiClient.dll`）。
- 部署根 `F:\TKWF_FRAMEWORK_PATH\build\refs\TKWF.Domain.ApiClient.dll` 构建于 **2026-10-02 07:17:34**（晚于修复提交 06:10:50）；解编译确认含 `TryGetExtensionString` + 新 `MapGraphQLError`——**修复已进入实际消费 DLL**。

### 7.4 XiaoShuTong 侧待办（待执行）

1. **重建 + 重发布 AdminWasm**——捆绑新 `TKWF.Domain.ApiClient.dll`（旧发布产物仍含缺陷 DLL）。
2. 按 §五 主验收走查：注入无效 SessionKey → 请求受保护接口 → `domainUser.OnAuthRequired` 触发 → App.razor 弹窗 → 「重新登录」跳 `/user/login?returnUrl=...` → 登录 → 回跳原页。
3. 回归：`loginByPassword`/其他业务错误仍走 OnServiceError，类型/文案不回归。
4. 验收通过后**移除 §六 OnServiceError 兜底分支**，还原纯 OnAuthRequired 路径。