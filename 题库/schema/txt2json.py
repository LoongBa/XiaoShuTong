#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
小书童 P1 题库迁移解析器：.txt → 统一 JSON Schema（初中 + 小学双范围）
- 原始题库：qtype 启发式推断
- 训练题库：按 S1-S4 标记映射 R1/R2/R3a/R3b
- 背景钩子：解析为知识卡片
  * chinese（默认流程）：author_card 知识卡片（旧逻辑，回归兼容）
  * english/history/geography/biology/chemistry/daodeyufazhi（hooks 命令）：
    word_card / event_card / region_card / concept_card / concept_card / concept_card 知识卡片
    ——按各学科 md 标题格式（`## Unit N ...` / `## 第N课 ...` / `## 节标题` /
       `## 第N单元 ...` + `### 节标题`）通用解析，字段名保留原始中文名。
- 判题策略标记（可选）：题干行内 `[js=exact|similarity|ai_guided|hybrid]` 与
  `[it=voice|text|select|drag|swipe|card]`，解析后剥离并写入 content 对应字段。
  未标注的题目不落盘新字段（判题引擎从题型/学科注册表推导缺省，向后兼容）。
- 输出：
  题库/chinese/七-九年级-统编教材/    （初中 7-9 年级）
  题库/chinese/小学1-6年级-统编教材/  （小学 1-6 年级）
  题库/{english|history|geography|biology|chemistry|daodeyufazhi}/{版本}/背景钩子-知识卡片.json
用法：
  python txt2json.py          # 语文迁移（默认，行为不变）
  python txt2json.py hooks    # 生成 6 学科背景钩子知识卡片 JSON
  python txt2json.py l12      # 生成历史/地理/道法 L1 背诵内容 + L2 背诵训练 JSON
                              #   （知识点按 第N课/第N节/第N章/第N单元 标题归属）
"""

import json
import os
import re
import sys
from pathlib import Path

BASE = Path(r"F:\LoongBa_Git\XiaoShuTong\题库")

# 范围配置：初中 / 小学
RANGES = [
    {
        "name": "七-九年级-统编教材",
        "bank_id": "chinese-7to9-pep",
        "bank_code": "7to9",
        "grades": {
            "七年级": {"prefix": "7", "dir": "七年级"},
            "八年级": {"prefix": "8", "dir": "八年级"},
            "九年级": {"prefix": "9", "dir": "九年级"},
        },
        "raw_suffix": "古诗文",
        "hook_filter": lambda n: n.startswith(("七", "八", "九")),
        "card_template": "author_card",
    },
    {
        "name": "小学1-6年级-统编教材",
        "bank_id": "chinese-p1to6-pep",
        "bank_code": "p1to6",
        "grades": {
            "一年级": {"prefix": "1", "dir": "一年级"},
            "二年级": {"prefix": "2", "dir": "二年级"},
            "三年级": {"prefix": "3", "dir": "三年级"},
            "四年级": {"prefix": "4", "dir": "四年级"},
            "五年级": {"prefix": "5", "dir": "五年级"},
            "六年级": {"prefix": "6", "dir": "六年级"},
        },
        "raw_suffix": "背诵内容",
        "hook_filter": lambda n: n.startswith(("一", "二", "三", "四", "五", "六")),
        "card_template": "author_card",
    },
]

VOLUMES = {"上": "a", "下": "b"}
S2TYPE = {"S1": "R1", "S2": "R2", "S3": "R3a", "S4": "R3b"}

VALID_JS = {"exact", "similarity", "ai_guided", "hybrid"}
VALID_IT = {"voice", "text", "select", "drag", "swipe", "card"}
_MARKER_RE = re.compile(r"\s*\[(js|it)=([a-z_]+)\]\s*")


def parse_override_markers(q: str) -> tuple:
    """
    从题干剥离判题策略标记，返回 (干净题干, overrides dict)。
    支持：`[js=ai_guided]` `[it=voice]`，二者可并存（按任意顺序）。
    非法值忽略（回退注册表推导），保留原题干文字，保持兼容旧题。
    """
    overrides = {}
    cleaned = q
    for m in _MARKER_RE.finditer(q):
        key = "judging_strategy" if m.group(1) == "js" else "interaction_type"
        val = m.group(2)
        allowed = VALID_JS if key == "judging_strategy" else VALID_IT
        if val in allowed:
            overrides[key] = val
            cleaned = cleaned.replace(m.group(0), "")
    return cleaned.strip(), overrides

seq = {"n": 0}

def next_id(subject: str, bank: str) -> str:
    seq["n"] += 1
    return f"Q-{subject}-{bank}-{seq['n']:04d}"

def extract_title(question: str) -> list:
    return re.findall(r"《([^》]+)》", question)

def make_keywords(answer: str) -> list:
    a = answer.strip()
    if not a:
        return [{"aliases": ["答案"], "weight": 1, "required": False}]
    parts = [p for p in re.split(r"[。；，,;]", a) if p.strip()]
    aliases = parts[:2] if len(parts) > 1 else [a]
    return [{"aliases": aliases, "weight": 2, "required": True}]

def infer_qtype(question: str) -> str:
    q = question.strip()
    if q.startswith("默写") or "默写《" in q:
        return "R3b"
    if re.search(r"作者|选自|体裁|著作|出处|朝代", q):
        return "R4"
    if re.search(r"后一句|下一句|上一句|完整句|后两句|前一句", q):
        return "R1"
    if re.search(r"中.*句子是|名句是|主旨", q):
        return "R1"
    return "R1"

def parse_raw_txt(file: Path, subject: str, bank_id: str, bank_code: str, grade_prefix: str, vol: str, raw_suffix: str) -> list:
    items = []
    content = file.read_text(encoding="utf-8-sig")
    for line in content.splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "###" not in line:
            continue
        q, a = line.split("###", 1)
        q, a = q.strip(), a.strip()
        if not q or not a:
            continue
        q, overrides = parse_override_markers(q)
        qtype = infer_qtype(q)
        titles = extract_title(q)
        seq["n"] += 1
        content = {
            "question": q,
            "answer": a,
            "tolerance": "semantic_tolerant",
        }
        if qtype != "R4":
            content["keywords"] = make_keywords(a)
        content.update(overrides)  # 可选 [js=] [it=] 标记落盘
        item = {
            "id": f"Q-{subject}-{bank_code}-{seq['n']:04d}",
            "bank_id": bank_id,
            "subject": subject,
            "topic": f"{grade_prefix}年级{vol}册",
            "knowledge_points": titles or ["未分类"],
            "type": qtype,
            "purpose_tags": ["memorize", "play"],
            "content": content,
            "meta": {
                "difficulty": 1,
                "source": f"人教社统编版 2024 修订（{raw_suffix}）",
                "knowledge_card_id": f"KC-{subject}-{titles[0] if titles else '未分类'}",
            },
        }
        if qtype == "R4":
            item["content"].pop("keywords", None)
        items.append(item)
    return items

def parse_training_txt(file: Path, subject: str, bank_id: str, bank_code: str) -> list:
    items = []
    content = file.read_text(encoding="utf-8-sig")
    current_type = None
    for line in content.splitlines():
        line = line.strip()
        if not line:
            continue
        m = re.match(r"^#\s*(S[1-4])\s", line)
        if m:
            current_type = S2TYPE[m.group(1)]
            continue
        if line.startswith("#"):
            continue
        if "###" not in line:
            continue
        q, a = line.split("###", 1)
        q, a = q.strip(), a.strip()
        if not q or not a:
            continue
        q, overrides = parse_override_markers(q)
        qtype = current_type or "R1"
        if q.startswith("默写"):
            qtype = "R3b" if "全文" in q or "整篇" in q else "R3a"
        titles = extract_title(q)
        seq["n"] += 1
        content = {
            "question": q,
            "answer": a,
            "keywords": make_keywords(a),
            "tolerance": "semantic_tolerant",
        }
        content.update(overrides)
        item = {
            "id": f"Q-{subject}-{bank_code}-{seq['n']:04d}",
            "bank_id": bank_id,
            "subject": subject,
            "topic": "背诵训练",
            "knowledge_points": titles or ["未分类"],
            "type": qtype,
            "purpose_tags": ["memorize"],
            "content": content,
            "meta": {
                "difficulty": 1,
                "source": "分阶检索训练题库（S1-S4）",
                "knowledge_card_id": f"KC-{subject}-{titles[0] if titles else '未分类'}",
            },
        }
        items.append(item)
    return items

def parse_background_hooks(files: list, subject: str, template: str) -> list:
    cards = []
    for f in files:
        content = f.read_text(encoding="utf-8-sig")
        current_title = None
        fields = {}
        for line in content.splitlines():
            line = line.strip()
            m = re.match(r"^##\s+《([^》]+)》", line)
            if m:
                if current_title and fields:
                    cards.append({
                        "id": f"KC-{subject}-{current_title}",
                        "subject": subject,
                        "template": template,
                        "title": current_title,
                        "fields": fields,
                    })
                current_title = m.group(1)
                fields = {}
                continue
            fm = re.match(r"^-\s*\*\*([^*]+)\*\*：(.+)$", line)
            if fm and current_title:
                fields[fm.group(1).strip()] = fm.group(2).strip()
        if current_title and fields:
            cards.append({
                "id": f"KC-{subject}-{current_title}",
                "subject": subject,
                "template": template,
                "title": current_title,
                "fields": fields,
            })
    return cards

# ============================================================
# 多学科背景钩子解析（English / History / Geography / Biology /
# Chemistry / Daodeyufazhi）——通用解析器（chinese 旧逻辑不回退）
# ============================================================

# subject -> (版本目录, knowledgeCardTemplate，对齐 题库/schema/学科注册表.json)
SUBJECT_HOOK_CONFIG = {
    "english": ("PEP-2024修订版", "word_card"),
    "history": ("统编-2024修订版", "event_card"),
    "geography": ("统编-2024修订版", "region_card"),
    "biology": ("统编-2024修订版", "concept_card"),
    "chemistry": ("PEP-2024修订版", "concept_card"),
    "daodeyufazhi": ("统编-2024修订版", "concept_card"),
}

_H2_RE = re.compile(r"^##\s+(.+)$")
_H3_RE = re.compile(r"^###\s+(.+)$")
# 标准字段行：`- **字段名**：值`（字段名后允许带注解，如 `**根尖结构**（下→上）：值`）
_FIELD_COLON_RE = re.compile(r"^-\s*\*\*(.+?)\*\*(.*?)[：:]\s*(.+)$")
# 兜底字段行：`- **字段名** 其余内容`（无冒号，如 `**血浆 + 血细胞**（红细胞/白细胞/血小板）`）
_FIELD_FALLBACK_RE = re.compile(r"^-\s*\*\*(.+?)\*\*(.*)$")
# 序号前缀：`第N课` / `第X课`（中文数字）/ `第N单元` / `Unit N`
_ORD_CN_RE = re.compile(r"^第[一二三四五六七八九十百\d]+(课|单元)\s*")
_ORD_UNIT_RE = re.compile(r"^Unit\s+\d+\s*")
_BOOK_TITLE_RE = re.compile(r"^《([^》]+)》")


def clean_hook_title(raw: str, subject: str) -> str:
    """节标题去序号 / 去书名号：保持与语文 KC-chinese-观沧海 同构。"""
    t = raw.strip()
    m = _BOOK_TITLE_RE.match(t)
    if m:
        return m.group(1).strip()
    if subject == "english":
        t = _ORD_UNIT_RE.sub("", t)
    else:
        t = _ORD_CN_RE.sub("", t)
    return t.strip()


def _book_key(fname: str) -> str:
    """由文件名提取「册/模块」键（用于同标题卡片消歧）。"""
    name = fname
    for suf in ("-背景钩子.md", "-知识卡片.md"):
        if name.endswith(suf):
            name = name[: -len(suf)]
            break
    m = re.match(r"^(模块[A-F])", name)
    if m:
        return m.group(1)
    return name


def parse_background_hooks_generic(files: list, subject: str, template: str) -> tuple:
    """
    通用背景钩子解析器（支持 6 学科标题格式 + 化学 3 级标题 + markdown 表格）。
    卡片 = `##`（非化学）或 `###`（化学）节标题；字段 = `- **字段名**：值` 行。

    返回 (cards, warnings)：
    - cards: [{id, subject, template, title, fields}]
    - warnings: 解析异常明细（无值字段 / 同标题消歧 / 非标准字段行等）
    """
    cards = []
    warnings = []

    def close_card(raw_title, fields, has_content, table_buf, src_fname):
        nonlocal cards, warnings
        if raw_title and has_content:
            if table_buf:
                key, n = "表格", 2
                while key in fields:
                    key = f"表格{n}"
                    n += 1
                fields[key] = "\n".join(table_buf).strip()
            cards.append({
                "_src": src_fname,
                "id": f"KC-{subject}-{clean_hook_title(raw_title, subject)}",
                "subject": subject,
                "template": template,
                "title": clean_hook_title(raw_title, subject),
                "fields": fields,
            })

    for f in files:
        content = f.read_text(encoding="utf-8-sig")
        raw_title = None
        fields = {}
        has_content = False
        table_buf = []
        for line in content.splitlines():
            s = line.strip()
            h2 = _H2_RE.match(s)
            h3 = _H3_RE.match(s)
            if h2 or h3:
                close_card(raw_title, fields, has_content, table_buf, f.name)
                raw_title = (h2 if h2 else h3).group(1).strip()
                fields = {}
                has_content = False
                table_buf = []
                continue
            if s.startswith("|"):  # markdown 表格行：保留为 fields 内字符串，卡片不丢
                if raw_title:
                    has_content = True
                    table_buf.append(s)
                continue
            if s.startswith("- "):
                if not raw_title:
                    continue
                m = _FIELD_COLON_RE.match(s)
                if m:
                    fields[(m.group(1) + m.group(2)).strip()] = m.group(3).strip()
                    has_content = True
                    continue
                m = _FIELD_FALLBACK_RE.match(s)
                if m:
                    name = m.group(1).strip()
                    value = m.group(2).strip()
                    fields[name] = value
                    has_content = True
                    if not value:
                        warnings.append(f"{f.name}：字段「{name}」无值（md 原样）")
                    continue
                # 纯 `- 文本` 列表项（无 ** 字段）：忽略
        close_card(raw_title, fields, has_content, table_buf, f.name)

    # 同标题消歧：同一 title 多次出现（如生物 模块A/模块B 同名节、历史 九上/九下 同名课）
    # 首个来源（文件排序在前）保留干净标题，后续卡片标题追加「（{册/模块}）」
    seen_first = {}
    for c in cards:
        seen_first.setdefault(c["title"], c["_src"])
    for c in cards:
        first_src = seen_first.get(c["title"])
        if first_src and c["_src"] != first_src:
            c["title"] = f"{c['title']}（{_book_key(c['_src'])}）"
            c["id"] = f"KC-{subject}-{c['title']}"
            warnings.append(
                f"{c['_src']}：与 {first_src} 同标题，已消歧 → {c['title']}")
    for c in cards:
        c.pop("_src", None)
    return cards, warnings


def migrate_subject_hooks():
    """生成 6 学科背景钩子知识卡片 JSON（不触碰 chinese）。"""
    for subj, (ver, template) in SUBJECT_HOOK_CONFIG.items():
        src_dir = BASE / "背景钩子" / subj / ver
        out = BASE / subj / ver
        files = sorted(src_dir.glob("*.md"))
        cards, warnings = parse_background_hooks_generic(files, subj, template)
        os.makedirs(out, exist_ok=True)
        dest = out / "背景钩子-知识卡片.json"
        dest.write_text(json.dumps(cards, ensure_ascii=False, indent=2), encoding="utf-8")
        empty_fields = [c for c in cards if not c["fields"]]
        print(f"[{subj}] 生成 {len(cards)} 张卡片 → {dest}")
        for w in warnings:
            print(f"  WARN: {w}")
        if empty_fields:
            for c in empty_fields:
                print(f"  EMPTY-FIELDS: {c['id']}")

def migrate_range(cfg: dict):
    global seq
    seq = {"n": 0}
    out = BASE / "chinese" / cfg["name"]
    os.makedirs(out, exist_ok=True)
    summary = {}

    for grade, info in cfg["grades"].items():
        gdir = BASE / info["dir"]
        for vol_key, vol in VOLUMES.items():
            raw_name = f"{grade}{vol_key}册-{cfg['raw_suffix']}.txt"
            raw_file = gdir / raw_name
            if raw_file.exists():
                items = parse_raw_txt(raw_file, "chinese", cfg["bank_id"], cfg["bank_code"], info["prefix"], vol, cfg["raw_suffix"])
                (out / f"{grade}{vol_key}册-背诵内容.json").write_text(
                    json.dumps(items, ensure_ascii=False, indent=2), encoding="utf-8")
                summary[f"{grade}{vol_key}原始"] = len(items)

            train_name = f"{grade}{vol_key}册-背诵训练.txt"
            train_file = BASE / "背诵训练" / "chinese" / "统编-2024修订版" / train_name
            if train_file.exists():
                items = parse_training_txt(train_file, "chinese", cfg["bank_id"], cfg["bank_code"])
                (out / f"{grade}{vol_key}册-背诵训练.json").write_text(
                    json.dumps(items, ensure_ascii=False, indent=2), encoding="utf-8")
                summary[f"{grade}{vol_key}训练"] = len(items)

    hook_files = sorted(
        f for f in (BASE / "背景钩子" / "chinese" / "统编-2024修订版").glob("*-背景钩子.md")
        if cfg["hook_filter"](f.name)
    )
    cards = parse_background_hooks(hook_files, "chinese", cfg["card_template"])
    if cards:
        (out / "背景钩子-知识卡片.json").write_text(
            json.dumps(cards, ensure_ascii=False, indent=2), encoding="utf-8")
        summary["知识卡片"] = len(cards)

    bank_meta = {
        "bank_id": cfg["bank_id"],
        "subject": "chinese",
        "name": cfg["name"],
        "version": "V1.0",
        "source": "人教社统编版 2024 修订",
        "purpose_tags": ["memorize", "assess", "play"],
        "supported_types": ["R1", "R2", "R3a", "R3b", "R4"],
        "knowledge_card_template": cfg["card_template"],
        "default_prompt": "通用判题",
        "type_overrides": {
            "R1": "题型-R1-补全记忆",
            "R2": "题型-R2-逆向补全",
            "R3a": "题型-R3a-段落默写",
            "R3b": "题型-R3b-整篇默写"
        },
        "privacy": "private",
        "owner": "system",
    }
    (out / "bank.json").write_text(
        json.dumps(bank_meta, ensure_ascii=False, indent=2), encoding="utf-8")

    total = sum(summary.values())
    print(f"\n迁移完成 → {out}")
    print(f"总条目：{total}")
    for k, v in summary.items():
        print(f"  {k}: {v}")

# ============================================================
# L1/L2 多学科迁移（历史 / 地理 / 道法）——语文（RANGES）不受影响
# ============================================================
# 知识点归属：按节/课/章/单元标题（去序号）；L1 题型按题目形态推断，
# L2 题型按 S1-S4 分阶标记映射。产物结构对齐语文
# （id/bank_id/subject/topic/knowledge_points/type/purpose_tags/content/meta）。

# 年级前缀（与 RANGES.grades 约定一致）
GRADE_PREFIX = {"七年级": "7", "八年级": "8", "九年级": "9"}

# 第N课 / 第N节 / 第N章 / 第N单元（中文或阿拉伯数字序号）标题提取
_ORD_SEC_RE = re.compile(r"^第[一二三四五六七八九十百\d]+(课|节|章|单元)\s*(.*)$")
# 填空形态：下划线 / 全角括号 / 半角括号留空
_FILL_BLANK_RE = re.compile(r"_{2,}|（\s*）|\(\s*\)")

# subject -> L1/L2 迁移配置（版本目录 / bank_id / bank_code / 容错度 / 卡片模板）
L12_SUBJECT_CONFIG = {
    "history": {
        "name": "历史",
        "version_dir": "统编-2024修订版",
        "bank_id": "history-7to9-2024r",
        "bank_code": "7to9",
        "tolerance": "exact",
        "knowledge_card_template": "event_card",
        "supported_types": ["R1", "R2", "R3a", "R3b"],
        "raw_suffix": "背诵内容",
    },
    "geography": {
        "name": "地理",
        "version_dir": "统编-2024修订版",
        "bank_id": "geography-7to9-2024r",
        "bank_code": "7to9",
        "tolerance": "exact",
        "knowledge_card_template": "region_card",
        "supported_types": ["R1", "R2", "R3a", "R3b"],
        "raw_suffix": "背诵内容",
    },
    "daodeyufazhi": {
        "name": "道德与法治",
        "version_dir": "统编-2024修订版",
        "bank_id": "daodeyufazhi-7to9-2024r",
        "bank_code": "7to9",
        "tolerance": "semantic_tolerant",
        "knowledge_card_template": "concept_card",
        "supported_types": ["R1", "R2", "R3a", "R3b"],
        "raw_suffix": "背诵内容",
    },
}


def clean_section_title(line: str):
    """若行是「第N课/第N节/第N章/第N单元」标题（允许 # / ## / ===== 包裹），
    返回去序号标题（如『第1课 远古时期的人类活动』→『远古时期的人类活动』）；
    否则返回 None。用于历史/地理/道法 L1/L2 的知识点归属。"""
    s = line.strip().lstrip("#").strip()
    s = s.strip("=").strip()
    if not s:
        return None
    m = _ORD_SEC_RE.match(s)
    if not m:
        return None
    title = m.group(2).strip().strip("=").strip()
    return title or None


def infer_qtype_form(question: str) -> str:
    """按题目形态兜底推断题型（非语文学科）：填空 → O5，问句 → R1。
    语文仍走 infer_qtype（中文关键词推断，行为不变）。"""
    q = question.strip()
    if _FILL_BLANK_RE.search(q):
        return "O5"
    return "R1"


def parse_raw_txt_generic(file: Path, subject: str, bank_id: str, bank_code: str,
                          grade_prefix: str, vol: str, raw_suffix: str,
                          tolerance: str) -> list:
    """多学科 L1 背诵内容解析（历史/地理/道法），结构对齐 parse_raw_txt：
    - knowledge_points = 最近一节标题（去序号）
    - type = 按题目形态推断（____→O5、问句→R1）
    - tolerance = 学科注册表 gradingPreference
    """
    items = []
    content = file.read_text(encoding="utf-8-sig")
    current_kp = None
    for line in content.splitlines():
        line = line.strip()
        if not line:
            continue
        kp = clean_section_title(line)
        if kp is not None:
            current_kp = kp
            continue
        if line.startswith("#") or "###" not in line:
            continue
        q, a = line.split("###", 1)
        q, a = q.strip(), a.strip()
        if not q or not a:
            continue
        q, overrides = parse_override_markers(q)
        qtype = infer_qtype_form(q)
        seq["n"] += 1
        content = {
            "question": q,
            "answer": a,
            "keywords": make_keywords(a),
            "tolerance": tolerance,
        }
        content.update(overrides)  # 可选 [js=] [it=] 标记落盘
        item = {
            "id": f"Q-{subject}-{bank_code}-{seq['n']:04d}",
            "bank_id": bank_id,
            "subject": subject,
            "topic": f"{grade_prefix}年级{vol}册",
            "knowledge_points": [current_kp] if current_kp else ["未分类"],
            "type": qtype,
            "purpose_tags": ["memorize", "play"],
            "content": content,
            "meta": {
                "difficulty": 1,
                "source": f"人教社统编版 2024 修订（{raw_suffix}）",
                "knowledge_card_id": f"KC-{subject}-{current_kp if current_kp else '未分类'}",
            },
        }
        items.append(item)
    return items


def parse_training_txt_generic(file: Path, subject: str, bank_id: str, bank_code: str,
                               tolerance: str) -> list:
    """多学科 L2 背诵训练解析（历史/地理/道法），结构对齐 parse_training_txt：
    - type = S1-S4 分阶标记映射（R1/R2/R3a/R3b），不做语文『默写』关键词覆盖
    - knowledge_points = 最近一节标题（## 第N课 / ## 第N章 等，去序号）
    """
    items = []
    content = file.read_text(encoding="utf-8-sig")
    current_type = None
    current_kp = None
    for line in content.splitlines():
        line = line.strip()
        if not line:
            continue
        m = re.match(r"^#\s*(S[1-4])\s", line)
        if m:
            current_type = S2TYPE[m.group(1)]
            continue
        kp = clean_section_title(line)
        if kp is not None:
            current_kp = kp
            continue
        if line.startswith("#"):
            continue
        if "###" not in line:
            continue
        q, a = line.split("###", 1)
        q, a = q.strip(), a.strip()
        if not q or not a:
            continue
        q, overrides = parse_override_markers(q)
        qtype = current_type or "R1"
        seq["n"] += 1
        content = {
            "question": q,
            "answer": a,
            "keywords": make_keywords(a),
            "tolerance": tolerance,
        }
        content.update(overrides)
        item = {
            "id": f"Q-{subject}-{bank_code}-{seq['n']:04d}",
            "bank_id": bank_id,
            "subject": subject,
            "topic": "背诵训练",
            "knowledge_points": [current_kp] if current_kp else ["未分类"],
            "type": qtype,
            "purpose_tags": ["memorize"],
            "content": content,
            "meta": {
                "difficulty": 1,
                "source": "分阶检索训练题库（S1-S4）",
                "knowledge_card_id": f"KC-{subject}-{current_kp if current_kp else '未分类'}",
            },
        }
        items.append(item)
    return items


def migrate_subject_l12():
    """生成历史/地理/道法 L1（背诵内容）+ L2（背诵训练）JSON（与语文同构）。
    语文 RANGES/migrate_range 与 hooks 流程均不受影响。"""
    global seq
    for subj, cfg in L12_SUBJECT_CONFIG.items():
        seq = {"n": 0}
        ver = cfg["version_dir"]
        src_dir = BASE / subj / ver
        trn_dir = BASE / "背诵训练" / subj / ver
        out_dir = BASE / subj / ver
        os.makedirs(out_dir, exist_ok=True)
        summary = {}

        for raw_file in sorted(src_dir.glob(f"*-{cfg['raw_suffix']}.txt")):
            m = re.match(r"^(.*年级)([上下])册-", raw_file.name)
            if not m:
                print(f"  SKIP（文件名无法解析年级/册）: {raw_file.name}")
                continue
            grade, vol_key = m.group(1), m.group(2)
            prefix = GRADE_PREFIX.get(grade, "")
            vol = VOLUMES[vol_key]
            items = parse_raw_txt_generic(
                raw_file, subj, cfg["bank_id"], cfg["bank_code"],
                prefix, vol, cfg["raw_suffix"], cfg["tolerance"])
            dest = out_dir / f"{grade}{vol_key}册-背诵内容.json"
            dest.write_text(json.dumps(items, ensure_ascii=False, indent=2), encoding="utf-8")
            summary[f"{grade}{vol_key}原始"] = len(items)

            train_file = trn_dir / f"{grade}{vol_key}册-背诵训练.txt"
            if train_file.exists():
                items = parse_training_txt_generic(
                    train_file, subj, cfg["bank_id"], cfg["bank_code"], cfg["tolerance"])
                (out_dir / f"{grade}{vol_key}册-背诵训练.json").write_text(
                    json.dumps(items, ensure_ascii=False, indent=2), encoding="utf-8")
                summary[f"{grade}{vol_key}训练"] = len(items)

        bank_meta = {
            "bank_id": cfg["bank_id"],
            "subject": subj,
            "name": cfg["name"],
            "version": "V1.0",
            "source": "人教社统编版 2024 修订",
            "purpose_tags": ["memorize", "assess", "play"],
            "supported_types": cfg["supported_types"],
            "knowledge_card_template": cfg["knowledge_card_template"],
            "grading_preference": cfg["tolerance"],
            "default_prompt": "通用判题",
            "privacy": "private",
            "owner": "system",
        }
        (out_dir / "bank.json").write_text(
            json.dumps(bank_meta, ensure_ascii=False, indent=2), encoding="utf-8")

        total = sum(summary.values())
        print(f"\n迁移完成 → {out_dir}")
        print(f"总条目：{total}")
        for k, v in summary.items():
            print(f"  {k}: {v}")


def main():
    args = sys.argv[1:]
    if args and args[0] == "hooks":
        migrate_subject_hooks()
    elif args and args[0] == "l12":
        migrate_subject_l12()
    else:
        for cfg in RANGES:
            migrate_range(cfg)

if __name__ == "__main__":
    main()
