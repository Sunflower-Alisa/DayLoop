from __future__ import annotations

import json

from app.agents.skill_match import (
    extract_skills_from_profile,
    extract_skills_from_text,
    skill_matching,
)
from app.core.logging import get_logger
from app.core.tracing import trace
from app.llm.client import LLMClient
from app.llm.parser import parse_json
from app.state import AgentState
from app.tools.jd_parser import JDParserTool

logger = get_logger("agent.skill_gap")

SYSTEM_SKILL_GAP = """你是求职能力诊断顾问。基于用户技能画像与目标岗位 JD，输出能力差距诊断。
要求用 JSON 输出，字段包含：
- mastered: 已掌握技能列表
- partial: 部分掌握技能列表（含理由）
- missing: 未掌握技能列表（含理由）
- hidden_gaps: 隐性差距（JD 未明说但岗位通常需要的能力，字符串）
- learning_suggestions: 按优先级排序的学习建议列表
- updated_skill_profile: 建议更新的技能画像（可合并的新技能）
只输出 JSON，不要输出其他内容。"""


class SkillGapAgent:
    """技能 Gap 分析 Agent（docs/05-architecture.md §34 / UC04）。

    流程：提取用户技能 → 提取 JD 技能 → 匹配 → Gap 检测/排序 → LLM 生成学习建议。
    输入 AgentState.skill_profile / resume / user_input（可选 JD）；结果写入 final_answer。
    """

    name = "skill_gap"

    def run(self, state: AgentState) -> dict:
        user_skills = self._get_user_skills(state)
        jd = self._get_jd(state)
        if not jd.get("skills"):
            state.final_answer = (
                "🎯 技能 Gap 分析需要目标岗位的 JD 才能进行。请把目标岗位的 JD 文本发给我，"
                "或先告诉我目标岗位（如 AI Agent 应用开发 / AI 产品经理 / FDE）。"
            )
            logger.info("skill_gap | 缺少 JD，请求用户提供")
            return {"ok": False, "reason": "missing_jd", "final": state.final_answer}

        with trace("skill_gap"):
            match = skill_matching(user_skills, jd.get("skills") or [])
            reasoning = self._llm_reason(state, jd, match)
            result = {
                "match": match,
                "reasoning": reasoning,
                "final": reasoning,
            }
            state.observations.append({"agent": self.name, "skill_match": match})
            state.final_answer = _render_answer(jd, match, reasoning)
            logger.info(
                "skill_gap done | match=%d mastered=%d missing=%d",
                match["match_score"],
                len(match["matched"]),
                len(match["missing"]),
            )
            return result

    # ---- 内部 ----

    def _get_user_skills(self, state: AgentState) -> list[str]:
        skills = extract_skills_from_profile(state.skill_profile)
        if state.resume:
            skills = _dedupe(skills + extract_skills_from_text(state.resume))
        return skills

    def _get_jd(self, state: AgentState) -> dict:
        # 优先用 context 已装好的 job 信息；否则解析 user_input
        job = state.user_profile.get("job") or state.perception_raw.get("job")
        if isinstance(job, dict) and job.get("skills"):
            return job
        jd_text = (state.user_input or "").strip()
        if jd_text:
            return JDParserTool().execute(text=jd_text)
        return {}

    def _llm_reason(self, state: AgentState, jd: dict, match: dict) -> dict:
        summary = {
            "job_title": jd.get("job_title", ""),
            "jd_skills": jd.get("skills", []),
            "user_skills": match["matched"],
            "partial_skills": match["partial"],
            "missing_skills": match["missing"],
        }
        prompt = (
            "请基于以下用户技能与 JD 技能对比，输出 JSON 能力诊断：\n"
            + json.dumps(summary, ensure_ascii=False, indent=1)
        )
        try:
            llm = LLMClient()
            raw = llm.chat(
                messages=[
                    {"role": "system", "content": SYSTEM_SKILL_GAP},
                    {"role": "user", "content": prompt},
                ],
                temperature=0.3,
            )
        except Exception as exc:
            logger.warning("skill_gap LLM 失败，退回规则结论: %s", exc)
            return self._rule_based_reason(match)
        try:
            return parse_json(raw)
        except Exception as exc:
            logger.warning("skill_gap LLM 输出非 JSON，退回规则结论: %s", exc)
            return self._rule_based_reason(match)

    def _rule_based_reason(self, match: dict) -> dict:
        return {
            "mastered": match["matched"],
            "partial": match["partial"],
            "missing": match["missing"],
            "hidden_gaps": ["AI Agent 工程化经验", "大模型应用落地经验"],
            "learning_suggestions": [f"优先补齐: {s}" for s in match["missing"][:5]],
            "updated_skill_profile": match["missing"][:5],
        }


def _dedupe(items: list[str]) -> list[str]:
    seen: list[str] = []
    for it in items:
        if it not in seen:
            seen.append(it)
    return seen


def _render_answer(jd: dict, match: dict, reasoning: dict) -> str:
    title = jd.get("job_title") or "目标岗位"
    lines = [
        f"🎯 技能 Gap 分析（{title}）——匹配度 {match['match_score']}/100：",
        f"- ✅ 已掌握：{', '.join(match['matched']) if match['matched'] else '无'}",
        f"- 🟡 部分掌握：{', '.join(match['partial']) if match['partial'] else '无'}",
        f"- ❌ 未掌握：{', '.join(match['missing']) if match['missing'] else '无'}",
        "",
        "📌 学习建议（按优先级）：",
    ]
    suggestions = reasoning.get("learning_suggestions") or []
    for i, s in enumerate(suggestions[:5], 1):
        lines.append(f"{i}. {s}")
    hidden = reasoning.get("hidden_gaps") or []
    if hidden:
        lines.append("")
        lines.append("🔍 隐性差距：" + "；".join(hidden[:3]))
    return "\n".join(lines)