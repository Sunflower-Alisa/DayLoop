import functools

from app.core.logging import get_logger

logger = get_logger("core.error")


def safe_node(fallback=None):
    def decorator(func):
        @functools.wraps(func)
        def wrapper(*args, **kwargs):
            try:
                return func(*args, **kwargs)
            except Exception as exc:
                logger.exception("node %s failed: %s", func.__name__, exc)
                if callable(fallback):
                    return fallback(*args, **kwargs)
                raise
        return wrapper

    return decorator