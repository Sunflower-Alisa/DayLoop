const express = require('express');
const router = express.Router();
const db = require('../database');
const { getUserId } = require('../middleware/auth');

const userIdOrZero = (req) => getUserId(req) || 0;

router.get('/topics', (req, res) => {
  const userId = userIdOrZero(req);
  const { category } = req.query;
  let sql = `
    SELECT t.*,
           (SELECT COALESCE(MAX(overall), 0) FROM speaking_records sr WHERE sr.topic_id = t.id AND sr.user_id = ?) as best_score,
           (SELECT COUNT(*) FROM speaking_records sr WHERE sr.topic_id = t.id AND sr.user_id = ?) as practice_count
    FROM speaking_topics t
    WHERE (t.user_id = 0 OR t.user_id = ?)
  `;
  const params = [userId, userId, userId];
  if (category) { sql += ' AND t.category = ?'; params.push(category); }
  sql += ' ORDER BY t.id';
  const topics = db.prepare(sql).all(...params);
  topics.forEach(t => {
    try { t.lines = JSON.parse(t.lines || '[]'); } catch { t.lines = []; }
  });
  res.json(topics);
});

router.get('/topics/:id', (req, res) => {
  const id = Number(req.params.id);
  const topic = db.prepare('SELECT * FROM speaking_topics WHERE id = ?').get(id);
  if (!topic) return res.status(404).json({ error: 'Topic not found' });
  try { topic.lines = JSON.parse(topic.lines || '[]'); } catch { topic.lines = []; }
  res.json(topic);
});

router.post('/records', (req, res) => {
  const userId = getUserId(req);
  if (!userId) return res.status(401).json({ error: '未登录' });
  const { topic_id, line_index = 0, audio_url = '', accuracy = 0, fluency = 0, completeness = 0, overall = 0 } = req.body;
  db.prepare(
    `INSERT INTO speaking_records (user_id, topic_id, line_index, audio_url, accuracy, fluency, completeness, overall)
     VALUES (?, ?, ?, ?, ?, ?, ?, ?)`
  ).run(userId, topic_id, line_index, audio_url, accuracy, fluency, completeness, overall);
  res.json({ ok: true });
});

router.get('/records', (req, res) => {
  const userId = userIdOrZero(req);
  const records = db.prepare(
    `SELECT id, topic_id, line_index, accuracy, fluency, completeness, overall, created_at
     FROM speaking_records WHERE user_id = ? ORDER BY created_at DESC LIMIT 100`
  ).all(userId);
  res.json(records);
});

module.exports = router;