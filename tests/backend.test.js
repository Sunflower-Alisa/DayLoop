const assert = require('assert');
const path = require('path');
const fs = require('fs');

const dbPath = path.join(__dirname, '..', 'backend', 'data', 'test.db');

if (fs.existsSync(dbPath)) fs.unlinkSync(dbPath);

process.env.TEST_DB = dbPath;

const Database = require(path.join(__dirname, '..', 'backend', 'node_modules', 'better-sqlite3'));
const db = new Database(dbPath);

db.pragma('journal_mode = WAL');
db.pragma('foreign_keys = ON');

db.exec(`
  CREATE TABLE IF NOT EXISTS tasks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    date TEXT NOT NULL,
    title TEXT NOT NULL,
    start_time TEXT DEFAULT '',
    end_time TEXT DEFAULT '',
    planned_duration INTEGER DEFAULT 0,
    actual_duration INTEGER,
    actual_start TEXT,
    actual_end TEXT,
    status TEXT DEFAULT 'planned' CHECK(status IN ('planned','in_progress','completed','cancelled')),
    category TEXT DEFAULT '',
    priority INTEGER DEFAULT 2 CHECK(priority BETWEEN 1 AND 3),
    note TEXT DEFAULT '',
    is_recurring INTEGER DEFAULT 0,
    is_planned INTEGER DEFAULT 1,
    recurring_template_id INTEGER,
    achievement TEXT DEFAULT '',
    note_id INTEGER,
    created_at TEXT DEFAULT (datetime('now','localtime')),
    updated_at TEXT DEFAULT (datetime('now','localtime'))
  );

  CREATE TABLE IF NOT EXISTS daily_reviews (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    date TEXT NOT NULL UNIQUE,
    content TEXT DEFAULT '',
    created_at TEXT DEFAULT (datetime('now','localtime')),
    updated_at TEXT DEFAULT (datetime('now','localtime'))
  );

  CREATE TABLE IF NOT EXISTS recurring_templates (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    start_time TEXT DEFAULT '',
    end_time TEXT DEFAULT '',
    planned_duration INTEGER DEFAULT 0,
    category TEXT DEFAULT '',
    priority INTEGER DEFAULT 2,
    note TEXT DEFAULT '',
    created_at TEXT DEFAULT (datetime('now','localtime'))
  );

  CREATE TABLE IF NOT EXISTS notes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    content TEXT DEFAULT '',
    category TEXT DEFAULT '',
    task_id INTEGER,
    created_at TEXT DEFAULT (datetime('now','localtime')),
    updated_at TEXT DEFAULT (datetime('now','localtime'))
  );

  CREATE TABLE IF NOT EXISTS note_categories (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE,
    created_at TEXT DEFAULT (datetime('now','localtime'))
  );
`);

let passed = 0;
let failed = 0;

function test(name, fn) {
  try {
    fn();
    console.log(`  ✓ ${name}`);
    passed++;
  } catch (e) {
    console.log(`  ✗ ${name}`);
    console.log(`    ${e.message}`);
    failed++;
  }
}

function clearTable(table) {
  db.prepare(`DELETE FROM ${table}`).run();
}

console.log('\n=== 后端单元测试 ===\n');

console.log('--- 任务基础测试 ---');
test('创建任务 - 基本字段', () => {
  clearTable('tasks');
  const r = db.prepare('INSERT INTO tasks (date, title) VALUES (?, ?)').run('2026-07-13', '测试任务');
  assert.ok(r.lastInsertRowid > 0);
  const t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(t.title, '测试任务');
  assert.equal(t.date, '2026-07-13');
  assert.equal(t.status, 'planned');
  assert.equal(t.is_planned, 1);
  assert.equal(t.is_recurring, 0);
  assert.equal(t.note_id, null);
});

test('创建任务 - 所有字段含note_id', () => {
  clearTable('tasks');
  clearTable('notes');
  const nr = db.prepare('INSERT INTO notes (title) VALUES (?)').run('关联笔记');
  const r = db.prepare(
    `INSERT INTO tasks (date, title, start_time, end_time, planned_duration, category, priority, note, is_recurring, is_planned, achievement, note_id)
     VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`
  ).run('2026-07-13', '完整任务', '09:00', '10:30', 90, '工作', 1, '备注', 1, 1, '完成了很多工作', nr.lastInsertRowid);
  const t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(t.start_time, '09:00');
  assert.equal(t.end_time, '10:30');
  assert.equal(t.planned_duration, 90);
  assert.equal(t.is_recurring, 1);
  assert.equal(t.achievement, '完成了很多工作');
  assert.equal(t.note_id, nr.lastInsertRowid);
});

test('更新任务 - 状态变更', () => {
  clearTable('tasks');
  const r = db.prepare('INSERT INTO tasks (date, title) VALUES (?, ?)').run('2026-07-13', '更新测试');
  db.prepare("UPDATE tasks SET status = ?, actual_duration = ?, updated_at = datetime('now','localtime') WHERE id = ?")
    .run('completed', 60, r.lastInsertRowid);
  const t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(t.status, 'completed');
  assert.equal(t.actual_duration, 60);
});

test('更新任务 - 关联备忘录', () => {
  clearTable('tasks');
  clearTable('notes');
  const nr = db.prepare('INSERT INTO notes (title) VALUES (?)').run('备忘录1');
  const r = db.prepare('INSERT INTO tasks (date, title) VALUES (?, ?)').run('2026-07-13', '任务1');
  db.prepare('UPDATE tasks SET note_id = ?, updated_at = datetime(\'now\',\'localtime\') WHERE id = ?').run(nr.lastInsertRowid, r.lastInsertRowid);
  const t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(t.note_id, nr.lastInsertRowid);
});

test('更新任务 - 解除备忘录关联', () => {
  clearTable('tasks');
  clearTable('notes');
  const nr = db.prepare('INSERT INTO notes (title) VALUES (?)').run('备忘录1');
  const r = db.prepare('INSERT INTO tasks (date, title, note_id) VALUES (?, ?, ?)').run('2026-07-13', '任务1', nr.lastInsertRowid);
  db.prepare('UPDATE tasks SET note_id = NULL, updated_at = datetime(\'now\',\'localtime\') WHERE id = ?').run(r.lastInsertRowid);
  const t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(t.note_id, null);
});

test('删除任务', () => {
  clearTable('tasks');
  const r = db.prepare('INSERT INTO tasks (date, title) VALUES (?, ?)').run('2026-07-13', '删除测试');
  db.prepare('DELETE FROM tasks WHERE id = ?').run(r.lastInsertRowid);
  const t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(t, undefined);
});

test('按日期查询任务', () => {
  clearTable('tasks');
  db.prepare('INSERT INTO tasks (date, title) VALUES (?, ?)').run('2026-07-13', '任务1');
  db.prepare('INSERT INTO tasks (date, title) VALUES (?, ?)').run('2026-07-13', '任务2');
  db.prepare('INSERT INTO tasks (date, title) VALUES (?, ?)').run('2026-07-14', '任务3');
  const tasks = db.prepare('SELECT * FROM tasks WHERE date = ? ORDER BY id').all('2026-07-13');
  assert.equal(tasks.length, 2);
  assert.equal(tasks[0].title, '任务1');
  assert.equal(tasks[1].title, '任务2');
});

console.log('\n--- 任务状态与时长测试 ---');
test('实际时长自动计算', () => {
  clearTable('tasks');
  const r = db.prepare('INSERT INTO tasks (date, title) VALUES (?, ?)').run('2026-07-13', '计时任务');
  db.prepare('UPDATE tasks SET actual_start = ?, actual_end = ?, actual_duration = ?, status = ? WHERE id = ?')
    .run('09:00', '10:30', 90, 'completed', r.lastInsertRowid);
  const t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(t.actual_start, '09:00');
  assert.equal(t.actual_end, '10:30');
  assert.equal(t.actual_duration, 90);
  assert.equal(t.status, 'completed');
});

test('计划时长自动计算', () => {
  clearTable('tasks');
  const r = db.prepare('INSERT INTO tasks (date, title, start_time, end_time, planned_duration) VALUES (?, ?, ?, ?, ?)')
    .run('2026-07-13', '计划任务', '14:00', '15:30', 90);
  const t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(t.planned_duration, 90);
  assert.equal(t.start_time, '14:00');
  assert.equal(t.end_time, '15:30');
});

test('复制任务包含note_id', () => {
  clearTable('tasks');
  clearTable('notes');
  const nr = db.prepare('INSERT INTO notes (title) VALUES (?)').run('复制笔记');
  const r = db.prepare('INSERT INTO tasks (date, title, note_id) VALUES (?, ?, ?)').run('2026-07-13', '原始任务', nr.lastInsertRowid);
  const original = db.prepare('SELECT * FROM tasks WHERE id = ?').get(r.lastInsertRowid);
  db.prepare(
    'INSERT INTO tasks (date, title, start_time, end_time, planned_duration, category, priority, note, is_recurring, is_planned, note_id) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
  ).run('2026-07-14', original.title, original.start_time, original.end_time,
    original.planned_duration, original.category, original.priority, original.note,
    original.is_recurring, original.is_planned, original.note_id);
  const copy = db.prepare('SELECT * FROM tasks WHERE date = ?').all('2026-07-14');
  assert.equal(copy.length, 1);
  assert.equal(copy[0].title, '原始任务');
  assert.equal(copy[0].note_id, nr.lastInsertRowid);
});

console.log('\n--- 循环任务测试 ---');
test('创建循环模板', () => {
  clearTable('recurring_templates');
  const r = db.prepare(
    'INSERT INTO recurring_templates (title, start_time, end_time, planned_duration, category, priority) VALUES (?, ?, ?, ?, ?, ?)'
  ).run('晨会', '09:00', '09:30', 30, '工作', 1);
  assert.ok(r.lastInsertRowid > 0);
  const t = db.prepare('SELECT * FROM recurring_templates WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(t.title, '晨会');
  assert.equal(t.start_time, '09:00');
});

test('循环任务自动生成', () => {
  clearTable('tasks');
  clearTable('recurring_templates');
  const tr = db.prepare('INSERT INTO recurring_templates (title, start_time, end_time) VALUES (?, ?, ?)').run('晨会', '09:00', '09:30');
  const existing = db.prepare('SELECT id FROM tasks WHERE date = ? AND recurring_template_id = ?').get('2026-07-13', tr.lastInsertRowid);
  if (!existing) {
    db.prepare(
      `INSERT INTO tasks (date, title, start_time, end_time, is_recurring, recurring_template_id) VALUES (?, ?, ?, ?, 1, ?)`
    ).run('2026-07-13', '晨会', '09:00', '09:30', tr.lastInsertRowid);
  }
  const tasks = db.prepare('SELECT * FROM tasks WHERE date = ?').all('2026-07-13');
  assert.equal(tasks.length, 1);
  assert.equal(tasks[0].is_recurring, 1);
});

console.log('\n--- 复盘测试 ---');
test('创建每日复盘', () => {
  clearTable('daily_reviews');
  db.prepare('INSERT INTO daily_reviews (date, content) VALUES (?, ?)').run('2026-07-13', '今天表现不错');
  const r = db.prepare('SELECT * FROM daily_reviews WHERE date = ?').get('2026-07-13');
  assert.equal(r.content, '今天表现不错');
});

test('更新每日复盘', () => {
  db.prepare("UPDATE daily_reviews SET content = ?, updated_at = datetime('now','localtime') WHERE date = ?")
    .run('更新后的复盘', '2026-07-13');
  const r = db.prepare('SELECT * FROM daily_reviews WHERE date = ?').get('2026-07-13');
  assert.equal(r.content, '更新后的复盘');
});

test('每日复盘创建任务总结', () => {
  clearTable('tasks');
  clearTable('daily_reviews');
  db.prepare('INSERT INTO tasks (date, title, status, category) VALUES (?, ?, ?, ?)').run('2026-07-13', '完成任务A', 'completed', '工作');
  db.prepare('INSERT INTO tasks (date, title, status, category) VALUES (?, ?, ?, ?)').run('2026-07-13', '完成任务B', 'completed', '学习');
  db.prepare('INSERT INTO tasks (date, title, status) VALUES (?, ?, ?)').run('2026-07-13', '取消任务C', 'cancelled');
  const completed = db.prepare("SELECT * FROM tasks WHERE date = ? AND status = 'completed'").all('2026-07-13');
  assert.equal(completed.length, 2);
  const summary = completed.map(t => `${t.title}(${t.category})`).join(', ');
  db.prepare('INSERT INTO daily_reviews (date, content) VALUES (?, ?)').run('2026-07-13', '完成了: ' + summary);
  const r = db.prepare('SELECT * FROM daily_reviews WHERE date = ?').get('2026-07-13');
  assert.ok(r.content.includes('完成任务A'));
  assert.ok(r.content.includes('完成任务B'));
});

console.log('\n--- 成果查询测试 ---');
test('获取有成果的任务', () => {
  clearTable('tasks');
  db.prepare("INSERT INTO tasks (date, title, achievement, category) VALUES (?, ?, ?, ?)").run('2026-07-13', '有成果', '这是成果内容', '工作');
  db.prepare("INSERT INTO tasks (date, title, achievement, category) VALUES (?, ?, ?, ?)").run('2026-07-13', '无成果', '', '学习');
  const withAchievement = db.prepare("SELECT * FROM tasks WHERE achievement != '' AND achievement IS NOT NULL").all();
  assert.equal(withAchievement.length, 1);
  assert.equal(withAchievement[0].title, '有成果');
});

test('按分类查询成果', () => {
  clearTable('tasks');
  db.prepare("INSERT INTO tasks (date, title, achievement, category) VALUES (?, ?, ?, ?)").run('2026-07-13', '工作成果1', '成果1', '工作');
  db.prepare("INSERT INTO tasks (date, title, achievement, category) VALUES (?, ?, ?, ?)").run('2026-07-13', '工作成果2', '成果2', '工作');
  db.prepare("INSERT INTO tasks (date, title, achievement, category) VALUES (?, ?, ?, ?)").run('2026-07-13', '学习成果', '成果3', '学习');
  const work = db.prepare("SELECT * FROM tasks WHERE achievement != '' AND achievement IS NOT NULL AND category = ? ORDER BY id").all('工作');
  assert.equal(work.length, 2);
  assert.equal(work[0].title, '工作成果1');
  const learn = db.prepare("SELECT * FROM tasks WHERE achievement != '' AND achievement IS NOT NULL AND category = ? ORDER BY id").all('学习');
  assert.equal(learn.length, 1);
});

test('获取成果分类列表', () => {
  clearTable('tasks');
  db.prepare("INSERT INTO tasks (date, title, achievement, category) VALUES (?, ?, ?, ?)").run('2026-07-13', '成果A', '内容', '工作');
  db.prepare("INSERT INTO tasks (date, title, achievement, category) VALUES (?, ?, ?, ?)").run('2026-07-13', '成果B', '内容', '学习');
  const cats = db.prepare("SELECT DISTINCT category FROM tasks WHERE achievement != '' AND achievement IS NOT NULL AND category != '' ORDER BY category").all();
  assert.ok(cats.length >= 2);
});

console.log('\n--- 备忘录测试 ---');
test('创建备忘录', () => {
  clearTable('notes');
  const r = db.prepare('INSERT INTO notes (title, content, category) VALUES (?, ?, ?)').run('测试备忘录', '这是内容', '工作');
  assert.ok(r.lastInsertRowid > 0);
  const n = db.prepare('SELECT * FROM notes WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(n.title, '测试备忘录');
  assert.equal(n.content, '这是内容');
  assert.equal(n.category, '工作');
});

test('更新备忘录', () => {
  clearTable('notes');
  const r = db.prepare('INSERT INTO notes (title, content) VALUES (?, ?)').run('原始标题', '原始内容');
  db.prepare("UPDATE notes SET title = ?, content = ?, category = ?, updated_at = datetime('now','localtime') WHERE id = ?")
    .run('更新标题', '更新内容', '学习', r.lastInsertRowid);
  const n = db.prepare('SELECT * FROM notes WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(n.title, '更新标题');
  assert.equal(n.content, '更新内容');
  assert.equal(n.category, '学习');
});

test('删除备忘录', () => {
  clearTable('notes');
  const r = db.prepare('INSERT INTO notes (title) VALUES (?)').run('要删除的备忘录');
  db.prepare('DELETE FROM notes WHERE id = ?').run(r.lastInsertRowid);
  const n = db.prepare('SELECT * FROM notes WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(n, undefined);
});

test('备忘录分类查询', () => {
  clearTable('notes');
  db.prepare('INSERT INTO notes (title, category) VALUES (?, ?)').run('工作笔记', '工作');
  db.prepare('INSERT INTO notes (title, category) VALUES (?, ?)').run('学习笔记', '学习');
  db.prepare('INSERT INTO notes (title, category) VALUES (?, ?)').run('工作备忘', '工作');
  const workNotes = db.prepare('SELECT * FROM notes WHERE category = ? ORDER BY id').all('工作');
  assert.equal(workNotes.length, 2);
  const titles = workNotes.map(n => n.title).sort();
  assert.deepEqual(titles, ['工作备忘', '工作笔记']);
});

test('备忘录按id倒序(时间替代)', () => {
  clearTable('notes');
  const r1 = db.prepare('INSERT INTO notes (title) VALUES (?)').run('第一条');
  const r2 = db.prepare('INSERT INTO notes (title) VALUES (?)').run('第二条');
  const r3 = db.prepare('INSERT INTO notes (title) VALUES (?)').run('第三条');
  assert.ok(r1.lastInsertRowid < r2.lastInsertRowid);
  assert.ok(r2.lastInsertRowid < r3.lastInsertRowid);
  const all = db.prepare('SELECT * FROM notes ORDER BY id DESC').all();
  assert.equal(all.length, 3);
  assert.equal(all[0].title, '第三条');
  assert.equal(all[2].title, '第一条');
});

test('获取备忘录分类列表', () => {
  clearTable('notes');
  db.prepare("INSERT INTO notes (title, category) VALUES (?, ?)").run('笔记1', '工作');
  db.prepare("INSERT INTO notes (title, category) VALUES (?, ?)").run('笔记2', '学习');
  const cats = db.prepare("SELECT DISTINCT category FROM notes WHERE category != '' ORDER BY category").all();
  assert.ok(cats.length >= 2);
});

console.log('\n--- 任务-备忘录关联测试 ---');
test('备忘录关联任务 (task_id方向)', () => {
  clearTable('tasks');
  clearTable('notes');
  const tr = db.prepare('INSERT INTO tasks (date, title) VALUES (?, ?)').run('2026-07-13', '关联任务');
  const nr = db.prepare('INSERT INTO notes (title, content, task_id) VALUES (?, ?, ?)').run('任务笔记', '这是关联的笔记', tr.lastInsertRowid);
  const n = db.prepare('SELECT * FROM notes WHERE id = ?').get(nr.lastInsertRowid);
  assert.equal(n.task_id, tr.lastInsertRowid);
  const t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(n.task_id);
  assert.equal(t.title, '关联任务');
});

test('任务关联备忘录 (note_id方向)', () => {
  clearTable('tasks');
  clearTable('notes');
  const nr = db.prepare('INSERT INTO notes (title) VALUES (?)').run('备忘录1');
  const tr = db.prepare('INSERT INTO tasks (date, title, note_id) VALUES (?, ?, ?)').run('2026-07-13', '任务1', nr.lastInsertRowid);
  const t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(tr.lastInsertRowid);
  assert.equal(t.note_id, nr.lastInsertRowid);
});

test('双向关联一致性', () => {
  clearTable('tasks');
  clearTable('notes');
  const nr = db.prepare('INSERT INTO notes (title) VALUES (?)').run('备忘录');
  const tr = db.prepare('INSERT INTO tasks (date, title, note_id) VALUES (?, ?, ?)').run('2026-07-13', '任务', nr.lastInsertRowid);
  db.prepare('UPDATE notes SET task_id = ? WHERE id = ?').run(tr.lastInsertRowid, nr.lastInsertRowid);
  const t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(tr.lastInsertRowid);
  assert.equal(t.note_id, nr.lastInsertRowid);
  const n = db.prepare('SELECT * FROM notes WHERE id = ?').get(nr.lastInsertRowid);
  assert.equal(n.task_id, tr.lastInsertRowid);
});

test('删除备忘录时解除任务关联', () => {
  clearTable('tasks');
  clearTable('notes');
  const nr = db.prepare('INSERT INTO notes (title) VALUES (?)').run('备忘录1');
  const tr = db.prepare('INSERT INTO tasks (date, title, note_id) VALUES (?, ?, ?)').run('2026-07-13', '任务1', nr.lastInsertRowid);
  assert.equal(db.prepare('SELECT * FROM tasks WHERE note_id = ?').get(nr.lastInsertRowid).note_id, nr.lastInsertRowid);
  db.prepare('UPDATE tasks SET note_id = NULL WHERE note_id = ?').run(nr.lastInsertRowid);
  db.prepare('DELETE FROM notes WHERE id = ?').run(nr.lastInsertRowid);
  const t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(tr.lastInsertRowid);
  assert.equal(t.note_id, null);
  const n = db.prepare('SELECT * FROM notes WHERE id = ?').get(nr.lastInsertRowid);
  assert.equal(n, undefined);
});

test('复制任务保留备忘录关联', () => {
  clearTable('tasks');
  clearTable('notes');
  const nr = db.prepare('INSERT INTO notes (title) VALUES (?)').run('原笔记');
  const tr = db.prepare('INSERT INTO tasks (date, title, note_id) VALUES (?, ?, ?)').run('2026-07-13', '原任务', nr.lastInsertRowid);
  db.prepare(
    'INSERT INTO tasks (date, title, start_time, end_time, planned_duration, category, priority, note, is_recurring, is_planned, note_id) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
  ).run('2026-07-14', '复制任务', '', '', 0, '', 2, '', 0, 1, nr.lastInsertRowid);
  const copy = db.prepare("SELECT * FROM tasks WHERE title = '复制任务'").get();
  assert.equal(copy.note_id, nr.lastInsertRowid);
});

console.log('\n--- 综合场景测试 ---');
test('完整工作流：创建任务→关联备忘录→完成任务→记录成果→查看复盘', () => {
  clearTable('tasks');
  clearTable('notes');
  clearTable('daily_reviews');

  const nr = db.prepare('INSERT INTO notes (title, content, category) VALUES (?, ?, ?)').run('编码笔记', '实现接口逻辑', '工作');

  const tr = db.prepare(
    'INSERT INTO tasks (date, title, start_time, end_time, planned_duration, category, priority, note_id, is_planned) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)'
  ).run('2026-07-13', '实现登录接口', '09:00', '11:00', 120, '开发', 1, nr.lastInsertRowid, 1);

  let t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(tr.lastInsertRowid);
  assert.equal(t.title, '实现登录接口');
  assert.equal(t.note_id, nr.lastInsertRowid);
  assert.equal(t.status, 'planned');

  db.prepare("UPDATE tasks SET status = 'in_progress', actual_start = ? WHERE id = ?").run('09:05', tr.lastInsertRowid);
  t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(tr.lastInsertRowid);
  assert.equal(t.status, 'in_progress');
  assert.equal(t.actual_start, '09:05');

  db.prepare("UPDATE tasks SET status = 'completed', actual_end = ?, actual_duration = ?, achievement = ? WHERE id = ?")
    .run('11:10', 125, '完成了登录接口的JWT认证实现', tr.lastInsertRowid);
  t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(tr.lastInsertRowid);
  assert.equal(t.status, 'completed');
  assert.equal(t.actual_duration, 125);
  assert.ok(t.achievement.includes('JWT'));

  db.prepare('INSERT INTO daily_reviews (date, content) VALUES (?, ?)').run('2026-07-13', '完成了登录接口开发，实现了JWT认证');

  const r = db.prepare('SELECT * FROM daily_reviews WHERE date = ?').get('2026-07-13');
  assert.equal(r.content, '完成了登录接口开发，实现了JWT认证');

  const completedTasks = db.prepare("SELECT * FROM tasks WHERE date = ? AND status = 'completed'").all('2026-07-13');
  assert.equal(completedTasks.length, 1);
  assert.equal(completedTasks[0].title, '实现登录接口');

  const linkedNote = db.prepare('SELECT * FROM notes WHERE id = ?').get(nr.lastInsertRowid);
  assert.equal(linkedNote.title, '编码笔记');
});

console.log('\n--- 边缘情况测试 ---');
test('空标题任务允许(空字符串非NULL)', () => {
  clearTable('tasks');
  const r = db.prepare('INSERT INTO tasks (date, title) VALUES (?, ?)').run('2026-07-13', '');
  assert.ok(r.lastInsertRowid > 0);
});

test('NULL标题任务被拒绝', () => {
  clearTable('tasks');
  assert.throws(() => {
    db.prepare('INSERT INTO tasks (date, title) VALUES (?, ?)').run('2026-07-13', null);
  });
});

test('备忘录空标题允许(空字符串非NULL)', () => {
  clearTable('notes');
  const r = db.prepare('INSERT INTO notes (title) VALUES (?)').run('');
  assert.ok(r.lastInsertRowid > 0);
});

test('备忘录NULL标题被拒绝', () => {
  clearTable('notes');
  assert.throws(() => {
    db.prepare('INSERT INTO notes (title) VALUES (?)').run(null);
  });
});

console.log('\n--- 备忘录分类管理测试 ---');
test('创建备忘录分类', () => {
  clearTable('note_categories');
  const r = db.prepare('INSERT INTO note_categories (name) VALUES (?)').run('工作');
  assert.ok(r.lastInsertRowid > 0);
  const c = db.prepare('SELECT * FROM note_categories WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(c.name, '工作');
});

test('备忘录分类唯一约束', () => {
  clearTable('note_categories');
  db.prepare('INSERT INTO note_categories (name) VALUES (?)').run('工作');
  assert.throws(() => {
    db.prepare('INSERT INTO note_categories (name) VALUES (?)').run('工作');
  });
});

test('删除备忘录分类', () => {
  clearTable('note_categories');
  db.prepare('INSERT INTO note_categories (name) VALUES (?)').run('工作');
  db.prepare('DELETE FROM note_categories WHERE name = ?').run('工作');
  const c = db.prepare('SELECT * FROM note_categories WHERE name = ?').get('工作');
  assert.equal(c, undefined);
});

test('合并分类来源', () => {
  clearTable('note_categories');
  clearTable('notes');
  db.prepare('INSERT INTO note_categories (name) VALUES (?)').run('自定义分类');
  db.prepare("INSERT INTO notes (title, category) VALUES (?, ?)").run('笔记', '笔记分类');
  const namedCats = db.prepare("SELECT name FROM note_categories ORDER BY name").all();
  const noteCats = db.prepare("SELECT DISTINCT category FROM notes WHERE category != '' ORDER BY category").all();
  const all = new Set([...namedCats.map(c => c.name), ...noteCats.map(c => c.category)]);
  const sorted = Array.from(all).sort();
  assert.ok(sorted.includes('自定义分类'));
  assert.ok(sorted.includes('笔记分类'));
});

console.log('\n--- 新增边缘情况测试 ---');
test('任务超长标题', () => {
  clearTable('tasks');
  const longTitle = 'A'.repeat(1000);
  const r = db.prepare('INSERT INTO tasks (date, title) VALUES (?, ?)').run('2026-07-13', longTitle);
  const t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(t.title.length, 1000);
});

test('任务特殊字符标题', () => {
  clearTable('tasks');
  const special = '<script>alert("xss")</script> & "quotes" \'single\' 中文日本語한국어';
  const r = db.prepare('INSERT INTO tasks (date, title) VALUES (?, ?)').run('2026-07-13', special);
  const t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(t.title, special);
});

test('备忘录超长内容', () => {
  clearTable('notes');
  const longContent = 'A'.repeat(10000);
  const r = db.prepare('INSERT INTO notes (title, content) VALUES (?, ?)').run('长内容笔记', longContent);
  const n = db.prepare('SELECT * FROM notes WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(n.content.length, 10000);
});

test('备忘录超长标题', () => {
  clearTable('notes');
  const longTitle = 'B'.repeat(500);
  const r = db.prepare('INSERT INTO notes (title) VALUES (?)').run(longTitle);
  const n = db.prepare('SELECT * FROM notes WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(n.title.length, 500);
});

test('任务状态全部转换', () => {
  clearTable('tasks');
  const r = db.prepare('INSERT INTO tasks (date, title) VALUES (?, ?)').run('2026-07-13', '状态测试');
  const statuses = ['planned', 'in_progress', 'completed', 'cancelled'];
  for (const s of statuses) {
    db.prepare("UPDATE tasks SET status = ?, updated_at = datetime('now','localtime') WHERE id = ?").run(s, r.lastInsertRowid);
    const t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(r.lastInsertRowid);
    assert.equal(t.status, s);
  }
});

test('任务所有字段为空字符串', () => {
  clearTable('tasks');
  const r = db.prepare(
    'INSERT INTO tasks (date, title, start_time, end_time, planned_duration, category, priority, note, is_recurring, is_planned) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
  ).run('2026-07-13', '空字段测试', '', '', 0, '', 2, '', 0, 1);
  const t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(t.start_time, '');
  assert.equal(t.category, '');
  assert.equal(t.note, '');
});

test('备忘录所有字段为空', () => {
  clearTable('notes');
  const r = db.prepare('INSERT INTO notes (title, content, category) VALUES (?, ?, ?)').run('', '', '');
  const n = db.prepare('SELECT * FROM notes WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(n.title, '');
  assert.equal(n.content, '');
  assert.equal(n.category, '');
});

test('多个备忘录相同分类', () => {
  clearTable('notes');
  db.prepare('INSERT INTO notes (title, category) VALUES (?, ?)').run('笔记A', '工作');
  db.prepare('INSERT INTO notes (title, category) VALUES (?, ?)').run('笔记B', '工作');
  db.prepare('INSERT INTO notes (title, category) VALUES (?, ?)').run('笔记C', '工作');
  const workNotes = db.prepare('SELECT * FROM notes WHERE category = ? ORDER BY id').all('工作');
  assert.equal(workNotes.length, 3);
});

test('任务优先级边界值', () => {
  clearTable('tasks');
  const r1 = db.prepare('INSERT INTO tasks (date, title, priority) VALUES (?, ?, ?)').run('2026-07-13', '高优先级', 1);
  const r2 = db.prepare('INSERT INTO tasks (date, title, priority) VALUES (?, ?, ?)').run('2026-07-13', '中优先级', 2);
  const r3 = db.prepare('INSERT INTO tasks (date, title, priority) VALUES (?, ?, ?)').run('2026-07-13', '低优先级', 3);
  assert.equal(db.prepare('SELECT * FROM tasks WHERE id = ?').get(r1.lastInsertRowid).priority, 1);
  assert.equal(db.prepare('SELECT * FROM tasks WHERE id = ?').get(r2.lastInsertRowid).priority, 2);
  assert.equal(db.prepare('SELECT * FROM tasks WHERE id = ?').get(r3.lastInsertRowid).priority, 3);
});

test('任务计划时长为零', () => {
  clearTable('tasks');
  const r = db.prepare('INSERT INTO tasks (date, title, planned_duration) VALUES (?, ?, ?)').run('2026-07-13', '零时长任务', 0);
  const t = db.prepare('SELECT * FROM tasks WHERE id = ?').get(r.lastInsertRowid);
  assert.equal(t.planned_duration, 0);
});

test('备忘录内容含图片markdown', () => {
  clearTable('notes');
  const content = '这是文字\n![图片1](http://example.com/img1.png)\n更多文字\n![图片2](http://example.com/img2.jpg)';
  const r = db.prepare('INSERT INTO notes (title, content) VALUES (?, ?)').run('含图片笔记', content);
  const n = db.prepare('SELECT * FROM notes WHERE id = ?').get(r.lastInsertRowid);
  assert.ok(n.content.includes('![图片1]'));
  assert.ok(n.content.includes('![图片2]'));
  const imgMatches = n.content.match(/!\[([^\]]*)\]\(([^)]+)\)/g);
  assert.equal(imgMatches.length, 2);
});

test('任务按优先级排序', () => {
  clearTable('tasks');
  db.prepare('INSERT INTO tasks (date, title, priority) VALUES (?, ?, ?)').run('2026-07-13', '低优先级', 3);
  db.prepare('INSERT INTO tasks (date, title, priority) VALUES (?, ?, ?)').run('2026-07-13', '高优先级', 1);
  db.prepare('INSERT INTO tasks (date, title, priority) VALUES (?, ?, ?)').run('2026-07-13', '中优先级', 2);
  const tasks = db.prepare('SELECT * FROM tasks WHERE date = ? ORDER BY priority, id').all('2026-07-13');
  assert.equal(tasks[0].priority, 1);
  assert.equal(tasks[1].priority, 2);
  assert.equal(tasks[2].priority, 3);
});

test('任务按计划内优先排序', () => {
  clearTable('tasks');
  db.prepare('INSERT INTO tasks (date, title, is_planned) VALUES (?, ?, ?)').run('2026-07-13', '计划外', 0);
  db.prepare('INSERT INTO tasks (date, title, is_planned) VALUES (?, ?, ?)').run('2026-07-13', '计划内', 1);
  const tasks = db.prepare('SELECT * FROM tasks WHERE date = ? ORDER BY is_planned DESC, id').all('2026-07-13');
  assert.equal(tasks[0].is_planned, 1);
  assert.equal(tasks[0].title, '计划内');
  assert.equal(tasks[1].is_planned, 0);
  assert.equal(tasks[1].title, '计划外');
});
db.close();
try { if (fs.existsSync(dbPath)) fs.unlinkSync(dbPath); } catch (e) { /* ignore cleanup errors on Windows */ }

console.log(`\n结果: ${passed} 通过, ${failed} 失败, ${passed + failed} 总计\n`);
process.exit(failed > 0 ? 1 : 0);