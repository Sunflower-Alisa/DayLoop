# Agent Flow

## 1. 文档目的

本文档定义 Personal AI Job Agent 的整体 Agent 执行流程。

重点描述：

* 用户请求如何进入 Agent
* Agent 如何理解用户意图
* 如何获取用户上下文
* 如何选择知识库
* 如何规划任务
* 如何调用工具
* 如何进行结果判断
* 如何进行反思和重试
* 如何生成最终结果
* 如何更新用户记忆

本文档关注的是：

> **Agent 如何完成任务**

具体技术架构、数据库、服务部署等内容在 `05-architecture.md` 中定义。

---

# 2. 整体 Agent Flow

系统采用统一的 Agent Loop：

```text
                    DayLoop
                       │
       ┌───────────────┼────────────────┐
       │               │                │
    User Input      DayLoop Data     External World
       │               │                │
       │          ┌────┼────┐       ┌───┼────┐
       │          │    │    │       │   │    │
       │        Plan  Skill  KB    Web Jobs GitHub
       │          │    │    │       │   │    │
       └──────────┴────┴────┴───────┴───┴────┘
                       │
                       ↓
                  Perception
                       ↓
                 Intent Router
                       ↓
                Context Builder
                       ↓
                    Planner
                       ↓
                   Executor
                       ↓
              Tool / RAG / Memory
                       ↓
                  Observation
                       ↓
                   Decision
                       ↓
                  Reflection
                       ↓
                  Final Result
                       ↓
                  DayLoop UI
                       ↓
                 Memory Update
```

---

# 3. Agent核心循环

Agent核心循环：

```text
    Perception
        ↓
    Context
        ↓
    Plan
        ↓
    Execute
        ↓
    Observe
        ↓
    Decide
        ↓
    Reflect
        ↓
    Continue / Retry / Re-plan
        ↓
    Final
```

其中：

### Perception

负责理解当前环境和用户输入。

例如：

> “帮我看看这个岗位值不值得投。”

Agent需要识别：

* 用户意图：JD分析
* 当前任务：岗位匹配分析
* 输入：JD
* 是否需要个人信息：是
* 是否需要知识库：可能需要
* 是否需要外部搜索：视情况而定

---

# 4. Context Building

Agent不能只依赖当前用户消息。

需要构建完整 Context：

```text
    Current Query
        +
    Conversation History
        +
    User Profile
        +
    Long-term Memory
        +
    Relevant Knowledge
        +
    Current Task State
        ↓
    Context
```

## 4.1 User Profile

保存稳定的用户信息，例如：

* 工作经历
* 技术栈
* 目标岗位
* 求职城市
* 求职偏好
* 技能水平

## 4.2 Long-term Memory

保存长期有价值的信息，例如：

* 用户已经完成的项目
* 用户关注的技术方向
* 用户过去面试中的薄弱点
* 用户曾经遇到的问题
* 用户的求职偏好变化

## 4.3 Knowledge

根据当前任务检索相关知识。

例如：

### JD分析

主要检索：

* AI Agent知识
* 岗位能力要求

### 模拟面试

主要检索：

* 面试题
* 技术知识
* 历史面试记录

---

# 5. Planner

Planner负责：

> **决定完成当前任务需要哪些步骤。**

例如：

用户：

> 帮我分析这个AI Agent岗位。

Planner：

```text
Step 1：解析JD
Step 2：提取岗位要求
Step 3：获取用户技能画像
Step 4：进行技能匹配
Step 5：分析Skill Gap
Step 6：生成投递建议
```

Planner不负责具体执行。

---

# 6. Executor

Executor负责执行Planner制定的步骤。

例如：

```text
    Planner
    ↓
    Step 1：解析JD
    ↓
    Executor
    ↓
    JD Parser Tool
    ↓
    Observation
```

Executor可以调用：

* Web Search
* Web Reader
* GitHub Search
* JD Parser
* Knowledge Search
* Memory Search
* Calculator
* Other Agent

---

# 7. Decision

Decision负责判断：

> **当前步骤执行完以后，下一步应该做什么。**

可能结果：

```text
FINISH
CONTINUE
RETRY
REPLAN
```

例如：

```text
Step 1：JD解析
       ↓
解析成功？
   ├── 是 → CONTINUE
   └── 否 → RETRY
```

或者：

```text
技能匹配
   ↓
发现JD要求信息不足
   ↓
REPLAN
   ↓
调用Web Search补充信息
```

---

# 8. Reflection

Reflection负责检查：

> **当前执行结果是否可靠。**

检查内容：

* 信息是否完整
* 是否存在明显错误
* 是否满足任务要求
* 是否有遗漏
* 是否需要重新执行
* 是否需要补充信息

例如：

```text
JD分析
 ↓
Reflection
 ↓
发现：
“薪资信息没有获取”
 ↓
Retry / Tool
 ↓
重新获取
```

---

# 9. Memory Update

任务完成后，不是所有内容都进入Memory。

只有具有长期价值的信息才进入长期记忆。

例如模拟面试结束：

```text
用户回答
   ↓
评估
   ↓
发现：
RAG原理理解较好
Rerank理解较弱
   ↓
Memory Update
   ↓
更新用户技能画像
```

以后进行下一次模拟面试时：

```text
User Profile
      +
历史Skill Gap
      ↓
生成更有针对性的面试题
```

---

# 10. UC01 AI行业信息收集 Flow

## 目标

每天获取过去24小时 AI行业重要动态。

## Flow

```text
    Scheduler / User
        ↓
    确定时间范围
        ↓
    确定关注范围
        ↓
    Planner
        ↓
    搜索信息
        ↓
    Web Search / GitHub Search
        ↓
    获取原始内容
        ↓
    Web Reader
        ↓
    信息清洗
        ↓
    去重
        ↓
    相关性判断
        ↓
    重要性判断
        ↓
    LLM总结
        ↓
    生成行业日报
        ↓
    保存重要信息
        ↓
    输出
```

## Agent需要判断

### 信息是否相关？

例如：

> 一个普通互联网新闻

→ 不相关

> Anthropic发布新的Agent能力

→ 高相关

### 信息是否重要？

例如：

> 某公司发布普通AI功能更新

→ 低优先级

> 新模型发布

→ 高优先级

---

# 11. UC02 AI招聘信息收集 Flow

## Flow

```text
    Scheduler
    ↓
    读取用户求职画像
    ↓
    读取目标岗位
    ↓
    Planner
    ↓
    搜索招聘信息
    ↓
    获取JD
    ↓
    解析JD
    ↓
    过滤
    ↓
    岗位匹配
    ↓
    匹配度计算
    ↓
    Ranking
    ↓
    生成投递建议
    ↓
    输出每日招聘报告
```

## Context

需要使用：

```text
User Profile
+
Job Preferences
+
Target Positions
+
Skill Profile
+
Location Preference
```

## 输出

```text
岗位
 ↓
匹配度
 ↓
优势
 ↓
风险
 ↓
是否推荐投递
```

---

# 12. UC03 JD分析 Flow

这是第一阶段最重要的 Agent Flow 之一。

## Flow

```text
User
 ↓
输入JD
 ↓
Perception
 ↓
识别：
“JD分析”
 ↓
Context Building
 ↓
读取个人技能画像
 ↓
读取目标岗位信息
 ↓
Planner
 ↓
解析JD
 ↓
提取：
├── 基础信息
├── 岗位职责
├── 技能要求
├── 工作经验
├── 学历
└── 加分项
 ↓
Skill Matching
 ↓
Gap Analysis
 ↓
Reasoning
 ↓
Reflection
 ↓
生成：
├── 匹配度
├── 优势
├── 风险
├── Gap
└── 投递建议
 ↓
Final Answer
```

---

# 13. UC04 技能Gap分析 Flow

## Flow

```text
User
 ↓
选择目标岗位
 ↓
获取JD
 ↓
读取个人简历
 ↓
读取User Profile
 ↓
读取历史项目
 ↓
Skill Extraction
 ↓
建立个人技能画像
 ↓
JD Skill Extraction
 ↓
Skill Matching
 ↓
Gap Detection
 ↓
Gap Ranking
 ↓
Reasoning
 ↓
生成：
├── 已掌握
├── 部分掌握
├── 未掌握
└── 隐性Gap
 ↓
生成学习建议
 ↓
更新Skill Profile
 ↓
Final Answer
```

---

# 14. UC05 面试知识库整理 Flow

## Flow

```text
Scheduler
 ↓
获取过去24小时新增笔记
 ↓
内容过滤
 ↓
判断是否与面试相关
 ↓
相关内容
 ↓
内容解析
 ↓
提取：
├── 面试题
├── 知识点
├── 答案
└── 延伸问题
 ↓
分类
 ↓
去重
 ↓
质量检查
 ↓
Embedding
 ↓
写入Knowledge Base
 ↓
生成每日新增面试内容报告
```

## 知识库结构

```text
Interview Question
       │
       ├── Position
       ├── Category
       ├── Difficulty
       ├── Answer
       ├── Knowledge Point
       ├── Follow-up Question
       ├── Source
       └── Updated Time
```

---

# 15. UC06 模拟面试 Flow

这是整个系统中最复杂的 Agent Flow。

## 15.1 面试准备阶段

```text
User
 ↓
选择岗位
 ↓
获取JD
 ↓
读取User Profile
 ↓
读取Skill Gap
 ↓
读取Interview Knowledge
 ↓
必要时Web Search
 ↓
Interview Planner
 ↓
生成Interview Plan
```

Interview Plan包括：

* 面试阶段
* 知识领域
* 问题难度
* 重点考察能力
* 用户薄弱点
* 预计面试时长

---

# 16. 模拟面试循环

```text
Interview Plan
      ↓
Generate Question
      ↓
User Answer
      ↓
Answer Evaluation
      ↓
Reflection
      ↓
判断回答情况
      │
      ├── 回答优秀
      │       ↓
      │    提高难度
      │
      ├── 回答一般
      │       ↓
      │    继续追问
      │
      └── 回答错误
              ↓
           深挖知识盲区
              ↓
         纠正 / 解释
              ↓
         继续提问
```

---

# 17. 模拟面试动态追问

模拟面试不能采用：

```text
Question 1
Question 2
Question 3
Question 4
```

这种固定流程。

应该采用：

```text
Question
   ↓
Answer
   ↓
Analyze
   ↓
Identify Weakness
   ↓
Follow-up Question
   ↓
Answer
   ↓
Analyze
   ↓
Continue
```

例如：

```text
Q1
什么是RAG？

      ↓

用户回答

      ↓

Agent发现：
基本理解正确
但没有提到Rerank

      ↓

Q2
为什么需要Rerank？

      ↓

用户回答

      ↓

Agent发现：
对Rerank理解不足

      ↓

Q3
如果Rerank之后Context仍然很长，
你会怎么处理？

      ↓

用户回答

      ↓

继续分析
```

这样才能实现：

> **真正的动态面试。**

---

# 18. 模拟面试结束 Flow

```text
Interview End
      ↓
Collect Interview History
      ↓
Answer Evaluation
      ↓
Overall Evaluation
      ↓
Skill Gap Detection
      ↓
Knowledge Gap Detection
      ↓
生成面试报告
      ↓
更新User Profile
      ↓
更新Skill Profile
      ↓
更新Memory
```

最终形成：

```text
本次面试
   ↓
发现Gap
   ↓
更新个人画像
   ↓
下一次面试
   ↓
针对Gap出题
```

---

# 19. Multi-Agent Flow

并不是所有任务都需要Multi-Agent。

## 简单任务

例如：

> 分析一个JD

使用单Agent即可：

```text
User
 ↓
Agent
 ↓
Tools / RAG
 ↓
Answer
```

## 复杂任务

例如：

> 帮我准备一次AI Agent岗位模拟面试

可以使用Multi-Agent：

```text
                    Supervisor
                         │
          ┌──────────────┼──────────────┐
          ↓              ↓              ↓
     Research Agent   Interview Agent  Evaluation Agent
          │              │              │
          ↓              ↓              ↓
      Web Search     Interview KB     Answer Analysis
                         │
                         ↓
                  Supervisor
                         │
                         ↓
                    Final Result
```

---

# 20. Agent选择原则

项目中不为了使用Multi-Agent而使用Multi-Agent。

遵循：

```text
简单任务
 ↓
Single Agent

复杂任务
 ↓
Workflow

任务之间存在明显独立职责
 ↓
Multi-Agent
```

例如：

| 场景        | 推荐架构                    |
| --------- | ----------------------- |
| JD信息提取    | Workflow / Single Agent |
| JD分析      | Single Agent + Tools    |
| Skill Gap | Workflow + LLM          |
| 行业信息收集    | Agent + Tools           |
| 招聘信息收集    | Agent + Tools           |
| 面试知识整理    | Workflow + RAG          |
| 模拟面试      | Agent Loop              |
| 复杂面试准备    | Multi-Agent             |

---

# 21. 统一状态 State

所有Agent Flow需要围绕统一Task State运行。

```text
TaskState

├── user_input
├── intent
├── user_profile
├── conversation_history
├── memory
├── retrieved_knowledge
├── current_task
├── plan
├── current_step
├── tool_results
├── observations
├── decision
├── reflection
├── final_answer
└── evaluation
```

不同Use Case只使用其中部分状态。

---

# 22. 整体系统最终 Flow

```text
                         User
                           │
                           ▼
                      Perception
                           │
                           ▼
                   Intent Recognition
                           │
                           ▼
                    Context Builder
                           │
          ┌────────────────┼────────────────┐
          │                │                │
       Memory             RAG          User Profile
          │                │                │
          └────────────────┼────────────────┘
                           │
                           ▼
                        Planner
                           │
                           ▼
                       Executor
                           │
             ┌─────────────┼─────────────┐
             │             │             │
             ▼             ▼             ▼
           Tools          RAG         Agents
             │             │             │
             └─────────────┼─────────────┘
                           │
                           ▼
                       Observation
                           │
                           ▼
                        Decision
                           │
                ┌──────────┼──────────┐
                │          │          │
             Continue     Retry      Re-plan
                │          │          │
                └──────────┼──────────┘
                           │
                           ▼
                       Reflection
                           │
                           ▼
                      Final Answer
                           │
                           ▼
                     Memory Update
                           │
                           ▼
                    Evaluation
```

---

# 23. Agent设计原则

## 原则1：先判断任务，再决定是否使用Agent

不是所有任务都需要Agent。

简单的数据处理可以直接使用Workflow或普通Function。

---

## 原则2：Planner负责计划，Executor负责执行

Planner：

> 做什么？

Executor：

> 怎么执行？

Decision：

> 下一步做什么？

Reflection：

> 做得对不对？

---

## 原则3：Memory不是聊天记录

只有对未来任务有价值的信息才应该进入长期Memory。

---

## 原则4：RAG不是所有问题都需要

只有需要外部知识或个人知识库时才进行Retrieval。

---

## 原则5：工具调用必须可验证

Agent调用工具之后必须检查：

* 是否成功
* 数据是否完整
* 数据是否符合预期
* 是否需要重试

---

## 原则6：复杂任务才使用Multi-Agent

避免为了展示Multi-Agent而人为增加系统复杂度。

---

# 24. Agent Flow与后续架构的关系

本文件确定：

> Agent如何工作。

下一步 `05-architecture.md` 再回答：

> 这些Flow在系统中具体由哪些模块实现。

对应关系：

```text
04-agent-flow
      ↓
定义行为
      ↓
05-architecture
      ↓
定义系统模块
      ↓
代码实现
```

因此：

* Agent Flow不绑定具体代码
* Agent Flow不绑定具体数据库
* Agent Flow不绑定具体模型
* Agent Flow不绑定具体框架

具体技术实现放到 `05-architecture.md`。
