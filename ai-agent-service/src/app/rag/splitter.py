from __future__ import annotations

import re

from app.core.logging import get_logger

logger = get_logger("rag.splitter")


def split_text(text: str, chunk_size: int = 500, overlap: int = 50) -> list[str]:
    """按字符切分文本为带重叠的 chunk（docs/05-architecture.md §40 RAG）。

    优先在段落/句子边界断开，避免硬切造成语义撕裂：
    1. 逐段累积，攒到 chunk_size 附近在最近的段落边界收束；
    2. 单段超长时再退回按字符硬切。
    """
    if not text or not text.strip():
        return []
    normalized = re.sub(r"\r\n", "\n", text)
    paragraphs = [p.strip() for p in normalized.split("\n") if p.strip()]

    chunks: list[str] = []
    buf = ""
    for para in paragraphs:
        if len(para) > chunk_size:
            # 超长段落硬切
            if buf.strip():
                chunks.append(buf.strip())
                buf = ""
            chunks.extend(_hard_split(para, chunk_size, overlap))
            continue
        if len(buf) + len(para) + 1 <= chunk_size + overlap * 0:
            buf = f"{buf}\n{para}" if buf else para
            continue
        # 段落边界换 chunk
        chunks.append(buf.strip())
        buf = para

    if buf.strip():
        chunks.append(buf.strip())
    # overlap：在相邻 chunk 间补一段前文尾巴
    merged: list[str] = []
    for i, ch in enumerate(chunks):
        if i == 0 or overlap <= 0:
            merged.append(ch)
            continue
        prev = chunks[i - 1]
        tail = prev[-overlap:] if len(prev) > overlap else prev
        merged.append(f"{tail}\n{ch}")

    logger.info("split_text | %d chars -> %d chunks", len(text), len(merged))
    return merged


def _hard_split(para: str, chunk_size: int, overlap: int) -> list[str]:
    out: list[str] = []
    start = 0
    n = len(para)
    while start < n:
        end = min(start + chunk_size, n)
        out.append(para[start:end])
        if end >= n:
            break
        start = end - overlap if overlap > 0 else end
    return out