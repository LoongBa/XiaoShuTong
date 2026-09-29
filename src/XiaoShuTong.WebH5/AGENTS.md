# AGENTS.md - 小书童-背书伴侣 WebH5

> 本目录为**纯静态 WebUI 类项目**（React + Vite + TypeScript，无 .NET 代码）。

## 开发工具

- **shadcn/ui 开发可用 shadcn MCP**（项目级 `.opencode/opencode.json` 已启用）——查询/安装组件、校验组件代码时调用 `shadcn` MCP 工具，禁止凭空编造组件 props 与结构。
- **禁用 .NET 类 MCP**：`codemap`、`dotnet-analyzer` 为本目录关闭（纯前端项目无 .NET 代码），后端 .NET 开发请移到仓库根目录。
- UI 与后端接口通过 `@tkwf/tsclient`（Tkwf 门面）调用，mock 数据见 `src/mock/`。

## Dependencies

- **zustand** (^5.0.15) - 状态管理，用于用户状态、任务数据、学习记录等全局状态
- **@tkwf/tsclient** - TKWF 前端 RPC 客户端（Tkwf.configure + Tkwf.User/Guest）
- **@tkwf/tsclient-mock** - mock 数据（MockTransport / MockHttpServer）

## Architecture（摘要）

- 目录结构（src/ 组件/路由/状态/类型详表）与路由清单见 `docs/AGENTS-参考附录.md §1.3`。
- 状态管理：Zustand（用户状态 + 数据状态 + 学习会话状态），persist 中间件持久化。

### 设计系统
- **颜色**：清爽蓝绿主色调，四阶记忆状态色（灰/黄/绿/金）贯穿全端作为状态标识
- **字体**：系统字体 + 楷体（题目区）
- **动画**：star-pop（星星弹出）、float-up（上浮）、pulse-glow（脉冲发光）

## Lessons

- 移动端 H5 设计采用底部 Tab 导航，单列卡片流布局。
- 判题反馈必须有【正确/错误 + 记忆状态变化】的即时视觉反馈。

> 变更记录见 `docs/AGENTS-变更记录.md`（AGENTS 自身变更史的权威位置）。