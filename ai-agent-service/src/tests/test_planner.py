from __future__ import annotations

from app.runtime.planner import Planner
from app.state import AgentState


def test_planner_jd_analysis_template():
    state = AgentState(intent="JD_ANALYSIS")
    steps = Planner().run(state)
    assert len(steps) == 5
    assert steps[0]["name"] == "解析 JD"
    assert state.plan == [s["name"] for s in steps]


def test_planner_job_search_template():
    state = AgentState(intent="JOB_SEARCH")
    steps = Planner().run(state)
    assert len(steps) == 4
    assert steps[0]["tool"] == "get_user_profile"


def test_planner_unknown_intent_uses_llm_or_default():
    state = AgentState(intent="UNKNOWN_INTENT", user_input="随便做什么都行")
    steps = Planner().run(state)
    # LLM 失败时退回默认计划，至少返回一个可执行步骤
    assert len(steps) >= 1


def test_planner_writes_state_plan():
    state = AgentState(intent="TASK_MANAGEMENT")
    Planner().run(state)
    assert state.plan == ["解析意图", "执行任务操作"]