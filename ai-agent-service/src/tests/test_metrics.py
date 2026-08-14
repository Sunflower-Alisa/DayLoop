from __future__ import annotations

from app.evaluation.metrics import (
    accuracy,
    average,
    average_tokens,
    hit_rate_at_k,
    is_within_target,
    mrr,
    p50,
    p95,
    precision_at_k,
    recall_at_k,
    task_success_rate,
    tool_success_rate,
)


def test_accuracy():
    assert accuracy(3, 4) == 0.75
    assert accuracy(0, 0) == 0.0


def test_average_empty():
    assert average([]) == 0.0
    assert average([1, 2, 3]) == 2.0


def test_percentiles():
    values = [1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0]
    assert p50(values) <= 6.0
    assert p95(values) <= 10.0


def test_hit_rate_at_k():
    assert hit_rate_at_k([True, True, False], 3) == 0.6667
    assert hit_rate_at_k([], 3) == 0.0


def test_precision_at_k():
    # 2 个 query，每个 Top3 中 1 个相关
    assert precision_at_k([1, 1], 3) == 0.3333


def test_recall_at_k():
    assert recall_at_k([1.0, 0.5]) == 0.75


def test_mrr():
    assert mrr([1, 2]) == 0.75
    assert mrr([0]) == 0.0


def test_task_success_rate():
    assert task_success_rate(9, 10) == 0.9


def test_tool_success_rate():
    assert tool_success_rate([True, True, False]) == 0.6667


def test_average_tokens():
    result = average_tokens([{"input": 100, "output": 50}, {"input": 200, "output": 100}])
    assert result["total"] == 450
    assert result["avg_input"] == 150.0
    assert result["avg_output"] == 75.0


def test_is_within_target():
    assert is_within_target(0.91, 0.90)
    assert not is_within_target(0.89, 0.90)