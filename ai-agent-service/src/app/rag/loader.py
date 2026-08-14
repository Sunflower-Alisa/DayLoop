from __future__ import annotations

import os
import re

from app.core.exceptions import RAGError
from app.core.logging import get_logger

logger = get_logger("rag.loader")


def load_pdf(path: str) -> list[str]:
    """读取 PDF 并返回文本页列表（docs/05-architecture.md §40）。

    依赖 pypdf；未安装时抛出 RAGError。
    """
    if not os.path.exists(path):
        raise RAGError(f"PDF 文件不存在: {path}")
    try:
        from pypdf import PdfReader
    except ImportError as exc:
        raise RAGError("load_pdf 需要安装 pypdf (pip install pypdf)", cause=exc) from exc

    try:
        reader = PdfReader(path)
        pages = []
        for page in reader.pages:
            text = page.extract_text() or ""
            text = re.sub(r"[\x00-\x08\x0b\x0c\x0e-\x1f]", "", text)
            text = re.sub(r"\s+", " ", text).strip()
            if text:
                pages.append(text)
    except Exception as exc:
        raise RAGError(f"解析 PDF 失败: {path}", cause=exc) from exc
    logger.info("load_pdf | %s -> %d pages", path, len(pages))
    return pages