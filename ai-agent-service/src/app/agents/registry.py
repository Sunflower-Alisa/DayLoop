from __future__ import annotations

from typing import Any

from app.core.exceptions import AgentError
from app.core.logging import get_logger

logger = get_logger("agent.registry")

_registry: dict[str, Any] = {}


def register(name: str):
    def decorator(cls):
        _registry[name] = cls
        return cls
    return decorator


def register_agent(name: str, agent: Any) -> None:
    """注册 Agent 实例（或类，惰性实例化）。"""
    _registry[name] = agent
    logger.info("agent 注册: %s", name)


def get_agent(name: str) -> Any:
    agent = _registry.get(name)
    if agent is None:
        raise AgentError(f"未知 Agent: {name}，可用: {', '.join(list_agents())}")
    return agent


def instantiate(name: str) -> Any:
    """获取 Agent 实例。注册的是类则实例化；是实例则直接返回。"""
    agent = get_agent(name)
    if isinstance(agent, type):
        return agent()
    return agent


def list_agents() -> list[str]:
    return list(_registry)