from __future__ import annotations

import re

from app.core.logging import get_logger
from app.perception.entity import CITY_KEYWORDS, COMPANY_KEYWORDS, POSITION_KEYWORDS
from app.tools.base import BaseTool

logger = get_logger("tools.jd_parser")

# JD 常见薪酬模式（可放宽，识别后归一为区间/文本）
_SALARY_RE = re.compile(r"(\d{1,2}[kK]|[0-9]{4,}-[0-9]{4,}|面议)")

# 学历
_EDU_RE = re.compile(r"(本科|硕士|博士|大专|不限学历|本科及以上|硕士及以上)")

# 职责 / 要求段落的小节标题
_SECTION_TITLES = ["岗位职责", "职位描述", "任职要求", "任职资格", "岗位要求", "工作职责", "工作内容", "加分项", "我们希望你", "你将负责", "职位亮点"]

# 硬性要求句式
_HARD_RE = re.compile(r"(\d+)\s*年.*(经验|以上|工作)")

# 加分项句式
_PLUS_RE = re.compile(r"(优先|加分项|了解|熟悉以下|掌握以下|有.*经验者优先|欢迎.*加分)")


class JDParserTool(BaseTool):
    """JD 解析工具（§25 JD Parser / UC03）。

    用规则从原始 JD 文本中提取结构化信息：
    job_title / company / city / salary / experience / education /
    responsibilities / requirements / skills / plus_points / jd_type。
    """

    name = "jd_parser"

    def execute(self, text: str = "", **kwargs) -> dict:
        """解析 JD 文本。输入 text；返回结构化 dict。

        特殊返回字段：
        - ok: 是否解析出有效内容
        - jd_type: 目标岗位类型（AI Agent应用开发 / AI产品经理 / FDE / 其他）
        """
        if not text or not text.strip():
            raise ValueError("jd_parser 需要提供 text")

        skills = self._extract_skills(text)
        hard = _HARD_RE.search(text)
        plus = _PLUS_RE.search(text)
        edu = _EDU_RE.search(text)

        result = {
            "ok": True,
            "job_title": self._extract_title(text),
            "company": self._extract_company(text),
            "city": self._extract_city(text),
            "salary": self._extract_salary(text),
            "experience": hard.group(0) if hard else "",
            "education": edu.group(1) if edu else "",
            "responsibilities": self._extract_section(text, ["岗位职责", "职位描述", "工作职责", "你将负责", "工作内容"]),
            "requirements": self._extract_section(text, ["任职要求", "任职资格", "岗位要求", "我们希望你"]),
            "skills": skills,
            "plus_points": self._extract_section(text, ["加分项"]),
            "has_plus_section": bool(plus),
            "jd_type": self._classify(text, skills),
        }
        logger.info("jd_parser done | type=%s skills=%d len=%d", result["jd_type"], len(skills), len(text))
        return result

    # ---- 内部提取 ----

    def _extract_title(self, text: str) -> str:
        for kw in POSITION_KEYWORDS:
            if kw.lower() in text.lower():
                return kw
        # 退而求其次：抓取最长匹配到职位关键词的片段
        m = re.search(r"(负责|岗位)?[\u4e00-\u9fa5A-Za-z0-9 ]{2,16}?(工程师|经理|开发|专员|专家|FDE)", text)
        return m.group(0).strip() if m else ""

    def _extract_company(self, text: str) -> str:
        for kw in COMPANY_KEYWORDS:
            if kw in text:
                return kw
        return ""

    def _extract_city(self, text: str) -> str:
        for kw in CITY_KEYWORDS:
            if kw in text:
                return kw
        return ""

    def _extract_salary(self, text: str) -> str:
        first = re.findall(r"\d{1,2}[kK]", text)
        if first:
            return f"{first[0]}-{first[-1]}" if len(first) > 1 else first[0]
        m = re.search(r"(\d{4,}-\d{4,})", text)
        if m:
            return m.group(1)
        return "面议" if "面议" in text else ""

    def _extract_section(self, text: str, titles: list[str]) -> list[str]:
        """按小节标题切分并返回该标题下的内容行列表。"""
        lines = [re.sub(r"\s+", " ", ln).strip() for ln in text.splitlines() if ln.strip()]
        sections: dict[str, list[str]] = {}
        current: list[str] | None = None
        for ln in lines:
            title = next((t for t in _SECTION_TITLES if ln.startswith(t)), None)
            if title is not None:
                current = sections.setdefault(title, [])
                rest = ln[len(title):].lstrip("：:.。 ").strip()
                if rest:
                    current.append(rest)
                continue
            if current is not None:
                current.append(ln)
        results: list[str] = []
        for t in titles:
            results.extend(sections.get(t, []))
        return results

    def _extract_skills(self, text: str) -> list[str]:
        """复用感知层实体词典抽取 JD 中的技能。"""
        from app.perception.entity import SKILL_KEYWORDS

        found: list[str] = []
        for kw in SKILL_KEYWORDS:
            if kw.lower() in text.lower() and kw not in found:
                found.append(kw)
        return found

    def _classify(self, text: str, skills: list[str]) -> str:
        tl = text.lower()
        if "ai agent" in tl or "agent 应用" in tl or ("agent" in tl and "开发" in tl):
            return "AI Agent应用开发"
        if "产品经理" in tl or "ai 产品" in tl:
            return "AI产品经理"
        if "fde" in tl or "前向交付" in tl or "客户交付" in tl:
            return "FDE"
        return "其他"