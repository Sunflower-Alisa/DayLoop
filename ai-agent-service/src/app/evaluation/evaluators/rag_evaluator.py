from __future__ import annotations

"""RAG 检索评测器（docs/06-evaluation.md §15-§19）。

把评测文档切分/嵌入/入库到临时 ChromaDB，再按查询检索，
计算 Hit Rate@K / Precision@K / Recall@K / MRR。
"""

import tempfile

from app.core.logging import get_logger
from app.evaluation.error_analysis import FailureRecord, aggregate_failures, classify_rag_failure
from app.evaluation.metrics import hit_rate_at_k, mrr, precision_at_k, recall_at_k
from app.rag.retriever import Retriever

logger = get_logger("evaluation.rag")


def evaluate_rag(case_data: dict, persist_dir: str | None = None, embed: bool = True) -> dict:
    """RAG 检索评测。case_data: {"documents": [{id, text}], "queries": [{id, query, expected_ids, top_k}]}。

    返回每查询的召回结果与聚合指标。
    """
    documents = case_data.get("documents", [])
    queries = case_data.get("queries", [])
    if not documents or not queries:
        return _empty(documents, queries)

    persist_dir = persist_dir or tempfile.mkdtemp(prefix="eval_rag_")
    retriever = Retriever(collection_name="eval_rag_kb", persist_dir=persist_dir)
    for doc in documents:
        retriever.add_documents([doc.get("text", "")], doc_id=doc.get("id", ""))

    details: list[dict] = []
    failures: list[FailureRecord] = []
    hit_flags: list[bool] = []
    relevant_in_top: list[int] = []
    recall_rates: list[float] = []
    ranks: list[int] = []

    for q in queries:
        query = q.get("query", "")
        top_k = q.get("top_k", 3)
        expected_ids = q.get("expected_ids", [])
        hits = retriever.retrieve(query, top_k=top_k)
        actual_ids = [h.get("source") or h.get("doc_id", "") for h in hits]

        rank = _first_expected_rank(actual_ids, expected_ids)
        hit = rank > 0
        hit_flags.append(hit)
        relevant_in_top.append(_count_expected(actual_ids, expected_ids))
        recall_rates.append(_recall(actual_ids, expected_ids))
        if rank > 0:
            ranks.append(rank)

        details.append(
            {
                "id": q.get("id"),
                "query": query,
                "top_k": top_k,
                "expected_ids": expected_ids,
                "actual_ids": actual_ids,
                "first_rank": rank,
                "hit": hit,
            }
        )
        if not hit:
            failures.append(classify_rag_failure(q, expected_ids, actual_ids))

    return {
        "category": "rag",
        "metrics": {
            "hit_rate@3": hit_rate_at_k(hit_flags, 3),
            "precision@3": precision_at_k(relevant_in_top, 3),
            "recall@3": recall_at_k(recall_rates),
            "mrr": mrr(ranks),
            "total": len(queries),
            "hit_count": sum(hit_flags),
        },
        "details": details,
        "failures": [f.to_dict() for f in failures],
        "failure_counts": aggregate_failures(failures),
    }


def _first_expected_rank(actual_ids: list[str], expected_ids: list[str]) -> int:
    for i, aid in enumerate(actual_ids, 1):
        if any(_same(aid, eid) for eid in expected_ids):
            return i
    return 0


def _count_expected(actual_ids: list[str], expected_ids: list[str]) -> int:
    return sum(1 for aid in actual_ids if any(_same(aid, eid) for eid in expected_ids))


def _recall(actual_ids: list[str], expected_ids: list[str]) -> float:
    if not expected_ids:
        return 1.0
    return _count_expected(actual_ids, expected_ids) / len(expected_ids)


def _same(a: str, b: str) -> bool:
    return a == b or a.rsplit(":", 1)[-1] == b or b in a


def _empty(documents: list, queries: list) -> dict:
    return {
        "category": "rag",
        "metrics": {"hit_rate@3": 0.0, "precision@3": 0.0, "recall@3": 0.0, "mrr": 0.0, "total": len(queries), "hit_count": 0},
        "details": [],
        "failures": [],
        "failure_counts": {},
    }
