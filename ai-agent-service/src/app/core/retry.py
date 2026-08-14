import functools
import logging
import random
import time

logger = logging.getLogger("agent-core.retry")


def retry(max_times: int = 3, base_delay: float = 1.0, jitter: bool = True):
    def decorator(func):
        @functools.wraps(func)
        def wrapper(*args, **kwargs):
            for attempt in range(1, max_times + 1):
                try:
                    return func(*args, **kwargs)
                except Exception as exc:
                    if attempt == max_times:
                        raise
                    delay = base_delay * (2 ** (attempt - 1))
                    if jitter:
                        delay *= random.uniform(0.5, 1.5)
                    logger.warning("retry %s/%s after %.2fs: %s", attempt, max_times, delay, exc)
                    time.sleep(delay)

        return wrapper

    return decorator
