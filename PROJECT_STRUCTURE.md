# 项目结构（XiaoShuTong）

> 活文档：仓库实际目录结构的权威索引。**目录结构变化时必须同步更新本文件**（见 `Agents_Use_TKWF.md §3 进度同步纪律`）。

```
XiaoShuTong/
├── Agents_Use_TKWF.md              ← OpenCode 项目级注入的开发规则（.opencode/opencode.json instructions 特配加载，非惯例 AGENTS.md 命名——勿改）
├── .opencode/
│   ├── opencode.json               ← OpenCode 配置（instructions 加载规则文件）
│   └── node_modules/               ← 本地插件依赖（gitignore）
├── .agents/skills/                 ← tkwf-* 项目级 skills（tkwf-business / entity / service / test / tsclient 等）
├── .TKWF/                          ← TKWF 活态文档与路由中枢
│   ├── TKWF-Rules.md               ← 增量开发路由（需求分层、Skill 路由、红线）
│   ├── DOMAIN_MAP.md               ← 实体数据地图（24 实体 + 1 视图，xCodeGen 生成）
│   ├── Domain_Api.md               ← AOP 控制器契约（SG1 生成）
│   ├── DataService_API.md          ← 数据服务方法索引（xCodeGen 生成）
│   ├── GraphQL_Api.md              ← GraphQL schema 契约（SG2 生成）
│   ├── Business.md                 ← 手写业务规则总纲（9 域 BR + 跨实体约束）
│   └── MOCK_SPEC.md / LOG.md / delegation-checklist.md
├── XiaoShuTong.slnx                ← 唯一解决方案（Dll 模式，4 项目）
├── Directory.Build.props           ← TkwfReferenceMode=Dll、MinVerTagPrefix=v、TKWFDeployPath 校验
├── Directory.Packages.props        ← CPM 集中包版本管理
├── Directory.Build.targets
├── buildSchema.ps1                 ← API 变更后导出 schema + ts-client codegen
├── pnpm-workspace.yaml / pnpm-lock.yaml  ← WebH5 前端包管理
├── README.md                       ← 仓库总览（其中目录树为模板性描述，实际结构以本文件为准）
├── PROJECT_STRUCTURE.md            ← 本文件
├── src/
│   ├── XiaoShuTong.Domain/         ← 领域层（Entity/VEntity/Service/Tools，TKWFRole=Domain）
│   ├── XiaoShuTong.WebApi/         ← REST + GraphQL 网关宿主（TKWFRole=ApiService）
│   ├── XiaoShuTong.AdminWasm/      ← 管理端 Blazor WASM（TKWFRole=ApiClient，独立版本线 AdminWasm-V，见其 AGENTS.md）
│   └── XiaoShuTong.WebH5/          ← 学生/家长端 React SPA（非 .NET，pnpm 工程，含子级 AGENTS.md）
├── tests/
│   └── XiaoShuTong.Tests/          ← 唯一测试项目（xunit.v3 + Tier 1.5 SQLite :memory:，68 文件 / 345 用例，见其 AGENTS.md）
├── 题库/                           ← 官方内容资产（7 学科四件套 + schema + 判题 Prompt，规范见 题库/AGENTS.md 与 题库建设规范.md）
└── docs/                           ← 项目文档（路由指南见 docs/AGENTS.md）
    ├── AGENTS.md                   ← 文档路由指南（入口总览 + 路由表）
    ├── 变更记录.md / 目录结构与版本管理规则.md / 平台管理系统-迭代计划.md / 平台管理系统需求方案.md
    ├── D01~D07 设计文档 + V*.0 开发方案/审核报告 + AdminWasm-V*.x 审核报告
    ├── 0-产品规划/ 1-需求分析/ 2-架构设计/ adr/ 模板/ 草稿/（含归档/） ...
```

## 勘误备忘（历史文档失实点，已按实际修正）

| 旧记载 | 实际 | 处置 |
|--------|------|------|
| `XiaoShuTong.Dev.slnx`（基于框架源码的测试项目） | **不存在**——仅 `XiaoShuTong.slnx`（Dll 模式，4 项目） | `Agents_Use_TKWF.md §1` 已同步修正；如未来恢复 ProjectRef/源码联调需另行决策建 Dev.slnx |
| `XiaoShuTong.Domain/` 等位于仓库根 | 位于 **`src/`** 子目录 | 本文件已按 src/ 组织 |
| `XiaoShuTong.AdminWeb/`（前端 SPA） | 拆分为 **AdminWasm**（Blazor 管理端）+ **WebH5**（React 学生/家长端） | 本文件已列全 |
| `build.ps1` / `scripts/` | 根目录**不存在**；仅有 `buildSchema.ps1`；WebH5 内自带 `scripts/` | 已移除幽灵项 |
