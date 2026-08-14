from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any


@dataclass
class PerceptionResult:
    """感知层输出：对用户输入的标准化理解结果。

    对应 05-architecture.md §17 Perception：
    输入 User Input / DayLoop Data / External Data / Conversation，
    输出 Intent / Entity / Task / Context Requirement。
    """

    text: str | None = None                      # 标准化后的文本（语音/图片转写后统一到这里）
    modality: str = "text"                       # text / audio / image / multimodal
    intent: str = "GENERAL_CHAT"                 # 意图，见 intent.py 中 INTENT_* 常量
    intent_confidence: float = 0.0               # 意图置信度
    entities: list[dict] = field(default_factory=list)   # 提取的实体列表
    context_requirements: list[str] = field(default_factory=list)  # 需要的上下文（JD/Resume/Skills...）
    task: str = ""                               # 当前任务的简短描述
    metadata: dict[str, Any] = field(default_factory=dict)  # 来源、耗时、原始输入等附加信息
    raw: str = ""                                # 原始输入文本（未经标准化）
