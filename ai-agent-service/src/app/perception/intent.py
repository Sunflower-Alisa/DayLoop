from __future__ import annotations

import re
from dataclasses import dataclass, field

from app.core.logging import get_logger

logger = get_logger("perception.intent")

# ===== 意图常量（与 docs/05-architecture.md §18 Intent Router 对齐） =====
INTENT_INDUSTRY_INFO = "INDUSTRY_INFO"            # UC01 AI行业信息
INTENT_JOB_SEARCH = "JOB_SEARCH"                  # UC02 招聘信息
INTENT_JD_ANALYSIS = "JD_ANALYSIS"                # UC03 JD分析
INTENT_SKILL_GAP = "SKILL_GAP"                    # UC04 Skill Gap
INTENT_INTERVIEW_KNOWLEDGE = "INTERVIEW_KNOWLEDGE"  # UC05 面试知识库
INTENT_MOCK_INTERVIEW = "MOCK_INTERVIEW"          # UC06 模拟面试
INTENT_RESUME_UPDATE = "RESUME_UPDATE"            # 更新简历
INTENT_TASK_MANAGEMENT = "TASK_MANAGEMENT"        # 任务管理
INTENT_GENERAL_CHAT = "GENERAL_CHAT"              # 通用聊天/兜底

ALL_INTENTS = [
    INTENT_INDUSTRY_INFO,
    INTENT_JOB_SEARCH,
    INTENT_JD_ANALYSIS,
    INTENT_SKILL_GAP,
    INTENT_INTERVIEW_KNOWLEDGE,
    INTENT_MOCK_INTERVIEW,
    INTENT_RESUME_UPDATE,
    INTENT_TASK_MANAGEMENT,
    INTENT_GENERAL_CHAT,
]

# 各 Use Case 需要的上下文（Context Requirement）
# 对应 docs/05-architecture.md §19 Context Manager 与 02-use-cases.md
CONTEXT_REQUIREMENTS: dict[str, list[str]] = {
    INTENT_JD_ANALYSIS: ["JD", "Resume", "Skill Profile", "Job Preference", "Memory"],
    INTENT_SKILL_GAP: ["Resume", "Skill Profile", "JD", "Memory"],
    INTENT_MOCK_INTERVIEW: ["JD", "Resume", "Skill Profile", "Interview Knowledge", "Memory"],
    INTENT_INTERVIEW_KNOWLEDGE: ["Interview Knowledge", "Memory"],
    INTENT_INDUSTRY_INFO: ["Knowledge", "External Data"],
    INTENT_JOB_SEARCH: ["Job Preference", "Target Position", "Skill Profile", "External Data"],
    INTENT_RESUME_UPDATE: ["Resume"],
    INTENT_TASK_MANAGEMENT: ["Tasks", "Memory"],
    INTENT_GENERAL_CHAT: ["Memory"],
}

# 规则匹配（Rule-based）。列表顺序即优先级，靠前的意图优先命中。
# 使用正则，支持「分析xxx这个岗位」「有什么动态」等中间插入词语的写法。
_RULES: list[tuple[str, list[str]]] = [
    (INTENT_MOCK_INTERVIEW, [
        r"模拟面试", r"面试我", r"开始面试", r"做个面试", r"面试一下", r"来面试",
    ]),
    (INTENT_INTERVIEW_KNOWLEDGE, [
        r"面试题", r"面试知识", r"知识库", r"题库", r"面试记录", r"面试笔记", r"面试内容",
    ]),
    (INTENT_RESUME_UPDATE, [
        r"更新.*简历", r"修改.*简历", r"改.*简历", r"简历.*更新", r"简历.*修改",
    ]),
    (INTENT_JD_ANALYSIS, [
        r"分析.*(?:jd|JD|岗位|职位)", r"(?:jd|JD).*分析", r"这个岗位", r"这个职位",
        r"值不值得", r"岗位匹配", r"分析岗位", r"投递建议",
    ]),
    (INTENT_SKILL_GAP, [
        r"技能差距", r"skill gap", r"还缺什么", r"差距", r"缺口", r"补什么技能", r"能力差距",
    ]),
    (INTENT_INDUSTRY_INFO, [
        r"行业信息", r"行业动态", r"行业日报", r"ai.*动态", r"行业新闻", r"今天.*动态", r"资讯",
    ]),
    (INTENT_JOB_SEARCH, [
        r"招聘信息", r"找工作", r"有什么岗位", r"求职", r"职位推荐", r"找.*岗位", r"有哪些.*岗位",
    ]),
    (INTENT_TASK_MANAGEMENT, [
        r"创建任务", r"创建.*任务", r"安排.*任务", r"加个任务", r"任务管理", r"待办", r"计划安排", r"提醒我", r"创建.*计划",
    ]),
]

# LLM 兜底的意图提示词
_LLM_PROMPT = """你是一个意图识别器。根据用户消息，从以下意图中选出一个最合适的，只输出 JSON，不要解释：
{intents}

用户消息：{message}

输出格式：{{"intent": "INTENT_NAME", "confidence": 0.0~1.0}}"""


@dataclass
class IntentResult:
    intent: str = INTENT_GENERAL_CHAT
    confidence: float = 0.0
    method: str = "rule"               # rule / llm
    hints: list[str] = field(default_factory=list)   # 命中的规则或依据


class IntentDetector:
    """意图识别：Rule + LLM 混合模式（docs/05-architecture.md §18）。"""

    def __init__(self, use_llm: bool = True) -> None:
        self.use_llm = use_llm

    def detect(self, message: str, entities: list | None = None) -> IntentResult:
        rule_result = self._detect_by_rule(message, entities)
        if rule_result.intent != INTENT_GENERAL_CHAT:
            return rule_result

        if self.use_llm:
            llm_result = self._detect_by_llm(message)
            if llm_result is not None:
                return llm_result

        return rule_result

    def _detect_by_rule(self, message: str, entities: list | None = None) -> IntentResult:
        text = message.lower()
        for intent, patterns in _RULES:
            for pat in patterns:
                if re.search(pat, text):
                    return IntentResult(intent=intent, confidence=0.9, method="rule", hints=[pat])

        # 兜底：若感知到完整的 JD 文本，默认进入 JD 分析
        if entities:
            for e in entities:
                etype = e.get("type") if isinstance(e, dict) else getattr(e, "type", None)
                if etype == "jd":
                    return IntentResult(
                        intent=INTENT_JD_ANALYSIS,
                        confidence=0.75,
                        method="rule",
                        hints=["jd_entity"],
                    )

        return IntentResult(intent=INTENT_GENERAL_CHAT, confidence=0.3, method="rule")

    def _detect_by_llm(self, message: str) -> IntentResult | None:
        try:
            from app.llm.client import LLMClient
            from app.llm.parser import parse_json

            client = LLMClient()
            prompt = _LLM_PROMPT.format(
                intents=", ".join(ALL_INTENTS),
                message=message[:500],
            )
            raw = client.chat(prompt, messages=[])
            data = parse_json(raw)
            intent = str(data.get("intent", "")).upper()
            if intent not in ALL_INTENTS:
                return None
            return IntentResult(
                intent=intent,
                confidence=float(data.get("confidence", 0.6)),
                method="llm",
            )
        except Exception as exc:  # LLM 失败则回退到 rule 结果
            logger.warning("intent LLM 识别失败，回退规则: %s", exc)
            return None