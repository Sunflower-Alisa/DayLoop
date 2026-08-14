from __future__ import annotations

from abc import ABC, abstractmethod
from typing import Any

from app.context.schema import ContextBlock
from app.core.logging import get_logger

logger = get_logger("context.provider")


class ContextProvider(ABC):
    """上下文数据源。

    每种上下文类型对应一个 Provider。真正的数据由外部系统提供：
    - DayLoop API（Profile / Resume / Skills / Tasks / Jobs / Knowledge）
    - RAG（知识检索）
    - Memory（长期记忆）
    - Session（会话内临时数据）

    本模块只定义接口；接入方实现 fetch，返回 ContextBlock 或 None。
    """

    name: str = "base"

    @abstractmethod
    def fetch(self, ctx_name: str, payload: dict | None = None) -> ContextBlock | None:
        """按需获取一块上下文。获取不到返回 None。"""
        raise NotImplementedError


class StaticProvider(ContextProvider):
    """基于预置数据的内存 Provider（本地测试 / 降级兜底用）。

    通过 register(ctx_name, value) 预先放入数据；fetch 时格式化返回。
    """

    name = "static"

    def __init__(self) -> None:
        self._data: dict[str, str] = {}
        self._meta: dict[str, dict[str, Any]] = {}

    def register(self, ctx_name: str, value: str, metadata: dict[str, Any] | None = None) -> None:
        self._data[ctx_name] = value
        self._meta[ctx_name] = metadata or {}

    def fetch(self, ctx_name: str, payload: dict | None = None) -> ContextBlock | None:
        if ctx_name not in self._data:
            return None
        return ContextBlock(
            name=ctx_name,
            content=self._data[ctx_name],
            source="static",
            priority=10,
            token_estimate=len(self._data[ctx_name]) // 2,
            metadata=self._meta[ctx_name],
        )


class ProviderRegistry:
    """按上下文类型路由到对应 Provider。"""

    def __init__(self) -> None:
        self._providers: dict[str, ContextProvider] = {}

    def register(self, ctx_name: str, provider: ContextProvider) -> None:
        self._providers[ctx_name] = provider
        logger.info("context provider 注册: %s -> %s", ctx_name, provider.name)

    def get(self, ctx_name: str) -> ContextProvider | None:
        return self._providers.get(ctx_name)

    def list(self) -> list[str]:
        return sorted(self._providers)

    def fetch(self, ctx_name: str, payload: dict | None = None) -> ContextBlock | None:
        provider = self.get(ctx_name)
        if provider is None:
            return None
        try:
            return provider.fetch(ctx_name, payload)
        except Exception as exc:  # Provider 失败不应中断整体上下文构建
            logger.warning("context provider fetch 失败 %s: %s", ctx_name, exc)
            return None