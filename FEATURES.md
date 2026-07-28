# DayLoop 功能文档

> 版本: 2.2.0 | 技术栈: Vue 3 + TypeScript (前端), Express.js + SQLite / .NET Core + SQLite (后端), Vite (构建工具)
>
> **每次新增功能或修改后，请同步更新本文档。**

---

## 目录

1. [页面路由](#1-页面路由)
2. [后端 API 接口](#2-后端-api-接口)
3. [数据库结构](#3-数据库结构)
4. [前端类型定义](#4-前端类型定义)
5. [Obsidian 知识库同步](#5-obsidian-知识库同步)
   - 5.1 [工作原理](#51-工作原理)
   - 5.2 [同步目录结构](#52-同步目录结构)
   - 5.3 [合并与排序规则](#53-合并与排序规则)
   - 5.4 [成就图片支持](#54-成就图片支持)
   - 5.5 [同步控制](#55-同步控制)
   - 5.6 [API](#56-api)
6. [PWA 功能](#6-pwa-功能)
7. [部署与启动](#7-部署与启动)
8. [测试](#8-测试)

---

## 1. 页面路由

| 路径 | 名称 | 组件 | 功能 |
|------|------|------|------|
| `/login` | login | Login.vue | 用户登录页 |
| `/register` | register | Register.vue | 用户注册页 |
| `/profile` | profile | Profile.vue | 用户信息页（我的）：头像、用户名、注册时间、服务器/Obsidian 设置、退出登录 |
| `/` | home | Home.vue | 仪表盘：今日统计、快速建任务、导航卡片、服务器配置、数据导出 |
| `/plan` | plan | DailyPlan.vue | 每日计划：日期导航、计划内/外任务列表、增删改查；完成任务弹窗含成果记录（支持文字+图片上传）、同步到知识库开关 |
| `/review` | review | Review.vue | 每日复盘：自动统计摘要、手动复盘文本、保存 |
| `/history` | history | History.vue | 历史数据浏览：按日期查看任务和复盘 |
| `/achievements` | achievements | Achievements.vue | 成果墙：按分类筛选已完成任务及其成果 |
| `/achievements/:id` | achievement-detail | AchievementDetail.vue | 单个成果详情 |
| `/notes` | notes | Notes.vue | 备忘录列表：搜索、分类筛选、图片缩略图 |
| `/notes/:id` | note-detail | NoteDetail.vue | 查看/编辑备忘录 |
| `/notes/new` | note-new | NoteDetail.vue | 新建备忘录 |
| `/notes/categories` | note-categories | CategoryManage.vue | 管理备忘录分类 |
| `/statistics` | statistics | Statistics.vue | 全局统计看板 |
| `/templates` | templates | RecurringTemplates.vue | 循环任务模板管理（含同步开关） |

---

## 2. 后端 API 接口

基础路径: `/api` | Node.js 默认端口: 3001 | .NET 默认端口: 5000

> 两个后端共享同一个 SQLite 数据库。前端 `frontend/` 连接 Node.js 后端，`frontend-dotnet/` 连接 .NET 后端。
> 所有需要登录的 API 需在请求头携带 `Authorization: Bearer <token>`。未登录返回 401，前端自动跳转登录页。

### 2.1 认证 `/api/auth`

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/auth/register` | 注册新用户。必填: `username`(至少2字符), `password`(至少4字符)。返回 `{ token, user }` |
| POST | `/api/auth/login` | 用户登录。返回 JWT `{ token, user }`，token 有效期 30 天 |
| GET | `/api/auth/me` | 获取当前登录用户信息（需 Bearer token） |

### 2.2 系统

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/version` | 返回服务器版本、主机名、LAN IP、端口、ngrok 公网 URL |

### 2.3 任务 `/api/tasks`

| 方法 | 路径 | 参数 | 说明 |
|------|------|------|------|
| GET | `/api/tasks` | `?date=YYYY-MM-DD`, `?search=text` | 按日期/搜索词查询任务 |
| POST | `/api/tasks` | - | 创建任务。必填: `date`, `title`。标记 `is_recurring=true` 时自动创建循环模板 |
| PUT | `/api/tasks/:id` | - | 更新任务字段。支持 `sync_enabled`、`note_id` 关联备忘录、`is_recurring` 自动创建模板 |
| GET | `/api/tasks/:id` | - | 获取单个任务 |
| POST | `/api/tasks/:id/copy` | - | 复制任务到指定日期，保留所有字段 |
| DELETE | `/api/tasks/:id` | - | 删除任务 |

### 2.4 每日复盘 `/api/reviews`

| 方法 | 路径 | 参数 | 说明 |
|------|------|------|------|
| GET | `/api/reviews` | `?date=YYYY-MM-DD` | 获取指定日期的复盘（无则返回 null） |
| PUT | `/api/reviews/:date` | - | 创建或更新指定日期的复盘 |

### 2.5 循环任务模板 `/api/recurring`

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/recurring` | 列出所有循环任务模板 |
| POST | `/api/recurring` | 创建循环模板。支持 `sync_enabled`(默认 true) |
| PUT | `/api/recurring/:id` | 更新循环模板。支持 `sync_enabled` |
| DELETE | `/api/recurring/:id` | 删除循环模板 |
| POST | `/api/recurring/generate` | 手动为指定 `date` 生成任务（从模板继承 `sync_enabled`） |

> **自动生成机制**：
> - **创建/更新任务**时若标记为循环任务（`is_recurring=true`），自动创建对应循环模板（如果同名模板不存在）
> - 每天 **09:00** 通过 cron 自动为次日生成任务（仅生成 `recurring_enabled=true` 的模板，支持 daily/weekly 两种模式）
> - 手动调用 `POST /api/recurring/generate` 可为任意日期生成

### 2.6 成果 `/api/achievements`

| 方法 | 路径 | 参数 | 说明 |
|------|------|------|------|
| GET | `/api/achievements` | `?category=name` | 列出有成果记录的任务，可按分类筛选 |
| GET | `/api/achievements/categories` | - | 列出有成果记录的分类列表 |
| GET | `/api/achievements/:id` | - | 获取单个成果详情 |

### 2.7 备忘录 `/api/notes`

| 方法 | 路径 | 参数 | 说明 |
|------|------|------|------|
| GET | `/api/notes` | `?category=name`, `?search=text` | 列出备忘录，支持分类/搜索筛选，附带关联任务信息 |
| POST | `/api/notes` | - | 创建备忘录。必填: `title`。可选: `content`, `category`, `task_id`, `tags` |
| GET | `/api/notes/categories` | - | 获取合并后的分类列表 |
| POST | `/api/notes/categories` | - | 创建分类。重复返回 409 |
| DELETE | `/api/notes/categories/:name` | - | 删除分类 |
| GET | `/api/notes/:id` | - | 获取单个备忘录（含关联任务信息） |
| PUT | `/api/notes/:id` | - | 更新备忘录 |
| DELETE | `/api/notes/:id` | - | 删除备忘录，同时解除关联任务的 note_id |

### 2.8 图片上传 `/api/upload`

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/upload/image` | 接收 base64 dataUrl，保存到 `backend/data/uploads/`，返回公网 URL |

### 2.9 数据导出 `/api/export`

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/export/json` | 导出全部数据为 JSON 下载（含版本号、时间戳、任务、备忘录、复盘、循环模板） |

### 2.10 统计 `/api/stats`

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/stats` | 返回聚合统计：任务总数/已完成/进行中/已取消/计划内、完成率、备忘录数、复盘数、近12周周统计 |

### 2.11 Obsidian 设置 `/api/settings`

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/settings` | 获取所有设置（返回 JSON 对象） |
| PUT | `/api/settings` | 更新设置。Body: `{"key": "obsidian_vault_path", "value": "D:/path"}` |
| POST | `/api/settings/sync-all` | 全量同步：清空并重新生成所有 Obsidian 笔记 |

### 2.12 静态文件

| 路径 | 说明 |
|------|------|
| `/uploads/*` | 提供上传的图片（`backend/data/uploads/`） |
| `/*` (非 `/api`) | 提供前端 SPA 构建产物（`frontend/dist/`），兜底路由 |

---

## 3. 数据库结构

数据库: SQLite | 文件: `backend/data/dayloop.db` | WAL 模式

### 3.1 `users` 用户表

| 字段 | 类型 | 默认值 | 约束 | 说明 |
|------|------|--------|------|------|
| id | INTEGER | AUTOINCREMENT | PRIMARY KEY | 自增 ID |
| username | TEXT | NOT NULL | UNIQUE | 用户名 |
| password_hash | TEXT | NOT NULL | | bcrypt 哈希密码 |
| created_at | TEXT | `datetime('now','localtime')` | | 注册时间 |

### 3.2 `tasks` 任务表

| 字段 | 类型 | 默认值 | 约束 | 说明 |
|------|------|--------|------|------|
| id | INTEGER | AUTOINCREMENT | PRIMARY KEY | 自增 ID |
| date | TEXT | NOT NULL | | 日期 YYYY-MM-DD |
| title | TEXT | NOT NULL | | 任务标题 |
| start_time | TEXT | `''` | | 计划开始时间 HH:MM |
| end_time | TEXT | `''` | | 计划结束时间 HH:MM |
| planned_duration | INTEGER | 0 | | 计划时长（分钟） |
| actual_duration | INTEGER | NULL | | 实际时长（分钟） |
| actual_start | TEXT | NULL | | 实际开始时间 |
| actual_end | TEXT | NULL | | 实际结束时间 |
| status | TEXT | `'planned'` | | 状态: planned/in_progress/completed/cancelled |
| category | TEXT | `''` | | 分类 |
| priority | INTEGER | 2 | 1-3 | 优先级（1高/2中/3低） |
| note | TEXT | `''` | | 备注 |
| is_recurring | INTEGER | 0 | | 是否循环任务 |
| is_planned | INTEGER | 1 | | 是否计划内 |
| recurring_template_id | INTEGER | NULL | | 关联循环模板 ID |
| achievement | TEXT | `''` | | 成果记录 |
| note_id | INTEGER | NULL | | 关联备忘录 ID |
| tags | TEXT | `''` | | 逗号分隔标签 |
| user_id | INTEGER | 0 | | 所属用户 ID |
| sync_enabled | INTEGER | 1 | | 成果是否同步到 Obsidian 知识库 |
| created_at | TEXT | `datetime('now','localtime')` | | 创建时间 |
| updated_at | TEXT | `datetime('now','localtime')` | | 更新时间 |

### 3.3 `daily_reviews` 每日复盘表

| 字段 | 类型 | 默认值 | 约束 | 说明 |
|------|------|--------|------|------|
| id | INTEGER | AUTOINCREMENT | PRIMARY KEY | 自增 ID |
| date | TEXT | NOT NULL | UNIQUE | 日期 YYYY-MM-DD |
| content | TEXT | `''` | | 复盘内容 |
| user_id | INTEGER | 0 | | 所属用户 ID |
| created_at | TEXT | `datetime('now','localtime')` | | 创建时间 |
| updated_at | TEXT | `datetime('now','localtime')` | | 更新时间 |

### 3.4 `recurring_templates` 循环任务模板表

| 字段 | 类型 | 默认值 | 约束 | 说明 |
|------|------|--------|------|------|
| id | INTEGER | AUTOINCREMENT | PRIMARY KEY | 自增 ID |
| title | TEXT | NOT NULL | | 模板标题 |
| start_time | TEXT | `''` | | 默认开始时间 |
| end_time | TEXT | `''` | | 默认结束时间 |
| planned_duration | INTEGER | 0 | | 默认计划时长 |
| category | TEXT | `''` | | 默认分类 |
| priority | INTEGER | 2 | | 默认优先级 |
| note | TEXT | `''` | | 默认备注 |
| user_id | INTEGER | 0 | | 所属用户 ID |
| recurrence_type | TEXT | `'daily'` | | 重复类型: daily/weekly |
| recurrence_days | TEXT | `''` | | 每周重复日（逗号分隔 0=周日） |
| recurring_enabled | INTEGER | 1 | | 是否启用自动生成 |
| sync_enabled | INTEGER | 1 | | 生成的任务默认是否同步到知识库 |
| created_at | TEXT | `datetime('now','localtime')` | | 创建时间 |

### 3.5 `notes` 备忘录表

| 字段 | 类型 | 默认值 | 约束 | 说明 |
|------|------|--------|------|------|
| id | INTEGER | AUTOINCREMENT | PRIMARY KEY | 自增 ID |
| title | TEXT | NOT NULL | | 标题 |
| content | TEXT | `''` | | 内容（支持 Markdown 图片） |
| category | TEXT | `''` | | 分类 |
| tags | TEXT | `''` | | 逗号分隔标签 |
| task_id | INTEGER | NULL | | 关联任务 ID |
| user_id | INTEGER | 0 | | 所属用户 ID |
| created_at | TEXT | `datetime('now','localtime')` | | 创建时间 |
| updated_at | TEXT | `datetime('now','localtime')` | | 更新时间 |

### 3.6 `note_categories` 备忘录分类表

| 字段 | 类型 | 默认值 | 约束 | 说明 |
|------|------|--------|------|------|
| id | INTEGER | AUTOINCREMENT | PRIMARY KEY | 自增 ID |
| name | TEXT | NOT NULL | UNIQUE | 分类名称 |
| user_id | INTEGER | 0 | | 所属用户 ID |
| created_at | TEXT | `datetime('now','localtime')` | | 创建时间 |

### 3.7 `app_settings` 设置表

| 字段 | 类型 | 默认值 | 约束 | 说明 |
|------|------|--------|------|------|
| key | TEXT | NOT NULL | PRIMARY KEY | 设置键名（如 `obsidian_vault_path`） |
| value | TEXT | `''` | | 设置值 |

### 3.8 `note_task_links` 备忘录-任务关联表

| 字段 | 类型 | 默认值 | 约束 | 说明 |
|------|------|--------|------|------|
| note_id | INTEGER | NOT NULL | PRIMARY KEY (复合) | 备忘录 ID |
| task_id | INTEGER | NOT NULL | PRIMARY KEY (复合) | 任务 ID |

> 备忘录与任务是多对多关系，通过此中间表关联。前端 Note 类型中的 `linked_tasks` 数组即来自此表。

---

## 4. 前端类型定义

### `Task`
```typescript
interface Task {
  id: number
  date: string
  title: string
  start_time: string
  end_time: string
  planned_duration: number
  actual_duration: number | null
  actual_start: string | null
  actual_end: string | null
  status: 'planned' | 'in_progress' | 'completed' | 'cancelled'
  category: string
  priority: 1 | 2 | 3
  note: string
  is_recurring: boolean
  is_planned: boolean
  recurring_template_id: number | null
  achievement: string
  note_id: number | null
  sync_enabled: boolean
  tags: string
  created_at: string
  updated_at: string
}
```

### `DailyReview`
```typescript
interface DailyReview {
  id: number; date: string; content: string
  created_at: string; updated_at: string
}
```

### `RecurringTemplate`
```typescript
interface RecurringTemplate {
  id: number; title: string; start_time: string; end_time: string
  planned_duration: number; category: string; priority: number; note: string
  created_at: string
  recurrence_type: string; recurrence_days: string; recurring_enabled: boolean
  sync_enabled: boolean
}
```

### `Note`
```typescript
interface Note {
  id: number; title: string; content: string; category: string; tags: string
  task_id: number | null
  linked_tasks: Array<{ id: number; title: string; date: string; start_time: string; end_time: string; status: string; category: string }>
  created_at: string; updated_at: string
}
```

---

## 5. Obsidian 知识库同步

DayLoop 支持将备忘录、每日复盘、任务成果实时同步到本地 Obsidian  vault。

### 5.1 工作原理

- 在 Profile 页面配置 `obsidian_vault_path`（本地 Obsidian 仓库路径）
- 每次新增/修改/删除数据时自动触发增量同步
- 支持全量重新同步（`POST /api/settings/sync-all`）

### 5.2 同步目录结构

```
{obsidian_vault_path}/DayLoop/
├── 备忘录/               # 笔记同步
│   ├── 01-笔记标题.md    # 按创建时间排序编号
│   ├── 02-笔记标题.md
│   └── 读书笔记-书名.md  # 含《》的笔记合并为读书笔记
├── 每日复盘/             # 每日复盘同步
│   └── YYYY-MM-DD.md
└── 每日成果/             # 任务成果同步
    ├── 标题.md           # 多次出现的相同标题 → 合并文件，按日期分段
    ├── YYYY-MM-DD-标题.md # 单次出现的标题 → 独立文件
    └── 读书笔记-书名.md  # 含《》的成果合并为读书笔记
```

### 5.3 合并与排序规则

| 场景 | 行为 |
|------|------|
| 相同标题的成果出现多次 | 合并为一个文件（如 `每天跑步.md`），按 `## YYYY-MM-DD` 分段 |
| 任务/笔记标题含《》 | 提取书名，合并为 `读书笔记-书名.md` |
| "今日复盘" 任务 | 排除，不同步到知识库 |
| 备忘录排序 | 按 `created_at` 升序排列，文件名编号如 `01-标题.md`、`02-标题.md` |

### 5.4 成就图片支持
- 完成任务时，成果记录支持插入图片（点击"🖼️ 图片"按钮上传）
- 同步到 Obsidian 后，图片以 Markdown 格式 `![文件名](url)` 呈现
- 图片存储在 `backend/data/uploads/`

### 5.5 同步控制

- **任务级别**: 完成任务时，通过"同步到知识库"开关控制该条成果是否同步（`sync_enabled` 字段）
- **模板级别**: 循环模板新增"同步到知识库"开关，生成的任务自动继承该设置
- **前端显示**: 任务卡片上显示 🚫 不同步标记，编辑表单和完成弹窗均有开关

### 5.6 API

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/settings` | 获取所有设置（含 `obsidian_vault_path`） |
| PUT | `/api/settings` | 更新设置（如配置 vault 路径） |
| POST | `/api/settings/sync-all` | 清空并全量重新生成所有 Markdown 文件 |

---

## 6. PWA 功能

### 6.1 Service Worker (`frontend/public/sw.js`)
- **缓存策略**: 静态资源缓存优先，同源 GET 请求网络优先+缓存回退，API 请求永不缓存
- **安装事件**: 预缓存所有静态资源，立即 `skipWaiting()`
- **激活事件**: 清理旧缓存，接管所有客户端
- **消息处理**:
  - `SKIP_WAITING`: 强制激活等待中的 Service Worker
  - `SHOW_NOTIFICATION`: 创建系统通知（标题、正文、图标、震动、点击 URL）
- **通知点击**: 关闭通知，聚焦已有窗口或打开新窗口
- **缓存名称**: `dayloop-v2`

### 6.2 Web App Manifest (`frontend/public/manifest.json`)

| 字段 | 值 |
|------|-----|
| name | DayLoop - 每日计划 |
| short_name | DayLoop |
| display | standalone（全屏 PWA） |
| orientation | portrait |
| background_color | `#f5f5f5` |
| theme_color | `#4f46e5` |
| 图标 | 10 种尺寸: 48~512px |

### 6.3 iOS/Safari 支持
- `apple-mobile-web-app-capable: yes`
- `apple-mobile-web-app-status-bar-style: black-translucent`
- Apple touch icons: 152x152, 167x167, 180x180
- Viewport: `viewport-fit=cover`

### 6.4 任务提醒通知（App.vue）
- 启动时请求通知权限
- 每 60 秒检查今日任务，若有计划中/进行中的任务在 5 分钟内开始，发送系统通知
- 点击通知跳转到计划页面

### 6.5 版本更新检测（App.vue）
- 每 30 秒轮询 `/api/version`
- 版本号变化时显示黄色更新横幅
- 点击横幅触发 Service Worker 更新并刷新页面

---

## 7. 部署与启动

### 7.1 `start.cmd` 交互菜单

| 选项 | 功能 |
|------|------|
| 1 | 启动服务器（本地）— 构建前端 + 启动后端 localhost:3001 |
| 2 | 启动服务器 + LAN 访问 — 显示局域网 IP |
| 3 | 启动服务器 + 公网访问 — 同上 + ngrok 隧道 |
| 4 | 开发者模式 — 后端 --watch 热重载 + Vite 开发服务器 5173 |
| 5 | 构建 Android APK |
| 6 | 构建 iOS 项目（仅 macOS） |
| 7 | 退出 |

### 7.2 `scripts/deploy.cmd` 部署脚本
1. 构建前端生产版本
2. 生成 PWA 图标
3. 在新窗口中启动后端
4. 打印访问说明（本地、ngrok、Cloudflare Tunnel、端口转发）

### 7.3 `scripts/dev-watch.cmd` 开发模式
- 后端 `node --watch` 自动重启
- 每 3 秒检测前端文件变化，自动重新构建

### 7.4 双后端架构
- **Node.js 后端**: `backend/`（Express.js），默认端口 3001，提供 `frontend/` 构建产物
- **.NET 后端**: `backend-dotnet/`（ASP.NET Core），默认端口 5000，提供 `frontend-dotnet/` 构建产物
- 两个后端共享同一 SQLite 数据库，功能完全一致

### 7.5 Docker 部署
- `Dockerfile`: 基于 node:18-alpine，构建前端 + 后端生产镜像
- `docker-compose.yml`: 单服务，端口 3001，持久化 volume `dayloop-data`
- `docker/android-builder/Dockerfile`: Docker 内构建 Android APK

### 7.6 Android APK 构建
- `scripts/build-apk.cmd`: 本地构建（需 Android SDK）
- `scripts/docker-build-apk.cmd`: Docker 内构建（无需本地 SDK）
- 使用 Capacitor 打包，输出 `app-debug.apk`

### 7.7 iOS 构建
- `scripts/build-ipa.cmd`: 仅 macOS + Xcode 可用
- 使用 Capacitor 生成 iOS 项目，在 Xcode 中手动构建

---

## 8. 测试

- 文件: `tests/backend.test.js`
- 运行: `node tests/backend.test.js`
- 当前 52 个测试用例，覆盖认证、任务、复盘、循环模板、备忘录、统计、导出、成果、设置、Obsidian 同步
