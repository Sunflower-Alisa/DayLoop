from __future__ import annotations

import json

from app.core.logging import get_logger
from app.core.tracing import trace
from app.llm.client import LLMClient
from app.llm.parser import parse_json
from app.state import AgentState

logger = get_logger("agent.planner")

SYSTEM_PLANNER = """你是任务规划器。把给定任务拆解成按顺序执行的步骤列表，每步包含动作与所需工具。
只输出 JSON：{"steps": [{"name": "...", "action": "...", "tool": "..."}]}，不要其他内容。"""


class Planner:
    """任务计划器（docs/05-architecture.md §21 Planner）。

    依据 intent 与上下文，把任务拆成可执行步骤。先按规则模板生成，
    复杂任务再交给 LLM 细化。结果写入 AgentState.plan。
    """

    # intent -> 固定步骤模板（规则路线，离线可用）
    _PLAN_TEMPLATES = {
        "JD_ANALYSIS": [
            {"name": "解析 JD", "action": "解析原始 JD 文本为结构化信息", "tool": "jd_parser"},
            {"name": "获取用户画像", "action": "读取用户简历与技能画像", "tool": "get_resume"},
            {"name": "技能匹配", "action": "将 JD 技能与用户技能比对", "tool": ""},
            {"name": "差距分析", "action": "识别能力差距与风险", "tool": ""},
            {"name": "投递建议", "action": "综合给出投递建议", "tool": ""},
        ],
        "SKILL_GAP": [
            {"name": "提取用户技能", "action": "从简历/画像抽取技能", "tool": ""},
            {"name": "提取 JD 技能", "action": "解析目标岗位 JD 技能", "tool": "jd_parser"},
            {"name": "技能匹配", "action": "比对相差技能", "tool": ""},
            {"name": "差距排序", "action": "按重要性排序差距", "tool": ""},
            {"name": "学习建议", "action": "生成学习路线建议", "tool": ""},
        ],
        "MOCK_INTERVIEW": [
            {"name": "生成面试计划", "action": "根据画像与岗位制定面试计划", "tool": ""},
            {"name": "提问与评估", "action": "动态展开模拟面试问答", "tool": ""},
            {"name": "总结报告", "action": "生成面试总结并更新画像", "tool": ""},
        ],
        "INDUSTRY_INFO": [
            {"name": "确定关注范围", "action": "明确关注公司与方向", "tool": ""},
            {"name": "收集信息", "action": "获取行业动态线索", "tool": ""},
            {"name": "总结日报", "action": "生成行业日报", "tool": ""},
        ],
        "JOB_SEARCH": [
            {"name": "读取求职画像", "action": "读取用户画像与目标岗位", "tool": "get_user_profile"},
            {"name": "获取岗位", "action": "获取候选招聘信息", "tool": "get_jobs"},
            {"name": "匹配排序", "action": "计算匹配度并排序", "tool": ""},
            {"name": "投递建议", "action": "生成投递建议", "tool": ""},
        ],
        "INTERVIEW_KNOWLEDGE": [
            {"name": "获取素材", "action": "获取新增笔记/资料", "tool": ""},
            {"name": "提取面试题", "action": "提取问题与知识点", "tool": ""},
            {"name": "入库", "action": "去重并写入知识库", "tool": ""},
        ],
        "RESUME_UPDATE": [
            {"name": "读取简历", "action": "获取现有简历", "tool": "get_resume"},
            {"name": "生成新简历", "action": "结合要点生成新版简历", "tool": ""},
            {"name": "写回", "action": "保存简历到 DayLoop", "tool": "update_resume"},
        ],
        "TASK_MANAGEMENT": [
            {"name": "解析意图", "action": "识别任务操作", "tool": ""},
            {"name": "执行任务操作", "action": "调用任务 API", "tool": "get_tasks"},
        ],
    }

    def run(self, state: AgentState) -> list[dict]:
        with trace("planner"):
            steps = self._PLAN_TEMPLATES.get(state.intent, [])
            if not steps:
                steps = self._llm_plan(state)
            state.plan = [s.get("name", "") for s in steps]
            logger.info("planner done | intent=%s steps=%d", state.intent, len(steps))
            return steps

    def _llm_plan(self, state: AgentState) -> list[dict]:
        task = state.task or state.user_input or ""
        try:
            llm = LLMClient()
            raw = llm.chat(
                messages=[
                    {"role": "system", "content": SYSTEM_PLANNER},
                    {"role": "user", "content": f"任务：{task}\n请输出拆解后的步骤 JSON。"},
                ],
                temperature=0.3,
            )
            parsed = parse_json(raw)
            return parsed.get("steps", [])
        except Exception as exc:
            logger.warning("planner LLM 失败，退回默认计划: %s", exc)
            return [{"name": "执行任务", "action": "调用相关能力完成任务", "tool": ""}]