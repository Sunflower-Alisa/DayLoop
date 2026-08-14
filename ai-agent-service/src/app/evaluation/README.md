# Evaluation 评测模块

基于 `docs/06-evaluation.md` 的 AI Agent 评测实现，覆盖规则评测、RAG 检索评测、
LLM-as-a-Judge、Agent 端到端任务成功率四类，并输出可读报告。

## 运行方式

```bash
# 全量评测（含 LLM Judge，需要 DEEPSEEK_API_KEY）
python -m app.evaluation.runner

# 跳过依赖 LLM 的评测（agent/interview/faithfulness/answer_relevance）
python -m app.evaluation.runner --offline

# 只跑指定类别
python -m app.evaluation.runner --category intent,jd,rag
python -m app.evaluation.runner --category agent,interview

# 自定义报告输出目录
python -m app.evaluation.runner --report-dir ./tmp/report
```

报告输出到 `reports/evaluation_report.json` 与 `reports/evaluation_report.md`。
Agent 评测期间会临时重定向 `MEMORY_DIR`/`INTERVIEW_SESSION_DIR`/`CHROMA_PERSIST_DIR`
到临时目录，不会污染工作区的 `.memory/` `.chroma/`。

## 类别与评测方式

| 类别 | 数据集 | 评测方式 | 是否需 LLM |
| --- | --- | --- | --- |
| `intent` | intent_cases.json | 规则 IntentDetector | 否 |
| `jd` | jd_cases.json | JD 字段解析对比 | 否 |
| `skill` | skill_cases.json | skill_matching 集合比对 | 否 |
| `memory` | memory_cases.json | LongTermMemory 存取召回 | 否 |
| `planner` | planner_cases.json | 计划步骤数 + 首步名 | 否 |
| `tool` | tool_cases.json | 工具实例化 + 执行 | 否 |
| `rag` | rag_cases.json | Chroma 检索 HitRate@3 等 | 否 |
| `agent` | agent_cases.json | Router → Agent 端到端 | 否（失败降级） |
| `interview` | interview_cases.json | LLM Judge 面试回答评分 | 是 |
| `faithfulness` | faithfulness_cases.json | LLM Judge 忠实度 | 是 |
| `answer_relevance` | answer_relevance_cases.json | LLM Judge 相关性 | 是 |

LLM Judge 失败时自动降级为关键词重叠启发式评分，评测不会中断。

## MVP 目标（docs/06-evaluation.md §57）

| 指标 | 目标 |
| --- | --- |
| intent_accuracy | 0.90 |
| task_success_rate | 0.85 |
| tool_success_rate | 0.95 |
| hit_rate@3 | 0.85 |

## 目录结构

```
evaluation/
├── __init__.py            # run_evaluation 惰性导出
├── metrics.py             # 指标函数（accuracy/hit_rate/mrr/...）
├── test_cases.py          # 测试集加载
├── error_analysis.py      # 失败归因分类
├── runner.py              # CLI 入口 + 汇总报告
├── datasets/              # 各类别 JSON 测试集
├── evaluators/
│   ├── rule_based.py      # 规则评测
│   ├── rag_evaluator.py   # RAG 检索评测
│   └── llm_judge.py       # LLM-as-a-Judge
└── reports/               # 报告输出目录
```

## 新增测试集指南

1. 在 `datasets/` 新建 `{name}_cases.json`，格式为 JSON 数组（rag 为 `{documents, queries}`）。
2. 在 `runner._CATEGORIES` 注册 `{name}`，指定 `("rule"|"rag"|"agent"|"llm_judge", needs_llm)`。
3. 若用 `rule`，在 `runner._run_category` 或 `rule_based.py` 补充对应的评测函数。
4. 运行 `python -m app.evaluation.runner --category {name}` 验证。

## 说明

- `rag_cases.json` 特殊：是 `{"documents": [...], "queries": [...]}` 结构，其余类别为数组。
- agent 类别在 DayLoop 服务不可用时按降级行为判定（能产出提示即算成功）。
- 离线模式（`--offline`）跳过 `agent`/`interview`/`faithfulness`/`answer_relevance`。
