from __future__ import annotations

import os

from openai import OpenAI
from app.core.config import settings
from app.core.retry import retry

# token 统计（供 offline evaluation 使用）
_token_stats = {"calls": 0, "input_tokens": 0, "output_tokens": 0}


class LLMClient:
    """统一的大模型调用入口。

    - 文本问答:    chat(prompt="...")
    - 多模态/多轮: chat(messages=[{"role": ..., "content": [...]}, ...])
    所有需要调用大模型的模块都应经由本类，而不是自建 OpenAI client。
    """

    def __init__(self, cfg: dict | None = None, provider: str | None = None) -> None:
        self.cfg = dict(cfg or settings.llm_config(provider))
        settings.require_api_key(self.cfg)
        self.base_url = self.cfg["base_url"]
        self.api_key = self.cfg["api_key"]
        self.model = self.cfg["model"]
        self.client = OpenAI(base_url=self.base_url, api_key=self.api_key)

    @retry(max_times=3, base_delay=1.0)
    def chat(
        self,
        prompt: str | None = None,
        messages: list[dict] | None = None,
        temperature: float = 0.7,
    ) -> str:
        """统一的 chat 调用。

        prompt 或 messages 至少提供一个：
        - prompt:   简单文本，等价于一条 user 消息；
        - messages: 完整消息列表，content 可为字符串或多模态内容块列表
                    （如 [{"type": "text", ...}, {"type": "image_url", ...}]）。
        """
        if messages:
            msgs = messages
        elif prompt:
            msgs = [{"role": "user", "content": prompt}]
        else:
            raise ValueError("chat: 需要提供 prompt 或 messages")

        response = self.client.chat.completions.create(
            model=self.model,
            messages=msgs,
            temperature=temperature,
        )
        usage = getattr(response, "usage", None)
        if usage is not None:
            _token_stats["calls"] += 1
            _token_stats["input_tokens"] += getattr(usage, "prompt_tokens", 0) or 0
            _token_stats["output_tokens"] += getattr(usage, "completion_tokens", 0) or 0

        return response.choices[0].message.content

    @staticmethod
    def get_token_stats() -> dict:
        """返回累计的调用次数与 token 用量拷贝。"""
        return dict(_token_stats)

    @staticmethod
    def reset_token_stats() -> None:
        """清空累计统计（每个评测用例开始前调用）。"""
        _token_stats["calls"] = 0
        _token_stats["input_tokens"] = 0
        _token_stats["output_tokens"] = 0

    def audio_transcribe(self, audio_path: str) -> str:
        """语音转文字（OpenAI Whisper Audio API）。"""
        from openai import OpenAI

        api_key = os.getenv("OPENAI_API_KEY")
        base_url = os.getenv("OPENAI_BASE_URL", "https://api.openai.com/v1")
        if not api_key:
            raise RuntimeError("audio_transcribe 需要设置 OPENAI_API_KEY 环境变量")

        client = OpenAI(api_key=api_key, base_url=base_url)
        with open(audio_path, "rb") as f:
            transcription = client.audio.transcriptions.create(
                model="whisper-1",
                file=f,
                response_format="text",
            )
        text = transcription if isinstance(transcription, str) else getattr(transcription, "text", "")
        return text.strip()