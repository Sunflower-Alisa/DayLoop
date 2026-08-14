# app/core —— 工程化横切能力使用指南

`app/core/` 提供跨层复用的基础能力：配置、日志、追踪、异常、重试、指标、兜底处理。
任何模块（agents/tools/rag/memory/api/services）都可以直接引用。

## 1. Config（配置管理）

基于 `pydantic-settings`，支持「默认值 → .env 文件 → 环境变量」三级覆盖，启动时类型校验。

```python
from app.core.config import settings, get_settings

# 全局单例（lru_cache，首次 import 即加载 .env）
settings.log_level                     # "INFO"
settings.llm_provider                  # "deepseek"
settings.memory_short_term_turns       # 10

# 动态切换 provider 后获取完整 LLM 配置（缺失 key 会抛错）
cfg = settings.llm_config()            # {"provider","base_url","api_key","model"}

# 测试中重载配置（换 .env 路径）
from app.core.config import Settings
test_settings = Settings(_env_file="tests/fixtures/.env.test")
```

配置文件：项目根 `.env`，模板见 `config/.env.example`。

## 2. Logging（日志）

统一日志入口，一次初始化，全局生效；日志级别由配置控制。

```python
from app.core.logging import setup_logging
import logging

logger = logging.getLogger("agent-core.agent")   # 任意 logger，建议按模块命名

def run(self):
    setup_logging("INFO")
    logger.info("analysis start")
    logger.warning("low confidence %.2f", 0.3)
    logger.exception("retry exhausted")          # 自动带 traceback
```

## 3. Tracing（调用链追踪）

基于 `contextvars` 的 request_id 贯穿，同一请求内所有日志/调用可串成一条链路。

```python
from app.core.tracing import trace, request_id_var
import logging

logger = logging.getLogger("agent-core")

def handle_message(user_id: str, message: str) -> dict:
    with trace(f"chat:{user_id}") as rid:       # 生成/复用 request_id
        logger.info("[%s] user message: %s", request_id_var.get(), message[:20])
        result = llm_client.chat(...)
        logger.info("[%s] result=%s", request_id_var.get(), result)
        return ...
```

## 4. Exceptions（异常体系）

统一错误码 + 分层异常，入口处按 code 转 HTTP 响应。

```python
from app.core.exceptions import FrameworkError, LLMError, ToolError, AgentError, ConfigError

# 抛出自定义异常
raise LLMError("provider timeout")              # code="E_LLM"

# 顶层捕获
try:
    result = supervisor.run(state)
except FrameworkError as e:
    return {"error": e.code, "message": str(e)}
```

## 5. Retry（重试）

指数退避 + 随机抖动，装饰任意可能瞬时失败的函数（LLM/Tool 调用）。

```python
from app.core.retry import retry

@retry(max_times=3, base_delay=1.0, jitter=True)
def call_llm(prompt: str) -> str:
    return client.chat([{"role": "user", "content": prompt}])
```

默认：重试 3 次，起始延迟 1s，每次 ×2 后加 ±50% 抖动。重试耗尽后异常原样上抛。

## 6. Metrics（指标）

进程内计数器 + 耗时统计，可汇出快照接入监控。

```python
from app.core.metrics import metrics

metrics.inc("chat.messages")
with metrics.timeit("llm.call"):
    reply = client.chat(...)

# 汇报时
report = metrics.snapshot()
# {"counters": {...}, "timings": {...}}
```

## 7. ErrorHandler（节点兜底）

`@safe_node` 装饰 Agent 图节点：单节点失败不拖垮整个链路，可选降级。

```python
from app.core.error_handler import safe_node

@safe_node(fallback=lambda state: {"result": "unknown"})
def analysis_node(state: dict) -> dict:
    return agent.run(state)
```

不带 fallback 时失败异常原样上抛（由上层或入口统一处理）。