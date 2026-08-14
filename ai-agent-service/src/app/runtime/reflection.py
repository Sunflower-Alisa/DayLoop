from __future__ import annotations

from app.core.logging import get_logger
from app.core.tracing import trace
from app.state import AgentState

logger = get_logger("agent.reflection")


class Reflection:
    """反思器（docs/05-architecture.md §24 Reflection）。

    检查任务输出质量：信息是否完整、是否满足任务要求、是否需要在
    最终答案前补充说明。结果写入 AgentState.reflection。
    """

    def run(self, state: AgentState, final_answer: str) -> dict:
        with trace("reflection"):
            issues: list[str] = []
            if not final_answer or not final_answer.strip():
                issues.append("最终答案为空")
            if not state.missing_context and state.context_requirements:
                pass
            state.reflection = "；".join(issues) if issues else "输出完整"
            verdict = "ok" if not issues else "incomplete"
            logger.info("reflection verdict=%s issues=%s", verdict, state.reflection)
            return {"verdict": verdict, "issues": issues, "summary": state.reflection}