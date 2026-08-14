from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any


# 上下文块类型的标准名称（与意图的 context_requirements 一一对应）
CTX_CURRENT_INPUT = "current_input"
CTX_CONVERSATION = "conversation_history"
CTX_USER_PROFILE = "user_profile"
CTX_RESUME = "resume"
CTX_SKILL_PROFILE = "skill_profile"
CTX_JOB_PREFERENCE = "job_preference"
CTX_TARGET_POSITION = "target_position"
CTX_MEMORY = "long_term_memory"
CTX_KNOWLEDGE = "knowledge"
CTX_TASKS = "tasks"
CTX_TASK = "current_task"
CTX_PLAN = "plan"
CTX_TOOL_RESULTS = "tool_results"


@dataclass
class ContextBlock:
    """一块上下文数据。

    name:   上下文块类型（对应 CTX_* 常量）
    content: 格式化后的文本内容（最终拼接进 prompt）
    source:  数据来源（dayloop / rag / memory / session / external）
    priority: 拼接顺序，数值越小越靠前
    token_estimate: 预估 token 数（用于预算裁剪）
    metadata: 附加信息（更新时间、置信度、原始 JSON 等）
    """

    name: str
    content: str = ""
    source: str = "session"
    priority: int = 100
    token_estimate: int = 0
    metadata: dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> dict:
        return {
            "name": self.name,
            "content": self.content,
            "source": self.source,
            "priority": self.priority,
            "token_estimate": self.token_estimate,
            "metadata": self.metadata,
        }


@dataclass
class AgentContext:
    """一次请求构建出的完整上下文。

    原则（05-architecture.md §19）：
    只包含当前任务需要的必要信息，不把所有用户数据塞给 LLM。

    - requirements:   本次任务声明的上下文需求（来自 PerceptionResult）
    - blocks:         实际加载的上下文块，按 priority 排序
    - missing:        声明了但未能加载的上下文块名（供日志 / 降级策略）
    - task / plan / current_step: 当前任务状态（Working Memory）
    - estimates:      预算信息（total_tokens, max_tokens, truncated）
    """

    requirements: list[str] = field(default_factory=list)
    blocks: list[ContextBlock] = field(default_factory=list)
    missing: list[str] = field(default_factory=list)
    task: str = ""
    plan: list[str] = field(default_factory=list)
    current_step: str = ""
    estimates: dict[str, Any] = field(default_factory=dict)

    @property
    def has_text(self) -> bool:
        """是否存在可用的上下文文本。"""
        return any(b.content for b in self.blocks)

    def block(self, name: str) -> ContextBlock | None:
        for b in self.blocks:
            if b.name == name:
                return b
        return None

    def format(self) -> str:
        """把上下文块拼成一个具备章节结构的文本（供 LLM prompt 使用）。"""
        parts: list[str] = []
        for b in sorted(self.blocks, key=lambda x: x.priority):
            if not b.content:
                continue
            parts.append(f"## {b.name}\n{b.content}")
        return "\n\n".join(parts)

    def to_dict(self) -> dict:
        return {
            "requirements": self.requirements,
            "blocks": [b.to_dict() for b in self.blocks],
            "missing": self.missing,
            "task": self.task,
            "plan": self.plan,
            "current_step": self.current_step,
            "estimates": self.estimates,
        }