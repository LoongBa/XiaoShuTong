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

## Architecture

### 项目结构
```
src/
├── components/          # 通用组件
│   ├── MemoryStateBadge.tsx   # 记忆状态徽章（✕灰/△黄/○绿/★金）
│   ├── TaskProgressBar.tsx    # 任务进度条
│   ├── StreakBadge.tsx        # 连续打卡徽章
│   ├── Heatmap.tsx            # 学习热力图
│   ├── EmptyState.tsx         # 空状态组件
│   ├── BottomNav.tsx          # 底部导航
│   ├── ShareReportModal.tsx   # 分享战报弹窗（V2新增）
│   └── StudyBuddySection.tsx  # 学习搭子组件（V2新增）
├── routes/              # 页面路由
│   ├── auth/            # 认证相关
│   │   ├── login.tsx    # 微信登录页
│   │   └── privacy.tsx  # 隐私协议页
│   ├── student/         # 学生端
│   │   ├── home.tsx     # 首页（任务中心）
│   │   ├── study/
│   │   │   ├── task.tsx   # 任务执行页（背诵答题）
│   │   │   ├── result.tsx # 会话结果页
│   │   │   └── review.tsx # 复习队列页
│   │   ├── bank/
│   │   │   └── self.tsx   # 自由背诵入口
│   │   ├── stats/
│   │   │   ├── heatmap.tsx # 热力图统计（V2: 分享战报）
│   │   │   └── wrong.tsx   # 错题本
│   │   ├── rank.tsx     # 排行榜（V2新增: 学习搭子、趋势箭头）
│   │   └── user/
│   │       └── profile.tsx # 个人中心
│   ├── teacher/         # 老师端
│   │   ├── dashboard.tsx    # 班级看板
│   │   └── tasks/
│   │       ├── create.tsx   # 任务创建
│   │       └── detail.tsx   # 任务详情
│   └── parent/          # 家长端
│       └── dashboard.tsx    # 成长报告
├── store/
│   └── appStore.ts      # Zustand状态管理
└── types/
    └── index.ts         # TypeScript类型定义
```

### 状态管理
使用Zustand进行全局状态管理，包含：
- 用户状态（currentUser, isLoggedIn, hasAgreedPrivacy）
- 数据状态（tasks, reviewItems, subjects, knowledgePoints, wrongAnswers, learningStats）
- 学习会话状态（currentTaskId, currentQuestionIndex, answers）

### 设计系统
- **颜色**：清爽蓝绿主色调，四阶记忆状态色（灰/黄/绿/金）
- **字体**：系统字体 + 楷体（题目区）
- **动画**：star-pop（星星弹出）、float-up（上浮）、pulse-glow（脉冲发光）

### 路由结构
- `/auth/login` - 登录页
- `/auth/privacy` - 隐私协议
- `/student/home` - 学生首页
- `/student/study/task` - 任务执行
- `/student/study/result` - 会话结果
- `/student/study/review` - 复习队列
- `/student/bank/self` - 自由背诵
- `/student/stats/heatmap` - 热力图
- `/student/stats/wrong` - 错题本
- `/student/user/profile` - 个人中心
- `/teacher/dashboard` - 老师看板
- `/teacher/tasks/create` - 创建任务
- `/teacher/tasks/detail` - 任务详情
- `/parent/dashboard` - 家长报告

## Lessons

- 使用Zustand的persist中间件实现数据持久化
- 移动端H5设计采用底部Tab导航，单列卡片流布局
- 记忆状态色（✕灰/△黄/○绿/★金）贯穿全端作为状态标识
- 判题反馈必须有【正确/错误 + 记忆状态变化】的即时视觉反馈
