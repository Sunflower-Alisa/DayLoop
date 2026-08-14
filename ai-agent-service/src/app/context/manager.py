from __future__ import annotations

from app.context.builder import ContextBuilder
from app.context.schema import AgentContext, ContextBlock
from app.core.logging import get_logger
from app.perception.result import PerceptionResult

logger = get_logger("context.manager")


class ContextManager:
    """上下文管理层（docs/05-architecture.md §19）。

    职责：
    1. 接收 PerceptionResult，解析 context_requirements；
    2. 通过 ContextBuilder 按需加载上下文（DayLoop / RAG / Memory / 会话）；
    3. 提供 `to_prompt()` —— 将上下文拼接为适合发送给 LLM 的 prompt 文本；
    4. 提供 `merge()` —— 把执行结果（Tool Results / Plan）增量写回 Working Memory。

    原则：不将所有用户数据全部发送给 LLM，只选择当前任务必要的信息。
    """

    def __init__(self, builder: ContextBuilder | None = None) -> None:
        self.builder = builder or ContextBuilder()

    def build(
        self,
        result: PerceptionResult,
        *,
        user_id: str | None = None,
        session_id: str | None = None,
        conversation_history: list[dict] | None = None,
        plan: list[str] | None = None,
        current_step: str = "",
        max_tokens: int = 2000,
    ) -> AgentContext:
        """基于感知结果构建当前任务的上下文。"""
        return self.builder.build(
            result,
            user_id=user_id,
            session_id=session_id,
            conversation_history=conversation_history,
            plan=plan,
            current_step=current_step,
            max_tokens=max_tokens,
        )

    def to_prompt(self, ctx: AgentContext) -> str:
        """把上下文渲染为一段结构化的 LLM prompt 文本。"""
        sections = [
            f"[系统上下文] 当前任务：{ctx.task or '通用'}; 当前步骤：{ctx.current_step or '-'}"
        ]
        prompt_text = ctx.format()
        if prompt_text:
            sections.append(prompt_text)
        if ctx.missing:
            sections.append("[缺失上下文] " + ", ".join(ctx.missing))
        return "\n\n".join(sections)

    def merge(
        self, ctx: AgentContext, *, tool_results: list[dict] | None = None, plan: list[str] | None = None
    ) -> AgentContext:
        """执行过程中增量更新 Working Memory（Tool Results / Plan）。"""
        if tool_results:
            for res in tool_results:
                ctx.blocks.append(
                    ContextBlock(
                        name="tool_results",
                        content=str(res),
                        source="tool",
                        priority=70,
                        token_estimate=len(str(res)) // 2,
                        metadata={"tool": res.get("tool")} if isinstance(res, dict) else {},
                    )
                )
        if plan is not None:
            ctx.plan = plan
        return ctx