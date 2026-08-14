from __future__ import annotations

import re

# 常见不需要的空字符：零宽字符、BOM、全角空格等
_ZERO_WIDTH = re.compile(r"[\u200b\u200c\u200d\ufeff]")

# 多空格 / 制表符 / 换行合并
_WS = re.compile(r"[ \t\r\n\u3000]+")


def normalize_text(raw: str) -> str:
    """标准化用户输入。

    原则：Normalizer 不要过度修改用户原话。
    只做无害的清洗：
    1. 去除零宽字符 / BOM
    2. 统一全角空格
    3. 压缩连续空白（含换行），保留句间语义
    """
    if not raw:
        return ""

    text = _ZERO_WIDTH.sub("", raw)
    text = _WS.sub(" ", text).strip()
    return text
