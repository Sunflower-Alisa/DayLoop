const express = require('express');
const router = express.Router();
const db = require('../database');
const { getUserId } = require('../middleware/auth');

function getUserIdOrZero(req) {
  return getUserId(req) || 0;
}

function getLinkedTasks(questionId) {
  const fromLinks = db.prepare(
    `SELECT t.id, t.title, t.date, t.start_time, t.end_time, t.status, t.category
     FROM tasks t
     INNER JOIN question_task_links qtl ON qtl.task_id = t.id
     WHERE qtl.question_id = ?
     ORDER BY t.date DESC, t.start_time`
  ).all(questionId);
  if (fromLinks.length > 0) return fromLinks;
  const q = db.prepare('SELECT task_id FROM questions WHERE id = ?').get(questionId);
  if (q && q.task_id) {
    const task = db.prepare(
      `SELECT id, title, date, start_time, end_time, status, category FROM tasks WHERE id = ?`
    ).get(q.task_id);
    if (task) return [task];
  }
  return [];
}

function enrichQuestion(q) {
  const linked_tasks = getLinkedTasks(q.id);
  return { ...q, linked_tasks };
}

router.get('/', (req, res) => {
  const { category, search } = req.query;
  const userId = getUserIdOrZero(req);
  let questions;
  if (category && search) {
    questions = db.prepare('SELECT * FROM questions WHERE user_id = ? AND category = ? AND (title LIKE ? OR content LIKE ?) ORDER BY created_at DESC').all(userId, category, `%${search}%`, `%${search}%`);
  } else if (category) {
    questions = db.prepare('SELECT * FROM questions WHERE user_id = ? AND category = ? ORDER BY created_at DESC').all(userId, category);
  } else if (search) {
    questions = db.prepare('SELECT * FROM questions WHERE user_id = ? AND (title LIKE ? OR content LIKE ?) ORDER BY created_at DESC').all(userId, `%${search}%`, `%${search}%`);
  } else {
    questions = db.prepare('SELECT * FROM questions WHERE user_id = ? ORDER BY created_at DESC').all(userId);
  }
  res.json(questions.map(enrichQuestion));
});

router.post('/', (req, res) => {
  const userId = getUserId(req) || 0;
  const { title, content, answer, answer_source, category, tags, task_ids } = req.body;
  if (!title || !title.trim()) return res.status(400).json({ error: 'Title is required' });
  const allowedSources = ['self', 'ai', 'web'];
  const source = allowedSources.includes(answer_source) ? answer_source : 'self';
  const r = db.prepare(
    'INSERT INTO questions (title, content, answer, answer_source, category, tags, user_id) VALUES (?, ?, ?, ?, ?, ?, ?)'
  ).run(title.trim(), content || '', answer || '', source, category || '', tags || '', userId);
  const questionId = r.lastInsertRowid;

  if (Array.isArray(task_ids) && task_ids.length > 0) {
    const insert = db.prepare('INSERT OR IGNORE INTO question_task_links (question_id, task_id) VALUES (?, ?)');
    for (const tid of task_ids) {
      insert.run(questionId, tid);
    }
  }

  const question = db.prepare('SELECT * FROM questions WHERE id = ?').get(questionId);
  res.json(enrichQuestion(question));
});

router.get('/categories', (req, res) => {
  const userId = getUserIdOrZero(req);
  const cats = db.prepare("SELECT DISTINCT category FROM questions WHERE user_id = ? AND category != '' ORDER BY category").all(userId);
  const namedCats = db.prepare("SELECT name FROM question_categories WHERE user_id = ? ORDER BY name").all(userId);
  const all = new Set([...cats.map(c => c.category), ...namedCats.map(c => c.name)]);
  res.json(Array.from(all).sort());
});

router.post('/categories', (req, res) => {
  const userId = getUserId(req) || 0;
  const { name } = req.body;
  if (!name || !name.trim()) return res.status(400).json({ error: 'Name is required' });
  try {
    db.prepare('INSERT INTO question_categories (name, user_id) VALUES (?, ?)').run(name.trim(), userId);
    res.json({ name: name.trim() });
  } catch (e) {
    if (e.message.includes('UNIQUE')) return res.status(409).json({ error: 'Category already exists' });
    throw e;
  }
});

router.delete('/categories/:name', (req, res) => {
  const userId = getUserId(req) || 0;
  const { name } = req.params;
  db.prepare('DELETE FROM question_categories WHERE name = ? AND user_id = ?').run(decodeURIComponent(name), userId);
  res.json({ message: 'Category deleted' });
});

router.get('/:id', (req, res) => {
  const { id } = req.params;
  const userId = getUserIdOrZero(req);
  const question = db.prepare('SELECT * FROM questions WHERE id = ? AND user_id = ?').get(id, userId);
  if (!question) return res.status(404).json({ error: 'Question not found' });
  res.json(enrichQuestion(question));
});

router.put('/:id', (req, res) => {
  const { id } = req.params;
  const userId = getUserId(req) || 0;
  const { title, content, answer, answer_source, category, tags, task_ids } = req.body;
  const allowedSources = ['self', 'ai', 'web'];
  const source = allowedSources.includes(answer_source) ? answer_source : undefined;
  db.prepare(`
    UPDATE questions SET
      title = COALESCE(?, title),
      content = COALESCE(?, content),
      answer = COALESCE(?, answer),
      answer_source = COALESCE(?, answer_source),
      category = COALESCE(?, category),
      tags = COALESCE(?, tags),
      updated_at = datetime('now','localtime')
    WHERE id = ? AND user_id = ?
  `).run(title, content, answer, source, category, tags, id, userId);

  if (Array.isArray(task_ids)) {
    db.prepare('DELETE FROM question_task_links WHERE question_id = ?').run(id);
    const insert = db.prepare('INSERT OR IGNORE INTO question_task_links (question_id, task_id) VALUES (?, ?)');
    for (const tid of task_ids) {
      insert.run(id, tid);
    }
  }

  const question = db.prepare('SELECT * FROM questions WHERE id = ?').get(id);
  if (!question) return res.status(404).json({ error: 'Question not found' });
  res.json(enrichQuestion(question));
});

router.delete('/:id', (req, res) => {
  const { id } = req.params;
  const userId = getUserId(req) || 0;
  // Verify ownership before modifying data
  const question = db.prepare('SELECT id, user_id FROM questions WHERE id = ? AND user_id = ?').get(id, userId);
  if (!question) return res.status(404).json({ error: 'Question not found' });
  
  db.prepare('DELETE FROM question_task_links WHERE question_id = ?').run(id);
  db.prepare('DELETE FROM questions WHERE id = ? AND user_id = ?').run(id, userId);
  res.json({ message: 'Question deleted' });
});

module.exports = router;
