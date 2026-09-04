#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
小书童 统一判题路由（P3）：按 judging_strategy 聚合调度 规则/相似度/AI 三引擎
架构（对齐《题型与交互设计规范 V0.1》§二.2 + ADR-003 三层路由）：

  题目 → 解析 judging_strategy → 路由到子引擎
  ├─ exact      → rule_grader      (O1-O4 客观题)
  ├─ similarity → similarity_grader(R 系列背诵, voice 容错)
  ├─ ai_guided  → ai_grader        (X1/X2 开放问答, 引导式反馈)
  └─ hybrid     → similarity 初筛 → AI 兜底 (R 系列 + O5, 最佳成本/准确率)

judging_strategy 解析优先级（题目级 > 题型级 > 学科级 > 全局默认）：
  content.judging_strategy > 题型注册表 judging_strategy > 学科注册表 defaultJudgingStrategy > 按 type 推导

用法：
  import grader
  grader.set_llm_client(my_fn)          # 注入 LLM（可选）
  r = grader.grade(question, user_answer, voice=True)
  python grader.py                        # 运行测试矩阵
"""

import json
import os

import rule_grader
import similarity_grader
import ai_grader


# ---------- 判题策略解析 ----------
# 按 type 推导的兜底策略（无注册表时的全局默认）
_TYPE_FALLBACK = {
    "R1": "hybrid", "R2": "hybrid", "R3a": "hybrid", "R3b": "hybrid",
    "O1": "exact", "O2": "exact", "O3": "exact", "O4": "exact",
    "O5": "hybrid",
    "X1": "ai_guided", "X2": "ai_guided", "X3": "exact",
    "R4": "display_only",
}
# 客观题（走 rule_grader）
_RULE_TYPES = ("O1", "O2", "O3", "O4")
# 背诵题（走 similarity_grader）
_SIM_TYPES = ("R1", "R2", "R3a", "R3b")
# AI 开放题（走 ai_grader）
_AI_TYPES = ("X1", "X2")


# 可注入的注册表缓存（由上层装配；缺省按 type 推导）
_type_registry: dict = {}   # {type: {judging_strategy: str, ...}}
_subject_registry: dict = None  # {subject: {defaultJudgingStrategy: str, ...}}


def load_registry_json(path: str) -> dict:
    """加载 JSON 注册表文件。"""
    if not os.path.exists(path):
        return {}
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def set_registries(types_path: str = None, subjects_path: str = None):
    """装载题型/学科注册表（数据驱动路由起点）。"""
    global _type_registry, _subject_registry
    if types_path:
        data = load_registry_json(types_path)
        _type_registry = {t["key"]: t for t in data.get("types", [])}
    if subjects_path:
        data = load_registry_json(subjects_path)
        _subject_registry = {s["subject"]: s for s in data.get("subjects", [])}


def resolve_strategy(question: dict) -> str:
    """
    解析判题策略。优先级：题目级 > 题型级 > 学科级 > 全局 type 推导。
    """
    # 1. 题目级（content.judging_strategy）
    js = (question.get("content") or {}).get("judging_strategy")
    if js:
        return js

    qtype = question.get("type", "")
    subject = question.get("subject", "")

    # 2. 题型级（R4 display_only 特判：注册表 grading=display_only 时跳过策略字段）
    if qtype in _type_registry:
        t = _type_registry[qtype]
        if t.get("grading") == "display_only":
            return "display_only"
        v = t.get("judging_strategy")
        if v:
            return v

    # 3. 学科级
    if _subject_registry:
        sub = _subject_registry.get(subject)
        if sub:
            v = sub.get("defaultJudgingStrategy")
            if v:
                return v

    # 4. 全局兜底
    return _TYPE_FALLBACK.get(qtype, "hybrid")


def resolve_interaction(question: dict) -> str:
    """解析交互类型（供前端可用；本题级覆盖优先）。"""
    ic = (question.get("content") or {}).get("interaction_type")
    if ic:
        return ic
    qtype = question.get("type", "")
    if qtype in _type_registry:
        v = _type_registry[qtype].get("interaction_type")
        if v:
            return v
    return "text"


# ---------- hybrid 判题（similarity 初筛 → AI 兜底） ----------
def _grade_hybrid(question: dict, user_answer, voice: bool, hint_level: str, **kw) -> dict:
    """成本最优路径：本地相似度初筛，仅 wrong 时才调 LLM。"""
    qtype = question.get("type", "")
    # 初筛：R 系列走 similarity_grader（含分段对齐）；O5 走 keyword ratio
    if qtype in _SIM_TYPES:
        r = similarity_grader.grade(question, user_answer, voice=voice)
    else:
        r = rule_grader.grade(question, user_answer)
        if r["result"] == "wrong":
            # O5 补充相似度维度（keyword 命中率一次未过）
            content = question.get("content", {})
            u = user_answer
            std = content.get("answer", "")
            csim = similarity_grader.char_similarity(
                similarity_grader.clean_sentence(u),
                similarity_grader.clean_sentence(std)) if std else 0.0
            if csim >= 0.85:
                r = {"result": "correct", "confidence": round(csim, 3),
                     "matched_keywords": r["matched_keywords"], "missing_keywords": [],
                     "hint": ""}
            elif csim >= 0.6:
                r = {"result": "partial", "confidence": round(csim, 3),
                     "matched_keywords": r["matched_keywords"],
                     "missing_keywords": r["missing_keywords"],
                     "hint": ""}
    # 初筛非 wrong → 直接返回（零 LLM 成本）
    if r["result"] != "wrong":
        r["grade_degraded"] = False
        return r
    # 初筛 wrong → AI 兜底
    return ai_grader.grade(question, user_answer, hint_level=hint_level, voice=voice, **kw)


# ---------- 统一入口 ----------
def grade(question: dict, user_answer, voice: bool = False,
          hint_level: str = "none", **kw) -> dict:
    """
    统一判题入口。按 judging_strategy 路由：
      exact  → rule_grader      （零成本，客观题）
      similarity → similarity_grader（背诵，voice 容错）
      ai_guided → ai_grader     （开放问答，引导式反馈）
      hybrid → 本地初筛 + AI 兜底（成本/准确率平衡）
      display_only → 直接返回 correct 占位（R4 卡片不判题）
    """
    strategy = resolve_strategy(question)
    qtype = question.get("type", "")

    if strategy == "exact":
        r = rule_grader.grade(question, user_answer, **kw)
        r.setdefault("grade_degraded", False)
        r.setdefault("strategy", "exact")
        return r

    if strategy == "similarity":
        r = similarity_grader.grade(question, user_answer, voice=voice)
        r.setdefault("grade_degraded", False)
        r["strategy"] = "similarity"
        return r

    if strategy == "ai_guided":
        r = ai_grader.grade(question, user_answer, hint_level=hint_level,
                            voice=voice, **kw)
        r["strategy"] = "ai_guided"
        return r

    if strategy == "hybrid":
        r = _grade_hybrid(question, user_answer, voice, hint_level, **kw)
        r["strategy"] = "hybrid"
        return r

    if strategy == "display_only":
        return {"result": "correct", "confidence": 1.0,
                "matched_keywords": [], "missing_keywords": [],
                "hint": "", "grade_degraded": False, "strategy": "display_only"}

    # 未知策略 → hybrid 兜底
    r = _grade_hybrid(question, user_answer, voice, hint_level, **kw)
    r["strategy"] = strategy
    return r


def set_llm_client(fn):
    """透传 LLM 客户端注入到 ai_grader。"""
    ai_grader.set_llm_client(fn)


# ================== 测试矩阵 ==================

def _mock_llm(payload, timeout_ms):
    return json.dumps({
        "result": "partial", "confidence": 0.72,
        "matched_keywords": ["统一文字"], "missing_keywords": ["统一货币"],
        "hint": "提示：还有'货币'方面",
    }, ensure_ascii=False)


def _run_tests():
    # 装配注册表（测试用真实文件）
    base = os.path.dirname(os.path.abspath(__file__))
    set_registries(os.path.join(base, "题型注册表.json"),
                   os.path.join(base, "学科注册表.json"))
    set_llm_client(_mock_llm)

    # 1. 策略解析：题目级覆盖
    q = {"id": "Q-ch-r1-0001", "subject": "chinese", "type": "R1",
         "content": {"question": "?", "answer": "床前明月光",
                     "keywords": [{"aliases": ["床前明月光"], "weight": 1, "required": False}]}}
    assert resolve_strategy(q) == "hybrid", resolve_strategy(q)  # 题型级 R1 → hybrid

    q2 = {"id": "Q-ch-r1-0002", "subject": "chinese", "type": "R1",
          "content": {"question": "?", "answer": "床前明月光", "judging_strategy": "exact"}}
    assert resolve_strategy(q2) == "exact", resolve_strategy(q2)  # 题目级优先

    qx = {"id": "Q-history-x1-0001", "subject": "history", "type": "X1",
          "content": {"question": "秦始皇统一六国的意义？", "answer": "统一文字货币度量衡",
                      "keywords": []}}
    assert resolve_strategy(qx) == "ai_guided", resolve_strategy(qx)  # 题型级 X1 → ai_guided

    # 2. exact 路由 → rule_grader
    qo = {"id": "Q-his-o1-0001", "subject": "history", "type": "O1",
          "content": {"options": ["A", "B"], "correct_option": "A"}}
    r = grade(qo, "A")
    assert r["result"] == "correct" and r["strategy"] == "exact", r

    # 3. similarity 路由（voice 容错）
    r = grade(q, "床前明月光", voice=True)
    assert r["result"] == "correct" and r["strategy"] == "hybrid", r  # R1 题型级 hybrid

    # 强制 similarity（题目级覆盖）
    q3 = {"id": "Q-ch-r1-0003", "subject": "chinese", "type": "R1",
          "content": {"question": "?", "answer": "床前明月光", "judging_strategy": "similarity",
                      "keywords": [{"aliases": ["床前明月光"], "weight": 1, "required": False}]}}
    r = grade(q3, "嗯那个床床床前明月光", voice=True)
    assert r["result"] == "correct" and r["strategy"] == "similarity", r

    # 4. ai_guided 路由 → AI 判题（引导式反馈）
    r = grade(qx, "统一了文字")
    assert r["result"] == "partial" and r["strategy"] == "ai_guided", r
    assert r["hint"], "AI 判题应带 hint 引导"

    # 5. hybrid：初筛 correct → 不调 LLM（零成本）
    set_llm_client(lambda p, t: (_ for _ in ()).throw(AssertionError("hybrid 正确时不应调 LLM")))
    r = grade(q, "床前明月光")
    assert r["result"] == "correct" and not r.get("bypass_llm"), r
    set_llm_client(_mock_llm)

    # 6. hybrid：初筛 wrong → AI 兜底
    q5 = {"id": "Q-his-r2-0001", "subject": "history", "type": "R2",
          "content": {"question": "?", "answer": "北京人使用打制石器",
                      "keywords": [{"aliases": ["打制石器"], "weight": 1, "required": False}]}}
    r = grade(q5, "北京人吃熟食")
    assert r["result"] in ("wrong", "partial"), r  # AI 兜底结果

    # 7. display_only（R4 卡片）
    q4 = {"id": "Q-ch-r4-0001", "subject": "chinese", "type": "R4",
          "content": {"question": "卡片标题", "answer": "卡片正文"}}
    r = grade(q4, "")
    assert r["result"] == "correct" and r["strategy"] == "display_only", r

    # 8. 放弃作答
    r = grade(qx, "不知道")
    assert r["result"] == "wrong" and r["hint"], r

    print("grader 统一路由测试矩阵: 全部通过 ✅")
    return 0


if __name__ == "__main__":
    import sys
    sys.exit(_run_tests())