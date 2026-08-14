from __future__ import annotations

import os

import httpx

from app.core.exceptions import APIError
from app.core.logging import get_logger

logger = get_logger("tools.dayloop")

# DayLoop Agent Integration API 基地址，可由环境变量覆盖（§8 / §9）
# Node 主后端默认端口 3001；如启用 .NET 镜像后端则设为 http://localhost:5000/api/v1/agent
DEFAULT_BASE_URL = os.getenv("DAYLOOP_API_BASE", "http://localhost:3001/api/v1/agent")
# 服务间认证（§43 Service Token）
SERVICE_TOKEN = os.getenv("DAYLOOP_SERVICE_TOKEN", "")
TIMEOUT = float(os.getenv("DAYLOOP_API_TIMEOUT", "5"))


class DayLoopClient:
    """DayLoop Agent Integration API 的 HTTP 客户端（§13.2 Agent → DayLoop）。

    封装 §9 定义的能力：Profile / Resume / Skills / Tasks / Learning /
    Jobs / Interview / Knowledge。DayLoop 不可用或未配置时返回
    {"ok": False} 而不是抛错，便于 Agent 降级。
    """

    def __init__(
        self,
        base_url: str | None = None,
        token: str | None = None,
        timeout: float = TIMEOUT,
    ) -> None:
        self.base_url = (base_url or DEFAULT_BASE_URL).rstrip("/")
        self.token = token if token is not None else SERVICE_TOKEN
        self._client = httpx.Client(
            base_url=self.base_url,
            timeout=timeout,
            headers=self._headers(),
        )

    def _headers(self) -> dict:
        headers = {"Content-Type": "application/json"}
        if self.token:
            headers["Authorization"] = f"Bearer {self.token}"
        return headers

    @property
    def http(self) -> httpx.Client:
        return self._client

    def _get(self, path: str, **params) -> dict:
        try:
            resp = self._client.get(path, params=params)
            resp.raise_for_status()
            return {"ok": True, "data": resp.json()}
        except httpx.HTTPStatusError as exc:
            logger.warning("DayLoop GET %s -> %s", path, exc.response.status_code)
            return {"ok": False, "error": exc.response.text[:200]}
        except Exception as exc:
            logger.warning("DayLoop GET %s 失败: %s", path, exc)
            return {"ok": False, "error": "DayLoop 不可用", "detail": str(exc)}

    def _post(self, path: str, json: dict) -> dict:
        try:
            resp = self._client.post(path, json=json)
            resp.raise_for_status()
            return {"ok": True, "data": resp.json()}
        except Exception as exc:
            logger.warning("DayLoop POST %s 失败: %s", path, exc)
            return {"ok": False, "error": "DayLoop 不可用", "detail": str(exc)}

    def _put(self, path: str, json: dict) -> dict:
        try:
            resp = self._client.put(path, json=json)
            resp.raise_for_status()
            return {"ok": True, "data": resp.json()}
        except Exception as exc:
            logger.warning("DayLoop PUT %s 失败: %s", path, exc)
            return {"ok": False, "error": "DayLoop 不可用", "detail": str(exc)}

    # ---- §9 业务 API ----

    def get_profile(self, user_id: str) -> dict:
        return self._get("/profile", user_id=user_id)

    def get_resume(self, user_id: str) -> dict:
        return self._get("/resume", user_id=user_id)

    def put_resume(self, user_id: str, content: str) -> dict:
        return self._put("/resume", {"user_id": user_id, "content": content})

    def get_skills(self, user_id: str) -> dict:
        return self._get("/skills", user_id=user_id)

    def update_skill(self, user_id: str, skill: str, level: str) -> dict:
        return self._post("/skills", {"user_id": user_id, "skill": skill, "level": level})

    def get_tasks(self, user_id: str, date: str | None = None) -> dict:
        params = {"user_id": user_id}
        if date:
            params["date"] = date
        return self._get("/tasks", **params)

    def create_task(self, user_id: str, title: str, **extra) -> dict:
        return self._post("/tasks", {"user_id": user_id, "title": title, **extra})

    def update_task(self, user_id: str, task_id: str, **extra) -> dict:
        return self._put(f"/tasks/{task_id}", {"user_id": user_id, **extra})

    def get_learning_history(self, user_id: str) -> dict:
        return self._get("/learning/history", user_id=user_id)

    def get_jobs(self, user_id: str, **params) -> dict:
        return self._get("/jobs", user_id=user_id, **params)

    def get_job(self, user_id: str, job_id: str) -> dict:
        return self._get(f"/jobs/{job_id}", user_id=user_id)

    def save_job(self, user_id: str, job: dict) -> dict:
        return self._post("/jobs", {"user_id": user_id, "job": job})

    def get_knowledge(self, user_id: str, **params) -> dict:
        return self._get("/knowledge", user_id=user_id, **params)

    def create_interview(self, user_id: str, job_id: str, mode: str = "agent") -> dict:
        return self._post("/interviews", {"user_id": user_id, "job_id": job_id, "mode": mode})

    def submit_answer(self, interview_id: str, answer: str) -> dict:
        return self._post(f"/interviews/{interview_id}/answer", {"answer": answer})

    def finish_interview(self, interview_id: str) -> dict:
        return self._post(f"/interviews/{interview_id}/finish", {})

    def close(self) -> None:
        self._client.close()


_client: DayLoopClient | None = None


def get_client() -> DayLoopClient:
    """获取全局 DayLoop 客户端（懒初始化）。"""
    global _client
    if _client is None:
        _client = DayLoopClient()
    return _client