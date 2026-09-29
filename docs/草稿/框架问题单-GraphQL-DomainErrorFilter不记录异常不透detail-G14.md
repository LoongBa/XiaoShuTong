---
title: 框架问题单——GraphQL 通道异常可观测性黑洞（DomainErrorFilter 不记录异常、不透 detail）（G14）
status: ✅ XiaoShuTong 验证 PASS（框架 v4.10.37 本地修复已部署，待框架组正式提交）
date: 2026-09-29
source: XiaoShuTong 反编译定案 + 运行实证（框架 v4.10.36，2026-09-29）
---

# 框架问题单：GraphQL 通道异常可观测性黑洞（DomainErrorFilter 不记录异常、不透 detail）（G14）

> **✅ 验证通过（2026-09-29 11:06，框架 v4.10.37 本地修复）**：框架组本地 worktree 已修复（未提交 git log，
> 2026-09-29 10:51 编译推送 refs）：`DomainErrorFilter.cs` 源码注释明写 **v4.10.37 (G14)**——
> ① 注入 `ILogger<DomainErrorFilter>`，`OnError` 中 `LogError` 完整记录异常（对齐 REST `WebExceptionMiddleware`）；
> ② `env` 由 `IWebHostEnvironment` 改为 `IHostEnvironment` + `WebHotChocolateExtensions.AddApplicationService<IHostEnvironment>()`
> → `isDev` 正确 → **Development 下 `extensions.detail` 携带完整堆栈（修复前 detail 死代码）**。
> XiaoShuTong 重建后验证：触发 INTERNAL_ERROR → 响应 `extensions.detail` **含完整堆栈**，消息 i18n 英文化 +
> `translationKey` 保留——**修复生效**。
>
> **日志侧实证（2026-09-29 11:1x，项目组控制台实录）**：触发 `notexist99` 登录失败时服务端现两条 `fail:` 日志：
> ① `AuthController'1.LoginByContextAsync()[0] 领域方法执行异常 | 方法: LoginByContextAsync | 用户: Guest`（AOP 拦截器，
> 含完整堆栈）；② **`DomainErrorFilter[0] GraphQL OnError - code: (null) - path: loginByContext`**（G14 修复的
> LogError，含完整堆栈，对齐 REST `WebExceptionMiddleware`）——**修复前 GraphQL 通道零异常日志，现异常黑洞已消除**。

> **背景**：排查 G12/G13 过程中，反复受困于 **GraphQL 通道真实异常不可见**——`loginByContext` 首登曾返回
> INTERNAL_ERROR 但**响应无 detail（即使 Development）、控制台无任何异常日志**。反编译定案：**GraphQL 边界
> `DomainErrorFilter` 不调用 `ILogger.LogError`，且其 `IWebHostEnvironment` 注入恒 null → `detail` 分支为死代码**。
> 独立为可观测性框架缺陷，直接影响所有 GraphQL 异常定位，建议框架组统一修复。

## 一、现象

1. **GraphQL resolver 异常 → INTERNAL_ERROR，控制台无异常日志**：
   反编译确认 `DomainErrorFilter.OnError` 对未识别异常走
   `Build(error, "INTERNAL_ERROR", ..., flag ? exception.ToString() : null, "Error_InternalError")`，
   **全程无 `ILogger.LogError`**——真实异常类型/栈只在响应 `detail`（Development 才可能透出），不发日志。
2. **`detail` 分支实为死代码（比已知更严重）**：`bool flag = env?.IsDevelopment() ?? false;`
   - `IWebHostEnvironment? env` 经 HC `AddErrorFilter<DomainErrorFilter>()` 在其**独立 schema DI scope** 构造，
     拿不到宿主 `IWebHostEnvironment` → **`env` 恒 null** → `flag` 恒 false。
   - **实证**：Development 环境（openapi/scalar 均映射、`ASPNETCORE_ENVIRONMENT=Development` 确认）下，
     GraphQL INTERNAL_ERROR 响应**仍无 `extensions.detail`** → detail 分支永不触发。
3. **REST 与 GraphQL 边界日志行为不一致**：
   - REST 经 `WebExceptionMiddleware`（`WebExceptionMiddleware.cs` L54）`_Logger.LogError(exception, ...)` —— **有日志**。
   - GraphQL 经 `DomainErrorFilter` —— **无日志** → **GraphQL 通道成异常黑洞**。

## 二、根因（反编译定案）

- 反编译 `TKWF.Domain.ApiService.HotChocolate.dll` `DomainErrorFilter.OnError`：
  `bool flag = env?.IsDevelopment() ?? false;` + `detail = flag ? exception.ToString() : null`，
  且**无任何 LogError 调用**。
- `DomainErrorFilter` 构造 `(IWebHostEnvironment? env = null, IFrameworkLocalizer? localizer = null)`，
  由 HC `AddErrorFilter<DomainErrorFilter>()`（`WebHotChocolateExtensions.AddGraphQLApiService`）在其
  **schema 独立 DI scope** 解析——宿主 `IWebHostEnvironment` 未注入 → `env` null。
- 佐证：排查期间临时在 Application 侧注册 `DiagnosticErrorFilter`（构造注入 `ILogger`）同样因 HC 独立
  DI scope 无法解析而启动失败——印证 HC filter 的 DI 隔离。

## 三、影响

- **生产环境真实异常不可见**：DB 故障/会话竞态/未预期异常全被吞为 INTERNAL_ERROR 且无日志，无法排查。
- **开发联调效率受阻**：须临时改中间件/反编译/改 `UseWebExceptionMiddleware` 才能定位，且即便如此
  `detail` 也因 env null 不出现。
- **REST 与 GraphQL 不一致**：REST 会 LogError，GraphQL 不 log——同一异常两个通道表现不同，误判率高。

## 四、复现路径（框架组可复现）

1. 方式 A（反编译）：`ilspycmd` 反编译 `DomainErrorFilter`，确认 `OnError` 无 `LogError` + `flag=env?.IsDevelopment()??false`。
2. 方式 B（运行实证）：WebApi 配置 `IsDevelopment:true`（Development）→ 触发一个 GraphQL resolver 异常
   （如无效登录/内部缺陷）→ 断言：控制台无 `LogError` 异常日志，响应 `extensions.detail` 为 null。

## 五、建议框架侧修复

| # | 修复方向 | 说明 |
|---|---------|------|
| 1 | **`DomainErrorFilter.OnError` 补 `ILogger.LogError(exception, ...)`** | 与 `WebExceptionMiddleware`（REST 边界）对齐；`IsDevelopment` 只控制响应 `detail` 是否透出，**日志始终记录** |
| 2 | **修复 `detail` 死代码** | 确认 HC `AddErrorFilter<T>()` 的 DI 注入到宿主 `IWebHostEnvironment`（或 HC scope 补充注册），使 Development 下 `detail` 能透出完整栈 |
| 3 | **统一 REST/GraphQL 错误边界日志策略** | 两通道异常均应落日志，仅响应详情按环境隐藏 |

**关键验证点**：修复后 GraphQL resolver 异常时**控制台必有 `LogError` 日志**（含异常类型/栈）；Development 下响应 `extensions.detail` 含完整栈。

## 六、验收标准（XiaoShuTong 侧）

- 主验收：触发任一 GraphQL resolver 异常 → 控制台出现 `ILogger.LogError` 异常日志（含类型+栈）。
- Development 下：响应 `extensions.detail` 非 null（含完整栈）。
- 回归：INTERNAL_ERROR 码/`translationKey`/消息语义不变；REST 边界行为不回归。

## 附：相关证据

- 反编译结果：`DomainErrorFilter.OnError`（HC DLL 反编译，本轮临时目录，可重新 ilspycmd 生成）。
- 对比源码：`WebExceptionMiddleware.cs` L54 `LogError` vs `DomainErrorFilter` 无 LogError（`F:\LoongBa_Git\_TKWF`）。
- 关联：G12/G13（会话层缺陷因本单黑洞而难定位）；建议与 G12/G13 一并反馈框架组。
