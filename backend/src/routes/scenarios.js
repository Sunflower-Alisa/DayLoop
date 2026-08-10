const express = require('express');
const router = express.Router();
const db = require('../database');
const { getUserId } = require('../middleware/auth');

const userIdOrZero = (req) => getUserId(req) || 0;
const today = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};

router.get('/', (req, res) => {
  const userId = userIdOrZero(req);
  const { category } = req.query;
  let sql = `
    SELECT s.*,
           (SELECT COUNT(*) FROM scenario_lines sl WHERE sl.scenario_id = s.id) as line_count,
           COALESCE((SELECT mastered FROM scenario_progress sp WHERE sp.scenario_id = s.id AND sp.user_id = ?), 0) as mastered
    FROM scenarios s
    WHERE (s.user_id = 0 OR s.user_id = ?)
  `;
  const params = [userId, userId];
  if (category) { sql += ' AND s.category = ?'; params.push(category); }
  sql += ' ORDER BY s.category, s.id';
  res.json(db.prepare(sql).all(...params));
});

router.get('/:id', (req, res) => {
  const userId = userIdOrZero(req);
  const id = Number(req.params.id);
  const scenario = db.prepare(`
    SELECT s.*,
           (SELECT COUNT(*) FROM scenario_lines sl WHERE sl.scenario_id = s.id) as line_count,
           COALESCE((SELECT mastered FROM scenario_progress sp WHERE sp.scenario_id = s.id AND sp.user_id = ?), 0) as mastered
    FROM scenarios s WHERE s.id = ?
  `).get(userId, id);
  if (!scenario) return res.status(404).json({ error: 'Scenario not found' });

  const lines = db.prepare('SELECT * FROM scenario_lines WHERE scenario_id = ? ORDER BY ord').all(id);
  const phrases = db.prepare('SELECT * FROM scenario_phrases WHERE scenario_id = ? ORDER BY id').all(id);
  const quizzes = db.prepare('SELECT * FROM scenario_quizzes WHERE scenario_id = ? ORDER BY id').all(id);
  quizzes.forEach(q => {
    try { q.options = JSON.parse(q.options || '[]'); } catch { q.options = []; }
  });
  res.json({ scenario, lines, phrases, quizzes });
});

router.post('/:id/quiz', (req, res) => {
  const userId = getUserId(req);
  if (!userId) return res.status(401).json({ error: '未登录' });
  const id = Number(req.params.id);
  const { total = 0, correct = 0 } = req.body;
  const rate = total > 0 ? correct / total : 0;
  const mastered = total > 0 && rate >= 0.6 ? 1 : 0;
  db.prepare(`
    INSERT INTO scenario_progress (user_id, scenario_id, mastered, updated_at)
    VALUES (?, ?, ?, datetime('now','localtime'))
    ON CONFLICT(user_id, scenario_id) DO UPDATE SET mastered = excluded.mastered, updated_at = excluded.updated_at
  `).run(userId, id, mastered);
  db.prepare("INSERT INTO learning_logs (user_id, date, type, topic_id, result) VALUES (?, ?, 'scenario', ?, ?)")
    .run(userId, today(), id, mastered === 1 ? 'correct' : 'wrong');
  res.json({ mastered: mastered === 1 });
});

module.exports = router;