# DayLoop 完整需求文档

> 版本 2.0.0 · 双后端（Node.js + .NET）· 双前端（Vue 3）
> 最后更新：2026-07-30

---

## 一、项目概述

DayLoop 是一款个人日程管理系统，融合了**任务管理、习惯追踪、每日复盘、笔记、问答知识库、周期性总结、Obsidian 知识库同步**等功能。

### 架构

- **双后端共享同一 SQLite 数据库**（`backend/data/dayloop.db`）
  - Node.js（Express + better-sqlite3），端口 3001
  - .NET（ASP.NET Core + Microsoft.Data.Sqlite），端口 5000
- **双前端完全独立但功能一致**
  - `frontend/` 对接 Node.js 后端
  - `frontend-dotnet/` 对接 .NET 后端
  - 均为 Vue 3 + TypeScript + Vite，构建产物由各自后端通过 `express.static` / `UseStaticFiles` 提供
- **部署**：后端通过 NSSM 注册为 Windows 服务，.NET 以 `dotnet run` 运行

---

## 二、用户认证（Login / Register / Profile）

### 页面

| 路由 | 组件 | 功能 |
|------|------|------|
| `/login` | `Login.vue` | 用户登录 |
| `/register` | `Register.vue` | 用户注册 |
| `/profile` | `Profile.vue` | 个人信息、修改密码、删除账户、服务器地址设置 |

### API

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/auth/register` | 注册（username≥2字符，password≥4字符） |
| POST | `/api/auth/login` | 登录，返回 JWT token（有效期30天） |
| GET | `/api/auth/me` | 获取当前用户信息 |
| PUT | `/api/auth/password` | 修改密码（需旧密码+新密码） |
| DELETE | `/api/auth/account` | 删除账户及所有数据（事务包裹） |

### 表结构

**`users`**: `id`, `username`, `password_hash` (bcrypt), `created_at`

### 逻辑要点

- JWT Bearer token 认证，所有 `/api/*` 路径均需认证（`/api/auth/login` 和 `/api/auth/register` 除外）
- 前端 `authHeaders()` 自动附加 `Authorization: Bearer <token>`
- 401 时自动跳转 `/login`
- 客户端可通过 `localStorage.setItem('dayloop_server_url', '<url>')` 配置服务器地址，支持远程连接

---

## 三、首页仪表盘（Home）

### 页面

`/` — `Home.vue`

### 功能

- 显示当天日期、星期
- 进度条：已完成任务数 / 总任务数 + 百分比
- 快速任务入口：标题 + 日期 + 时间，点击跳转到 DailyPlan 并预填
- 今日任务列表：显示当天所有任务，支持勾选完成
- 完成弹窗：勾选任务时弹出，记录：
  - 成果描述（支持图片上传转 base64）
  - 关联笔记
  - 实际开始/结束时间
  - 实际耗时（分钟）
  - 是否同步到 Obsidian
- 快速入口卡片：跳转到复盘页面和成果页面

---

## 四、今日计划（DailyPlan）

### 页面

`/plan` — `DailyPlan.vue` — **核心功能页面**

### API：任务 CRUD

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/tasks?date=YYYY-MM-DD&search=` | 查询某天任务（支持按标题/备注搜索） |
| GET | `/api/tasks/range?start=&end=` | 查询日期范围任务 |
| GET | `/api/tasks/:id` | 查询单个任务 |
| POST | `/api/tasks` | 创建任务 |
| PUT | `/api/tasks/:id` | 更新任务（用 COALESCE 实现局部更新） |
| DELETE | `/api/tasks/:id` | 删除单个任务 |
| DELETE | `/api/tasks/by-name/:title` | 删除所有同名任务（仅今天起未完成的） |
| POST | `/api/tasks/:id/copy` | 复制任务到指定日期（默认今天） |

### 表结构：tasks

```
id, date, title, start_time, end_time
planned_duration (INTEGER, 分钟)
actual_duration (INTEGER, 分钟, 可空)
actual_start, actual_end (TEXT, 可空)
status: 'planned' | 'in_progress' | 'completed' | 'cancelled'
category, priority (1高/2中/3低), note
is_recurring (BOOLEAN), is_planned (BOOLEAN)
recurring_template_id (INTEGER, FK → recurring_templates)
achievement, note_id (FK → notes)
sync_enabled (BOOLEAN, 默认true)
planned_days (INTEGER, 默认1)
overall_status: 'pending' | 'completed'
tags, user_id, created_at, updated_at
```

### 前端功能

- **日期导航**：前一天/后一天、回到今天按钮
- **分区视图**：计划内任务（is_planned=true）vs 计划外任务
- **创建/编辑弹窗**：标题、日期、开始/结束时间、计划时长（自动根据起止时间计算）、计划完成天数、分类（datalist 自动补全）、优先级(1-3)、备注、关联笔记、是否循环任务、是否计划内、是否同步
- **状态切换下拉框**：计划中 → 进行中 → 已完成 → 已取消
- **完成弹窗**：状态改为"已完成"时弹出，包含：
  - 实际起止时间 + 实际耗时（自动计算）
  - 成果记录（支持图片上传工具栏）
  - 任务总结（跨天任务的整体总结，可选，自动加载已有总结）
  - 整体任务已完成开关
  - 同步到知识库开关
- **复制任务**：选择日期复制
- **删除任务**：确认后删除
- **删除同名**：确认后删除所有同名未来未完成任务
- **已完成按钮禁用**：status=completed 的任务"删除"和"删除同名"按钮变灰

### 业务逻辑

1. 创建任务时勾选"循环任务" → 自动创建同标题的 `recurring_templates` 记录，并设置任务的 `recurring_template_id`
2. 完成任务时勾选"整体已完成" → 按标题找到循环模板 → 查询全部同名且 status=completed 的任务 → 统计不重复日期数 → 若小于模板 planned_days 则自动下调
3. 进入页面时自动触发循环任务生成（`POST /api/recurring/generate`）

---

## 五、循环模板（RecurringTemplates）

### 页面

`/templates` — `RecurringTemplates.vue`

### API

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/recurring` | 列表 |
| POST | `/api/recurring` | 创建 |
| PUT | `/api/recurring/:id` | 更新 |
| DELETE | `/api/recurring/:id` | 删除 |
| POST | `/api/recurring/generate` | 手动触发生成（指定日期） |

### 表结构：recurring_templates

```
id, title, start_time, end_time, planned_duration
category, priority, note, user_id, created_at
recurrence_type: 'daily' | 'weekly'
recurrence_days: 逗号分隔的星期数字（0=周日）
recurring_enabled (BOOLEAN, 默认true)
sync_enabled (BOOLEAN, 默认true)
planned_days (INTEGER, 默认1)
```

### 前端功能

- 按"每日"和"每周"分区显示
- 模板卡片显示：标题、时间、分类、优先级、时长、天数
- 启用/禁用开关
- 创建/编辑弹窗：标题、起止时间、计划时长、优先级、分类、重复方式（每日/每周）、每周选择日、备注、启用循环、同步开关、计划完成天数

### 自动生成逻辑（每日09:00定时任务）

```
每天 09:00（Node: node-cron / .NET: BackgroundService）：
  计算明天日期
  遍历所有启用的模板：
    如果是 weekly → 检查明天是否在 recurrence_days 中
    检查明天日期+模板组合是否已有任务 → 有则跳过
    检查模板 planned_days：
      SELECT COUNT(DISTINCT date) FROM tasks WHERE recurring_template_id = ?
      如果已有天数 >= planned_days → 跳过不再生成
    创建任务（is_recurring=1, overall_status='pending'）
```

---

## 六、日历视图（Calendar）

### 页面

`/calendar` — `Calendar.vue`

### 功能

- **三种视图**：周视图 / 月视图 / 季度视图
- **导航**：前后切换 + "今天"按钮
- **周视图**：7列格子，显示日期、空闲程度徽标、任务卡片（优先级颜色 + 状态图标）
- **月视图/季度视图**：格子中显示最多3个任务点 + "+N" 溢出提示
- **空闲计算**（`freeTime()`，客户端计算）：

```
以 [360, 1080] = 6:00~18:00 为12h活动窗口
过滤有起止时间的任务 → 转为分钟数 → 按开始时间排序
遍历合并重叠区间，累加 occupied
freeMin = 720 - occupied
分级：≥480min → 充裕 / 240~480 → 较多 / 120~240 → 适中 / 1~120 → 较紧 / 0 → 已满
```

- **图例**：优先级颜色（红/黄/蓝）+ 空闲等级说明

### API

`GET /api/tasks/range?start=&end=`

---

## 七、每日复盘（Review）

### 页面

`/review` — `Review.vue`

### API

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/reviews?date=YYYY-MM-DD` | 查询某天复盘 |
| PUT | `/api/reviews/:date` | 保存/更新复盘 |

### 表结构：daily_reviews

```
id, date (UNIQUE), content, tags, user_id, created_at, updated_at
```

### 前端功能

- 日期导航
- 统计卡片：计划内完成数/总数、完成率%、计划总时长、实际总时长、计划外任务数、时长差值
- 自动摘要（客户端生成）：当天所有任务的状态、时长、成果预览
- 自由输入文本域（含引导占位符）
- 保存时自动创建"今日复盘"任务（status=completed，achievement=复盘内容）

---

## 八、周期性总结（Summary）

### 页面

`/summary` — `Summary.vue`

### API

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/summaries?type=&period=` | 查询某期总结 |
| PUT | `/api/summaries/:type/:period` | 保存/更新总结（首次自动生成 AI 摘要） |
| POST | `/api/summaries/generate` | 重新生成 AI 摘要 |
| GET | `/api/summaries/list?type=` | 列出某类型的所有期 |

### 表结构：summaries

```
id, type ('weekly'|'monthly'|'quarterly'|'yearly')
period_key (如: 2026-W30, 2026-07, 2026-Q2, 2026)
content (用户手动输入), auto_summary (自动生成)
user_id, created_at, updated_at
```

### 自动摘要内容（generateAutoSummary）

- 概览：总任务、已完成、已取消、完成率
- 时长统计：计划 vs 实际（分钟）
- 分类统计：每个分类的 完成数/总数/百分比
- Top5 耗时任务

### 定时生成（每晚22:00）

周日 → 生成上周总结 / 月末 → 生成月度 / 季度末 → 季度 / 年底 → 年度

### 任务总结（TaskSummary）

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/task-summaries?title=` | 查询某任务的跨天总结 |
| PUT | `/api/task-summaries/:title` | 创建/更新任务总结 |

**表结构：task_summaries**: `id, title, content, user_id, created_at, updated_at`

---

## 九、统计（Statistics）

### 页面

`/statistics` — `Statistics.vue`

### API

`GET /api/stats` → 返回：

- `totalTasks, completedTasks, cancelledTasks, inProgressTasks, plannedTasks`
- `completionRate`（百分比）
- `totalNotes, totalReviews`
- `totalPlannedDuration, totalActualDuration`（分钟）
- `weeklyStats`：近12周各周的总任务数和完成数

### 前端展示

- 9个统计卡片网格
- 周趋势柱状图（纯CSS横条，显示每周完成比例）

---

## 十、笔记（Notes）

### 页面

| 路由 | 组件 | 功能 |
|------|------|------|
| `/notes` | `Notes.vue` | 列表/搜索/筛选 |
| `/notes/new` | `NoteDetail.vue` | 新建 |
| `/notes/:id` | `NoteDetail.vue` | 查看/编辑 |
| `/notes/categories` | `CategoryManage.vue` | 管理分类 |

### API

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/notes?category=&search=` | 列表（可筛选） |
| GET | `/api/notes/:id` | 详情（含关联任务） |
| POST | `/api/notes` | 创建（可关联任务） |
| PUT | `/api/notes/:id` | 更新 |
| DELETE | `/api/notes/:id` | 删除（清理关联） |
| GET | `/api/notes/categories` | 列出所有分类 |
| POST | `/api/notes/categories` | 创建分类 |
| DELETE | `/api/notes/categories/:name` | 删除分类 |

### 表结构

**notes**: `id, title, content, category, tags, task_id(旧), user_id, created_at, updated_at`
**note_categories**: `id, name(UNIQUE), user_id`
**note_task_links**（多对多关联表）: `note_id, task_id`

### 功能

- 搜索栏（300ms 防抖）
- 分类筛选按钮
- 分页（每页5条）
- 笔记卡片：标题、内容预览（剥离 Markdown 和图片）、图片缩略图(最多3张)、标签、关联任务
- 编辑页：图片上传工具栏 + 预览切换 + 相册删除
- 分类管理页：增删分类
- 笔记可关联多个任务（通过 junction table）

---

## 十一、问答知识库（Questions）

### 页面

| 路由 | 组件 | 功能 |
|------|------|------|
| `/questions` | `Questions.vue` | 列表/搜索/筛选 |
| `/questions/new` | `QuestionDetail.vue` | 新建 |
| `/questions/:id` | `QuestionDetail.vue` | 查看/编辑 |
| `/questions/categories` | `CategoryManage.vue` | 管理分类 |

### API

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/questions?category=&search=` | 列表 |
| GET | `/api/questions/:id` | 详情 |
| POST | `/api/questions` | 创建 |
| PUT | `/api/questions/:id` | 更新 |
| DELETE | `/api/questions/:id` | 删除 |
| GET/POST/DELETE | `/api/questions/categories` | 分类管理 |

### 表结构

**questions**: `id, title, content, answer, answer_source('self'|'ai'|'web'), category, tags, task_id(旧), user_id, created_at, updated_at`
**question_categories**: `id, name(UNIQUE), user_id`
**question_task_links**: `question_id, task_id`

### 功能

- 同笔记的布局：搜索、分类筛选、分页
- 答案来源徽标：self=蓝 / AI=粉 / web=绿
- 答案预览
- 标签显示为 `#tag` 标签

---

## 十二、成果展示（Achievements）

### 页面

| 路由 | 组件 |
|------|------|
| `/achievements` | `Achievements.vue` |
| `/achievements/:id` | `AchievementDetail.vue` |

### API

`GET /api/achievements?category=` — 查询所有有成果的任务（achievement 非空）
`GET /api/achievements/categories` — 成果分类列表
`GET /api/achievements/:id` — 单条成果详情

### 概念

成果不是独立表，而是 `tasks` 表中 `achievement` 字段不为空的任务。

### 功能

- 分类筛选
- 分页（每页5条）
- 成果卡片：日期、分类、标题、成果预览（渲染图片）
- 详情页：完整内容

---

## 十三、历史记录（History）

### 页面

`/history` — `History.vue`

### 功能

- 日期下拉选择器（从数据库所有有任务的日期填充）
- 统计行：任务总数 / 计划内/外/已完成
- 任务列表：时间徽标、状态徽标、计划/实际时长、成果预览
- 同时显示该日期的复盘内容（如有）

---

## 十四、分类管理（CategoryManage）

### 页面

`/notes/categories` 和 `/questions/categories` — 共享 `CategoryManage.vue`

### 功能

- 根据路由自动识别是笔记分类还是问答分类
- 新增分类：输入框 + 按钮
- 删除分类：确认后删除（不删除该分类下的内容）
- 已存在的分类显示为可移除标签

---

## 十五、图片上传

### API

`POST /api/upload/image` — 上传 base64 图片，返回 URL

### 逻辑

- 接受 `data:image/{format};base64,...` 格式
- 保存到 `backend/data/uploads/`，文件名 `{timestamp}-{random}.{ext}`
- 返回 URL 如 `/uploads/1234567890-abc.jpg`
- 用于：任务成果、笔记内容、问答内容

---

## 十六、数据导出

### API

`GET /api/export/json` — 导出全部用户数据为 JSON 下载

### 导出结构

```json
{ "version": "1.0", "exported_at": "...", "tasks": [...], "notes": [...], "reviews": [...], "templates": [...] }
```

---

## 十七、系统信息

### API

`GET /api/version` — 返回服务器版本、主机名、LAN IP、端口、公网地址

### 配置

- Node.js 默认端口 3001（可通过 `PORT` 环境变量修改）
- .NET 默认端口 5000（可通过 `PORT` 环境变量修改）
- 可选 ngrok 隧道（设置 `TUNNEL=true` + `NGROK_AUTH_TOKEN`）

---

## 十八、Obsidian 知识库同步

### 设置 API

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/settings` | 获取所有设置 |
| PUT | `/api/settings` | 更新设置（仅允许 obsidian_vault_path） |
| POST | `/api/settings/sync-all` | 全量同步 |

### 表结构：app_settings

`key TEXT PRIMARY KEY, value TEXT`

### 同步触发点

- 笔记创建/更新 → `syncNote()` → 全量重写笔记
- 复盘保存 → `syncReview()` → 全量重写复盘
- 任务完成/成果更新 → `syncAchievement()` → 全量重写成果

### 仓库文件结构

```
{vault_path}/DayLoop/
├── 备忘录/
│   ├── 01-title.md           (独立笔记，编号排序)
│   └── XX-读书笔记-{书名}.md  (读书笔记合并)
├── 每日复盘/
│   └── YYYY-MM-DD-每日复盘.md
├── 每日成果/
│   ├── YYYY-MM-DD-title.md       (单天任务)
│   ├── title.md                  (同名任务合并)
│   └── YYYY-MM-DD-读书笔记-{书名}.md
└── 图片/ (引用的图片复制到此，重写相对路径)
```

### 特性

- **读书检测**：标题含 `《书名》` 格式的任务/笔记识别为读书笔记，合并写入
- **同名合并**：同名任务合并为一个文件，按日期分段
- **图片复制**：`/uploads/...` 引用的图片复制到仓库，路径改为相对路径
- **YAML frontmatter**：每个文件包含日期、分类、标签、来源、类型等元数据
- **全量重写**：`sync-all` 删除并重建整个 `DayLoop/` 目录

---

## 十九、前端路由汇总

| 路由 | 页面 | 认证 |
|------|------|------|
| `/login` | 登录 | 游客 |
| `/register` | 注册 | 游客 |
| `/profile` | 个人信息 | 需要 |
| `/` | 首页仪表盘 | 需要 |
| `/plan` | 今日计划 | 需要 |
| `/review` | 每日复盘 | 需要 |
| `/history` | 历史记录 | 需要 |
| `/achievements` | 成果列表 | 需要 |
| `/achievements/:id` | 成果详情 | 需要 |
| `/notes` | 笔记列表 | 需要 |
| `/notes/:id` | 笔记详情/编辑 | 需要 |
| `/notes/new` | 新建笔记 | 需要 |
| `/notes/categories` | 笔记分类管理 | 需要 |
| `/statistics` | 统计 | 需要 |
| `/templates` | 循环模板 | 需要 |
| `/questions` | 问答列表 | 需要 |
| `/questions/categories` | 问答分类管理 | 需要 |
| `/questions/new` | 新建问答 | 需要 |
| `/questions/:id` | 问答详情/编辑 | 需要 |
| `/summary` | 周期性总结 | 需要 |
| `/calendar` | 日历视图 | 需要 |

---

## 二十、数据库完整表清单

| 表名 | 说明 | 备注 |
|------|------|------|
| `users` | 用户账号 | bcrypt 密码 |
| `tasks` | 任务（同时也是成果存储） | 核心表，字段最多 |
| `daily_reviews` | 每日复盘 | date UNIQUE |
| `recurring_templates` | 循环模板 | daily/weekly 两种 |
| `notes` | 笔记 | 支持图片 |
| `note_categories` | 笔记分类 | name UNIQUE |
| `note_task_links` | 笔记-任务关联 | 多对多 |
| `questions` | 问答知识库 | answer_source 三种 |
| `question_categories` | 问答分类 | name UNIQUE |
| `question_task_links` | 问答-任务关联 | 多对多 |
| `app_settings` | 系统设置 | key-value |
| `summaries` | 周期性总结 | 四种类型 |
| `task_summaries` | 任务跨天总结 | title 关联 |

---

## 二十一、近期新增功能汇总（2026-07）

### 7.1 计划完成天数（planned_days）

- `tasks` 表新增 `planned_days INTEGER DEFAULT 1`
- `recurring_templates` 表新增 `planned_days INTEGER DEFAULT 1`
- 任务表单可设置，循环模板表单可设置
- 自动生成循环任务时检查：`COUNT(DISTINCT date) < planned_days` 才生成

### 7.2 任务总结（task_summaries）

- 新建 `task_summaries` 表
- 创建/更新任务总结 API
- 完成弹窗中加载并保存

### 7.3 整体完成状态（overall_status）

- `tasks` 表新增 `overall_status TEXT DEFAULT 'pending'`
- 完成弹窗中"整体任务已完成"开关
- 确认完成时自动计算实际完成天数，下调模板的 `planned_days`
- 统计范围：same title + status=completed 的不重复日期数

### 7.4 空闲计算修正

- 窗口从 [0,720]（半夜到中午）改为 [360,1080]（6:00~18:00）

### 7.5 删除同名范围修正

- 只删今天起未完成的任务（`date >= today AND status != 'completed'`）

### 7.6 任务与模板关联

- 创建/编辑循环任务时，自动设置 `recurring_template_id`

### 7.7 已完成禁止删除

- status=completed 的任务，"删除"和"删除同名"按钮禁用
