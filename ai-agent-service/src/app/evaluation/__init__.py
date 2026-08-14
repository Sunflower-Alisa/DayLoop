from app.evaluation.metrics import (
    accuracy,
    average,
    hit_rate_at_k,
    mrr,
    precision_at_k,
    recall_at_k,
)


def run_evaluation(*args, **kwargs):
    """惰性导入 runner，避免 `python -m app.evaluation.runner` 时触发重复导入警告。"""
    from app.evaluation.runner import run_evaluation as _run

    return _run(*args, **kwargs)


__all__ = [
    "run_evaluation",
    "accuracy",
    "hit_rate_at_k",
    "precision_at_k",
    "recall_at_k",
    "mrr",
    "average",
]
