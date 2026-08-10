const express = require('express');
const router = express.Router();
const db = require('../database');
const { getUserId } = require('../middleware/auth');

const today = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};
const addDays = (n) => {
  const d = new Date();
  d.setDate(d.getDate() + n);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};
const userIdOrZero = (req) => getUserId(req) || 0;

function hasLog(userId, date) {
  const r = db.prepare('SELECT COUNT(*) as c FROM learning_logs WHERE user_id = ? AND date = ?').get(userId, date);
  return r.c > 0;
}

function computeStreak(userId) {
  let streak = 0;
  let cursor = today();
  if (!hasLog(userId, today())) cursor = addDays(-1);
  for (let i = 0; i < 366; i++) {
    if (hasLog(userId, cursor)) {
      streak++;
      cursor = addDays(-(i + 1));
    } else {
      break;
    }
  }
  return streak;
}

function count(sql, userId) {
  const r = db.prepare(sql).get(userId);
  return r.c;
}

function sumDuration(userId, start, end) {
  if (!start) {
    const r = db.prepare('SELECT COALESCE(SUM(duration_seconds), 0) as s FROM study_sessions WHERE user_id = ?').get(userId);
    return r.s;
  }
  const r = db.prepare('SELECT COALESCE(SUM(duration_seconds), 0) as s FROM study_sessions WHERE user_id = ? AND date BETWEEN ? AND ?').get(userId, start, end);
  return r.s;
}

function countLog(userId, date, type) {
  const r = db.prepare('SELECT COUNT(*) as c FROM learning_logs WHERE user_id = ? AND date = ? AND type = ?').get(userId, date, type);
  return r.c;
}

function dailyGoal(userId) {
  const row = db.prepare('SELECT value FROM app_settings WHERE key = ?').get(`word_goal_${userId}`);
  const goal = row ? Number(row.value) : 0;
  return goal > 0 ? goal : 20;
}

router.get('/dashboard', (req, res) => {
  const userId = userIdOrZero(req);
  const t = today();
  const dash = {};

  dash.streak = computeStreak(userId);
  dash.checked_in_today = hasLog(userId, t);
  dash.total_words = count('SELECT COUNT(*) as c FROM word_progress WHERE user_id = ?', userId);
  dash.mastered_words = count("SELECT COUNT(*) as c FROM word_progress WHERE user_id = ? AND status = 'mastered'", userId);
  dash.learning_words = count("SELECT COUNT(*) as c FROM word_progress WHERE user_id = ? AND status IN ('learning','reviewing')", userId);
  dash.wrong_count = count('SELECT COUNT(*) as c FROM wrong_words WHERE user_id = ?', userId);
  dash.scenario_count = count('SELECT COUNT(*) as c FROM scenarios WHERE user_id = 0 OR user_id = ?', userId);
  dash.scenario_mastered = count('SELECT COUNT(*) as c FROM scenario_progress WHERE user_id = ? AND mastered = 1', userId);
  dash.clip_count = count('SELECT COUNT(*) as c FROM video_clips WHERE user_id = 0 OR user_id = ?', userId);

  dash.new_goal = dailyGoal(userId);
  dash.new_done = countLog(userId, t, 'new');
  dash.review_done = countLog(userId, t, 'review');

  dash.today_seconds = sumDuration(userId, t, t);
  dash.week_seconds = sumDuration(userId, addDays(-6), t);
  dash.total_seconds = sumDuration(userId, '', '');

  const avg = db.prepare('SELECT COALESCE(AVG(overall), 0) as a FROM speaking_records WHERE user_id = ?').get(userId);
  dash.speaking_avg = Math.round(avg.a || 0);

  res.json(dash);
});

router.get('/streak', (req, res) => {
  const userId = userIdOrZero(req);
  res.json({ streak: computeStreak(userId) });
});

router.post('/sessions', (req, res) => {
  const userId = getUserId(req);
  if (!userId) return res.status(401).json({ error: '未登录' });
  const { module = '', start_time = '', end_time = '', duration_seconds = 0 } = req.body;
  if (duration_seconds <= 0) return res.json({ ok: true });
  db.prepare(
    'INSERT INTO study_sessions (user_id, date, module, start_time, end_time, duration_seconds) VALUES (?, ?, ?, ?, ?, ?)'
  ).run(userId, today(), module, start_time, end_time, duration_seconds);
  res.json({ ok: true });
});

router.get('/sessions', (req, res) => {
  const userId = userIdOrZero(req);
  const t = today();
  res.json({
    today: sumDuration(userId, t, t),
    week: sumDuration(userId, addDays(-6), t),
    total: sumDuration(userId, '', ''),
  });
});

module.exports = router;