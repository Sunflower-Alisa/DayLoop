from __future__ import annotations

import os
import uuid

os.environ.setdefault("ANONYMIZED_TELEMETRY", "False")

import chromadb
import posthog
from chromadb.telemetry.product import ProductTelemetryClient

# chromadb 0.6.x 自带 posthog telemetry 与当前 posthog 版本不兼容，静默掉以免噪音
ProductTelemetryClient.capture = lambda self, event: None  # type: ignore[assignment]
posthog.capture = lambda *args, **kwargs: None  # type: ignore[assignment]

from app.core.exceptions import RAGError
from app.core.logging import get_logger
from app.rag.embedding import Embedder
from app.rag.splitter import split_text

logger = get_logger("rag.retriever")


class Retriever:
    """向量检索器（docs/05-architecture.md §40 RAG）。

    基于 ChromaDB 持久化存储文档 chunk 的 Embedding 并支持相似度检索。
    - add_documents: 切分 → 嵌入 → upsert（相同 doc_id 幂等）
    - retrieve: 按 query 召回 top_k 个 chunk
    """

    def __init__(self, collection_name: str = "dayloop_kb", persist_dir: str | None = None) -> None:
        self.embedder = Embedder()
        persist_dir = persist_dir or os.getenv("CHROMA_PERSIST_DIR", os.path.join(os.getcwd(), ".chroma"))
        try:
            self._client = chromadb.PersistentClient(path=persist_dir)
            self._collection = self._client.get_or_create_collection(
                name=collection_name, metadata={"hnsw:space": "cosine"}
            )
        except Exception as exc:
            raise RAGError(f"初始化 ChromaDB 失败: {persist_dir}", cause=exc) from exc
        self.collection_name = collection_name
        logger.info("retriever ready | collection=%s dir=%s", collection_name, persist_dir)

    def add_documents(self, docs: list[str], doc_id: str = "", chunk_size: int = 500, overlap: int = 50) -> int:
        """把文档切分、嵌入并入库；返回新增 chunk 数。"""
        if not docs:
            return 0
        chunks: list[str] = []
        for d in docs:
            chunks.extend(split_text(d, chunk_size, overlap))
        if not chunks:
            return 0
        vectors = self.embedder.embed_texts(chunks)
        ids = [f"{doc_id or 'doc'}:{i}:{uuid.uuid4().hex[:8]}" for i in range(len(chunks))]
        metadatas = [{"doc_id": doc_id, "chunk_index": i, "chunk": c} for i, c in enumerate(chunks)]
        self._collection.upsert(ids=ids, embeddings=vectors, documents=chunks, metadatas=metadatas)
        logger.info("retriever add | doc=%s chunks=%d", doc_id, len(chunks))
        return len(chunks)

    def retrieve(self, query: str, top_k: int = 5) -> list[dict]:
        """按 query 召回 top_k 个 chunk，返回 [{doc_id, text, score}]。"""
        if not query or not query.strip():
            return []
        qv = self.embedder.embed_query(query)
        try:
            result = self._collection.query(query_embeddings=[qv], n_results=top_k)
        except Exception as exc:
            raise RAGError("ChromaDB 检索失败", cause=exc) from exc
        ids = result.get("ids", [[]])[0]
        documents = result.get("documents", [[]])[0]
        metadatas = result.get("metadatas", [[]])[0]
        distances = result.get("distances", [[]])[0]
        out: list[dict] = []
        for i, doc_id in enumerate(ids):
            meta = metadatas[i] if i < len(metadatas) else {}
            dist = distances[i] if i < len(distances) else 1.0
            score = max(0.0, min(1.0, 1.0 - float(dist)))
            out.append(
                {
                    "doc_id": doc_id,
                    "text": documents[i] if i < len(documents) else "",
                    "source": meta.get("doc_id", ""),
                    "chunk_index": meta.get("chunk_index", 0),
                    "score": round(score, 4),
                }
            )
        logger.info("retriever query | q_len=%d hits=%d", len(query), len(out))
        return out

    def count(self) -> int:
        return self._collection.count()