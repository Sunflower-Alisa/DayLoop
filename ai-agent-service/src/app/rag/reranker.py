from __future__ import annotations

import re

from app.core.logging import get_logger

logger = get_logger("rag.reranker")


def rerank(query: str, candidates: list[dict], top_k: int = 3) -> list[dict]:
    """轻量重排：在向量相似度基础上叠加关键词命中加分（docs/05-architecture.md §40）。

    候选格式 [{doc_id, text, score}]；返回按综合分倒序、截断 top_k。
    """
    if not candidates:
        return []
    q_tokens = set(re.findall(r"[\u4e00-\u9fff]|[a-zA-Z0-9]+", query.lower()))
    for c in candidates:
        text_tokens = set(re.findall(r"[\u4e00-\u9fff]|[a-zA-Z0-9]+", (c.get("text") or "").lower()))
        overlap = len(q_tokens & text_tokens)
        base = float(c.get("score", 0.0))
        c["rerank_score"] = round(min(1.0, base + 0.05 * overlap), 4)
    ranked = sorted(candidates, key=lambda c: c.get("rerank_score", 0.0), reverse=True)
    return ranked[:top_k]