#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
小书童 AI 判题引擎（P3）：LLM 语义判题层 + Prompt 按级绑定 + 超时降级
- Prompt 绑定优先级：单题级 > 题型级 > 题库级 > 学科级 > 全局默认（对齐判题Prompt README §一.2）
- LLM 客户端可插拔：set_llm_client() 注入，测试用 mock，生产接真实 API
- 统一 JSON 契约：{result, confidence, matched_keywords, missing_keywords, hint}
- 降级路由：LLM 超时(>3s)/失败/解析错误 → 降级 similarity_grader + 标记 grade_degraded

用法：
  import ai_grader
  ai_grader.set_llm_client(my_async_fn)   # 可选，缺省用 stub（直接降级）
  r = ai_grader.grade(question, user_answer, hint_level="none")
  python ai_grader.py                      # 运行测试矩阵（stub 模式：验证降级链路）
"""

import json
import os
import time

from rule_grader import _norm, _keyword_hit_ratio

# ---------- 契约常量 ----------
RESULT_VALUES = ("correct", "partial", "wrong")
DEFAULT_LLM_TIMEOUT_MS = 3000
KEYWORD_CORRECT = 0.85   # 本地拦截阈值（对齐 ADR-003：≥85% 直接判 correct，不调 LLM）
KEYWORD_PARTIAL = 0.60

# ---------- Prompt 绑定 ----------
_PROMPT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "判题Prompt")
_DEFAULT_PROMPT = "通用判题-Prompt.md"

# 全局可注入：{question: prompt文件路径} 的单题覆盖（生产由上层装配）
_question_prompt_overrides: dict = {}


def set_question_prompt_override(question_id: str, prompt_file: str):
    """注册单题级 Prompt 覆盖（最高优先级）。"""
    _question_prompt_overrides[question_id] = prompt_file


def clear_question_prompt_overrides():
    _question_prompt_overrides.clear()


def resolve_prompt(question: dict, subject_registry: dict = None) -> str:
    """
    按绑定优先级解析 Prompt 内容（返回读取到的 Prompt 文本）。
    优先级：单题级 > 题型级 > 题库级 > 学科级 > 全局默认
    """
    qid = question.get("id", "")
    qtype = question.get("type", "")
    subject = question.get("subject", "")

    # 1. 单题级
    if qid in _question_prompt_overrides:
        return _load_prompt(_question_prompt_overrides[qid])

    # 2. 题型级（meta.prompt_override 或题型注册表）
    meta_prompt = (question.get("meta") or {}).get("prompt_override")
    if meta_prompt:
        return _load_prompt(meta_prompt)

    # 3. 学科级 promptOverrides[type]
    if subject_registry:
        for sub in subject_registry.get("subjects", []):
            if sub.get("subject") == subject:
                ov = sub.get("promptOverrides", {})
                if qtype in ov:
                    return _load_prompt(ov[qtype])

    # 4. 全局默认
    return _load_prompt(_DEFAULT_PROMPT)


def _load_prompt(name: str) -> str:
    """读取 Prompt 文件；缺省回落全局默认。"""
    if not name:
        name = _DEFAULT_PROMPT
    p = os.path.join(_PROMPT_DIR, name)
    if not os.path.exists(p):
        # 允许不带 .md 后缀
        p2 = os.path.join(_PROMPT_DIR, name if name.endswith(".md") else name + ".md")
        if not os.path.exists(p2):
            return ""
        p = p2
    with open(p, encoding="utf-8") as f:
        return f.read()


def build_llm_request(question: dict, user_answer, hint_level: str = "none", prompt: str = "") -> dict:
    """构造发给 LLM 的请求载荷（对齐 通用判题-Prompt.md 输入格式）。"""
    content = question.get("content", {})
    return {
        "prompt": prompt,
        "question": content.get("question", ""),
        "standard_answer": content.get("answer", ""),
        "keywords": content.get("keywords", []) or [],
        "user_answer": _norm(user_answer),
        "hint_level": hint_level,
    }


# ---------- LLM 客户端（可插拔） ----------
_llm_client = None  # 签名：async def llm_call(payload: dict, timeout_ms: int) -> str  (返回 JSON 字符串)


def set_llm_client(fn):
    """注入 LLM 调用函数。fn(payload, timeout_ms) -> str（LLM 原始 JSON 输出）。"""
    global _llm_client
    _llm_client = fn


class _StubLLM:
    """缺省 stub：直接抛错，强制走降级链路（生产必须先 set_llm_client）。"""

    def __call__(self, payload, timeout_ms):
        raise NotImplementedError("ai_grader 需要先 set_llm_client() 注入真实 LLM")


_llm_client = _StubLLM()


# ---------- 输出解析 ----------
def parse_llm_response(raw: str) -> dict:
    """解析 LLM JSON 输出；失败返回 None（触发降级）。"""
    if not raw:
        return None
    try:
        # 容错：剥离 ```json ``` 围栏
        t = raw.strip()
        if t.startswith("```"):
            t = t.split("```", 2)[1] if t.count("```") >= 2 else t.strip("`")
            t = t.strip()
        data = json.loads(t)
    except (json.JSONDecodeError, ValueError):
        # 尝试截取第一个 { ... } 块
        try:
            start, end = raw.find("{"), raw.rfind("}")
            if start == -1 or end <= start:
                return None
            data = json.loads(raw[start:end + 1])
        except (json.JSONDecodeError, ValueError):
            return None

    result = data.get("result")
    if result not in RESULT_VALUES:
        return None
    confidence = float(data.get("confidence", 0.5))
    confidence = max(0.0, min(1.0, confidence))
    return {
        "result": result,
        "confidence": round(confidence, 3),
        "matched_keywords": data.get("matched_keywords", []) or [],
        "missing_keywords": data.get("missing_keywords", []) or [],
        "hint": data.get("hint", "") or "",
    }


# ---------- 本地拦截 + AI 主流程 ----------
# "放弃作答"信号（对齐 通用判题-Prompt.md 判题边界：→ wrong + 引导）
_GIVE_UP = ("不知道", "不会", "不会背", "跳过", "忘了", "不记得", "想不起来", "pass", "skip", "i don't know")


def _detect_give_up(user_answer) -> bool:
    u = _norm(user_answer).strip().lower()
    if not u:
        return True
    return any(u == g or u.startswith(g + "。") or u.startswith(g + ",") or u == g + "！" for g in _GIVE_UP)


def _local_precheck(content: dict, user_answer: str) -> dict | None:
    """
    本地关键词预检（对齐 ADR-003 三层路由）：
      ≥0.85 → correct，直接短路由，不调 LLM
      0.60-0.85 → partial（AI 只做二次确认，非必需）
      返回 None → 进入 LLM
    """
    keywords = content.get("keywords", [])
    if not keywords:
        return None
    ratio, matched, missing, req_miss = _keyword_hit_ratio(content, user_answer)
    if ratio >= KEYWORD_CORRECT and not req_miss:
        return {"result": "correct", "confidence": round(ratio, 3),
                "matched_keywords": matched, "missing_keywords": missing, "hint": "",
                "bypass_llm": True}
    if ratio >= KEYWORD_PARTIAL and KEYWORD_PARTIAL <= ratio < KEYWORD_CORRECT and not req_miss:
        # partial 区间：本地可判，但优先交 LLM 确认语义（有 keywords 时边界模糊）
        return None
    return None


def grade(question: dict, user_answer, hint_level: str = "none",
          subject_registry: dict = None, voice: bool = False,
          timeout_ms: int = None, **kw) -> dict:
    """
    AI 判题统一入口：
    1. 本地关键词预检（≥85% 直接 correct，零 LLM 成本）
    2. 否则调 LLM（带解析后 Prompt）
    3. LLM 超时/失败/解析错误 → 降级 similarity_grader + grade_degraded=True
    """
    content = question.get("content", {})
    timeout_ms = timeout_ms or DEFAULT_LLM_TIMEOUT_MS

    # 0. 放弃作答预检（不消耗 LLM）
    if _detect_give_up(user_answer):
        return {"result": "wrong", "confidence": 0.1,
                "matched_keywords": [], "missing_keywords": [content.get("answer", "")] or [],
                "hint": "提示：可以点击'请提示我'获取线索", "grade_degraded": False}

    # 1. 本地预检
    pre = _local_precheck(content, user_answer)
    if pre:
        pre["grade_degraded"] = False
        return pre

    # 2. 构造 LLM 请求
    prompt = resolve_prompt(question, subject_registry)
    payload = build_llm_request(question, user_answer, hint_level, prompt)

    # 3. 调 LLM（同步包装，供测试；生产可换 async）
    raw, elapsed = None, 0.0
    try:
        t0 = time.monotonic()
        raw = _llm_client(payload, timeout_ms)
        elapsed = time.monotonic() - t0
    except NotImplementedError:
        raw = None
    except Exception:
        raw = None

    parsed = parse_llm_response(raw) if raw else None
    if parsed:
        parsed["grade_degraded"] = False
        parsed.setdefault("latency_ms", int(elapsed * 1000))
        return parsed

    # 4. 降级：similarity_grader（R 系列）或规则引擎兜底结果
    from similarity_grader import grade as sim_grade
    if question.get("type", "") in ("R1", "R2", "R3a", "R3b"):
        r = sim_grade(question, user_answer, voice=voice)
    else:
        a = content.get("answer", "")
        kratio, matched, missing, req_miss = _keyword_hit_ratio(content, user_answer)
        if kratio >= KEYWORD_CORRECT and not req_miss:
            r = {"result": "correct", "confidence": round(kratio, 3),
                 "matched_keywords": matched, "missing_keywords": missing, "hint": ""}
        elif kratio >= KEYWORD_PARTIAL:
            r = {"result": "partial", "confidence": round(kratio, 3),
                 "matched_keywords": matched, "missing_keywords": missing, "hint": ""}
        else:
            r = {"result": "wrong", "confidence": round(kratio or 0.0, 3),
                 "matched_keywords": matched, "missing_keywords": missing,
                 "hint": "提示：可向 AI 导师求助" if not missing else ""}
    if not r.get("matched_keywords"):
        r["matched_keywords"] = []
    r["grade_degraded"] = True
    r["missing_keywords"] = r.get("missing_keywords") or []
    return r


# ================== 测试矩阵 ==================

def _mock_llm_good(payload, timeout_ms):
    """mock：返回结构化正确 JSON（模拟 LLM 判 partial）。"""
    return json.dumps({
        "result": "partial",
        "confidence": 0.72,
        "matched_keywords": ["星汉灿烂"],
        "missing_keywords": ["若出其里"],
        "hint": "提示：与'星汉'相关",
    }, ensure_ascii=False)


def _mock_llm_bad(payload, timeout_ms):
    """mock：返回非 JSON（模拟 LLM 输出异常 → 触发降级）。"""
    return "抱歉，我无法解析这个答案。"


class _MockTimeout:
    def __call__(self, payload, timeout_ms):
        raise TimeoutError("mock timeout")


def _run_tests():
    q = {"id": "Q-ch-r1-0001", "subject": "chinese", "type": "R1",
         "content": {"question": "《观沧海》中描写大海吞吐日月的句子是？",
                     "answer": "日月之行，若出其中；星汉灿烂，若出其里。",
                     "keywords": [{"aliases": ["日月之行"], "weight": 1, "required": False},
                                  {"aliases": ["星汉灿烂"], "weight": 1, "required": False}]}}

    # 1. 本地预检：高命中 → correct，不调 LLM
    set_llm_client(_mock_llm_good)
    r = grade(q, "日月之行，若出其中；星汉灿烂，若出其里")
    assert r["result"] == "correct" and r.get("bypass_llm"), r

    # 2. LLM 正常：partial 区间 → 走 LLM → partial
    r = grade(q, "日月之行，若出其中")
    assert r["result"] == "partial" and not r.get("grade_degraded"), r
    assert r["missing_keywords"] == ["若出其里"], r

    # 3. LLM 异常输出 → 降级 similarity + grade_degraded
    #    注意：完整答案会在本地预检短路（bypass_llm），故用部分答案触发 LLM 路径
    set_llm_client(_mock_llm_bad)
    r = grade(q, "日月之行，若出其中")
    assert r.get("grade_degraded") is True, r
    assert r["result"] == "partial", r  # 降级后关键词 0.5 命中 + 高相似 → partial

    # 4. LLM 超时 → 降级
    set_llm_client(_MockTimeout())
    r = grade(q, "日月之行，若出其中")
    assert r.get("grade_degraded") is True and r["result"] == "partial", r

    # 5. 完全不会 → wrong
    set_llm_client(_mock_llm_good)
    r = grade(q, "不知道")
    assert r["result"] == "wrong", r

    # 6. resolve_prompt 回退全局默认
    p = resolve_prompt(q)
    assert "AI 导师" in p, "应加载通用判题-Prompt.md"

    # 7. 无 keywords 题目（X1 简答）：直接走 LLM
    qx = {"id": "Q-his-x1-0001", "subject": "history", "type": "X1",
          "content": {"question": "秦始皇统一六国的意义？", "answer": "统一文字货币度量衡"}}
    r = grade(qx, "统一了文字和货币")
    assert not r.get("grade_degraded"), r
    assert r["result"] == "partial", r

    print("ai_grader 测试矩阵: 全部通过 ✅")
    return 0


if __name__ == "__main__":
    import sys
    sys.exit(_run_tests())