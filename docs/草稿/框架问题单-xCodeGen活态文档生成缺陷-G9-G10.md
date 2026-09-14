---
title: 框架问题单——xCodeGen 活态文档生成缺陷（G9/G10）
status: 待框架组处理
date: 2026-09-14
source: XiaoShuTong 活态文档审计（2026-09-14）+ xCodeGen AfterBuild 实证（v4.10.23-preview）
---

# 框架问题单：xCodeGen 活态文档生成缺陷（G9/G10）

> **背景**：Tier 1.5 问题单（G1-G8+F1/F2+G6/G6b+G7/G7b）已全部闭环归档（见
> `docs/草稿/归档/框架问题单-Tier1.5-SQLite内存库-v4.10.8不可用.md`）。本问题单为 **2026-09-14 活态文档审计**
> 新发现 xCodeGen 文档生成缺陷，独立主题，转交框架组处理。
>
> **实证链路**：`dotnet build` 触发 xCodeGen AfterBuild（v4.10.23-preview 正常产出）+ 全链路同步
> （`buildSchema.ps1` → schema.graphql → gen-ts-client → gen-mock-handlers --mock-spec）均正常执行。

## 一、现象

### G9：DOMAIN_MAP.md 未收录 VEntity（PkPlayerStatsView）

xCodeGen AfterBuild 生成的 `.TKWF/DOMAIN_MAP.md` 列出 **24 个实体、0 个 VEntity**——而
`PkPlayerStatsView`（`[DomainGenerateCode(IsView=true)]` + ViewSql + ViewSqlSQLite，手写定义与 `.g.cs`
均存在且于 09-12 生成）**未出现在文档中**。同时 `.TKWF/Business.md` Pk-BR-24~27 引用 `PkPlayerStats`
实体 → **文档间引用断裂**（Business.md 引用的实体在 DOMAIN_MAP.md 中无对应行）。

### G10：实体描述占位符泄漏——"条件工厂（V4.9）..." 写入实体描述

DOMAIN_MAP.md 24 实体中 **18 个描述字段为占位符**：`条件工厂（V4.9）。支持两种使用方式：
Xxx.Conditions.ByXxx(value)...`——仅 6 个实体（AiModelConfig/Attempts/Banks/BetaInviteCodes/Questions/StudyBuddies）
有真实业务描述。泄漏源为各实体生成文件（`Conditions/{Entity}Conditions.g.cs` / `{Entity}.g.cs`）的 XML Doc。

## 二、根因（待框架确认）

| 缺口 | 疑似根因 |
|------|---------|
| **G9** | xCodeGen `EntityMetadataGenerator` 写 DOMAIN_MAP.md 时仅枚举 `IsView=false` 实体，或按类型过滤时遗漏 VEntity——DOMAIN_MAP 表头声称"实体 vs 视图"两类，但实际仅输出实体行 |
| **G10** | xCodeGen 实体描述提取**取错 Doc 源**——从 Conditions 工厂类/实体生成文件的 XML Doc 提取占位文本，而非手写实体类（`Entities/{Entity}.cs`）的 `/// <summary>` 业务描述 |

## 三、影响

- **G9**：活态文档缺失 VEntity——领域视图设计（Aggregation 统计）无文档映射；`Business.md` BR 引用
  断裂，审计/消费端检索断层
- **G10**：18/24 实体无业务描述——活态文档价值减半；实体 `/// <summary>` 真实描述被占位符遮蔽，
  检索与审计误导

## 四、建议框架侧

| 缺口 | 建议 |
|------|------|
| **G9** | DOMAIN_MAP.md 生成时收录 VEntity 行（标注类型=视图）；补 PkPlayerStatsView 类回归用例 |
| **G10** | ① 实体描述优先取手写实体类的 `/// <summary>`（非生成文件/非 Conditions 工厂 Doc）；② 无手写描述时输出空或"待补充"，而非泄漏内嵌占位文本 |

## 五、验收标准（XiaoShuTong 侧）

框架修复 + 部署后，重新 `dotnet build` 触发 xCodeGen：
- DOMAIN_MAP.md 出现 `PkPlayerStatsView | 视图` 行（G9 关闭）
- 18 个占位符描述替换为实体真实 `/// <summary>` 或空/待补充（G10 关闭）