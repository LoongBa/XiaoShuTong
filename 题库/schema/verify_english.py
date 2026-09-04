#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""P3 英语适配器验证：schema 校验 + O 系列规则判题演示 + Prompt 路由验证"""
import json
import re
import sys

BASE = r"F:\LoongBa_Git\XiaoShuTong\题库"
sys.path.insert(0, BASE + r"\schema")
import rule_grader

items = json.load(open(BASE + r"\english\PEP-2024修订版\english-7to9-pep-2024r.json", encoding="utf-8"))
qmap = {q["id"]: q for q in items}

# 1. schema 校验
pattern = r"^Q-[a-z]{2,}-[a-z0-9]+-[0-9]{4,}$"
valid_types = ["R1", "R2", "R3a", "R3b", "R4", "O1", "O2", "O3", "O4", "O5", "X1", "X2", "X3"]
problems = 0
for q in items:
    if not re.match(pattern, q["id"]):
        print(f"BAD {q['id']}: ID 格式"); problems += 1
    if q["type"] not in valid_types:
        print(f"BAD {q['id']}: type={q['type']}"); problems += 1
    if q["type"] in ("O1", "O2", "O3") and (not q["content"].get("options") or not q["content"].get("correct_option")):
        print(f"BAD {q['id']}: O 系列缺 options/correct_option"); problems += 1
    if q["type"] == "O4" and not q["content"].get("pairs"):
        print(f"BAD {q['id']}: O4 缺 pairs"); problems += 1
    if q["type"] in ("R1", "R2", "R3a", "R3b", "O5") and not q["content"].get("keywords"):
        print(f"BAD {q['id']}: 缺 keywords"); problems += 1
print(f"1. 英语题库 schema 校验: {len(items)} 题, 问题={problems}")

# 2. O 系列规则判题演示（复用 rule_grader）
print("\n2. O 系列规则判题演示:")
o_demos = [
    ("Q-eng-o1-0001", "B", "correct"),
    ("Q-eng-o1-0001", "A", "wrong"),
    ("Q-eng-o2-0001", "A,B,D", "correct"),
    ("Q-eng-o2-0001", "A,B", "partial"),
    ("Q-eng-o3-0001", "对", "correct"),
    ("Q-eng-o3-0001", "错", "wrong"),
    ("Q-eng-o4-0001", [{"left": "big", "right": "small"}, {"left": "hot", "right": "cold"},
                       {"left": "fast", "right": "slow"}, {"left": "happy", "right": "sad"}], "correct"),
    ("Q-eng-o4-0001", [{"left": "big", "right": "small"}], "partial"),
    ("Q-eng-o5-0001", "my", "correct"),
    ("Q-eng-o5-0001", "me", "wrong"),
]
ok = 0
for qid, ans, exp in o_demos:
    r = rule_grader.grade(qmap[qid], ans)
    tag = "PASS" if r["result"] == exp else "FAIL"
    if r["result"] == exp: ok += 1
    print(f"  [{tag}] {qid} user={str(ans)[:24]!r} -> {r['result']}")
print(f"  O 系列规则判题: {ok}/{len(o_demos)} 通过")

# 3. Prompt 路由验证（学科注册表映射）
reg = json.load(open(BASE + r"\schema\学科注册表.json", encoding="utf-8"))
eng = next(s for s in reg["subjects"] if s["subject"] == "english")
print(f"\n3. 英语学科注册表: gradingPreference={eng['gradingPreference']}, cardTemplate={eng['knowledgeCardTemplate']}")
print(f"   supportedTypes={eng['supportedTypes']}")
for qtype in ("R1", "R2", "R3a", "R3b"):
    print(f"   {qtype} -> prompt={eng['promptOverrides'].get(qtype, '通用判题(未覆盖)')}")
