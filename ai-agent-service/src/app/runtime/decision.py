from __future__ import annotations

from app.core.logging import get_logger
from app.core.tracing import trace
from app.state import AgentState

logger = get_logger("agent.decision")


class Decision:
    """决策器（docs/05-architecture.md §23 Decision）。

    依据当前 step 执行结果判断下一步动作：
    - tool_call: 还需要调用工具
    - continue:  进入下一个步骤
    - retry:     当前步骤失败，重试
    - replan:    计划需要调整
    - finish:    全部步骤完成，输出最终答案
    """

    def run(self, state: AgentState, steps: list[dict], step_index: int, result: dict) -> str:
        with trace("decision"):
            ok = result.get("ok", True)
            if not ok:
                state.decision = "retry"
                logger.info("decision=%s (step %s failed)", state.decision, step_index)
                return state.decision
            if step_index + 1 >= len(steps):
                state.decision = "finish"
            else:
                state.decision = "continue"
            logger.info("decision=%s step_index=%d/%d", state.decision, step_index + 1, len(steps))
            return state.decision