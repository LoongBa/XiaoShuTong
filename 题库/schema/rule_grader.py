#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
小书童 规则判题引擎（P2）：O1-O5 客观题规则比对 + 测试矩阵
- O1 单选：exact 比对
- O2 多选：set_exact + 允许部分
- O3 判断：exact + 变体规范化
- O4 连线：pairs 逐对 + 部分
- O5 填空：keyword 同义词组命中率（复用 D01 阈值）
输出对齐 D01 判题契约：{result, confidence, matched_keywords, missing_keywords, hint}
用法：
  import rule_grader
  grader.grade(question_dict, user_answer)  # question_dict 含 type/content
  python rule_grader.py                       # 运行测试矩阵
"""

import re

# 阈值（D01 6.3 / ADR-003）
KEYWORD_CORRECT = 0.85
KEYWORD_PARTIAL = 0.60


def _norm(s: str) -> str:
    """规范化：去首尾空白/全半角统一"""
    if s is None:
        return ""
    s = str(s).strip()
    s = s.replace("（", "(").replace("）", ")").replace("，", ",").replace("；", ";")
    return s


def _split_set(s: str):
    """将 "A,B" 拆为规范化集合"""
    if isinstance(s, list):
        return {_norm(x).upper() for x in s if _norm(x)}
    return {_norm(x).upper() for x in _norm(s).split(",") if _norm(x)}


def grade_o1(content: dict, user_answer) -> dict:
    """单选：exact"""
    correct = _norm(content.get("correct_option", "")).upper()
    user = _norm(user_answer).upper()
    ok = user == correct
    return {
        "result": "correct" if ok else "wrong",
        "confidence": 1.0 if ok else 1.0,
        "matched_keywords": [correct] if ok else [],
        "missing_keywords": [] if ok else [correct],
        "hint": "",
    }


def grade_o2(content: dict, user_answer, allow_partial=True) -> dict:
    """多选：set_exact，缺选/多选部分给分"""
    correct_set = _split_set(content.get("correct_option", ""))
    user_set = _split_set(user_answer)
    if correct_set == user_set:
        return {"result": "correct", "confidence": 1.0,
                "matched_keywords": sorted(correct_set), "missing_keywords": [], "hint": ""}
    inter = correct_set & user_set
    if not inter:
        return {"result": "wrong", "confidence": 1.0,
                "matched_keywords": [], "missing_keywords": sorted(correct_set), "hint": ""}
    if allow_partial:
        conf = len(inter) / len(correct_set) if correct_set else 0
        missing = sorted(correct_set - user_set)
        extra = sorted(user_set - correct_set)
        return {"result": "partial", "confidence": round(conf, 3),
                "matched_keywords": sorted(inter),
                "missing_keywords": missing + [f"多选:{e}" for e in extra],
                "hint": ""}
    return {"result": "wrong", "confidence": 1.0,
            "matched_keywords": [], "missing_keywords": sorted(correct_set), "hint": ""}


_JUDGE_MAP = {"对": "对", "正确": "对", "√": "对", "✓": "对", "true": "对",
              "错": "错", "错误": "错", "×": "错", "✗": "错", "false": "错"}


def grade_o3(content: dict, user_answer) -> dict:
    """判断：变体规范化"""
    correct = _JUDGE_MAP.get(_norm(content.get("correct_option", "")).lower(), _norm(content.get("correct_option", "")))
    user = _JUDGE_MAP.get(_norm(user_answer).lower(), _norm(user_answer))
    ok = user == correct
    return {
        "result": "correct" if ok else "wrong",
        "confidence": 1.0,
        "matched_keywords": [correct] if ok else [],
        "missing_keywords": [] if ok else [correct],
        "hint": "",
    }


def grade_o4(content: dict, user_answer) -> dict:
    """连线：pairs 逐对 + 部分给分"""
    pairs = content.get("pairs", [])
    if not pairs:
        return {"result": "wrong", "confidence": 0, "matched_keywords": [], "missing_keywords": [], "hint": ""}
    # 标准映射：left -> right
    std = {_norm(p.get("left", "")): _norm(p.get("right", "")) for p in pairs}
    # 用户答案：[{left,right}] 或 {left:right}
    user_map = {}
    if isinstance(user_answer, dict):
        for k, v in user_answer.items():
            user_map[_norm(k)] = _norm(v)
    elif isinstance(user_answer, list):
        for item in user_answer:
            if isinstance(item, dict):
                user_map[_norm(item.get("left", ""))] = _norm(item.get("right", ""))
    else:
        # 字符串 "左1=右1;左2=右2"
        for seg in _norm(user_answer).split(";"):
            if "=" in seg:
                l, r = seg.split("=", 1)
                user_map[_norm(l)] = _norm(r)
    hit = [l for l, r in std.items() if user_map.get(l) == r]
    total = len(std)
    hit_n = len(hit)
    if hit_n == total:
        return {"result": "correct", "confidence": 1.0,
                "matched_keywords": sorted(std.keys()), "missing_keywords": [], "hint": ""}
    if hit_n == 0:
        return {"result": "wrong", "confidence": 1.0,
                "matched_keywords": [], "missing_keywords": sorted(std.keys()), "hint": ""}
    conf = round(hit_n / total, 3)
    missing = sorted(set(std.keys()) - set(hit))
    return {"result": "partial", "confidence": conf,
            "matched_keywords": sorted(hit), "missing_keywords": missing, "hint": ""}


def _keyword_hit_ratio(content: dict, user_answer: str) -> tuple:
    """O5 填空：keywords 同义词组命中率 → (命中率, matched, missing, required_miss)"""
    keywords = content.get("keywords", [])
    user = _norm(user_answer)
    total_w = 0.0
    hit_w = 0.0
    matched = []
    missing = []
    required_miss = False
    for kw in keywords:
        if isinstance(kw, str):
            aliases, weight, required = [kw], 1.0, False
        else:
            aliases = kw.get("aliases", [])
            weight = float(kw.get("weight", 1.0))
            required = bool(kw.get("required", False))
        total_w += weight
        hit = any(alias in user for alias in aliases if alias)
        if hit:
            hit_w += weight
            matched.append(aliases[0])
        else:
            if required:
                required_miss = True
            missing.append(aliases[0])
    ratio = hit_w / total_w if total_w else 0
    return ratio, matched, missing, required_miss


def grade_o5(content: dict, user_answer) -> dict:
    """填空：keyword 命中率"""
    ratio, matched, missing, req_miss = _keyword_hit_ratio(content, user_answer)
    if ratio >= KEYWORD_CORRECT:
        if req_miss:
            return {"result": "partial", "confidence": round(ratio, 3),
                    "matched_keywords": matched, "missing_keywords": missing, "hint": ""}
        return {"result": "correct", "confidence": round(ratio, 3),
                "matched_keywords": matched, "missing_keywords": missing, "hint": ""}
    if ratio >= KEYWORD_PARTIAL:
        return {"result": "partial", "confidence": round(ratio, 3),
                "matched_keywords": matched, "missing_keywords": missing, "hint": ""}
    return {"result": "wrong", "confidence": round(ratio, 3),
            "matched_keywords": matched, "missing_keywords": missing, "hint": ""}


_GRADERS = {"O1": grade_o1, "O2": grade_o2, "O3": grade_o3, "O4": grade_o4, "O5": grade_o5}


def grade(question: dict, user_answer, **kw) -> dict:
    """统一判题入口：按 question.type 路由"""
    qtype = question.get("type", "")
    grader = _GRADERS.get(qtype)
    if not grader:
        return {"result": "wrong", "confidence": 0, "matched_keywords": [],
                "missing_keywords": [], "hint": f"题型 {qtype} 无规则判题器（走 LLM/hybrid）"}
    return grader(question.get("content", {}), user_answer, **kw)


# ================== 测试矩阵 ==================

def _run_tests():
    cases = [
        # (题型, 题目content, 用户答案, 期望result, 期望confidence区间)
        ("O1", {"options": ["A:甲", "B:乙"], "correct_option": "A"}, "A", "correct", None),
        ("O1", {"options": ["A:甲", "B:乙"], "correct_option": "A"}, "B", "wrong", None),
        ("O2", {"options": ["A", "B", "C"], "correct_option": "A,B"}, "A,B", "correct", None),
        ("O2", {"options": ["A", "B", "C"], "correct_option": "A,B"}, "A", "partial", None),
        ("O2", {"options": ["A", "B", "C"], "correct_option": "A,B"}, "C", "wrong", None),
        ("O3", {"correct_option": "对"}, "对", "correct", None),
        ("O3", {"correct_option": "对"}, "√", "correct", None),
        ("O3", {"correct_option": "对"}, "错", "wrong", None),
        ("O4", {"pairs": [{"left": "李白", "right": "唐"}, {"left": "苏轼", "right": "宋"}, {"left": "杜甫", "right": "唐"}]},
         [{"left": "李白", "right": "唐"}, {"left": "苏轼", "right": "宋"}, {"left": "杜甫", "right": "唐"}], "correct", None),
        ("O4", {"pairs": [{"left": "李白", "right": "唐"}, {"left": "苏轼", "right": "宋"}, {"left": "杜甫", "right": "唐"}]},
         [{"left": "李白", "right": "唐"}, {"left": "苏轼", "right": "宋"}], "partial", None),
        ("O4", {"pairs": [{"left": "李白", "right": "唐"}, {"left": "苏轼", "right": "宋"}]},
         [{"left": "李白", "right": "宋"}], "wrong", None),
        ("O5", {"answer": "床前明月光", "keywords": [{"aliases": ["床前明月光"], "weight": 2, "required": True}]},
         "床前明月光", "correct", None),
        ("O5", {"answer": "床前明月光", "keywords": [{"aliases": ["床前明月光"], "weight": 2, "required": True},
                                                    {"aliases": ["疑是地上霜"], "weight": 1, "required": False}]},
         "床前明月光", "partial", None),  # required 命中但缺一联 → 66%
        ("O5", {"answer": "A", "keywords": [{"aliases": ["A"], "weight": 1, "required": False}]},
         "B", "wrong", None),
        ("O5", {"answer": "A", "keywords": [{"aliases": ["A"], "weight": 2, "required": True}]},
         "B", "wrong", None),
    ]
    passed = 0
    failed = []
    for idx, (qtype, content, user, exp_result, _) in enumerate(cases, 1):
        q = {"type": qtype, "content": content}
        r = grade(q, user)
        tag = "✅" if r["result"] == exp_result else "❌"
        if r["result"] == exp_result:
            passed += 1
        else:
            failed.append((idx, qtype, exp_result, r["result"], user))
        print(f"{tag} T{idx:02d} [{qtype}] user={user!r} → {r['result']} conf={r['confidence']}")
    print(f"\n测试矩阵: {passed}/{len(cases)} 通过")
    if failed:
        for f in failed:
            print(f"  FAIL T{f[0]:02d} {f[1]}: 期望={f[2]} 实际={f[3]} user={f[4]!r}")
        return 1
    return 0


if __name__ == "__main__":
    import sys
    sys.exit(_run_tests())
