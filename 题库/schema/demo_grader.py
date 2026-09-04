#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""客观题示例判题演示：加载示例数据，用 rule_grader 判题"""
import json
import sys

sys.path.insert(0, r"F:\LoongBa_Git\XiaoShuTong\题库\schema")
import rule_grader

items = json.load(open(r"F:\LoongBa_Git\XiaoShuTong\题库\examples\客观题示例.json", encoding="utf-8"))
qmap = {q["id"]: q for q in items}

demos = [
    ("Q-his-o1-0001", "B"),
    ("Q-his-o1-0001", "C"),
    ("Q-his-o2-0001", "A,B"),
    ("Q-his-o2-0001", "A"),
    ("Q-geo-o3-0001", "对"),
    ("Q-geo-o3-0001", "×"),
    ("Q-his-o4-0001", [{"left": "李白", "right": "唐"}, {"left": "苏轼", "right": "宋"},
                       {"left": "岳飞", "right": "宋"}, {"left": "诸葛亮", "right": "三国"}]),
    ("Q-his-o4-0001", [{"left": "李白", "right": "宋"}]),
    ("Q-bio-o5-0001", "细胞"),
    ("Q-che-o5-0001", "Fe"),
]

print("=== 示例数据判题演示（rule_grader）===")
for qid, ans in demos:
    r = rule_grader.grade(qmap[qid], ans)
    qtype = qmap[qid]["type"]
    print(f"{qid} [{qtype}] user={str(ans)[:28]!r} -> {r['result']} conf={r['confidence']}")
