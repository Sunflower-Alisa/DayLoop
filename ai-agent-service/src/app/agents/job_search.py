from __future__ import annotations

from app.core.logging import get_logger
from app.core.tracing import trace
from app.agents.skill_match import extract_skills_from_profile, skill_matching
from app.state import AgentState
from app.tools.dayloop import get_client

logger = get_logger("agent.job_search")


class JobSearchAgent:
    """AI 招聘信息收集 Agent（UC02 / docs/05-architecture.md §34）。

    流程：读取用户画像与技能 → 获取候选岗位（DayLoop API）→ JD 技能匹配
    → 匹配度计算 → 排序 → 生成投递建议。
    依赖 DayLoop 提供岗位数据；DayLoop 不可用时优雅降级。
    """

    name = "job_search"

    def run(self, state: AgentState) -> dict:
        with trace("job_search"):
            jobs = self._fetch_jobs(state)
            user_skills = extract_skills_from_profile(state.skill_profile)
            ranked = self._rank_jobs(jobs, user_skills)
            report = {
                "jobs": ranked,
                "final": _render_report(ranked),
            }
            state.final_answer = report["final"]
            state.observations.append({"agent": self.name, "ranked_jobs": ranked})
            logger.info("job_search done | jobs=%d", len(ranked))
            return report

    # ---- 内部 ----

    def _fetch_jobs(self, state: AgentState) -> list[dict]:
        result = get_client().get_jobs(state.user_id)
        if not result.get("ok"):
            logger.warning("job_search DayLoop 不可用，返回提示")
            return []
        data = result.get("data", [])
        if isinstance(data, dict):
            data = data.get("jobs", data.get("items", []))
        return data if isinstance(data, list) else []

    def _rank_jobs(self, jobs: list[dict], user_skills: list[str]) -> list[dict]:
        if not jobs:
            return []
        ranked: list[dict] = []
        for job in jobs:
            text = " ".join(str(job.get(k, "")) for k in ("title", "job_title", "description", "requirements", "skills"))
            jd_skills = self._extract_skills(text)
            match = skill_matching(user_skills, jd_skills)
            ranked.append(
                {
                    "job_title": job.get("title") or job.get("job_title", ""),
                    "company": job.get("company", ""),
                    "city": job.get("city", ""),
                    "salary": job.get("salary", ""),
                    "match_score": match["match_score"],
                    "matched_skills": match["matched"],
                    "missing_skills": match["missing"],
                    "recommendation": "投递" if match["match_score"] >= 75 else ("谨慎投递" if match["match_score"] >= 50 else "不投"),
                }
            )
        ranked.sort(key=lambda j: j["match_score"], reverse=True)
        return ranked[:10]

    def _extract_skills(self, text: str) -> list[str]:
        from app.agents.skill_match import extract_skills_from_text

        return extract_skills_from_text(text)


def _render_report(jobs: list[dict]) -> str:
    if not jobs:
        return "🔍 招聘信息：目前没有符合条件的新岗位，或招聘数据源暂不可用。"
    lines = ["🔍 AI 招聘信息（按匹配度排序）："]
    for i, j in enumerate(jobs, 1):
        lines.append(
            f"{i}. {j['job_title']} @ {j['company'] or '未知'}（{j['city'] or '未知'}，{j['salary'] or '面议'}）"
            f"—— 匹配度 {j['match_score']}/100，建议{j['recommendation']}"
        )
        if j.get("matched_skills"):
            lines.append(f"   优势技能：{', '.join(j['matched_skills'][:4])}")
        if j.get("missing_skills"):
            lines.append(f"   待补技能：{', '.join(j['missing_skills'][:4])}")
    return "\n".join(lines)