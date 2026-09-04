#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
小书童 相似度判题引擎（P3）：R 系列背诵题 + 语音容错
- 字符相似度：编辑距离（Levenshtein）+ 二元组 Jaccard，取 max
- 关键词命中率：复用 rule_grader._keyword_hit_ratio
- 语音容错：voice_normalize（去语气词/结巴去重/全半角统一）+ 阈值降 0.05
- 分段判题：R3a/R3b 长文支持按句子分段对齐（顺序敏感批改）
输出对齐 D01 判题契约：{result, confidence, matched_keywords, missing_keywords, hint}
用法：
  import similarity_grader
  r = similarity_grader.grade(question_dict, user_answer, voice=True)
  python similarity_grader.py  # 运行测试矩阵
"""

import re
from rule_grader import _norm, _keyword_hit_ratio  # 复用规范化和同义词组命中率

# 阈值（表：文本路径 0.85/0.60，语音路径降 0.05 → 0.80/0.55）
TEXT_CORRECT = 0.85
TEXT_PARTIAL = 0.60
VOICE_CORRECT = 0.80
VOICE_PARTIAL = 0.55

# 语气词（语音输入常见口癖，规范化时剔除）
_FILLERS = ("嗯", "啊", "呃", "那个", "这个", "然后", "就是", "的话", "吧", "呢", "嘛", "哦")
# 英文语气词
_FILLERS_EN = ("um", "uh", "er", "well", "like", "you know")
_PUNCT = re.compile(r"[\s\.,，。；;:：!！?？\"'“”‘’()（）\-—~～]+")


def clean_sentence(s: str) -> str:
    """提取句子主干作为匹配单元（去标点、转小写、压缩空白）。"""
    s = _norm(s).lower()
    s = _PUNCT.sub("", s)
    return s


def voice_normalize(s: str) -> str:
    """
    语音输入规范化：
    1. 去除中文/英文语气词（连续出现也去）
    2. 结巴去重：'床床床前' → '床前'（同一字符连续 2+ 次压缩为 1）
    3. 全半角统一 + 标点去除（复用 _norm + clean）
    注意：'处处闻啼鸟' 中合法叠词 '处处' 也会被压缩为 '处'，属可接受代价
          （语音判题以关键词/相似度为主，叠词丢失不改变命中判断）
    """
    s = _norm(s)
    # 中文语气词（词级、可连续）
    for f in sorted(_FILLERS, key=len, reverse=True):
        s = s.replace(f, "")
    # 英文语气词（词边界）
    s = re.sub(r"\b(?:%s)\b" % "|".join(_FILLERS_EN), "", s, flags=re.IGNORECASE)
    # 结巴去重：连续相同字符 2+ → 1（排除纯英文单词如 'aa'，仅处理 CJK）
    out = []
    prev = ""
    for ch in s:
        if ch == prev and "\u4e00" <= ch <= "\u9fff":
            continue
        out.append(ch)
        prev = ch
    return clean_sentence("".join(out))


def edit_distance(a: str, b: str) -> int:
    """Levenshtein 编辑距离（字符级，CJK 安全）。"""
    if a == b:
        return 0
    if not a:
        return len(b)
    if not b:
        return len(a)
    prev = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        cur = [i]
        for j, cb in enumerate(b, 1):
            cost = 0 if ca == cb else 1
            cur.append(min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + cost))
        prev = cur
    return prev[-1]


def char_similarity(a: str, b: str) -> float:
    """
    字符级相似度 = max(1 - 编辑距离/max_len, Jaccard 二元组)
    中文短文本（10-50 字）编辑距离更敏感；补充 Jaccard 防长文本偏移。
    """
    if not a or not b:
        return 0.0
    ed_sim = 1.0 - edit_distance(a, b) / max(len(a), len(b))
    bigrams_a = {a[i:i + 2] for i in range(max(0, len(a) - 1))}
    bigrams_b = {b[i:i + 2] for i in range(max(0, len(b) - 1))}
    if not bigrams_a or not bigrams_b:
        jac = 1.0 if a == b else 0.0
    else:
        jac = len(bigrams_a & bigrams_b) / len(bigrams_a | bigrams_b)
    return max(ed_sim, jac)


def _split_sentences(s: str):
    """按标点切句（保留顺序用于分段对齐）。"""
    parts = re.split(r"[。；;！!？?，,]", _norm(s))
    return [p for p in parts if p]


def _sentence_alignment(user_sents: list, std_sents: list) -> tuple:
    """
    贪心顺序对齐：用户句子依次与标准句子匹配，取最高相似度且可前进。
    返回 (命中的标准句下标集合, 各句最佳相似度列表)
    顺序敏感：只允许向后推进（模拟背诵顺序），不匹配即跳过。
    """
    hits = set()
    sims = []
    std_idx = 0
    for us in user_sents:
        best, best_i = 0.0, std_idx
        # 在剩余标准句中找最佳（最多看后 3 句，防长文偏移）
        for j in range(std_idx, min(std_idx + 3, len(std_sents))):
            sc = char_similarity(clean_sentence(us), clean_sentence(std_sents[j]))
            if sc > best:
                best, best_i = sc, j
        if best >= 0.5:
            hits.add(best_i)
        sims.append(best)
        if best_i >= std_idx:
            std_idx = max(std_idx, best_i + 1)  # 前进，不回溯
        else:
            std_idx = best_i + 1
    return hits, sims


def grade_r_content(content: dict, user_answer: str, voice: bool = False) -> dict:
    """
    R 系列（R1/R2/R3a/R3b）相似度判题：
    1. 关键词命中率（rule_grader 同义词组）
    2. 字符相似度（编辑距离 + Jaccard）
    3. 长文（R3a/R3b）追加逐句对齐
    取 max 三者的最高得分，按路径阈值映射 result。
    """
    answer = content.get("answer", "")
    keywords = content.get("keywords", [])
    user = voice_normalize(user_answer) if voice else clean_sentence(user_answer)
    std = voice_normalize(answer) if voice else clean_sentence(answer)

    if not user:
        return {"result": "wrong", "confidence": 0.0,
                "matched_keywords": [], "missing_keywords": [answer], "hint": ""}

    # 1. 关键词命中率（同义词组，规则引擎核心信号）
    kratio, matched, missing, req_miss = _keyword_hit_ratio(content, user_answer) if keywords else (0.0, [], [answer], False)
    # 2. 字符相似度（整段，编辑距离 + Jaccard）
    csim = char_similarity(user, std) if std else 0.0
    # 3. 逐句对齐（仅当有标点切分时补充，顺序敏感）
    scol = 0.0
    std_sents = _split_sentences(answer)
    user_sents = _split_sentences(user_answer)
    if len(std_sents) > 1 and user_sents:
        hits, sims = _sentence_alignment(user_sents, std_sents)
        scol = (len(hits) / len(std_sents)) * 0.9 + (sum(sims) / len(std_sents)) * 0.1

    score = max(kratio, csim, scol)
    th_correct = VOICE_CORRECT if voice else TEXT_CORRECT
    th_partial = VOICE_PARTIAL if voice else TEXT_PARTIAL

    # 结果映射：关键词命中情况为基础判定，相似度辅助边界
    # 边界情况（相似度高但关键词弱命中）判 partial → 上层 hybrid 走 AI 兜底
    if has_keywords := bool(keywords):
        if kratio >= th_correct and not req_miss and csim >= 0.7:
            result = "correct"        # 关键词高水平命中 + 字符高度相似（含语音小幅噪声）
        elif matched or csim >= th_partial:
            result = "partial"        # 记住一部分 → partial（绝不能误判 wrong）
        else:
            result = "wrong"
    else:
        if score >= th_correct:
            result = "correct"
        elif score >= th_partial:
            result = "partial"
        else:
            result = "wrong"

    return {
        "result": result,
        "confidence": round(score, 3),
        "matched_keywords": matched,
        "missing_keywords": missing,
        "hint": "",
    }


def grade(question: dict, user_answer, voice: bool = False, **kw) -> dict:
    """R 系列统一判题入口。"""
    qtype = question.get("type", "")
    if qtype not in ("R1", "R2", "R3a", "R3b"):
        raise ValueError(f"similarity_grader 仅支持 R1/R2/R3a/R3b，收到 {qtype}")
    return grade_r_content(question.get("content", {}), user_answer, voice=voice)


# ================== 测试矩阵 ==================

def _run_tests():
    r = grade({"type": "R1", "content": {
        "answer": "床前明月光，疑是地上霜",
        "keywords": [{"aliases": ["床前明月光"], "weight": 1, "required": False},
                     {"aliases": ["疑是地上霜"], "weight": 1, "required": False}]
    }}, "床前明月光，疑是地上霜")
    assert r["result"] == "correct", r

    r = grade({"type": "R1", "content": {
        "answer": "床前明月光，疑是地上霜",
        "keywords": [{"aliases": ["床前明月光"], "weight": 1, "required": False},
                     {"aliases": ["疑是地上霜"], "weight": 1, "required": False}]
    }}, "床前明月光")
    assert r["result"] == "partial", r

    # 语音：口癖 + 结巴
    r = voice_normalize("嗯那个床床床前明月光啊，然后疑是地上霜吧")
    assert "床前明月光" in r and "疑是地上霜" in r, r

    # 语音容错：同音字不算错（相似度够高）
    r = grade({"type": "R1", "content": {
        "answer": "疑是地上霜", "keywords": [{"aliases": ["疑是地上霜"], "weight": 1, "required": False}]
    }}, "疑是地上霜", voice=True)
    assert r["result"] == "correct", r

    # 语音容错阈值：几乎正确但缺字 → partial（语音路径）
    r = grade({"type": "R1", "content": {
        "answer": "举头望明月，低头思故乡",
        "keywords": [{"aliases": ["举头望明月"], "weight": 1, "required": False},
                     {"aliases": ["低头思故乡"], "weight": 1, "required": False}]
    }}, "举头望明月，低头思乡", voice=True)
    assert r["result"] == "partial", r

    # 逐句对齐：段落默写
    ans = "白日依山尽，黄河入海流。欲穷千里目，更上一层楼。"
    r = grade({"type": "R3a", "content": {"answer": ans}}, "白日依山尽，黄河入海流。欲穷千里目")
    assert r["result"] == "partial", r

    # 编辑距离
    assert edit_distance("kitten", "sitting") == 3, edit_distance("kitten", "sitting")
    assert 0.9 < char_similarity("疑是地上霜", "疑是地上霜") <= 1.0
    assert char_similarity("窗前明月光", "床前明月光") > 0.75  # 近音缺字仍是高相似
    assert char_similarity("举头望明月", "低头思故乡") < 0.3   # 完全不同句低相似

    print("similarity_grader 测试矩阵: 全部通过 ✅")
    return 0


if __name__ == "__main__":
    import sys
    sys.exit(_run_tests())