from __future__ import annotations

import pytest

from app.tools.jd_parser import JDParserTool


@pytest.fixture
def parser():
    return JDParserTool()


JD_AGENT = """AI Agent 应用开发工程师
岗位职责：负责基于 LangGraph 与 RAG 的 AI Agent 应用开发，完成多步任务编排。
任职要求：3年以上Python开发经验，熟悉 LangChain、FastAPI、Tool Calling，本科以上学历。
工作地点：北京，薪资 25k-45k。"""


def test_parse_basic_fields(parser):
    result = parser.execute(text=JD_AGENT)
    assert result["ok"] is True
    assert "AI" in result["job_title"]
    assert result["city"] == "北京"
    assert result["salary"] == "25k-45k"
    assert result["education"] == "本科"


def test_parse_skills_extraction(parser):
    result = parser.execute(text=JD_AGENT)
    assert "Python" in result["skills"]
    assert "RAG" in result["skills"]
    assert "LangGraph" in result["skills"]
    assert "Tool Calling" in result["skills"]


def test_parse_requires_nonempty_text(parser):
    with pytest.raises(ValueError):
        parser.execute(text="   ")


def test_parse_empty_jd_ok_false(parser):
    result = parser.execute(text="这是一个完全没有任何格式的正常english文本，什么都没有。")
    assert result["ok"] is True


def test_parse_jd_type(parser):
    result = parser.execute(text=JD_AGENT)
    assert result["jd_type"] in {"AI Agent应用开发", "AI岗位", "其他"}