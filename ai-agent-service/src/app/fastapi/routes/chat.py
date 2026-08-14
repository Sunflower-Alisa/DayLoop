from __future__ import annotations

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel

from app.agents.bootstrap import build_router
from app.core.error_handler import safe_node
from app.core.logging import get_logger
from app.core.tracing import trace
from app.perception.perception import PerceptionService
from app.state import AgentState, from_perception

logger = get_logger("core.api")


class ChatRequest(BaseModel):
    user_id: str = ""
    session_id: str = ""
    message: str = ""
    extra: dict = {}


class ChatResponse(BaseModel):
    session_id: str = ""
    intent: str = ""
    message: str = ""
    metadata: dict = {}


router = APIRouter(prefix="/v1/chat", tags=["chat"])


@router.post("", response_model=ChatResponse)
@safe_node()
def chat(body: ChatRequest) -> dict:
    """统一 Agent 入口（docs/05-architecture.md §11 Chat API）。

    流程：Perception → Intent Router → Use Case Agent → final_answer。
    """
    with trace("chat"):
        msg = (body.message or "").strip()
        if not msg:
            raise HTTPException(status_code=400, detail="message 不能为空")

        perception = PerceptionService().perceive(message=msg, extra=body.extra or {})
        state = AgentState(session_id=body.session_id, user_id=body.user_id)
        from_perception(state, perception)

        router = build_router()
        handler = router.route(state.intent)
        result = handler(state)

        message = state.final_answer or state.task or "处理完成"
        metadata = {
            "intent_confidence": perception.intent_confidence,
            "intent_method": perception.metadata.get("intent_method"),
            "task_id": state.metadata.get("task_id", ""),
            "agent_result": result if isinstance(result, dict) else {},
            "missing_context": state.missing_context,
        }
        logger.info("chat done | intent=%s session=%s", state.intent, body.session_id)
        return ChatResponse(
            session_id=body.session_id,
            intent=state.intent,
            message=message,
            metadata=metadata,
        ).model_dump()