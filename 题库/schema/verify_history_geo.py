#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""P4 历史/地理适配器验证：schema 校验 + O 系列规则判题 + 知识卡片结构 + 题目-卡片引用完整性"""
import json
import re
import sys

BASE = r"F:\LoongBa_Git\XiaoShuTong\题库"
sys.path.insert(0, BASE + r"\schema")
import rule_grader

# ---------- 载入数据 ----------
banks = {
    "history":   json.load(open(BASE + r"\history\统编-2024修订版\history-7to9-2024r.json", encoding="utf-8")),
    "geography": json.load(open(BASE + r"\geography\统编-2024修订版\geography-7to9-2024r.json", encoding="utf-8")),
}
cards = {
    "history":   json.load(open(BASE + r"\history\统编-2024修订版\history-cards.json", encoding="utf-8")),
    "geography": json.load(open(BASE + r"\geography\统编-2024修订版\geography-cards.json", encoding="utf-8")),
}
reg = json.load(open(BASE + r"\schema\学科注册表.json", encoding="utf-8"))

pattern = r"^Q-[a-z]{2,}-[a-z0-9]+-[0-9]{4,}$"
valid_types = ["R1", "R2", "R3a", "R3b", "R4", "O1", "O2", "O3", "O4", "O5", "X1", "X2", "X3"]
total_problems = 0

# ---------- 1. 题库 schema 校验 ----------
for subj, items in banks.items():
    problems = 0
    for q in items:
        if not re.match(pattern, q["id"]):
            print(f"BAD {q['id']}: ID 格式"); problems += 1
        if q["type"] not in valid_types:
            print(f"BAD {q['id']}: type={q['type']}"); problems += 1
        if q["subject"] != subj:
            print(f"BAD {q['id']}: subject={q['subject']} 与目录不符"); problems += 1
        if q["type"] in ("O1", "O2", "O3") and (not q["content"].get("options") or not q["content"].get("correct_option")):
            print(f"BAD {q['id']}: O 系列缺 options/correct_option"); problems += 1
        if q["type"] == "O4" and not q["content"].get("pairs"):
            print(f"BAD {q['id']}: O4 缺 pairs"); problems += 1
        if q["type"] in ("R1", "R2") and not q["content"].get("keywords"):
            print(f"BAD {q['id']}: R 系列缺 keywords"); problems += 1
        if not q["content"].get("tolerance"):
            print(f"BAD {q['id']}: 缺 tolerance"); problems += 1
    total_problems += problems
    print(f"1. {subj} 题库 schema 校验: {len(items)} 题, 问题={problems}")

# ---------- 2. 知识卡片结构校验 ----------
for subj, c in cards.items():
    problems = 0
    expected_template = "event_card" if subj == "history" else "region_card"
    if c["template"] != expected_template:
        print(f"BAD {subj}: template={c['template']} 应为 {expected_template}"); problems += 1
    for card in c["cards"]:
        for field in ("id", "subject", "name", "kind", "fields", "linked_question_ids"):
            if field not in card:
                print(f"BAD {card.get('id', '?')}: 缺字段 {field}"); problems += 1
        if card["subject"] != subj:
            print(f"BAD {card['id']}: subject 不符"); problems += 1
        all_qids = {q["id"] for q in banks[subj]}
        for qid in card["linked_question_ids"]:
            if qid not in all_qids:
                print(f"BAD {card['id']}: 引用题目 {qid} 不存在"); problems += 1
    total_problems += problems
    print(f"2. {subj} 知识卡片结构: {len(c['cards'])} 张, 问题={problems}")

# ---------- 3. 题目→卡片反向引用校验 ----------
for subj, items in banks.items():
    card_ids = {x["id"] for x in cards[subj]["cards"]}
    problems = 0
    for q in items:
        kid = q["meta"].get("knowledge_card_id")
        if not kid:
            print(f"BAD {q['id']}: 缺 knowledge_card_id"); problems += 1
        elif kid not in card_ids:
            print(f"BAD {q['id']}: 引用卡片 {kid} 不存在"); problems += 1
    total_problems += problems
    print(f"3. {subj} 题目→卡片反向引用: 问题={problems}")

# ---------- 4. O 系列规则判题演示（复用 rule_grader）----------
demos = [
    ("history",   "Q-his-o1-0001", "A", "correct"),
    ("history",   "Q-his-o1-0001", "C", "wrong"),
    ("history",   "Q-his-o2-0001", "A,B,D", "correct"),
    ("history",   "Q-his-o2-0001", "A,B", "partial"),
    ("history",   "Q-his-o3-0001", "对", "correct"),
    ("history",   "Q-his-o3-0001", "错", "wrong"),
    ("history",   "Q-his-o4-0001",
     [{"left": "蔡伦", "right": "改进造纸术"},
      {"left": "张仲景", "right": "著《伤寒杂病论》"},
      {"left": "祖冲之", "right": "把圆周率精确到小数点后第七位"},
      {"left": "毕昇", "right": "发明活字印刷术"}], "correct"),
    ("history",   "Q-his-o4-0001", [{"left": "蔡伦", "right": "改进造纸术"}], "partial"),
    ("geography", "Q-geo-o1-0001", "C", "correct"),
    ("geography", "Q-geo-o1-0001", "A", "wrong"),
    ("geography", "Q-geo-o2-0001", "A,B,C,D", "correct"),
    ("geography", "Q-geo-o2-0001", "A,D", "partial"),
    ("geography", "Q-geo-o3-0001", "对", "correct"),
    ("geography", "Q-geo-o3-0001", "错", "wrong"),
    ("geography", "Q-geo-o4-0001",
     [{"left": "广东省", "right": "粤"},
      {"left": "山东省", "right": "鲁"},
      {"left": "四川省", "right": "川（蜀）"},
      {"left": "湖北省", "right": "鄂"}], "correct"),
    ("geography", "Q-geo-o4-0001", [{"left": "广东省", "right": "粤"}, {"left": "山东省", "right": "粤"}], "partial"),
]
ok = 0
qmap = {}
for subj, items in banks.items():
    for q in items:
        qmap[q["id"]] = q
for bank, qid, ans, exp in demos:
    r = rule_grader.grade(qmap[qid], ans)
    tag = "PASS" if r["result"] == exp else "FAIL"
    if r["result"] == exp: ok += 1
    print(f"  [{tag}] {qid} {str(ans)[:32]!r} -> {r['result']}")
print(f"4. O 系列规则判题: {ok}/{len(demos)} 通过")

# ---------- 5. 注册表路由验证 ----------
print("\n5. 学科注册表路由验证:")
for subj, expected_card in (("history", "event_card"), ("geography", "region_card")):
    s = next(x for x in reg["subjects"] if x["subject"] == subj)
    issues = []
    if s["gradingPreference"] != "exact": issues.append(f"gradingPreference={s['gradingPreference']}")
    if s["knowledgeCardTemplate"] != expected_card: issues.append(f"cardTemplate={s['knowledgeCardTemplate']}")
    used = {q["type"] for q in banks[subj]}
    missing = used - set(s["supportedTypes"])
    if missing: issues.append(f"题库题型 {missing} 未注册")
    tag = "PASS" if not issues else f"FAIL {issues}"
    print(f"  [{tag}] {subj}: supportedTypes={s['supportedTypes']}, grading={s['gradingPreference']}, card={s['knowledgeCardTemplate']}")
    for qtype in ("R1", "R2", "O1", "O2", "O3", "O4"):
        print(f"     {qtype} -> prompt={s['promptOverrides'].get(qtype, '通用判题(未覆盖)')}")

print(f"\n===== 总结: 总问题={total_problems}, 判题 {ok}/{len(demos)} 通过 =====")
sys.exit(1 if (total_problems or ok < len(demos)) else 0)
