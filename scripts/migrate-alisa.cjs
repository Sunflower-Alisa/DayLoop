const Database = require('better-sqlite3');
const path = require('path');
const bcrypt = require('bcryptjs');

const dbPath = path.join(__dirname, '..', 'backend', 'data', 'dayloop.db');
const db = new Database(dbPath);

// Ensure users table exists and has user_id columns (same as database.js)
db.exec(`
  CREATE TABLE IF NOT EXISTS users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    username TEXT NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    created_at TEXT DEFAULT (datetime('now','localtime'))
  )
`);

function addColumnIfMissing(table, column, definition) {
  const cols = db.pragma(`table_info(${table})`).map(c => c.name);
  if (!cols.includes(column)) {
    db.exec(`ALTER TABLE ${table} ADD COLUMN ${column} ${definition}`);
  }
}

addColumnIfMissing('tasks', 'user_id', 'INTEGER DEFAULT 0');
addColumnIfMissing('daily_reviews', 'user_id', 'INTEGER DEFAULT 0');
addColumnIfMissing('recurring_templates', 'user_id', 'INTEGER DEFAULT 0');
addColumnIfMissing('notes', 'user_id', 'INTEGER DEFAULT 0');
addColumnIfMissing('note_categories', 'user_id', 'INTEGER DEFAULT 0');

// Create alisa user if not exists
let alisa = db.prepare('SELECT id FROM users WHERE username = ?').get('alisa');
let alisaId;
if (alisa) {
  alisaId = alisa.id;
  console.log(`User 'alisa' already exists with id=${alisaId}`);
} else {
  const hash = bcrypt.hashSync('alisa123', 10);
  const result = db.prepare('INSERT INTO users (username, password_hash) VALUES (?, ?)').run('alisa', hash);
  alisaId = result.lastInsertRowid;
  console.log(`Created user 'alisa' with id=${alisaId} (password: alisa123)`);
}

// Migrate existing data (user_id = 0) to alisa
const tables = [
  { name: 'tasks', count: 0 },
  { name: 'daily_reviews', count: 0 },
  { name: 'recurring_templates', count: 0 },
  { name: 'notes', count: 0 },
  { name: 'note_categories', count: 0 },
];

for (const t of tables) {
  const result = db.prepare(`UPDATE ${t.name} SET user_id = ? WHERE user_id = 0`).run(alisaId);
  t.count = result.changes;
  if (t.count > 0) console.log(`Migrated ${t.count} ${t.name} to user alisa`);
}

const total = tables.reduce((s, t) => s + t.count, 0);
console.log(`\nMigration complete. Total records migrated: ${total}`);
console.log('You can now log in with username: alisa, password: alisa123');

db.close();
