from __future__ import annotations

import time

from fastapi import Request
from starlette.middleware.base import BaseHTTPMiddleware

from app.core.logging import get_logger
from app.core.tracing import new_request_id

logger = get_logger("core.api")


class RequestLoggingMiddleware(BaseHTTPMiddleware):
    """请求日志中间件：为每个请求生成 request_id 并记录耗时。"""

    async def dispatch(self, request: Request, call_next):
        rid = new_request_id()
        start = time.time()
        method = request.method
        path = request.url.path
        logger.info("request start | id=%s %s %s", rid, method, path)
        try:
            response = await call_next(request)
        except Exception as exc:
            logger.error("request error | id=%s %s %s | %s", rid, method, path, exc)
            raise
        elapsed_ms = (time.time() - start) * 1000
        response.headers["X-Request-ID"] = rid
        logger.info("request done | id=%s %s %s status=%d %.1fms", rid, method, path, response.status_code, elapsed_ms)
        return response