from __future__ import annotations

"""LLM-as-a-Judge 评测器（docs/06-evaluation.md §41-§42）。

用于难以用规则判断的指标：
- Faithfulness：回答是否基于检索上下文（§21）
- Answer Relevance：回答是否真正回答问题（§22）
- Interview Answer Evaluation：面试回答质量评分（§40）

注意（§42）：LLM Judge 不作为唯一评价方式，本模块与规则评测互补；
LLM 调用失败时降级为关键词重叠的启发式评分。
"""

import json

from app.core.logging import get_logger
from app.evaluation.metrics import average
from app.llm.client import LLMClient
from app.llm.parser import parse_json

logger = get_logger("evaluation.llm_judge")

_SYSTEM_JUDGE = """你是严谨的 AI 回答评估专家。根据给定标准对回答打分。
只输出 JSON：{"score": 0-10 整数, "reason": "简短理由"}，不要其他内容。"""


def evaluate_faithfulness(cases: list[dict], use_llm: bool = True) -> dict:
    """Faithfulness：回答是否基于检索上下文（§21）。case: {id, context, answer}。"""
    return _run_judge_cases("faithfulness", cases, use_llm, _faithfulness_llm, _faithfulness_heuristic)


def evaluate_answer_relevance(cases: list[dict], use_llm: bool = True) -> dict:
    """Answer Relevance：回答是否真正回答用户问题（§22）。case: {id, question, answer}。"""
    return _run_judge_cases(
        "answer_relevance", cases, use_llm, _relevance_llm, _relevance_heuristic
    )


def evaluate_interview(cases: list[dict], use_llm: bool = True) -> dict:
    """面试回答评估（§40/§41）。case: {id, question, expected_answer, answer}。"""
    return _run_judge_cases("interview", cases, use_llm, _interview_llm, _interview_heuristic)


# ---- 通用 Runner ----

def _run_judge_cases(
    category: str,
    cases: list[dict],
    use_llm: bool,
    llm_fn,
    heuristic_fn,
) -> dict:
    scores: list[float] = []
    details: list[dict] = []
    llm_used = 0

    for case in cases:
        if use_llm:
            try:
                score, reason = llm_fn(case)
                llm_used += 1
            except Exception as exc:
                logger.warning("llm_judge[%s] LLM 失败，降级启发式: %s", category, exc)
                score, reason = heuristic_fn(case)
        else:
            score, reason = heuristic_fn(case)
        scores.append(score)
        details.append(
            {
                "id": case.get("id"),
                "score": score,
                "reason": reason,
                "method": "llm" if llm_used and reason != "heuristic" else "heuristic",
            }
        )

    return {
        "category": category,
        "metrics": {
            "avg_score": average(scores),
            "total": len(cases),
            "llm_used": llm_used,
        },
        "details": details,
        "failures": [],
        "failure_counts": {},
    }


# ---- Faithfulness ----

def _faithfulness_llm(case: dict) -> tuple[float, str]:
    prompt = (
        "请评估回答是否基于给定的检索上下文（Faithfulness），禁止编造上下文之外的内容。\n"
        f"上下文：{case.get('context', '')}\n回答：{case.get('answer', '')}\n"
        "输出 JSON：{\"score\": 0-10, \"reason\": \"...\"}"
    )
    return _ask_llm(prompt)


def _faithfulness_heuristic(case: dict) -> tuple[float, str]:
    ctx = case.get("context", "")
    ans = case.get("answer", "")
    overlap = _keyword_overlap(ctx, ans)
    return round(overlap * 10, 1), f"heuristic overlap={overlap:.2f}"


# ---- Answer Relevance ----

def _relevance_llm(case: dict) -> tuple[float, str]:
    prompt = (
        "请评估回答是否真正回答了用户的问题（Answer Relevance），离题或泛泛而谈给低分。\n"
        f"问题：{case.get('question', '')}\n回答：{case.get('answer', '')}\n"
        "输出 JSON：{\"score\": 0-10, \"reason\": \"...\"}"
    )
    return _ask_llm(prompt)


def _relevance_heuristic(case: dict) -> tuple[float, str]:
    q = case.get("question", "")
    ans = case.get("answer", "")
    overlap = _keyword_overlap(q, ans)
    return round(overlap * 10, 1), f"heuristic overlap={overlap:.2f}"


# ---- Interview Answer ----

def _interview_llm(case: dict) -> tuple[float, str]:
    prompt = (
        "你是 AI 岗位面试评估专家。评价求职者的回答，从技术正确性、完整性、深度、清晰度评分。\n"
        f"问题：{case.get('question', '')}\n参考答案要点：{case.get('expected_answer', '')}\n"
        f"求职者回答：{case.get('answer', '')}\n"
        "输出 JSON：{\"score\": 0-10, \"reason\": \"...\"}"
    )
    return _ask_llm(prompt)


def _interview_heuristic(case: dict) -> tuple[float, str]:
    ans = case.get("answer", "")
    exp = case.get("expected_answer", "")
    overlap = _keyword_overlap(exp, ans)
    base = 5.0
    score = min(10.0, base + overlap * 5.0)
    return round(score, 1), f"heuristic overlap={overlap:.2f}"


# ---- 工具函数 ----

def _ask_llm(prompt: str) -> tuple[float, str]:
    llm = LLMClient()
    raw = llm.chat(
        messages=[
            {"role": "system", "content": _SYSTEM_JUDGE},
            {"role": "user", "content": prompt},
        ],
        temperature=0.0,
    )
    data = parse_json(raw)
    score = float(data.get("score", 0))
    reason = str(data.get("reason", ""))
    return max(0.0, min(10.0, score)), reason


def _keyword_overlap(text_a: str, text_b: str) -> float:
    """关键词重叠率（启发式 Faithfulness/Relevance 的离线代理）。"""
    import re

    def tokens(t: str) -> set[str]:
        return set(re.findall(r"[\u4e00-\u9fff]{2,}|[a-zA-Z0-9_]{3,}", (t or "").lower()))

    a = tokens(text_a)
    b = tokens(text_b)
    if not a or not b:
        return 0.0
    return len(a & b) / len(b)


def _dump_case(case: dict) -> str:
    return json.dumps(case, ensure_ascii=False, default=str)
