const Database = require('better-sqlite3');
const path = require('path');
const fs = require('fs');

const dbPath = path.join(__dirname, '..', 'data', 'dayloop.db');
fs.mkdirSync(path.dirname(dbPath), { recursive: true });
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
addColumnIfMissing('tasks', 'planned_days', 'INTEGER DEFAULT 1');
addColumnIfMissing('recurring_templates', 'sync_enabled', 'INTEGER DEFAULT 1');
addColumnIfMissing('recurring_templates', 'planned_days', 'INTEGER DEFAULT 1');
addColumnIfMissing('tasks', 'overall_status', "TEXT DEFAULT 'pending'");

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

db.exec(`
  CREATE TABLE IF NOT EXISTS summaries (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    type TEXT NOT NULL CHECK(type IN ('weekly','monthly','quarterly','yearly')),
    period_key TEXT NOT NULL,
    content TEXT DEFAULT '',
    auto_summary TEXT DEFAULT '',
    user_id INTEGER DEFAULT 0,
    created_at TEXT DEFAULT (datetime('now','localtime')),
    updated_at TEXT DEFAULT (datetime('now','localtime'))
  )
`);

db.exec(`
  CREATE TABLE IF NOT EXISTS task_summaries (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    content TEXT DEFAULT '',
    user_id INTEGER DEFAULT 0,
    created_at TEXT DEFAULT (datetime('now','localtime')),
    updated_at TEXT DEFAULT (datetime('now','localtime'))
  )
`);

addColumnIfMissing('daily_reviews', 'tags', 'TEXT DEFAULT \'\'');

// ===== English Learning tables =====
db.exec(`
  CREATE TABLE IF NOT EXISTS word_books (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    level TEXT DEFAULT 'intermediate',
    description TEXT DEFAULT '',
    cover_color TEXT DEFAULT '#4f46e5',
    is_default INTEGER DEFAULT 0,
    user_id INTEGER DEFAULT 0,
    created_at TEXT DEFAULT (datetime('now','localtime'))
  );

  CREATE TABLE IF NOT EXISTS words (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    word TEXT NOT NULL,
    phonetic TEXT DEFAULT '',
    pos TEXT DEFAULT '',
    meaning TEXT DEFAULT '',
    example_en TEXT DEFAULT '',
    example_cn TEXT DEFAULT '',
    image_url TEXT DEFAULT '',
    audio_url TEXT DEFAULT '',
    book_id INTEGER DEFAULT 0,
    created_at TEXT DEFAULT (datetime('now','localtime'))
  );

  CREATE TABLE IF NOT EXISTS word_progress (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    word_id INTEGER NOT NULL,
    status TEXT DEFAULT 'new',
    stage INTEGER DEFAULT 0,
    correct_streak INTEGER DEFAULT 0,
    wrong_count INTEGER DEFAULT 0,
    last_review_at TEXT DEFAULT '',
    next_review_at TEXT DEFAULT '',
    UNIQUE(user_id, word_id)
  );

  CREATE TABLE IF NOT EXISTS learning_logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    date TEXT DEFAULT '',
    type TEXT DEFAULT 'new',
    word_id INTEGER,
    topic_id INTEGER,
    result TEXT DEFAULT '',
    created_at TEXT DEFAULT (datetime('now','localtime'))
  );

  CREATE TABLE IF NOT EXISTS wrong_words (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    word_id INTEGER NOT NULL,
    created_at TEXT DEFAULT (datetime('now','localtime')),
    UNIQUE(user_id, word_id)
  );

  CREATE TABLE IF NOT EXISTS study_sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    date TEXT DEFAULT '',
    module TEXT DEFAULT '',
    start_time TEXT DEFAULT '',
    end_time TEXT DEFAULT '',
    duration_seconds INTEGER DEFAULT 0,
    created_at TEXT DEFAULT (datetime('now','localtime'))
  );

  CREATE TABLE IF NOT EXISTS scenarios (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    category TEXT DEFAULT '',
    level INTEGER DEFAULT 1,
    icon TEXT DEFAULT '',
    description TEXT DEFAULT '',
    user_id INTEGER DEFAULT 0,
    created_at TEXT DEFAULT (datetime('now','localtime'))
  );

  CREATE TABLE IF NOT EXISTS scenario_lines (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    scenario_id INTEGER NOT NULL,
    ord INTEGER DEFAULT 0,
    speaker TEXT DEFAULT '',
    en_text TEXT DEFAULT '',
    cn_text TEXT DEFAULT '',
    audio_url TEXT DEFAULT ''
  );

  CREATE TABLE IF NOT EXISTS scenario_phrases (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    scenario_id INTEGER NOT NULL,
    phrase TEXT DEFAULT '',
    meaning TEXT DEFAULT '',
    example_en TEXT DEFAULT '',
    example_cn TEXT DEFAULT ''
  );

  CREATE TABLE IF NOT EXISTS scenario_quizzes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    scenario_id INTEGER NOT NULL,
    question_en TEXT DEFAULT '',
    question_cn TEXT DEFAULT '',
    options TEXT DEFAULT '',
    answer_index INTEGER DEFAULT 0,
    explanation TEXT DEFAULT ''
  );

  CREATE TABLE IF NOT EXISTS scenario_progress (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    scenario_id INTEGER NOT NULL,
    mastered INTEGER DEFAULT 0,
    updated_at TEXT DEFAULT (datetime('now','localtime')),
    UNIQUE(user_id, scenario_id)
  );

  CREATE TABLE IF NOT EXISTS speaking_topics (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    category TEXT DEFAULT 'daily',
    level TEXT DEFAULT 'beginner',
    lines TEXT DEFAULT '',
    source_type TEXT DEFAULT 'topic',
    source_id INTEGER DEFAULT 0,
    user_id INTEGER DEFAULT 0,
    created_at TEXT DEFAULT (datetime('now','localtime'))
  );

  CREATE TABLE IF NOT EXISTS speaking_records (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    topic_id INTEGER NOT NULL,
    line_index INTEGER DEFAULT 0,
    audio_url TEXT DEFAULT '',
    accuracy INTEGER DEFAULT 0,
    fluency INTEGER DEFAULT 0,
    completeness INTEGER DEFAULT 0,
    overall INTEGER DEFAULT 0,
    created_at TEXT DEFAULT (datetime('now','localtime'))
  );

  CREATE TABLE IF NOT EXISTS video_clips (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    source TEXT DEFAULT '',
    cover_url TEXT DEFAULT '',
    path TEXT DEFAULT '',
    duration INTEGER DEFAULT 0,
    level TEXT DEFAULT 'medium',
    tags TEXT DEFAULT '',
    description TEXT DEFAULT '',
    user_id INTEGER DEFAULT 0,
    created_at TEXT DEFAULT (datetime('now','localtime'))
  );

  CREATE TABLE IF NOT EXISTS clip_lines (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    clip_id INTEGER NOT NULL,
    ord INTEGER DEFAULT 0,
    speaker TEXT DEFAULT '',
    en_text TEXT DEFAULT '',
    cn_text TEXT DEFAULT '',
    start_time REAL DEFAULT 0,
    end_time REAL DEFAULT 0
  );
`);

module.exports = db;
