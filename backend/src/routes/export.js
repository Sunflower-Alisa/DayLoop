const express = require('express');
const router = express.Router();
const db = require('../database');
const { getUserId } = require('../middleware/auth');

function getUserIdOrZero(req) {
  return getUserId(req) || 0;
}

router.get('/json', (req, res) => {
  const userId = getUserIdOrZero(req);
  const tasks = db.prepare('SELECT * FROM tasks WHERE user_id = ? ORDER BY date DESC, id').all(userId);
  const notes = db.prepare('SELECT * FROM notes WHERE user_id = ? ORDER BY created_at DESC').all(userId);
  const reviews = db.prepare('SELECT * FROM daily_reviews WHERE user_id = ? ORDER BY date DESC').all(userId);
  const templates = db.prepare('SELECT * FROM recurring_templates WHERE user_id = ? ORDER BY id').all(userId);

  const enrichedNotes = notes.map(n => {
    let task = null;
    if (n.task_id) {
      task = db.prepare('SELECT id, title, date, start_time, end_time, status, category FROM tasks WHERE id = ?').get(n.task_id);
    }
    return { ...n, linked_task: task };
  });

  const exportData = {
    version: '1.0',
    exported_at: new Date().toISOString(),
    tasks,
    notes: enrichedNotes,
    reviews: db.prepare('SELECT * FROM daily_reviews WHERE user_id = ? ORDER BY date DESC').all(userId),
    templates: db.prepare('SELECT * FROM recurring_templates WHERE user_id = ? ORDER BY id').all(userId),
  };

  res.setHeader('Content-Type', 'application/json');
  res.setHeader('Content-Disposition', `attachment; filename=dayloop-export-${new Date().toISOString().slice(0, 10)}.json`);
  res.json(exportData);
});

module.exports = router;