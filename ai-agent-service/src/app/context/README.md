# Context 上下文管理模块

`app/context` 是 AI Agent 的上下文管理层，对应 `docs/05-architecture.md` §19 Context Manager。

## 职责

根据当前任务动态构建 LLM 上下文。设计核心原则：

> **不将所有用户数据全部发送给 LLM，而是根据当前任务选择必要的信息。**

Context 可能包含：Current Input、Conversation History、User Profile、Resume、Skill Profile、Long-term Memory、Retrieved Knowledge、Current Task、Plan、Tool Results。

## 目录结构

| 文件 | 说明 |
| --- | --- |
| `schema.py` | 数据模型：`ContextBlock`（单块上下文）、`AgentContext`（整体上下文） |
| `provider.py` | 数据源抽象：`ContextProvider` 接口、`ProviderRegistry` 路由、`StaticProvider` 内存兜底 |
| `builder.py` | 构建器：需求解析 → Provider 读取 → token 预算裁剪 → 拼装 |
| `manager.py` | 高层入口：`build()` / `to_prompt()` / `merge()` |
| `__init__.py` | 包公开 API |

## 数据流

```
PerceptionResult (intent + context_requirements)
        │
        ▼
ContextBuilder.build()
   1. 总是携带 current_input（用户消息本身）
   2. 拼接 conversation_history（short-term memory，最近 N 轮）
   3. 只加载 requirements 声明过的上下文块
        └─→ ProviderRegistry.fetch(want) ─→ 数据源 (DayLoop API / RAG / Memory / 会话)
   4. 附加 Working Memory：plan / current_step
   5. token 预算裁剪（超出 max_tokens 从低优先级块截断/丢弃）
        │
        ▼
AgentContext (blocks / missing / plan / estimates)
        │
        ▼
ContextManager.to_prompt()  →  结构化 prompt 文本（发给 LLM）
```

## 核心概念

### ContextBlock（schema.py）

一块上下文数据：

```python
ContextBlock(
    name="resume",           # 上下文块类型（CTX_* 常量）
    content="3年Python...",   # 格式化后的文本（最终进 prompt）
    source="dayloop",        # dayloop / rag / memory / session / tool / static
    priority=10,             # 拼接顺序，越小越靠前
    token_estimate=0,        # 预估 token 数（用于预算裁剪）
    metadata={},             # 附加信息（更新时间、置信度、原始 JSON）
)
```

### AgentContext（schema.py）

一次请求构建出的完整上下文，字段：

| 字段 | 说明 |
| --- | --- |
| `requirements` | 本次任务声明的上下文需求（来自 PerceptionResult） |
| `blocks` | 实际加载的上下文块，按 priority 排序 |
| `missing` | 声明了但未能加载的块名（供日志/降级） |
| `task` / `plan` / `current_step` | 当前任务状态（Working Memory） |
| `estimates` | 预算信息（total_tokens / max_tokens） |

`AgentContext.format()` 把块拼成 `## name\ncontent` 章节结构；`AgentContext.block(name)` 按名取块。

### Provider 抽象（provider.py）

每种上下文类型对应一个数据源。真正的数据由外部系统提供：

- **DayLoop API**：Profile / Resume / Skills / Tasks / Jobs / Knowledge
- **RAG**：知识检索
- **Memory**：长期记忆
- **Session**：会话内临时数据

```python
class ContextProvider(ABC):
    def fetch(self, ctx_name: str, payload: dict | None = None) -> ContextBlock | None:
        ...
```

`ProviderRegistry` 把「需求名 → Provider」做路由；Provider 失败不会中断整体构建（有 try/except 兜底）。

`StaticProvider` 是内存版实现，注册预置数据即可用，适合离线测试 / 无后端时的降级。

## 使用说明

### 1. 最小示例

```python
from app.perception import PerceptionService
from app.context import ContextManager

# 1. 感知
result = PerceptionService(use_llm_intent=False).perceive("帮我分析这个AI Agent岗位的JD")

# 2. 构建上下文（未注册任何 Provider，只有 current_input + conversation）
ctx = ContextManager().build(result, conversation_history=[...])

# 3. 渲染为 prompt 文本
prompt = ContextManager().to_prompt(ctx)
```

### 2. 接入真实数据源

实现 `ContextProvider` 并注册到 `ProviderRegistry`：

```python
from app.context import ContextManager, ContextBuilder, ProviderRegistry, ContextProvider

class DayLoopProvider(ContextProvider):
    name = "dayloop"
    def fetch(self, ctx_name, payload=None):
        # 通过 DayLoop Agent API 获取，如 GET /api/v1/agent/resume
        data = call_dayloop_api(ctx_name, payload["user_id"])
        if not data:
            return None
        return ContextBlock(name=ctx_name, content=format_for_prompt(data),
                            source="dayloop", priority=10)

providers = ProviderRegistry()
for want in ["JD", "Resume", "Skill Profile", "Job Preference", "Memory"]:
    providers.register(want, DayLoopProvider())
providers.register("Tasks", ...)
providers.register("Interview Knowledge", ...)  # 走 RAG

mgr = ContextManager(ContextBuilder(providers=providers))
ctx = mgr.build(result, user_id="alisa", session_id="s1", plan=[...], current_step="技能匹配")
```

### 3. 完整参数说明

`ContextManager.build()`：

| 参数 | 说明 |
| --- | --- |
| `result` | `PerceptionResult`（必填，含 intent / context_requirements） |
| `user_id` / `session_id` | 用户与会话标识，透传给 Provider 的 payload |
| `conversation_history` | 会话历史（short-term memory），最近 8 轮 |
| `plan` | 任务步骤计划（Planner 产出） |
| `current_step` | 当前执行步骤 |
| `max_tokens` | 上下文预算上限，默认 2000 |

### 4. 执行过程中增量更新

```python
ctx = mgr.build(result, ...)

# Planner / Executor 产出后写回 Working Memory
mgr.merge(ctx, plan=["解析JD", "技能匹配", "给出建议"])
mgr.merge(ctx, tool_results=[{"tool": "get_resume", "ok": True}])
```

## 与其它模块的关系

- **上游**：`app/perception` —— 提供 `intent` 与 `context_requirements`（意图 → 需求映射见 perception/intent.py 的 `CONTEXT_REQUIREMENTS`）
- **下游**：Planner / Executor / LLM —— 消费 `AgentContext.to_prompt()` 作为 prompt
- **数据源**：DayLoop Agent API / RAG / Memory —— 通过 `ContextProvider` 注入

## 常见问题

**Q: 不注册任何 Provider 会怎样？**
A: 只有 `current_input` 和 `conversation_history` 会被带上，其余需求进入 `missing` 列表，不会崩溃。适合先跑通流程再接入数据。

**Q: 上下文超长怎么办？**
A: `max_tokens` 预算裁剪：超出时从低优先级的业务块开始截断/丢弃，`current_input` 和 `conversation_history` 始终保留。可通过 `estimates` 查看裁剪结果。

**Q: 如何新增一种上下文类型？**
A: 在 `schema.py` 定义 `CTX_*` 常量 → 在感知层的 `CONTEXT_REQUIREMENTS` 中按意图声明 → 注册对应 Provider。
