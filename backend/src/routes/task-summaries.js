const express = require('express');
const router = express.Router();
const db = require('../database');
const { getUserId } = require('../middleware/auth');

// GET /api/task-summaries?title=xxx
router.get('/', (req, res) => {
  const userId = getUserId(req) || 0;
  const { title } = req.query;
  if (!title) return res.status(400).json({ error: 'title is required' });
  const row = db.prepare('SELECT * FROM task_summaries WHERE title = ? AND user_id = ?').get(title, userId);
  res.json(row || null);
});

// PUT /api/task-summaries/:title
router.put('/:title', (req, res) => {
  const userId = getUserId(req) || 0;
  const { title } = req.params;
  const { content } = req.body;
  const existing = db.prepare('SELECT id FROM task_summaries WHERE title = ? AND user_id = ?').get(title, userId);
  if (existing) {
    db.prepare("UPDATE task_summaries SET content = ?, updated_at = datetime('now','localtime') WHERE id = ?").run(content || '', existing.id);
  } else {
    db.prepare('INSERT INTO task_summaries (title, content, user_id) VALUES (?, ?, ?)').run(title, content || '', userId);
  }
  const row = db.prepare('SELECT * FROM task_summaries WHERE title = ? AND user_id = ?').get(title, userId);
  res.json(row);
});

module.exports = router;
