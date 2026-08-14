from __future__ import annotations

"""评测指标计算（docs/06-evaluation.md §6-§45）。

所有指标函数均为纯计算，不依赖 LLM / DayLoop，便于离线评测与回归对比。
"""

from statistics import median, quantiles
from typing import Iterable, Sequence


def accuracy(correct: int, total: int) -> float:
    """正确率 = Correct / Total（§6 Intent Accuracy 等）。"""
    return round(correct / total, 4) if total else 0.0


def average(values: Sequence[float]) -> float:
    return round(sum(values) / len(values), 4) if values else 0.0


def percentile(values: Sequence[float], p: float) -> float:
    """P 分位数（§44 Latency 的 P50/P95/P99）。"""
    if not values:
        return 0.0
    q = quantiles(sorted(values), n=100, method="inclusive")
    return round(q[max(1, min(100, int(p))) - 1], 4)


def p50(values: Sequence[float]) -> float:
    return percentile(values, 50)


def p95(values: Sequence[float]) -> float:
    return percentile(values, 95)


def hit_rate_at_k(hits: Sequence[bool], k: int) -> float:
    """Hit Rate@K：正确答案出现在 Top-K 内的比例（§16）。"""
    if not hits:
        return 0.0
    return round(sum(hits) / len(hits), 4)


def precision_at_k(relevant_in_top: Sequence[int], k: int) -> float:
    """Precision@K：Top-K 结果中真正相关的比例（§17）。"""
    if not relevant_in_top:
        return 0.0
    return round(sum(min(r, k) for r in relevant_in_top) / (len(relevant_in_top) * k), 4)


def recall_at_k(hit_rates: Sequence[float]) -> float:
    """Recall@K：相关知识中被检索出来的比例（§18）。"""
    return round(average(hit_rates), 4)


def mrr(ranks: Iterable[int]) -> float:
    """MRR：第一个正确结果排名的倒数均值（§19）。"""
    values = [1.0 / r if r > 0 else 0.0 for r in ranks]
    return round(average(values), 4)


def task_success_rate(success: int, total: int) -> float:
    """Task Success Rate = 成功完成任务 / 总任务（§13）。"""
    return round(success / total, 4) if total else 0.0


def tool_success_rate(ok: Sequence[bool]) -> float:
    """Tool Execution Success Rate（§10）。"""
    if not ok:
        return 0.0
    return round(sum(ok) / len(ok), 4)


def average_tokens(tokens: Iterable[dict]) -> dict:
    """Token 用量汇总（§45）。tokens: [{input, output, ...}]。"""
    data = list(tokens)
    if not data:
        return {"input": 0, "output": 0, "total": 0, "avg_input": 0, "avg_output": 0}
    total_in = sum(int(t.get("input", 0)) for t in data)
    total_out = sum(int(t.get("output", 0)) for t in data)
    return {
        "input": total_in,
        "output": total_out,
        "total": total_in + total_out,
        "avg_input": round(total_in / len(data), 2),
        "avg_output": round(total_out / len(data), 2),
    }


def is_within_target(metric: float, target: float, tolerance: float = 0.0) -> bool:
    """判断指标是否达到目标（§57 MVP 目标）。"""
    return metric >= target - tolerance
