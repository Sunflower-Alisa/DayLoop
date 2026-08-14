from __future__ import annotations

import json

from app.core.logging import get_logger
from app.core.tracing import trace
from app.llm.client import LLMClient
from app.llm.parser import parse_json
from app.state import AgentState

logger = get_logger("agent.industry_info")

SYSTEM_INDUSTRY = """你是 AI 行业情报分析师。基于给定的行业信息线索，判断其相关性与重要性，并生成日报条目。
只输出 JSON：{"items": [{"title": "...", "source": "...", "relevance": "high|mid|low", "priority": 3|2|1, "summary": "...", "impact": "...", "career_relevance": "..."}]}
不要输出其他内容。"""

_DEFAULT_SOURCES = [
    {"title": "AI Agent 相关技术进展", "source": "GitHub / 技术社区", "query": "AI Agent 进展"},
    {"title": "大模型与 RAG 应用落地", "source": "Anthropic / 字节 / 阿里 / 腾讯", "query": "LLM RAG 应用"},
]


class IndustryInfoAgent:
    """AI 行业信息收集 Agent（UC01 / docs/05-architecture.md §33）。

    流程：确定关注范围 → 获取信息线索 → 相关性/重要性判断 → LLM 总结 → 生成行业日报。
    MVP：无 Web Search 工具时使用内置线索 + LLM 组织；DayLoop 知识库可经 RAG 补充。
    """

    name = "industry_info"

    def run(self, state: AgentState) -> dict:
        with trace("industry_info"):
            clues = self._collect_clues(state)
            digest = self._llm_summarize(clues)
            report = {
                "clues": clues,
                "digest": digest,
                "final": _render_report(digest),
            }
            state.final_answer = report["final"]
            state.observations.append({"agent": self.name, "digest": digest})
            logger.info("industry_info done | clues=%d items=%d", len(clues), len(digest.get("items", [])))
            return report

    # ---- 内部 ----

    def _collect_clues(self, state: AgentState) -> list[dict]:
        """收集信息线索：优先 Web Search 实时线索，其次知识库检索，最后默认关注源。"""
        clues: list[dict] = []

        try:
            from app.tools.web_search import WebSearchTool

            for source in _DEFAULT_SOURCES:
                result = WebSearchTool().execute(query=source["query"], max_results=3)
                if result.get("ok"):
                    for item in result["results"]:
                        clues.append(
                            {
                                "title": item.get("title", ""),
                                "source": source["source"],
                                "content": item.get("snippet", item.get("title", "")),
                                "url": item.get("url", ""),
                            }
                        )
        except Exception as exc:
            logger.warning("industry_info Web Search 不可用，回退知识库: %s", exc)

        if not clues:
            try:
                from app.rag.retriever import Retriever

                hits = Retriever(collection_name="industry_kb").retrieve("AI 行业 大模型 Agent 动态", top_k=5)
                for h in hits:
                    clues.append({"title": h["text"][:60], "source": h.get("source", "知识库"), "content": h["text"]})
            except Exception as exc:
                logger.warning("industry_info 知识库检索失败，使用默认线索: %s", exc)

        if not clues:
            clues = [dict(s) | {"content": s["title"]} for s in _DEFAULT_SOURCES]
        return clues

    def _llm_summarize(self, clues: list[dict]) -> dict:
        prompt = "请分析以下 AI 行业信息线索，输出 JSON 日报条目：\n" + json.dumps(clues, ensure_ascii=False, indent=1)
        try:
            llm = LLMClient()
            raw = llm.chat(
                messages=[
                    {"role": "system", "content": SYSTEM_INDUSTRY},
                    {"role": "user", "content": prompt},
                ],
                temperature=0.3,
            )
            return parse_json(raw)
        except Exception as exc:
            logger.warning("industry_info LLM 失败，退回规则摘要: %s", exc)
            return {
                "items": [
                    {
                        "title": c["title"],
                        "source": c.get("source", ""),
                        "relevance": "mid",
                        "priority": 2,
                        "summary": c.get("content", "")[:120],
                        "impact": "待核实",
                        "career_relevance": "与 AI 职业方向相关，建议关注",
                    }
                    for c in clues
                ]
            }


def _render_report(digest: dict) -> str:
    items = digest.get("items", [])
    if not items:
        return "📊 AI 行业动态：暂无新的重要信息。"
    lines = ["📊 AI 行业日报（过去24小时）："]
    by_priority = {"3": [], "2": [], "1": []}
    for it in items:
        by_priority[str(it.get("priority", 2))].append(it)
    for pri, star in (("3", "⭐⭐⭐ 高优先级"), ("2", "⭐⭐ 中优先级"), ("1", "⭐ 低优先级")):
        group = by_priority[pri]
        if not group:
            continue
        lines.append(f"\n{star}")
        for it in group[:5]:
            lines.append(f"- {it.get('title', '')}")
            if it.get("summary"):
                lines.append(f"  {it['summary'][:80]}")
            if it.get("impact"):
                lines.append(f"  影响：{it['impact'][:60]}")
            if it.get("career_relevance"):
                lines.append(f"  相关性：{it['career_relevance'][:60]}")
    return "\n".join(lines)