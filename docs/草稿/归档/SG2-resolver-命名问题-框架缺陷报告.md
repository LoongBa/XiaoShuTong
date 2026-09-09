# SG2 Resolver 字段命名缺陷报告 — 已闭环归档

> **状态**：✅ 已归档（2026-09-09）——消费者接受为框架内置提示（消歧已生效）；框架组反馈已记录（见 §四·决议）
> **报告方**：XiaoShuTong 项目（消费者）
> **日期**：2026-09-08
> **决议日期**：2026-09-09
> **框架模块**：`_TKWF/_Domain.SG/ApiService.SG/ApiServiceGenerator.Resolvers.cs`
> **项目证据**：`XiaoShuTong.WebApi/schema.graphql`（45.9KB）· `XiaoShuTong.WebH5/src/gql/ts-client.g.ts`

---

## 一、问题概述

**SG2 生成 GraphQL resolver 字段时，直接用服务方法名作为字段名，未包含"服务名"维度。** 当消费者项目约定统一入口方法命名（如全部 `ExecuteAsync`）时，不同服务的 resolver 字段必然重名冲突，且 SG2 **静默丢弃冲突字段**（不报错、不告警），导致大量业务服务在 GraphQL 层不可达。

---

## 二、影响范围（XiaoShuTong 实证）

| 指标 | 值 |
|---|---|
| 领域 Service 数 | **57**（全部 `ExecuteAsync` 命名） |
| AOP 控制器数（REST 可达） | 54 |
| GraphQL 暴露的业务入口 | **仅 2 个**：`execute`（ListTasks）+ `execute_ByRequest`（RemoveMember，参数消歧幸存） |
| schema 总字段（含 auth） | Query 4 + Mutation 4 = **8** |
| ts-client.g.ts | 8 operations / 6 services（4 auth + execute×2 + registerSecure/changePasswordSecure） |
| GraphQL 不可达服务 | **55 个**（群组/学习/题库/搭子/PK/家长等全部业务域） |

**对照 DMP-Lite（无此问题）**：服务方法名各异（`listMembers`/`queryPaymentLogs`/`createCouponAsync`），resolver 字段天然唯一。

---

## 三、根因分析

```csharp
// ApiServiceGenerator.Resolvers.cs（关键路径）
var resolverMethodName = method.ResolverName;   // ExecuteAsync → "execute"
descriptor.Name(resolverMethodName + ...);      // 直接作为 GraphQL 字段名
```

- SG2 的 `ResolverName` = 方法名剥离 Async 后缀（`ExecuteAsync` → `execute`）
- **无服务名前缀**、无去重、冲突时按参数消歧（`execute_ByRequest`）仅个别幸存
- Controller 有 `[GenerateController(ControllerName = "...")]` 消歧入口，**resolver 无对应消歧机制**
- schema export 不报错 → 服务"静默丢失"难以发现

---

## 四、建议方案（框架组评估）

| 方案 | 做法 | 备注 |
|:---:|------|------|
| **A** | resolver 字段命名加入服务名维度：`{ServiceShortName}_{MethodName}`（对齐 `TypeNameConvention.GetServiceShortName`） | 默认无冲突；兼容 DMP 现有多样化命名（加前缀不破坏既有 schema 语义） |
| **B** | 提供方法/服务别名注解（类似 `[GenerateController(ControllerName)]` → `[GraphQLField(Name=...)]` / 复用 `ExposeAlias`） | 消费者显式消歧，灵活但需每处标注 |
| **C** | SG2 检测 resolver 字段重名时**告警/报错**（而非静默丢弃） | 最小改动，先防"悄然丢失" |

> 建议组合：**C 先行（快速止损）+ A 或 B 根治**。

### 决议（2026-09-09，消费者接受 + 框架组反馈）

XiaoShuTong 侧定案：**保留统一 `ExecuteAsync` 服务约定，接受 WARN004 为框架内置提示**（SG2 已自动消歧为 `{服务短名}_Execute`，schema 与 ts-client 契约已再生成且一致，行为正确），不执行 53 个服务全量改名（破坏性 API 变更）。

**框架组反馈（2026-09-09）**：

> WARN004 已是 Warning 级且去重后只报一次，噪音可控，**可不加开关**。

消费者侧核实结论（XiaoShuTong 对部署 refs 4.9.113 实测）：

- ✅ **采纳"不加开关"建议**：WARN004 当前即 Warning 级、整桶只报一次 → 无需任何开关属性，XiaoShuTong 不加标注、不加 NoWarn。
- ⚠️ 框架组反馈中提到的 `[TKWFSeverity(TKWFDiagnostic.WARN004, EnumSeverity.Hidden)]` 标注方式**当前部署版本不可用**（供框架组参考，非本项目实施项）：
  - `TKWFDiagnostic` 枚举（`_TKWF\_Framework\Abstractions\Extensions\TKWFDiagnostic.cs`）当前仅含 `DI001`/`TKWF0034`，**无 WARN004**；
  - `EnumSeverity` 仅含 `Warning`/`Error`，**无 `Hidden`**；
  - 机制差异：`TKWFSeverity` 是**运行时 Guard** 严重级别覆盖（标注于 `ExtensionInitializer`/`DomainHostInitializerBase`），而 WARN004 是 **SG2 编译期诊断**（`Guard.cs` `Warning(SgId.Sg2a, "WARN004", ...)` 发射）——如需支持该标注方式，框架需将编译期诊断接入同一 severity 覆盖管线。

---

## 五、项目侧临时缓解（不阻塞 UI 开发）

1. **REST 通道不受影响**：`/api/{controller}` 按控制器名路由，54 个控制器 REST 全部可达
2. UI 消费端暂走 REST：`transportType: "rest"` + `restExplicitBodyQueries` 配置（tkwf-tsclient V4.9.19 支持）
3. 长期依赖框架组落地方案 A/B 后，GraphQL（QueryBuilder / ts-client）才可完整覆盖

---

## 六、验证方法（复现与回归）

```powershell
# 复现：导出 schema 后比对字段数 vs Service 数
# schema Query/Mutation 字段数（8）应接近 Service 数（57）才对
Select-String "type Query" src/XiaoShuTong.WebApi/schema.graphql

# gen-ts-client 输出 Services 计数
cd src/XiaoShuTong.WebH5 && npx tsx scripts/gen-ts-client.ts
# 期望：Services: N 接近控制器数 54；当前仅 6
```

**框架组验收标准**：同一 `ExecuteAsync` 命名项目，schema 暴露字段数 = Service 数（无静默丢弃）；或消费者可显式指定 resolver 字段别名。

---

## 七、关联产物

| 文件 | 说明 |
|---|---|
| `XiaoShuTong.WebApi/schema.graphql` | 当前 schema（暴露 8 字段，证据） |
| `XiaoShuTong.WebH5/src/gql/ts-client.g.ts` | gen-ts-client 产物（6 services，证据） |
| `XiaoShuTong.WebH5/scripts/gen-ts-client.ts` | 消费端 codegen 脚本（复用 DMP 模板） |
| `.TKWF/LOG.md` | 原始记录入口（条目 ③ 指向本文件） |
