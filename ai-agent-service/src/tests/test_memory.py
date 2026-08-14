from __future__ import annotations

import tempfile

from app.memory.long_term import LongTermMemory


def _new_memory(tmp: tempfile.TemporaryDirectory) -> LongTermMemory:
    return LongTermMemory(user_id="test_user", memory_dir=tmp.name)


def test_save_and_count(tmp_path):
    mem = LongTermMemory(user_id="u1", memory_dir=str(tmp_path))
    mem.save({"type": "preference", "content": "目标岗位是 AI Agent 开发"})
    mem.save({"type": "skill", "content": "熟悉 Python 与 RAG"})
    assert mem.count() == 2


def test_query_recalls_by_keyword(tmp_path):
    mem = LongTermMemory(user_id="u1", memory_dir=str(tmp_path))
    mem.save({"type": "preference", "content": "目标岗位是 AI Agent 开发"})
    hits = mem.query("AI Agent", top_k=5)
    assert hits
    assert "AI Agent" in hits[0]["content"]


def test_query_empty_returns_recent(tmp_path):
    mem = LongTermMemory(user_id="u1", memory_dir=str(tmp_path))
    mem.save({"type": "general", "content": "第一条"})
    mem.save({"type": "general", "content": "第二条"})
    hits = mem.query("", top_k=1)
    assert hits and "第二条" in hits[0]["content"]


def test_persistence_across_instances(tmp_path):
    mem1 = LongTermMemory(user_id="u1", memory_dir=str(tmp_path))
    mem1.save({"type": "preference", "content": "目标岗位 AI Agent"})
    mem2 = LongTermMemory(user_id="u1", memory_dir=str(tmp_path))
    assert mem2.count() == 1
    assert "AI Agent" in mem2.query("AI Agent")[0]["content"]


def test_corrupted_file_resets(tmp_path):
    f = tmp_path / "u1.json"
    f.write_text("{broken json", encoding="utf-8")
    mem = LongTermMemory(user_id="u1", memory_dir=str(tmp_path))
    assert mem.count() == 0