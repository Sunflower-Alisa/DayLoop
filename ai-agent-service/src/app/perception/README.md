# Perception 感知层

`app/preception` 是 AI Agent 的感知层，对应 `docs/05-architecture.md` §17 Perception。

## 职责

把用户的原始输入（文本 / 语音 / 图片 / 混合）转换为下游可用的结构化理解：

```
输入  User Input / DayLoop Data / External Data / Conversation
        │
        ▼
   [Modality]  text | audio | image | multimodal
        │
        ▼
   [ASR] 语音 → 文本
   [Multimodal] 图片 → 结构化描述
        │
        ▼
   [Normalizer] 清洗文本（零宽字符 / 空白压缩）
        │
        ▼
   [EntityExtractor] 规则抽取实体（岗位/城市/公司/技能/URL/JD）
   [IntentDetector]  规则优先 + LLM 兜底识别意图
        │
        ▼
   [PerceptionResult]  Intent / Entity / Task / Context Requirement
```

## 目录结构

| 文件 | 说明 |
| --- | --- |
| `perception.py` | 入口 `PerceptionService`，编排整条感知流水线 |
| `result.py` | 输出模型 `PerceptionResult` |
| `normalizer.py` | 输入文本清洗 |
| `asr.py` | 语音转文字（OpenAI Whisper） |
| `multimodal.py` | 图片分析（多模态 LLM） |
| `entity.py` | 规则式实体抽取 |
| `intent.py` | 意图常量 / 上下文需求表 / 意图识别 |
| `__init__.py` | 包公开 API |

## 依赖说明

- 所有大模型调用统一经由 `app/llm/client.py` 的 `LLMClient`，感知层不自建 OpenAI client。
- 配置统一读取 `app/core/config.py` 的 `settings`，不直接读环境变量。
- 意图识别是「规则优先 + LLM 兜底」：规则能命中就用规则（离线可用、稳定），否则才调 LLM。
- 实体抽取为纯规则实现，不依赖 LLM。

## 快速开始

### 1. 环境准备

```bash
cd ai-agent-service/src
# 安装依赖（venv 已存在时跳过）
.venv/Scripts/python.exe -m pip install openai

# 设置 LLM 配置（二选一）
#   deepseek（默认，用于意图识别兜底）
$env:LLM_PROVIDER = "deepseek"
$env:DEEPSEEK_API_KEY = "sk-..."
#   openai（用于多模态图片分析 / 语音转写，需额外设置）
$env:OPENAI_API_KEY = "sk-..."
```

> 不配置 key 也能用：意图识别会回退到纯规则模式，不会崩溃。

### 2. 最小示例

```python
from app.preception import PerceptionService

svc = PerceptionService()

# 文本输入
r = svc.perceive("帮我分析一下这个AI Agent岗位的JD，看看值不值得投")
print(r.intent)                  # JD_ANALYSIS
print(r.intent_confidence)       # 0.9
print(r.task)                    # 分析JD并评估岗位匹配
print(r.context_requirements)    # ['JD', 'Resume', 'Skill Profile', 'Job Preference', 'Memory']
print(r.entities)                # [{'type': 'position', 'value': 'AI Agent', ...}]
```

### 3. 各输入模态

```python
# 语音（需要 OPENAI_API_KEY）
r = svc.perceive(audio_path="path/to/audio.mp3")

# 图片（需要 OPENAI_API_KEY）
r = svc.perceive(image_path="path/to/jd_screenshot.png")

# 多模态（文本 + 图片同时提供）
r = svc.perceive("看看这张图的岗位要求", image_path="path/to/jd.png")

# 附加元数据（会话信息等，写入 r.metadata["extra"]）
r = svc.perceive("帮我安排任务", extra={"user_id": "alisa", "session_id": "s1"})
```

### 4. 只做规则模式（无 API key / 离线）

```python
svc = PerceptionService(use_llm_intent=False)
r = svc.perceive("你好")
r.metadata["intent_method"]      # rule
```

### 5. 单独使用子模块

```python
from app.preception.entity import EntityExtractor
from app.preception.intent import IntentDetector
from app.preception.normalizer import normalize_text

normalize_text("  帮我\n分析\n这个JD  ")          # "帮我 分析 这个JD"
EntityExtractor().extract("工作地点：上海，技能：Python RAG")
IntentDetector(use_llm=False).detect("我们来模拟面试")
```

## 意图列表

| 常量 | 说明 | 需要的上下文 |
| --- | --- | --- |
| `INDUSTRY_INFO` | AI 行业信息（UC01） | Knowledge, External Data |
| `JOB_SEARCH` | 招聘信息（UC02） | Job Preference, Target Position, Skill Profile, External Data |
| `JD_ANALYSIS` | JD 分析（UC03） | JD, Resume, Skill Profile, Job Preference, Memory |
| `SKILL_GAP` | 技能差距（UC04） | Resume, Skill Profile, JD, Memory |
| `INTERVIEW_KNOWLEDGE` | 面试知识库（UC05） | Interview Knowledge, Memory |
| `MOCK_INTERVIEW` | 模拟面试（UC06） | JD, Resume, Skill Profile, Interview Knowledge, Memory |
| `RESUME_UPDATE` | 更新简历 | Resume |
| `TASK_MANAGEMENT` | 任务管理 | Tasks, Memory |
| `GENERAL_CHAT` | 通用对话（兜底） | Memory |

## 实体类型

规则抽取，`confidence` 表示置信度：

| 类型 | 示例 |
| --- | --- |
| `position` | AI Agent 应用开发、AI 产品经理、算法工程师 |
| `city` | 北京、上海、深圳、远程 |
| `company` | 字节跳动、腾讯、Anthropic |
| `skill` | Python、RAG、LangGraph、向量数据库 |
| `url` | https://... |
| `jd` | 完整的 JD 段落（命中岗位职责/任职要求等标记） |

别名会自动归一：`langgraph → LangGraph`、`大模型 → LLM` 等（见 `entity.py` 的 `SKILL_ALIASES`）。

## PerceptionResult 字段

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `text` | str | 标准化后的文本（语音/图片统一转到这里） |
| `modality` | str | `text` / `audio` / `image` / `multimodal` |
| `intent` | str | 意图，见 `INTENT_*` 常量 |
| `intent_confidence` | float | 意图置信度 |
| `entities` | list[dict] | 实体列表 `{type, value, confidence}` |
| `context_requirements` | list[str] | 下游需要加载的上下文 |
| `task` | str | 任务简短描述 |
| `metadata` | dict | 附加信息（intent_method / intent_hints / 文件路径 / extra） |
| `raw` | str | 原始输入（未标准化） |

## 常见问题

**Q: 不配 API key 会怎样？**
A: 意图识别回退到纯规则；ASR / 多模态图片分析会抛 `RuntimeError`（需要 `OPENAI_API_KEY`）。文件路径不存在时抛 `FileNotFoundError`。

**Q: 为什么意图识别是规则优先而不是直接走 LLM？**
A: 规则模式零成本、稳定、可离线测试（offline evaluation 友好），覆盖设计文档中的 9 类意图足够；LLM 仅作兜底，提高未命中规则的泛化能力。

**Q: 想新增一种意图？**
A: 在 `intent.py` 添加 `INTENT_*` 常量 → 加入 `ALL_INTENTS` → 在 `CONTEXT_REQUIREMENTS` 声明所需上下文 → 在 `_RULES` 添加正则。完成后更新 `perception.py` 的 `_TASK_DESC`。
