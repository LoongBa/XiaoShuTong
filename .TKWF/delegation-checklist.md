# Delegation-Checklist

> 委托 subagent 编写代码前，Sisyphus 逐条检查。违反任意一条不得发出委托。

## §0 执行器前置检查（委托前）

| # | 检查项 | 通过标准 |
|:-:|--------|---------|
| 1 | 脚本路径与项目架构匹配 | 如 `buildSchema.ps1` 的 FrontendDir 未硬编码为不存在的项目（如 Wasm 项目不跑 npm codegen） |
| 2 | npm 配置的目录与脚本运行目录一致 | `Ensure-NpmScript` 等自愈函数的目标目录存在 |
| 3 | 项目约定参数已对齐 | 项目名、命名空间等与 §4 项目约定一致（V4.9.80 起用户类型由宿主初始化器推断，不再作为项目约定参数） |
| 4 | 前置脚本/工具已执行完毕 | 如 §一 脚本、npm install、xCodeGen 生成已完成 |

## §1 读取范围检查

| # | 检查项 | 通过标准 |
|:-:|--------|---------|
| 1 | 所有 `Read` 指令都在 Prompt-*.md 的"必读"表中 | 不在必读表中的文件 → 删掉，改为注入 |
| 2 | 没有"参考已有实现"、"保持风格一致"类的泛读指令 | 风格由模板保证，不需要读源码 |
| 3 | 注入的信息已经替代了需要读的文档 | 如 DS 文档字段已提取到 prompt 中，subagent 不需要再读 DS 文档 |
| 4 | MUST NOT DO 中明确禁止了越界读取 | 逐条列出：不读 *.g.cs、不读 DataServices/*.cs、不读 Entities/*.cs |

## §2 注入完整性检查

| # | 检查项 | 通过标准 |
|:-:|--------|---------|
| 1 | 所有 BR 规则已注入到 prompt 中 | 不需要 subagent 读 U 文档 |
| 2 | 所有 DTO 结构已注入 | 不需要 subagent 读 DS/U 文档 |
| 3 | 所有 DataService 依赖已列出 | 不需要 subagent 读 DataService_API.md（除非是通用查询模式） |
| 4 | 项目特定值已替换：{App}、命名空间、路径 | 没有占位符残留（V4.9.80 起 UserType 不再出现在 DomainGenerateCode 中） |

## §3 规范遵守检查

| # | 检查项 | 通过标准 |
|:-:|--------|---------|
| 1 | MUST DO 和 MUST NOT DO 都用了 | 缺一不可 |
| 2 | MUST NOT DO 覆盖了框架红线 | 不修改框架代码、不提交 git、不手写 Controller/DI |
| 3 | MUST NOT DO 覆盖了模板禁止项 | 如 Entity 不写 UId/IsFromPersistentSource、Service 不构造函数注入 DataService |
| 4 | 6 个部分齐全 | TASK / EXPECTED OUTCOME / REQUIRED TOOLS / MUST DO / MUST NOT DO / CONTEXT |

## §4 验证检查

| # | 检查项 | 通过标准 |
|:-:|--------|---------|
| 1 | 预期的产出物路径清晰 | 文件路径、类名、方法签名 |
| 2 | 验收标准可量化 | 0 error / test pass / 特定文件生成 |
| 3 | 超时/失败处理策略已写明 | 测试 subagent 5 分钟超时止损 |

---

> 使用方式：每次写 `task(...)` 前，打开此文件逐条过一遍。
> 任何一条不通过 → 修改 prompt 后再发出。