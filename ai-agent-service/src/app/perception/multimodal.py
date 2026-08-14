from __future__ import annotations

import base64
import os

from app.core.exceptions import PerceptionError
from app.core.logging import get_logger
from app.llm.client import LLMClient

logger = get_logger("perception.multimodal")


class MultimodalProcessor:
    """图片感知：通过多模态 LLM 分析图片内容。"""

    def analyze(self, image_path: str) -> dict:
        """分析图片，返回结构化描述。

        返回:
        {
          "content": 图片内容一句话概括,
          "key_text": 图片中的关键文字,
          "objects":  图中重要对象列表,
          "possible_intent": 可能的用户意图,
        }
        """
        if not image_path or not os.path.exists(image_path):
            raise PerceptionError(f"图片文件不存在: {image_path}")

        extension = os.path.splitext(image_path)[1].lower() or ".png"
        mime = {
            ".png": "image/png",
            ".jpg": "image/jpeg",
            ".jpeg": "image/jpeg",
            ".webp": "image/webp",
            ".gif": "image/gif",
        }.get(extension, "image/png")

        with open(image_path, "rb") as f:
            image_b64 = base64.b64encode(f.read()).decode("utf-8")

        data_url = f"data:{mime};base64,{image_b64}"
        content = self._analyze_via_llm(data_url)
        logger.info("multimodal analyze done")
        return content

    @classmethod
    def _analyze_via_llm(cls, image_data_url: str) -> dict:
        """统一通过 LLMClient 调用多模态模型。"""
        prompt = """
        请分析这张图片。
        返回 JSON（不要额外解释）：
        {
          "content": "图片内容一句话概括",
          "key_text": ["图片中的关键文字"],
          "objects": ["重要对象"],
          "possible_intent": "可能的用户意图，如 JD 截图 / 简历截图 / 面试题截图 / 其他"
        }
        """
        client = LLMClient(provider="openai")
        # 多模态图片使用 openai，模型可被环境变量覆盖
        client.model = os.getenv("OPENAI_MULTIMODAL_MODEL", client.model)
        messages = [
            {
                "role": "user",
                "content": [
                    {"type": "text", "text": prompt},
                    {"type": "image_url", "image_url": {"url": image_data_url}},
                ],
            }
        ]
        text = client.chat(messages=messages, temperature=0.2)

        from app.llm.parser import parse_json

        try:
            return parse_json(text)
        except Exception:
            logger.warning("multimodal LLM 返回非 JSON，使用降级结构")
            return {
                "content": text,
                "key_text": [],
                "objects": [],
                "possible_intent": "其他",
            }