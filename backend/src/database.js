const Database = require('better-sqlite3');
const path = require('path');

const dbPath = path.join(__dirname, '..', 'data', 'dayloop.db');
const db = new Database(dbPath);

db.pragma('journal_mode = WAL');
db.pragma('foreign_keys = ON');

db.exec(`
  CREATE TABLE IF NOT EXISTS users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    username TEXT NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    created_at TEXT DEFAULT (datetime('now','localtime'))
  );

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
    user_id INTEGER DEFAULT 0,
    created_at TEXT DEFAULT (datetime('now','localtime')),
    updated_at TEXT DEFAULT (datetime('now','localtime'))
  );

  CREATE TABLE IF NOT EXISTS daily_reviews (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    date TEXT NOT NULL UNIQUE,
    content TEXT DEFAULT '',
    user_id INTEGER DEFAULT 0,
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
    user_id INTEGER DEFAULT 0,
    created_at TEXT DEFAULT (datetime('now','localtime'))
  );

  CREATE TABLE IF NOT EXISTS notes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    content TEXT DEFAULT '',
    category TEXT DEFAULT '',
    task_id INTEGER,
    user_id INTEGER DEFAULT 0,
    created_at TEXT DEFAULT (datetime('now','localtime')),
    updated_at TEXT DEFAULT (datetime('now','localtime'))
  );
`);

function addColumnIfMissing(table, column, definition) {
  const cols = db.pragma(`table_info(${table})`).map(c => c.name);
  if (!cols.includes(column)) {
    db.exec(`ALTER TABLE ${table} ADD COLUMN ${column} ${definition}`);
  }
}

addColumnIfMissing('tasks', 'start_time', 'TEXT DEFAULT \'\'');
addColumnIfMissing('tasks', 'end_time', 'TEXT DEFAULT \'\'');
addColumnIfMissing('tasks', 'actual_start', 'TEXT');
addColumnIfMissing('tasks', 'actual_end', 'TEXT');
addColumnIfMissing('tasks', 'is_recurring', 'INTEGER DEFAULT 0');
addColumnIfMissing('tasks', 'is_planned', 'INTEGER DEFAULT 1');
addColumnIfMissing('tasks', 'recurring_template_id', 'INTEGER');
addColumnIfMissing('tasks', 'achievement', 'TEXT DEFAULT \'\'');
addColumnIfMissing('tasks', 'note_id', 'INTEGER');
addColumnIfMissing('notes', 'tags', 'TEXT DEFAULT \'\'');
addColumnIfMissing('tasks', 'tags', 'TEXT DEFAULT \'\'');
addColumnIfMissing('tasks', 'user_id', 'INTEGER DEFAULT 0');
addColumnIfMissing('daily_reviews', 'user_id', 'INTEGER DEFAULT 0');
addColumnIfMissing('recurring_templates', 'user_id', 'INTEGER DEFAULT 0');
addColumnIfMissing('recurring_templates', 'recurrence_type', 'TEXT DEFAULT \'daily\'');
addColumnIfMissing('recurring_templates', 'recurrence_days', 'TEXT DEFAULT \'\'');
addColumnIfMissing('recurring_templates', 'recurring_enabled', 'INTEGER DEFAULT 1');
addColumnIfMissing('notes', 'user_id', 'INTEGER DEFAULT 0');
addColumnIfMissing('note_categories', 'user_id', 'INTEGER DEFAULT 0');
addColumnIfMissing('tasks', 'sync_enabled', 'INTEGER DEFAULT 1');
addColumnIfMissing('recurring_templates', 'sync_enabled', 'INTEGER DEFAULT 1');

db.exec(`
  CREATE TABLE IF NOT EXISTS questions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    content TEXT DEFAULT '',
    answer TEXT DEFAULT '',
    answer_source TEXT DEFAULT 'self' CHECK(answer_source IN ('self','ai','web')),
    category TEXT DEFAULT '',
    tags TEXT DEFAULT '',
    task_id INTEGER,
    user_id INTEGER DEFAULT 0,
    created_at TEXT DEFAULT (datetime('now','localtime')),
    updated_at TEXT DEFAULT (datetime('now','localtime'))
  );

  CREATE TABLE IF NOT EXISTS question_categories (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE,
    user_id INTEGER DEFAULT 0,
    created_at TEXT DEFAULT (datetime('now','localtime'))
  );

  CREATE TABLE IF NOT EXISTS question_task_links (
    question_id INTEGER NOT NULL,
    task_id INTEGER NOT NULL,
    PRIMARY KEY (question_id, task_id)
  );
`);

db.exec(`
  CREATE TABLE IF NOT EXISTS note_categories (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE,
    user_id INTEGER DEFAULT 0,
    created_at TEXT DEFAULT (datetime('now','localtime'))
  )
`);

db.exec(`
  CREATE TABLE IF NOT EXISTS note_task_links (
    note_id INTEGER NOT NULL,
    task_id INTEGER NOT NULL,
    PRIMARY KEY (note_id, task_id)
  )
`);

db.exec(`
  CREATE TABLE IF NOT EXISTS app_settings (
    key TEXT PRIMARY KEY,
    value TEXT DEFAULT ''
  )
`);

module.exports = db;
