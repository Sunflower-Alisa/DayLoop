# DayLoop 个人学习成长工作台 · 需求设计文档

> 版本：v3.0（规划稿）
> 日期：2026-08-03
> 状态：待评审
> 定位：将 DayLoop 从「每日计划与复盘工具」升级为「个人学习成长工作台」

---

## 目录

1. [产品概述](#1-产品概述)
2. [模块划分与总体架构](#2-模块划分与总体架构)
3. [首页工作台（Home）](#3-首页工作台home)
4. [模块一：日程规划](#4-模块一日程规划)
5. [模块二：知识管理](#5-模块二知识管理)
6. [模块三：英语学习](#6-模块三英语学习)
   - 6.1 单词背诵（百词斩式）
   - 6.2 场景英语学习
   - 6.3 口语练习
   - 6.4 影视切片预览与跟读
7. [全局界面布局规范](#7-全局界面布局规范)
8. [页面路由规划](#8-页面路由规划)
9. [功能展示与跳转逻辑](#9-功能展示与跳转逻辑)
10. [数据模型设计](#10-数据模型设计)
11. [API 设计](#11-api-设计)
12. [技术方案与第三方依赖](#12-技术方案与第三方依赖)
13. [版本规划](#13-版本规划)
14. [非功能需求](#14-非功能需求)
15. [待确认问题](#15-待确认问题)

---

## 1. 产品概述

### 1.1 产品定位

DayLoop 从单一的「每日计划与复盘工具」升级为**个人学习成长工作台**，将「时间管理、知识沉淀、语言学习」三大板块整合到一个应用内，帮助用户完成从 **规划 → 执行 → 记录 → 复盘 → 优化** 的个人成长闭环，并在此基础上持续积累学习资产（单词量、场景知识、口语能力、影视素材）。

### 1.2 目标用户

- 个人学习者、自学者（备考、职业提升、兴趣学习）
- 需要同时管理「日程」与「学习」的上班族/学生
- 注重数据自托管、隐私、可导出的轻量工具爱好者

### 1.3 设计原则

| 原则 | 说明 |
|------|------|
| 一个入口管全部 | 首页工作台聚合今日任务 + 今日学习 + 成长数据，减少切换成本 |
| 模块边界清晰 | 三大模块（日程规划 / 知识管理 / 英语学习）导航分组、职责分明 |
| 移动优先 PWA | 沿用现有移动优先（max-width 480px）与 PWA 能力 |
| 学习闭环完整 | 英语学习遵循「学 → 练 → 测 → 复习 → 沉淀」闭环 |
| 渐进增强 | 核心能力优先免费浏览器方案（Web Speech API），通过 `evaluateApi`/`ttsProvider` 接口预留扩展 |
| 主题可扩展 | 当前单一配色主题，通过 `themeProvider` 接口预留多主题/暗色模式扩展 |
| 数据可控 | 所有学习数据进入现有 SQLite，支持统一导出，Obsidian 同步可选扩展 |

### 1.4 现有基线

| 项 | 说明 |
|----|------|
| 前端 | Vue 3 + TypeScript + Vite，双前端 `frontend/`（Node 后端）与 `frontend-dotnet/`（.NET 后端） |
| 后端 | Express.js（3001）+ ASP.NET Core（5000），共享同一 SQLite（`backend/data/dayloop.db`） |
| 现有功能 | 今日计划、复盘、总结、日历、成果、统计、循环模板、历史、备忘录、问题库、Obsidian 同步、PWA |
| 现有认证 | JWT Bearer token，`/api/auth` 注册登录 |

**改造范围说明**：
- 「日程规划」模块 = 现有 8 个核心功能**平移重组**，交互逻辑尽量不动，仅改导航与路由前缀（可选）。
- 「知识管理」模块 = 备忘录、问题库从「核心功能」中独立，赋予独立模块地位与增强建议。
- 「英语学习」模块 = **全新开发**，为本版重点。
- **实现优先级**：保留双后端（Node + .NET）功能一致；英语学习模块**先以 .NET 栈为主实现**（`backend-dotnet/` + `frontend-dotnet/`），稳定后同步移植到 Node.js 栈（`backend/` + `frontend/`）。

---

## 2. 模块划分与总体架构

### 2.1 模块总览

```
DayLoop 个人学习成长工作台
│
├── 首页工作台（Home）          聚合视图：今日任务 + 今日学习 + 成长数据
│
├── 模块一：日程规划            （现有 8 功能平移重组）
│     ├── 今日计划 /plan
│     ├── 每日复盘 /review
│     ├── 周期性总结 /summary
│     ├── 日历预览 /calendar
│     ├── 成果墙 /achievements
│     ├── 统计看板 /statistics
│     ├── 循环模板 /templates
│     └── 历史记录 /history
│
├── 模块二：知识管理            （备忘录、问题库独立成模块）
│     ├── 备忘录 /notes
│     └── 问题库 /questions
│
├── 模块三：英语学习            （新增，重点）
│     ├── 单词背诵 /english/words        （百词斩式）
│     ├── 场景英语 /english/scenarios
│     ├── 口语练习 /english/speaking
│     └── 影视切片 /english/clips
│
└── 系统
      ├── 个人设置 /profile
      ├── 服务器配置 / 导出数据（侧边栏动作）
      └── 登录/注册 /login /register
```

### 2.2 侧边栏导航设计

沿用现有抽屉式侧边栏（`App.vue` 中的 `sidebar-nav`），按「模块分组」重新组织 `coreNav`：

| 分组标题 | 导航项 | 图标 | 路由 name |
|----------|--------|------|-----------|
| **工作台** | 首页 | 🏠 | home |
| **日程规划** | 今日计划 / 每日复盘 / 周期性总结 / 日历预览 / 成果墙 / 统计看板 / 循环模板 | 📋 📊 📑 📅 🏆 📈 🔄 | plan / review / summary / calendar / achievements / statistics / templates |
| **知识管理** | 备忘录 / 问题库 | 📝 ❓ | notes / questions |
| **英语学习** | 单词背诵 / 场景英语 / 口语练习 / 影视切片 | 🔤 💬 🎤 🎬 | english-words / english-scenarios / english-speaking / english-clips |
| **系统** | 历史记录 / 个人设置 / 服务器配置 / 导出数据 | 🗂 👤 🌐 📤 | history / profile /（动作）/（动作） |

> 说明：历史记录归入「系统」层级（按日期浏览的归档性质）；服务器配置与导出数据保持为侧边栏动作按钮。

---

## 3. 首页工作台（Home）

### 3.1 定位

首页从「任务仪表盘」升级为「工作台」，成为用户每日的**唯一启动入口**，聚合三条主线：日程、学习、成长数据。

### 3.2 界面布局（自上而下）

```
┌────────────────────────────────┐
│ 顶部问候：日期 + 星期 + 用户名  │
│ 问候卡：xxx，又是元气满满的一天 │
│ [连续打卡 🔥3天] [今日学习完成度] │
├────────────────────────────────┤
│ ① 今日概览卡（双进度）           │
│    任务进度   ▓▓▓░░ 3/5  60%    │
│    学习进度   ▓▓░░░░ 12/30 40%  │
│    [去规划] [去背单词]           │
├────────────────────────────────┤
│ ② 今日任务列表（前 3 条 + 展开）│
│    快速勾选完成（复用现有逻辑）  │
├────────────────────────────────┤
│ ③ 学习速览卡                    │
│    今日单词 20新词/30复习  [开始] │
│    口语练习 1 篇待练         [去练]│
│    影视切片 今日推荐           [去看]│
├────────────────────────────────┤
│ ④ 成长数据卡（本周）             │
│    学习时长 / 单词量 / 连续天数   │
│    迷你柱状图（任务完成率周趋势） │
├────────────────────────────────┤
│ ⑤ 快捷入口宫格（模块入口卡片）    │
│    今日计划│备忘录│问题库│         │
│    单词背诵│场景英语│口语│影视     │
└────────────────────────────────┘
```

### 3.3 核心交互

| 区块 | 交互 |
|------|------|
| ① 今日概览 | 点击「去规划」跳 `/plan`；点击「去背单词」跳 `/english/words`（若今日未学则直接进入学习会话） |
| ② 今日任务 | 复用现有任务勾选 + 完成弹窗（成果记录）逻辑 |
| ③ 学习速览 | 单词卡显示「今日需学 X 新词 + Y 复习」；点击后进入 `words` 页，未开始则自动拉起学习会话 |
| ⑤ 快捷宫格 | 8 个入口卡，对应 8 个模块页面，图标 + 名称 + 一句话描述 |
| 学习完成度 | 从 `daily_learning_logs` 聚合当日已完成新词数 / 计划数 |

---

## 4. 模块一：日程规划

> 本模块全部为**现有功能平移**，不改核心业务逻辑，仅统一入口与视觉。以下每项给出：定位、布局要点、可选的轻量增强。

### 4.1 今日计划 `/plan`

- **定位**：每日任务的核心管理页，保持现状。
- **布局**：日期导航栏（上一天/今天/下一天）→ 计划内任务分组 → 计划外任务分组 → 底部「+ 新建任务」。
- **增强建议（可选）**：顶部增加「学习任务」快捷分类标签（英语/阅读等），便于把学习任务纳入日程。

### 4.2 每日复盘 `/review`

- **定位**：晚间反思入口，保持现状。
- **布局**：自动统计摘要卡（完成率、时长对比）→ 手动复盘文本框 → 保存按钮。
- **增强建议**：可追加「今日学习小结」分区（可选），将当日单词/口语数据写入复盘。

### 4.3 周期性总结 `/summary`

- **定位**：周/月/季度/年总结，保持现状。
- **布局**：周期类型 Tab → 期次选择 → 自动摘要 + 手动总结。

### 4.4 日历预览 `/calendar`

- **定位**：周/月/季度视图 + 空闲程度，保持现状。
- **增强建议**：学习日标记（有学习记录的日子显示学习角标），形成「成长日历」。

### 4.5 成果墙 `/achievements`（含 `/achievements/:id`）

- **定位**：已完成任务的成果沉淀，按分类筛选，保持现状。

### 4.6 统计看板 `/statistics`

- **定位**：全局统计，保持现状。
- **增强建议**：新增「学习统计」Tab：单词总量、掌握率、连续打卡、场景完成数、口语平均分、影视学习时长、累计学习时长。

### 4.7 循环模板 `/templates`

- **定位**：每日/每周循环任务模板管理，保持现状。

### 4.8 历史记录 `/history`

- **定位**：按日期浏览历史任务与复盘，保持现状。

---

## 5. 模块二：知识管理

> 备忘录与问题库从「核心功能」中独立为正式模块，作为知识沉淀的两大支柱。功能交互保持现状，突出模块化定位。

### 5.1 备忘录 `/notes`（含 `/notes/:id`、`/notes/new`、`/notes/categories`）

- **定位**：个人知识库/灵感池，Markdown 富文本 + 图片，可关联任务、分类、搜索。
- **模块化定位**：与「日程规划」解耦，可独立沉淀学习笔记、读书笔记、课程笔记。
- **增强建议（与英语学习打通）**：
  - 单词详情页支持「一键生成单词笔记」到备忘录；
  - 影视切片页支持「把该切片台词/词汇保存为备忘录」。

### 5.2 问题库 `/questions`（含 `/questions/:id`、`/questions/new`、`/questions/categories`）

- **定位**：记录学习/工作中遇到的问题及解答来源（自答/AI/网络），关联任务。
- **模块化定位**：作为「学习问题收集箱」，与场景英语、影视生词可互相关联（可选：问题可标记「待解决/已解决」增强）。

---

## 6. 模块三：英语学习（新增，重点）

### 6.1 模块概览与学习闭环

英语学习模块遵循完整闭环：**「学（单词/场景/影视）→ 练（跟读/口语）→ 测（题型/测验）→ 复习（间隔重复）→ 沉淀（笔记/错词本）」**。

**模块首页 `/english`（英语学习总览）** 布局：

```
┌────────────────────────────────┐
│ 顶部：英语学习徽标 + 连续打卡🔥  │
│ 今日学习目标进度（新词/复习）     │
├────────────────────────────────┤
│ 今日待办卡                       │
│  [背单词] 20新词+30复习   开始  │
│  [口语跟读] 2 个句子待练  开始  │
│  [影视切片] 今日推荐《...》 观看 │
├────────────────────────────────┤
│ 四个功能入口大卡（2×2）           │
│  ┌────────┬────────┐            │
│  │ 🔤单词背诵│ 💬场景英语│        │
│  ├────────┼────────┤            │
│  │ 🎤口语练习│ 🎬影视切片│        │
│  └────────┴────────┘            │
├────────────────────────────────┤
│ 学习数据：总词数/掌握率/打卡日历  │
│ 学习时长：今日 X 分钟 · 本周 Y 分钟│
│ 近期记录：错词本/收藏/学习历史    │
└────────────────────────────────┘
```

**打卡机制（贯穿全模块）**：每日完成至少一次学习（新词学习 / 口语练习 / 影视跟读）即记为打卡，连续天数展示在英语首页与工作台首页。

**学习时长统计**：进入单词学习/复习、口语练习、影视跟读等会话页即开启计时，离开或完成时结算为 `study_sessions` 记录；聚合为今日/本周/累计时长，展示在英语首页、工作台首页与统计看板。

---

### 6.2 单词背诵（百词斩式）`/english/words`

#### 6.2.1 功能结构

```
单词背诵
├── ① 学习首页 /english/words
│     ├── 打卡日历（连续天数、今日状态）
│     ├── 今日任务卡（X 新词 + Y 复习）
│     ├── [开始学习] [开始复习]
│     └── 数据区（总学 / 掌握 / 待复习 / 错词本）
├── ② 词书管理 /english/wordbooks
│     ├── 词书列表（封面/等级/词数/进度）
│     └── 词书详情 → 每日计划设置（新词数）
├── ③ 学习会话（全屏专注页）/english/words/learn
│     └── 逐词学习 + 巩固题型
├── ④ 复习会话（间隔重复）/english/words/review
├── ⑤ 单词详情 /english/words/:id
└── ⑥ 错词本 /english/words/wrong
```

#### 6.2.2 学习流程（百词斩式核心交互）

**Step 1 — 选词书 / 设计划**
- 首次使用选择词书（四级/六级/考研/雅思/高考/自定义导入）。
- 设置每日新词目标（默认 20，可调 10/15/20/30/50），系统按词书剩余量计算预计完成天数。

**Step 2 — 新词学习会话（`learn`）**
每个新词经历「**学习卡片 → 巩固题目**」两步：

1. **学习卡片**：整屏展示单词的图片 + 英文单词 + 音标 + 发音按钮 + 中文释义 + 例句（可点读）。右侧附「词根词缀 / 变形 / 记忆提示」折叠区。
   - 按钮：「认识，跳过」→ 该词立即进入复习队列；「发音」→ TTS 播放。
2. **巩固题目**（随机从以下题型出 1 题）：
   - **看词选义**：展示单词，4 个中文释义选项；
   - **看图选词**：展示图片，4 个英文单词选项（百词斩招牌题）；
   - **听音选义**：自动播放发音，4 个中文释义选项；
   - **看词选图**：展示单词，4 张图片选项；
   - **例句填空**：例句挖空，4 个单词选项；
   - **拼写题**：打乱字母，点击字母拼出单词（作对 2 次判掌握）。
3. **即时反馈**：选对 → 绿色高亮 + 提示音，自动下一词；选错 → 红色高亮 + 展示正确答案 + 发音，该词记入「待复习」。

**Step 3 — 学习完成结果页**
- 统计卡：本次正确率、学习新词数、掌握数、加入错词本数。
- 操作：查看错词 / 立即开始复习 / 返回。

**Step 4 — 复习会话（`review`，间隔重复）**
- 从「待复习队列」按遗忘曲线取词，只出巩固题（无学习卡片、无跳过）。
- 答对 → 进入下一复习阶段；答错 → 重置回阶段 1。

#### 6.2.3 记忆算法（间隔重复，参考艾宾浩斯）

| 阶段 | 间隔 | 说明 |
|------|------|------|
| S0 | 学习当天 | 新词学习即进入 S0 |
| S1 | +1 天 | 第 2 天复习 |
| S2 | +2 天 | 第 4 天复习 |
| S3 | +4 天 | 第 8 天复习 |
| S4 | +7 天 | 第 15 天复习 |
| S5 | +15 天 | 第 30 天复习 → 判「已掌握」 |

- 状态机：`new（新词）→ learning（学习中）→ reviewing（复习中）→ mastered（已掌握）`
- 复习答错 → 回到 `learning` 且阶段重置 S0；连续答对 3 次 → 额外加权进度。
- 每日复习队列 = 所有 `next_review_at <= 今天` 的词。

#### 6.2.4 单词详情页 `/english/words/:id`

```
┌────────────────────────────┐
│ 大图（词卡图片）             │
│ [🔊发音] 单词 · 音标        │
│ 词性·释义（多条）           │
│ 例句 + 中文（可点读）        │
│ 词根词缀 / 变形 / 记忆提示   │
│ [⭐收藏] [➕生成备忘录笔记]    │
│ [标记已掌握 / 移出错词本]     │
└────────────────────────────┘
```

#### 6.2.5 词书与数据来源（已确认：联网下载资源）

- **词书下载**：开发期从网络获取开源词书资源（如 ECDICT 离线词库、公开的四六级/考研词表），整理后随「词书库」页面提供下载导入；MVP 内置 1 本入门词书保证首屏可用。
- **图片下载**：开发期批量从网络下载单词配图（免费图源，如 Openverse 等公开图片素材），随词书一起打包入库；无图词用「纯色底 + 首字母大字」兜底样式。
- **离线可用**：下载后的词书与图片均落库/落盘（`backend/data/`），学习过程无需联网。
- **CSV 导入**：支持 `单词,音标,词性,释义,例句,例句中文,图片URL` 格式导入，构建自定义词书。

---

### 6.3 场景英语学习 `/english/scenarios`

#### 6.3.1 定位

以「真实场景」为单位组织对话、句型、词汇、测验，解决「背了单词不会用」的问题。

#### 6.3.2 页面结构

**① 场景列表 `/english/scenarios`**
- 顶部筛选：全部分类 / 生活 / 职场 / 旅行 / 购物 / 餐饮 / 医疗 / 社交 / 校园。
- 场景卡片：图标、场景名、对话句数、难度星级、掌握度进度条。
- 布局：分类 Tab 横向滚动 + 卡片网格（2 列）。

**② 场景详情 `/english/scenarios/:id`**（4 个 Tab）

| Tab | 内容 | 交互 |
|-----|------|------|
| 对话学习 | 对话逐条展示（说话人 + 英文 + 中文 + 发音） | 点击任意句播放；每条可「跟读」按钮 → 复用口语评测组件 |
| 关键句型 | 句型卡片：句式结构 + 例句 + 中文，可收藏 | 点击例句发音；收藏进「我的句型」 |
| 场景词汇 | 该场景高频词表（词卡列表） | 点击进单词详情；可加入学习计划 |
| 场景测验 | 情境选择题：给定场景情境，选最合适回应 | 做完即时反馈 + 解析；记录得分 |

**③ 场景完成判定**：对话全部播放过 + 测验 ≥ 60 分 → 场景标记「已掌握」。

---

### 6.4 口语练习 `/english/speaking`

#### 6.4.1 定位

以「跟读评测」为核心的口语训练：播放标准音 → 用户录音 → 系统打分（准确度/流利度/完整度）→ 逐句完成汇总。

#### 6.4.2 页面结构

**① 口语首页 `/english/speaking`**
- 今日推荐话题卡（从学习记录推荐未练话题）。
- 分类浏览：日常口语 / 职场英语 / 场景对话 / 影视经典。
- 练习记录：历史评分列表、平均分趋势。

**② 跟读练习页 `/english/speaking/:id`**

```
┌──────────────────────────────┐
│ 进度：句子 2/5   ▓▓▓░░        │
│ ┌──────────────────────────┐ │
│ │  句子文本（英文，大字）      │ │
│ │  Chinese 释义             │ │
│ │  [▶ 标准音] [0.75x/1.0x]  │ │
│ └──────────────────────────┘ │
│ [🎤 按住录音 / 点击开始录音]   │
│  录音状态 + 波形/音量提示     │
│ [▶ 回放我的录音]              │
│ 评分：准确度 92 · 流利度 88 · │
│       完整度 95  总分 91 ★★★ │
│ 发音对比：标准 vs 我的（波形）  │
│ [下一句]                      │
└──────────────────────────────┘
```

- 流程：听标准音 → 试跟读（可慢速）→ 录音 → 自动评测 → 查看评分与波形 → 下一句。
- 全部完成后：汇总页展示总分、各句得分、建议（< 80 分的句子提示重练）。

#### 6.4.3 评测技术方案（已确认：免费浏览器识别）

- **方案**：Web Speech API `SpeechRecognition` 浏览器原生语音识别，将识别文本与原文做匹配度评分（结合 Levenshtein 距离 + 关键音素近似），换算为「准确度 / 流利度 / 完整度 / 总分」。
- **成本**：免费，Chrome / Edge / Android WebView 可用；iOS Safari 录音需 HTTPS。
- **接口抽象**：前端统一依赖打分结构 `{ accuracy, fluency, completeness, overall }`，封装于 `evaluateApi`，后续如需升级评测能力，仅替换该实现，不影响页面。

---

### 6.5 影视切片预览与跟读 `/english/clips`

#### 6.5.1 定位

把电影/剧集/动画切片作为沉浸式学习素材：**先看片段 → 再逐句跟读**，兼顾兴趣与训练。

#### 6.5.2 页面结构

**① 切片库 `/english/clips`**
- 筛选：来源（美剧/电影/动画/纪录片）· 难度（简单/中等/困难）· 时长。
- 卡片：视频封面 + 剧名 + 切片名 + 时长 + 难度徽标 + 已学进度。
- 推荐位：今日推荐 1 个切片。

**② 切片详情 `/english/clips/:id`**（3 个 Tab）

| Tab | 内容 | 交互 |
|-----|------|------|
| 预览 | 视频播放器（原生 `<video>`）+ 中英双语字幕 + 生词标注 | 点击生词弹出释义气泡；可加入生词本；时间轴按台词分段 |
| 台词跟读 | 逐句列表：原音 → 跟读 → 评测（复用口语评测组件） | 复用 6.4 评测流程 |
| 词汇表 | 从台词提取的高频/生词列表 | 点击进单词详情；可加入学习计划 |

**③ 跟读模式流程**：逐句「播放原音 → 显示台词（英 + 中）→ 用户录音跟读 → 评分 → 下一句」，全部完成解锁「整段模仿」挑战（可选）。

#### 6.5.3 数据来源（已确认：网络找素材 → 本地下载 → 内置播放器）

| 来源 | 说明 |
|------|------|
| 网络素材 | 开发期从免费/开源渠道（CC 素材库、公开片段）获取影视/动画/纪录片片段 |
| 本地存储 | 视频或音频下载到本地 `backend/data/uploads/clips/`，`video_clips.video_url` 指向本地文件 |
| 内置播放器 | 原生 `<video>`/`<audio>` 播放本地文件，支持逐句跳转（`clip_lines.start_time/end_time`） |
| 字幕 | 随素材整理中英字幕，录入 `clip_lines` |
| 本地上传 | 用户可自行上传素材 + 手动录入台词（可选） |

> 版权提示：素材仅限个人学习使用，优先采用 CC0/开放授权内容。

---

## 7. 全局界面布局规范

### 7.1 整体框架（沿用现有）

```
┌──────────────────────────────────────┐
│ 顶栏 top-bar（52px，品牌渐变）          │
│  [≡] DayLoop   状态·版本·同步          │
├──────────────────────────────────────┤
│ 侧边栏 sidebar（280px 抽屉，分组导航）  │
│  │ 工作台 / 日程规划 / 知识管理 /      │
│  │ 英语学习 / 系统                     │
├──────────────────────────────────────┤
│ 内容区 main（max-width 480px 居中）     │
│  ← 各页面 router-view                  │
└──────────────────────────────────────┘
```

### 7.2 断点与适配

| 断点 | 布局 |
|------|------|
| < 540px | 单列移动布局，内容区 max-width 480px 居中 |
| 540 ~ 767px | 内容区 90vw |
| 768 ~ 1023px | max-width 720px |
| ≥ 1024px | max-width 860px，卡片可 2 列栅格 |

### 7.3 设计令牌（沿用现有 CSS 变量）

- 主色 `--primary #4f46e5`，语义色 `--success/--warning/--danger`，卡片圆角 `--radius 12px`。
- 学习模块可引入辅助色区分：单词（靛蓝）、场景（青绿）、口语（紫）、影视（橙红），用于模块入口图标与徽标。

### 7.4 通用组件清单

| 组件 | 用途 |
|------|------|
| `ProgressBar` | 任务/学习进度条 |
| `StreakBadge` | 连续打卡徽标 🔥N |
| `ScoreRing` | 口语评分环形图 |
| `WordCard` | 单词卡片（图片/释义/发音） |
| `AudioButton` | 发音/原音播放按钮 |
| `RecorderButton` | 录音按钮（按住/点击） |
| `VideoPlayer` | 切片视频播放器（带字幕行） |
| `EmptyState` | 空状态占位 |

### 7.5 主题系统（已确认：单一主题 + 接口预留）

- 当前版本使用现有单一配色主题（CSS 变量驱动）。
- 预留 `themeProvider` 接口（封装主题令牌读取/切换），为后续暗色模式/多主题扩展留好边界。
- 扩展点：新增主题令牌组（如 `--bg-dark` 等）+ `prefers-color-scheme` 或用户显式切换。

---

## 8. 页面路由规划

| 路由 path | name | 组件 | 归属模块 | 说明 |
|-----------|------|------|----------|------|
| `/login` | login | Login.vue | 系统 | 登录 |
| `/register` | register | Register.vue | 系统 | 注册 |
| `/profile` | profile | Profile.vue | 系统 | 个人设置 |
| `/` | home | Home.vue | 工作台 | 首页仪表盘（升级为工作台） |
| `/plan` | plan | DailyPlan.vue | 日程规划 | 今日计划 |
| `/review` | review | Review.vue | 日程规划 | 每日复盘 |
| `/summary` | summary | Summary.vue | 日程规划 | 周期性总结 |
| `/calendar` | calendar | Calendar.vue | 日程规划 | 日历预览 |
| `/achievements` | achievements | Achievements.vue | 日程规划 | 成果墙 |
| `/achievements/:id` | achievement-detail | AchievementDetail.vue | 日程规划 | 成果详情 |
| `/statistics` | statistics | Statistics.vue | 日程规划 | 统计看板 |
| `/templates` | templates | RecurringTemplates.vue | 日程规划 | 循环模板 |
| `/history` | history | History.vue | 系统 | 历史记录 |
| `/notes` | notes | Notes.vue | 知识管理 | 备忘录 |
| `/notes/:id` | note-detail | NoteDetail.vue | 知识管理 | 备忘录详情 |
| `/notes/new` | note-new | NoteDetail.vue | 知识管理 | 新建备忘录 |
| `/notes/categories` | note-categories | CategoryManage.vue | 知识管理 | 备忘录分类 |
| `/questions` | questions | Questions.vue | 知识管理 | 问题库 |
| `/questions/:id` | question-detail | QuestionDetail.vue | 知识管理 | 问题详情 |
| `/questions/new` | question-new | QuestionDetail.vue | 知识管理 | 新建问题 |
| `/questions/categories` | question-categories | CategoryManage.vue | 知识管理 | 问题分类 |
| `/english` | english | EnglishHome.vue | 英语学习 | 英语学习总览（新增） |
| `/english/words` | english-words | Words.vue | 英语学习 | 单词学习首页（新增） |
| `/english/words/learn` | english-words-learn | WordLearn.vue | 英语学习 | 新词学习会话（新增） |
| `/english/words/review` | english-words-review | WordReview.vue | 英语学习 | 复习会话（新增） |
| `/english/words/wrong` | english-words-wrong | WrongWords.vue | 英语学习 | 错词本（新增） |
| `/english/words/:id` | english-word-detail | WordDetail.vue | 英语学习 | 单词详情（新增） |
| `/english/wordbooks` | english-wordbooks | WordBooks.vue | 英语学习 | 词书管理（新增） |
| `/english/wordbooks/:id` | english-wordbook-detail | WordBookDetail.vue | 英语学习 | 词书详情/计划设置（新增） |
| `/english/scenarios` | english-scenarios | Scenarios.vue | 英语学习 | 场景英语列表（新增） |
| `/english/scenarios/:id` | english-scenario-detail | ScenarioDetail.vue | 英语学习 | 场景详情（新增） |
| `/english/speaking` | english-speaking | Speaking.vue | 英语学习 | 口语练习首页（新增） |
| `/english/speaking/:id` | english-speaking-detail | SpeakingPractice.vue | 英语学习 | 跟读练习页（新增） |
| `/english/clips` | english-clips | Clips.vue | 英语学习 | 影视切片库（新增） |
| `/english/clips/:id` | english-clip-detail | ClipDetail.vue | 英语学习 | 切片详情/跟读（新增） |

> 说明：现有路由全部保留（向后兼容），新增 `EnglishHome` 与各英语学习页面。学习会话类页面建议 `meta: { fullscreen: true }` 隐藏侧边栏干扰，全屏专注。

---

## 9. 功能展示与跳转逻辑

### 9.1 核心用户旅程

#### 旅程 A：早晨规划
```
打开工作台 → 首页看到今日任务 0/5
  → 点「去规划」→ /plan 今日计划
  → 添加任务/确认循环任务
  → 返回首页，进度条更新
```

#### 旅程 B：日常学习（单词）
```
首页点「去背单词」
  → /english/words 今日任务卡 X新词+Y复习
  → 点「开始学习」→ /english/words/learn（全屏）
  → 逐词学习卡片+巩固题
  → 完成结果页（正确率/错词）
  → 点「开始复习」→ /english/words/review
  → 复习完成 → 返回单词首页，打卡🔥+1
  → （可点错词进错词本 / 点单词进详情）
```

#### 旅程 C：沉浸学习（影视→口语）
```
英语首页点「影视切片」
  → /english/clips → 点今日推荐
  → /english/clips/:id → Tab「预览」看片段
  → 生词点击 → 加入生词本
  → Tab「台词跟读」→ 逐句跟读 → 口语评测
  → 完成后跳转 /english/speaking/:id 复用评分
```

#### 旅程 D：晚间复盘沉淀
```
首页点「每日复盘」
  → /review 看今日统计摘要
  → 追加今日学习小结（可选）
  → 保存
  → 首页成长数据卡更新
```

### 9.2 页面流转图（文字版）

```
Home 工作台
├── 今日概览[去规划] ──────────→ /plan
├── 今日概览[去背单词] ────────→ /english/words
├── 任务列表[勾选完成] ────────→ 完成弹窗(成果) ─→ /achievements
├── 学习速览[开始/去练/去看] ──→ /english/words · /english/speaking · /english/clips
├── 快捷宫格
│    ├── 今日计划 ──→ /plan
│    ├── 备忘录 ────→ /notes
│    ├── 问题库 ────→ /questions
│    ├── 单词背诵 ──→ /english/words
│    ├── 场景英语 ──→ /english/scenarios
│    ├── 口语练习 ──→ /english/speaking
│    └── 影视切片 ──→ /english/clips
└── 侧边栏 ──→ 各模块分组页面

单词背诵 /english/words
├── [开始学习] → /english/words/learn → 结果页 → [开始复习]→ /english/words/review → 单词首页
├── 词书卡 → /english/wordbooks → /english/wordbooks/:id（设计划）→ 学习
├── 错词本入口 → /english/words/wrong → 点击词 → /english/words/:id
└── 单词卡 → /english/words/:id → [生成备忘录笔记]→ /notes/new

场景英语 /english/scenarios
└── 场景卡 → /english/scenarios/:id
     ├── 对话跟读按钮 → /english/speaking/:id（带场景上下文）
     └── 场景词汇 → 词卡 → /english/words/:id → 加入计划

口语练习 /english/speaking
└── 话题卡 → /english/speaking/:id → 逐句跟读评测 → 汇总页 → 返回

影视切片 /english/clips
└── 切片卡 → /english/clips/:id
     ├── 预览 Tab → 生词 → 单词详情 → 加入生词本
     └── 台词跟读 Tab → 复用口语评测
```

### 9.3 关键跳转矩阵

| 来源 | 动作 | 目标 | 传参/状态 |
|------|------|------|-----------|
| 首页 | 去背单词 | words/learn | `?auto=1` 直接拉起会话 |
| 单词详情 | 生成笔记 | notes/new | 预填 title=单词，content=释义例句 |
| 单词详情 | 加入生词本 | （本地） | `word_id` |
| 场景对话 | 跟读 | speaking/:id | `scenario_id` 上下文 |
| 影视台词 | 跟读 | 同一页 Tab | `clip_id` + `line_id` |
| 口语结果 | 重新练习 | speaking/:id | 同话题 |
| 复盘页 | 学习小结 | 首页 | 更新成长数据 |

---

## 10. 数据模型设计

> 遵循现有 SQLite + snake_case 约定，全部表带 `user_id`。以下为新增表。

### 10.1 单词相关

**`word_books` 词书**

| 字段 | 类型 | 说明 |
|------|------|------|
| id | INTEGER PK | |
| name | TEXT | 词书名（如「四级核心」） |
| level | TEXT | 难度等级（beginner/intermediate/advanced） |
| description | TEXT | 简介 |
| cover_color | TEXT | 封面底色 |
| is_default | INTEGER | 是否内置 |
| user_id | INTEGER | 所属用户（0=系统内置） |
| created_at | TEXT | |

**`words` 单词**

| 字段 | 类型 | 说明 |
|------|------|------|
| id | INTEGER PK | |
| word | TEXT | 单词 |
| phonetic | TEXT | 音标（UK/US 合并或分字段） |
| pos | TEXT | 词性 |
| meaning | TEXT | 中文释义（可 JSON 多义） |
| example_en | TEXT | 例句 |
| example_cn | TEXT | 例句中文 |
| image_url | TEXT | 图片（可空，空则兜底样式） |
| audio_url | TEXT | 音频（可空，空则 TTS） |
| book_id | INTEGER | 所属词书 |
| created_at | TEXT | |

**`word_progress` 单词学习进度（每用户每词一条）**

| 字段 | 类型 | 说明 |
|------|------|------|
| id | INTEGER PK | |
| user_id | INTEGER | |
| word_id | INTEGER | |
| status | TEXT | new/learning/reviewing/mastered |
| stage | INTEGER | 遗忘曲线阶段 0~5 |
| correct_streak | INTEGER | 连续答对次数 |
| wrong_count | INTEGER | 累计答错 |
| last_review_at | TEXT | 上次复习时间 |
| next_review_at | TEXT | 下次复习时间（按阶段间隔计算） |
| UNIQUE(user_id, word_id) | | |

**`learning_logs` 学习流水（打卡/统计来源）**

| 字段 | 类型 | 说明 |
|------|------|------|
| id | INTEGER PK | |
| user_id | INTEGER | |
| date | TEXT | YYYY-MM-DD |
| type | TEXT | new/review/speaking/clip |
| word_id | INTEGER NULL | 单词学习时记录 |
| topic_id | INTEGER NULL | 口语/影视记录 |
| result | TEXT | correct/wrong/score |
| created_at | TEXT | |

**`wrong_words` 错词本**

| 字段 | 类型 | 说明 |
|------|------|------|
| id | INTEGER PK | |
| user_id | INTEGER | |
| word_id | INTEGER | |
| created_at | TEXT | |
| UNIQUE(user_id, word_id) | | |

**`study_sessions` 学习时长会话（计时统计来源）**

| 字段 | 类型 | 说明 |
|------|------|------|
| id | INTEGER PK | |
| user_id | INTEGER | |
| date | TEXT | YYYY-MM-DD |
| module | TEXT | words/scenarios/speaking/clips |
| start_time | TEXT | 会话开始时间 |
| end_time | TEXT | 会话结束时间 |
| duration_seconds | INTEGER | 有效时长（秒），离开页面/完成会话时结算 |
| created_at | TEXT | |

### 10.2 场景英语

**`scenarios` 场景**

| 字段 | 类型 | 说明 |
|------|------|------|
| id | INTEGER PK | |
| title | TEXT | 场景名 |
| category | TEXT | 生活/职场/旅行/购物/餐饮/医疗/社交/校园 |
| level | INTEGER | 难度 1~5 |
| icon | TEXT | emoji 图标 |
| description | TEXT | 场景介绍 |
| mastered | INTEGER | 是否已掌握（用户级可放进度表） |

**`scenario_lines` 场景对话行**

| 字段 | 类型 | 说明 |
|------|------|------|
| id | INTEGER PK | |
| scenario_id | INTEGER | |
| order | INTEGER | 顺序 |
| speaker | TEXT | 说话人（A/B 或角色名） |
| en_text | TEXT | 英文 |
| cn_text | TEXT | 中文 |
| audio_url | TEXT | 音频（可空） |

**`scenario_phrases` 关键句型**

| 字段 | 类型 | 说明 |
|------|------|------|
| id | INTEGER PK | |
| scenario_id | INTEGER | |
| phrase | TEXT | 句型结构 |
| meaning | TEXT | 用法说明 |
| example_en / example_cn | TEXT | 示例 |

**`scenario_quizzes` 场景测验**

| 字段 | 类型 | 说明 |
|------|------|------|
| id | INTEGER PK | |
| scenario_id | INTEGER | |
| question_en / question_cn | TEXT | 题干 |
| options | TEXT | JSON 数组（4 项） |
| answer_index | INTEGER | 正确项下标 |
| explanation | TEXT | 解析 |

### 10.3 口语练习

**`speaking_topics` 口语话题**

| 字段 | 类型 | 说明 |
|------|------|------|
| id | INTEGER PK | |
| title | TEXT | 话题名 |
| category | TEXT | 日常/职场/场景/影视 |
| level | INTEGER | 难度 |
| lines | TEXT | JSON：句子数组 [{ en, cn, audio_url }] |
| source_type | TEXT | topic/scenario/clip |
| source_id | INTEGER | 关联场景或切片 |

**`speaking_records` 口语练习记录**

| 字段 | 类型 | 说明 |
|------|------|------|
| id | INTEGER PK | |
| user_id | INTEGER | |
| topic_id | INTEGER | |
| line_index | INTEGER | 第几句 |
| audio_url | TEXT | 录音文件 |
| accuracy / fluency / completeness / overall | INTEGER | 0~100 分 |
| created_at | TEXT | |

### 10.4 影视切片

**`video_clips` 影视切片**

| 字段 | 类型 | 说明 |
|------|------|------|
| id | INTEGER PK | |
| title | TEXT | 切片名 |
| source | TEXT | 来源（美剧/电影/动画/纪录片） |
| cover_url | TEXT | 封面 |
| video_url | TEXT | 视频地址 |
| duration | INTEGER | 秒 |
| level | TEXT | 简单/中等/困难 |
| tags | TEXT | 逗号分隔 |
| description | TEXT | 简介 |
| user_id | INTEGER | 0=内置 |

**`clip_lines` 切片台词**

| 字段 | 类型 | 说明 |
|------|------|------|
| id | INTEGER PK | |
| clip_id | INTEGER | |
| order | INTEGER | |
| speaker | TEXT | |
| en_text / cn_text | TEXT | |
| start_time / end_time | REAL | 秒，用于字幕跳转 |

---

## 11. API 设计

> 统一前缀 `/api`，鉴权同现有 Bearer token。新增路由建议单独文件：`backend/src/routes/words.js`、`english.js`、`speaking.js`、`clips.js`，.NET 后端同步实现。

### 11.1 单词 `/api/words`

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/words/books` | 词书列表（含用户进度） |
| GET | `/api/words/books/:id` | 词书详情 + 每日目标设置 |
| PUT | `/api/words/books/:id/goal` | 设置每日新词目标 |
| GET | `/api/words/daily` | 今日任务：新词队列 + 复习队列 |
| POST | `/api/words/learn` | 提交新词学习结果 `{ word_id, result, stage }` |
| POST | `/api/words/review` | 提交复习结果 |
| GET | `/api/words/wrong` | 错词本列表 |
| DELETE | `/api/words/wrong/:wordId` | 移出错词本 |
| GET | `/api/words/:id` | 单词详情 |
| GET | `/api/words/books/:id/import` | CSV 导入模板说明 |
| POST | `/api/words/import` | CSV 导入自定义词书 |

### 11.2 场景 `/api/scenarios`

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/scenarios` | 场景列表（含掌握度） |
| GET | `/api/scenarios/:id` | 场景详情（对话/句型/词汇/测验） |
| POST | `/api/scenarios/:id/quiz` | 提交测验结果 |
| PUT | `/api/scenarios/:id/progress` | 更新掌握状态 |

### 11.3 口语 `/api/speaking`

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/speaking/topics` | 话题列表（含今日推荐） |
| GET | `/api/speaking/topics/:id` | 话题句子列表 |
| POST | `/api/speaking/evaluate` | 提交录音 URL + 原文，返回评分（MVP 为浏览器端评测，此接口预留） |
| POST | `/api/speaking/records` | 保存评分记录 |

### 11.4 影视 `/api/clips`

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/clips` | 切片列表（筛选/推荐） |
| GET | `/api/clips/:id` | 切片详情 + 台词列表 |
| POST | `/api/clips/import` | 上传视频 + 台词导入（MVP） |

### 11.5 学习统计 `/api/english`

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/english/dashboard` | 英语首页聚合：打卡、今日任务、各入口进度、今日学习时长 |
| GET | `/api/english/stats` | 学习统计：总词数/掌握率/连续天数/口语均分/影视时长/累计学习时长 |
| GET | `/api/english/streak` | 连续打卡天数 |
| POST | `/api/english/sessions` | 上报学习会话结束 `{ module, start_time, end_time, duration_seconds }` |
| GET | `/api/english/sessions` | 查询学习时长（今日/本周/累计） |

---

## 12. 技术方案与第三方依赖

### 12.1 语音（TTS / STT）

| 能力 | MVP 方案 | 说明 |
|------|----------|------|
| 单词/句子发音 | Web Speech API `speechSynthesis`（en-US 语音） | 免费，Chrome/Edge/Android 可用；无 audio_url 时自动降级 |
| 录音 | `MediaRecorder` API | 浏览器录音，输出 webm → 后端存储 |
| 口语评测 | 本地 `SpeechRecognition` 文本匹配评分 | 免费方案；`evaluateApi` 抽象预留 V2 替换 |

### 12.2 视频（已确认：本地素材 + 内置播放器）

| 场景 | 方案 |
|------|------|
| 素材 | 网络找免费/开源片段，下载到本地 `backend/data/uploads/clips/` |
| 播放 | 内置 `<video>` / `<audio>` 播放本地文件，按 `clip_lines` 时间轴逐句跳转 |
| 字幕 | 随素材整理中英字幕录入；支持纯音频素材（听力跟读场景） |

### 12.3 单词库数据（已确认：联网下载资源）

| 来源 | 说明 |
|------|------|
| 网络词书 | 开发期从开源资源（ECDICT 离线词库、公开词表）整理，随「词书库」提供下载导入 |
| 网络图片 | 开发期批量下载免费图源单词配图，随词书打包入库 |
| 内置词书 | 发行包内置《核心 500 词》（含图片），保证首屏可用 |
| CSV 导入 | `单词,音标,词性,释义,例句,例句中文,图片URL` |

### 12.4 前端新增依赖（候选）

| 依赖 | 用途 | 备注 |
|------|------|------|
| 无强依赖 | 原生 `SpeechSynthesis`/`MediaRecorder` 即可覆盖 MVP | 避免引入重量级 SDK |

### 12.5 目录/文件规划

```
frontend/src/
├── views/english/
│   ├── EnglishHome.vue
│   ├── words/（Words/WordLearn/WordReview/WordDetail/WrongWords/WordBooks/WordBookDetail）
│   ├── scenarios/（Scenarios/ScenarioDetail）
│   ├── speaking/（Speaking/SpeakingPractice）
│   └── clips/（Clips/ClipDetail）
├── components/english/（WordCard/AudioButton/RecorderButton/ScoreRing/VideoPlayer/StreakBadge）
├── api/english.ts（封装 11.5 节接口）
├── store/english.ts（学习会话状态：队列/进度/打卡）
└── types/english.ts

backend/src/routes/
├── words.js / scenarios.js / speaking.js / clips.js / english.js
```

> **实现顺序**：英语学习模块先以 .NET 栈实现（`backend-dotnet/` 路由 + `frontend-dotnet/` 页面），稳定后同步移植 Node 栈（`backend/` + `frontend/`）；两栈页面与 API 保持一致。

---

## 13. 版本规划

| 版本 | 范围 | 里程碑 |
|------|------|--------|
| **V3.0 MVP** | ① 信息架构重组（侧边栏分组 + 首页工作台 + 路由整理）；② 备忘录/问题库独立模块；③ 单词背诵（联网下载词书+图片 + 学习/复习/错词本 + TTS + 打卡）；④ 场景英语（内置 3 场景）；⑤ 口语跟读（Web Speech 免费评测）；⑥ 影视切片（本地素材 + 内置播放器 + 台词跟读）；⑦ 学习时长统计（学习会话计时）；⑧ 主题接口预留（当前单一配色）。**以 .NET 栈为主实现** | 可日常使用闭环 |
| **V3.1** | 词书 CSV 导入 + 自定义词书；场景扩展到 10+；影视素材扩充 | 内容扩充 |
| **V3.2** | 学习统计入统计看板；单词一键生成备忘录；Node.js 栈同步移植英语学习模块 | 跨模块整合 + 双栈对齐 |
| **V4.0** | 多主题模式（暗色/浅色切换）；AI 口语对话；可扩展评测能力（可选） | 体验升级 |

---

## 14. 非功能需求

| 类别 | 要求 |
|------|------|
| 性能 | 单词学习会话启动 < 1s；题库/台词接口 < 200ms；图片懒加载 |
| 离线 | 学习会话依赖浏览器能力，弱网可用；已加载视频切片支持缓存播放 |
| 数据 | 全部数据入 SQLite，支持 `/api/export/json` 统一导出；录音/图片存 `backend/data/uploads` |
| 隐私 | 录音与学习数据仅本机使用，不依赖第三方云服务；评测采用浏览器本地识别 |
| 兼容 | Chrome/Edge/Android WebView（Capacitor）优先；iOS Safari 支持 TTS，录音需 HTTPS |
| 主题 | 当前单一配色主题；通过 `themeProvider` 接口预留多主题/暗色模式扩展 |
| 扩展 | `evaluateApi`、`ttsProvider`、`themeProvider`、词库导入均做接口抽象，便于替换 |

---

## 15. 需求决策确认

| # | 问题 | 决策（已确认） |
|---|------|----------------|
| 1 | 图片/词书资源 | 从网络下载开源词书与单词配图，随「词书库」提供下载导入；开发期批量整理入库 |
| 2 | 口语评测 | 免费浏览器识别（Web Speech API 本地文本匹配评分），不接入付费云评测 |
| 3 | 影视素材 | 网络找免费素材，下载到本地 `backend/data/uploads/clips/`，内置 `<video>`/`<audio>` 播放器播放视频或音频 |
| 4 | Obsidian 同步 | 英语学习数据不同步 Obsidian |
| 5 | 双后端 | 保留双后端；先以 .NET（backend-dotnet + frontend-dotnet）为主实现，后续同步移植 Node |
| 6 | 学习时长统计 | 需要；通过学习会话计时（study_sessions）统计，展示于首页与学习统计 |
| 7 | 主题 | 当前单一配色主题，预留 `themeProvider` 主题接口，后续再扩展主题模式 |

---

*本文档为规划稿，评审通过后按第 13 节版本规划拆分开发任务。*
