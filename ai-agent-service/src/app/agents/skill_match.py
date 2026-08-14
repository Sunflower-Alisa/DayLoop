from __future__ import annotations

import re

from app.core.logging import get_logger
from app.perception.entity import SKILL_KEYWORDS

logger = get_logger("agent.skill_match")


def normalize_skill(name: str) -> str:
    return re.sub(r"[\s\-/]+", "", name).lower()


def extract_skills_from_text(text: str) -> list[str]:
    """从任意文本（简历/JD）抽取已知技能关键词。"""
    if not text:
        return []
    found: list[str] = []
    tl = text.lower()
    for kw in SKILL_KEYWORDS:
        if kw.lower() in tl and kw not in found:
            found.append(kw)
    return found


def extract_skills_from_profile(skill_profile: dict) -> list[str]:
    """从 skill_profile 提取技能名列表。

    兼容两种结构：
    - {"skills": ["Python", ...]}
    - {"skills": [{"name": "Python", "level": "熟练"}, ...]}
    """
    raw = skill_profile.get("skills") or []
    names: list[str] = []
    for item in raw:
        if isinstance(item, str):
            names.append(item)
        elif isinstance(item, dict):
            nm = item.get("name") or ""
            if nm:
                names.append(nm)
    return names


def skill_matching(user_skills: list[str], jd_skills: list[str]) -> dict:
    """JD技能 vs 用户技能 匹配（docs/05-architecture.md §32）。

    返回：
    - matched: 已掌握技能
    - partial: 部分掌握（JD 技能中用户可覆盖的子串/近似技能）
    - missing: 未掌握（JD 要求但用户没有）
    - match_score: 0-100 匹配度
    - overlap_details: {jd_skill: {"status": "matched|partial|missing", "user_skill": 近似技能名|""}}
    """
    user_norm = {normalize_skill(s): s for s in user_skills}
    matched: list[str] = []
    partial: list[str] = []
    missing: list[str] = []
    overlap_details: dict[str, dict] = {}

    for jd_skill in jd_skills:
        jn = normalize_skill(jd_skill)
        hit = user_norm.get(jn)
        if hit is not None:
            matched.append(jd_skill)
            overlap_details[jd_skill] = {"status": "matched", "user_skill": hit}
            continue
        # 部分匹配：JD 技能被用户技能包含（如 JD 要求 "LangGraph"，用户有 "LangGraph Agent"）
        partial_hit = _find_partial(jd_skill, user_skills)
        if partial_hit:
            partial.append(jd_skill)
            overlap_details[jd_skill] = {"status": "partial", "user_skill": partial_hit}
            continue
        missing.append(jd_skill)
        overlap_details[jd_skill] = {"status": "missing", "user_skill": ""}

    total = len(jd_skills)
    score = 0
    if total:
        score = round((len(matched) + 0.5 * len(partial)) / total * 100)
    return {
        "matched": matched,
        "partial": partial,
        "missing": missing,
        "match_score": score,
        "overlap_details": overlap_details,
    }


def _find_partial(jd_skill: str, user_skills: list[str]) -> str:
    jn = normalize_skill(jd_skill)
    for us in user_skills:
        un = normalize_skill(us)
        if jn in un or un in jn:
            return us
    return ""
