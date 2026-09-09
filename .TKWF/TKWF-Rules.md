# TKWF 增量开发路由规则

> **定位**: 解决方案级路由中枢（`{AC-Kit}/TKWF_Rules.md` 模板的实例）。
> **维护**: 手动维护，规则稳定；活态文档由 xCodeGen / buildSchema 自动产出（P2）。
> **消费模式**: 本文件在项目初始化时由 `create-new-solution.ps1` 复制到 `.TKWF/TKWF-Rules.md`，按项目实际替换 `XiaoShuTong` 与域清单。
> **职责边界**: 本路由**只服务增量开发**（项目已初始化后的日常变更）。全量开发/初始化阶段由 AC-Kit 主脚本执行（`{AC-Kit}/scripts/create-new-solution.ps1`），见 AC-Kit README 职责分层。

---

## 一、消费模式（二选一，由 Directory.Build.props 决定）

| 模式 | 机制 | 适用场景 |
|------|------|---------|
| **Dll 模式**（默认） | `TkwfReferenceMode=Dll`，引用 `$(TKWFDeployPath)build\refs\` 已编译 DLL | 脱离 TKWF 源码，纯 DLL 编译，验证发布一致性；离线可用 |
| **NuGet 模式** | `PackageReference` 引用 `TKWF.*` 已发布包（TKWF 自动发布至 nuget.org） | 标准分发，随包版本演进 |

> 切换方式：`Directory.Build.props` 中设 `<TkwfReferenceMode>Dll</TkwfReferenceMode>`（或 NuGet 模式改用 PackageReference）。
> 运行资源（dll / xCodeGen.Cli.exe / 模板）**原地引用部署包**，不复制到解决方案（决策 D7）。

---

## 二、需求分层分解（步骤 1：判定变更层级）

| 变更信号 | 层级 | 涉及活态文档 |
|---------|------|-------------|
| 新增/改实体字段、新增实体 | 领域层 | `.TKWF/{域}/DOMAIN_MAP.md` |
| 新增/改搜索条件、数据服务 | 领域层 | `.TKWF/{域}/DataService_API.md` |
| **新增/改业务规则** | **领域层** | **`.TKWF/{域}/Business.md`**（先执行 `tkwf-business` 物化/增量更新，再进入 Service 实现） |
| **首次编写 Service 前** | **领域层** | **检查 `.TKWF/{域}/Business.md` 是否已物化**（骨架占位符 → 先执行 `tkwf-business`；未物化直接写 Service 会导致 BR 缺失 → 质量下降） |
| 新增对外 API、改签名 | 接入层 | `.TKWF/{域}/schema.graphql` + `Domain_Api.md` |
| 前端消费新 API | 表现层 | `{前端}/src/gql/domain-client.g.ts` |
| 跨层（实体→页面） | 全链路 | 按上述顺序逐层处理 |

> `{域}` = 每个业务域一个子目录（决策 D5），如 `merchant` / `platform`。

> ⚠️ **Business.md 质量是 Service 质量的前置**：BR（业务规则）是 Service 业务验证逻辑的唯一权威来源。
> - Business.md 未物化/空骨架 → Service 跳过业务验证或臆造规则 → **质量下降**
> - Business.md 落后于现实（BR 缺失、过时）→ Service 按错误规则实现 → **返工成本 + 隐藏缺陷**
> - 因此「新增/改业务规则」与「首次编写 Service」两条路径都**先检查 Business.md**，未物化/落后时先执行 `tkwf-business`（见 §三）。

---

## 三、Skill 路由表（步骤 2：加载）

| 任务 | Skill | 关键参数 |
|------|-------|---------|
| 物化/维护业务规则（BR） | `tkwf-business` | `{Domain}=<域>` |
| 查询/统计梳理（VEntity 设计） | `tkwf-ventity-design` | `{Domain}=<域>` `{切片}=<切片名>`（触发门槛：含跨表聚合/报表/跨切片消费；UI 定稿后、DS 场景层前执行） |
| 新增实体 | `tkwf-entity` | `{Domain}=<域>` `{Entity}=<实体名>` |
| 数据服务/条件 | `tkwf-service` | `{Domain}=<域>` `{Entity}=...` |
| 领域测试 | `tkwf-test` | `{Domain}=<域>` |
| 表现层 UI | `tkwf-ui`（暂缓） | — |

> **执行顺序**：凡涉及业务规则的 Service 编写，**先 `tkwf-business` 物化/更新 Business.md（确保 BR 就绪），再 `tkwf-service` 实现**。`tkwf-service` Step 0 会检查 Business.md，未物化时提示先执行 `tkwf-business`。

> Skill 在项目初始化时由 `create-new-solution.ps1` 从 AC-Kit `skills/` 复制到 `.agents/skills/`（3g 步骤，复数，OpenCode/Claude 规范路径），按项目替换 `{App}`（有 `$env:TKWFDeployPath` 时同时替换 `{AC-Kit}`）。增量开发从 `.agents/skills/` 加载，**不依赖 AC-Kit**。

---

## 四、文档路径速查

```
XiaoShuTong\
├── Agents_Use_TKWF.md           ← OpenCode 自动加载（进 git，opencode.json 配置）
├── .agents\skills\               ← tkwf-entity / tkwf-service / tkwf-test（进 git，3g 复制自 AC-Kit skills/）
├── .TKWF\
│   ├── TKWF-Rules.md            ← 本文件（进 git）
│   ├── xCodeGen\                ← 生成配置（进 git）
│   │   ├── merchant.xCodeGen.json / platform.xCodeGen.json
│   │   （模板单源在部署包，经 %TKWFDeployPath% 引用）
│   ├── {域}\                    ← 域活态文档（gitignore，xCodeGen 生成）
│   │   ├── DOMAIN_MAP.md  DataService_API.md  Domain_Api.md
│   │   ├── Business.md   LOG.md   AGENTS.md
│   │   └── schema.graphql（接入层导出）
├── build.ps1 / buildSchema.ps1 / build-quick.cmd   ← 执行工具（不进 .TKWF）
└── {前端}\src\gql\domain-client.g.ts               ← 前端编译依赖
```

---

## 五、顶层红线（违反即失败）

1. 业务逻辑锁定领域层，表现层零业务代码
2. 不手写 `.g.cs` / DataService / Conditions / Controller 接口 / DI 注册（由 xCodeGen/SG 生成）
3. 数据访问必须经 `DomainUser.Use<T>()`，写入必须处于 TransactionScope
4. 禁止读 `*.g.cs` / `*.biz.cs` / `DataServices\*.cs` 生成源码——读活态文档（P3）
5. 生成物（`.TKWF/{域}/`、schema.graphql、`src/gql/`）不进 git；规则/配置（Agents_Use_TKWF.md、TKWF-Rules.md、`.TKWF/xCodeGen/`）进 git

---

## 六、刷新活态文档

- 领域变更后：`dotnet build`（AfterBuild 自动跑 xCodeGen）
- API 变更后：`.\buildSchema.ps1`（导出 schema + codegen）
- 参考速查：`{AC-Kit}/references/`（领域开发速查、Dto 裁剪速查、VEntity 速查、测试最佳实践）
