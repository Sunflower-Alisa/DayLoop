const express = require('express');
const router = express.Router();
const db = require('../database');
const { getUserId } = require('../middleware/auth');

const Intervals = [1, 2, 4, 7, 15];
const MaxMastered = 'mastered';

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

function currentBookId(userId) {
  const row = db.prepare('SELECT value FROM app_settings WHERE key = ?').get(`word_book_${userId}`);
  if (row && Number(row.value) > 0) return Number(row.value);
  const def = db.prepare('SELECT id FROM word_books WHERE is_default = 1 OR user_id = ? ORDER BY is_default DESC, id LIMIT 1').get(userId);
  return def ? def.id : 0;
}

function dailyGoal(userId) {
  const row = db.prepare('SELECT value FROM app_settings WHERE key = ?').get(`word_goal_${userId}`);
  const goal = row ? Number(row.value) : 0;
  return goal > 0 ? goal : 20;
}

function setSetting(key, value) {
  db.prepare("INSERT INTO app_settings (key, value) VALUES (?, ?) ON CONFLICT(key) DO UPDATE SET value = excluded.value").run(key, value);
}

function countLog(userId, date, type) {
  const r = db.prepare('SELECT COUNT(*) as c FROM learning_logs WHERE user_id = ? AND date = ? AND type = ?').get(userId, date, type);
  return r.c;
}

router.get('/books', (req, res) => {
  const userId = userIdOrZero(req);
  const rows = db.prepare(`
    SELECT b.*,
           (SELECT COUNT(*) FROM words w WHERE w.book_id = b.id) as word_count,
           (SELECT COUNT(*) FROM word_progress wp WHERE wp.word_id IN (SELECT id FROM words WHERE book_id = b.id) AND wp.user_id = @uid) as learned_count,
           (SELECT COUNT(*) FROM word_progress wp WHERE wp.word_id IN (SELECT id FROM words WHERE book_id = b.id) AND wp.user_id = @uid AND wp.status = 'mastered') as mastered_count
    FROM word_books b
    WHERE b.user_id = 0 OR b.user_id = @uid
    ORDER BY b.is_default DESC, b.id
  `).all({ uid: userId });
  const goal = dailyGoal(userId);
  const current = currentBookId(userId);
  rows.forEach(b => { b.daily_goal = goal; });
  const selected = rows.find(b => b.id === current);
  if (selected) selected.daily_goal = goal;
  res.json(rows);
});

router.post('/books', (req, res) => {
  const userId = getUserId(req);
  if (!userId) return res.status(401).json({ error: '未登录' });
  const { name, level, description, cover_color } = req.body;
  if (!name || !name.trim()) return res.status(400).json({ error: 'Name is required' });
  const info = db.prepare(
    'INSERT INTO word_books (name, level, description, cover_color, user_id) VALUES (?, ?, ?, ?, ?)'
  ).run(name.trim(), level || 'beginner', description || '', cover_color || '#4f46e5', userId);
  setSetting(`word_book_${userId}`, String(info.lastInsertRowid));
  res.json({ id: info.lastInsertRowid });
});

router.put('/books/:id/goal', (req, res) => {
  const userId = getUserId(req);
  if (!userId) return res.status(401).json({ error: '未登录' });
  const { daily_goal } = req.body;
  if (!daily_goal || daily_goal < 5 || daily_goal > 200) return res.status(400).json({ error: 'Daily goal must be 5-200' });
  const id = Number(req.params.id);
  db.prepare("INSERT INTO app_settings (key, value) VALUES ('word_goal_' + ?, ?) ON CONFLICT(key) DO UPDATE SET value = excluded.value").run(String(userId), String(daily_goal));
  db.prepare("INSERT INTO app_settings (key, value) VALUES ('word_book_' + ?, ?) ON CONFLICT(key) DO UPDATE SET value = excluded.value").run(String(userId), String(id));
  res.json({ daily_goal: daily_goal });
});

router.get('/books/:id', (req, res) => {
  const userId = userIdOrZero(req);
  const id = Number(req.params.id);
  const sql = `
    SELECT w.id, w.word, w.phonetic, w.pos, w.meaning, w.example_en, w.example_cn, w.image_url, w.audio_url, w.book_id,
           COALESCE(wp.status, 'new') as status,
           COALESCE(wp.stage, 0) as stage,
           (SELECT COUNT(*) FROM wrong_words ww WHERE ww.word_id = w.id AND ww.user_id = ?) as in_wrong
    FROM words w
    LEFT JOIN word_progress wp ON wp.word_id = w.id AND wp.user_id = ?
    WHERE w.book_id = ?
    ORDER BY w.id
  `;
  const words = db.prepare(sql).all(userId, userId, id);
  res.json({ book_id: id, words });
});

router.get('/daily', (req, res) => {
  const userId = userIdOrZero(req);
  const bookId = currentBookId(userId);
  const goal = dailyGoal(userId);
  const t = today();

  const newDone = countLog(userId, t, 'new');
  const reviewDone = countLog(userId, t, 'review');
  const result = { has_book: bookId > 0, new_goal: goal, new_done: newDone, review_done: reviewDone, new_words: [], review_words: [] };

  if (bookId === 0) return res.json(result);

  result.review_words = db.prepare(`
    SELECT w.id, w.word, w.phonetic, w.pos, w.meaning, w.example_en, w.example_cn, w.image_url, w.audio_url, w.book_id,
           wp.status, wp.stage,
           (SELECT COUNT(*) FROM wrong_words ww WHERE ww.word_id = w.id AND ww.user_id = ?) as in_wrong
    FROM word_progress wp
    JOIN words w ON w.id = wp.word_id
    WHERE wp.user_id = ? AND wp.status IN ('learning','reviewing') AND wp.next_review_at <= ?
    ORDER BY wp.next_review_at ASC
    LIMIT 30
  `).all(userId, userId, t);

  const remainNew = Math.max(0, goal - newDone);
  if (remainNew > 0) {
    result.new_words = db.prepare(`
      SELECT w.id, w.word, w.phonetic, w.pos, w.meaning, w.example_en, w.example_cn, w.image_url, w.audio_url, w.book_id,
             'new' as status, 0 as stage,
             (SELECT COUNT(*) FROM wrong_words ww WHERE ww.word_id = w.id AND ww.user_id = ?) as in_wrong
      FROM words w
      WHERE w.book_id = ? AND NOT EXISTS (SELECT 1 FROM word_progress wp WHERE wp.word_id = w.id AND wp.user_id = ?)
      ORDER BY w.id
      LIMIT ?
    `).all(userId, bookId, userId, remainNew);
  }
  res.json(result);
});

router.post('/learn', (req, res) => {
  const userId = getUserId(req);
  if (!userId) return res.status(401).json({ error: '未登录' });
  const { word_id, correct, is_review, know } = req.body;
  const t = today();

  const existing = db.prepare('SELECT status, stage FROM word_progress WHERE user_id = ? AND word_id = ?').get(userId, word_id);
  const exists = !!existing;
  const status = existing ? existing.status : '';
  const stage = existing ? existing.stage : 0;

  let newStatus, newStage;
  if (know) { newStatus = MaxMastered; newStage = 5; }
  else if (!exists || status === 'learning') { newStatus = correct ? 'reviewing' : 'learning'; newStage = 0; }
  else if (status === 'reviewing') {
    if (correct) {
      newStage = stage + 1;
      newStatus = newStage >= Intervals.length ? MaxMastered : 'reviewing';
    } else { newStatus = 'learning'; newStage = 0; }
  } else { newStatus = status; newStage = stage; }

  const nextReview = newStatus === MaxMastered ? '' : addDays(Intervals[Math.min(newStage, Intervals.length - 1)]);
  const wrong = !correct && !know;

  db.prepare(`
    INSERT INTO word_progress (user_id, word_id, status, stage, correct_streak, wrong_count, last_review_at, next_review_at)
    VALUES (?, ?, ?, ?, 0, ?, ?, ?)
    ON CONFLICT(user_id, word_id) DO UPDATE SET
      status = excluded.status,
      stage = excluded.stage,
      correct_streak = CASE WHEN excluded.stage = 0 THEN 0 ELSE word_progress.correct_streak + 1 END,
      wrong_count = word_progress.wrong_count + excluded.wrong_count,
      last_review_at = excluded.last_review_at,
      next_review_at = excluded.next_review_at
  `).run(userId, word_id, newStatus, newStage, wrong ? 1 : 0, t, nextReview);

  if (wrong) {
    db.prepare('INSERT OR IGNORE INTO wrong_words (user_id, word_id) VALUES (?, ?)').run(userId, word_id);
  } else {
    db.prepare('DELETE FROM wrong_words WHERE user_id = ? AND word_id = ?').run(userId, word_id);
  }

  db.prepare('INSERT INTO learning_logs (user_id, date, type, word_id, result) VALUES (?, ?, ?, ?, ?)')
    .run(userId, t, is_review ? 'review' : 'new', word_id, know ? 'know' : (correct ? 'correct' : 'wrong'));

  res.json({ word_id, ok: true });
});

router.get('/wrong', (req, res) => {
  const userId = userIdOrZero(req);
  const words = db.prepare(`
    SELECT w.id, w.word, w.phonetic, w.pos, w.meaning, w.example_en, w.example_cn, w.image_url, w.audio_url, w.book_id,
           COALESCE(wp.status, 'learning') as status, COALESCE(wp.stage, 0) as stage, 1 as in_wrong
    FROM wrong_words ww JOIN words w ON w.id = ww.word_id
    LEFT JOIN word_progress wp ON wp.word_id = w.id AND wp.user_id = ?
    WHERE ww.user_id = ?
    ORDER BY ww.created_at DESC
  `).all(userId, userId);
  res.json(words);
});

router.delete('/wrong/:wordId', (req, res) => {
  const userId = getUserId(req);
  if (!userId) return res.status(401).json({ error: '未登录' });
  db.prepare('DELETE FROM wrong_words WHERE user_id = ? AND word_id = ?').run(userId, Number(req.params.wordId));
  res.json({ ok: true });
});

router.get('/:id', (req, res) => {
  const userId = userIdOrZero(req);
  const id = Number(req.params.id);
  const word = db.prepare(`
    SELECT w.id, w.word, w.phonetic, w.pos, w.meaning, w.example_en, w.example_cn, w.image_url, w.audio_url, w.book_id,
           COALESCE(wp.status, 'new') as status, COALESCE(wp.stage, 0) as stage,
           (SELECT COUNT(*) FROM wrong_words ww WHERE ww.word_id = w.id AND ww.user_id = ?) as in_wrong
    FROM words w LEFT JOIN word_progress wp ON wp.word_id = w.id AND wp.user_id = ?
    WHERE w.id = ?
  `).get(userId, userId, id);
  if (!word) return res.status(404).json({ error: 'Word not found' });
  res.json(word);
});

module.exports = router;