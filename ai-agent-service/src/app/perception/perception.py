from __future__ import annotations

import logging

from app.preception.asr import ASR
from app.preception.entity import EntityExtractor
from app.preception.intent import CONTEXT_REQUIREMENTS, IntentDetector
from app.preception.multimodal import MultimodalProcessor
from app.preception.normalizer import normalize_text
from app.preception.result import PerceptionResult

logger = logging.getLogger("agent-preception")


# 意图 → 任务描述（简短，供日志/AgentState 使用）
_TASK_DESC = {
    "INDUSTRY_INFO": "收集与总结 AI 行业信息",
    "JOB_SEARCH": "检索并匹配招聘信息",
    "JD_ANALYSIS": "分析JD并评估岗位匹配",
    "SKILL_GAP": "分析技能差距",
    "INTERVIEW_KNOWLEDGE": "整理面试知识库",
    "MOCK_INTERVIEW": "执行模拟面试",
    "RESUME_UPDATE": "更新简历",
    "TASK_MANAGEMENT": "管理任务",
    "GENERAL_CHAT": "通用对话",
}


class PerceptionService:
    """感知层入口。

    对应 docs/05-architecture.md §17 Perception：
    输入 User Input / DayLoop Data / External Data / Conversation，
    输出 Intent / Entity / Task / Context Requirement。
    """

    def __init__(
        self,
        use_llm_intent: bool = True,
        asr_engine: str = "whisper",
    ) -> None:
        self.entity_extractor = EntityExtractor()
        self.intent_detector = IntentDetector(use_llm=use_llm_intent)
        self.asr = ASR(engine=asr_engine)
        self.multimodal = MultimodalProcessor()
        self._use_llm_intent = use_llm_intent

    def perceive(
        self,
        message: str | None = None,
        *,
        audio_path: str | None = None,
        image_path: str | None = None,
        extra: dict | None = None,
    ) -> PerceptionResult:
        """感知一条用户输入。

        Args:
            message: 用户文本输入
            audio_path: 音频文件路径（语音 → 文本）
            image_path: 图片文件路径（图片 → 文本/结构化描述）
            extra: 额外的 DayLoop 数据/会话信息，写入 metadata
        """
        modality = self._detect_modality(message, audio_path, image_path)
        raw = message or ""

        # 语音输入：ASR 转写
        if audio_path and modality in ("audio", "multimodal"):
            raw = self.asr.transcribe(audio_path)

        # 图片输入：多模态分析提取文本
        image_note = ""
        if image_path and modality in ("image", "multimodal"):
            analysis = self.multimodal.analyze(image_path)
            image_note = self._format_image_analysis(analysis)

        text = normalize_text(image_note + " " + raw) if image_note else normalize_text(raw)

        # 实体抽取（Rule-based，稳定可离线）
        entities = [e.to_dict() for e in self.entity_extractor.extract(text)]

        # 意图识别（Rule + LLM）
        intent_result = self.intent_detector.detect(text, entities)

        result = PerceptionResult(
            text=text,
            modality=modality,
            intent=intent_result.intent,
            intent_confidence=intent_result.confidence,
            entities=entities,
            context_requirements=list(CONTEXT_REQUIREMENTS.get(intent_result.intent, [])),
            task=_TASK_DESC.get(intent_result.intent, ""),
            metadata={
                "intent_method": intent_result.method,
                "intent_hints": intent_result.hints,
                "audio_path": audio_path,
                "image_path": image_path,
                "extra": extra or {},
            },
            raw=message or "",
        )
        logger.info(
            "perception | modality=%s intent=%s conf=%.2f method=%s entities=%d",
            modality,
            result.intent,
            result.intent_confidence,
            intent_result.method,
            len(entities),
        )
        return result

    @staticmethod
    def _detect_modality(message, audio_path, image_path) -> str:
        has_text = bool(message and message.strip())
        has_audio = bool(audio_path)
        has_image = bool(image_path)
        modes = sum([has_text, has_audio, has_image])
        if modes >= 2:
            return "multimodal"
        if has_audio:
            return "audio"
        if has_image:
            return "image"
        return "text"

    @staticmethod
    def _format_image_analysis(analysis: dict) -> str:
        parts: list[str] = []
        content = analysis.get("content")
        if content:
            parts.append(str(content))
        key_text = analysis.get("key_text") or []
        if key_text:
            parts.append("关键文字：" + "；".join(str(k) for k in key_text))
        return " ".join(parts)