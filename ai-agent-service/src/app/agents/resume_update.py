from __future__ import annotations

import json

from app.core.logging import get_logger
from app.core.tracing import trace
from app.llm.client import LLMClient
from app.llm.parser import parse_json
from app.state import AgentState
from app.tools.dayloop import get_client

logger = get_logger("agent.resume_update")

SYSTEM_RESUME = """你是简历优化顾问。根据用户提供的简历或修改要点，生成更新后的简历内容。
要求输出 JSON：{"resume": "完整简历文本", "changes": ["本次修改点..."], "suggestions": ["进一步优化建议..."]}
不要输出其他内容。"""


class ResumeUpdateAgent:
    """简历更新 Agent。

    流程：读取现有简历（DayLoop）→ 结合用户修改要点 → LLM 生成新版简历
    → 写回 DayLoop → 返回修改摘要与建议。
    不依赖简历内容非空：用户直接给要点即可。
    """

    name = "resume_update"

    def run(self, state: AgentState) -> dict:
        with trace("resume_update"):
            current = self._get_current(state)
            update_input = state.user_input or ""
            result = self._llm_update(current, update_input)
            saved = self._save_resume(state, result.get("resume", ""))

            return_ = {
                "changes": result.get("changes", []),
                "suggestions": result.get("suggestions", []),
                "saved": saved,
                "final": _render_result(result, saved),
            }
            state.final_answer = return_["final"]
            state.observations.append({"agent": self.name, "changes": result.get("changes", [])})
            logger.info("resume_update done | saved=%s changes=%d", saved, len(result.get("changes", [])))
            return return_

    # ---- 内部 ----

    def _get_current(self, state: AgentState) -> str:
        result = get_client().get_resume(state.user_id)
        if not result.get("ok"):
            return state.resume or ""
        data = result.get("data", "")
        if isinstance(data, dict):
            data = data.get("content", "")
        return str(data or "")

    def _llm_update(self, current: str, update_input: str) -> dict:
        prompt = (
            f"现有简历：\n{current or '（空）'}\n\n用户修改要点：\n{update_input}\n\n"
            "请输出 JSON（resume/changes/suggestions）。"
        )
        try:
            llm = LLMClient()
            raw = llm.chat(
                messages=[
                    {"role": "system", "content": SYSTEM_RESUME},
                    {"role": "user", "content": prompt},
                ],
                temperature=0.3,
            )
            return parse_json(raw)
        except Exception as exc:
            logger.warning("resume_update LLM 失败，退回直接拼接: %s", exc)
            return {
                "resume": f"{current}\n{update_input}".strip(),
                "changes": ["根据用户要点更新简历"],
                "suggestions": [],
            }

    def _save_resume(self, state: AgentState, content: str) -> bool:
        result = get_client().put_resume(state.user_id, content)
        if not result.get("ok"):
            logger.warning("resume_update DayLoop 写入失败: %s", result.get("error"))
            return False
        return True


def _render_result(result: dict, saved: bool) -> str:
    lines = [f"📄 简历更新{'完成' if saved else '生成完成（未写入 DayLoop）'}："]
    changes = result.get("changes") or []
    if changes:
        lines.append("修改点：")
        lines.extend(f"- {c}" for c in changes[:5])
    suggestions = result.get("suggestions") or []
    if suggestions:
        lines.append("优化建议：")
        lines.extend(f"- {s}" for s in suggestions[:5])
    return "\n".join(lines)