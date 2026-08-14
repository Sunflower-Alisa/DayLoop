from __future__ import annotations

from app.core.logging import get_logger
from app.core.tracing import trace
from app.llm.client import LLMClient
from app.state import AgentState

logger = get_logger("agent.general_chat")


class GeneralChatAgent:
    """通用对话 Agent（兜底处理器）。

    对未匹配到专用 Use Case 的输入进行自由对话。若配置了长期记忆，
    可附带最近记忆作为上下文。
    """

    name = "general_chat"

    def run(self, state: AgentState) -> dict:
        with trace("general_chat"):
            context = self._memory_context(state)
            prompt = state.user_input or "你好"
            msgs = [{"role": "user", "content": prompt}]
            if context:
                msgs.insert(0, {"role": "system", "content": f"以下是用户最近的记忆资料，可参考：\n{context}"})
            try:
                llm = LLMClient()
                answer = llm.chat(messages=msgs, temperature=0.7)
                answer = (answer or "").strip() or "（未生成回复）"
            except Exception as exc:
                logger.warning("general_chat LLM 失败，退回固定回复: %s", exc)
                answer = "我收到了你的消息。你可以让我帮你分析 JD、查看技能差距、模拟面试或整理面试知识。"
            state.final_answer = answer
            state.observations.append({"agent": self.name})
            logger.info("general_chat done | len=%d", len(answer))
            return {"final": answer, "final_answer": answer}

    def _memory_context(self, state: AgentState) -> str:
        if not state.retrieved_memory:
            return ""
        parts = [str(m.get("content", m.get("text", ""))) for m in state.retrieved_memory[:3]]
        return "\n".join(parts)