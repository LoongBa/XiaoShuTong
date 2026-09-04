#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
小书童 统一判题路由演示（P3）：覆盖 exact / similarity / ai_guided / hybrid 全策略
- 装载 题型/学科 注册表（数据驱动路由）
- 使用统一入口 grader.grade() 判题
- LLM 用 mock（无真实 API 也能演示降级与引导式反馈）
- 展示《题型与交互设计规范 V0.1》判题策略 × 交互类型解耦

用法：
  python demo_grader.py
"""

import json
import os
import sys

BASE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, BASE)

import grader
import similarity_grader
import ai_grader
import rule_grader


def _mock_llm(payload, timeout_ms):
    """Mock LLM：基于标准答案做语义判题（演示引导式反馈）。
    - 宽松匹配：关键词提取核心双字（如"统一文字"→"文字"），命中即算。
    """
    std = payload.get("standard_answer", "")
    user = payload.get("user_answer", "")
    # 放弃作答 → wrong
    if not user or user in ("不知道", "不会", "跳过"):
        return json.dumps({"result": "wrong", "confidence": 0.1,
                          "matched_keywords": [], "missing_keywords": [],
                          "hint": "提示：可以点击'请提示我'获取线索"}, ensure_ascii=False)

    def cores(k):
        """关键词核心：取去掉动词/功能词后的 2-4 字片段。"""
        for drop in ("统一", "建立", "实行"):
            if k.startswith(drop):
                k = k[len(drop):]
        return k

    kw_cores = [cores(k) for k in payload.get("keywords", [])]
    # 全匹配 → correct（核心词全部覆盖）
    if kw_cores and all(c in user for c in kw_cores if c):
        return json.dumps({"result": "correct", "confidence": 0.95,
                          "matched_keywords": payload.get("keywords", []),
                          "missing_keywords": [], "hint": ""}, ensure_ascii=False)
    # 部分匹配 → partial + 引导
    matched = [k for k, c in zip(payload.get("keywords", []), kw_cores) if c and c in user]
    missing = [k for k, c in zip(payload.get("keywords", []), kw_cores) if c and c not in user]
    if matched:
        return json.dumps({"result": "partial", "confidence": 0.7,
                          "matched_keywords": matched, "missing_keywords": missing,
                          "hint": f"提示：还缺{'、'.join(missing[:2])}"}, ensure_ascii=False)
    # 语义相似（关键词无命中但有相关内容词）
    if std and any(k in user for k in ("制度", "度量", "文字")) and "策略" not in user:
        return json.dumps({"result": "partial", "confidence": 0.6,
                          "matched_keywords": [], "missing_keywords": [],
                          "hint": "提示：意思接近，注意关键术语"}, ensure_ascii=False)
    return json.dumps({"result": "wrong", "confidence": 0.3,
                      "matched_keywords": [], "missing_keywords": [],
                      "hint": "提示：再回忆一下关键点"}, ensure_ascii=False)


def _sep(title):
    print(f"\n{'=' * 62}\n{title}\n{'=' * 62}")


def main():
    # 1. 装配注册表 + 注入 mock LLM
    grader.set_registries(
        os.path.join(BASE, "题型注册表.json"),
        os.path.join(BASE, "学科注册表.json"),
    )
    grader.set_llm_client(_mock_llm)

    print("小书童 统一判题路由演示")
    print(f"判题策略解析: 题目级 > 题型级 > 学科级 > 全局  | 注册表已装载")

    # ── 2. exact：O1 单选（规则判题，零 LLM）──
    _sep("1. exact 策略 — O1 单选（rule_grader，零 LLM 成本）")
    q_hist = {"id": "Q-his-o1-0001", "subject": "history", "type": "O1",
              "content": {"question": "玄武门之变后，哪位皇子即位为唐太宗？",
                          "options": ["A. 李建成", "B. 李世民", "C. 李元吉", "D. 李渊"],
                          "correct_option": "B"}}
    for ans in ("B", "C"):
        r = grader.grade(q_hist, ans)
        print(f"  用户选 {ans!r:6} → {r['result']:8} conf={r['confidence']} [{r['strategy']}]")

    # ── 3. exact：O4 连线（规则判题，部分给分）──
    _sep("2. exact 策略 — O4 连线（rule_grader，部分给分）")
    q_link = {"id": "Q-his-o4-0001", "subject": "history", "type": "O4",
              "content": {"question": "请将下列历史人物与对应朝代连线。",
                          "pairs": [{"left": "李白", "right": "唐"}, {"left": "苏轼", "right": "宋"},
                                    {"left": "岳飞", "right": "宋"}, {"left": "诸葛亮", "right": "三国"}]}}
    for ans in (
        [{"left": "李白", "right": "唐"}, {"left": "苏轼", "right": "宋"},
         {"left": "岳飞", "right": "宋"}, {"left": "诸葛亮", "right": "三国"}],
        [{"left": "李白", "right": "唐"}, {"left": "苏轼", "right": "宋"}],
    ):
        r = grader.grade(q_link, ans)
        print(f"  连对 {len(ans)} 对 → {r['result']:8} conf={r['confidence']} [{r['strategy']}]")

    # ── 4. hybrid：R1 补全记忆（similarity 初筛 + AI 兜底）──
    _sep("3. hybrid 策略 — R1 补全记忆（similarity 初筛，正确时零 LLM）")
    q_r1 = {"id": "Q-ch-r1-0001", "subject": "chinese", "type": "R1",
            "content": {"question": "床前明月光，疑是地上霜。的上一句是？",
                        "answer": "床前明月光，疑是地上霜",
                        "keywords": [{"aliases": ["床前明月光"], "weight": 1, "required": False},
                                     {"aliases": ["疑是地上霜"], "weight": 1, "required": False}]}}
    for ans, voice in (("床前明月光，疑是地上霜", False), ("床前明月光", False)):
        r = grader.grade(q_r1, ans, voice=voice)
        print(f"  用户: {ans!r:28} → {r['result']:8} conf={r['confidence']} [{r['strategy']}] "
              f"{'✈️LLM' if r.get('bypass_llm') else ''}")

    # ── 5. similarity：R1 语音输入（容错：口癖/结巴）──
    _sep("4. similarity 策略 — R1 语音背诵（voice 容错：口癖/结巴）")
    q_sim = {"id": "Q-ch-r1-0002", "subject": "chinese", "type": "R1",
             "content": {"question": "背诵《静夜思》前两句",
                         "answer": "床前明月光，疑是地上霜",
                         "judging_strategy": "similarity",  # 题目级覆盖
                         "interaction_type": "voice",
                         "keywords": [{"aliases": ["床前明月光"], "weight": 1, "required": False},
                                      {"aliases": ["疑是地上霜"], "weight": 1, "required": False}]}}
    for ans in ("嗯那个床床床前明月光啊，然后疑是地上霜吧",
                "床前明月光，疑是地上霜",
                "床前明月光"):
        clean = similarity_grader.voice_normalize(ans)
        r = grader.grade(q_sim, ans, voice=True)
        print(f"  语音: {ans[:20]!r:24} → 规范化: {clean[:14]!r:16} → {r['result']:8} "
              f"conf={r['confidence']} [{r['strategy']}]")

    # ── 6. hybrid：R3a 段落默写（逐句对齐）──
    _sep("5. hybrid 策略 — R3a 段落默写（逐句顺序对齐）")
    q_r3 = {"id": "Q-ch-r3a-0001", "subject": "chinese", "type": "R3a",
            "content": {"question": "默写《登鹳雀楼》",
                        "answer": "白日依山尽，黄河入海流。欲穷千里目，更上一层楼。",
                        "keywords": [{"aliases": ["白日依山尽", "黄河入海流", "欲穷千里目", "更上一层楼"],
                                      "weight": 1, "required": False}]}}
    for ans in ("白日依山尽，黄河入海流。欲穷千里目，更上一层楼。",
                "白日依山尽，黄河入海流。欲穷千里目",
                "黄河入海流，白日依山尽。更上一层楼，欲穷千里目。"):
        r = grader.grade(q_r3, ans)
        print(f"  用户: {ans[:24]!r:28} → {r['result']:8} conf={r['confidence']} [{r['strategy']}]")

    # ── 7. ai_guided：X1 历史简答（LLM + 引导式反馈）──
    _sep("6. ai_guided 策略 — X1 历史简答（LLM 引导式反馈，核心差异化）")
    q_x = {"id": "Q-his-x1-0001", "subject": "history", "type": "X1",
           "content": {"question": "秦始皇统一六国的历史意义是什么？",
                       "answer": "统一文字、货币、度量衡，建立中央集权制度",
                       "keywords": ["统一文字", "统一货币", "度量衡", "中央集权"]}}
    for ans in ("统一了文字和货币", "建立了中央集权制度", "不知道"):
        r = grader.grade(q_x, ans)
        extra = f"  hint: {r['hint']}" if r.get("hint") else ""
        print(f"  用户: {ans!r:18} → {r['result']:8} conf={r['confidence']} [{r['strategy']}]{extra}")

    # ── 8. 交互类型解析演示 ──
    _sep("7. 交互类型解析（interaction_type 数据驱动）")
    for q in (q_r1, q_sim, q_hist, q_partial_demo := {"id": "Q-x", "subject": "chinese", "type": "R3b",
                                                      "content": {"question": "整篇默写"}}):
        it = grader.resolve_interaction(q)
        js = grader.resolve_strategy(q)
        print(f"  {q['type']:3} → interaction={it:8} judging={js:10}  ({q.get('subject','')})")

    print("\n演示完成 ✅")


if __name__ == "__main__":
    main()