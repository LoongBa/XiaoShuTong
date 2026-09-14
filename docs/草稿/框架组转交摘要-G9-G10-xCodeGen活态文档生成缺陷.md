---
title: 框架组转交摘要——xCodeGen 活态文档生成缺陷（G9/G10）
date: 2026-09-14
source: XiaoShuTong 活态文档审计（docs/草稿/框架问题单-xCodeGen活态文档生成缺陷-G9-G10.md）
转交状态: 待框架组处理
---

# G9/G10 转交摘要：xCodeGen 活态文档生成缺陷

> 完整上下文见 `docs/草稿/框架问题单-xCodeGen活态文档生成缺陷-G9-G10.md`。

## G9 — DOMAIN_MAP.md 未收录 VEntity

- **现象**：`PkPlayerStatsView`（IsView=true + ViewSql + ViewSqlSQLite，.g.cs 已生成）**未出现在 DOMAIN_MAP.md**（24 实体 / 0 视图）。Business.md Pk-BR-24~27 引用 `PkPlayerStats` → 文档引用断裂
- **疑似根因**：xCodeGen 生成 DOMAIN_MAP.md 时仅枚举 `IsView=false`，遗漏 VEntity
- **建议**：DOMAIN_MAP.md 收录 VEntity 行（类型=视图）+ 回归用例

## G10 — 实体描述泄漏占位符

- **现象**：18/24 实体描述为 `条件工厂（V4.9）...` 占位符（泄漏自 `Conditions/*.g.cs`/`*.g.cs` XML Doc），仅 6 个实体有真实业务描述
- **疑似根因**：实体描述提取取错 Doc 源（Conditions 工厂/生成文件，而非手写实体类 `/// <summary>`）
- **建议**：① 描述优先取手写实体类 `<summary>`；② 无描述时输出空/待补充，不泄漏占位文本

## 验收标准（XiaoShuTong 侧）

框架修复部署后 `dotnet build` 触发 xCodeGen：DOMAIN_MAP.md 出现 `PkPlayerStatsView | 视图`（G9）；18 个占位符替换为真实描述或空/待补充（G10）。