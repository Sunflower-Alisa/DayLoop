from __future__ import annotations

import os

from app.core.exceptions import PerceptionError
from app.core.logging import get_logger
from app.llm.client import LLMClient

logger = get_logger("perception.asr")


class ASR:
    """语音转文字（Speech-to-Text）感知。

    支持 engine:
    - whisper: 调用 OpenAI Whisper API（无需本地模型）
    """

    def __init__(self, engine: str = "whisper") -> None:
        self.engine = engine

    def transcribe(self, audio_path: str) -> str:
        """将音频文件转写为文字。"""
        if not audio_path or not os.path.exists(audio_path):
            raise PerceptionError(f"音频文件不存在: {audio_path}")

        if self.engine == "whisper":
            return LLMClient().audio_transcribe(audio_path)

        raise PerceptionError(f"Unsupported ASR engine: {self.engine}")