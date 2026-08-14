from __future__ import annotations

from typing import Any, TypedDict

from langgraph.graph import END, START, StateGraph
from langgraph.graph.state import CompiledStateGraph

from app.agents.bootstrap import build_router
from app.core.logging import get_logger
from app.runtime.decision import Decision
from app.runtime.executor import Executor
from app.runtime.planner import Planner
from app.state import AgentState, from_perception

logger = get_logger("agent.graph")


class GraphState(TypedDict, total=False):
    """LangGraph State：包装 AgentState 与编排元信息。"""

    agent: Any                    # AgentState
    steps: list[dict]             # Planner 输出
    step_index: int               # 当前步骤索引
    executor_result: dict         # 上一步执行结果
    decision: str                 # continue / retry / finish
    final_answer: str
    error: str


def _perception(state: GraphState) -> GraphState:
    from app.perception.perception import PerceptionService

    agent = state.get("agent")
    if agent is not None and (agent.user_input or agent.intent):
        return state
    result = PerceptionService().perceive(message=state.get("user_input") or "")
    agent = AgentState()
    from_perception(agent, result)
    return {"agent": agent}


def _context(state: GraphState) -> GraphState:
    agent: AgentState = state["agent"]
    from app.context.manager import ContextManager
    from app.perception.result import PerceptionResult

    result = PerceptionResult(
        text=agent.user_input or "",
        modality=agent.perception_raw.get("modality", "text"),
        intent=agent.intent,
        intent_confidence=agent.perception_raw.get("intent_confidence", 0.0),
        entities=agent.entities,
        context_requirements=agent.context_requirements,
        task=agent.task,
    )
    ctx = ContextManager().build(
        result,
        user_id=agent.user_id or None,
        session_id=agent.session_id or None,
        conversation_history=agent.conversation_history,
        plan=agent.plan or None,
        current_step=agent.current_step,
    )
    agent.metadata["context"] = ContextManager().to_prompt(ctx)
    agent.missing_context = list(getattr(ctx, "missing", []))
    return {"agent": agent}


def _planner(state: GraphState) -> GraphState:
    agent: AgentState = state["agent"]
    steps = Planner().run(agent)
    return {"agent": agent, "steps": steps, "step_index": 0}


def _executor(state: GraphState) -> GraphState:
    agent: AgentState = state["agent"]
    steps: list[dict] = state["steps"]
    idx: int = state.get("step_index", 0)
    step = steps[idx] if idx < len(steps) else {"name": "finalize", "action": "汇总输出", "tool": ""}
    result = Executor().run(agent, step)
    return {"agent": agent, "executor_result": result, "step_index": idx + 1}


def _decision(state: GraphState) -> GraphState:
    agent: AgentState = state["agent"]
    steps: list[dict] = state["steps"]
    idx: int = state.get("step_index", 0)
    decision = Decision().run(agent, steps, idx - 1, state.get("executor_result") or {})
    return {"agent": agent, "decision": decision}


def _route_after_decision(state: GraphState) -> str:
    decision = state.get("decision") or "continue"
    if decision in {"finish", "replan"}:
        return "finish"
    return "continue"


def _finalize(state: GraphState) -> GraphState:
    agent: AgentState = state["agent"]
    # 若 Agent 尚未产出 final_answer（规则编排兜底），组装简单总结
    if not agent.final_answer:
        agent.final_answer = f"（任务已完成）{agent.current_step or agent.task or '处理完成'}"
    reflection = state.get("reflect_result") or {}
    return {"agent": agent, "final_answer": agent.final_answer, "reflect_result": reflection}


def build_graph(compile: bool = True) -> CompiledStateGraph:
    """构建 LangGraph 编排图（docs/05-architecture.md §21-§24）。

    节点：perception → context → planner → executor → decision（循环）→ finalize。
    若 use case 已在 IntentRouter 注册专用 Agent，直接走 Agent 单步执行，
    否则走通用 Planner/Executor/Decision/Reflection 编排。
    """
    builder = StateGraph(GraphState)

    builder.add_node("perception", _perception)
    builder.add_node("context", _context)
    builder.add_node("planner", _planner)
    builder.add_node("executor", _executor)
    builder.add_node("decision", _decision)
    builder.add_node("finalize", _finalize)

    builder.add_edge(START, "perception")
    builder.add_edge("perception", "context")
    builder.add_edge("context", "planner")
    builder.add_edge("planner", "executor")
    builder.add_edge("executor", "decision")
    builder.add_conditional_edges(
        "decision",
        _route_after_decision,
        {
            "continue": "executor",
            "retry": "executor",
            "finish": "finalize",
        },
    )
    builder.add_edge("finalize", END)

    graph = builder.compile()
    logger.info("LangGraph 编排图构建完成")
    return graph


def run_agent_flow(state: GraphState) -> dict:
    """执行编排：先查 IntentRouter 专用 Agent，命中则直通；否则走通用编排。"""
    agent: AgentState = state.get("agent")
    if agent is None:
        agent = AgentState()

    # 已在 Router 注册的 Use Case 走专用 Agent
    if agent.intent:
        router = build_router()
        handler = router.get(agent.intent)
        if handler is not None:
            _ = handler(agent)
            if not agent.final_answer:
                agent.final_answer = agent.task or "处理完成"
            return {"agent": agent, "final_answer": agent.final_answer}

    graph = build_graph()
    result = graph.invoke({**state, "agent": agent})
    out: AgentState = result.get("agent", agent)
    return {"agent": out, "final_answer": out.final_answer}