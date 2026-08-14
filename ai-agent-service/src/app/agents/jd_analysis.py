from __future__ import annotations

import json
import re

from app.agents.skill_match import (
    extract_skills_from_profile,
    extract_skills_from_text,
    skill_matching,
)
from app.core.exceptions import AgentError
from app.core.logging import get_logger
from app.core.tracing import trace
from app.llm.client import LLMClient
from app.llm.parser import parse_json
from app.state import AgentState
from app.tools.jd_parser import JDParserTool

logger = get_logger("agent.jd_analysis")

SYSTEM_JD_ANALYSIS = """你是覆盖 AI 岗位的求职顾问。根据结构化的 JD 信息和用户技能画像，输出客观的 JD 分析结论。
要求用 JSON 输出，字段包含：
- match_score: 0-100 整数匹配度
- strengths: 用户在该岗位上的优势列表（字符串）
- gaps: 用户的能力差距（字符串，含依据）
- missing_skills: 用户缺失的核心技能列表
- recommendation: 投递建议, 取值 "投递" / "谨慎投递" / "不投"，一句话理由
只输出 JSON，不要输出其他内容。"""


class JDAnalysisAgent:
    """JD 分析 Agent（docs/05-architecture.md §32 / UC03）。

    流程：解析 JD → 获取用户技能画像 → 技能匹配 → Gap 分析 → LLM 推理 → 投递建议。
    输入 AgentState.user_input（原始 JD 文本）；结果写入 final_answer 并返回结构化 dict。
    """

    name = "jd_analysis"

    def run(self, state: AgentState) -> dict:
        jd_text = (state.user_input or "").strip()
        if not jd_text:
            raise AgentError("JD 分析需要提供 JD 文本")

        with trace("jd_analysis"):
            parsed = self._parse_jd(jd_text)
            user_skills = self._get_user_skills(state)
            jd_skills = parsed.get("skills") or []
            if parsed.get("responsibilities") or parsed.get("requirements"):
                jd_skills = _merge_skills(jd_skills, parsed)

            match = skill_matching(user_skills, jd_skills)
            reasoning = self._llm_reason(state, parsed, match)
            analysis = {
                "parsed": parsed,
                "match": match,
                "reasoning": reasoning,
                "final": reasoning,
            }
            state.observations.append({"agent": self.name, "parsed_jd": parsed})
            state.observations.append({"agent": self.name, "skill_match": match})
            state.final_answer = _render_answer(parsed, match, reasoning)
            logger.info(
                "jd_analysis done | type=%s match=%d missing=%d",
                parsed.get("jd_type"),
                match["match_score"],
                len(match["missing"]),
            )
            return analysis

    # ---- 内部 ----

    def _parse_jd(self, text: str) -> dict:
        parsed = JDParserTool().execute(text=text)
        if not parsed.get("ok"):
            raise AgentError("JD 文本解析失败")
        return parsed

    def _get_user_skills(self, state: AgentState) -> list[str]:
        skills = extract_skills_from_profile(state.skill_profile)
        if state.resume:
            skills = _dedupe(skills + extract_skills_from_text(state.resume))
        return skills

    def _llm_reason(self, state: AgentState, parsed: dict, match: dict) -> dict:
        summary = {
            "job_title": parsed.get("job_title", ""),
            "jd_type": parsed.get("jd_type", ""),
            "company": parsed.get("company", ""),
            "city": parsed.get("city", ""),
            "salary": parsed.get("salary", ""),
            "experience": parsed.get("experience", ""),
            "education": parsed.get("education", ""),
            "core_responsibilities": parsed.get("responsibilities", [])[:3],
            "core_requirements": parsed.get("requirements", [])[:5],
            "jd_skills": parsed.get("skills", []),
            "user_skills": match["matched"],
            "partial_skills": match["partial"],
            "missing_skills": match["missing"],
        }
        prompt = (
            "请分析以下求职场景，输出 JSON：\n" + json.dumps(summary, ensure_ascii=False, indent=1) + "\n"
            "综合匹配度、优劣势、风险给出投递建议。"
        )
        try:
            llm = LLMClient()
            raw = llm.chat(
                messages=[
                    {"role": "system", "content": SYSTEM_JD_ANALYSIS},
                    {"role": "user", "content": prompt},
                ],
                temperature=0.3,
            )
        except Exception as exc:
            logger.warning("jd_analysis LLM 失败，退回规则结论: %s", exc)
            return self._rule_based_reason(parsed, match)
        try:
            return parse_json(raw)
        except Exception as exc:
            logger.warning("jd_analysis LLM 输出非 JSON，退回规则结论: %s", exc)
            return self._rule_based_reason(parsed, match)

    def _rule_based_reason(self, parsed: dict, match: dict) -> dict:
        score = match["match_score"]
        rec = "投递" if score >= 75 else ("谨慎投递" if score >= 50 else "不投")
        return {
            "match_score": score,
            "strengths": [f"已掌握核心技能: {s}" for s in match["matched"][:5]],
            "gaps": [f"尚未掌握: {s}" for s in match["missing"][:5]],
            "missing_skills": match["missing"],
            "recommendation": f"{rec}（规则判定，匹配度 {score}）",
        }


def _merge_skills(jd_skills: list[str], parsed: dict) -> list[str]:
    """从职责/要求段落再抽一次技能，补充 JD Parser 已有结果。"""
    extra = extract_skills_from_text(
        " ".join(parsed.get("responsibilities", []) + parsed.get("requirements", []))
    )
    return _dedupe(jd_skills + extra)


def _dedupe(items: list[str]) -> list[str]:
    seen: list[str] = []
    for it in items:
        if it not in seen:
            seen.append(it)
    return seen


def _render_answer(parsed: dict, match: dict, reasoning: dict) -> str:
    title = parsed.get("job_title") or "目标岗位"
    lines = [
        f"📋 {title}（{parsed.get('jd_type', '')}）分析：",
        f"- 公司/城市/薪资：{parsed.get('company', '未知')} / {parsed.get('city', '未知')} / {parsed.get('salary', '面议')}",
        f"- 硬性要求：{parsed.get('experience', '未注明')}；学历：{parsed.get('education', '未注明')}",
        f"- 技能匹配度：{match['match_score']}/100",
        f"- 已掌握：{', '.join(match['matched']) if match['matched'] else '无'}",
        f"- 部分掌握：{', '.join(match['partial']) if match['partial'] else '无'}",
        f"- 未掌握：{', '.join(match['missing']) if match['missing'] else '无'}",
        "",
        "💡 投递建议：",
    ]
    if reasoning.get("strengths"):
        lines.append(f"- 优势：{'；'.join(reasoning['strengths'][:3])}")
    if reasoning.get("gaps"):
        lines.append(f"- 差距：{'；'.join(reasoning['gaps'][:3])}")
    lines.append(f"- 建议：{reasoning.get('recommendation', '参考匹配度而定')}")
    return "\n".join(lines)