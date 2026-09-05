---
title: ADR-007 技术栈分端调整：前端统一 React + shadcn/ui
status: 活跃
date: 2026-09-05
superseded_by:
---

# ADR-007 技术栈分端调整：前端统一 React + shadcn/ui

## 状态

活跃

## 上下文

- 问题描述：ADR-001 原定前端统一 Vue3 + Uni-app（H5 + 小程序一套代码）。经产品战略收敛（《产品战略与定位 V1》：老师端 PC 教研 + 学生/家长端移动）、技术调研与 AI 开发模式评估后，前端方案需调整为分端差异化。
- 约束条件：
  1. 产品形态：**H5 先行**（微信接入/小程序接入未开通，H5 即点即用是 MVP 唯一前端）
  2. 开发模式：**AI agent 协作开发**——框架的"AI 友好度"（生成代码一次通过率、语料充足度）直接影响迭代速度
  3. AI 友好度评估：React + shadcn/ui 训练语料充足、API 标准（fetch/hook/Web API），AI 一次生成通过率显著高于 uni-app；uni-app 自有 API（uni.request）、条件编译 #ifdef、vue2/vue3 双引擎、小程序专属 API 混淆是 AI 高频错误源
  4. 小程序定位：延后（微信接入开放后），届时用 **Taro**（React 语法多端框架）接续，当前 React H5 组件/逻辑大部分可复用
  5. PC 端：老师教研端（建班级/传资料/看板/RAG），与管理端（后台）区别对待
- 相关需求：V3 四（二）技术选型；《产品战略与定位 V1》§五/§九
- 关联 ADR：ADR-001（被部分取代——其"H5+小程序统一 uni-app"决策不再适用，其余保留）

## 选项

### 选项 A：全端 Vue3 + Uni-app（ADR-001 原方案）

- 描述：H5 + 小程序一套代码
- 优点：多端覆盖
- 缺点：H5 先行阶段多端价值为零；uni-app AI 生成错误率高（条件编译/双引擎/API 幻觉），拖慢 AI 协作迭代

### 选项 B：React + shadcn/ui 统一（选定）

- 描述：H5 + PC 统一 React + shadcn/ui；小程序延后 Taro 接续；管理端后续需要时用 Blazor Wasm 简化
- 优点：AI 友好度最高（React 语料充足、shadcn 模式标准）；H5 开发效率高；PC 与 H5 共享组件/逻辑；未来小程序 Taro（React 语法）无缝接续
- 缺点：多端需 Taro 编译（非一套代码直出）；管理端若用 Blazor 需额外技术栈（但仅限后台内部使用）

## 决策

选定：选项 B

| 端 | 选型 | 阶段 |
|----|------|------|
| 后端 | ASP.NET Core (**.NET 10**) + PostgreSQL + Redis | MVP |
| **H5（学生/家长）** | **React + shadcn/ui + Vite** | MVP（先行） |
| **PC（老师教研）** | **React + shadcn/ui**（复用 H5 组件） | MVP/后续 |
| 小程序（学生/家长） | Taro（React 语法） | 延后（微信接入开放后） |
| 管理端（后台） | Blazor Wasm（可选简化） | 后续需要时评估 |

理由：AI agent 开发模式是核心约束——前端统一 React 使 AI 生成质量与迭代速度最大化；H5 先行阶段 uni-app 的多端价值缺失；Taro 为未来小程序迁移保留低成本路径。

## 后果

- 正面影响：H5 先行最快出 MVP；React + shadcn AI 友好度最优；PC 与 H5 复用组件
- 负面影响 / 风险：H5 → 小程序需经 Taro 编译（非原生一套代码）；管理端 Blazor 引入额外栈（仅后台，接受）
- 后续待办：更新 D04 技术栈表（.NET 10 + React）；D05 前端结构按 React 体系梳理；ADR-001 标记部分被取代

## 关联文档

- 需求文档版本：V3
- 架构文档版本：D01 V1.2.1 / D04 V1.2（待更新）
- 开发方案版本：-