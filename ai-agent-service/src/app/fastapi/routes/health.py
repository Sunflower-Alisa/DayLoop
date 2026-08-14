from fastapi import APIRouter

router = APIRouter(prefix="/v1", tags=["health"])


@router.get("/health")
def health() -> dict:
    """健康检查（docs/05-architecture.md §10.1 Health）。"""
    return {"status": "ok", "service": "ai-agent", "version": "1.0.0"}