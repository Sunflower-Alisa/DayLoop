# DayLoop AI Agent Service

AI 求职助手 Agent 服务：围绕「行业信息 → 招聘机会 → JD 分析 → 能力 Gap → 面试知识 → 模拟面试」建立求职辅助闭环。

```
DayLoop（前端/.NET/Node）── HTTP ──▶ 本服务（FastAPI）
                                      │
        ┌───────────────┴───────────────────┐
        ▼                                   ▼
   Perception 层                     Agents 层
   （意图/实体识别）                  （Use Case Agent 执行）
        │                                   │
        ▼                                   ▼
   Context 层                      Tools / RAG / Memory / LLM
   （按需构建上下文）
```

## 目录结构

```
src/app
├── main.py                  FastAPI 入口（/api/v1/chat、/api/v1/health）
├── state.py                 AgentState：贯穿全流程的统一状态
├── core/                    日志/异常/配置/重试/追踪 等基础设施
├── perception/              感知层（意图识别、实体抽取、ASR、多模态）
├── context/                 上下文管理（按需加载、token 预算裁剪）
├── agents/                  意图路由 + 各 Use Case Agent + LangGraph 编排
├── runtime/                 Planner / Executor / Decision / Reflection
├── tools/                   JD Parser、DayLoop 业务工具、工具注册
├── rag/                     RAG 管道（切分 / Embedding / ChromaDB 检索 / 重排）
├── memory/                  短期 / 长期（JSON 落盘）/ 语义记忆
├── llm/                     统一 LLM 入口（LLMClient）+ prompt/解析
└── fastapi/                 API 路由、鉴权、中间件
```

## 快速开始

### 1. 环境准备

```bash
cd ai-agent-service/src
# 安装依赖（已有 .venv 可跳过）
.venv/Scripts/python.exe -m pip install -r requirements.txt
```

### 2. 配置

```bash
# LLM（deepseek 默认，用于意图兜底 + Agent 推理）
LLM_PROVIDER=deepseek
DEEPSEEK_API_KEY=sk-xxx

# 可选：OpenAI（多模态 / ASR / Embedding 需 API Key；缺失时 Embedding 走离线哈希）
OPENAI_API_KEY=sk-xxx

# Web Search（bing 默认免 key；也可切换 duckduckgo / tavily / serpapi）
WEB_SEARCH_PROVIDER=bing
# TAVILY_API_KEY=xxx   SERPAPI_KEY=xxx   BING_SEARCH_KEY=xxx

# DayLoop Agent Integration API（§9，Node 主后端端口 3001）
DAYLOOP_API_BASE=http://localhost:3001/api/v1/agent
DAYLOOP_SERVICE_TOKEN=DayLoop-Agent-Service-Token-2026

# 运行时产物目录（默认落在 src 下，已加入 .gitignore）
# CHROMA_PERSIST_DIR=.chroma   MEMORY_DIR=.memory
```

### 3. 启动

```bash
.venv/Scripts/python.exe -m uvicorn app.main:app --port 5173 --reload
```

### 4. 调用

```bash
# 健康检查
curl http://localhost:5173/api/v1/health

# 统一 Agent 入口
curl -X POST http://localhost:5173/api/v1/chat \
  -H "Content-Type: application/json" \
  -d '{"user_id":"u1","session_id":"s1","message":"帮我分析这个AI Agent岗位JD，要求熟悉Python、RAG"}'
```

## 支持的能力（Use Case）

| 输入示例 | 意图 | 处理 Agent |
| --- | --- | --- |
| 帮我分析这个 JD / 这个岗位怎么样 | JD_ANALYSIS | JD 结构化分析 + 匹配度 + 投递建议 |
| 我有哪些技能差距 / 帮我做能力诊断 | SKILL_GAP | 技能匹配 + 差距 + 学习建议 |
| 开始模拟面试 | MOCK_INTERVIEW | 多轮动态面试 + 总结报告 |
| 有什么AI行业新闻 / 行业日报 | INDUSTRY_INFO | Web 实时搜索 + 行业动态 + 日报 |
| 帮我找AI Agent开发岗位 | JOB_SEARCH | 招聘匹配 + 排序 |
| 整理我的面试题 / 面试笔记 | INTERVIEW_KNOWLEDGE | 面试题提取 + 知识库 |
| 更新我的简历，加上RAG经验 | RESUME_UPDATE | 简历生成 + 写回 |
| 看看今天有哪些任务 / 创建任务 | TASK_MANAGEMENT | 任务查询/创建 |
| 其他闲聊 | GENERAL_CHAT | 自由对话（兜底） |

## 请求 / 响应

```jsonc
// POST /api/v1/chat
{
  "user_id": "123",
  "session_id": "abc123",
  "message": "帮我分析这个AI Agent岗位"
}

// 响应
{
  "session_id": "abc123",
  "intent": "JD_ANALYSIS",
  "message": "📋 AI Agent（AI Agent应用开发）分析：...",
  "metadata": {
    "intent_confidence": 0.9,
    "intent_method": "rule",
    "agent_result": {},
    "missing_context": []
  }
}
```

## 编排方式

- **简单/明确任务**：`IntentRouter` 直接路由到 Use Case Agent 单步执行。
- **复杂任务**：LangGraph StateGraph 编排（`app/agents/graph.py`）：perception → context → planner → executor → decision（循环）→ finalize。
- **Web Search**（`app/tools/web_search.py`）：Bing 默认免 key，Agent（如 UC01 行业信息）优先用实时搜索结果，失败自动降级。

## 关键设计约定

- 所有大模型调用统一经 `app/llm/client.py::LLMClient`，各模块不自建 client。
- 日志统一 `app/core/logging.py::get_logger`（命名空间 `agent.*`），异常统一分层（`app/core/exceptions.py`）。
- 技能匹配等核心逻辑为纯规则实现，可离线运行与评测。
- 依赖 DayLoop 的能力在数据源不可用时优雅降级，不抛错。

## 相关文档

- `docs/02-use-cases.md` —— 各 Use Case 需求与验收标准
- `docs/04-agent-flow.md` —— Agent Flow 设计
- `docs/05-architecture.md` —— 系统架构与模块设计
- `docs/06-evaluation.md` —— 评测体系
- `src/app/{perception,context,agents}/README.md` —— 各层模块说明
