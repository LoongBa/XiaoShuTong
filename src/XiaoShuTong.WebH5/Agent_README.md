# WebUI 消费端脚手架（Agent 首要阅读文档）

> ⚠️ **Agent 强制先读**：此文件是消费端（WebUI/WebH5/AdminWeb）开发的首要阅读文档。**动手前必须先读**：
> 1. 本文件全部内容（尤其「消费者项目搭建最佳实践」章节——XiaoShuTong 实证踩坑沉淀，含 Dll 模式依赖/GraphQL 命名/pnpm workspace 预检清单）
> 2. 然后才读项目内 schema.graphql / ts-client.g.ts 等生成产物
>
> **定位**：WebApi 的 GraphQL schema 导出 + graphql-codegen TypeScript 生成管线 + ts-client / mock 消费链。
> Blazor Wasm 是 C# 前端，不消费 TypeScript；独立前端（React/Vue/H5）才需要。

## 文件清单

| 文件 | 用途 |
|:----|------|
| `Agent_README.md` | **本文件**：Agent 首要阅读 + 最佳实践沉淀（改名自原 README.md，避免与项目 README 混淆） |
| `buildSchema.ps1.template` | Schema 导出 + codegen 管线脚本（脚手架替换 `{Name}` 后放解决方案根目录） |
| `codegen.yml.template` | graphql-codegen 配置（`buildSchema.ps1` 自愈逻辑自动从模板创建，存于 WebApi 项目） |

## 工作流

```
buildSchema.ps1（解决方案根目录）
  ├─ 1. dotnet build WebApi
  ├─ 2. dotnet run -- schema export → schema.graphql（WebApi 项目）
  ├─ 3. 自愈：Ensure-CodegenConfig（从 AC-Kit 模板创建 codegen.yml）
  │         Ensure-NpmScript（添加 npm script 'codegen'）
  └─ 4. npm run codegen → graphql-codegen → TypeScript 类型（前端目录）
```

## 路径约定

| 路径 | 说明 |
|:----|------|
| `src/{Name}.WebApi/schema.graphql` | WebApi 导出的 GraphQL schema |
| `src/{Name}.WebApi/codegen.yml` | graphql-codegen 配置（由 buildSchema.ps1 自愈创建） |
| `src/{Name}.Wasm/` 或 `src/{Name}.AdminWeb/` | 前端项目目录（npm 包安装于此，codegen 输出于此） |
| `src/{Name}.Wasm/src/gql/types.ts` | 生成的 TypeScript 类型文件 |

## 自愈机制

`buildSchema.ps1` 的 `Ensure-CodegenConfig` 和 `Ensure-NpmScript` 函数在以下场景自动修复：

- **codegen.yml 不存在** → 从 `AC-Kit/scaffolding/WebUI/codegen.yml.template` 复制，替换 `{Name}` 为项目名
- **package.json 缺少 codegen script** → 自动添加 `codegen` 和 `codegen:watch` 脚本
- 首次运行 `buildSchema.ps1` 时自动完成所有配置，无需手动干预

## 消费者项目搭建最佳实践（踩坑沉淀，XiaoShuTong 实证 2026-09）

> 以下问题在 XiaoShuTong（WebApiBlazorWasm + WebH5 消费端）首次搭建 ts-client/mock 链路时全部踩到并修复。新项目脚手架落地后按此清单预检，避免重蹈。

### ① Dll 模式依赖契约（schema export 运行期失败）

**现象**：`dotnet run -- schema export` 抛 `Could not load file or assembly 'FreeSql, Version=X.Y.Z'`，或缺 `FreeSql.DbContext` / `FreeSql.Extensions.Linq`。

**根因**：
- 框架 refs 版本是 CPM 唯一权威：`build\refs\Directory.Packages.props` 定义了 FreeSql 等依赖的当前版本。项目 `Directory.Packages.props` 若锁旧版 → 运行期版本断层。
- **Dll 模式（refs 引用）框架 DLL 不传递 NuGet 依赖**：`TKWF.Domain.FreeSql.dll` 依赖 `FreeSql` / `FreeSql.DbContext` / `FreeSql.Extensions.Linq`，须消费者在 Domain.csproj 显式声明（版本由 CPM 统一）。

**修复**：
```xml
<!-- Directory.Packages.props：与 build\refs\Directory.Packages.props 对齐 -->
<PackageVersion Include="FreeSql" Version="3.5.311" />
<PackageVersion Include="FreeSql.DbContext" Version="3.5.311" />
<PackageVersion Include="FreeSql.Extensions.Linq" Version="3.5.311" />
<PackageVersion Include="FreeSql.Provider.PostgreSQL" Version="3.5.311" />
```
```xml
<!-- Domain.csproj：显式声明框架 DLL 的 NuGet 传递依赖 -->
<PackageReference Include="FreeSql" />
<PackageReference Include="FreeSql.DbContext" />
<PackageReference Include="FreeSql.Extensions.Linq" />
<PackageReference Include="FreeSql.Provider.PostgreSQL" />
```

**预检**：改完清 obj 重建后，跑一次 `dotnet run -- schema export`——**能导出 = 运行期依赖链完整**（编译通过不代表运行期无缺）。

### ② GraphQL 类型名全局唯一（跨域同名 DTO）

**现象**：schema export 抛 `The name 'XxxDto' was already registered by another type`。

**根因**：HotChocolate 按 **C# 短类名**注册 ObjectType（非全名）。不同域的同名 DTO（如 Learning/Stats 的 `GetWrongQuestionsResDto`）必然冲突；`[GenerateController(ControllerName=...)]` 只消歧控制器，**不消歧 DTO**。

**修复（命名纪律）**：
- 跨域可暴露的 Res/Item DTO 一律 `{域前缀}{ClassName}Dto`（如 `StatsGetWrongQuestionsResDto`、`ParentWeakPointDto`）；
- 跨域共享 DTO 放 `Services/Shared/` 且全局查重（可用 `grep "record XxxDto"` 遍历，同名跨目录即预警）。

**预检**：`Select-String -Path schema.graphql -Pattern "^type " | Measure-Object`——schema 构建成功即类型名唯一。

### ③ schema export 输出路径

`dotnet run` 的工作目录 = WebApi 项目目录。`--output` 传**绝对路径**（`Resolve-Path`），勿传相对路径（会被二次拼接成 `<WebApi>\<WebApi>\schema.graphql`）。

### ④ MSB3492（obj 缓存损坏）

**现象**：`MSB3492: 无法读取现有文件 obj\...\CoreCompileInputs.cache`。

**修复**：`dotnet build-server shutdown` → 删 `obj/` → **逐项目 build**（Domain 先、WebApi 后）→ 脚本重跑。勿在损坏缓存上硬跑（脚本 `dotnet build -q` 并行构建时更易触发）。

### ⑤ 跨仓库 SDK 依赖用 pnpm workspace（替代 file: 相对深度）

**现象**：`file:../../../../tkwf-tsclient` 相对路径随目录层级漂移（DMP 2 级 / WebH5 3 级），易错。

**修复**：解决方案根 `pnpm-workspace.yaml` 声明消费端 + SDK 仓库，package.json 用 `workspace:*`：

```yaml
# <解决方案根>/pnpm-workspace.yaml
packages:
  - 'src/{Name}.WebH5'        # 消费端
  - '../tkwf-tsclient'        # @tkwf/tsclient SDK 仓库
  - '../TKWF-tsclient-mock'   # @tkwf/tsclient-mock SDK 仓库
allowBuilds:
  esbuild: true
```
```json
"@tkwf/tsclient": "workspace:*",
"@tkwf/tsclient-mock": "workspace:*"
```

SDK 位置只在 workspace 一处声明，改路径只改一处，pnpm 符号链接复用。

### ⑥ gen-ts-client 运行时依赖

`@tkwf/tsclient` 的 `parseGraphQLSchema`（生成 GraphQL_Api.md）需 `graphql` 运行时包：`pnpm add graphql`（勿只做 devDependency，codegen 脚本运行时需要）。

---

## 本项目（XiaoShuTong）专属对接指引

> 框架通用最佳实践见上文。以下是本项目的实测路径与当前限制，**必须先读**。

### P1. 项目资产位置

| 资产 | 路径 | 说明 |
|---|---|---|
| schema.graphql | `src/XiaoShuTong.WebApi/schema.graphql` | buildSchema.ps1 导出（WebApi 项目内） |
| ts-client.g.ts | `src/XiaoShuTong.WebH5/src/gql/ts-client.g.ts` | 本仓库 codegen 产物（勿手改） |
| GraphQL_Api.md | `.TKWF/GraphQL_Api.md` | gen-ts-client 附带生成 |
| MOCK_SPEC.md | `.TKWF/MOCK_SPEC.md` | mock 数据策略（8 域，已物化） |
| Business.md | `.TKWF/Business.md` | 248 BR + 20 C（mock 字段策略依据） |

### P2. 框架 SG2 修复状态（✅ 已修复，v4.9.109 起）

**历史问题（已解决）**：全部 Service 统一 `ExecuteAsync` 命名 + SG2 resolver 字段名=方法名 → 冲突静默丢弃，GraphQL 仅暴露 8 操作（Query 4 + Mutation 4），55 个业务服务不可达。已转交框架组修复。

**✅ 当前状态（v4.9.109 已验证）**：
- SG2 resolver 字段名 = `{ServiceShortName}_{MethodName}`（如 `listTasks_Execute` / `createBank_Execute`），**57 个服务全部可达**
- schema 实测：Query **56** 字段 + Mutation 4 = 60（对比修复前 4+4）
- ts-client.g.ts 重建：**60 operations → 58 services → 191 types**（1352 行）

**命名自洽验证（无需改消费端）**：
- SG2 resolver 方法名（`AcceptBuddyInvite_Execute`）→ HotChocolate 自动映射为 GraphQL 字段（`acceptBuddyInvite_Execute`）→ gen-ts-client.ts 从 schema 提取 → ts-client `Query.acceptBuddyInvite_Execute`——**三层同源（schema 驱动），零适配**
- SG3/Wasm：本项目无 SG3 客户端代理产物（ts-client 走 WebH5 前端独立路径），无需改动
- 命名形态：方法名带 `_Execute` 后缀属约定风格（服务接口如 `AcceptBuddyInvite_ExecuteService`），非缺陷；UI 若偏好简洁命名可后续用框架 `ExposeAlias` 映射

**若想保留原 `execute` 字段名**：需各服务统一方法命名避开重名（即在 SG2 修复机制之外自行保证方法名唯一）。

### P3. 常用命令（WebH5 目录）

```powershell
pnpm install          # 安装（workspace 解析双 SDK）
npx tsx scripts/gen-ts-client.ts   # 重新生成 ts-client.g.ts（需 schema 已导出）
pnpm run gen-mock    # gen-mock-handlers 填充 ts-client.mock.g.ts（需 ts-client.g.ts）
```

### P4. mock 数据生成步骤（UI Agent 场景）

1. 后端 schema 已导出（`buildSchema.ps1` 或 `dotnet run -- schema export`）
2. `npx tsx scripts/gen-ts-client.ts` → 生成 ts-client.g.ts（已就绪）
3. `pnpm run gen-mock` → 生成 ts-client.mock.g.ts + 填充 MOCK_SPEC.md §1
4. 按 `.TKWF/MOCK_SPEC.md` §2/§3 填 `src/mock/data.ts` → `pnpm run dev:mock`