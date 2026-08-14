from __future__ import annotations

"""基于规则的评测器（docs/06-evaluation.md §6/§10/§18/§27/§34）。

全部离线可跑（不依赖 LLM / DayLoop）：
- intent: 规则意图识别准确率（§6）
- jd: JD 字段提取准确率（§34）
- skill: 技能匹配准确率（§35）
- memory: 记忆召回（§27）
- planner: 计划生成质量（§7）
- tool: 工具执行成功率（§10）
"""

from typing import Any

from app.agents.skill_match import skill_matching
from app.core.logging import get_logger
from app.evaluation.error_analysis import (
    FailureRecord,
    aggregate_failures,
    classify_field_failure,
    classify_intent_failure,
    classify_memory_failure,
    classify_tool_failure,
)
from app.evaluation.metrics import accuracy, tool_success_rate
from app.memory.long_term import LongTermMemory
from app.perception.intent import IntentDetector
from app.runtime.planner import Planner
from app.state import AgentState
from app.tools.jd_parser import JDParserTool

logger = get_logger("evaluation.rule_based")


def evaluate_intent(cases: list[dict]) -> dict:
    """意图识别准确率（§6）。使用规则识别器（稳定、可离线），不依赖 LLM。"""
    correct, total = 0, 0
    failures: list[FailureRecord] = []
    details: list[dict] = []

    for case in cases:
        total += 1
        expected = case.get("expected", "")
        detector = IntentDetector(use_llm=False)
        result = detector.detect(case.get("input", ""))
        ok = result.intent == expected
        correct += 1 if ok else 0
        details.append(
            {
                "id": case["id"],
                "input": case.get("input"),
                "expected": expected,
                "actual": result.intent,
                "method": result.method,
                "correct": ok,
            }
        )
        if not ok:
            failures.append(classify_intent_failure(case, expected, result.intent))

    return {
        "category": "intent",
        "metrics": {"intent_accuracy": accuracy(correct, total), "total": total, "correct": correct},
        "details": details,
        "failures": [f.to_dict() for f in failures],
        "failure_counts": aggregate_failures(failures),
    }


def evaluate_jd(cases: list[dict]) -> dict:
    """JD 字段提取准确率（§34）。比较解析结果与预期字段。"""
    parser = JDParserTool()
    total = 0
    field_correct = 0
    field_total = 0
    failures: list[FailureRecord] = []
    details: list[dict] = []

    for case in cases:
        total += 1
        parsed = parser.execute(text=case.get("jd", ""))
        expected: dict = case.get("expected", {})
        case_failures: list[str] = []

        for field, exp in expected.items():
            field_total += 1
            actual = parsed.get(field)
            ok = _compare(field, exp, actual)
            field_correct += 1 if ok else 0
            if not ok:
                case_failures.append(f"{field}: expected={exp!r} actual={actual!r}")

        details.append(
            {
                "id": case["id"],
                "parsed": parsed,
                "expected": expected,
                "field_accuracy": round(field_correct / field_total, 4) if field_total else 0.0,
                "errors": case_failures,
            }
        )
        if case_failures:
            failures.append(
                FailureRecord(case["id"], "context_error", "; ".join(case_failures))
            )

    return {
        "category": "jd",
        "metrics": {
            "jd_field_accuracy": accuracy(field_correct, field_total),
            "cases": total,
            "field_total": field_total,
            "field_correct": field_correct,
        },
        "details": details,
        "failures": [f.to_dict() for f in failures],
        "failure_counts": aggregate_failures(failures),
    }


def evaluate_skill(cases: list[dict]) -> dict:
    """技能匹配准确率（§35）。比较 matched/partial/missing 集合。"""
    correct, total = 0, 0
    failures: list[FailureRecord] = []
    details: list[dict] = []

    for case in cases:
        total += 1
        result = skill_matching(case.get("user_skills", []), case.get("jd_skills", []))
        exp: dict = case.get("expected", {})
        ok = (
            set(result["matched"]) == set(exp.get("matched", []))
            and set(result["partial"]) == set(exp.get("partial", []))
            and set(result["missing"]) == set(exp.get("missing", []))
            and result["match_score"] >= exp.get("min_score", 0)
        )
        correct += 1 if ok else 0
        details.append(
            {
                "id": case["id"],
                "result": result,
                "expected": exp,
                "correct": ok,
            }
        )
        if not ok:
            failures.append(
                FailureRecord(case["id"], "context_error", f"result={result} expected={exp}")
            )

    return {
        "category": "skill",
        "metrics": {"skill_matching_accuracy": accuracy(correct, total), "total": total, "correct": correct},
        "details": details,
        "failures": [f.to_dict() for f in failures],
        "failure_counts": aggregate_failures(failures),
    }


def evaluate_memory(cases: list[dict], memory_dir: str | None = None) -> dict:
    """记忆召回（§27）：保存 → 按关键词查询 → 检查预期内容是否被召回。"""
    mem = LongTermMemory(user_id="eval_user", memory_dir=memory_dir)
    correct, total = 0, 0
    failures: list[FailureRecord] = []
    details: list[dict] = []

    for case in cases:
        total += 1
        save = case.get("save", {})
        mem.save({"type": save.get("type", "general"), "content": save.get("content", "")})
        hits = mem.query(case.get("query", ""), top_k=5)
        expected = case.get("expected", "")
        found = any(expected in str(h.get("content", "")) for h in hits)
        ok = found
        correct += 1 if ok else 0
        details.append(
            {
                "id": case["id"],
                "query": case.get("query"),
                "expected": expected,
                "recalled": [h.get("content", "") for h in hits[:3]],
                "correct": ok,
            }
        )
        if not ok:
            failures.append(classify_memory_failure(case, expected, found))

    return {
        "category": "memory",
        "metrics": {"memory_recall": accuracy(correct, total), "total": total, "correct": correct},
        "details": details,
        "failures": [f.to_dict() for f in failures],
        "failure_counts": aggregate_failures(failures),
    }


def evaluate_planner(cases: list[dict]) -> dict:
    """计划生成质量（§7）：步骤数正确 + 首步名称正确。"""
    planner = Planner()
    correct, total = 0, 0
    failures: list[FailureRecord] = []
    details: list[dict] = []

    for case in cases:
        total += 1
        state = AgentState(intent=case.get("intent", ""))
        steps = planner.run(state)
        ok = len(steps) == case.get("expected_steps", 0) and (
            steps[0].get("name") == case.get("expected_first", "") if steps else False
        )
        correct += 1 if ok else 0
        details.append(
            {
                "id": case["id"],
                "intent": case.get("intent"),
                "steps": [s.get("name") for s in steps],
                "expected_steps": case.get("expected_steps"),
                "expected_first": case.get("expected_first"),
                "correct": ok,
            }
        )
        if not ok:
            failures.append(
                classify_field_failure(
                    case, "plan", case.get("expected_steps"), [s.get("name") for s in steps]
                )
            )

    return {
        "category": "planner",
        "metrics": {"plan_success_rate": accuracy(correct, total), "total": total, "correct": correct},
        "details": details,
        "failures": [f.to_dict() for f in failures],
        "failure_counts": aggregate_failures(failures),
    }


def evaluate_tool(cases: list[dict]) -> dict:
    """工具执行成功率（§10）：实例化工具并执行，对比 expected_ok。"""
    from app.tools.registry import instantiate

    ok_flags: list[bool] = []
    failures: list[FailureRecord] = []
    details: list[dict] = []

    for case in cases:
        tool_name = case.get("tool", "")
        expected_ok = case.get("expected_ok", True)
        try:
            tool = instantiate(tool_name)
            output = tool.execute(text=case.get("input", ""), user_id=case.get("input", ""))
            actual_ok = bool(output.get("ok", True))
            min_results = case.get("expected_results_min")
            if min_results is not None and actual_ok:
                actual_ok = len(output.get("results", [])) >= min_results
        except Exception as exc:
            actual_ok = False
            output = {"error": str(exc)}
        ok = actual_ok == expected_ok
        ok_flags.append(ok)
        details.append(
            {
                "id": case["id"],
                "tool": tool_name,
                "expected_ok": expected_ok,
                "actual_ok": actual_ok,
                "output": output if isinstance(output, dict) else {"value": output},
                "correct": ok,
            }
        )
        if not ok:
            failures.append(classify_tool_failure(case, expected_ok, actual_ok, str(output)))

    return {
        "category": "tool",
        "metrics": {
            "tool_success_rate": tool_success_rate(ok_flags),
            "total": len(cases),
            "correct": sum(ok_flags),
        },
        "details": details,
        "failures": [f.to_dict() for f in failures],
        "failure_counts": aggregate_failures(failures),
    }


def _compare(field: str, expected: Any, actual: Any) -> bool:
    if field == "skills":
        exp = {str(s).lower() for s in expected}
        act = {str(s).lower() for s in (actual or [])}
        return exp.issubset(act)
    return str(expected) == str(actual)
