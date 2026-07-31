# DayLoop 完整功能需求与实现文档

> 基于代码实现逆向生成，覆盖所有功能及具体实现逻辑。
> 版本: 2.2.0 | 最后更新: 2026-07-30

---

## 目录

1. [系统架构](#1-系统架构)
2. [用户认证](#2-用户认证)
3. [任务管理](#3-任务管理)
4. [每日复盘](#4-每日复盘)
5. [循环任务模板](#5-循环任务模板)
6. [备忘录](#6-备忘录)
7. [问答知识库](#7-问答知识库)
8. [成果展示](#8-成果展示)
9. [周期性总结](#9-周期性总结)
10. [任务跨天总结](#10-任务跨天总结)
11. [日历视图](#11-日历视图)
12. [统计看板](#12-统计看板)
13. [数据导出](#13-数据导出)
14. [图片上传](#14-图片上传)
15. [系统设置与版本](#15-系统设置与版本)
16. [Obsidian 知识库同步](#16-obsidian-知识库同步)
17. [定时任务](#17-定时任务)
18. [数据库完整结构](#18-数据库完整结构)
19. [前端路由汇总](#19-前端路由汇总)
20. [已知问题与技术债务](#20-已知问题与技术债务)

---

## 1. 系统架构

### 1.1 整体架构

| 组件 | 技术栈 | 端口 | 说明 |
|------|--------|------|------|
| 后端 (Node.js) | Express.js + better-sqlite3 | 3001 | 主后端实现，提供所有 API |
| 后端 (.NET) | ASP.NET Core + Microsoft.Data.Sqlite | 5000 | 备用后端实现，API 完全一致 |
| 前端 (frontend/) | Vue 3 + TypeScript + Vite | - | 连接 Node.js 后端 (proxy → 3001) |
| 前端 (frontend-dotnet/) | Vue 3 + TypeScript + Vite | - | 连接 .NET 后端 (proxy → 5000) |
| 数据库 | SQLite (WAL 模式) | - | 共享文件 `backend/data/dayloop.db` |

**关键约束**：两个后端共享同一 SQLite 数据库文件，功能完全一致。两个前端除代理目标外代码一致。

### 1.2 认证机制

- JWT Bearer token 认证
- Token 有效期 30 天
- 401 时前端自动跳转 `/login`
- Token 存储在 `localStorage`，请求时自动附加 `Authorization: Bearer <token>` 头
- 前端可通过 `localStorage.setItem('dayloop_server_url', url)` 配置自定义服务器地址

---

## 2. 用户认证

### 2.1 注册

- **接口**: `POST /api/auth/register`
- **参数**: `{ username, password }`
- **验证**: username ≥ 2 字符，password ≥ 4 字符，用户名唯一
- **实现**: 密码经 bcrypt hash 10 轮后存入 `users` 表，返回 JWT token + user 对象

### 2.2 登录

- **接口**: `POST /api/auth/login`
- **参数**: `{ username, password }`
- **实现**: 查询用户名，bcrypt 对比密码，生成 JWT token（含 userId, iat, exp）

### 2.3 获取当前用户

- **接口**: `GET /api/auth/me`
- **实现**: 从 JWT 解析 userId，查询 `users` 表返回 id, username, created_at

### 2.4 修改密码

- **接口**: `PUT /api/auth/password`
- **参数**: `{ oldPassword, newPassword }`
- **验证**: 新密码 ≥ 4 字符
- **实现**: 对比旧密码哈希 → 更新为新密码的 bcrypt 哈希

### 2.5 删除账户 ⚠️ 跨表操作

- **接口**: `DELETE /api/auth/account`
- **实现 (Node.js)**: 使用 SQLite 事务依次删除：
  ```sql
  DELETE FROM tasks WHERE user_id = ?
  DELETE FROM daily_reviews WHERE user_id = ?
  DELETE FROM recurring_templates WHERE user_id = ?
  DELETE FROM notes WHERE user_id = ?
  DELETE FROM note_categories WHERE user_id = ?
  DELETE FROM users WHERE id = ?
  ```
  注意：未删除 `questions`, `question_categories`, `summaries`, `task_summaries` 表的相关记录。.NET 实现依赖 `ON DELETE CASCADE` 外键。

---

## 3. 任务管理

### 3.1 核心表结构

```sql
tasks (
  id, date, title, start_time, end_time,
  planned_duration, actual_duration, actual_start, actual_end,
  status CHECK('planned','in_progress','completed','cancelled'),
  category, priority CHECK(1-3), note,
  is_recurring, is_planned,
  recurring_template_id, -- FK → recurring_templates.id
  achievement, note_id, -- FK → notes.id
  tags, user_id,
  sync_enabled, planned_days DEFAULT 1,
  overall_status DEFAULT 'pending' CHECK('pending','completed'),
  created_at, updated_at
)
```

### 3.2 任务 CRUD

| 操作 | 接口 | 说明 |
|------|------|------|
| 列表 | `GET /api/tasks?date=&search=` | 支持按日期 + 标题/备注模糊搜索 |
| 范围查询 | `GET /api/tasks/range?start=&end=` | 日历视图使用 |
| 单个查询 | `GET /api/tasks/:id` | - |
| 创建 | `POST /api/tasks` | 见下方特殊逻辑 |
| 更新 | `PUT /api/tasks/:id` | 使用 COALESCE 实现局部更新 |
| 复制 | `POST /api/tasks/:id/copy` | 见下方特殊逻辑 |
| 删除 | `DELETE /api/tasks/:id` | - |
| 批量删除 | `DELETE /api/tasks/by-name/:title` | 见下方特殊逻辑 |

### 3.3 创建任务特殊逻辑 ⚠️

**跨表操作：创建任务时的循环模板自动关联**

```
流程图：
1. INSERT INTO tasks (...)
2. IF is_recurring = true:
   a. SELECT id FROM recurring_templates WHERE title = ? AND user_id = ?  -- 查同名模板
   b. IF 不存在 → INSERT INTO recurring_templates (...)  -- 自动创建模板
   c. SELECT id FROM recurring_templates WHERE title = ? AND user_id = ?  -- 再查一次
   d. UPDATE tasks SET recurring_template_id = ? WHERE id = ?  -- 关联模板ID
3. IF note_id 有值 → UPDATE notes SET task_id = ? WHERE id = ?  -- 关联笔记
```

**前端创建表单字段**：
- 必填: `date`, `title`
- 可选: `start_time`, `end_time`, `planned_duration`（起止时间自动计算）、`category`, `priority(1-3)`, `note`, `is_recurring`, `is_planned`, `sync_enabled`, `note_id`, `planned_days`(默认1)

### 3.4 更新任务特殊逻辑 ⚠️

**跨表操作**：
1. 如果 `body` 中带 `is_recurring=true` → 自动创建同名循环模板（如不存在），并设置 `recurring_template_id`
2. 如果 `body` 中带 `note_id`：
   - 若旧 `note_id` 存在且不等于新值 → 清除旧 note 的 `task_id`
   - 若新 `note_id` 有值 → `UPDATE notes SET task_id = ?`
   - 若新 `note_id` 为 null → 清除旧 note 的 `task_id`
3. 每次更新后调用 `syncAchievement()` 触发 Obsidian 同步

**前端更新表单**：与创建共用同一表单，预填当前值。

### 3.5 复制任务特殊逻辑

- **接口**: `POST /api/tasks/:id/copy`
- **参数**: `{ date }`（可选，默认今天）
- **实现**: 查询原任务 → `INSERT INTO tasks` 保留 `title, start_time, end_time, planned_duration, category, priority, note, is_recurring, is_planned, note_id, planned_days, overall_status`，**不复制** `achievement, actual_duration, actual_start, actual_end, status`（status 为默认 `planned`）

### 3.6 批量删除特殊逻辑

- **接口**: `DELETE /api/tasks/by-name/:title`
- **实现**: 
  ```sql
  DELETE FROM tasks WHERE title = ? AND user_id = ? AND date >= ? AND status != 'completed'
  ```
  - 仅删除 **今天及以后** 的任务
  - 不删除 **已完成** 的任务
- **前端**: 调用前弹出确认对话框，显示删除数量

### 3.7 完成任务特殊逻辑 ⚠️（核心业务逻辑）

**前端流程**（`DailyPlan.vue` `updateStatus/confirmComplete`）：

```
用户点击"完成"按钮
  ↓
弹出完成弹窗，包含：
  ├── 实际开始时间（预填 start_time）
  ├── 实际结束时间
  ├── 实际耗时（自动计算）
  ├── 成果记录（支持图片上传转 base64，已有成果预填）
  ├── 同步到知识库开关（默认 true）
  ├── 任务总结（GET /api/task-summaries?title= 异步加载）
  └── ☐ 整体任务已完成 复选框
  ↓
用户确认 → PUT /api/tasks/:id 更新：
  ├── status = 'completed'
  ├── actual_start, actual_end, actual_duration
  ├── achievement
  ├── sync_enabled
  └── overall_status = 'completed'（如果勾选了复选框）
  ↓
如果任务总结内容非空 → PUT /api/task-summaries 保存总结
  ↓
如果勾选了"整体已完成" → 调整模板 planned_days：
  ├── GET /api/recurring → 查找同名模板
  ├── GET /api/tasks（全部任务）→ 筛选 title 相同且 status=completed 的任务
  ├── 计算不重复日期数 actualDays
  └── 若 actualDays > 0 且 < template.planned_days
      → PUT /api/recurring/:id 将 planned_days 下调为 actualDays
```

**状态流转**（仅前端约束，后端无校验）：
```
planned → in_progress → completed
     ↘         ↘       ↗
       cancelled ←---→ 任何状态均可取消
```

### 3.8 排序规则

任务列表按 `is_planned DESC, priority ASC, start_time ASC, id ASC` 排序。即：
1. 计划内（is_planned=1）排在计划外前面
2. 同计划类型内优先级高的排前面（1 > 2 > 3）
3. 同优先级按开始时间排序

### 3.9 已完成任务删除保护（前端）

- 当 `task.status === 'completed'` 时，删除按钮和批量删除按钮 `:disabled`
- 禁用态按钮以灰色显示（CSS）

---

## 4. 每日复盘

### 4.1 表结构

```sql
daily_reviews (
  id, date UNIQUE, content, tags DEFAULT '',
  user_id, created_at, updated_at
)
```

### 4.2 API

| 操作 | 接口 | 说明 |
|------|------|------|
| 查询 | `GET /api/reviews?date=` | 按日期查，无则返回 null |
| 保存 | `PUT /api/reviews/:date` | 参数 `{ content }`，创建或更新 |

### 4.3 保存复盘特殊逻辑 ⚠️ 跨表操作

```
PUT /api/reviews/:date
  ↓
1. INSERT OR UPDATE daily_reviews
  ↓
2. 跨表：创建/更新"今日复盘"任务
   a. SELECT id FROM tasks WHERE title='今日复盘' AND date=? AND user_id=?
   b. IF 存在 → UPDATE tasks SET status='completed', achievement=content
   c. IF 不存在 → INSERT INTO tasks (date, title, status, achievement, is_planned=0)
   ↓
3. 调用 syncReview(review) → Obsidian 同步
```

- "今日复盘"任务的 `date` 与复盘日期相同（非次日）
- `is_planned=0`（计划外）

---

## 5. 循环任务模板

### 5.1 表结构

```sql
recurring_templates (
  id, title, start_time, end_time, planned_duration,
  category, priority, note, user_id, created_at,
  recurrence_type DEFAULT 'daily' CHECK('daily','weekly'),
  recurrence_days DEFAULT '', -- 逗号分隔 0=周日
  recurring_enabled DEFAULT 1, sync_enabled DEFAULT 1,
  planned_days DEFAULT 1
)
```

### 5.2 API

| 操作 | 接口 | 说明 |
|------|------|------|
| 列表 | `GET /api/recurring` | - |
| 创建 | `POST /api/recurring` | 参数: title, mode, weekdays, planned_days, ... |
| 更新 | `PUT /api/recurring/:id` | COALESCE 局部更新 |
| 删除 | `DELETE /api/recurring/:id` | 不删除已生成的任务 |
| 手动生成 | `POST /api/recurring/generate` | 见下方 |

### 5.3 手动生成任务逻辑 ⚠️

```
POST /api/recurring/generate { date }
  ↓
获取所有模板 → 遍历每个模板：
  1. IF NOT recurring_enabled → skip
  2. IF recurrence_type='weekly' → 检查目标日期的星期是否在 recurrence_days 中
  3. SELECT id FROM tasks WHERE date=? AND recurring_template_id=?  -- 防重复
     IF 已存在 → skip
  4. 检查 planned_days 限制：
     SELECT COUNT(DISTINCT date) FROM tasks WHERE recurring_template_id=?
     IF cnt >= planned_days → skip（已达到计划天数）
  5. INSERT INTO tasks (date, title, ..., is_recurring=1, recurring_template_id, planned_days, overall_status='pending')
```

**前端触发**：进入今日计划页面时，若选中日期为今天 → `api.generateRecurringTasks(today)`

### 5.4 循环模式规则

| 类型 | behavior |
|------|----------|
| daily | 每天生成 |
| weekly | 仅 `weekdays` 中的星期生成（如 '1,3,5' = 周一三五） |

**星期映射**: `getDay()` 0=周日, 1=周一, ..., 6=周六

### 5.5 模板 UI 功能

- 卡片显示: 标题、时间、分类、优先级、时长、计划天数
- 启用/禁用开关 → 控制 `recurring_enabled`
- 创建/编辑弹窗: 标题、起止时间、计划时长、优先级、分类、重复类型、每周选择日、备注、同步开关、计划天数

---

## 6. 备忘录

### 6.1 表结构

```sql
notes (
  id, title, content DEFAULT '', category DEFAULT '',
  tags DEFAULT '', task_id, -- 旧的一对一关联
  user_id, created_at, updated_at
)

note_categories (
  id, name UNIQUE, user_id, created_at
)

note_task_links ( -- 多对多关联表
  note_id, task_id, PRIMARY KEY (note_id, task_id)
)
```

### 6.2 API

| 操作 | 接口 | 说明 |
|------|------|------|
| 列表 | `GET /api/notes?category=&search=` | 支持分类筛选 + 标题/内容模糊搜索 |
| 详情 | `GET /api/notes/:id` | 返回含 `linked_tasks` 数组 |
| 创建 | `POST /api/notes` | 见下方 |
| 更新 | `PUT /api/notes/:id` | 见下方 |
| 删除 | `DELETE /api/notes/:id` | 见下方 |
| 分类列表 | `GET /api/notes/categories` | 合并 notes.category + note_categories.name |
| 创建分类 | `POST /api/notes/categories` | |
| 删除分类 | `DELETE /api/notes/categories/:name` | 不删除该分类下的笔记 |

### 6.3 创建/更新特殊逻辑 ⚠️ 跨表操作

**创建笔记**：
```
1. INSERT INTO notes (title, content, category, tags)
2. IF task_ids 数组非空:
   → 遍历 task_ids:
      INSERT INTO note_task_links (note_id, task_id) -- 建立多对多关联
      UPDATE tasks SET note_id = ? WHERE id = ?  -- 反向更新任务（冗余字段）
3. 调用 syncNote() → Obsidian 同步
```

**更新笔记**：
```
1. UPDATE notes SET ...
2. IF task_ids 是数组（即使空数组）:
   → DELETE FROM note_task_links WHERE note_id = ?  -- 先删旧关联
   → 遍历新 task_ids: INSERT INTO note_task_links + UPDATE tasks SET note_id
3. 调用 syncNote() → Obsidian 同步
```

**删除笔记**：
```
1. SELECT task_id FROM note_task_links WHERE note_id = ? -- 查所有关联任务
2. 遍历: UPDATE tasks SET note_id = NULL WHERE id = ?  -- 清理冗余字段
3. DELETE FROM note_task_links WHERE note_id = ?
4. DELETE FROM notes WHERE id = ?
```

### 6.4 关联任务查询逻辑

`getLinkedTasks(noteId)` 三阶段降级：
1. 优先查 `note_task_links` 多对多关联表
2. 若无 → 查 `tasks.note_id` 一对一关联（旧方式）
3. 若无 → 查 `notes.task_id` 旧字段
4. 返回合并任务列表

### 6.5 标签筛选逻辑

- 前端传 `tag` 参数
- 后端将 `notes.tags` 按**空格**分割，逐个比较（Node.js 精确比较，.NET 大小写不敏感）

---

## 7. 问答知识库

### 7.1 表结构

```sql
questions (
  id, title, content, answer, category, tags,
  answer_source DEFAULT 'self' CHECK('self','ai','web'),
  task_id, -- 旧关联
  user_id, created_at, updated_at
)

question_categories (
  id, name UNIQUE, user_id, created_at
)

question_task_links (
  question_id, task_id, PRIMARY KEY (question_id, task_id)
)
```

### 7.2 API

与备忘录完全对称的操作（CRUD + categories + task links）。

| 操作 | 接口 | 说明 |
|------|------|------|
| 列表 | `GET /api/questions?category=&search=` | 支持分类 + 搜索 |
| 详情 | `GET /api/questions/:id` | 含 `linked_tasks` |
| 创建 | `POST /api/questions` | 含 `task_ids` 多对多关联 |
| 更新 | `PUT /api/questions/:id` | 含 `task_ids` 重设关联 |
| 删除 | `DELETE /api/questions/:id` | 先删关联表，再删自身 |
| 分类管理 | `GET/POST/DELETE /api/questions/categories` | 同备忘录 |

### 7.3 标签筛选逻辑

与备忘录相同（空格分割，精确匹配）。

### 7.4 答案来源徽标

前端显示：self=蓝 / ai=粉 / web=绿。

---

## 8. 成果展示

### 8.1 概念

成果不是独立表，而是 `tasks` 表中 `achievement` 字段**非空**的任务。

### 8.2 API

| 操作 | 接口 | 说明 |
|------|------|------|
| 列表 | `GET /api/achievements?year=` | 按年筛选，默认当年 |
| 分类列表 | `GET /api/achievements/categories` | 返回有成果的分类 |
| 详情 | `GET /api/achievements/:id` | 返回任务完整信息 |

### 8.3 实现逻辑

```sql
SELECT * FROM tasks WHERE user_id = ? AND achievement != '' AND achievement IS NOT NULL
AND strftime('%Y', date) = ? ORDER BY date DESC
```

### 8.4 前端显示

- 年筛选器（下拉框）
- 按日期分组展示
- 每条成果渲染图片（正则替换 `![alt](url)` 为 `<img>`）
- 详情页显示关联的笔记和问答

---

## 9. 周期性总结

### 9.1 表结构

```sql
summaries (
  id, type CHECK('weekly','monthly','quarterly','yearly'),
  period_key, -- 如 2026-W30, 2026-07, 2026-Q2, 2026
  content DEFAULT '', -- 用户手动输入
  auto_summary DEFAULT '', -- 系统自动生成
  user_id, created_at, updated_at
)
```

### 9.2 API

| 操作 | 接口 | 说明 |
|------|------|------|
| 查询 | `GET /api/summaries?type=&period=` | 无则返回 null |
| 保存 | `PUT /api/summaries/:type/:period` | body: `{ content }`，首次自动生成 AI 摘要 |
| 重新生成 | `POST /api/summaries/generate` | body: `{ type, period }`，仅更新 auto_summary |
| 列表 | `GET /api/summaries/list?type=` | 返回 period_key + updated_at |

### 9.3 保存总结特殊逻辑 ⚠️

```
PUT /api/summaries/:type/:period
  ↓
查询是否已存在：
  IF 存在 → UPDATE content, updated_at
  IF 不存在 → INSERT (type, period_key, content, auto_summary)
    其中 auto_summary 由 generateAutoSummary() 计算
```

### 9.4 自动摘要生成 (generateAutoSummary)

```
输入: userId, type, periodKey
  ↓
1. 调用 getPeriodDateRange() 计算起止日期
   - weekly: 解析年+周号 → 计算周一~周日
   - monthly: 解析年+月 → 当月首末
   - quarterly: 解析年+季度 → 季度首末
   - yearly: 解析年 → 1月1日~12月31日
  ↓
2. 查询该范围内所有任务
3. 计算统计：
   - 总任务数、已完成、已取消
   - 完成率 = round(completed / total * 100)
   - 总计划时长、总实际时长
   - 分类统计：每类的 完成数/总数/百分比/时长
   - Top5 耗时任务（actual_duration 排序）
4. 拼接 Markdown 格式摘要文本
```

### 9.5 前端 UI

- 四种类型标签切换：周/月/季度/年
- 上下导航切换周期
- 自动摘要显示区（read-only，Markdown 渲染）
- 手动输入文本域 + 保存按钮
- "重新生成"按钮 → 调用 `POST /api/summaries/generate`

---

## 10. 任务跨天总结

### 10.1 表结构

```sql
task_summaries (
  id, title, -- 任务标题
  content DEFAULT '',
  user_id, created_at, updated_at
)
```

**唯一约束**: (user_id, title)，即每个用户每个任务标题只有一个总结。

### 10.2 API

| 操作 | 接口 | 说明 |
|------|------|------|
| 查询 | `GET /api/task-summaries?title=` | 按标题查 |
| 保存 | `PUT /api/task-summaries/:title` | body: `{ content }`，upsert |

### 10.3 前端调用

- 完成任务弹窗中异步加载 `GET /api/task-summaries?title=X` 预填
- 确认完成时若总结内容非空 → `PUT /api/task-summaries` 保存

**⚠️ 注意**: 前端 API 调用为 `PUT /api/task-summaries`（body 含 title+content），而后端路由为 `PUT /api/task-summaries/:title`。当前实现中前端未拼接 title 到 URL，存在不匹配。

---

## 11. 日历视图

### 11.1 视图模式

| 模式 | 说明 |
|------|------|
| 周视图 | 7列格子，显示日期 + 空闲程度徽标 + 任务卡片（优先级颜色 + 状态图标） |
| 月视图 | 月历格子，每个格子最多3个任务点 + "+N" 溢出提示 |
| 季度视图 | 3个月连续显示，同月视图 |

### 11.2 空闲时间计算 (freeTime) ⚠️ 纯客户端计算

```
const DAY_START = 360, DAY_END = 1080  // 6:00~18:00 (720min 窗口)
  ↓
过滤当天有起止时间的任务 → 转为分钟数 → 按 start 排序
遍历合并重叠区间，累加 occupied 分钟
freeMin = 720 - occupied
分级：
  ≥480min → 充裕
  240~480 → 较多
  120~240 → 适中
  1~120   → 较紧
  0       → 已满
```

**图例**：优先级颜色（1高=红 / 2中=黄 / 3低=蓝）+ 空闲等级说明

### 11.3 导航

- 前后切换按钮
- "今天"按钮
- 切换时调用 `GET /api/tasks/range?start=&end=` 加载视图范围内的任务

### 11.4 天数显示

周视图: 7天（周一~周日）
月视图: 当月所有天
季度视图: 3个月的所有天

---

## 12. 统计看板

### 12.1 API

`GET /api/stats` → 返回:

```json
{
  "totalTasks": number,
  "completedTasks": number,
  "cancelledTasks": number,
  "inProgressTasks": number,
  "plannedTasks": number,
  "completionRate": number,  -- 百分比
  "totalNotes": number,
  "totalReviews": number,
  "totalPlannedDuration": number,
  "totalActualDuration": number,
  "weeklyStats": [
    { "week": "YYYY-MM-DD", "total": number, "completed": number }
  ]  -- 近12周
}
```

### 12.2 统计逻辑

```sql
-- 总任务数、各类状态数
SELECT COUNT(*) as total FROM tasks WHERE user_id = ?
SELECT COUNT(*) as completed FROM tasks WHERE user_id = ? AND status = 'completed'
-- 类似地统计 cancelled, in_progress, planned

-- 完成率
ROUND(CAST(completed AS REAL) / NULLIF(total, 0) * 100) -- SQL计算

-- 总时长
SELECT COALESCE(SUM(planned_duration), 0) FROM tasks WHERE user_id = ?
SELECT COALESCE(SUM(actual_duration), 0) FROM tasks WHERE user_id = ?

-- 近12周周统计（Node.js）
-- 计算12周前日期，按周分组（用 strftime('%W', date) 或用 week 计算）
SELECT date, COUNT(*) as total,
  SUM(CASE WHEN status = 'completed' THEN 1 ELSE 0 END) as completed
FROM tasks WHERE user_id = ? AND date >= ? GROUP BY week
```

### 12.3 前端展示

- 统计卡片网格（9个指标）
- 周趋势柱状图（纯 CSS）
- 时长格式化为 `Xh Ym`（`formatDuration` 函数）

---

## 13. 数据导出

### 13.1 API

`GET /api/export/json` → 一次性导出所有用户数据

### 13.2 导出数据范围

```json
{
  "version": "2.0.0",
  "exported_at": "YYYY-MM-DD HH:mm:ss",
  "tasks": [...],
  "reviews": [...],
  "notes": [...],
  "note_categories": [...],
  "questions": [...],
  "question_categories": [...],
  "recurring_templates": [...],
  "summaries": [...],
  "task_summaries": [...],
  "settings": [...]
}
```

---

## 14. 图片上传

### 14.1 API

`POST /api/upload/image` — 接收 base64 data URL，返回 `/uploads/xxx.jpg` URL

### 14.2 实现逻辑

```
输入: { dataUrl: "data:image/jpeg;base64,..." }
  ↓
1. 从 dataUrl 提取 format (jpeg/png/gif) 和 base64 数据
2. 生成文件名: {timestamp}-{random5hex}.{ext}
   - Node.js: Date.now() + 随机 5 位 hex
   - .NET: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 随机 5 位 hex
3. 保存到 backend/data/uploads/
4. 返回 { url: "/uploads/filename.ext" }
```

### 14.3 使用场景

- 任务成果记录（完成弹窗中的文本编辑器）
- 笔记内容（富文本）
- 问答内容

### 14.4 注意事项

- 无定期清理机制，图片持续累积
- 删除任务/笔记时不会删除关联图片文件

---

## 15. 系统设置与版本

### 15.1 设置 API

| 操作 | 接口 | 说明 |
|------|------|------|
| 查询 | `GET /api/settings` | 返回所有键值对 |
| 更新 | `PUT /api/settings` | body: `{ key, value }`，upsert |

### 15.2 已知设置键

| 键 | 用途 |
|---|------|
| `obsidian_vault_path` | Obsidian 仓库路径 |

### 15.3 版本信息

`GET /api/version` → 返回 `{ version, server, lanIP, port, publicUrl }`

---

## 16. Obsidian 知识库同步

### 16.1 同步模式

全量重写模式：每次同步时**删除**目标目录下所有文件，重新生成。

### 16.2 API

| 操作 | 接口 | 说明 |
|------|------|------|
| 全量同步 | `POST /api/settings/sync-all` | 清空并重建整个 DayLoop 目录 |

### 16.3 触发点

- 笔记创建/更新 → 自动调用 `syncNote()`
- 复盘保存 → 自动调用 `syncReview()`
- 任务更新（含 achievement 变化） → 自动调用 `syncAchievement()`
- 手动全量同步 → `POST /api/settings/sync-all`

### 16.4 目录结构

```
{vault_path}/DayLoop/
├── 备忘录/
│   ├── 01-title.md            -- 独立笔记，按创建时间编号
│   ├── 02-title.md
│   └── XX-读书笔记-书名.md    -- 含《》的笔记合并为读书笔记
├── 每日复盘/
│   └── YYYY-MM-DD-每日复盘.md
├── 每日成果/
│   ├── YYYY-MM-DD-title.md    -- 单次出现的成果
│   ├── title.md               -- 多次出现的同名成果合并
│   └── YYYY-MM-DD-读书笔记-书名.md
└── 图片/                      -- 引用的图片复制到此
```

### 16.5 笔记同步逻辑 ⚠️ 跨表操作

```
syncAllNotes():
  ↓
1. 获取 vault 路径，清空 备忘录/ 目录
2. 查询所有笔记 ORDER BY created_at ASC, id ASC
3. 分组：
   - 标题含《》→ 提取书名，归入 bookGroups Map
   - 无书名 → 归入 standaloneNotes
4. 写入独立笔记：
   - 文件名: {序号}-{slug}.md
   - 含 YAML frontmatter: created, updated, category, tags, source
5. 写入读书笔记（合并）：
   - 文件名: {序号}-读书笔记-{书名}.md
   - 每个笔记按 ## YYYY-MM-DD 分段
   - 含 YAML frontmatter: book, tags, type
6. 图片处理：
   - 正则匹配 ![](/uploads/xxx) → 复制图片到 vault/DayLoop/图片/
   - 路径重写为 ![](../图片/xxx)
```

### 16.6 复盘同步逻辑

```
syncAllReviews():
  ↓
1. 清空 每日复盘/ 目录
2. 查询所有复盘 ORDER BY date ASC
3. 每个复盘写入: {date}-每日复盘.md
   - YAML frontmatter: date, created, updated, type, source
```

### 16.7 成果同步逻辑 ⚠️ 跨表逻辑

```
syncAllAchievements():
  ↓
1. 查询：SELECT FROM tasks WHERE achievement != '' AND title != '今日复盘' AND sync_enabled != 0
2. 分组：
   - 标题含《》→ 读书笔记（合并）
   - 同名任务出现多次 → 合并文件，按日期分段
   - 单次任务 → 独立文件（文件名含日期前缀）
3. 单次任务格式：
   - 文件名: {date}-{slug}.md（含完整 YAML frontmatter）
   - 内容: # 标题 + > 成果 + 备注 + 时间
4. 多次任务合并格式：
   - 文件名: {slug}.md（无日期前缀）
   - 内容: ## YYYY-MM-DD 分段
5. 读书笔记统一位于 `读书笔记-{书名}.md`
```

### 16.8 slugify 规则

```javascript
text.replace(/[\\/:*?"<>|]/g, '')  // 移除非法字符
    .replace(/\s+/g, '-')            // 空格→连字符
    .replace(/-+/g, '-')             // 合并连续连字符
    .replace(/^-|-$/g, '')           // 去除首尾连字符
    .substring(0, 100)               // 最大100字符
```

---

## 17. 定时任务

### 17.1 循环任务生成 (09:00 每日)

**实现**（Node.js）：
```
cron.schedule('0 9 * * *', generateNextDayTasks)
  ↓
1. 计算明天日期
2. 查询所有 recurring_templates
3. 遍历模板：
   a. IF NOT recurring_enabled → skip
   b. IF weekly → 检查明天星期是否在 recurrence_days
   c. SELECT id FROM tasks WHERE date=? AND recurring_template_id=? → IF 存在 → skip
   d. 检查 planned_days: SELECT COUNT(DISTINCT date) FROM tasks WHERE recurring_template_id=?
      IF cnt >= planned_days → skip
   e. INSERT INTO tasks (date, title, ..., is_recurring=1, recurring_template_id, planned_days, overall_status='pending')
```

**.NET** 实现：`RecurringTaskService` 继承 `BackgroundService`，每 60 秒检查当前时间是否为 09:00。

### 17.2 周期性总结自动生成 (22:00 每日)

**实现**（Node.js）：
```
cron.schedule('0 22 * * *', autoGenerateSummaries)
  ↓
1. 判断今天是否处于周期边界：
   - 周日 (dow=0) → 上周周报（周号 = 计算上周所在的周）
   - 月末 (day === lastDayOfMonth) → 月报
   - 季末 (day === 季末且 month === 季末月) → 季报
   - 12月31日 → 年报
2. 遍历所有用户 → 遍历所有匹配的周期：
   - SELECT id FROM summaries WHERE user_id=? AND type=? AND period_key=?
   - IF 不存在 → 调用 generateAutoSummary() → INSERT INTO summaries
```

**.NET** 实现：`SummarySchedulerService` 继承 `BackgroundService`，每 60 秒检查当前时间是否为 22:00。

---

## 18. 数据库完整结构

### 18.1 全部 13 张表

| 表名 | 说明 | 核心字段 |
|------|------|----------|
| `users` | 用户账号 | id, username, password_hash, created_at |
| `tasks` | 任务（核心表） | id, date, title, status, priority, category, **recurring_template_id**, achievement, note_id, tags, sync_enabled, **planned_days**, **overall_status** |
| `daily_reviews` | 每日复盘 | id, date(UNIQUE), content, tags |
| `recurring_templates` | 循环模板 | id, title, recurrence_type, recurrence_days, recurring_enabled, sync_enabled, **planned_days** |
| `notes` | 备忘录 | id, title, content, category, tags, task_id |
| `note_categories` | 笔记分类 | id, name(UNIQUE) |
| `note_task_links` | 笔记-任务多对多 | (note_id, task_id) PK |
| `questions` | 问答 | id, title, content, answer, answer_source, category, tags, task_id |
| `question_categories` | 问答分类 | id, name(UNIQUE) |
| `question_task_links` | 问答-任务多对多 | (question_id, task_id) PK |
| `app_settings` | 系统设置 | key(PK), value |
| `summaries` | 周期性总结 | id, type, period_key, content, auto_summary |
| `task_summaries` | 任务跨天总结 | id, title(UNIQUE per user), content |

### 18.2 索引

`tasks` 表: `(user_id, date)` 索引（查询优化）

### 18.3 外键关系

```mermaid
tasks.recurring_template_id → recurring_templates.id
tasks.note_id → notes.id
notes.task_id → tasks.id (旧关联)
note_task_links.note_id → notes.id (CASCADE)
note_task_links.task_id → tasks.id (CASCADE)
question_task_links.question_id → questions.id (CASCADE)
question_task_links.task_id → tasks.id (CASCADE)
```

### 18.4 列迁移历史

部分字段通过 `ALTER TABLE ADD COLUMN` 逐步添加的（而非初始建表）。迁移字段列表：
`start_time`, `end_time`, `actual_start`, `actual_end`, `is_recurring`, `is_planned`, `recurring_template_id`, `achievement`, `note_id`, `tags`, `user_id`（多表）, `recurrence_type`, `recurrence_days`, `recurring_enabled`, `sync_enabled`, `planned_days`, `overall_status`, `daily_reviews.tags`

---

## 19. 前端路由汇总

| 路由 | 组件 | 功能 |
|------|------|------|
| `/` | Home.vue | 仪表盘：今日统计、快速建任务、快捷入口 |
| `/plan` | DailyPlan.vue | 每日计划：日期导航、计划内/外任务、完成弹窗、复制/删除 |
| `/review` | Review.vue | 每日复盘：自动统计 + 手动内容 |
| `/history` | History.vue | 历史浏览：按日期查任务和复盘 |
| `/achievements` | Achievements.vue | 成果墙：按年筛选 |
| `/achievements/:id` | AchievementDetail.vue | 成果详情 |
| `/notes` | Notes.vue | 笔记列表：搜索/分类筛选 |
| `/notes/:id` | NoteDetail.vue | 笔记编辑 |
| `/notes/new` | NoteDetail.vue | 新建笔记 |
| `/notes/categories` | CategoryManage.vue | 笔记分类管理 |
| `/statistics` | Statistics.vue | 统计看板 |
| `/templates` | RecurringTemplates.vue | 循环模板管理 |
| `/questions` | Questions.vue | 问答列表 |
| `/questions/:id` | QuestionDetail.vue | 问答编辑 |
| `/questions/new` | QuestionDetail.vue | 新建问答 |
| `/questions/categories` | CategoryManage.vue | 问答分类管理 |
| `/summary` | Summary.vue | 周期性总结 |
| `/calendar` | Calendar.vue | 日历视图 |
| `/profile` | Profile.vue | 个人信息/Obsidian 设置/删除账号 |
| `/login` | Login.vue | 登录 |
| `/register` | Register.vue | 注册 |

**认证守卫**: 所有路由除 `/login` 和 `/register` 外均需登录，未登录重定向到 `/login`。

---

## 20. 已知问题与技术债务

### 20.1 安全性问题

1. **.NET StatsController SQL 注入**: `tagStats` 查询使用字符串拼接而非参数化查询
2. **后端无输入长度校验**: 仅前端表单限制 title 50 字符，API 直接调用可创建超长字段

### 20.2 API 不一致

3. **任务搜索不一致**: Node.js 精确匹配（`name = ?`），.NET 模糊搜索（`LIKE '%' || ? || '%'`）
4. **任务总结 PUT 不匹配**: 前端 `PUT /api/task-summaries`（body 含 title），后端路由 `PUT /api/task-summaries/:title`（从 URL 取）
5. **重复维护成本**: 每次功能变更需同时修改 Node.js 和 .NET 两个后端 + 两个前端

### 20.3 数据完整性问题

6. **删除账号时未清理所有数据**: Node.js 实现的 `DELETE /api/auth/account` 仅删除 tasks, daily_reviews, recurring_templates, notes, note_categories，遗漏 questions, question_categories, summaries, task_summaries, app_settings 等
7. **上传图片无清理机制**: 删除笔记/任务时，关联的 `/uploads/` 图片文件仍保留

### 20.4 功能缺陷

8. **"今日复盘"任务日期重叠**: 复盘保存时创建的"今日复盘"任务 date = 复盘日期。若用户手动创建了同名任务，或修改过去日期的复盘，可能出现重复或意外行为
9. **前端-dotnet 为完整副本**: `frontend-dotnet/` 是 `frontend/` 的完整副本（仅 vite.config.ts 代理目标不同），维护负担大

### 20.5 其他

10. **无结构化日志**: Node.js 使用 `console.log`，.NET 使用 `ILogger<T>`，无请求日志中间件或错误追踪
11. **没有输入消毒**: 笔记/任务/问答的 Markdown 内容直接存储和展示，无 XSS 防护
