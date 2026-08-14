from __future__ import annotations

from unittest.mock import MagicMock, patch

import pytest

from app.tools.web_search import WebSearchTool

_BING_HTML = '''<li class="b_algo"><h2 class=""><a target="_blank" href="https://example.com/1">AI 行业动态</a></h2>
<div class="b_caption"><p class="b_lineclamp2">上海AI实验室开源科研智能体工作台</p></div></li>
<li class="b_algo"><h2 class=""><a target="_blank" href="https://example.com/2">大模型应用落地</a></h2>
<div class="b_caption"><p class="b_lineclamp2">RAG 应用实践分享</p></div></li>'''


def _fake_response(text: str = _BING_HTML):
    resp = MagicMock()
    resp.status_code = 200
    resp.text = text
    resp.raise_for_status = MagicMock()
    return resp


@patch("httpx.get", return_value=_fake_response())
def test_web_search_bing_ok(mock_get):
    result = WebSearchTool().execute(query="AI 行业动态", max_results=2)
    assert result["ok"] is True
    assert len(result["results"]) == 2
    assert result["results"][0]["title"] == "AI 行业动态"
    assert result["results"][0]["url"] == "https://example.com/1"


@patch("httpx.get", return_value=_fake_response("<html></html>"))
def test_web_search_no_results(mock_get):
    result = WebSearchTool().execute(query="没有结果的查询", max_results=3)
    assert result["ok"] is False
    assert result["results"] == []


@patch("httpx.get", side_effect=Exception("timeout"))
def test_web_search_provider_failure(mock_get):
    result = WebSearchTool().execute(query="任意查询")
    assert result["ok"] is False
    assert "失败" in result["error"] or "超时" in result["error"] or result["error"]


def test_web_search_empty_query():
    with pytest.raises(ValueError):
        WebSearchTool().execute(query="   ")


@patch("httpx.get", return_value=_fake_response())
def test_web_search_accepts_text_alias(mock_get):
    """兼容晒评工具用 text= 调用（rule_based evaluate_tool）。"""
    result = WebSearchTool().execute(text="AI Agent 岗位")
    assert result["ok"] is True