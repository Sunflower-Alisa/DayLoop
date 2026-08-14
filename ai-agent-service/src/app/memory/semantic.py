from __future__ import annotations

from app.core.logging import get_logger
from app.memory.long_term import LongTermMemory
from app.rag.retriever import Retriever

logger = get_logger("memory.semantic")


class SemanticMemory(LongTermMemory):
    """语义记忆（docs/05-architecture.md §31 Memory）。

    在 LongTermMemory 的落盘基础上，叠加向量检索，实现跨用户所有记忆的
    语义召回（不依赖精确关键词）。
    """

    def __init__(self, user_id: str = "", memory_dir: str | None = None, collection: str = "memory_kb") -> None:
        super().__init__(user_id, memory_dir)
        self._vector = Retriever(collection_name=f"{collection}_{user_id or 'default'}")

    def save(self, memory: dict) -> None:
        super().save(memory)
        try:
            self._vector.add_documents(
                [str(memory.get("content", memory))], doc_id=f"mem_{user_id}" if (user_id := self.user_id) else "mem"
            )
        except Exception as exc:
            logger.warning("语义记忆向量写入失败（不影响 JSON 落盘）: %s", exc)

    def query(self, text: str = "", top_k: int = 5) -> list[dict]:
        """优先语义召回，失败时退回关键词检索。"""
        try:
            hits = self._vector.retrieve(text, top_k=top_k)
            if hits:
                return [{"id": h["doc_id"], "text": h["text"], "score": h["score"], "semantic": True} for h in hits]
        except Exception as exc:
            logger.warning("语义召回失败，退回关键词: %s", exc)
        return super().query(text, top_k=top_k)