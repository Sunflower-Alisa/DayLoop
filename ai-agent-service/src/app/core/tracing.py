import uuid
from contextlib import contextmanager
from contextvars import ContextVar

request_id_var: ContextVar[str] = ContextVar("request_id", default="")


def new_request_id() -> str:
    return uuid.uuid4().hex[:12]


@contextmanager
def trace(operation: str):
    request_id = request_id_var.get() or new_request_id()
    token = request_id_var.set(request_id)
    try:
        yield request_id
    finally:
        request_id_var.reset(token)
