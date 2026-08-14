from __future__ import annotations

import os

import httpx

from app.core.logging import get_logger
from app.tools.base import BaseTool

logger = get_logger("tools.web_search")

# 搜索提供商：bing（默认，免 key HTML 抓取）/ duckduckgo / bing_api / tavily / serpapi
PROVIDER = os.getenv("WEB_SEARCH_PROVIDER", "bing").lower()

# 各提供商 API key（可选；bing_html / duckduckgo 免 key）
BING_KEY = os.getenv("BING_SEARCH_KEY", "")
TAVILY_KEY = os.getenv("TAVILY_API_KEY", "")
SERPAPI_KEY = os.getenv("SERPAPI_KEY", "")

TIMEOUT = float(os.getenv("WEB_SEARCH_TIMEOUT", "8"))
MAX_RESULTS = int(os.getenv("WEB_SEARCH_MAX_RESULTS", "5"))

_UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120 Safari/537.36"


class WebSearchTool(BaseTool):
    """Web 搜索工具（docs/05-architecture.md §31 Tool 列表）。

    默认使用 Bing 网页搜索（免 key HTML 解析，当前网络可访问）。
    也可通过 WEB_SEARCH_PROVIDER 切换为 duckduckgo / bing_api / tavily / serpapi。
    主提供商失败时自动回退 Bing 网页搜索；全部不可用时返回
    {"ok": False} 便于 Agent 降级，不会抛错中断流程。
    """

    name = "web_search"

    def execute(self, query: str = "", text: str = "", max_results: int | None = None, **kwargs) -> dict:
        query = query or text
        if not query or not query.strip():
            raise ValueError("web_search 需要提供 query")

        limit = max_results or MAX_RESULTS
        results: list[dict] = []
        error: str | None = None

        # 依次尝试：主提供商 → Bing HTML 回退
        attempts: list[tuple[str, object]] = [(PROVIDER, None)]
        if PROVIDER != "bing":
            attempts.append(("bing", None))

        for provider, _ in attempts:
            fn = self._provider_fn(provider)
            if fn is None:
                error = f"未配置可用的 Web Search 提供商（{provider}），请配置 API key 或改用 bing/duckduckgo"
                continue
            results, error = fn(query, limit)
            if results:
                break

        if error or not results:
            logger.warning("web_search 无结果: query=%s error=%s", query, error)
            return {"ok": False, "tool": self.name, "query": query, "results": results, "error": error or "无搜索结果"}

        logger.info("web_search done | query=%s results=%d", query, len(results))
        return {"ok": True, "tool": self.name, "query": query, "results": results}

    def _provider_fn(self, provider: str):
        if provider == "bing":
            return self._search_bing_html
        if provider == "duckduckgo":
            return self._search_duckduckgo
        if provider == "bing_api":
            return self._search_bing if BING_KEY else None
        if provider == "tavily":
            return self._search_tavily if TAVILY_KEY else None
        if provider == "serpapi":
            return self._search_serpapi if SERPAPI_KEY else None
        return None

    # ---- Bing HTML（默认，免 key） ----

    def _search_bing_html(self, query: str, limit: int) -> tuple[list[dict], str | None]:
        import html as html_mod
        import re

        try:
            resp = httpx.get(
                "https://cn.bing.com/search",
                params={"q": query},
                headers={"User-Agent": _UA},
                timeout=TIMEOUT,
                follow_redirects=True,
            )
            resp.raise_for_status()
        except Exception as exc:
            return [], f"Bing 请求失败: {exc}"

        results: list[dict] = []
        for block in re.findall(r'<li class="b_algo".*?</li>', resp.text, re.DOTALL):
            title_m = re.search(r'<h2[^>]*><a[^>]*href="([^"]+)"[^>]*>(.*?)</a></h2>', block, re.DOTALL)
            snippet_m = re.search(r'<p[^>]*class="b_lineclamp[0-9]"[^>]*>(.*?)</p>', block, re.DOTALL)
            if not title_m:
                continue
            title = html_mod.unescape(re.sub(r"<[^>]+>", "", title_m.group(2))).strip()
            results.append(
                {
                    "title": title,
                    "url": html_mod.unescape(title_m.group(1)),
                    "snippet": html_mod.unescape(re.sub(r"<[^>]+>", "", snippet_m.group(1))).strip()
                    if snippet_m
                    else "",
                }
            )
            if len(results) >= limit:
                break
        return results, None

    # ---- DuckDuckGo（免 key） ----

    def _search_duckduckgo(self, query: str, limit: int) -> tuple[list[dict], str | None]:
        import html as html_mod
        import re

        url = "https://html.duckduckgo.com/html/"
        try:
            resp = httpx.get(
                url,
                params={"q": query, "kl": "cn-zh"},
                headers={"User-Agent": _UA},
                timeout=TIMEOUT,
                follow_redirects=True,
            )
            resp.raise_for_status()
        except Exception as exc:
            return [], f"DuckDuckGo 请求失败: {exc}"

        results: list[dict] = []
        for block in re.findall(r'<div class="result[^"]*">(.*?)</div>\s*</div>', resp.text, re.DOTALL):
            title_m = re.search(r'<a[^>]*class="result__a"[^>]*href="([^"]+)"[^>]*>(.*?)</a>', block, re.DOTALL)
            snippet_m = re.search(r'<a[^>]*class="result__snippet"[^>]*>(.*?)</a>', block, re.DOTALL)
            if not title_m:
                continue
            title = re.sub(r"<[^>]+>", "", title_m.group(2))
            results.append(
                {
                    "title": html_mod.unescape(title).strip(),
                    "url": html_mod.unescape(title_m.group(1)),
                    "snippet": html_mod.unescape(re.sub(r"<[^>]+>", "", snippet_m.group(1))).strip()
                    if snippet_m
                    else "",
                }
            )
            if len(results) >= limit:
                break
        return results, None

    # ---- Bing API ----

    def _search_bing(self, query: str, limit: int) -> tuple[list[dict], str | None]:
        try:
            resp = httpx.get(
                "https://api.bing.microsoft.com/v7.0/search",
                params={"q": query, "count": limit, "mkt": "zh-CN"},
                headers={"Ocp-Apim-Subscription-Key": BING_KEY},
                timeout=TIMEOUT,
            )
            resp.raise_for_status()
            data = resp.json()
        except Exception as exc:
            return [], f"Bing API 请求失败: {exc}"

        results = [
            {
                "title": item.get("name", ""),
                "url": item.get("url", ""),
                "snippet": item.get("snippet", ""),
            }
            for item in data.get("webPages", {}).get("value", [])[:limit]
        ]
        return results, None

    # ---- Tavily ----

    def _search_tavily(self, query: str, limit: int) -> tuple[list[dict], str | None]:
        try:
            resp = httpx.post(
                "https://api.tavily.com/search",
                json={"api_key": TAVILY_KEY, "query": query, "max_results": limit},
                timeout=TIMEOUT,
            )
            resp.raise_for_status()
            data = resp.json()
        except Exception as exc:
            return [], f"Tavily 请求失败: {exc}"

        results = [
            {
                "title": item.get("title", ""),
                "url": item.get("url", ""),
                "snippet": item.get("content", ""),
            }
            for item in data.get("results", [])[:limit]
        ]
        return results, None

    # ---- SerpAPI ----

    def _search_serpapi(self, query: str, limit: int) -> tuple[list[dict], str | None]:
        try:
            resp = httpx.get(
                "https://serpapi.com/search.json",
                params={"engine": "google", "q": query, "api_key": SERPAPI_KEY, "num": limit, "hl": "zh-cn"},
                timeout=TIMEOUT,
            )
            resp.raise_for_status()
            data = resp.json()
        except Exception as exc:
            return [], f"SerpAPI 请求失败: {exc}"

        results = [
            {
                "title": item.get("title", ""),
                "url": item.get("link", ""),
                "snippet": item.get("snippet", ""),
            }
            for item in data.get("organic_results", [])[:limit]
        ]
        return results, None


# 注册到全局 registry（导入即注册，与 jd_parser 一致）
from app.tools.registry import register_tool

register_tool(WebSearchTool.name, WebSearchTool)
