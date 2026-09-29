# AGENTS 变更记录

> 各级 **AGENTS.md / Agents_Use_TKWF.md 文件自身**的变更历史（规范/结构/状态口径调整）。产品与内容变更记录见 `docs/变更记录.md`，二者分离。
> 新增/修订任一 AGENTS.md 时，**追加到本清单末尾**（日期 + 变更内容），不覆盖历史。

---

## 2026-09-29

- **Agents_Use_TKWF.md**：AGENTS 瘦身重构——§6.2「建议使用的 Skill」表并入 §7 强制 Skill 路由（追加 tkwf-tsclient 行）；§3 进度同步纪律表更新（docs/AGENTS.md 拆状态快照、子产品 AGENTS.md 变更改记本文件）。
- **docs/AGENTS.md**：「题库素材与版本状态」节由内嵌快照改为路由引用；「核心规则（简要）」精简为引用 `Agents_Use_TKWF.md`。
- **src/XiaoShuTong.Domain/AGENTS.md**：删除与根重复的 Skill 路由表/生成物禁区顺引；目录树移 `docs/AGENTS-参考附录.md`；变更记录移至本文件。
- **src/XiaoShuTong.AdminWasm/AGENTS.md**：Pages 树与路由-Service 映射移附录；变更记录移至本文件。
- **src/XiaoShuTong.WebH5/AGENTS.md**：src 目录树/状态管理/路由结构移附录；变更记录移至本文件。
- **tests/XiaoShuTong.Tests/AGENTS.md**：项目结构树移附录；基线历史（429→442→445）移入本文件；G7b 缺口节移附录；变更记录移至本文件。
- **题库/AGENTS.md**：「学科版本要点」状态快照改为路由引用 `各册更新情况清单.md`；变更记录移至本文件。

## 2026-09-23

- **docs/AGENTS.md**：（初始创建，承接历史文档路由职责）
- **src/XiaoShuTong.Domain/AGENTS.md**：初始创建——领域层 Agent 规范（定位/目录结构/强制 Skill 路由/活态文档优先/生成物禁区/tkwf-business 时机/Service 编写要点/构建验证），落实"编写业务方法必须 tkwf-service skill"。
- **src/XiaoShuTong.AdminWasm/AGENTS.md**：初始创建——项目定位/调用通道/版本纪律/页面路由映射/生成物禁区/构建验证。
- **src/XiaoShuTong.WebH5/AGENTS.md**：补「变更记录」节——对齐其余子产品 AGENTS.md（AdminWasm/tests/题库 均有）。文件本体为更早提交创建（含 开发工具/依赖/架构/Lessons），此前无变更记录。
- **tests/XiaoShuTong.Tests/AGENTS.md**：初始创建。基线 AdminWasm-V0.1.8（Tier 1.5 恢复，G7b 由框架 v4.10.21 修复），345 通过 / 0 失败 / 2 跳过。
- **题库/AGENTS.md**：初始创建——题库内容资产 Agent 规范（对齐 2024 修订版四件套收尾完成状态）。

---

## 测试基线变更溯源（tests/AGENTS.md 内嵌历史，移此留存）

| 日期 | 变化 | 说明 |
|------|------|------|
| 2026-09-23 | → 345/0/2 | 初始基线（AdminWasm-V0.1.8，Tier 1.5 恢复） |
| 2026-09-28 | 345 → 429/0/2 | 领域-V0.6.19；V0.6.9~V0.6.19 十二轮迭代新增用例 |
| 2026-09-29 | 429 → 442/0/2 | 领域-V0.7.0；搭子同年级跨群候选 +9、薄弱点下钻 +4 |
| 2026-09-29 | 442 → 445/0/2 | 领域-V0.7.1；薄弱点 Topic 富化 +2（另有走查 Removed 重邀复用 +1 计入 V0.7.0） |