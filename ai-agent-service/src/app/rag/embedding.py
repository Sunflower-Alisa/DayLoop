from __future__ import annotations

import hashlib
import os

import numpy as np

from app.core.logging import get_logger
from app.core.retry import retry

logger = get_logger("rag.embedding")


class Embedder:
    """Embedding 服务（docs/05-architecture.md §40）。

    优先使用 OpenAI 嵌入模型；未配置 OPENAI_API_KEY 时退化为
    基于字符哈希的确定性向量，保证 RAG 管道离线可用（召回质量有限）。
    """

    def __init__(self, model: str | None = None) -> None:
        self.model = model or os.getenv("OPENAI_EMBEDDING_MODEL", "text-embedding-3-small")
        self._offline = not bool(os.getenv("OPENAI_API_KEY"))
        self._client = None
        if not self._offline:
            from openai import OpenAI

            self._client = OpenAI(
                api_key=os.getenv("OPENAI_API_KEY"),
                base_url=os.getenv("OPENAI_BASE_URL", "https://api.openai.com/v1"),
            )
        logger.info("embedder ready | model=%s mode=%s", self.model, "offline" if self._offline else "openai")

    @retry(max_times=3, base_delay=0.5)
    def embed_texts(self, texts: list[str]) -> list[list[float]]:
        if not texts:
            return []
        if self._offline:
            return [self._hash_embedding(t) for t in texts]
        try:
            resp = self._client.embeddings.create(model=self.model, input=texts)
            return [d.embedding for d in resp.data]
        except Exception as exc:
            logger.warning("Embedding API 失败，退回离线向量: %s", exc)
            return [self._hash_embedding(t) for t in texts]

    def embed_query(self, query: str) -> list[float]:
        return self.embed_texts([query])[0]

    @staticmethod
    def _hash_embedding(text: str, dim: int = 256) -> list[float]:
        feature = np.zeros(dim, dtype=np.float32)
        tokens = _tokenize(text)
        for tok in tokens:
            h = hashlib.md5(tok.encode("utf-8")).digest()
            idx = int.from_bytes(h[:4], "little") % dim
            sign = 1.0 if h[4] % 2 == 0 else -1.0
            feature[idx] += sign
        norm = np.linalg.norm(feature) or 1.0
        return (feature / norm).tolist()


def _tokenize(text: str) -> list[str]:
    import re

    tokens = re.findall(r"[\u4e00-\u9fff]|[a-zA-Z0-9]+", text.lower())
    # 中文按字、英文按词
    out: list[str] = []
    for t in tokens:
        if re.match(r"[\u4e00-\u9fff]", t):
            out.append(t)
        else:
            out.append(t)
    return out or [text]