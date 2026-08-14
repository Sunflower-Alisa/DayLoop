from __future__ import annotations

import re

from app.core.exceptions import ParsingError


def parse_json(text: str) -> dict:
    """从 LLM 输出中提取第一个 JSON 对象并解析。

    兼容模型输出前后包含解释文字 / 代码块标记的情况。
    """
    if not text:
        raise ParsingError("LLM 输出为空，无法解析 JSON")

    cleaned = text.strip()
    # 常见 go → 直接剥掉 ```json ... ``` 围栏
    fenced = re.match(r"```(?:json)?\s*(.*?)\s*```", cleaned, re.DOTALL)
    if fenced:
        cleaned = fenced.group(1).strip()

    match = re.search(r"\{.*\}", cleaned, re.DOTALL)
    if not match:
        raise ParsingError(f"无法从 LLM 输出中解析 JSON: {text[:120]}")

    import json

    return json.loads(match.group(0))