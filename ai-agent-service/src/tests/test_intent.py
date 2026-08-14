from __future__ import annotations

import pytest

from app.perception.intent import (
    INTENT_GENERAL_CHAT,
    INTENT_INDUSTRY_INFO,
    INTENT_INTERVIEW_KNOWLEDGE,
    INTENT_JD_ANALYSIS,
    INTENT_JOB_SEARCH,
    INTENT_MOCK_INTERVIEW,
    INTENT_RESUME_UPDATE,
    INTENT_SKILL_GAP,
    INTENT_TASK_MANAGEMENT,
    IntentDetector,
)


@pytest.fixture
def detector():
    return IntentDetector(use_llm=False)


def test_intent_mock_interview(detector):
    assert detector.detect("开始模拟面试吧").intent == INTENT_MOCK_INTERVIEW


def test_intent_jd_analysis(detector):
    assert detector.detect("帮我分析这个JD值不值得投").intent == INTENT_JD_ANALYSIS


def test_intent_skill_gap(detector):
    assert detector.detect("我有哪些技能差距").intent == INTENT_SKILL_GAP


def test_intent_industry_info(detector):
    assert detector.detect("今天有什么AI行业动态").intent == INTENT_INDUSTRY_INFO


def test_intent_job_search(detector):
    assert detector.detect("帮我找找工作岗位").intent == INTENT_JOB_SEARCH


def test_intent_interview_knowledge(detector):
    assert detector.detect("整理一下我的面试题").intent == INTENT_INTERVIEW_KNOWLEDGE


def test_intent_resume_update(detector):
    assert detector.detect("更新一下我的简历").intent == INTENT_RESUME_UPDATE


def test_intent_task_management(detector):
    assert detector.detect("帮我创建明天上午10点学习Python的任务").intent == INTENT_TASK_MANAGEMENT


def test_intent_general_chat_fallback(detector):
    assert detector.detect("今天天气怎么样").intent == INTENT_GENERAL_CHAT


def test_intent_rule_method_used(detector):
    result = detector.detect("帮我分析这个岗位")
    assert result.method == "rule"
    assert result.confidence >= 0.7