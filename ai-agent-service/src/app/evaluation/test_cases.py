from __future__ import annotations

import json
from pathlib import Path

from app.core.logging import get_logger

logger = get_logger("evaluation.datasets")

_DATASETS_DIR = Path(__file__).parent / "datasets"


def load_cases(name: str) -> list[dict] | dict:
    """加载 datasets/{name}.json 用例文件（docs/06-evaluation.md §48）。

    返回 list（intent/jd/skill/memory/planner/tool/interview/agent）或 dict（rag）。
    文件缺失时返回空结构并告警，避免阻塞评测主流程。
    """
    path = _DATASETS_DIR / f"{name}.json"
    if not path.exists():
        logger.warning("评测数据集缺失: %s", path)
        return [] if name != "rag" else {"documents": [], "queries": []}
    try:
        with open(path, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception as exc:
        logger.warning("评测数据集解析失败: %s (%s)", path, exc)
        return [] if name != "rag" else {"documents": [], "queries": []}


def available_datasets() -> list[str]:
    return sorted(p.stem for p in _DATASETS_DIR.glob("*.json"))
