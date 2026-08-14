from __future__ import annotations

import re
from dataclasses import dataclass, field

# 常见目标岗位 / 关键词（来自 docs/02-use-cases.md 目标岗位与技能要求）
POSITION_KEYWORDS = [
    "AI Agent 应用开发", "AI Agent应用开发", "AI Agent", "AI 产品经理", "AI产品经理",
    "产品经理", "FDE", "算法工程师", "后端开发", "前端开发", "全栈工程师",
    "数据工程师", "机器学习工程师", "大模型开发", "提示词工程师",
]

CITY_KEYWORDS = [
    "北京", "上海", "广州", "深圳", "杭州", "成都", "南京", "武汉",
    "苏州", "西安", "长沙", "重庆", "天津", "远程",
]

COMPANY_KEYWORDS = [
    "字节跳动", "字节", "阿里巴巴", "阿里", "腾讯", "Anthropic",
    "OpenAI", "GitHub", "百度", "美团", "微软", "Google", "Meta",
    "小米", "华为", "京东", "快手",
]

SKILL_KEYWORDS = [
    "Python", "Java", "C++", "C#", "JavaScript", "TypeScript", "Go", "Rust",
    "LLM", "RAG", "Agent", "LangGraph", "LangChain", "Memory", "Tool Calling",
    "Prompt Engineering", "Context Engineering", "Multi-Agent", "A2A", "MCP",
    "向量数据库", "ChromaDB", "SQLite", "FastAPI", "Embedding", "Retrieval",
    "Rerank", "深度学习", "Transformer",
]

SKILL_ALIASES = {
    "大模型": "LLM",
    "langgraph": "LangGraph",
    "langchain": "LangChain",
    "python": "Python",
    "typescript": "TypeScript",
    "javascript": "JavaScript",
    "vector db": "向量数据库",
}

_URL_RE = re.compile(r"https?://[^\s]+")
_JD_MARKERS = ["岗位职责", "职位描述", "任职要求", "任职资格", "岗位要求", "工作职责", "Job Description", "职责要求"]


@dataclass
class Entity:
    type: str          # position / city / company / skill / url / jd
    value: str
    confidence: float = 1.0
    metadata: dict = field(default_factory=dict)

    def to_dict(self) -> dict:
        return {"type": self.type, "value": self.value, "confidence": self.confidence}


class EntityExtractor:
    """基于规则的实体抽取（无需 LLM，稳定、可离线测试）。"""

    def extract(self, text: str) -> list[Entity]:
        entities: list[Entity] = []
        if not text:
            return entities

        entities.extend(self._extract_urls(text))
        entities.extend(self._extract_keywords(text, "position", POSITION_KEYWORDS))
        entities.extend(self._extract_keywords(text, "city", CITY_KEYWORDS))
        entities.extend(self._extract_keywords(text, "company", COMPANY_KEYWORDS))
        entities.extend(self._extract_keywords(text, "skill", SKILL_KEYWORDS))
        entities.extend(self._extract_alias_skills(text))
        entities.extend(self._extract_jd(text))

        return self._dedupe(entities)

    @staticmethod
    def _extract_urls(text: str) -> list[Entity]:
        return [
            Entity(type="url", value=m.group(0).rstrip("。，,;；"), confidence=0.95)
            for m in _URL_RE.finditer(text)
        ]

    @staticmethod
    def _extract_keywords(text: str, etype: str, keywords: list[str]) -> list[Entity]:
        found: list[Entity] = []
        for kw in keywords:
            if kw.lower() in text.lower():
                found.append(Entity(type=etype, value=kw, confidence=0.9))
        return found

    @staticmethod
    def _extract_alias_skills(text: str) -> list[Entity]:
        found: list[Entity] = []
        for alias, canonical in SKILL_ALIASES.items():
            if alias.lower() in text.lower():
                found.append(Entity(type="skill", value=canonical, confidence=0.8))
        return found

    @staticmethod
    def _extract_jd(text: str) -> list[Entity]:
        """若输入包含 JD 特征（职位描述段落），整段视为 JD 实体。"""
        markers = sum(1 for m in _JD_MARKERS if m in text)
        if markers >= 2 or (markers >= 1 and len(text) > 200):
            return [Entity(type="jd", value=text, confidence=0.85)]
        return []

    @staticmethod
    def _dedupe(entities: list[Entity]) -> list[Entity]:
        seen: set[tuple[str, str]] = set()
        result: list[Entity] = []
        for e in entities:
            key = (e.type, e.value.lower())
            if key in seen:
                continue
            seen.add(key)
            result.append(e)
        return result
