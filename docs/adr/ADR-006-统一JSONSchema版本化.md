---
title: ADR-006 统一 JSON Schema 版本化落地
status: 活跃
date: 2026-09-04
superseded_by:
---

# ADR-006 统一 JSON Schema 版本化落地

## 状态

活跃

## 上下文

- 问题描述：现有题库为 `问题###答案` .txt 格式（435 原始题 + 1278 训练题），无结构化元数据（题型/学科/知识点/容错度/Prompt 绑定），无法支撑多学科框架与判题路由。需要统一的版本化 Schema 作为数据契约。
- 约束条件：现有 .txt 须无损解析迁移；题目用稳定 id（改版不删旧题，superseded_by 标记）；JSON Schema 草案 2020-12；跨学科跨题型通用。
- 相关需求：D01 七（题库设计）
- 关联 ADR：ADR-002（框架数据层）、ADR-003（判题契约）

## 选项

### 选项 A：维持 .txt 不结构化

- 描述：继续用 `问题###答案` 文本
- 优点：零迁移成本
- 缺点：无法承载题型/学科/容错度/同义词组元数据；判题路由无从分派；无法跨学科复用（Oracle 评审 F1：P1 标注"Schema 已落地"但解析器未落地，状态误导）

### 选项 B：统一 JSON Schema（question.v1.json + bank.v1.json）（选定）

- 描述：题目 Schema 含 id/bank_id/subject/type/purpose_tags/content（keywords 同义词组+权重）/meta（难度/来源/卡片关联/审计字段），题型键 R1-R4/O1-O5/X1-X3（R3 分 R3a/R3b 子型）；题库 Schema 含 supported_types/knowledge_card_template/type_overrides/privacy
- 优点：数据契约强约束（type 条件化校验 R4 展示型/O 客观型）；跨学科复用；与 D03 API 契约、D02 落库表直接映射；Oracle 评审修复后 JSON 合法
- 缺点：需解析器（.txt→JSON）；迁移一次性成本

## 决策

选定：选项 B（统一 JSON Schema 版本化落地）

理由：Oracle 评审（B1，高严重度）确认原 anyOf 排斥 R4 展示型、content.required 对卡片型不适用——需 type 条件化校验；版本化（question.v1.json V1.1）支持演进不破坏；schema 作为题库与 API/数据库间的单一契约源。

## 后果

- 正面影响：数据契约强类型；判题路由按 type 分派；跨学科复用基础
- 负面影响 / 风险：解析器未落地（P1 剩余）；迁移后需用 435 题跑 schema 校验回归
- 后续待办：.txt→JSON 解析器（P1）；现有题库迁移到 `题库/chinese/七-九年级-统编教材/` 结构

## 关联文档

- 需求文档版本：V3
- 架构文档版本：D01 V1.2.1
- 开发方案版本：-
