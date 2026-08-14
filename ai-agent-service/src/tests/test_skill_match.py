from __future__ import annotations

from app.agents.skill_match import (
    extract_skills_from_profile,
    extract_skills_from_text,
    normalize_skill,
    skill_matching,
)


def test_normalize_skill():
    assert normalize_skill("Python") == "python"
    assert normalize_skill("Tool Calling") == "toolcalling"
    assert normalize_skill("C++") == "c++"


def test_extract_skills_from_text():
    skills = extract_skills_from_text("我熟悉 Python、RAG、LangGraph 和 FastAPI")
    assert "Python" in skills
    assert "RAG" in skills
    assert "LangGraph" in skills


def test_extract_skills_from_profile_strings():
    assert extract_skills_from_profile({"skills": ["Python", "RAG"]}) == ["Python", "RAG"]


def test_extract_skills_from_profile_dicts():
    profile = {"skills": [{"name": "Python", "level": "熟练"}, {"name": "LangChain"}]}
    assert extract_skills_from_profile(profile) == ["Python", "LangChain"]


def test_skill_matching_full_match():
    result = skill_matching(["Python", "RAG", "LangChain"], ["Python", "RAG", "LangChain"])
    assert result["matched"] == ["Python", "RAG", "LangChain"]
    assert result["missing"] == []
    assert result["match_score"] >= 0.9


def test_skill_matching_partial_and_missing():
    result = skill_matching(["Python"], ["Python", "RAG", "LangGraph"])
    assert result["matched"] == ["Python"]
    assert set(result["missing"]) == {"RAG", "LangGraph"}
    assert result["match_score"] < 100


def test_skill_matching_no_skills():
    result = skill_matching([], ["RAG"])
    assert result["matched"] == []
    assert result["missing"] == ["RAG"]
    assert result["match_score"] == 0