from __future__ import annotations

import re
from datetime import date

from app.core.logging import get_logger
from app.core.tracing import trace
from app.state import AgentState
from app.tools.dayloop import get_client

logger = get_logger("agent.task_management")

_ACTIONS = ("list", "create", "update", "finish", "delete")


class TaskManagementAgent:
    """任务管理 Agent。

    流程：解析用户意图（列出/创建/更新/完成任务）→ 调用 DayLoop Tasks API
    → 返回任务操作结果。DayLoop 不可用时返回提示。
    """

    name = "task_management"

    def run(self, state: AgentState) -> dict:
        with trace("task_management"):
            action, payload = self._parse(state.user_input or "")
            result = self._execute(state, action, payload)
            state.final_answer = _render(result, action)
            state.observations.append({"agent": self.name, "action": action, "result": result})
            logger.info("task_management done | action=%s ok=%s", action, result.get("ok", False))
            return {"action": action, "result": result, "final": state.final_answer}

    # ---- 内部 ----

    def _parse(self, text: str) -> tuple[str, dict]:
        tl = text.strip()
        if not tl:
            return "list", {}
        # 创建任务：把标题内容识别出来
        if re.search(r"创建|新建|添加|加个|记一下", tl):
            title = re.sub(r"^(创建|新建|添加|加个|记一下|任务|一个)?[\s:：]*", "", tl).strip()
            return "create", {"title": title or "新任务"}
        if re.search(r"完成|标记|done", tl):
            return "finish", {"title": tl}
        if re.search(r"删除|取消.*任务", tl):
            return "delete", {"title": tl}
        return "list", {}

    def _execute(self, state: AgentState, action: str, payload: dict) -> dict:
        client = get_client()
        if action == "list":
            result = client.get_tasks(state.user_id, date.today().isoformat())
            if not result.get("ok"):
                return {"ok": False, "error": result.get("error", "DayLoop 不可用")}
            return {"ok": True, "tasks": result.get("data", [])}
        if action == "create":
            result = client.create_task(state.user_id, payload.get("title", ""))
            if not result.get("ok"):
                return {"ok": False, "error": result.get("error", "DayLoop 不可用")}
            return {"ok": True, "task": result.get("data", {})}
        # finish / delete 需要 task_id，MVP 只做列表与创建
        return {"ok": False, "error": f"暂不支持操作: {action}（可列出/创建任务）", "action": action}


def _render(result: dict, action: str) -> str:
    if not result.get("ok"):
        return f"🗂️ 任务操作失败：{result.get('error', '未知错误')}"
    if action == "list":
        tasks = result.get("tasks") or []
        if not tasks:
            return "🗂️ 今日暂无任务。"
        lines = ["🗂️ 今日任务："]
        for i, t in enumerate(tasks, 1):
            title = t.get("title", "") if isinstance(t, dict) else str(t)
            done = (t.get("status") == "done") if isinstance(t, dict) else False
            lines.append(f"{i}. {'✅' if done else '⬜'} {title}")
        return "\n".join(lines)
    if action == "create":
        return f"🗂️ 已创建任务：{result.get('task', {}).get('title', '') if isinstance(result.get('task'), dict) else ''}"
    return "🗂️ 操作完成。"