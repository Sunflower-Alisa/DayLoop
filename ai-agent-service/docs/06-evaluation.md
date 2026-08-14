> **DayLoop + 独立 AI Agent Service + RAG + Memory + Tool Calling + 模拟面试 + API**

> **怎么证明这个 Agent 是“有效的”，而不是仅仅“能跑起来”？**

# 06-evaluation.md

# Evaluation

## 1. 评测目标

AI Agent Evaluation用于衡量DayLoop AI Agent在真实使用场景下的：

- 任务完成能力
- 意图识别能力
- Agent决策能力
- Tool Calling准确性
- RAG检索质量
- RAG回答质量
- Memory有效性
- Context构建质量
- 模拟面试质量
- API稳定性
- 响应速度
- Token消耗
- 系统可靠性

评测的核心目标不是：

> Agent是否能够返回答案。

而是：

> Agent是否能够在正确的Context下，选择正确的行动，完成用户任务，并产生可验证的结果。


# 2. Evaluation总体架构

                         Evaluation System
                                │
             ┌──────────────────┼──────────────────┐
             │                  │                  │
             ▼                  ▼                  ▼
        Offline Test       Online Evaluation   Human Evaluation
             │                  │                  │
             ▼                  ▼                  ▼
        Test Dataset        Real Requests      User Feedback
             │                  │                  │
             └──────────────────┼──────────────────┘
                                ▼
                         Evaluation Engine
                                │
                ┌───────────────┼───────────────┐
                ▼               ▼               ▼
             Metrics       Error Analysis     Reports


# 3. Evaluation分层

评测分为五个层级：

```text
L1 API Layer
L2 Agent Layer
L3 RAG / Memory Layer
L4 Use Case Layer
L5 System Layer
```

---

# 4. L1 API Layer Evaluation

主要评估Agent API和DayLoop API之间的通信质量。

指标：

* HTTP成功率
* API错误率
* Timeout Rate
* P95响应时间
* P99响应时间
* Schema正确率

---

## 4.1 API Success Rate

```text
API Success Rate =
Successful Requests / Total Requests
```

目标：

```text
>= 99%
```

---

## 4.2 API Schema Accuracy

检查：

* Request是否符合Schema
* Response是否符合Schema
* 字段是否缺失
* 数据类型是否正确

例如：

```json
{
  "user_id": "123",
  "skills": []
}
```

不能返回：

```json
{
  "user": 123
}
```

---

# 5. L2 Agent Layer Evaluation

评估Agent本身。

核心指标：

```text
Intent Accuracy
Plan Success Rate
Tool Selection Accuracy
Tool Execution Success Rate
Task Success Rate
Decision Accuracy
Reflection Effectiveness
```

---

# 6. Intent Accuracy

评估Agent是否正确理解用户意图。

测试：

```text
输入：
帮我看看这个岗位值不值得投。

Expected:
JD_ANALYSIS
```

Agent输出：

```text
JD_ANALYSIS
```

则：

```text
Correct = 1
```

否则：

```text
Correct = 0
```

公式：

```text
Intent Accuracy =
Correct Intent / Total Cases
```

目标：

```text
>= 90%
```

---

# 7. Plan Quality

评估Planner生成的任务计划是否合理。

例如：

```text
任务：
分析JD与个人技能匹配度

正确Plan：

1. 解析JD
2. 获取Resume
3. 获取Skills
4. 提取JD技能要求
5. Skill Matching
6. Gap Analysis
7. 输出建议
```

评估：

* 步骤完整性
* 步骤顺序
* 是否包含不必要步骤
* 是否遗漏关键步骤
* 是否能够执行

---

# 8. Plan Success Rate

定义：

```text
Plan Success Rate =
Successfully Completed Plans / Total Plans
```

例如：

```text
100个测试任务

成功完成：
87

Plan Success Rate = 87%
```

---

# 9. Tool Selection Evaluation

评估Agent是否选择正确的Tool。

例如：

```text
用户：
帮我查一下GitHub最近有哪些AI Agent项目。

正确Tool：
github_search
```

如果调用：

```text
calculator
```

则Tool Selection错误。

---

## 9.1 Tool Selection Accuracy

```text
Tool Selection Accuracy =
Correct Tool Selection / Total Tool Decisions
```

---

# 10. Tool Execution Evaluation

Tool选择正确并不代表执行成功。

需要区分：

```text
Tool Selection
        ↓
Tool Execution
```

指标：

```text
Tool Execution Success Rate
Tool Error Rate
Tool Timeout Rate
```

---

# 11. Decision Evaluation

Decision负责：

```text
继续
结束
Retry
Re-plan
Tool Call
```

例如：

```text
当前Step：
获取用户Skills

Tool返回成功。

正确Decision：
Continue
```

如果Agent错误地：

```text
Retry
```

则Decision错误。

---

# 12. Agent Loop Evaluation

重点防止：

```text
无限循环
```

指标：

```text
Average Iterations
Max Iterations
Loop Error Rate
Premature Stop Rate
```

---

## 12.1 Premature Stop

Agent过早结束：

```text
Plan：

1. Parse JD
2. Get Resume
3. Get Skills
4. Compare
5. Generate Result

实际：

1. Parse JD
2. Generate Result
```

这种情况属于：

```text
Premature Stop
```

---

# 13. Task Success Rate

这是Agent最重要的指标之一。

定义：

```text
Task Success Rate =
Successfully Completed Tasks / Total Tasks
```

例如：

```text
JD分析
100 cases

成功：
91

Task Success Rate = 91%
```

---

# 14. L3 RAG Evaluation

RAG评测分为两个部分：

```text
Retrieval Evaluation
+
Generation Evaluation
```

不能只评价最终答案。

---

# 15. Retrieval Evaluation

评估：

> Agent有没有找到正确的知识？

核心指标：

```text
Precision@K
Recall@K
MRR
Hit Rate@K
NDCG@K
```

---

# 16. Hit Rate@K

如果正确答案存在于Top-K结果：

```text
Hit = 1
```

否则：

```text
Hit = 0
```

公式：

```text
Hit Rate@K =
Queries with Relevant Document in Top-K
/
Total Queries
```

例如：

```text
100个问题

Top-5找到相关知识：
92

Hit Rate@5 = 92%
```

---

# 17. Precision@K

Top-K结果中，有多少是真正相关的。

```text
Precision@K =
Relevant Documents in Top-K
/
K
```

---

# 18. Recall@K

所有相关知识中，有多少被检索出来。

```text
Recall@K =
Relevant Retrieved Documents
/
Total Relevant Documents
```

---

# 19. MRR

用于评价：

> 第一个正确结果出现得有多靠前。

如果：

```text
Top1 = 正确
```

则：

```text
MRR = 1
```

如果：

```text
Top2 = 正确
```

则：

```text
MRR = 0.5
```

---

# 20. RAG Generation Evaluation

检索正确并不代表最终答案正确。

因此需要评价：

```text
Faithfulness
Answer Relevance
Context Relevance
Completeness
```

---

# 21. Faithfulness

回答是否基于检索到的知识。

重点防止：

> RAG找到了正确资料，但是LLM自己编造了内容。

---

# 22. Answer Relevance

回答是否真正回答了用户的问题。

例如：

```text
问题：
RAG中的Chunk为什么需要Overlap？

回答：
RAG是一种知识增强技术。
```

虽然内容没有完全错误，但：

```text
Answer Relevance = Low
```

---

# 23. Context Relevance

检索出来的Context是否与问题相关。

例如：

```text
Query：
什么是RAG Evaluation？

Top5：
4个是RAG Evaluation
1个是Python基础
```

Context Relevance较高。

---

# 24. RAG综合指标

RAG Evaluation：

```text
Retrieval
│
├── Hit Rate@K
├── Precision@K
├── Recall@K
├── MRR
└── NDCG
        │
        ▼
Generation
│
├── Faithfulness
├── Answer Relevance
├── Context Relevance
└── Completeness
```

---

# 25. RAG Evaluation Dataset

建立固定测试集：

```text
evaluation/
└── rag_cases.json
```

示例：

```json
{
  "id": "rag_001",
  "question": "什么是Agent Memory？",
  "expected_documents": [
    "memory.md"
  ],
  "expected_answer_keywords": [
    "长期记忆",
    "短期记忆",
    "Context"
  ]
}
```

---

# 26. L3 Memory Evaluation

Memory主要评估：

```text
Memory Retrieval Accuracy
Memory Relevance
Memory Recall
Memory Precision
Memory Update Accuracy
Memory Conflict Rate
```

---

# 27. Memory Recall

测试：

```text
用户：
我目标岗位是AI Agent开发。

保存Memory。

---

若干轮对话后：

用户：
我目前主要找什么岗位？
```

Agent应该回答：

```text
AI Agent开发
```

---

# 28. Memory Relevance

不是所有Memory都应该进入Context。

例如：

```text
Memory：

用户喜欢某种音乐
用户目标岗位
用户技能
用户昨天吃了什么
```

进行：

```text
JD Analysis
```

应该优先：

```text
目标岗位
技能
简历
```

而不是：

```text
昨天吃了什么
```

---

# 29. Memory Update Accuracy

测试：

```text
旧Memory：

Python = 基础

用户：

我最近完成了一个Python Agent项目。
```

Agent应该更新：

```text
Python = 项目经验
```

而不是：

```text
重复创建Memory
```

---

# 30. L4 Use Case Evaluation

根据实际业务场景建立测试集。

```text
UC01 AI行业信息
UC02 AI招聘
UC03 JD分析
UC04 Skill Gap
UC05 面试题整理
UC06 模拟面试
```

---

# 31. UC01 AI行业信息评测

评估：

```text
信息覆盖率
信息准确率
重复信息率
时效性
摘要质量
相关性
```

重点：

> 过去24小时的信息是否真正属于过去24小时。

---

# 32. UC02 AI招聘评测

评估：

```text
岗位相关性
岗位信息准确性
重复率
发布时间准确性
匹配度
推荐准确率
```

重点：

> Agent推荐的岗位是否真的值得用户投递。

---

# 33. UC03 JD分析评测

评估：

```text
JD字段提取准确率
Skill Extraction Accuracy
Requirement Classification
Skill Matching Accuracy
Gap Analysis Accuracy
Recommendation Accuracy
```

---

# 34. JD字段提取

例如：

```text
JD：

要求：
3年以上Python开发经验
熟悉RAG、LangGraph
本科以上学历
```

Agent应该提取：

```json
{
  "experience": "3+",
  "education": "Bachelor",
  "skills": [
    "Python",
    "RAG",
    "LangGraph"
  ]
}
```

---

# 35. UC04 Skill Gap Evaluation

评估：

```text
Skill Detection Accuracy
Gap Detection Accuracy
Strength Detection Accuracy
Recommendation Quality
```

最终：

```text
Resume
   +
JD
   ↓
Skill Matching
   ↓
Strength
Gap
Missing Skill
   ↓
Recommendation
```

---

# 36. UC05 Interview Knowledge Evaluation

评估：

```text
Interview Content Detection
Question Extraction Accuracy
Duplicate Detection
Classification Accuracy
Knowledge Quality
```

例如：

```text
笔记：

今天聊了RAG的面试题...
```

Agent应该识别：

```text
Interview Related = True
```

并提取：

```text
Question
Answer
Category
Difficulty
```

---

# 37. UC06 Mock Interview Evaluation

模拟面试需要单独建立评价体系。

核心指标：

```text
Question Quality
Difficulty Matching
Resume Relevance
JD Relevance
Answer Evaluation Accuracy
Follow-up Quality
Interview Coherence
```

---

# 38. Interview Question Quality

好的问题应该：

* 与目标岗位相关
* 与用户简历相关
* 与用户技能相关
* 难度合理
* 能够继续追问

例如：

```text
用户简历：
实现过RAG系统

第一问：

请介绍一下你实现的RAG系统。

↓

用户回答

第二问：

你的Retriever使用了什么策略？

↓

第三问：

如果Recall很高但是Precision很低，你会怎么优化？
```

这比随机生成面试题更加符合真实面试。

---

# 39. Follow-up Quality

评估：

> Agent是否根据用户刚才的回答继续追问。

而不是：

```text
Question 1
 ↓
Question 2
 ↓
Question 3
```

每个问题之间没有关系。

好的面试：

```text
Question
 ↓
Answer
 ↓
Analysis
 ↓
Follow-up
 ↓
Answer
 ↓
Deeper Follow-up
```

---

# 40. Interview Answer Evaluation

用户回答后评价：

```text
Technical Accuracy
Completeness
Depth
Clarity
Structure
Practical Experience
```

输出：

```json
{
  "score": 78,
  "strengths": [],
  "weaknesses": [],
  "missing_points": [],
  "suggestions": []
}
```

---

# 41. LLM-as-a-Judge

对于难以通过规则判断的指标，可以使用LLM进行评价。

例如：

```text
Question
Expected Answer
User Answer
```

交给Evaluator LLM：

```text
请评价用户回答：

1. 技术正确性
2. 完整性
3. 深度
4. 是否存在事实错误
5. 是否真正回答问题
```

输出：

```json
{
  "correctness": 8,
  "completeness": 7,
  "depth": 6,
  "overall": 7.2
}
```

---

# 42. LLM-as-a-Judge注意事项

LLM Judge不能作为唯一评价方式。

应该结合：

```text
Rule-based
+
Ground Truth
+
LLM Judge
+
Human Evaluation
```

避免：

> 一个LLM评价另一个LLM，然后把评价结果直接当成真值。

---

# 43. L5 System Evaluation

系统级指标：

```text
Task Success Rate
Availability
Latency
Token Usage
Cost
Error Rate
User Satisfaction
```

---

# 44. Latency

记录：

```text
Average Latency
P50
P95
P99
```

例如：

```text
P50 = 3s
P95 = 8s
P99 = 15s
```

重点关注P95，而不是只看平均值。

---

# 45. Token Evaluation

记录：

```text
Input Tokens
Output Tokens
Total Tokens
```

计算：

```text
Average Tokens / Task
```

重点观察：

> Context Engineering是否有效减少了无效Context。

---

# 46. Cost Evaluation

如果LLM收费，则：

```text
Task Cost
Daily Cost
Monthly Estimated Cost
```

例如：

```text
JD Analysis
平均Token = 5000

Mock Interview
平均Token = 12000
```

根据实际模型价格计算成本。

---

# 47. User Satisfaction

用户评价：

```text
1 ~ 5
```

例如：

```text
回答是否有帮助？
1 2 3 4 5
```

以及：

```text
是否愿意采纳Agent建议？
Yes / No
```

---

# 48. Evaluation Dataset

建立统一测试数据：

```text
evaluation/
│
├── test_cases.py
├── datasets/
│   ├── intent_cases.json
│   ├── planner_cases.json
│   ├── tool_cases.json
│   ├── rag_cases.json
│   ├── memory_cases.json
│   ├── jd_cases.json
│   └── interview_cases.json
│
├── metrics.py
├── evaluators/
│   ├── rule_based.py
│   ├── llm_judge.py
│   └── rag_evaluator.py
│
├── error_analysis.py
└── reports/
```

---

# 49. Test Case结构

统一：

```json
{
  "id": "jd_001",
  "category": "JD_ANALYSIS",
  "input": "...",
  "context": {},
  "expected": {},
  "evaluation": {
    "type": "llm_judge"
  }
}
```

---

# 50. Evaluation类型

分为：

## Unit Evaluation

测试单个模块：

```text
Planner
Retriever
Tool
Memory
Parser
```

---

## Integration Evaluation

测试：

```text
Agent + Tool
Agent + RAG
Agent + Memory
Agent + DayLoop API
```

---

## End-to-End Evaluation

模拟真实用户：

```text
User
 ↓
DayLoop
 ↓
Agent
 ↓
Tools
 ↓
RAG
 ↓
DayLoop
 ↓
Result
```

---

# 51. Regression Evaluation

每次修改Agent后自动运行固定测试集。

例如：

```text
v1.0

Task Success = 86%
RAG Hit@5 = 89%

↓

修改Context Manager

↓

v1.1

Task Success = 91%
RAG Hit@5 = 90%
```

如果出现：

```text
v1.2

Task Success = 83%
```

则发现Regression。

---

# 52. Agent Evaluation Pipeline

```text
Code Change
    ↓
Run Unit Tests
    ↓
Run Agent Test Cases
    ↓
Run RAG Evaluation
    ↓
Run E2E Evaluation
    ↓
Calculate Metrics
    ↓
Compare Previous Version
    ↓
Regression Detection
    ↓
Evaluation Report
```

---

# 53. Error Analysis

不能只记录：

```text
Success / Failure
```

还需要分析失败原因。

分类：

```text
Error
│
├── Intent Error
├── Context Error
├── Planning Error
├── Tool Selection Error
├── Tool Execution Error
├── RAG Retrieval Error
├── Hallucination
├── Memory Error
├── Decision Error
├── API Error
└── Final Answer Error
```

---

# 54. Error Analysis Example

```text
Case:
JD分析

Result:
Failure

Analysis:

Intent:
Correct

Context:
Missing Skill Profile

Planner:
Correct

Tool:
get_skills未调用

Decision:
Premature Stop

Final Answer:
Incomplete
```

最终归因：

```text
Primary Error:
Decision Error

Secondary Error:
Context / Tool Error
```

---

# 55. Evaluation Dashboard

未来可以在DayLoop中增加：

```text
AI Agent Evaluation
│
├── Task Success Rate
├── Intent Accuracy
├── Tool Accuracy
├── RAG Hit@5
├── Memory Recall
├── Avg Latency
├── Token Usage
├── Cost
└── User Satisfaction
```

---

# 56. MVP Evaluation指标

MVP阶段不追求一次性实现所有指标。

优先实现：

| 指标                          | 优先级 |
| --------------------------- | --- |
| Task Success Rate           | P0  |
| Intent Accuracy             | P0  |
| Tool Success Rate           | P0  |
| RAG Hit@5                   | P0  |
| RAG Faithfulness            | P0  |
| JD Skill Extraction         | P0  |
| Skill Gap Accuracy          | P0  |
| Interview Question Quality  | P0  |
| Interview Answer Evaluation | P0  |
| API Success Rate            | P1  |
| P95 Latency                 | P1  |
| Token Usage                 | P1  |
| Cost                        | P1  |
| Memory Recall               | P1  |
| User Satisfaction           | P1  |

---

# 57. MVP目标

第一阶段目标：

```text
Task Success Rate       >= 85%
Intent Accuracy         >= 90%
Tool Success Rate       >= 95%
RAG Hit@5               >= 85%
API Success Rate        >= 99%
```

这些数字不是绝对行业标准，而是项目初期用于判断迭代是否有效的内部目标。

随着真实数据增加，再根据Baseline调整。

---

# 58. Evaluation Baseline

任何优化之前必须建立Baseline。

例如：

```text
Agent v0.1

Task Success Rate: 78%
Intent Accuracy: 85%
RAG Hit@5: 72%
Tool Success Rate: 91%
P95 Latency: 8.2s
```

优化：

```text
Context Engineering
```

之后：

```text
Agent v0.2

Task Success Rate: 86%
Intent Accuracy: 91%
RAG Hit@5: 84%
Tool Success Rate: 96%
P95 Latency: 7.1s
```

这样才能证明：

> 优化确实有效。

---

# 59. Evaluation原则

## 原则1：先定义成功，再实现Agent

不要：

```text
先写Agent
↓
最后想怎么评价
```

应该：

```text
Use Case
↓
Success Criteria
↓
Test Cases
↓
Agent
↓
Evaluation
```

---

## 原则2：不要只评价最终答案

需要评价整个Agent过程：

```text
Intent
 ↓
Context
 ↓
Plan
 ↓
Tool
 ↓
Observation
 ↓
Decision
 ↓
Final Answer
```

---

## 原则3：离线评测 + 在线评测

```text
Offline Evaluation
+
Online Evaluation
```

离线保证稳定。

在线反映真实用户体验。

---

## 原则4：自动评价 + 人工评价

```text
Automated Evaluation
+
LLM Judge
+
Human Evaluation
```

三者结合。

---

# 60. 最终Evaluation闭环

```text
                User / Test Dataset
                         │
                         ▼
                     AI Agent
                         │
        ┌────────────────┼────────────────┐
        ▼                ▼                ▼
      Intent           Plan             Tool
        │                │                │
        └────────────────┼────────────────┘
                         ▼
                       RAG
                         │
                         ▼
                      Memory
                         │
                         ▼
                     Execution
                         │
                         ▼
                       Result
                         │
          ┌──────────────┼──────────────┐
          ▼              ▼              ▼
       Metrics       Error Analysis   Human Judge
          │              │              │
          └──────────────┼──────────────┘
                         ▼
                  Evaluation Report
                         │
                         ▼
                    Agent Version
                         │
                         ▼
                  Regression Test
                         │
                         └──────────→ Next Version
```

---

# 61. 最终目标

AI Agent Evaluation最终形成：

```text
Build
 ↓
Test
 ↓
Evaluate
 ↓
Analyze
 ↓
Optimize
 ↓
Regression Test
 ↓
Release
```

形成完整的Agent Engineering闭环：

> **不是让Agent“看起来很聪明”，而是通过可量化指标证明Agent在真实任务中越来越可靠。**

````