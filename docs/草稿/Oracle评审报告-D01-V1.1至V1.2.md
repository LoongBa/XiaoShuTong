# Oracle 架构评审报告（D01 V1.1 → V1.2）

> 评审对象：`docs/D01-统一题库与多学科学习平台设计.md`（V1.1）
> 评审日期：2026-09-04
> 评审结论：**7.5 / 10** —— 骨架可立，但关节处（题型族粒度、判题契约、状态机字段、PK 技术基线）需一次"V1.2 对齐补丁"
> 修复状态：**已全部落地**（见本报告第四节）

---

## 一、总体评价

三维解耦（学科×题型×学习目的）方向正确、五层分层清晰、零废弃迁移有诚意，但存在 5 处真实抽象漏洞（Schema 排斥 R4、Prompt 命名断层且 S3/S4 坍缩、keyword 阈值与 V3 三元结果冲突、PK 实时性与 H5 轮询冲突、memory_states 字段缺失），必须在 P2 之前修复，否则框架落地即返工。

---

## 二、TOP 5 必须修复问题

| 排序 | 问题 | 严重度 | 修复动作 |
|:---:|------|:---:|---------|
| 1 | **question.v1.json 的 content.anyOf 排斥 R4**（R4 无 keywords/options/pairs 无法通过校验）+ required(question,answer) 对展示型不适用 | 高 | anyOf 增加 R4 分支；content.required 按 type 条件化 |
| 2 | **Prompt S1-S4 与 R1-R3 命名断层，且 S3/S4 坍缩为 R3**（判题逻辑不同却共用键） | 高 | R3 拆 R3a（段落）/R3b（整篇）子型，Prompt 文件重命名对齐 |
| 3 | **memory_states 缺 consecutive_correct/history_accuracy**（"连续2次→★"无法实现） | 高 | MemoryState 实体与表补字段 + hint×result×state 迁移矩阵 |
| 4 | **keyword≥80% 直接判 correct 跳过 partial**，违反 V3 三元判题契约 | 高 | 阈值对齐：≥85%→correct，60-85%→partial，<60%→LLM |
| 5 | **PK 实时同步需求与 H5 30s 轮询降级冲突**，技术可行性存疑 | 高 | 加异步回合制备选方案；P1a 阶段做 WS 可行性 PoC |

---

## 三、前瞻性优化建议（采纳 3/5）

| # | 建议 | 采纳 |
|---|------|:---:|
| 1 | 题型族引入"子型"维度（R3a/R3b） | ✅ 已落地 |
| 2 | keywords 升级为"同义词组+权重+required"结构 | ✅ 已落地（question.v1.json） |
| 3 | 引入"场景-状态机耦合策略"（full/feedback_only/isolated） | ✅ 已落地（D01 10.3） |
| 4 | 学科适配器增加"按题型覆盖容错度"（gradingPreference overrides） | 🔄 部分（schema 补 tolerance 字段，注册表 overrides 待 P3 英语适配器时落地） |
| 5 | R1 题型 contentSchema 按学科分化（英语翻译 vs 语文补全） | ⏳ 待 P3 英语适配器时评估（引入 translation 子型） |

---

## 四、V1.2 修复落地清单（已核验）

| 修复 | 文件 | 状态 |
|------|------|:---:|
| R3a/R3b 子型化 + Prompt 重命名 | `题库/判题Prompt/题型-R3a-段落默写.md`、`题型-R3b-整篇默写.md` 等 4 文件 | ✅ |
| question.v1.json 修复（R4 兼容/子型化/容错度/审计字段/keywords 升级） | `题库/schema/question.v1.json`（V1.1） | ✅ JSON 合法 |
| bank.v1.json 补 supported_types/knowledge_card_template | `题库/schema/bank.v1.json`（V1.1） | ✅ JSON 合法 |
| keyword 阈值对齐三元判题（6.3 判题链路由） | `docs/D01` | ✅ |
| memory_states 字段补齐 + 迁移矩阵（10.1/10.2） | `docs/D01` | ✅ |
| 场景-状态机耦合策略（10.3） | `docs/D01` | ✅ |
| PK 异步回合制备选方案（8.4.1/8.4.3 双模式时序图） | `docs/D01` | ✅ |
| Prompt 绑定矩阵文件引用对齐（9.2） | `docs/D01` | ✅ |
| 决策记录 + 变更记录 V1.2（13.1/变更记录） | `docs/D01` | ✅ |
| P1a（PK 技术 PoC）加入路线图 | `docs/D01` | ✅ |

---

## 五、遗留待办（P2 前）

1. P1a：PK WebSocket 可行性 PoC（微信 X5 内核验证）
2. .txt → JSON 解析器（P1 剩余）
3. P2：客观型 O1-O5 题型注册前，用现有 435 题跑一遍 schema 校验作为回归基线
4. P3 英语适配器：评估 R1 翻译子型（source_lang/target_lang 字段）
