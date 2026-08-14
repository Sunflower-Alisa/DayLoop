from __future__ import annotations

from typing import Callable

from app.core.exceptions import AgentError
from app.core.logging import get_logger
from app.perception.intent import (
    INTENT_GENERAL_CHAT,
    INTENT_INDUSTRY_INFO,
    INTENT_INTERVIEW_KNOWLEDGE,
    INTENT_JD_ANALYSIS,
    INTENT_JOB_SEARCH,
    INTENT_MOCK_INTERVIEW,
    INTENT_RESUME_UPDATE,
    INTENT_SKILL_GAP,
    INTENT_TASK_MANAGEMENT,
)
from app.state import AgentState

logger = get_logger("agent.router")

# 处理器：state -> dict（处理结果，如 final_answer / 建议动作）
Handler = Callable[[AgentState], dict]


class IntentRouter:
    """意图路由器（docs/05-architecture.md §18 Intent Router）。

    把 Perception 识别出的 intent 路由到对应 Use Case 处理器。
    未注册的意图落入 GENERAL_CHAT 兜底。
    """

    def __init__(self) -> None:
        self._handlers: dict[str, Handler] = {}

    def register(self, intent: str, handler: Handler) -> None:
        self._handlers[intent] = handler
        logger.info("intent router 注册: %s -> %s", intent, getattr(handler, "__name__", handler))

    def get(self, intent: str) -> Handler | None:
        return self._handlers.get(intent)

    def route(self, intent: str) -> Handler:
        """返回 intent 对应的处理器；未注册则返回通用对话处理器（若无则抛错）。"""
        handler = self._handlers.get(intent) or self._handlers.get(INTENT_GENERAL_CHAT)
        if handler is None:
            raise AgentError(f"intent 无可用处理器: {intent}")
        return handler

    def handle(self, intent: str, state: AgentState) -> dict:
        handler = self.route(intent)
        result = handler(state)
        logger.info("intent router | %s -> %s done", intent, getattr(handler, "__name__", handler))
        return result

    def intents(self) -> list[str]:
        return sorted(self._handlers)
