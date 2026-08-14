from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any


@dataclass
class AgentState:
    """统一 Agent 状态（docs/05-architecture.md §20 Agent State）。

    贯穿 Perception → Intent Router → Context → Planner → Executor → Decision
    → Reflection 全过程。设计为普通 dataclass，便于序列化/日志/评测；
    LangGraph 场景可再封装成 TypedDict State。
    """

    session_id: str = ""
    user_id: str = ""

    # ==== Perception 输出 ====
    intent: str = ""
    user_input: str = ""
    entities: list[dict] = field(default_factory=list)
    context_requirements: list[str] = field(default_factory=list)
    perception_raw: dict[str, Any] = field(default_factory=dict)

    # ==== Context ====
    conversation_history: list[dict] = field(default_factory=list)
    user_profile: dict[str, Any] = field(default_factory=dict)
    resume: str = ""
    skill_profile: dict[str, Any] = field(default_factory=dict)
    retrieved_memory: list[dict] = field(default_factory=list)
    retrieved_knowledge: list[dict] = field(default_factory=list)
    missing_context: list[str] = field(default_factory=list)

    # ==== Working / Executor ====
    task: str = ""
    plan: list[str] = field(default_factory=list)
    current_step: str = ""
    tool_calls: list[dict] = field(default_factory=list)
    observations: list[dict] = field(default_factory=list)

    # ==== Decision / Reflection ====
    decision: str = ""                        # continue / finish / retry / replan / tool_call
    reflection: str = ""
    loops: int = 0
    max_loops: int = 10

    # ==== 最终输出 ====
    final_answer: str = ""
    evaluation: dict[str, Any] = field(default_factory=dict)

    # ==== 通用元信息 ====
    metadata: dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> dict:
        return {
            "session_id": self.session_id,
            "user_id": self.user_id,
            "intent": self.intent,
            "user_input": self.user_input,
            "entities": self.entities,
            "context_requirements": self.context_requirements,
            "conversation_history": self.conversation_history,
            "user_profile": self.user_profile,
            "resume": self.resume,
            "skill_profile": self.skill_profile,
            "retrieved_memory": self.retrieved_memory,
            "retrieved_knowledge": self.retrieved_knowledge,
            "missing_context": self.missing_context,
            "task": self.task,
            "plan": self.plan,
            "current_step": self.current_step,
            "tool_calls": self.tool_calls,
            "observations": self.observations,
            "decision": self.decision,
            "reflection": self.reflection,
            "loops": self.loops,
            "max_loops": self.max_loops,
            "final_answer": self.final_answer,
            "evaluation": self.evaluation,
        }


def from_perception(state: AgentState, result: Any) -> AgentState:
    """把 PerceptionResult 写入 AgentState。"""
    state.intent = result.intent
    state.user_input = result.raw or ""
    state.entities = result.entities
    state.context_requirements = list(result.context_requirements)
    state.task = result.task
    state.perception_raw = {
        "modality": result.modality,
        "intent_confidence": result.intent_confidence,
        "intent_method": result.metadata.get("intent_method"),
    }
    return state