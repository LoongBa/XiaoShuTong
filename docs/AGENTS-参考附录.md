# AGENTS 参考附录

> 收纳从各级 AGENTS.md 拆出的**参考性细节**（目录结构详表、已闭环缺口参考）。各 AGENTS.md 只保留规则/红线/经验/路由，并指向本文件。
> 本文档**不自动装载**，按需查阅；目录结构变化时同步更新（见 `Agents_Use_TKWF.md` §3 进度同步纪律）。

---

## 1. 子产品目录结构（详表）

### 1.1 领域层 `src/XiaoShuTong.Domain/`

```
src/XiaoShuTong.Domain/
├── Entities/          # 实体（12 子域目录；手写字段+注解，xCodeGen 生成 .g.cs/Dto/DataService/Conditions）
├── DataServices/      # 数据服务（★生成源码，禁止读/写）
├── Services/          # 业务服务（12 子域目录；★手写区，业务逻辑全部在此）
├── Tools/             # 跨域工具（11 个：MemoryStateMachine/KeywordGroup/UidGenerator 等）
├── Enums.cs           # 枚举
└── .xCodeGen/         # 生成配置/缓存（勿手改）
```

**Services 12 子域**：Bank / Buddy / GroupManagement / Judging / Learning / Parent / Pk / Platform / Rank / Shared / Stats / TaskManagement（子域对齐 Entities 目录）。

### 1.2 管理端 `src/XiaoShuTong.AdminWasm/`

**Pages/（18 个页面文件）**：

```
Pages/
├── Home.razor              # 占位欢迎页
├── User/Login/Login.razor  # 登录页
├── Dashboard/Index.razor   # 群主看板
├── Groups/                 # 群组管理（列表/激活建群/名单导入/CSV导出/详情）
├── Tasks/                  # 任务管理（创建/详情/周报）
├── Banks/                  # 题库管理（列表/创建/导入/详情/AI预处理/校验入库）
└── Platform/AiModels.razor # AI 模型配置
```

**页面-路由-Service 映射**（完整清单在 `docs/平台管理系统-迭代计划.md` §〇/§一，此处仅摘关键入口，统一挂 `/owner/*`）：

| 路由 | 页面 | Service（节选） |
|------|------|----------------|
| `/owner/dashboard` | Dashboard/Index | `IGetOwnerDashboardService` |
| `/owner/groups/list` | Groups/GroupList | `IListGroupsService` |
| `/owner/groups/detail/{id}` | Groups/GroupDetail | `IManageGroupMembersService`（F4 成员管理内嵌） |
| `/owner/tasks/create` | Tasks/TaskCreate | `ICreateTaskService` + `IListBanksService` |
| `/owner/banks` | Banks/BankList | `IListBanksService` |
| `/owner/platform/ai-models` | Platform/AiModels | AI 模型配置（超计划页） |

### 1.3 学生/家长端 `src/XiaoShuTong.WebH5/`

**完整目录树**：

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

**状态管理**（Zustand 全局状态）：用户状态（currentUser, isLoggedIn, hasAgreedPrivacy）、数据状态（tasks, reviewItems, subjects, knowledgePoints, wrongAnswers, learningStats）、学习会话状态（currentTaskId, currentQuestionIndex, answers）。

**路由结构**：`/auth/login`、`/auth/privacy`、`/student/home`、`/student/study/{task,result,review}`、`/student/bank/self`、`/student/stats/{heatmap,wrong}`、`/student/user/profile`、`/teacher/dashboard`、`/teacher/tasks/{create,detail}`、`/parent/dashboard`。

**设计系统**：清爽蓝绿主色调，四阶记忆状态色（灰/黄/绿/金）；系统字体 + 楷体（题目区）；动画 star-pop（星星弹出）/ float-up（上浮）/ pulse-glow（脉冲发光）。

### 1.4 测试项目 `tests/XiaoShuTong.Tests/`

```
tests/XiaoShuTong.Tests/
├── XiaoShuTongTestBase.cs            # Tier 1.5 中间基类（每 Fact 前 Reset，见契约节）
├── XiaoShuTongDomainTestFixture.cs   # 集合 Fixture + CollectionDefinition（串行契约）
├── XiaoShuTongServiceTests.cs        # 测试类模板（2 个 Skip 示例 = 基线 2 跳过）
├── XiaoShuTong.Tests.csproj
├── appsettings.Test.json             # DomainOptions 连接串（脚本创建时替换 {ConnectionString}）
├── Bank/  Buddy/  GroupManagement/  Judging/  Learning/
├── Parent/  Pk/  Platform/  Rank/  Stats/  TaskManagement/
└── bin/  obj/                        # 构建产物，禁止改动
```

**UC 目录内测试类命名**：`{ServiceName}Tests`（如 `CreatePkMatchServiceTests`）、Job 为 `{JobName}JobTests`（如 `PkForfeitDetectionJobTests`）。

---

## 2. 已闭环框架缺口（历史参考）

> 以下问题已由框架侧修复并验证，仅留档供新代码编写时参考。

### G7b：DateTime? 往返 UTC Kind（框架 v4.10.19/21 已修复）

`DateTimeUtcHandler` 曾未覆盖 `DateTime?`（nullable），导致 nullable 字段往返 +8h（Kind=Unspecified）；框架修复后 `SqliteTypeHandlerRegistrar` 追加 `DateTimeNullableUtcHandler`（`PropertyType == typeHandler.Type` 精确匹配触发建 TEXT 列），基线 7 用例清零。**新测试涉及 `DateTime?` 断言时注意 UTC Kind 往返**。