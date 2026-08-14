from __future__ import annotations

from typing import Any

from app.core.logging import get_logger
from app.tools.base import BaseTool
from app.tools.dayloop import DayLoopClient, get_client

logger = get_logger("tools.dayloop")


# ---- §26 DayLoop Tools：每个工具对应用户级业务能力 ----

class GetUserProfileTool(BaseTool):
    name = "get_user_profile"

    def execute(self, user_id: str = "", **kwargs) -> dict:
        client = self._client()
        result = client.get_profile(user_id)
        if not result.get("ok"):
            return {"ok": False, "tool": self.name, "error": result.get("error")}
        return {"ok": True, "tool": self.name, "profile": result["data"]}

    @staticmethod
    def _client() -> DayLoopClient:
        return get_client()


class GetResumeTool(BaseTool):
    name = "get_resume"

    def execute(self, user_id: str = "", **kwargs) -> dict:
        result = get_client().get_resume(user_id)
        if not result.get("ok"):
            return {"ok": False, "tool": self.name, "error": result.get("error")}
        return {"ok": True, "tool": self.name, "resume": result["data"]}


class UpdateResumeTool(BaseTool):
    name = "update_resume"

    def execute(self, user_id: str = "", content: str = "", **kwargs) -> dict:
        result = get_client().put_resume(user_id, content)
        if not result.get("ok"):
            return {"ok": False, "tool": self.name, "error": result.get("error")}
        return {"ok": True, "tool": self.name, "updated": True}


class GetSkillsTool(BaseTool):
    name = "get_skills"

    def execute(self, user_id: str = "", **kwargs) -> dict:
        result = get_client().get_skills(user_id)
        if not result.get("ok"):
            return {"ok": False, "tool": self.name, "error": result.get("error")}
        return {"ok": True, "tool": self.name, "skills": result["data"]}


class UpdateSkillTool(BaseTool):
    name = "update_skill"

    def execute(self, user_id: str = "", skill: str = "", level: str = "", **kwargs) -> dict:
        result = get_client().update_skill(user_id, skill, level)
        if not result.get("ok"):
            return {"ok": False, "tool": self.name, "error": result.get("error")}
        return {"ok": True, "tool": self.name, "updated": True}


class GetTasksTool(BaseTool):
    name = "get_tasks"

    def execute(self, user_id: str = "", date: str = "", **kwargs) -> dict:
        result = get_client().get_tasks(user_id, date or None)
        if not result.get("ok"):
            return {"ok": False, "tool": self.name, "error": result.get("error")}
        return {"ok": True, "tool": self.name, "tasks": result["data"]}


class CreateTaskTool(BaseTool):
    name = "create_task"

    def execute(self, user_id: str = "", title: str = "", **kwargs: Any) -> dict:
        result = get_client().create_task(user_id, title, **kwargs)
        if not result.get("ok"):
            return {"ok": False, "tool": self.name, "error": result.get("error")}
        return {"ok": True, "tool": self.name, "task": result["data"]}


class GetLearningHistoryTool(BaseTool):
    name = "get_learning_history"

    def execute(self, user_id: str = "", **kwargs) -> dict:
        result = get_client().get_learning_history(user_id)
        if not result.get("ok"):
            return {"ok": False, "tool": self.name, "error": result.get("error")}
        return {"ok": True, "tool": self.name, "history": result["data"]}


class GetJobsTool(BaseTool):
    name = "get_jobs"

    def execute(self, user_id: str = "", **kwargs) -> dict:
        result = get_client().get_jobs(user_id, **kwargs)
        if not result.get("ok"):
            return {"ok": False, "tool": self.name, "error": result.get("error")}
        return {"ok": True, "tool": self.name, "jobs": result["data"]}


class SaveJobTool(BaseTool):
    name = "save_job"

    def execute(self, user_id: str = "", job: dict | None = None, **kwargs) -> dict:
        job = job or {}
        result = get_client().save_job(user_id, job)
        if not result.get("ok"):
            return {"ok": False, "tool": self.name, "error": result.get("error")}
        return {"ok": True, "tool": self.name, "job": result["data"]}


# ==== 注册到全局 registry ====
_TOOLS: list[type[BaseTool]] = [
    GetUserProfileTool,
    GetResumeTool,
    UpdateResumeTool,
    GetSkillsTool,
    UpdateSkillTool,
    GetTasksTool,
    CreateTaskTool,
    GetLearningHistoryTool,
    GetJobsTool,
    SaveJobTool,
]


def register_dayloop_tools() -> None:
    """把 §26 DayLoop 工具批量注册进 app.tools.registry。"""
    from app.tools.registry import register_tool

    for cls in _TOOLS:
        register_tool(cls.name, cls)
        logger.info("dayloop tool 注册: %s", cls.name)