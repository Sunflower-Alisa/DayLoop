from __future__ import annotations

from typing import Any

from app.core.exceptions import ToolError
from app.core.logging import get_logger

logger = get_logger("tools.registry")

_registry: dict[str, type] = {}


def register(name: str):
    """装饰器：注册 Tool 类。兼容早期用法 register('name')。"""

    def decorator(cls):
        _registry[name] = cls
        return cls

    return decorator


def register_tool(name: str, cls: type) -> None:
    """注册 Tool。若 cls 是类则延迟实例化，实例则直接缓存。"""
    _registry[name] = cls


def get_tool(name: str) -> Any:
    cls = _registry.get(name)
    if cls is None:
        raise ToolError(f"未知 Tool: {name}，可用: {', '.join(list_tools())}")
    return cls


def instantiate(name: str) -> Any:
    """获取 Tool 实例（惰性实例化）。Tool 无状态可共用单例。"""
    cls = get_tool(name)
    return cls()


def list_tools() -> list[str]:
    return sorted(_registry)


def describe_tools() -> list[dict]:
    """返回 Tool Schema 列表（供 LLM Tool Calling 使用，§4.3 统一 Schema）。"""
    out: list[dict] = []
    for name in list_tools():
        out.append(
            {
                "name": name,
                "description": f"Agent 可通过 {name} 访问对应能力",
                "input_schema": {"type": "object", "properties": {}},
            }
        )
    return out