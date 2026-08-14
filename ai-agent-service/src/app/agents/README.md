# Agents 层

`app/agents` 是 AI Agent 的决策与执行层，对应 `docs/05-architecture.md` §18（Intent Router）/ §21-§24（Planner / Executor / Decision / Reflection）。

## 职责

根据感知层识别出的意图，调用对应的 Use Case Agent 完成任务（调用 Tools / RAG / Memory / LLM 生成答案）：

```
PerceptionResult (intent / entities / context_requirements)
        │
        ▼
   [IntentRouter]  意图 → 处理器 映射（未注册回落 GENERAL_CHAT）
        │
        ▼
   [Use Case Agent]  JD分析 / 技能Gap / 模拟面试 / 招聘 / 行业信息 / ...
        │
        ▼
   [Tools / RAG / Memory / LLM]  执行与推理
        │
        ▼
   AgentState.final_answer → API 响应
```

## 目录结构

| 文件 | 说明 |
| --- | --- |
| `bootstrap.py` | `build_router()` 组装 IntentRouter 并注册全部 Agent |
| `router.py` | `IntentRouter`：register / route / handle |
| `registry.py` | Agent 全局注册表（类或实例） |
| `base.py` | `BaseAgent` 抽象基类 |
| `state.py`（`app/state.py`） | `AgentState`：贯穿全流程的统一状态 + `from_perception()` |
| `skill_match.py` | 可离线的技能匹配/差距分析核心 |
| `jd_analysis.py` | UC03 JD 分析 Agent |
| `skill_gap.py` | UC04 技能 Gap 分析 Agent |
| `interview.py` | UC06 模拟面试 Agent（多轮会话 + 报告） |
| `industry_info.py` | UC01 AI 行业信息收集 Agent |
| `job_search.py` | UC02 AI 招聘信息收集 Agent |
| `interview_knowledge.py` | UC05 面试知识库整理 Agent |
| `resume_update.py` | 简历更新 Agent |
| `task_management.py` | 任务管理 Agent |
| `general_chat.py` | 通用对话兜底 Agent |
| `graph.py` | LangGraph 编排图（perception→context→planner→executor→decision→finalize） |

## 意图 → Agent 映射

| 意图 | Agent | Use Case |
| --- | --- | --- |
| `INDUSTRY_INFO` | `IndustryInfoAgent` | UC01 行业日报 |
| `JOB_SEARCH` | `JobSearchAgent` | UC02 招聘匹配 |
| `JD_ANALYSIS` | `JDAnalysisAgent` | UC03 JD 分析 |
| `SKILL_GAP` | `SkillGapAgent` | UC04 技能差距 |
| `INTERVIEW_KNOWLEDGE` | `InterviewKnowledgeAgent` | UC05 面试知识 |
| `MOCK_INTERVIEW` | `MockInterviewAgent` | UC06 模拟面试 |
| `RESUME_UPDATE` | `ResumeUpdateAgent` | 简历更新 |
| `TASK_MANAGEMENT` | `TaskManagementAgent` | 任务管理 |
| `GENERAL_CHAT` | `GeneralChatAgent` | 通用对话（兜底） |

## 快速开始

```python
from app.agents.bootstrap import build_router
from app.state import AgentState, from_perception
from app.perception.perception import PerceptionService

router = build_router()

# 1) 感知
result = PerceptionService().perceive(message="帮我分析这个AI Agent岗位JD，要求熟悉RAG")

# 2) 构建 AgentState
state = AgentState(session_id="s1", user_id="u1")
from_perception(state, result)

# 3) 路由并执行
handler = router.route(state.intent)   # -> JDAnalysisAgent.run
handler(state)
print(state.final_answer)              # 最终答案写入 AgentState
```

## 新增一个 Use Case Agent

1. 在 `app/agents/` 下新建 `xxx.py`，实现 `run(state: AgentState) -> dict`，把最终答案写入 `state.final_answer`；
2. 在 `bootstrap.py` 中 `router.register(INTENT_XXX, XxxAgent().run)` 并 `register_agent(...)`；
3. 在 `app/agents/__init__.py` 导出新类；
4.（可选）在 `runtime/planner.py` 的 `_PLAN_TEMPLATES` 补充该意图的步骤模板。

## 编排两种模式

- **专用 Agent 直通**：`run_agent_flow()` 先查 IntentRouter，命中 Use Case Agent 直接执行（简单/明确任务）。
- **LangGraph 通用编排**：未注册意图走 `build_graph()`，经 Planner/Executor/Decision/Reflection 循环（`app/agents/graph.py`）。

## 依赖说明

- 所有 Agent 统一经 `LLMClient` 调用大模型，统一 `get_logger`/异常分层（`app/core`）。
- 技能匹配/差距分析核心（`skill_match.py`）为纯规则实现，可离线、可评测。
- 依赖 DayLoop 的 Agent（Job Search / Resume / Task）在数据源不可用时优雅降级，不抛错。
