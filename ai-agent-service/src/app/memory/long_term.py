from __future__ import annotations

import json
import os
import uuid
from datetime import datetime, timezone
from pathlib import Path

from app.core.exceptions import MemoryError
from app.core.logging import get_logger

logger = get_logger("memory.long_term")


class LongTermMemory:
    """长期记忆持久化（docs/04-agent-flow.md §9 Memory Update）。

    将具有长期价值的用户信息（技能画像、求职偏好、面试表现等）以 JSON
    追加式落盘，支持按文本关键词检索。文件持久化在 MEMORY_DIR 下按 user_id 分文件。
    """

    def __init__(self, user_id: str = "", memory_dir: str | None = None) -> None:
        self.user_id = user_id
        memory_dir = memory_dir or os.getenv("MEMORY_DIR", os.path.join(os.getcwd(), ".memory"))
        self._path = Path(memory_dir)
        self._path.mkdir(parents=True, exist_ok=True)
        self._file = self._path / f"{user_id or 'default'}.json"
        self._records: list[dict] = self._load()

    def _load(self) -> list[dict]:
        if not self._file.exists():
            return []
        try:
            with open(self._file, "r", encoding="utf-8") as f:
                data = json.load(f)
            return data if isinstance(data, list) else []
        except Exception as exc:
            logger.warning("长期记忆文件损坏，重置: %s (%s)", self._file, exc)
            return []

    def _flush(self) -> None:
        try:
            with open(self._file, "w", encoding="utf-8") as f:
                json.dump(self._records, f, ensure_ascii=False, indent=1)
        except Exception as exc:
            raise MemoryError(f"长期记忆写入失败: {self._file}", cause=exc) from exc

    def save(self, memory: dict) -> None:
        """保存一条记忆记录。memory 应包含 type（记忆类型）与 content（主要内容）。"""
        if not memory:
            return
        record = {
            "id": uuid.uuid4().hex[:12],
            "type": memory.pop("type", "general"),
            "content": memory.get("content", memory),
            "user_id": self.user_id,
            "ts": datetime.now(timezone.utc).isoformat(),
            **memory,
        }
        self._records.append(record)
        self._flush()
        logger.info("long-term memory 保存 | type=%s id=%s", record["type"], record["id"])

    def query(self, text: str = "", top_k: int = 5) -> list[dict]:
        """按内容关键词模糊检索最近 top_k 条记忆。"""
        if not text or not text.strip():
            return list(reversed(self._records[-top_k:]))
        kw = text.lower()
        scored: list[tuple[float, dict]] = []
        for rec in self._records:
            blob = json.dumps(rec, ensure_ascii=False).lower()
            hits = sum(1 for tok in _split(kw) if tok in blob)
            if hits:
                scored.append((hits, rec))
        scored.sort(key=lambda x: x[0], reverse=True)
        return [rec for _, rec in scored[:top_k]]

    def all(self) -> list[dict]:
        return list(self._records)

    def count(self) -> int:
        return len(self._records)


def _split(text: str) -> list[str]:
    import re

    tokens = re.findall(r"[\u4e00-\u9fff]{2,}|[a-zA-Z0-9_]+", text.lower())
    return tokens