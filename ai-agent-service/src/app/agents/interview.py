from __future__ import annotations

import json
import os
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from app.core.exceptions import AgentError
from app.core.logging import get_logger
from app.core.tracing import trace
from app.llm.client import LLMClient
from app.llm.parser import parse_json
from app.state import AgentState

logger = get_logger("agent.interview")

_SESSION_DIR_ENV = "INTERVIEW_SESSION_DIR"

SYSTEM_INTERVIEWER = """你是资深 AI 岗位面试官。基于求职者画像与目标 JD 进行动态模拟面试：
- 先考察基础概念，根据回答质量动态调难度、深入追问
- 回答好 → 提高难度；回答一般 → 继续追问；回答错误 → 深挖盲区并纠正解释
- 每个问题只提一个，等待求职者回答后再决定下一步
只输出 JSON，不要输出其他内容。"""

SYSTEM_EVALUATOR = """你是 AI 岗位面试评估专家。评估求职者上一轮回答质量，并决定下一轮动作。
输出 JSON：
- score: 0-100 整数
- strengths: 回答的亮点列表
- weaknesses: 回答的不足列表
- verdict: "pass_up"（回答优秀，提高难度）/ "follow_up"（回答一般，继续追问）/ "remedy"（回答错误，深挖纠正）
- feedback: 给求职者的简短评价（不超过80字）
- next_question: 下一个问题（或追问），空则不提问"""

SYSTEM_REPORT = """你是 AI 岗位面试总结专家。基于整场面试问答记录，输出总结报告。
输出 JSON：
- overall_score: 0-100 整数
- strength_areas: 表现好的知识领域列表
- weak_areas: 薄弱知识领域列表
- skill_insights: 对求职者技能画像的更新建议列表
- review_suggestions: 面试问题参考答案要点
- summary: 一句话总结"""


def _session_dir() -> Path:
    d = os.getenv(_SESSION_DIR_ENV, os.path.join(os.getcwd(), ".memory", "interviews"))
    p = Path(d)
    p.mkdir(parents=True, exist_ok=True)
    return p


class MockInterviewSession:
    """一场模拟面试的持久化会话。"""

    def __init__(self, session_id: str) -> None:
        self.session_id = session_id
        self._file = _session_dir() / f"{session_id}.json"
        self.state: dict[str, Any] = self._load() or self._default()

    @staticmethod
    def _default() -> dict:
        return {
            "user_id": "",
            "job_title": "",
            "target_role": "",
            "focus": [],
            "rounds": [],
            "started": False,
            "last_question": "",
            "started_at": datetime.now(timezone.utc).isoformat(),
            "finished_at": "",
        }

    def _load(self) -> dict | None:
        if not self._file.exists():
            return None
        try:
            with open(self._file, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            return None

    def _flush(self) -> None:
        try:
            with open(self._file, "w", encoding="utf-8") as f:
                json.dump(self.state, f, ensure_ascii=False, indent=1)
        except Exception as exc:
            raise AgentError(f"面试会话写入失败: {self._file}", cause=exc) from exc

    def is_first_turn(self) -> bool:
        return bool(self.state.get("started")) is False

    def mark_started(self) -> None:
        self.state["started"] = True

    def add_round(self, question: str, answer: str, eval: dict) -> None:
        self.state["rounds"].append({"question": question, "answer": answer, "evaluation": eval})

    def finish(self, report: dict) -> None:
        self.state["finished_at"] = datetime.now(timezone.utc).isoformat()
        self.state["report"] = report
        self._flush()


class MockInterviewAgent:
    """模拟面试 Agent（docs/04-agent-flow.md §15-§18 / UC06，P0）。

    - 首轮：读取 JD/技能画像生成面试计划并出第一题；
    - 后续轮：评估上轮回答 → 动态追问（提高难度/继续追问/深挖纠正）；
    - 结束：生成总结报告并写入长期记忆。
    会话以 session_id 持久化到本地 JSON，支持多轮异步连贯面试。
    """

    name = "mock_interview"
    MAX_QUESTIONS = 8

    def run(self, state: AgentState) -> dict:
        session_id = state.session_id or "interview_default"
        user_input = (state.user_input or "").strip()
        with trace("mock_interview"):
            session = MockInterviewSession(session_id)

            # 首轮：建立面试计划并抛出第一题（等待回答）
            if session.is_first_turn():
                self._prepare(session, state)
                q = self._first_question(session, state)
                session.state["last_question"] = q
                session.mark_started()
                session._flush()
                state.final_answer = _render_question(q)
                return {"finished": False, "question": q, "session_id": session_id, "round": 0}

            # 后续轮：用户回答上一题 → 评估 → 动态追问 / 结束
            last_q = session.state.get("last_question") or session.state["rounds"][-1]["question"]
            if not user_input:
                state.final_answer = _render_question(last_q)
                return {"finished": False, "question": last_q, "session_id": session_id}

            ev = self._evaluate(session, state, last_q, user_input)
            session.add_round(last_q, user_input, ev)

            if self._should_end(session):
                report = self._finish_report(session, state)
                session.finish(report)
                state.final_answer = _render_report(report)
                return {"finished": True, "report": report, "session_id": session_id}

            q = ev.get("next_question") or self._next_by_verdict(session, state, ev)
            session.state["last_question"] = q
            session._flush()
            state.final_answer = _render_question(q)
            return {"finished": False, "question": q, "session_id": session_id, "round": len(session.state["rounds"])}

    # ---- 内部 ----

    def _prepare(self, session: MockInterviewSession, state: AgentState) -> None:
        session.state["user_id"] = state.user_id
        job_title = (state.user_profile.get("job") or {}).get("job_title", "") if isinstance(
            state.user_profile.get("job"), dict
        ) else ""
        session.state["job_title"] = job_title or "AI 岗位"
        skills = state.skill_profile.get("skills", [])
        focus = []
        if isinstance(skills, list):
            for s in skills:
                if isinstance(s, str):
                    focus.append(s)
                elif isinstance(s, dict) and s.get("name"):
                    focus.append(s["name"])
        session.state["focus"] = focus[:5]
        session._flush()

    def _first_question(self, session: MockInterviewSession, state: AgentState) -> str:
        llm = LLMClient()
        prompt = (
            f"目标岗位：{session.state['job_title']}；重点考察：{', '.join(session.state['focus']) or '通用 AI 基础'}。\n"
            "请生成第 1 个面试问题（先考基础概念），只输出 JSON：{\"question\": \"...\"}"
        )
        try:
            raw = llm.chat(messages=self._sys_msgs(SYSTEM_INTERVIEWER, prompt), temperature=0.7)
            return (parse_json(raw) or {}).get("question", "请简要介绍一下你对该岗位所需核心技能的理解。")
        except Exception as exc:
            logger.warning("interview 生成首题失败，使用默认问题: %s", exc)
            return "请简要介绍一下你对该岗位所需核心技能的理解。"

    def _evaluate(self, session: MockInterviewSession, state: AgentState, question: str, answer: str) -> dict:
        llm = LLMClient()
        prompt = (
            f"岗位：{session.state['job_title']}；本轮问题：{question}\n"
            f"求职者回答：{answer}\n"
            "请评估并输出 JSON（score/strengths/weaknesses/verdict/feedback/next_question）。"
        )
        try:
            raw = llm.chat(messages=self._sys_msgs(SYSTEM_EVALUATOR, prompt), temperature=0.3)
            ev = parse_json(raw)
        except Exception as exc:
            logger.warning("interview 评估失败，退回规则评估: %s", exc)
            ev = {
                "score": 50,
                "verdict": "follow_up",
                "feedback": "回答收下了，我们继续。",
                "next_question": self._rule_follow_up(session),
            }
        ev.setdefault("strengths", [])
        ev.setdefault("weaknesses", [])
        ev.setdefault("next_question", "")
        return ev

    def _next_by_verdict(self, session: MockInterviewSession, state: AgentState, ev: dict) -> str:
        if ev.get("next_question"):
            return ev["next_question"]
        if ev.get("verdict") == "remedy":
            return "让我们回到基础知识：请解释一下该技能的核心原理，并给出一个实际场景中的应用。"
        return self._rule_follow_up(session)

    def _rule_follow_up(self, session: MockInterviewSession) -> str:
        focus = session.state["focus"]
        return f"针对 {focus[0] if focus else '该技能'}，如果线上出现召回效果差，你会如何定位和优化？"

    def _should_end(self, session: MockInterviewSession) -> bool:
        if len(session.state["rounds"]) >= self.MAX_QUESTIONS:
            return True
        last_round = session.state["rounds"][-1]
        answer = (last_round.get("answer") or "").strip()
        if answer and answer.lower() in {"结束", "结束面试", "finish", "不面试了", "够了"}:
            return True
        eval = last_round.get("evaluation") or {}
        return eval.get("verdict") == "end_interview"

    def _finish_report(self, session: MockInterviewSession, state: AgentState) -> dict:
        llm = LLMClient()
        rounds = session.state["rounds"]
        transcript = "\n".join(
            [
                f"Q{idx}: {r['question']}\nA{idx}: {r['answer']}\n评估: score={r.get('evaluation', {}).get('score', '-')} verdict={r.get('evaluation', {}).get('verdict', '-')}"
                for idx, r in enumerate(rounds, 1)
            ]
        )
        prompt = f"岗位：{session.state['job_title']}\n整场面试记录：\n{transcript}\n请生成总结报告，只输出 JSON。"
        try:
            raw = llm.chat(messages=self._sys_msgs(SYSTEM_REPORT, prompt), temperature=0.3)
            report = parse_json(raw)
        except Exception as exc:
            logger.warning("interview 报告生成失败，退回规则报告: %s", exc)
            report = {
                "overall_score": 60,
                "strength_areas": [],
                "weak_areas": [session.state["job_title"]],
                "skill_insights": ["建议针对薄弱知识领域加强系统学习与实践"],
                "review_suggestions": [],
                "summary": "面试完成，建议复习薄弱环节。",
            }
        report.setdefault("overall_score", 60)
        report.setdefault("summary", "面试完成。")
        # 写入长期记忆
        try:
            from app.memory import LongTermMemory

            LongTermMemory(user_id=session.state["user_id"]).save(
                {
                    "type": "interview_report",
                    "content": {
                        "job_title": session.state["job_title"],
                        "overall_score": report["overall_score"],
                        "weak_areas": report.get("weak_areas", []),
                        "summary": report["summary"],
                    },
                }
            )
        except Exception as exc:
            logger.warning("面试报告写入记忆失败: %s", exc)
        return report

    @staticmethod
    def _sys_msgs(system: str, user: str) -> list[dict]:
        return [
            {"role": "system", "content": system},
            {"role": "user", "content": user},
        ]


def _render_question(q: str) -> str:
    return f"🎤 面试官: {q}\n\n（回答后我会继续追问或调节难度；输入“结束”结束面试）"


def _render_report(report: dict) -> str:
    lines = [
        "📋 模拟面试完成，总结报告：",
        f"- 综合评分：{report.get('overall_score', '-')}/100",
    ]
    if report.get("strength_areas"):
        lines.append(f"- 表现好的领域：{'；'.join(report['strength_areas'])}")
    if report.get("weak_areas"):
        lines.append(f"- 薄弱领域：{'；'.join(report['weak_areas'])}")
    if report.get("skill_insights"):
        lines.append("- 技能画像更新建议：")
        for s in report["skill_insights"][:4]:
            lines.append(f"  · {s}")
    lines.append(f"- 总结：{report.get('summary', '')}")
    return "\n".join(lines)