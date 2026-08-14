from __future__ import annotations


class FrameworkError(Exception):
    """框架统一异常基类。code 用于对外错误编码（如 HTTP 响应）。"""
    code = "E_UNKNOWN"

    def __init__(self, message: str = "", *, cause: Exception | None = None) -> None:
        self.cause = cause
        if cause is not None:
            message = f"{message} (cause: {type(cause).__name__}: {cause})"
        super().__init__(message)


class ConfigError(FrameworkError):
    code = "E_CONFIG"


class LLMError(FrameworkError):
    code = "E_LLM"


class ToolError(FrameworkError):
    code = "E_TOOL"


class AgentError(FrameworkError):
    code = "E_AGENT"


class PerceptionError(FrameworkError):
    code = "E_PERCEPTION"


class ContextError(FrameworkError):
    code = "E_CONTEXT"


class RAGError(FrameworkError):
    code = "E_RAG"


class MemoryError(FrameworkError):
    code = "E_MEMORY"


class APIError(FrameworkError):
    code = "E_API"


class ParsingError(LLMError):
    """LLM 输出解析失败（JSON 等）。"""
    code = "E_LLM_PARSE"