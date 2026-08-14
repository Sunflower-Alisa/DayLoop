from __future__ import annotations

"""评测 Runner（docs/06-evaluation.md §52 Agent Evaluation Pipeline）。

执行固定测试集 → 计算指标 → 错误归因 → 与 MVP 目标对比 → 输出报告。
用法：
    python -m app.evaluation.runner                 # 全量（含 LLM Judge）
    python -m app.evaluation.runner --offline       # 跳过 LLM 相关评测
    python -m app.evaluation.runner --category intent,jd  # 只跑指定类别
"""

import argparse
import json
import os
import sys
import tempfile
import time
from datetime import datetime, timezone
from pathlib import Path

from app.core.logging import get_logger, setup_logging
from app.evaluation.test_cases import available_datasets, load_cases

logger = get_logger("evaluation.runner")

# MVP 目标（docs/06-evaluation.md §57）
TARGETS = {
    "intent_accuracy": 0.90,
    "task_success_rate": 0.85,
    "tool_success_rate": 0.95,
    "hit_rate@3": 0.85,
}

# 类别 → (评测函数, 是否需要 LLM)
_CATEGORIES = {
    "intent": ("rule", None),
    "jd": ("rule", None),
    "skill": ("rule", None),
    "memory": ("rule", None),
    "planner": ("rule", None),
    "tool": ("rule", None),
    "rag": ("rag", None),
    "agent": ("agent", True),
    "interview": ("llm_judge", True),
    "faithfulness": ("llm_judge", True),
    "answer_relevance": ("llm_judge", True),
}


def run_evaluation(
    categories: list[str] | None = None,
    offline: bool = False,
    report_dir: str | None = None,
) -> dict:
    """执行评测并返回聚合报告。

    Args:
        categories: 要执行的类别子集；None = 全部。
        offline: 跳过依赖 LLM 的评测（interview/faithfulness/answer_relevance/agent）。
        report_dir: 报告输出目录；默认 src/app/evaluation/reports。
    """
    setup_logging(os.getenv("LOG_LEVEL", "INFO"))
    start = time.perf_counter()

    cats = categories or sorted(_CATEGORIES)
    if offline:
        cats = [c for c in cats if not _CATEGORIES[c][1]]

    # 临时运行目录（避免污染工作区的 .chroma/.memory）
    tmpdir = tempfile.mkdtemp(prefix="eval_runtime_")
    memory_dir = os.path.join(tmpdir, "memory")
    chroma_dir = os.path.join(tmpdir, "chroma")
    os.makedirs(memory_dir, exist_ok=True)

    results: list[dict] = []
    for cat in cats:
        logger.info("=== 开始评测: %s ===", cat)
        try:
            result = _run_category(cat, memory_dir=memory_dir, chroma_dir=chroma_dir, offline=offline)
            results.append(result)
        except Exception as exc:
            logger.exception("评测类别失败: %s (%s)", cat, exc)
            results.append(
                {
                    "category": cat,
                    "metrics": {},
                    "details": [],
                    "failures": [{"case_id": "*", "category": "unknown", "detail": str(exc)}],
                    "failure_counts": {"unknown": 1},
                    "error": str(exc),
                }
            )

    elapsed = time.perf_counter() - start
    summary = _build_summary(results)
    report = {
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "offline": offline,
        "categories": cats,
        "elapsed_seconds": round(elapsed, 2),
        "summary": summary,
        "targets": TARGETS,
        "results": results,
    }

    report_dir = report_dir or os.path.join(os.path.dirname(__file__), "reports")
    _write_report(report, report_dir)
    _print_summary(summary)
    return report


# ---- 各类别执行 ----

def _run_category(cat: str, *, memory_dir: str, chroma_dir: str, offline: bool) -> dict:
    kind, needs_llm = _CATEGORIES[cat]
    cases = load_cases(f"{cat}_cases") if f"{cat}_cases" in available_datasets() else []

    if kind == "rule":
        from app.evaluation.evaluators.rule_based import (
            evaluate_intent,
            evaluate_jd,
            evaluate_memory,
            evaluate_planner,
            evaluate_skill,
            evaluate_tool,
        )

        fn = {
            "intent": evaluate_intent,
            "jd": evaluate_jd,
            "skill": evaluate_skill,
            "memory": lambda c, **kw: evaluate_memory(c, **kw),
            "planner": evaluate_planner,
            "tool": evaluate_tool,
        }[cat]
        return fn(cases, memory_dir=memory_dir) if cat == "memory" else fn(cases)

    if kind == "rag":
        from app.evaluation.evaluators.rag_evaluator import evaluate_rag

        return evaluate_rag(cases, persist_dir=chroma_dir)

    if kind == "agent":
        return _evaluate_agents(cases, memory_dir=memory_dir, offline=offline)

    if kind == "llm_judge":
        from app.evaluation.evaluators.llm_judge import (
            evaluate_answer_relevance,
            evaluate_faithfulness,
            evaluate_interview,
        )

        fn = {
            "interview": evaluate_interview,
            "faithfulness": evaluate_faithfulness,
            "answer_relevance": evaluate_answer_relevance,
        }[cat]
        return fn(cases, use_llm=not offline)

    return {"category": cat, "metrics": {}, "details": [], "failures": [], "failure_counts": {}}


def _evaluate_agents(cases: list[dict], *, memory_dir: str, offline: bool) -> dict:
    """端到端任务成功率（§13）：真实走 Router → Use Case Agent 单步执行。

    Agent 内部已实现优雅降级（DayLoop 不可用 / LLM 失败时返回提示），
    因此即使离线也能判断任务是否完成。
    """
    from app.agents.bootstrap import build_router
    from app.state import AgentState

    router = build_router()
    correct, total = 0, 0
    details: list[dict] = []
    failures: list[dict] = []
    latencies: list[float] = []

    os.environ["MEMORY_DIR"] = memory_dir
    os.environ["INTERVIEW_SESSION_DIR"] = os.path.join(memory_dir, "interviews")
    os.environ["CHROMA_PERSIST_DIR"] = os.path.join(memory_dir, "chroma")

    for case in cases:
        total += 1
        t0 = time.perf_counter()
        state = AgentState(
            session_id=f"eval_{case['id']}",
            user_id="eval_user",
            user_input=case.get("input", ""),
            intent=case.get("intent", ""),
            skill_profile=case.get("state", {}).get("skill_profile", {}),
            resume=case.get("state", {}).get("resume", ""),
        )
        try:
            handler = router.route(case.get("intent", ""))
            _ = handler(state)
            final = state.final_answer or ""
            expect = case.get("expect", {})
            ok = _check_answer(final, expect)
            err = ""
        except Exception as exc:
            final = ""
            ok = False
            err = f"{type(exc).__name__}: {exc}"

        latencies.append(round(time.perf_counter() - t0, 3))
        correct += 1 if ok else 0
        details.append(
            {
                "id": case["id"],
                "intent": case.get("intent"),
                "final_answer": final[:200],
                "latency_s": latencies[-1],
                "correct": ok,
                "error": err,
            }
        )
        if not ok:
            failures.append({"case_id": case["id"], "category": "task_error", "detail": err or "final_answer 为空或未达预期"})

    metrics = {
        "task_success_rate": round(correct / total, 4) if total else 0.0,
        "total": total,
        "correct": correct,
        "avg_latency_s": round(sum(latencies) / len(latencies), 3) if latencies else 0.0,
    }
    return {
        "category": "agent",
        "metrics": metrics,
        "details": details,
        "failures": failures,
        "failure_counts": {"task_error": len(failures), "unknown": 0},
    }


def _check_answer(final: str, expect: dict) -> bool:
    if expect.get("non_empty") and not final.strip():
        return False
    for kw in expect.get("contains", []):
        if kw and kw not in final:
            return False
    return True


# ---- 汇总 ----

def _build_summary(results: list[dict]) -> dict:
    summary: dict = {}
    for r in results:
        metrics = r.get("metrics", {})
        for name, value in metrics.items():
            if name in {"total", "correct", "llm_used", "avg_latency_s", "field_total", "field_correct", "cases", "hit_count", "avg_score", "avg_input", "avg_output"}:
                continue
            summary[name] = round(float(value), 4)
    # 判定是否达标
    for target_name, target in TARGETS.items():
        if target_name in summary:
            summary[f"{target_name}_target"] = target
            summary[f"{target_name}_met"] = summary[target_name] >= target
    return summary


def _print_summary(summary: dict) -> None:
    lines = ["\n" + "=" * 60, "Evaluation Summary", "=" * 60]
    for k, v in summary.items():
        lines.append(f"  {k:<32} {v}")
    print("\n".join(lines))


def _write_report(report: dict, report_dir: str) -> None:
    path = Path(report_dir)
    path.mkdir(parents=True, exist_ok=True)
    json_path = path / "evaluation_report.json"
    md_path = path / "evaluation_report.md"

    with open(json_path, "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)
    md_path.write_text(_render_markdown(report), encoding="utf-8")
    logger.info("评测报告已输出: %s / %s", json_path, md_path)


def _render_markdown(report: dict) -> str:
    lines = [
        "# AI Agent Evaluation Report",
        "",
        f"- Generated: {report['generated_at']}",
        f"- Mode: {'offline' if report['offline'] else 'online'}",
        f"- Categories: {', '.join(report['categories'])}",
        f"- Elapsed: {report['elapsed_seconds']}s",
        "",
        "## Summary",
        "",
        "| Metric | Value | Target | Met |",
        "| --- | --- | --- | --- |",
    ]
    for k, v in report["summary"].items():
        if k.endswith("_target"):
            continue
        if k.endswith("_met"):
            continue
        target = report["summary"].get(f"{k}_target")
        met = report["summary"].get(f"{k}_met")
        target_s = f"{target:.2f}" if isinstance(target, (int, float)) else "-"
        met_s = "✅" if met else "❌" if met is False else "-"
        lines.append(f"| {k} | {v} | {target_s} | {met_s} |")

    lines += ["", "## Detail", ""]
    for r in report["results"]:
        lines.append(f"### {r['category']} ({r.get('metrics', {}).get('total', '-')} cases)")
        lines.append(f"```json\n{json.dumps(r.get('metrics', {}), ensure_ascii=False, indent=2)}\n```")
        failures = r.get("failures", [])
        if failures:
            lines.append(f"Failures: {len(failures)}")
            for f in failures:
                lines.append(f"- `{f.get('case_id')}` {f.get('category')}: {f.get('detail')}")
        lines.append("")
    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="DayLoop AI Agent Evaluation")
    parser.add_argument("--category", help="只运行指定类别，逗号分隔，如 intent,jd,rag,agent")
    parser.add_argument("--offline", action="store_true", help="跳过依赖 LLM 的评测（agent/interview/faithfulness/answer_relevance）")
    parser.add_argument("--report-dir", default=None, help="报告输出目录")
    args = parser.parse_args(argv)

    cats = [c.strip() for c in args.category.split(",") if c.strip()] if args.category else None
    if cats:
        invalid = [c for c in cats if c not in _CATEGORIES]
        if invalid:
            print(f"未知类别: {invalid}，可用: {sorted(_CATEGORIES)}", file=sys.stderr)
            return 2

    run_evaluation(categories=cats, offline=args.offline, report_dir=args.report_dir)
    return 0


if __name__ == "__main__":
    sys.exit(main())
