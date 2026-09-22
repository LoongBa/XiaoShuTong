# AGENTS.md - XiaoShuTong 测试项目

> 本目录为仓库**唯一测试项目**：xunit.v3（net10.0）+ **Tier 1.5（SQLite :memory: 真实内存库 + 真实视图）**领域 Contract 测试。
> 当前基线（AdminWasm-V0.1.8，Tier 1.5 恢复）：**345 通过 / 0 失败 / 2 跳过**，框架 v4.10.21 修复 G7b 后全套件全绿。

## 项目定位

- 全仓库唯一测试项目，覆盖各领域 Service（带 `[GenerateController]` 的业务服务）、DataService 与 Job 类。
- 测试跑**真实 FreeSql + SQLite 内存库**的聚合/视图/投影语义（VEntity 只读通道全链路），不是 Mock 数据层。
- Dll 模式引用框架（`TkwfReferenceMode=Dll`，`TKWFRole=Client` + `TKWFTest=true`）：构建需 `TKWFDeployPath` 环境变量（当前指向 `F:\TKWF_FRAMEWORK_PATH`），缺失时 `Directory.Build.props` 的 `_TKWF_ValidateDeployPath` 提前报错。
- 额外引用 `TKWF.Domain.Testing.Mock`（框架 targets 未自动注入 Mock 程序集），供 `MockDbEntityDAC.Clear()` 隔离等场景使用。

## 项目结构

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

UC 目录内测试类命名：`{ServiceName}Tests`（如 `CreatePkMatchServiceTests`）、Job 为 `{JobName}JobTests`（如 `PkForfeitDetectionJobTests`）。

## Tier 1.5 契约（关键）

- **Fixture 配置**：`XiaoShuTongDomainTestFixture.InitializeAsync` 官方单点用法 `options.UseFreeSqlEntityDAC(FreeSql.DataType.Sqlite, "Data Source=:memory:")`，一行即真实内存库 + 真实视图（`PkPlayerStatsView.ViewSqlSQLite` 方言聚合语义），禁止自定义兜底或手写 DAC。整体经 `services.ConfigTestDomainAsync<XiaoShuTongUserInfo, XiaoShuTongDomainInitializer, DomainWebOptions>(options)` 引导，覆盖 5 个阻塞点：ILogger → AddLogging、会话管理 → TestSessionManager、DataService 注册 → DomainHost.Initialize（内置 throw-factory）、IEntityDAC → FreeSqlEntityDAC、种子/自举 → ServiceProviderBuiltCallbackAsync。
- **集合串行**：`[CollectionDefinition("XiaoShuTongDomain", DisableParallelization = true)]`（ADR66 契约）。Tier 1.5 单 host 共享同一内存库，Reset 期间共享库状态变更，并行测试会互相干扰，不得改动此开关。
- **每 Fact 前隔离**：`XiaoShuTongTestBase.OnInitializeAsync`（override 基类虚方法，非遮蔽 IAsyncLifetime）调 `Fixture.Host.ResetTier15SqliteMemoryDbAsync()`——原地清空（DELETE 数据 + DROP/重建视图），实例/引用保持有效（G6b 修复后），每个 `[Fact]` 从空库开始。G6 实证：跨 Fact 状态残留会造成读聚合错乱/主键冲突/数据污染。
- **继承链**：测试类必须继承 `XiaoShuTongTestBase`（其继承 `DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>`），并标注 `[Collection("XiaoShuTongDomain")]` 匹配 Fixture 的 CollectionDefinition；不得直接继承 `DomainXunitTestBase`，保证 Tier 1.5 状态隔离契约一致。
- **DataService 不能构造函数注入**（throw-factory），统一在方法体内 `this.User.Use<T>()` 获取（V4.9+ 统一 API，具体类自动走 NoAop）。
- **生命周期**：Fixture 构造函数 → Fixture.InitializeAsync → 测试类构造函数 → 测试类.InitializeAsync（创建会话 scope + User）→ 每个测试方法 → 测试类.DisposeAsync（销毁会话）。

## UId 补种纪律

- 实体未实现 `IEntityTracked` 时框架**不自动生成 UId**，测试种子必须显式 `UidGenerator.NewId()` 赋值（审核报告 §五：18 处实体初始化器 + 5 处 using，风格与 Pk 域既有种子一致）。
- 缺失 UId 在 MockDb 下被无唯一约束掩盖，SQLite 迁移暴露——V0.1.8 首跑 21 个 UId 约束失败即此（UId 修复后清零）。

## 生成物禁区

- ❌ 禁止读任何生成源码：`*.g.cs` / `*.biz.cs` / `DataServices\*.cs` / `Entities\*.cs`。
- ❌ 禁止绕过 Skill 手写生成物（Controller / DI 注册 / .g.cs 成员 / gql 字符串）。
- ✅ Service/DataService 方法签名以活态文档 `.TKWF/{Domain}/Domain_Api.md` 与 `.TKWF/{Domain}/DataService_API.md` 为准。

## 新增测试

- 必须走 **`tkwf-test` skill**：领域 Contract 测试，InMemory DAC 免 Mock，测试类继承 `DomainXunitTestBase`（本目录落地为 `XiaoShuTongTestBase`）。
- 必传参数：`{Domain}=业务域名`、`{UC}=用况目录名`、`{ServiceName}=Service 类名`。
- 测试类加 `[Trait("Category", "Contract")]` 供 CI 过滤；写完对照 `.TKWF/{Domain}/Business.md` BR 表输出 BR 实现检查清单。

## 验证命令

- 单跑测试：`dotnet test tests/XiaoShuTong.Tests/`（Dll 模式需先确认 `TKWFDeployPath` 已设置）。
- 一键复验：`tests/verify-g7b.ps1`（特征检测 + 全套件，审核报告 §六 使用）。
- CI 过滤：`dotnet test --filter "Category=Contract"`。
- 框架同步（版本升级后）：`pwsh %TKWFDeployPath%\build\build-deployment.ps1 -Destination F:\TKWF_FRAMEWORK_PATH -LinkRefs`。

## 已知框架缺口（已闭环参考）

- **G7b**：`DateTimeUtcHandler` 曾未覆盖 `DateTime?`（nullable），导致 nullable 字段往返 +8h（Kind=Unspecified）；已由框架 v4.10.19/21 修复（`SqliteTypeHandlerRegistrar` 追加 `DateTimeNullableUtcHandler`，`PropertyType == typeHandler.Type` 精确匹配触发建 TEXT 列），基线 7 用例清零。新测试涉及 `DateTime?` 断言时注意 UTC Kind 往返。

## 变更记录

- 2026-09-23：初始创建。基线 AdminWasm-V0.1.8（Tier 1.5 恢复，G7b 由框架 v4.10.21 修复），345 通过 / 0 失败 / 2 跳过。
