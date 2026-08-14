> **DayLoop（.NET / Node.js）是业务宿主系统，AI Agent 是独立的 Python/FastAPI 服务，通过 API 与 DayLoop 双向通信。**

# 05-architecture.md

# Architecture

## 1. 架构概述

DayLoop AI 是一个将 AI Agent 嵌入 DayLoop 的个人成长与求职智能工作台。

DayLoop负责：

- 用户界面
- 每日任务
- 用户资料
- 简历
- 技能画像
- 学习记录
- 招聘信息
- 面试记录
- 知识库管理
- 数据持久化

AI Agent负责：

- 感知用户与环境状态
- 理解用户意图
- 构建任务上下文
- 任务规划
- Tool Calling
- RAG检索
- Memory管理
- 任务执行
- 决策
- Reflection
- 动态模拟面试
- 根据执行结果更新DayLoop

核心架构原则：

> DayLoop负责“环境与业务”，AI Agent负责“智能”。

AI Agent作为独立服务运行，不直接访问DayLoop数据库，而是通过API调用DayLoop提供的业务能力。


# 2. 整体架构


                         ┌──────────────────────┐
                         │      DayLoop UI      │
                         │                      │
                         │ Dashboard            │
                         │ Daily Loop           │
                         │ Profile              │
                         │ Jobs                 │
                         │ Knowledge            │
                         │ AI Chat              │
                         └──────────┬───────────┘
                                    │
                                    │ HTTP
                                    ▼
                         ┌──────────────────────┐
                         │   DayLoop Backend    │
                         │                      │
                         │ .NET / Node.js       │
                         │                      │
                         │ Business Services    │
                         │ Data Access          │
                         └──────────┬───────────┘
                                    │
                           Agent Integration API
                                    │
                           HTTP / REST / SSE
                                    │
                                    ▼
                    ┌──────────────────────────────┐
                    │      AI Agent Service        │
                    │                              │
                    │ Python + FastAPI             │
                    ├──────────────────────────────┤
                    │       Agent Gateway          │
                    ├──────────────────────────────┤
                    │       Agent Runtime          │
                    │                              │
                    │ Perception                   │
                    │ Intent Router                │
                    │ Context Manager              │
                    │ Planner                      │
                    │ Executor                     │
                    │ Decision                     │
                    │ Reflection                   │
                    ├──────────────────────────────┤
                    │ Tools                        │
                    │ RAG                          │
                    │ Memory                       │
                    └──────────────┬───────────────┘
                                   │
                 ┌─────────────────┼──────────────────┐
                 │                 │                  │
                 ▼                 ▼                  ▼
              LLM API          Vector DB        External APIs
                                                  │
                                         ┌────────┼────────┐
                                         │        │        │
                                       Web     GitHub    Jobs

# 3. 系统边界

系统由两个核心系统组成：

## 3.1 DayLoop

DayLoop是主业务系统。

负责：

* 用户管理
* 数据管理
* UI
* 业务逻辑
* 任务管理
* 求职信息
* 学习记录
* 面试记录

DayLoop可能存在：

```text
.NET Backend
Node.js Backend
```

两套Backend可以继续独立存在。

AI Agent不关心具体使用哪一种Backend。

---

## 3.2 AI Agent Service

AI Agent是独立的智能服务。

技术栈：

```text
Python
FastAPI
LangGraph
LLM
RAG
Memory
Tools
```

负责：

* Agent Runtime
* Agent Workflow
* Context Engineering
* Tool Calling
* RAG
* Memory
* Evaluation

---

# 4. 核心设计原则

## 4.1 DayLoop与Agent解耦

Agent不直接依赖DayLoop内部代码。

Agent不直接访问DayLoop数据库。

双方通过API Contract通信。

```text
DayLoop
   │
   │ API
   ▼
AI Agent
```

这样可以避免：

```text
Agent
  ↓
直接访问
  ↓
DayLoop Database
```

导致强耦合。

---

## 4.2 Agent与LLM解耦

业务代码不直接绑定某个LLM。

统一使用：

```text
LLMProvider
```

例如：

```text
LLMProvider
├── DeepSeek
├── OpenAI
└── Other Models
```

Agent只依赖统一接口。

---

## 4.3 Agent与Tool解耦

Agent不关心Tool具体实现。

Tool通过统一Schema注册：

```text
Tool
├── name
├── description
├── input_schema
└── execute()
```

Agent通过Tool Registry发现和调用工具。

---

## 4.4 Memory与Conversation解耦

Conversation记录的是聊天上下文。

Memory记录的是值得长期保留的信息。

两者不能简单等同。

---

## 4.5 RAG与Agent解耦

RAG负责：

> 找到与当前任务相关的知识。

Agent负责：

> 决定什么时候需要知识以及如何使用知识。

---

# 5. 分层架构

系统整体分为：

```text
Presentation Layer
        ↓
DayLoop Application Layer
        ↓
API Integration Layer
        ↓
Agent Gateway
        ↓
Agent Runtime
        ↓
Capability Layer
        ↓
Data / External Services
```

---

# 6. Presentation Layer

由DayLoop UI负责。

主要页面：

```text
DayLoop
│
├── Dashboard
├── Daily Loop
├── Profile
├── Resume
├── Skills
├── Jobs
├── Knowledge
├── Interview
└── AI Chat
```

---

## 6.1 Dashboard

展示：

* 今日任务
* AI行业动态
* 招聘机会
* Skill Gap
* 面试任务
* Agent建议

---

## 6.2 AI Chat

AI Chat是用户与Agent交互的主要入口。

用户可以：

```text
分析JD
开始模拟面试
更新简历
更新技能
查询行业信息
查询招聘信息
查询学习记录
创建任务
```

Chat是Agent的重要入口，但：

> Chat不是Agent唯一的输出方式。

Agent还可以直接更新DayLoop中的业务数据。

---

# 7. DayLoop Backend Layer

DayLoop Backend负责业务系统。

可以继续使用：

```text
.NET
Node.js
```

不要求Agent了解内部实现。

例如：

```text
User Service
Resume Service
Skill Service
Job Service
Task Service
Learning Service
Interview Service
Knowledge Service
```

---

# 8. Agent Integration API

这是DayLoop与AI Agent之间的核心通信层。

原则：

> Agent通过统一API访问DayLoop能力，而不是直接访问数据库。

---

## 8.1 API设计目标

需要满足：

* 统一
* 稳定
* 可版本化
* 与具体Backend实现解耦
* 支持.NET
* 支持Node.js
* 支持未来其他客户端
* 支持Agent调用

API版本：

```text
/api/v1
```

---

# 9. DayLoop提供给Agent的API

可以划分为：

```text
Agent Integration API
│
├── Profile
├── Resume
├── Skills
├── Tasks
├── Learning
├── Jobs
├── Interview
├── Knowledge
└── Memory
```

---

## 9.1 Profile API

```http
GET /api/v1/agent/profile
```

获取：

* 用户基本信息
* 求职目标
* 求职偏好
* 指定城市
* 目标岗位

---

## 9.2 Resume API

```http
GET /api/v1/agent/resume
PUT /api/v1/agent/resume
```

用于：

* 获取简历
* 更新简历

---

## 9.3 Skills API

```http
GET /api/v1/agent/skills
POST /api/v1/agent/skills
PUT /api/v1/agent/skills/{id}
```

用于：

* 获取技能画像
* 更新技能
* 更新技能等级
* 增加新技能

---

## 9.4 Task API

```http
GET /api/v1/agent/tasks
POST /api/v1/agent/tasks
PUT /api/v1/agent/tasks/{id}
```

Agent可以：

* 查询今日任务
* 创建任务
* 修改任务
* 更新任务状态

---

## 9.5 Learning API

```http
GET /api/v1/agent/learning/history
```

获取：

* 最近学习记录
* 学习主题
* 学习时间
* 学习进度

---

## 9.6 Job API

```http
GET /api/v1/agent/jobs
POST /api/v1/agent/jobs
GET /api/v1/agent/jobs/{id}
```

用于：

* 查询招聘信息
* 保存招聘信息
* 查询岗位详情

---

## 9.7 Interview API

```http
POST /api/v1/agent/interviews
GET /api/v1/agent/interviews/{id}
POST /api/v1/agent/interviews/{id}/answer
POST /api/v1/agent/interviews/{id}/finish
```

用于模拟面试。

---

## 9.8 Knowledge API

```http
GET /api/v1/agent/knowledge
POST /api/v1/agent/knowledge
```

用于：

* 查询知识
* 写入知识
* 获取知识库信息

---

# 10. Agent Service API

AI Agent本身也提供API给DayLoop调用。

```text
Agent API
│
├── Health
├── Chat
├── Chat Stream
├── Tasks
├── JD Analysis
└── Interviews
```

---

## 10.1 Health

```http
GET /api/v1/health
```

返回：

```json
{
  "status": "ok",
  "service": "ai-agent",
  "version": "1.0.0"
}
```

---

# 11. Chat API

统一Agent入口：

```http
POST /api/v1/chat
```

请求：

```json
{
  "user_id": "123",
  "session_id": "abc123",
  "message": "帮我分析这个AI Agent岗位"
}
```

返回：

```json
{
  "session_id": "abc123",
  "intent": "JD_ANALYSIS",
  "message": "该岗位与你的匹配度约为82%，建议投递。",
  "metadata": {
    "task_id": "task_001"
  }
}
```

---

# 12. Streaming Chat API

Agent任务可能包含多个步骤。

例如：

```text
Planner
 ↓
RAG
 ↓
Tool
 ↓
Decision
 ↓
LLM
```

因此支持SSE：

```http
POST /api/v1/chat/stream
```

前端可以实时显示：

```text
Agent正在分析...

✓ 已读取简历
✓ 已读取目标JD
✓ 已获取技能画像
✓ 正在进行技能匹配
✓ 正在查询相关技术要求
✓ 分析完成
```

---

# 13. API通信关系

系统存在两种方向的API调用。

## 13.1 DayLoop → Agent

用户发起请求：

```text
User
 ↓
DayLoop UI
 ↓
DayLoop Backend
 ↓
Agent API
```

例如：

```text
POST /api/v1/chat
```

---

## 13.2 Agent → DayLoop

Agent需要获取或修改业务数据：

```text
Agent
 ↓
DayLoop Agent API
 ↓
DayLoop Backend
 ↓
Database
```

例如：

```text
GET /api/v1/agent/profile
GET /api/v1/agent/skills
POST /api/v1/agent/tasks
```

---

# 14. 双向API架构

```text
                    DayLoop
                       │
                       │ User Request
                       ▼
               ┌───────────────┐
               │ Agent Service │
               └───────┬───────┘
                       │
                       │ Query / Action
                       ▼
                    DayLoop
                       │
                       ▼
                    Database
                       │
                       │ Data
                       ▼
               ┌───────────────┐
               │ Agent Service │
               └───────┬───────┘
                       │
                       ▼
                    Result
                       │
                       ▼
                    DayLoop
```

---

# 15. Agent Gateway

Agent API进入Agent Runtime之前增加Gateway。

```text
HTTP Request
     ↓
Agent Gateway
     ↓
Authentication
     ↓
Authorization
     ↓
Rate Limit
     ↓
Request Validation
     ↓
Session Management
     ↓
Agent Runtime
```

Agent Gateway不负责Agent推理。

---

# 16. Agent Runtime

Agent Runtime是系统智能核心。

```text
Agent Runtime
│
├── Perception
├── Intent Router
├── Context Manager
├── Planner
├── Executor
├── Decision
├── Reflection
├── Memory Manager
└── State Manager
```

---

# 17. Perception

负责理解当前环境。

输入：

```text
User Input
DayLoop Data
External Data
Conversation
```

输出：

```text
Intent
Entity
Task
Context Requirement
```

例如：

```text
用户：
帮我分析这个AI Agent岗位。

↓

Intent:
JD_ANALYSIS

Need:
JD
Resume
Skill Profile
Job Preference
```

---

# 18. Intent Router

负责将用户请求路由到具体Use Case。

```text
Intent Router
│
├── INDUSTRY_INFO
├── JOB_SEARCH
├── JD_ANALYSIS
├── SKILL_GAP
├── INTERVIEW_KNOWLEDGE
├── MOCK_INTERVIEW
├── RESUME_UPDATE
├── TASK_MANAGEMENT
└── GENERAL_CHAT
```

采用：

```text
Rule + LLM
```

混合模式。

---

# 19. Context Manager

根据当前任务动态构建Context。

Context可能包含：

```text
Current Input
Conversation History
User Profile
Resume
Skill Profile
Long-term Memory
Retrieved Knowledge
Current Task
Plan
Tool Results
```

原则：

> 不将所有用户数据全部发送给LLM，而是根据当前任务选择必要的信息。

---

# 20. Agent State

Agent采用统一State管理任务执行。

```text
AgentState
│
├── session_id
├── user_id
├── intent
├── user_input
├── conversation_history
├── user_profile
├── resume
├── skill_profile
├── retrieved_memory
├── retrieved_knowledge
├── task
├── plan
├── current_step
├── tool_calls
├── observations
├── decision
├── reflection
├── final_answer
└── evaluation
```

复杂Workflow使用：

> LangGraph State Graph

---

# 21. Planner

Planner负责将复杂任务拆解成可执行步骤。

例如：

```text
任务：
分析JD

Plan:
1. 解析JD
2. 获取用户简历
3. 获取技能画像
4. 匹配技能
5. 分析Gap
6. 给出投递建议
```

---

# 22. Executor

Executor负责执行当前Step。

可以调用：

```text
Tool
RAG
Memory
DayLoop API
LLM
```

---

# 23. Decision

Decision负责判断：

```text
当前Step是否完成？
是否需要Tool？
是否需要Retry？
是否需要Re-plan？
是否可以结束？
```

---

# 24. Reflection

Reflection负责：

* 检查结果
* 发现错误
* 发现遗漏
* 判断输出质量
* 决定是否需要重新执行

---

# 25. Tool System

Agent通过Tool访问能力。

```text
Tools
│
├── Web Search
├── Web Reader
├── GitHub Search
├── Job Search
├── JD Parser
├── Resume Parser
├── Knowledge Search
├── Memory Search
└── DayLoop Tools
```

---

# 26. DayLoop Tools

DayLoop Tools是本系统的重要组成部分。

```text
DayLoop Tools
│
├── get_user_profile()
├── get_resume()
├── get_skills()
├── update_skill()
├── get_tasks()
├── create_task()
├── update_task()
├── get_learning_history()
├── get_job_preferences()
├── save_interview()
└── update_resume()
```

内部通过HTTP调用DayLoop API。

例如：

```text
Agent
 ↓
get_user_skills()
 ↓
HTTP
 ↓
DayLoop Agent API
 ↓
.NET / Node.js
 ↓
Database
```

---

# 27. Memory Architecture

Memory分为：

```text
Memory
│
├── Short-term Memory
├── Working Memory
└── Long-term Memory
```

---

## Short-term Memory

当前Conversation。

---

## Working Memory

当前任务执行状态。

例如：

```text
Task:
JD Analysis

Current Step:
Skill Matching

Observation:
JD要求RAG Evaluation

Next:
读取用户Skill Profile
```

---

## Long-term Memory

保存长期有价值的信息。

例如：

```text
Target Position:
AI Agent Application Developer

Skill:
Python
RAG
LangGraph

Skill Gap:
RAG Evaluation

Preference:
互联网 / AI
```

---

# 28. RAG Architecture

```text
Document
   ↓
Loader
   ↓
Cleaner
   ↓
Chunker
   ↓
Embedding
   ↓
Vector DB
   ↓
Retriever
   ↓
Reranker
   ↓
Context
   ↓
LLM
```

---

# 29. Knowledge Base

知识库划分：

```text
Knowledge Base
│
├── Interview Knowledge
├── AI Technical Knowledge
├── Industry Knowledge
├── Job Knowledge
└── Learning Notes
```

面试知识：

```json
{
  "question": "...",
  "answer": "...",
  "category": "RAG",
  "position": "AI Agent",
  "difficulty": "medium",
  "source": "...",
  "created_at": "..."
}
```

---

# 30. Interview Agent

模拟面试是系统的核心Agent场景之一。

```text
User
 ↓
Start Interview
 ↓
Load User Profile
 ↓
Load Resume
 ↓
Load Target JD
 ↓
Load Skill Profile
 ↓
Retrieve Interview Knowledge
 ↓
Interview Planner
 ↓
Generate Question
 ↓
User Answer
 ↓
Answer Evaluation
 ↓
Follow-up Question
 ↓
User Answer
 ↓
...
 ↓
Interview Summary
 ↓
Skill Gap Update
 ↓
Memory Update
```

---

# 31. Interview API

```http
POST /api/v1/interviews
```

创建面试。

请求：

```json
{
  "user_id": "123",
  "job_id": "job_001",
  "mode": "agent"
}
```

返回：

```json
{
  "interview_id": "interview_001",
  "status": "started"
}
```

提交答案：

```http
POST /api/v1/interviews/{id}/answer
```

请求：

```json
{
  "answer": "我认为RAG主要解决..."
}
```

返回：

```json
{
  "evaluation": {
    "score": 78,
    "strengths": [],
    "weaknesses": []
  },
  "next_question": "如果Retriever返回了大量无关结果，你会如何解决？"
}
```

---

# 32. JD Analysis Architecture

```text
User
 ↓
Paste JD
 ↓
DayLoop
 ↓
Agent API
 ↓
JD Analysis Agent
 ↓
Parse JD
 ↓
Get Resume
 ↓
Get Skills
 ↓
Get Preferences
 ↓
Skill Matching
 ↓
Gap Analysis
 ↓
Recommendation
 ↓
DayLoop
```

结果：

```json
{
  "match_score": 82,
  "strengths": [],
  "gaps": [],
  "missing_skills": [],
  "recommendation": "投递"
}
```

---

# 33. AI Industry Information Architecture

```text
Scheduler
 ↓
Agent Task
 ↓
Search Web / GitHub
 ↓
Collect Information
 ↓
Deduplicate
 ↓
Summarize
 ↓
Classify
 ↓
Evaluate Relevance
 ↓
Save to DayLoop
 ↓
Dashboard
```

关注范围：

```text
ByteDance
Alibaba
Tencent
Anthropic
GitHub
```

---

# 34. Recruitment Architecture

```text
Scheduler
 ↓
Collect Jobs
 ↓
Filter Target Positions
 ↓
Load User Preferences
 ↓
JD Analysis
 ↓
Skill Matching
 ↓
Ranking
 ↓
Save Jobs
 ↓
DayLoop Dashboard
```

目标岗位：

```text
AI Agent Application Developer
AI Product Manager
FDE
```

---

# 35. Scheduler

自动任务由Scheduler触发。

例如：

```text
08:00
 ↓
AI Industry Collection

08:10
 ↓
Job Collection

08:20
 ↓
Interview Knowledge Update
```

流程：

```text
Scheduler
 ↓
Create Agent Task
 ↓
Agent Runtime
 ↓
Execute
 ↓
Save Result
 ↓
DayLoop Dashboard
```

MVP：

```text
APScheduler / Cron
```

后续再根据规模考虑：

```text
Message Queue
Task Queue
Distributed Scheduler
```

---

# 36. Agent Task与User Task

DayLoop中的Task分为：

## User Task

用户需要完成：

```text
学习RAG Evaluation
完善简历
准备面试
```

## Agent Task

Agent自动执行：

```text
收集AI行业信息
收集招聘信息
整理面试知识
分析JD
```

二者可以形成闭环：

```text
Agent Task
 ↓
发现Skill Gap
 ↓
Create User Task
 ↓
DayLoop
 ↓
User完成学习
 ↓
Skill Profile更新
 ↓
Agent重新评估
```

---

# 37. Data Architecture

数据分为：

```text
Data
│
├── Relational Data
├── Vector Data
├── File Data
├── Session Data
└── Agent Logs
```

---

# 38. Relational Database

保存：

```text
Users
Profiles
Resumes
Skills
Jobs
JobRequirements
Tasks
LearningRecords
Interviews
InterviewAnswers
InterviewReports
Conversations
AgentTasks
```

MVP：

```text
SQLite
```

后续：

```text
PostgreSQL
```

---

# 39. Vector Database

用于：

* Interview Knowledge
* AI Technical Knowledge
* Learning Notes
* Industry Knowledge
* Long-term Memory

MVP：

```text
ChromaDB
```

后续：

```text
PostgreSQL + pgvector
```

---

# 40. File Storage

用于：

* PDF简历
* JD文件
* Markdown
* 学习资料
* 用户上传文件

MVP：

```text
Local Storage
```

生产环境：

```text
Object Storage
```

---

# 41. External Services

```text
External Services
│
├── LLM Provider
├── Embedding Provider
├── Web Search
├── GitHub API
├── Job APIs
└── Content Sources
```

通过Adapter统一管理。

---

# 42. LLM Gateway

```text
Agent
 ↓
LLMProvider
 ↓
┌──────────┬──────────┐
│          │          │
DeepSeek  OpenAI   Other LLM
```

避免Agent业务逻辑直接依赖具体模型。

---

# 43. Authentication

DayLoop和Agent之间需要服务间认证。

推荐：

```text
DayLoop
 ↓
Service Token
 ↓
Agent
```

Agent调用DayLoop：

```text
Authorization: Bearer <service-token>
```

用户身份通过：

```text
user_id
```

或者JWT Claims传递。

原则：

> Agent不能通过客户端传入任意user_id访问其他用户数据。

用户身份必须由可信的DayLoop Backend传递和验证。

---

# 44. Authorization

Agent Tools分为：

## Read Tools

可以自动执行：

```text
读取Profile
读取Resume
读取Skills
读取Tasks
搜索Knowledge
```

## Write Tools

需要更严格控制：

```text
修改Resume
修改Skills
创建Task
修改Job
保存Interview
```

---

# 45. 高风险操作

对于敏感写操作，可以采用：

```text
Agent
 ↓
Propose Action
 ↓
User Confirmation
 ↓
Execute
```

例如：

> 我准备将你的技能“RAG Evaluation”更新为“Project Experience”，是否确认？

用户确认后：

```text
update_skill()
```

---

# 46. Observability

Agent执行过程需要记录：

```text
Request
 ↓
Intent
 ↓
Context
 ↓
Plan
 ↓
Tool Calls
 ↓
Tool Results
 ↓
Decision
 ↓
Reflection
 ↓
Final Answer
```

记录指标：

```text
Token Usage
Response Time
Tool Call Count
Tool Error Count
Agent Loop Count
RAG Retrieval
LLM Cost
Evaluation Score
```

用于：

* Debug
* Evaluation
* 性能优化
* 成本控制
* Agent质量分析

---

# 47. Logging

日志分为：

```text
Application Log
Agent Log
Tool Log
LLM Log
RAG Log
API Log
Error Log
```

Agent日志示例：

```text
[INFO] intent=JD_ANALYSIS
[INFO] planner steps=5
[INFO] tool=get_resume
[INFO] tool=get_skills
[INFO] tool=knowledge_search
[INFO] decision=continue
[INFO] decision=finish
```

---

# 48. Error Handling

Agent可能出现：

```text
LLM Error
Tool Error
API Error
RAG Error
Timeout
Invalid Output
Agent Loop
```

需要：

```text
Retry
Fallback
Timeout
Max Iteration
Error Recovery
```

例如：

```text
Tool失败
 ↓
Retry
 ↓
仍然失败
 ↓
Alternative Tool
 ↓
仍然失败
 ↓
Return Partial Result
```

---

# 49. Agent Loop限制

必须设置：

```text
MAX_ITERATIONS
MAX_TOOL_CALLS
TIMEOUT
```

防止：

```text
Agent
 ↓
Decision
 ↓
Tool
 ↓
Decision
 ↓
Tool
 ↓
无限循环
```

---

# 50. MVP技术选型

第一阶段采用：

| 模块               | 技术                          |
| ---------------- | --------------------------- |
| DayLoop Frontend | 现有DayLoop                   |
| DayLoop Backend  | .NET / Node.js              |
| Agent Backend    | Python                      |
| Agent API        | FastAPI                     |
| Agent Workflow   | LangGraph                   |
| LLM              | DeepSeek / OpenAI           |
| Embedding        | Sentence Transformers / API |
| Vector DB        | ChromaDB                    |
| Database         | SQLite                      |
| Scheduler        | APScheduler                 |
| Web Search       | Search API                  |
| GitHub           | GitHub API                  |
| File Storage     | Local                       |
| Evaluation       | Python                      |

---

# 51. MVP部署架构

MVP阶段采用模块化单体 + 独立Agent Service。

```text
┌───────────────────────────────────────┐
│              DayLoop                  │
│                                       │
│ UI + .NET / Node.js Backend           │
│                                       │
│ SQLite / Existing DB                 │
└──────────────────┬────────────────────┘
                   │
                   │ HTTP / SSE
                   ▼
┌───────────────────────────────────────┐
│           AI Agent Service            │
│                                       │
│ FastAPI                               │
│ LangGraph                             │
│ Tools                                 │
│ RAG                                   │
│ Memory                                │
└──────────────────┬────────────────────┘
                   │
        ┌──────────┼───────────┐
        ▼          ▼           ▼
      LLM       Chroma       External
                Vector DB      APIs
```

---

# 52. 为什么Agent独立部署

DayLoop存在：

```text
.NET
Node.js
```

如果Agent直接嵌入某一个Backend：

```text
.NET
 └── Agent
```

或者：

```text
Node.js
 └── Agent
```

都会导致Agent与Backend强耦合。

独立Agent Service：

```text
.NET ─────┐
          ├──→ Agent
Node ─────┘
```

可以：

* 独立开发
* 独立部署
* 独立扩展
* 独立升级LLM
* 独立进行Agent Evaluation
* 被多个客户端复用

---

# 53. Agent Service内部目录建议

```text
agent-service/
│
├── app/
│   ├── api/
│   │   ├── routes/
│   │   │   ├── chat.py
│   │   │   ├── interview.py
│   │   │   ├── task.py
│   │   │   └── health.py
│   │   │
│   │   └── schemas/
│   │
│   ├── agent/
│   │   ├── runtime.py
│   │   ├── state.py
│   │   ├── planner.py
│   │   ├── executor.py
│   │   ├── decision.py
│   │   ├── reflection.py
│   │   └── router.py
│   │
│   ├── context/
│   │   ├── manager.py
│   │   └── builder.py
│   │
│   ├── memory/
│   │   ├── short_memory.py
│   │   └── long_memory.py
│   │
│   ├── rag/
│   │   ├── loader.py
│   │   ├── chunker.py
│   │   ├── retriever.py
│   │   └── reranker.py
│   │
│   ├── tools/
│   │   ├── registry.py
│   │   ├── search.py
│   │   ├── github.py
│   │   ├── knowledge.py
│   │   └── dayloop/
│   │       ├── profile.py
│   │       ├── resume.py
│   │       ├── skills.py
│   │       └── tasks.py
│   │
│   ├── llm/
│   │   ├── provider.py
│   │   └── deepseek.py
│   │
│   ├── evaluation/
│   │   ├── test_cases.py
│   │   ├── metrics.py
│   │   └── error_analysis.py
│   │
│   └── config/
│
├── tests/
├── logs/
├── requirements.txt
└── main.py
```

---

# 54. API Contract示例

Agent调用DayLoop：

```http
GET /api/v1/agent/profile
Authorization: Bearer <service-token>
X-User-Id: 123
```

返回：

```json
{
  "user_id": "123",
  "target_positions": [
    "AI Agent Application Developer",
    "AI Product Manager",
    "FDE"
  ],
  "target_cities": [
    "..."
  ]
}
```

Agent调用Skills：

```http
GET /api/v1/agent/skills
Authorization: Bearer <service-token>
X-User-Id: 123
```

返回：

```json
{
  "skills": [
    {
      "name": "Python",
      "level": "project"
    },
    {
      "name": "RAG",
      "level": "project"
    },
    {
      "name": "LangGraph",
      "level": "project"
    }
  ]
}
```

---

# 55. API Contract设计原则

API必须做到：

### Stable

Agent不应该依赖DayLoop内部实现。

### Versioned

```text
/api/v1
/api/v2
```

### Typed

使用：

```text
Pydantic
OpenAPI
JSON Schema
```

定义请求和响应结构。

### Idempotent

对于需要重复调用的API尽可能支持幂等。

### Observable

记录：

```text
request_id
user_id
session_id
agent_task_id
```

方便追踪。

---

# 56. Request Trace

一次Agent任务应该可以通过统一ID追踪：

```text
request_id
      │
      ├── DayLoop API
      │
      ├── Agent API
      │
      ├── Agent Runtime
      │
      ├── Tool Calls
      │
      ├── RAG
      │
      └── LLM
```

例如：

```text
request_id = req_001
session_id = session_001
task_id = task_001
```

这样出现问题时可以完整还原Agent执行过程。

---

# 57. Agent与DayLoop的数据闭环

最终系统形成：

```text
                 External World
                       │
          ┌────────────┼────────────┐
          ▼            ▼            ▼
        News          Jobs       Knowledge
          │            │            │
          └────────────┼────────────┘
                       ▼
                     Agent
                       │
            ┌──────────┼──────────┐
            ▼          ▼          ▼
         Analysis     Gap    Recommendation
            │          │          │
            └──────────┼──────────┘
                       ▼
                    DayLoop
                       │
            ┌──────────┼──────────┐
            ▼          ▼          ▼
          Tasks      Skills    Knowledge
            │          │          │
            └──────────┼──────────┘
                       ▼
                     Agent
```

形成：

> 感知 → 分析 → 决策 → 行动 → 反馈 → 再感知

---

# 58. UC与Architecture映射

| Use Case       | Agent能力            | DayLoop API                           | Agent Tools      | RAG          |
| -------------- | ------------------ | ------------------------------------- | ---------------- | ------------ |
| UC01 AI行业信息    | Search / Summarize | 保存行业信息                                | Web / GitHub     | Industry KB  |
| UC02 AI招聘      | Search / Ranking   | Jobs                                  | Job Search       | Job KB       |
| UC03 JD分析      | Reasoning          | Profile / Resume / Skills             | JD Parser        | AI KB        |
| UC04 Skill Gap | Matching           | Resume / Skills                       | Resume Parser    | Job KB       |
| UC05 面试题整理     | Extraction         | Knowledge                             | Note Reader      | Interview KB |
| UC06 模拟面试      | Dynamic Agent Loop | Profile / Resume / Skills / Interview | Knowledge Search | Interview KB |

---

# 59. 架构演进路线

## Phase 1：Agent MVP

```text
DayLoop
+
FastAPI Agent
+
LLM
+
Tools
+
RAG
+
Memory
```

---

## Phase 2：Agent Workflow

```text
LangGraph
+
Planner
+
Executor
+
Decision
+
Reflection
+
State
```

---

## Phase 3：DayLoop深度整合

```text
Agent
 ↓
Profile
 ↓
Resume
 ↓
Skills
 ↓
Tasks
 ↓
Learning
 ↓
Interview
```

---

## Phase 4：主动Agent

```text
Scheduler
 ↓
Agent
 ↓
感知环境
 ↓
发现问题
 ↓
生成建议
 ↓
创建Task
 ↓
DayLoop
```

---

## Phase 5：Multi-Agent

只有当单Agent复杂度明显增加时再引入：

```text
Supervisor
│
├── Research Agent
├── Job Agent
├── JD Agent
├── Interview Agent
└── Evaluation Agent
```

MVP阶段不强制使用Multi-Agent。

---

# 60. 最终架构

```text
┌───────────────────────────────────────────────────────────┐
│                         DayLoop                           │
│                                                           │
│ Dashboard │ Daily Loop │ Profile │ Jobs │ Knowledge │ Chat│
└───────────────────────────┬───────────────────────────────┘
                            │
                            ▼
┌───────────────────────────────────────────────────────────┐
│                    DayLoop Backend                        │
│                                                           │
│                 .NET / Node.js                            │
│                                                           │
│ User │ Resume │ Skills │ Jobs │ Tasks │ Learning │ Interview│
└───────────────────────────┬───────────────────────────────┘
                            │
                     Agent Integration API
                            │
                       HTTP / REST
                       HTTP / SSE
                            │
                            ▼
┌───────────────────────────────────────────────────────────┐
│                    AI Agent Service                       │
│                                                           │
│                         FastAPI                           │
│                                                           │
│ ┌───────────────────────────────────────────────────────┐ │
│ │                  Agent Runtime                        │ │
│ │                                                       │ │
│ │ Perception → Intent → Context → Planner → Executor    │ │
│ │                                      ↓                │ │
│ │                               Decision → Reflection   │ │
│ └───────────────────────────────────────────────────────┘ │
│                                                           │
│ ┌─────────────┐ ┌─────────────┐ ┌──────────────────────┐ │
│ │   Memory    │ │     RAG     │ │        Tools         │ │
│ │             │ │             │ │                      │ │
│ │ Short-term  │ │ Retriever   │ │ Web                  │ │
│ │ Working     │ │ Reranker    │ │ GitHub               │ │
│ │ Long-term   │ │ Vector DB   │ │ Job Search           │ │
│ └─────────────┘ └─────────────┘ │ JD Parser            │ │
│                                 │ DayLoop API           │ │
│                                 └──────────────────────┘ │
└───────────────────────────┬───────────────────────────────┘
                            │
              ┌─────────────┼─────────────┐
              ▼             ▼             ▼
           LLM API       Vector DB    External APIs
```

---
