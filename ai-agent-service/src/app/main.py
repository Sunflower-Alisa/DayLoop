from __future__ import annotations

import os

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.core.logging import get_logger, setup_logging

app = FastAPI(
    title="DayLoop AI Agent Service",
    description="求职 AI 助手（Perception → Intent → Agent → Tools/RAG/Memory）",
    version="1.0.0",
)


@app.on_event("startup")
def _startup():
    setup_logging(os.getenv("LOG_LEVEL", "INFO"))
    from app.agents.bootstrap import build_router

    build_router()


@app.on_event("startup")
def _startup_cors():
    app.add_middleware(
        CORSMiddleware,
        allow_origins=["*"],
        allow_methods=["*"],
        allow_headers=["*"],
    )


from app.fastapi.middleware.request_logging import RequestLoggingMiddleware
from app.fastapi.routes.chat import router as chat_router
from app.fastapi.routes.health import router as health_router

app.add_middleware(RequestLoggingMiddleware)

app.include_router(chat_router, prefix="/api")
app.include_router(health_router, prefix="/api")

logger = get_logger("core.api")
logger.info("DayLoop AI Agent Service 启动，路由: /api/v1/health, /api/v1/chat")