# HANDOVER · 题库判题域切片交接文档

> **定位**：设计阶段→实施阶段的承上启下文档（切片验证版）。Agent 阅读本文档后完成自引导加载后续动作——AC-Kit。
> **项目**：小书童（XiaoShuTong）· 题库判题域切片
> **范围**：模块 3（题库管理）+ 模块 5（AI 导师判题），切片序号 03（前序：切片 01 群组管理域、切片 02 学习 Session 域）
> **更新日期**：2026-09-06

---

## 文档流总览

```
R01(需求)→ S01(场景)→ DS01(数据结构)→ U01(用况与契约)

DS01 ──→ Entity.cs（手写）     参考：{AC-Kit}/templates/Entity.cs.txt
   └──→ xCodeGen → DataService（自动生成，MSBuild AfterBuild 触发）

U01 ──→ Service + [GenerateController]（手写） ← SG 自动生成接口，不手写
   └──→ 服务契约一览（并入实施总览） + 本 HANDOVER
```

> ⚠️ **必须执行顺序**：`DS01 → Entity → xCodeGen → DataService/DTO → U01 → Service`
> - 缺少 xCodeGen → DataService/DTO/Conditions 缺失 → Service 无法编译
> - xCodeGen 失败时上报 Sisyphus，不要自行绕过（手写 DataService 与 SG 生成的 Controller 不兼容）

---

## §一 输入文档清单

- **R-series**：`docs/草稿/切片验证-题库判题域/R01-需求与功能设计-题库与判题.md`
- **S-series**：`docs/草稿/切片验证-题库判题域/S01-场景描述-题库与判题.md`
- **DS-series**：`docs/草稿/切片验证-题库判题域/DS01-数据结构设计-题库与判题.md`
- **U-series**：`docs/草稿/切片验证-题库判题域/U01-用况与契约-题库与判题.md`
- **实施总览**：`docs/草稿/切片验证-题库判题域/题库判题域-切片实施总览.md`

---

## §二 设计规格清单

### UC 跟踪表（Agent 逐项填写）

| UC# | UC 名称 | 子域 | § Entity | § Service | § 测试 | § Schema | § 验证 | 备注 |
|:---:|---------|:----:|:--------:|:---------:|:------:|:--------:|:------:|------|
| B.1 | 题库列表 | Bank | ☐ | ☐ | ☐ | ☐ | ☐ | |
| B.2 | 题库详情 | Bank | ☐ | ☐ | ☐ | ☐ | ☐ | |
| B.3 | 创建题库 | Bank | ☐ | ☐ | ☐ | ☐ | ☐ | |
| B.4 | 导入题目 | Bank | ☐ | ☐ | ☐ | ☐ | ☐ | CROSS + 文件写入 |
| B.5 | 防爬取题 | Bank | ☐ | ☐ | ☐ | ☐ | ☐ | Callee |
| B.6 | 知识卡片 | Bank | ☐ | ☐ | ☐ | ☐ | ☐ | |
| B.7 | AI 预处理 | Bank | ☐ | ☐ | ☐ | ☐ | ☐ | BackgroundJob |
| B.8 | 背诵点校验入库 | Bank | ☐ | ☐ | ☐ | ☐ | ☐ | CROSS |
| J.1 | 判题引擎 | Judging | ☐ | ☐ | ☐ | ☐ | ☐ | Callee（被学习域消费） |
| J.2 | 判错反馈 | Judging | ☐ | ☐ | ☐ | ☐ | ☐ | |

### Entity → DS 映射表

| Entity | 子域 | 关联 DS 文档 | 文件路径 |
|--------|:----:|:------------:|---------|
| Banks | Bank | DS01 | `Entities/Bank/Banks.cs` |
| Questions | Bank | DS01 | `Entities/Bank/Questions.cs` |
| JudgmentFeedback | Judging | DS01 | `Entities/Judging/JudgmentFeedback.cs` |

> **配置源（非 Entity）**：题型注册表 / 学科注册表 / Prompt 资源为 JSON 配置，由服务层读取（IContentFileStore / 配置加载），不建 Entity。

### Service 跟踪表

| Service 类 | 子域 | 关联 UC | 备注 |
|-----------|:----:|:-------:|------|
| ListBanksService | Bank | B.1 | [GenerateController] |
| GetBankDetailService | Bank | B.2 | [GenerateController] |
| CreateBankService | Bank | B.3 | [GenerateController] |
| ImportQuestionsService | Bank | B.4 | [GenerateController] + [Transactional] |
| GetNextQuestionService | Bank | B.5 | Callee（被学习域调用） |
| GetKnowledgeCardService | Bank | B.6 | [GenerateController] |
| BankContentPreprocessJob | Bank | B.7 | BackgroundJob，无 Controller |
| ReviewBackingPointsService | Bank | B.8 | [GenerateController] + [Transactional] |
| JudgingEngineService | Judging | J.1 | Callee（被学习域 SubmitAttemptService 消费） |
| SubmitJudgmentFeedbackService | Judging | J.2 | [GenerateController] |

### 测试跟踪表

| 测试类 | 关联 Service | 重点 |
|-------|-------------|------|
| ListBanksServiceTests | ListBanksService | 可见性过滤/空列表/学科分页 |
| ImportQuestionsServiceTests | ImportQuestionsService | 解析/关键词提取/文件+索引原子/CROSS 回滚 |
| GetNextQuestionServiceTests | GetNextQuestionService | 防爬不含答案/状态机排序/题集耗尽 |
| ReviewBackingPointsServiceTests | ReviewBackingPointsService | 未校验拦截/AI 草稿不入库/原子 |
| JudgingEngineServiceTests | JudgingEngineService | **D07 测试矩阵 14 例**（O1-O5）/required 强制 partial/LLM 降级/阈值边界 |
| SubmitJudgmentFeedbackServiceTests | SubmitJudgmentFeedbackService | 幂等/作答存在校验/CROSS 回滚 |

---

## §三 服务契约一览

> 完整表格见 `题库判题域-切片实施总览.md`（10 个 Service，BR-01~39，CROSS 事务 3 处：B.4/B.7/B.8，跨模块依赖 5 项：账户/群组/学习/任务/判题引擎互通）

---

## §四 项目约定

| 项 | 值 |
|----|-----|
| 项目名 | XiaoShuTong |
| 切片范围 | 题库管理域（模块 3）+ AI 判题域（模块 5）；切片 03（前序：01 群组管理、02 学习 Session） |
| 子域 | `Bank`（题库）+ `Judging`（判题） |
| 用户类型 | 群主(owner)/学生(student)/家长(parent)；题库 UC 群主、取题/判题学生、反馈学生 |
| 开发路线 | WebApi（切片验证，全量待定） |
| 测试模式 | 内存 DAC（切片验证） |
| 审计字段命名 | `CreateTime`/`UpdateTime`（存量 CreatedAt/UpdatedAt → 改名） |
| 枚举 ORM 映射 | `[Column(MapType = typeof(string))]`，PascalCase 存字符串（存量小写 → 迁移映射） |
| 错误码 | 数字域码 15xx（题库）/ 90xx（判题反馈）/ 10xx（全局）+ SNAKE_CASE 双列（迁移决策） |
| 主键决策 | 系统内部 `long Id`；外部关联/跨系统 `Uid`（uuid 业务键），实体表补 Uid 列 |
| 内容权威 | Banks/Questions DB 为查询索引；内容权威 = JsonPath 文件；答案永不下发客户端（防爬） |
| 框架路径 | `$env:TKWF_FRAMEWORK_PATH` |

---

## §五 编码启动序列（切片验证）

| 步骤 | 动作 | Skill / 工具 | 产出 | 完成标志 |
|:----:|------|-------------|------|---------|
| 1 | 读 DS01 -> 写 3 个 Entity.cs | tkwf-entity | `Entities/Bank/*.cs` + `Entities/Judging/*.cs` | 文件存在 |
| 2 | `dotnet build` | - | xCodeGen 生成 DataService/DTO/Conditions | `.g.cs` 存在 |
| 3 | 读 U01 -> 写 10 个 Service.cs | tkwf-service | `Services/Bank/*.cs` + `Services/Judging/*.cs` | 文件存在 |
| 4 | 读 U01 BR -> 写测试 | tkwf-test | `*ServiceTests.cs` | 文件存在 |
| 5 | `dotnet build` | - | 全项目编译 | 0 error |
| 6 | `dotnet test` | - | 测试执行 | 全部通过 |

> ⚠️ **存量迁移前置确认（路径 B 转化衔接）**：DS01「存量差异标注」①主键（uuid → long Id + Uid）已按前序切片决策确定；⑤内容权威=JSON 文件（DB 仅索引）为**本切片特殊约束**，实施前需确认内容文件存储接入方式（IContentFileStore 抽象）；⑥D03 CRUD 缺口 [Proposed] 需业务确认后回改 D03。

---

## 附录：8 项交接条件自查（DG-01 §2.4）

| # | 条件 | 验证方式 | 结果 |
|:-:|------|---------|:----:|
| 1 | U-series 所有 Use-Case 的契约签名已定义（含 DTO 字段类型） | 审查 | ✅ 10 UC 契约签名 + Req/Res DTO 完整 |
| 2 | 所有 BR-xx 规则已列全，无「待定」「后续补充」 | 审查 | ✅ BR-01~39 全覆盖 |
| 3 | 所有 Use-Case 引用的 DataService 已在 DS 中定义 | 交叉核对 | ✅ 3 实体均有对应 DataService（Banks/Questions/JudgmentFeedback）；跨模块引用已标注 |
| 4 | 所有跨模块依赖已标注 | 交叉核对 | ✅ 账户/群组/学习/任务 4 项 + 判题引擎互通（UC 头/编排逻辑/实施总览三处一致） |
| 5 | 所有 Alternative Flow 至少有一条对应的 BR-xx | 审查 | ✅ 每条替代流均有 BR 对应 |
| 6 | 所有 R-series 约束条件的交叉引用均指向已存在功能点 | 交叉核对 | ✅ R01 约束均在 U01 有 BR |
| 7 | 交叉模块影响扫描已完成 | 审查 | ✅ R01「交叉模块影响扫描」覆盖模块 2/4/6/9/10；受影响的模块 R-doc（学习域）已在前序切片接收 |
| 8 | 所有 DS 实体的子域标注与 U 文档 UC 头子域一致 | 交叉核对 | ✅ `Bank`/`Judging` 与 UC 头一致 |

**⚠️ 补充说明**：条件 1~8 在"设计文档"层面全部满足。但存在 **3 类非阻断缺口**需实施前确认（见下节）：D03 CRUD 缺口 [Proposed] 待业务确认回改、内容文件存储抽象接入方式、判题供应商（LLM）配置接入。

---

## 已决策项（本切片新增）

| # | 决策 | 说明 |
|:-:|------|------|
| 1 | 子域划分 Bank/Judging | 题库与判题各自内聚，命名空间独立 |
| 2 | 内容权威=JSON 文件（DB 索引） | 对齐 D01 §7.6；防爬 DRM 核心，答案永不下发 |
| 3 | AI 预处理纳入切切片（UI 阶段二） | PRD 明示"群主上传 AI 预处理已纳入 MVP 生产链路"，业务能力先行 |
| 4 | 判错反馈域（90xx）纳入切片（D03 排期 A7） | 业务需求优先级高于 D03 落地顺序约束 |
| 5 | 判题引擎 = Callee 内部服务 | 被学习域 SubmitAttemptService 消费，不暴露独立 Controller（除判错反馈） |

## 待确认项清单

| # | 项 | 影响 | 建议 |
|:-:|---|------|------|
| 1 | **D03 题库 CRUD 缺口**：题库更新/删除、单题录入/修改/删除、分类标签、批量粘贴、上传接口均为 [Proposed] | 接口契约 | 业务确认后回改 D03（或建立 U 层为契约来源的决策） |
| 2 | **IContentFileStore 抽象**：内容权威 JSON 文件写入/读取的存储接入（本地/OSS） | 实施 | 对齐 D04 OSS 生命周期约定，实施阶段确认 |
| 3 | **判题 LLM 供应商配置**：OpenAI 兼容接口（火山引擎/DeepSeek/智谱） | 实施 | 环境配置 + Prompt 资源加载，实施阶段确认 |
| 4 | **题型注册表加载方式**：配置 JSON（题库/schema/题型注册表.json）加载与热更新 | 实施 | 服务层读取 + 缓存 TTL，实施阶段确认 |

---

> *文档版本：第一阶段 v1.0*
> *编制日期：2026-09-06*
> *同步版本：R01 v1.0 / S01 v1.0 / DS01 v1.0 / U01 v1.0*