from __future__ import annotations

"""失败归因分析（docs/06-evaluation.md §53-§54）。

对每个评测用例，如果未达到预期，给出主因/次因分类，便于定位问题发生在
哪个环节（Intent / Context / Planning / Tool / RAG / Memory / Decision / Answer）。
"""

# 错误分类（§53）
ERROR_CATEGORIES = [
    "intent_error",
    "context_error",
    "planning_error",
    "tool_selection_error",
    "tool_execution_error",
    "rag_retrieval_error",
    "hallucination",
    "memory_error",
    "decision_error",
    "api_error",
    "final_answer_error",
    "unknown",
]


class FailureRecord:
    """单条失败归因记录。"""

    def __init__(self, case_id: str, category: str = "unknown", detail: str = "") -> None:
        self.case_id = case_id
        self.category = category
        self.detail = detail

    def to_dict(self) -> dict:
        return {"case_id": self.case_id, "category": self.category, "detail": self.detail}


def classify_intent_failure(case: dict, expected: str, actual: str) -> FailureRecord:
    return FailureRecord(
        case["id"],
        "intent_error",
        f"expected={expected} actual={actual}",
    )


def classify_field_failure(case: dict, field: str, expected, actual) -> FailureRecord:
    return FailureRecord(
        case["id"],
        "final_answer_error" if field == "final_answer" else "context_error",
        f"field={field} expected={expected!r} actual={actual!r}",
    )


def classify_tool_failure(case: dict, expected_ok: bool, actual_ok: bool, error: str = "") -> FailureRecord:
    cat = "tool_execution_error" if expected_ok else "tool_selection_error"
    return FailureRecord(case["id"], cat, f"expected_ok={expected_ok} actual_ok={actual_ok} {error}")


def classify_memory_failure(case: dict, expected: str, found: bool) -> FailureRecord:
    return FailureRecord(
        case["id"],
        "memory_error",
        f"expected_keyword={expected!r} found={found}",
    )


def classify_rag_failure(case: dict, expected_ids: list[str], actual_ids: list[str]) -> FailureRecord:
    return FailureRecord(
        case["id"],
        "rag_retrieval_error",
        f"expected={expected_ids} actual_top={actual_ids}",
    )


def aggregate_failures(records: list[FailureRecord]) -> dict:
    """按类别汇总失败次数，返回 {category: count}。"""
    counts: dict[str, int] = {c: 0 for c in ERROR_CATEGORIES}
    for r in records:
        counts[r.category] = counts.get(r.category, 0) + 1
    return counts
