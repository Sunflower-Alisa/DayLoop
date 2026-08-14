from __future__ import annotations

from functools import lru_cache

from app.agents.general_chat import GeneralChatAgent
from app.agents.industry_info import IndustryInfoAgent
from app.agents.interview import MockInterviewAgent
from app.agents.interview_knowledge import InterviewKnowledgeAgent
from app.agents.jd_analysis import JDAnalysisAgent
from app.agents.job_search import JobSearchAgent
from app.agents.registry import register_agent
from app.agents.resume_update import ResumeUpdateAgent
from app.agents.router import IntentRouter
from app.agents.skill_gap import SkillGapAgent
from app.agents.task_management import TaskManagementAgent
from app.core.logging import get_logger
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
)
from app.tools.dayloop_tools import register_dayloop_tools

logger = get_logger("agent.bootstrap")


@lru_cache(maxsize=1)
def build_router() -> IntentRouter:
    """组装 IntentRouter 并注册全部 Use Case Agent。"""
    register_dayloop_tools()
    router = IntentRouter()

    # ---- UC01 AI 行业信息 ----
    router.register(INTENT_INDUSTRY_INFO, IndustryInfoAgent().run)
    register_agent("industry_info", IndustryInfoAgent())

    # ---- UC02 AI 招聘信息 ----
    router.register(INTENT_JOB_SEARCH, JobSearchAgent().run)
    register_agent("job_search", JobSearchAgent())

    # ---- UC03 JD 分析 ----
    router.register(INTENT_JD_ANALYSIS, JDAnalysisAgent().run)
    register_agent("jd_analysis", JDAnalysisAgent())

    # ---- UC04 技能 Gap ----
    router.register(INTENT_SKILL_GAP, SkillGapAgent().run)
    register_agent("skill_gap", SkillGapAgent())

    # ---- UC05 面试知识库 ----
    router.register(INTENT_INTERVIEW_KNOWLEDGE, InterviewKnowledgeAgent().run)
    register_agent("interview_knowledge", InterviewKnowledgeAgent())

    # ---- UC06 模拟面试 ----
    router.register(INTENT_MOCK_INTERVIEW, MockInterviewAgent().run)
    register_agent("mock_interview", MockInterviewAgent())

    # ---- 简历更新 ----
    router.register(INTENT_RESUME_UPDATE, ResumeUpdateAgent().run)
    register_agent("resume_update", ResumeUpdateAgent())

    # ---- 任务管理 ----
    router.register(INTENT_TASK_MANAGEMENT, TaskManagementAgent().run)
    register_agent("task_management", TaskManagementAgent())

    # ---- 通用对话兜底 ----
    router.register(INTENT_GENERAL_CHAT, GeneralChatAgent().run)
    register_agent("general_chat", GeneralChatAgent())

    logger.info("router 就绪，intents=%s", router.intents())
    return router