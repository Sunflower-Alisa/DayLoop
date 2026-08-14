from __future__ import annotations

import os

PROVIDER = os.getenv("LLM_PROVIDER", "deepseek")

PROVIDERS = {
    "deepseek": {
        "base_url": "https://api.deepseek.com",
        "api_key_env": "DEEPSEEK_API_KEY",
        "api_key": os.getenv("DEEPSEEK_API_KEY"),
        "model": "deepseek-v4-flash",
    },
    "openai": {
        "base_url": "https://api.openai.com/v1",
        "api_key_env": "OPENAI_API_KEY",
        "api_key": os.getenv("OPENAI_API_KEY"),
        "model": "gpt-4o-mini",
    },
}

if PROVIDER not in PROVIDERS:
    raise ValueError(f"未知 LLM 提供商: {PROVIDER}，可选: {', '.join(PROVIDERS)}")


class Settings:
    """统一配置入口。"""

    def llm_config(self, provider: str | None = None) -> dict:
        """返回指定（或当前）提供商的 LLM 配置副本。

        供 LLMClient 及所有需要调用大模型的模块使用，避免各模块直接读环境变量。
        """
        name = provider or PROVIDER
        if name not in PROVIDERS:
            raise ValueError(f"未知 LLM 提供商: {name}，可选: {', '.join(PROVIDERS)}")
        return dict(PROVIDERS[name])

    def require_api_key(self, cfg: dict) -> None:
        """校验 API key，缺失时给出明确错误（在真正要调用大模型时才检查）。"""
        if not cfg.get("api_key"):
            raise RuntimeError(
                f"LLM 未配置 API key（{cfg.get('api_key_env')}），"
                "请设置对应环境变量后再调用大模型"
            )


settings = Settings()

# 兼容旧引用
cfg = PROVIDERS[PROVIDER]
MODEL = cfg["model"]
