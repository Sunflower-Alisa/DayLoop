from __future__ import annotations

from abc import ABC, abstractmethod


class BaseTool(ABC):
    """Tool 基类（docs/05-architecture.md §4.3 / §25）。

    name: 工具标识
    description: 工具说明（供 LLM Tool Calling 选择）
    """

    name: str = "base"
    description: str = ""

    @abstractmethod
    def execute(self, **kwargs) -> dict:
        raise NotImplementedError