const express = require('express');
const router = express.Router();
const db = require('../database');
const { getUserId } = require('../middleware/auth');
const { syncReview } = require('../sync/obsidian');

function getUserIdOrZero(req) {
  return getUserId(req) || 0;
}

router.get('/', (req, res) => {
  const { date } = req.query;
  const userId = getUserIdOrZero(req);
  if (date) {
    const review = db.prepare('SELECT * FROM daily_reviews WHERE user_id = ? AND date = ?').get(userId, date);
    return res.json(review || null);
  }
  res.json(db.prepare('SELECT * FROM daily_reviews WHERE user_id = ? ORDER BY date DESC').all(userId));
});

router.put('/:date', (req, res) => {
  const { date } = req.params;
  const { content } = req.body;
  if (content === undefined) return res.status(400).json({ error: 'content is required' });
  const userId = getUserId(req) || 0;
  const existing = db.prepare('SELECT * FROM daily_reviews WHERE user_id = ? AND date = ?').get(userId, date);
  if (existing) {
    db.prepare("UPDATE daily_reviews SET content = ?, updated_at = datetime('now','localtime') WHERE user_id = ? AND date = ?").run(content, userId, date);
  } else {
    db.prepare('INSERT INTO daily_reviews (date, content, user_id) VALUES (?, ?, ?)').run(date, content, userId);
  }
  const review = db.prepare('SELECT * FROM daily_reviews WHERE user_id = ? AND date = ?').get(userId, date);

  // Sync: find or create "今日复盘" task, mark completed, set achievement
  const reviewTask = db.prepare("SELECT id FROM tasks WHERE user_id = ? AND date = ? AND title = '今日复盘'").get(userId, date);
  if (reviewTask) {
    db.prepare("UPDATE tasks SET status = 'completed', achievement = ?, updated_at = datetime('now','localtime') WHERE id = ?").run(content, reviewTask.id);
  } else {
    db.prepare("INSERT INTO tasks (date, title, status, achievement, is_planned, user_id) VALUES (?, '今日复盘', 'completed', ?, 0, ?)").run(date, content, userId);
  }

  syncReview(review);
  res.json(review);
});

module.exports = router;
