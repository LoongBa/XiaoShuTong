#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
小书童 P1 题库迁移解析器：.txt → 统一 JSON Schema（初中 + 小学双范围）
- 原始题库：qtype 启发式推断
- 训练题库：按 S1-S4 标记映射 R1/R2/R3a/R3b
- 背景钩子：解析为 author_card 知识卡片
- 输出：
  题库/chinese/七-九年级-统编教材/    （初中 7-9 年级）
  题库/chinese/小学1-6年级-统编教材/  （小学 1-6 年级）
用法：python txt2json.py
"""

import json
import os
import re
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
        qtype = infer_qtype(q)
        titles = extract_title(q)
        seq["n"] += 1
        item = {
            "id": f"Q-{subject}-{bank_code}-{seq['n']:04d}",
            "bank_id": bank_id,
            "subject": subject,
            "topic": f"{grade_prefix}年级{vol}册",
            "knowledge_points": titles or ["未分类"],
            "type": qtype,
            "purpose_tags": ["memorize", "play"],
            "content": {
                "question": q,
                "answer": a,
                "keywords": make_keywords(a),
                "tolerance": "semantic_tolerant",
            },
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
        qtype = current_type or "R1"
        if q.startswith("默写"):
            qtype = "R3b" if "全文" in q or "整篇" in q else "R3a"
        titles = extract_title(q)
        seq["n"] += 1
        item = {
            "id": f"Q-{subject}-{bank_code}-{seq['n']:04d}",
            "bank_id": bank_id,
            "subject": subject,
            "topic": "背诵训练",
            "knowledge_points": titles or ["未分类"],
            "type": qtype,
            "purpose_tags": ["memorize"],
            "content": {
                "question": q,
                "answer": a,
                "keywords": make_keywords(a),
                "tolerance": "semantic_tolerant",
            },
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
            train_file = BASE / "背诵训练" / info["dir"] / train_name
            if train_file.exists():
                items = parse_training_txt(train_file, "chinese", cfg["bank_id"], cfg["bank_code"])
                (out / f"{grade}{vol_key}册-背诵训练.json").write_text(
                    json.dumps(items, ensure_ascii=False, indent=2), encoding="utf-8")
                summary[f"{grade}{vol_key}训练"] = len(items)

    hook_files = sorted(
        f for f in (BASE / "背景钩子").glob("*-背景钩子.md")
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

def main():
    for cfg in RANGES:
        migrate_range(cfg)

if __name__ == "__main__":
    main()
