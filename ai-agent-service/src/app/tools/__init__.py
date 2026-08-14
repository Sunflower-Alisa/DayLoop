from app.tools.base import BaseTool
from app.tools.dayloop_tools import register_dayloop_tools
from app.tools.jd_parser import JDParserTool
from app.tools.registry import describe_tools, get_tool, instantiate, list_tools, register, register_tool
from app.tools.web_search import WebSearchTool

# 导入时注册核心离线工具
register_tool(JDParserTool.name, JDParserTool)

__all__ = [
    "BaseTool",
    "JDParserTool",
    "WebSearchTool",
    "register",
    "register_tool",
    "get_tool",
    "instantiate",
    "list_tools",
    "describe_tools",
    "register_dayloop_tools",
]