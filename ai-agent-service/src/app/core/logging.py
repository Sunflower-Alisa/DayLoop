import logging
import sys

# 统一日志命名空间。所有模块一律通过 get_logger() 获取 logger，
# 禁止在各模块里直接 logging.getLogger("agent-xxx") 或打印。

_NAMESPACE = "agent"


def setup_logging(level: str = "INFO") -> logging.Logger:
    """初始化全局日志配置。应用启动时调用一次。

    所有 agent.* 命名空间的 logger 共享一个根 Handler，
    避免每个模块重复配置、避免日志散落。
    """
    root = logging.getLogger(_NAMESPACE)
    root.setLevel(getattr(logging, level.upper(), logging.INFO))
    root.propagate = False

    # 幂等：已有 Handler 时不再追加（防止重复调用导致重复输出）
    if not any(isinstance(h, logging.StreamHandler) for h in root.handlers):
        handler = logging.StreamHandler(sys.stdout)
        handler.setFormatter(
            logging.Formatter(
                "%(asctime)s | %(levelname)-7s | %(name)s | %(message)s",
                datefmt="%Y-%m-%d %H:%M:%S",
            )
        )
        root.addHandler(handler)
    return root


def get_logger(name: str) -> logging.Logger:
    """获取统一命名空间下的模块 logger。

    用法：logger = core.logging.get_logger("perception.intent")
    生成名为 agent.perception.intent 的 logger，受 setup_logging 统一管理。
    """
    return logging.getLogger(f"{_NAMESPACE}.{name}")