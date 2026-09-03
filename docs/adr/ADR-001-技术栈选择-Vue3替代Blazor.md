---
title: ADR-001 技术栈选择：Vue3 + Uni-app 替代 Blazor WASM
status: 活跃
date: 2026-09-04
superseded_by:
---

# ADR-001 技术栈选择：Vue3 + Uni-app 替代 Blazor WASM

## 状态

活跃

## 上下文

- 问题描述：V2 需求草稿原定前端 Blazor WebAssembly + 后端 ASP.NET Core + Blazor Server，经 22 份外部 AI 评估（需求评估 10 份 + 综合评估 7 份有效）评审，9/10 模型担忧 Blazor WASM 在微信 H5 环境（X5 内核）的加载性能（首屏需下载 .NET runtime 2MB+，弱网 FCP 可能 >5s），且原方案存在"Blazor Server 当后端框架"的概念混淆。
- 约束条件：产品形态为微信 H5（首期）+ 小程序（迁移预留）；团队技术栈需统一；首屏 ≤5s 非功能指标（V3）。
- 相关需求：V3 四（二）技术选型
- 关联 ADR：无

## 选项

### 选项 A：保留 Blazor WASM + AOT 优化

- 描述：AOT 编译 + Brotli 压缩 + CDN + 骨架屏缓解首屏问题
- 优点：贴合团队既有 Blazor 技术栈（评估中仅豆包 1/10 支持）
- 缺点：微信 X5 内核 WASM 支持不稳定；9/10 模型不看好；首屏性能风险高

### 选项 B：Blazor Server + Web API

- 描述：服务端渲染，首屏极快，无运行时下载
- 优点：首屏体验好
- 缺点：仍绑死 Blazor 生态；小程序迁移成本高（Blazor 无小程序方案）

### 选项 C：Vue3 + Uni-app（选定）

- 描述：Vue3 组合式 API + Uni-app 跨端框架，H5 为主，小程序一键迁移
- 优点：微信兼容性好；Uni-app 一套代码多端（H5/小程序/App）；社区资源丰富；生态成熟
- 缺点：需团队从 Blazor 切换 Vue（学习成本）；放弃现有 Blazor 代码积累

## 决策

选定：选项 C（Vue3 + Uni-app）

理由：产品核心场景是微信私域传播（H5 即点即用），微信 X5 内核兼容性是第一优先级；Uni-app 为 V3 备忘录中的小程序迁移预留零成本路径（D01 技术栈决策）；9/10 外部模型共识支持该方向。

## 后果

- 正面影响：微信 H5 兼容性最优；小程序迁移成本低（前端已 Uni-app）；生态丰富易招人
- 负面影响 / 风险：团队需学习 Vue3；已有 Blazor 代码（若有）作废
- 后续待办：后端保持 ASP.NET Core Web API（D04 技术栈）；前端骨架 D4-P1 用 Uni-app 搭建

## 关联文档

- 需求文档版本：V3
- 架构文档版本：D01 V1.2.1
- 开发方案版本：-
