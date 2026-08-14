from __future__ import annotations

from app.core.exceptions import ToolError
from app.core.logging import get_logger
from app.core.tracing import trace
from app.state import AgentState
from app.tools.registry import instantiate

logger = get_logger("agent.executor")


class Executor:
    """步骤执行器（docs/05-architecture.md §22 Executor）。

    执行当前 step：若有指定工具则调用工具，否则记录为 LLM/规则动作。
    - 工具未注册（LLM 幻觉出的名字）→ 非致命跳过（ok=True, skipped=True）；
    - 工具真实执行异常 → 记录失败（ok=False），由 Decision 决定 retry。
    执行记录写入 AgentState.tool_calls / observations。
    """

    def run(self, state: AgentState, step: dict) -> dict:
        with trace("executor"):
            tool_name = _normalize_tool(step.get("tool") or "")
            result: dict = {"step": step.get("name", ""), "tool": tool_name, "ok": True}

            if tool_name in {"", "llm", "reasoning"}:
                result["note"] = "由下游 Agent 处理（LLM/规则）"
            else:
                try:
                    tool = instantiate(tool_name)
                except ToolError as exc:
                    logger.warning("executor tool 未注册，跳过: %s", exc)
                    result["skipped"] = True
                    result["note"] = "工具未注册，跳过"
                else:
                    try:
                        kw = self._collect_kwargs(state, step)
                        output = tool.execute(**{k: v for k, v in kw.items() if v is not None})
                        result["output"] = output
                        state.tool_calls.append({"tool": tool_name, "step": step.get("name", ""), "output": output})
                    except Exception as exc:
                        logger.warning("executor 执行异常: %s", exc)
                        result["ok"] = False
                        result["error"] = str(exc)

            state.observations.append(result)
            logger.info("executor step=%s tool=%s ok=%s", step.get("name", ""), tool_name, result["ok"])
            return result

    def _collect_kwargs(self, state: AgentState, step: dict) -> dict:
        """根据工具差异准备参数。"""
        action = step.get("action", "")
        return {"text": state.user_input, "user_id": state.user_id}


def _normalize_tool(name: str) -> str:
    """LLM 可能输出 无/None/对应工具 等占位符，统一归一为空（不调用工具）。"""
    if name in {"", "无", "None", "none", "对应工具", "不需要", "-", "null", "N/A"}:
        return ""
    return name.strip()