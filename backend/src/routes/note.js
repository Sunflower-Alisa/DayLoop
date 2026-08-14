const express = require('express');
const router = express.Router();
const db = require('../database');
const { getUserId } = require('../middleware/auth');
const { syncNote } = require('../sync/obsidian');

function getUserIdOrZero(req) {
  return getUserId(req) || 0;
}

function getLinkedTasks(noteId) {
  const fromLinks = db.prepare(
    `SELECT t.id, t.title, t.date, t.start_time, t.end_time, t.status, t.category
     FROM tasks t
     INNER JOIN note_task_links ntl ON ntl.task_id = t.id
     WHERE ntl.note_id = ?
     ORDER BY t.date DESC, t.start_time`
  ).all(noteId);
  if (fromLinks.length > 0) return fromLinks;
  const fromTasks = db.prepare(
    `SELECT id, title, date, start_time, end_time, status, category FROM tasks WHERE note_id = ? ORDER BY date DESC, start_time`
  ).all(noteId);
  if (fromTasks.length > 0) return fromTasks;
  const note = db.prepare('SELECT task_id FROM notes WHERE id = ?').get(noteId);
  if (note && note.task_id) {
    const task = db.prepare(
      `SELECT id, title, date, start_time, end_time, status, category FROM tasks WHERE id = ?`
    ).get(note.task_id);
    if (task) return [task];
  }
  return [];
}

function enrichNote(note) {
  const linked_tasks = getLinkedTasks(note.id);
  return { ...note, linked_tasks };
}

router.get('/', (req, res) => {
  const { category, search } = req.query;
  const userId = getUserIdOrZero(req);
  let notes;
  if (category && search) {
    notes = db.prepare('SELECT * FROM notes WHERE user_id = ? AND category = ? AND (title LIKE ? OR content LIKE ?) ORDER BY created_at DESC').all(userId, category, `%${search}%`, `%${search}%`);
  } else if (category) {
    notes = db.prepare('SELECT * FROM notes WHERE user_id = ? AND category = ? ORDER BY created_at DESC').all(userId, category);
  } else if (search) {
    notes = db.prepare('SELECT * FROM notes WHERE user_id = ? AND (title LIKE ? OR content LIKE ?) ORDER BY created_at DESC').all(userId, `%${search}%`, `%${search}%`);
  } else {
    notes = db.prepare('SELECT * FROM notes WHERE user_id = ? ORDER BY created_at DESC').all(userId);
  }
  res.json(notes.map(enrichNote));
});

router.post('/', (req, res) => {
  const userId = getUserId(req) || 0;
  const { title, content, category, task_ids, tags } = req.body;
  if (!title || !title.trim()) return res.status(400).json({ error: 'Title is required' });
  const r = db.prepare(
    'INSERT INTO notes (title, content, category, tags, user_id) VALUES (?, ?, ?, ?, ?)'
  ).run(title.trim(), content || '', category || '', tags || '', userId);
  const noteId = r.lastInsertRowid;

  if (Array.isArray(task_ids) && task_ids.length > 0) {
    const insert = db.prepare('INSERT OR IGNORE INTO note_task_links (note_id, task_id) VALUES (?, ?)');
    for (const tid of task_ids) {
      insert.run(noteId, tid);
      db.prepare('UPDATE tasks SET note_id = ? WHERE id = ?').run(noteId, tid);
    }
  }

  const note = db.prepare('SELECT * FROM notes WHERE id = ?').get(noteId);
  syncNote(note);
  res.json(enrichNote(note));
});

router.get('/categories', (req, res) => {
  const userId = getUserIdOrZero(req);
  const cats = db.prepare("SELECT DISTINCT category FROM notes WHERE user_id = ? AND category != '' ORDER BY category").all(userId);
  const namedCats = db.prepare("SELECT name FROM note_categories WHERE user_id = ? ORDER BY name").all(userId);
  const all = new Set([...cats.map(c => c.category), ...namedCats.map(c => c.name)]);
  res.json(Array.from(all).sort());
});

router.post('/categories', (req, res) => {
  const userId = getUserId(req) || 0;
  const { name } = req.body;
  if (!name || !name.trim()) return res.status(400).json({ error: 'Name is required' });
  try {
    db.prepare('INSERT INTO note_categories (name, user_id) VALUES (?, ?)').run(name.trim(), userId);
    res.json({ name: name.trim() });
  } catch (e) {
    if (e.message.includes('UNIQUE')) return res.status(409).json({ error: 'Category already exists' });
    throw e;
  }
});

router.delete('/categories/:name', (req, res) => {
  const userId = getUserId(req) || 0;
  const { name } = req.params;
  db.prepare('DELETE FROM note_categories WHERE name = ? AND user_id = ?').run(decodeURIComponent(name), userId);
  res.json({ message: 'Category deleted' });
});

router.get('/:id', (req, res) => {
  const { id } = req.params;
  const userId = getUserIdOrZero(req);
  const note = db.prepare('SELECT * FROM notes WHERE id = ? AND user_id = ?').get(id, userId);
  if (!note) return res.status(404).json({ error: 'Note not found' });
  res.json(enrichNote(note));
});

router.put('/:id', (req, res) => {
  const { id } = req.params;
  const userId = getUserId(req) || 0;
  const { title, content, category, task_ids, tags } = req.body;
  db.prepare(`
    UPDATE notes SET
      title = COALESCE(?, title),
      content = COALESCE(?, content),
      category = COALESCE(?, category),
      tags = COALESCE(?, tags),
      updated_at = datetime('now','localtime')
    WHERE id = ? AND user_id = ?
  `).run(title, content, category, tags, id, userId);

  if (Array.isArray(task_ids)) {
    db.prepare('DELETE FROM note_task_links WHERE note_id = ?').run(id);
    const insert = db.prepare('INSERT OR IGNORE INTO note_task_links (note_id, task_id) VALUES (?, ?)');
    for (const tid of task_ids) {
      insert.run(id, tid);
      db.prepare('UPDATE tasks SET note_id = ? WHERE id = ?').run(id, tid);
    }
  }

  const note = db.prepare('SELECT * FROM notes WHERE id = ?').get(id);
  if (!note) return res.status(404).json({ error: 'Note not found' });
  syncNote(note);
  res.json(enrichNote(note));
});

router.delete('/:id', (req, res) => {
  const { id } = req.params;
  const userId = getUserId(req) || 0;
  // Verify ownership before modifying data
  const note = db.prepare('SELECT id, user_id FROM notes WHERE id = ? AND user_id = ?').get(id, userId);
  if (!note) return res.status(404).json({ error: 'Note not found' });
  
  const linked = db.prepare('SELECT task_id FROM note_task_links WHERE note_id = ?').all(id);
  for (const l of linked) {
    db.prepare('UPDATE tasks SET note_id = NULL WHERE id = ?').run(l.task_id);
  }
  db.prepare('DELETE FROM note_task_links WHERE note_id = ?').run(id);
  db.prepare('DELETE FROM notes WHERE id = ? AND user_id = ?').run(id, userId);
  res.json({ message: 'Note deleted' });
});

module.exports = router;