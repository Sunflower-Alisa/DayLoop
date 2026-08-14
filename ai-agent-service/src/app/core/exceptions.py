class FrameworkError(Exception):
    code = "E_UNKNOWN"


class ConfigError(FrameworkError):
    code = "E_CONFIG"


class LLMError(FrameworkError):
    code = "E_LLM"


class ToolError(FrameworkError):
    code = "E_TOOL"


class AgentError(FrameworkError):
    code = "E_AGENT"