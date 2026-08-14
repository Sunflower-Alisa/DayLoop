from __future__ import annotations

from app.agents.bootstrap import build_router
from app.agents.registry import list_agents
from app.perception.intent import ALL_INTENTS


def test_router_registers_all_intents():
    router = build_router()
    for intent in ALL_INTENTS:
        assert intent in router.intents(), f"缺失意图: {intent}"


def test_router_handlers_callable():
    router = build_router()
    for intent in ALL_INTENTS:
        handler = router.route(intent)
        assert callable(handler)


def test_router_unknown_intent_returns_general_chat():
    router = build_router()
    handler = router.route("NOT_A_REAL_INTENT")
    assert callable(handler)


def test_all_agents_registered():
    agents = list_agents()
    assert "industry_info" in agents
    assert "job_search" in agents
    assert "jd_analysis" in agents
    assert "skill_gap" in agents
    assert "interview_knowledge" in agents
    assert "mock_interview" in agents
    assert "resume_update" in agents
    assert "task_management" in agents
    assert "general_chat" in agents


def test_web_search_tool_registered():
    from app.tools.registry import list_tools

    assert "web_search" in list_tools()
    assert "jd_parser" in list_tools()