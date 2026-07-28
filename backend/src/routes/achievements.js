const express = require('express');
const router = express.Router();
const db = require('../database');
const { getUserId } = require('../middleware/auth');

function getUserIdOrZero(req) {
  return getUserId(req) || 0;
}

router.get('/', (req, res) => {
  const { category } = req.query;
  const userId = getUserIdOrZero(req);
  let tasks;
  if (category) {
    tasks = db.prepare(
      "SELECT * FROM tasks WHERE user_id = ? AND achievement != '' AND achievement IS NOT NULL AND category = ? ORDER BY date DESC, start_time DESC"
    ).all(userId, category);
  } else {
    tasks = db.prepare(
      "SELECT * FROM tasks WHERE user_id = ? AND achievement != '' AND achievement IS NOT NULL ORDER BY date DESC, start_time DESC"
    ).all(userId);
  }
  res.json(tasks);
});

router.get('/categories', (req, res) => {
  const userId = getUserIdOrZero(req);
  const cats = db.prepare(
    "SELECT DISTINCT category FROM tasks WHERE user_id = ? AND achievement != '' AND achievement IS NOT NULL AND category != '' ORDER BY category"
  ).all(userId);
  res.json(cats.map(c => c.category));
});

router.get('/:id', (req, res) => {
  const { id } = req.params;
  const userId = getUserIdOrZero(req);
  const task = db.prepare('SELECT * FROM tasks WHERE id = ? AND user_id = ?').get(id, userId);
  if (!task) return res.status(404).json({ error: 'Task not found' });
  res.json(task);
});

module.exports = router;
