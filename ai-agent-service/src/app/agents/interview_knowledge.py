from __future__ import annotations

import json

from app.core.logging import get_logger
from app.core.tracing import trace
from app.llm.client import LLMClient
from app.llm.parser import parse_json
from app.state import AgentState

logger = get_logger("agent.interview_knowledge")

SYSTEM_KNOWLEDGE = """你是面试知识整理专家。根据原始笔记/资料，提取与目标岗位面试相关的面试题与知识点。
只输出 JSON：{"questions": [{"question": "...", "category": "LLM|RAG|Agent|工程化|AI产品|FDE|其他", "difficulty": "easy|medium|hard", "knowledge_points": [...], "answer": "...", "follow_up": [...]}]}
不要输出其他内容。"""

_CATEGORIES = ["LLM", "RAG", "Agent", "工程化", "AI产品", "FDE", "其他"]


class InterviewKnowledgeAgent:
    """面试知识库整理 Agent（UC05 / docs/05-architecture.md §14）。

    流程：获取新增内容 → 相关性判断 → 提取面试题/知识点 → 分类/难度 → 去重
    → 写入知识库（本地 JSON + 向量检索库）。
    """

    name = "interview_knowledge"

    def run(self, state: AgentState) -> dict:
        with trace("interview_knowledge"):
            notes = self._collect_notes(state)
            questions = self._extract(notes)
            questions = self._dedupe(questions)
            self._persist(questions, state)
            result = {
                "questions": questions,
                "stats": {
                    "total": len(questions),
                    "by_category": {c: sum(1 for q in questions if q.get("category") == c) for c in _CATEGORIES},
                },
                "final": _render_report(questions),
            }
            state.final_answer = result["final"]
            state.observations.append({"agent": self.name, "stats": result["stats"]})
            logger.info("interview_knowledge done | questions=%d", len(questions))
            return result

    # ---- 内部 ----

    def _collect_notes(self, state: AgentState) -> list[str]:
        """获取素材：优先用户输入，其次检索知识库。"""
        notes: list[str] = []
        if state.user_input and state.user_input.strip():
            notes.append(state.user_input)
        try:
            from app.rag.retriever import Retriever

            hits = Retriever(collection_name="interview_kb").retrieve("面试 笔记 技术知识", top_k=5)
            for h in hits:
                if h["text"] not in notes:
                    notes.append(h["text"])
        except Exception as exc:
            logger.warning("interview_knowledge 检索失败（不影响输入处理）: %s", exc)
        return notes

    def _extract(self, notes: list[str]) -> list[dict]:
        if not notes:
            return []
        prompt = "请从以下资料中提取面试题与知识点，输出 JSON：\n" + json.dumps(notes, ensure_ascii=False, indent=1)
        try:
            llm = LLMClient()
            raw = llm.chat(
                messages=[
                    {"role": "system", "content": SYSTEM_KNOWLEDGE},
                    {"role": "user", "content": prompt},
                ],
                temperature=0.3,
            )
            parsed = parse_json(raw)
            return parsed.get("questions", [])
        except Exception as exc:
            logger.warning("interview_knowledge LLM 失败，退回规则提取: %s", exc)
            return [{"question": n[:100], "category": _classify(n), "difficulty": "medium", "answer": "", "knowledge_points": []} for n in notes]

    @staticmethod
    def _dedupe(questions: list[dict]) -> list[dict]:
        seen: set[str] = set()
        out: list[dict] = []
        for q in questions:
            key = (q.get("question") or "").strip()
            if not key or key in seen:
                continue
            seen.add(key)
            q.setdefault("category", _classify(key))
            q.setdefault("difficulty", "medium")
            q.setdefault("answer", "")
            q.setdefault("knowledge_points", [])
            q.setdefault("follow_up", [])
            out.append(q)
        return out

    def _persist(self, questions: list[dict], state: AgentState) -> None:
        if not questions:
            return
        try:
            from app.memory import LongTermMemory

            mem = LongTermMemory(user_id=state.user_id)
            for q in questions[:20]:
                mem.save({"type": "interview_question", "content": {"question": q["question"], "category": q["category"], "difficulty": q["difficulty"], "answer": q["answer"]}})
            logger.info("interview_knowledge 写入记忆 %d 条", min(len(questions), 20))
        except Exception as exc:
            logger.warning("interview_knowledge 记忆写入失败: %s", exc)


def _classify(text: str) -> str:
    tl = text.lower()
    if any(k in tl for k in ("rag", "chunk", "embedding", "retriever", "rerank", "向量", "检索")):
        return "RAG"
    if any(k in tl for k in ("agent", "tool calling", "multi-agent", "reflection", "planner")):
        return "Agent"
    if any(k in tl for k in ("llm", "transformer", "attention", "prompt", "微调", "大模型")):
        return "LLM"
    if any(k in tl for k in ("api", "并发", "缓存", "消息队列", "数据库", "部署", "监控")):
        return "工程化"
    if any(k in tl for k in ("产品", "fde", "客户", "交付")):
        return "AI产品" if "fde" not in tl else "FDE"
    return "其他"


def _render_report(questions: list[dict]) -> str:
    if not questions:
        return "📚 面试知识库：本次没有提取到新的面试题。"
    lines = [f"📚 面试知识库整理（新增 {len(questions)} 条）："]
    for q in questions[:10]:
        lines.append(f"- [{q['category']} | {q['difficulty']}] {q['question']}")
        if q.get("knowledge_points"):
            lines.append(f"  知识点：{', '.join(q['knowledge_points'][:3])}")
    return "\n".join(lines)