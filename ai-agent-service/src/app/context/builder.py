from __future__ import annotations

import logging
from typing import Any

from app.context.provider import ContextProvider, ProviderRegistry
from app.context.schema import (
    CTX_CONVERSATION,
    CTX_CURRENT_INPUT,
    CTX_PLAN,
    AgentContext,
    ContextBlock,
)
from app.perception.result import PerceptionResult

logger = logging.getLogger("agent-context.builder")

# 意图 → 上下文需求（与 perception/intent.py 的 CONTEXT_REQUIREMENTS 一致）
# 设计上不内置 onClick 处理，而是按需加载。


class ContextBuilder:
    """根据当前任务动态构建 Context（docs/05-architecture.md §19）。

    原则：不将所有用户数据全部发送给 LLM，只根据当前任务选择必要的信息。
    实际数据来源于外部 Provider（DayLoop API / RAG / Memory / 会话），
    本类只负责：需求解析 → Provider 读取 → 预算裁剪 → 拼装。
    """

    def __init__(self, providers: ProviderRegistry | None = None) -> None:
        self.providers = providers or ProviderRegistry()

    def register(self, ctx_name: str, provider: ContextProvider) -> None:
        """注册某个上下文类型对应的数据源。"""
        self.providers.register(ctx_name, provider)

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
        """基于感知结果构建上下文。

        Args:
            result: Perception 输出（含 intent / context_requirements）
            user_id: 用户标识
            session_id: 会话标识
            conversation_history: 会话历史（short-term memory）
            plan: 任务步骤计划（由 Planner 提供）
            current_step: 当前执行步骤
            max_tokens: 上下文预算上限，超出则裁剪

        Returns:
            AgentContext
        """
        requirements = list(result.context_requirements or [])
        payload = {
            "user_id": user_id,
            "session_id": session_id,
            "intent": result.intent,
        }

        ctx = AgentContext(requirements=requirements)
        blocks: list[ContextBlock] = []

        # 1. 总是带上当前输入（用户消息本身）
        if result.text:
            blocks.append(
                ContextBlock(
                    name=CTX_CURRENT_INPUT,
                    content=result.text,
                    source="session",
                    priority=1,
                    token_estimate=len(result.text) // 2,
                )
            )

        # 2. 会话历史（short-term memory）
        blocks.append(self._conversation_block(conversation_history))

        # 3. 按需加载业务上下文（只加载 requirements 声明过的数据）
        for want in requirements:
            block = self._load_requirement(want, payload)
            if block is None:
                ctx.missing.append(want)
            else:
                blocks.append(block)

        # 4. Working Memory（当前任务状态）
        if plan:
            ctx.plan = plan
            blocks.append(
                ContextBlock(
                    name=CTX_PLAN,
                    content="\n".join(f"{i + 1}. {s}" for i, s in enumerate(plan)),
                    source="agent",
                    priority=60,
                    token_estimate=len(plan) * 12,
                )
            )
        ctx.current_step = current_step or ""

        blocks.sort(key=lambda b: b.priority)

        # 5. 预算裁剪：超出 max_tokens 时从低优先级的业务块截断
        ctx.blocks, ctx.estimates = self._apply_budget(blocks, max_tokens)
        logger.info(
            "context build | requirements=%s blocks=%d missing=%s tokens=%s/%s",
            requirements,
            len(ctx.blocks),
            ctx.missing,
            ctx.estimates.get("total_tokens"),
            max_tokens,
        )
        return ctx

    def _load_requirement(self, want: str, payload: dict) -> ContextBlock | None:
        """单个需求 → 上下文块。返回块或提示缺失。"""
        return self.providers.fetch(want, payload)

    def _conversation_block(
        self, history: list[dict] | None, max_turns: int = 8
    ) -> ContextBlock:
        if not history:
            return ContextBlock(name=CTX_CONVERSATION, content="", priority=5)
        recent = history[-max_turns:]
        lines = [f"{h.get('role', 'user')}: {h.get('content', '')}" for h in recent]
        text = "\n".join(lines)
        return ContextBlock(
            name=CTX_CONVERSATION,
            content=text,
            source="session",
            priority=5,
            token_estimate=len(text) // 2,
        )

    def _apply_budget(
        self, blocks: list[ContextBlock], max_tokens: int
    ) -> tuple[list[ContextBlock], dict[str, Any]]:
        total = sum(b.token_estimate for b in blocks)
        kept: list[ContextBlock] = []

        for b in blocks:
            if not b.content:
                continue
            if total <= max_tokens or b.name in (CTX_CURRENT_INPUT, CTX_CONVERSATION):
                kept.append(b)
                continue
            # 超出预算：优先截断非关键块（保留前 max_tokens/2 token）
            budget_left = max_tokens - sum(
                b2.token_estimate for b2 in kept if b2.token_estimate
            )
            if budget_left > 0:
                b_keep = ContextBlock(
                    name=b.name,
                    content=b.content[: budget_left * 2],
                    source=b.source,
                    priority=b.priority,
                    token_estimate=budget_left,
                    metadata=b.metadata,
                )
                kept.append(b_keep)
            else:
                ctx_name = b.name
                logger.info("context 预算超限，丢弃块: %s", ctx_name)

        return kept, {"total_tokens": total, "max_tokens": max_tokens}