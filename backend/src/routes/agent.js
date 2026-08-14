const express = require('express');
const router = express.Router();
const db = require('../database');

// §43 服务间认证：Agent Service 通过 Service Token 调用，user_id 由调用方（可信 Agent）传递。
const SERVICE_TOKEN = process.env.DAYLOOP_SERVICE_TOKEN || '';

function requireServiceToken(req, res, next) {
  if (!SERVICE_TOKEN) {
    return res.status(503).json({ error: 'DAYLOOP_SERVICE_TOKEN 未配置，Agent API 已禁用' });
  }
  const auth = req.headers.authorization || '';
  if (auth !== `Bearer ${SERVICE_TOKEN}`) {
    return res.status(401).json({ error: 'Invalid service token' });
  }
  next();
}

router.use(requireServiceToken);

function parseUserId(req) {
  const raw = req.query.user_id ?? (req.body && req.body.user_id);
  const id = parseInt(String(raw), 10);
  return Number.isFinite(id) ? id : 0;
}

function now() {
  return new Date().toLocaleString('sv-SE', { hour12: false });
}

// ---- Profile ----
router.get('/profile', (req, res) => {
  const userId = parseUserId(req);
  const row = db.prepare('SELECT * FROM user_profiles WHERE user_id = ?').get(userId);
  res.json(row || { user_id: userId });
});

router.put('/profile', (req, res) => {
  const userId = parseUserId(req);
  const fields = ['name', 'title', 'bio', 'company', 'location', 'email', 'phone', 'github', 'linkedin', 'website'];
  const existing = db.prepare('SELECT id FROM user_profiles WHERE user_id = ?').get(userId);
  const cur = existing ? db.prepare('SELECT * FROM user_profiles WHERE id = ?').get(existing.id) : {};
  const vals = {};
  for (const f of fields) {
    vals[f] = req.body[f] !== undefined ? req.body[f] : (cur[f] || '');
  }
  if (existing) {
    db.prepare(
      `UPDATE user_profiles SET name=?, title=?, bio=?, company=?, location=?, email=?, phone=?, github=?, linkedin=?, website=?, updated_at=? WHERE id=?`
    ).run(vals.name, vals.title, vals.bio, vals.company, vals.location, vals.email, vals.phone, vals.github, vals.linkedin, vals.website, now(), existing.id);
  } else {
    db.prepare(
      `INSERT INTO user_profiles (user_id, name, title, bio, company, location, email, phone, github, linkedin, website, updated_at) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)`
    ).run(userId, vals.name, vals.title, vals.bio, vals.company, vals.location, vals.email, vals.phone, vals.github, vals.linkedin, vals.website, now());
  }
  res.json(db.prepare('SELECT * FROM user_profiles WHERE user_id = ?').get(userId));
});

// ---- Resume ----
router.get('/resume', (req, res) => {
  const userId = parseUserId(req);
  const row = db.prepare('SELECT * FROM resumes WHERE user_id = ? ORDER BY version DESC LIMIT 1').get(userId);
  res.json(row || { user_id: userId, content: '', version: 0 });
});

router.put('/resume', (req, res) => {
  const userId = parseUserId(req);
  const content = String(req.body.content || '');
  const latest = db.prepare('SELECT * FROM resumes WHERE user_id = ? ORDER BY version DESC LIMIT 1').get(userId);
  const version = (latest ? latest.version : 0) + 1;
  const result = db.prepare(
    'INSERT INTO resumes (user_id, content, version, updated_at) VALUES (?, ?, ?, ?)'
  ).run(userId, content, version, now());
  res.json(db.prepare('SELECT * FROM resumes WHERE id = ?').get(result.lastInsertRowid));
});

// ---- Skills ----
router.get('/skills', (req, res) => {
  const userId = parseUserId(req);
  const rows = db.prepare('SELECT * FROM skills WHERE user_id = ? ORDER BY level DESC, id').all(userId);
  res.json(rows);
});

router.post('/skills', (req, res) => {
  const userId = parseUserId(req);
  const skill = String(req.body.skill || '').trim();
  if (!skill) return res.status(400).json({ error: 'skill is required' });
  const level = String(req.body.level || 'intermediate');
  const category = String(req.body.category || '');
  const existing = db.prepare('SELECT * FROM skills WHERE user_id = ? AND skill = ?').get(userId, skill);
  if (existing) {
    db.prepare('UPDATE skills SET level=?, category=?, updated_at=? WHERE id=?').run(level, category, now(), existing.id);
    return res.json(db.prepare('SELECT * FROM skills WHERE id = ?').get(existing.id));
  }
  const result = db.prepare(
    'INSERT INTO skills (user_id, skill, level, category, updated_at) VALUES (?, ?, ?, ?, ?)'
  ).run(userId, skill, level, category, now());
  res.status(201).json(db.prepare('SELECT * FROM skills WHERE id = ?').get(result.lastInsertRowid));
});

router.put('/skills/:id', (req, res) => {
  const userId = parseUserId(req);
  const row = db.prepare('SELECT * FROM skills WHERE id = ? AND user_id = ?').get(req.params.id, userId);
  if (!row) return res.status(404).json({ error: 'Skill not found' });
  const level = req.body.level !== undefined ? req.body.level : row.level;
  const category = req.body.category !== undefined ? req.body.category : row.category;
  const skill = req.body.skill !== undefined ? req.body.skill : row.skill;
  db.prepare('UPDATE skills SET skill=?, level=?, category=?, updated_at=? WHERE id=?').run(skill, level, category, now(), row.id);
  res.json(db.prepare('SELECT * FROM skills WHERE id = ?').get(row.id));
});

// ---- Tasks（复用 tasks 表，按 §9 契约提供）----
router.get('/tasks', (req, res) => {
  const userId = parseUserId(req);
  const { date } = req.query;
  if (date) {
    return res.json(db.prepare('SELECT * FROM tasks WHERE user_id = ? AND date = ? ORDER BY is_planned DESC, priority, start_time, id').all(userId, date));
  }
  res.json(db.prepare('SELECT * FROM tasks WHERE user_id = ? ORDER BY date DESC, is_planned DESC, priority, start_time, id').all(userId));
});

router.post('/tasks', (req, res) => {
  const userId = parseUserId(req);
  const title = String(req.body.title || '').trim();
  if (!title) return res.status(400).json({ error: 'title is required' });
  const date = String(req.body.date || new Date().toLocaleDateString('en-CA'));
  const result = db.prepare(
    `INSERT INTO tasks (date, title, start_time, end_time, planned_duration, category, priority, note, is_recurring, is_planned, achievement, sync_enabled, planned_days, overall_status, user_id)
     VALUES (?, ?, ?, ?, ?, ?, ?, ?, 0, 1, ?, ?, ?, 'pending', ?)`
  ).run(
    date, title,
    req.body.start_time || '', req.body.end_time || '',
    parseInt(req.body.planned_duration, 10) || 0,
    req.body.category || '', parseInt(req.body.priority, 10) || 2,
    req.body.note || '', req.body.achievement || '',
    req.body.sync_enabled !== undefined ? (req.body.sync_enabled ? 1 : 0) : 1,
    parseInt(req.body.planned_days, 10) || 1, userId
  );
  res.status(201).json(db.prepare('SELECT * FROM tasks WHERE id = ?').get(result.lastInsertRowid));
});

router.put('/tasks/:id', (req, res) => {
  const userId = parseUserId(req);
  const row = db.prepare('SELECT * FROM tasks WHERE id = ? AND user_id = ?').get(req.params.id, userId);
  if (!row) return res.status(404).json({ error: 'Task not found' });
  const setters = [];
  const vals = [];
  for (const f of ['date', 'title', 'start_time', 'end_time', 'category', 'priority', 'note', 'status']) {
    if (req.body[f] !== undefined) {
      setters.push(`${f} = ?`);
      vals.push(req.body[f]);
    }
  }
  if (setters.length) {
    db.prepare(`UPDATE tasks SET ${setters.join(', ')}, updated_at = ? WHERE id = ?`).run(...vals, now(), row.id);
  }
  res.json(db.prepare('SELECT * FROM tasks WHERE id = ?').get(row.id));
});

// ---- Learning history ----
router.get('/learning/history', (req, res) => {
  const userId = parseUserId(req);
  const items = [];
  const tasks = db.prepare(
    "SELECT date, title, category, status, note, updated_at FROM tasks WHERE user_id = ? AND status = 'completed' ORDER BY date DESC LIMIT 20"
  ).all(userId);
  for (const t of tasks) items.push({ type: 'task', date: t.date, title: t.title, category: t.category, status: t.status, note: t.note, updated_at: t.updated_at });
  const reviews = db.prepare('SELECT date, content FROM daily_reviews WHERE user_id = ? ORDER BY date DESC LIMIT 10').all(userId);
  for (const r of reviews) items.push({ type: 'review', date: r.date, content: r.content });
  const summaries = db.prepare('SELECT type, period_key, auto_summary FROM summaries WHERE user_id = ? ORDER BY created_at DESC LIMIT 10').all(userId);
  for (const s of summaries) items.push({ type: 'summary', period: s.period_key, content: s.auto_summary, period_type: s.type });
  items.sort((a, b) => String(b.date || '').localeCompare(String(a.date || '')));
  res.json(items);
});

// ---- Jobs ----
router.get('/jobs', (req, res) => {
  const userId = parseUserId(req);
  const { status, search } = req.query;
  if (status) {
    return res.json(db.prepare('SELECT * FROM jobs WHERE user_id = ? AND status = ? ORDER BY created_at DESC, id DESC').all(userId, status));
  }
  if (search) {
    return res.json(db.prepare('SELECT * FROM jobs WHERE user_id = ? AND (title LIKE ? OR company LIKE ? OR description LIKE ?) ORDER BY created_at DESC, id DESC').all(userId, `%${search}%`, `%${search}%`, `%${search}%`));
  }
  res.json(db.prepare('SELECT * FROM jobs WHERE user_id = ? ORDER BY created_at DESC, id DESC').all(userId));
});

router.get('/jobs/:id', (req, res) => {
  const userId = parseUserId(req);
  const row = db.prepare('SELECT * FROM jobs WHERE id = ? AND user_id = ?').get(req.params.id, userId);
  if (!row) return res.status(404).json({ error: 'Job not found' });
  res.json(row);
});

router.post('/jobs', (req, res) => {
  const userId = parseUserId(req);
  const job = req.body.job || req.body;
  const title = String(job.title || job.job_title || '').trim();
  if (!title) return res.status(400).json({ error: 'job.title is required' });
  const result = db.prepare(
    `INSERT INTO jobs (user_id, title, company, city, salary, url, description, requirements, skills, status, source, updated_at)
     VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`
  ).run(
    userId, title, job.company || '', job.city || '', job.salary || '', job.url || '',
    job.description || '', job.requirements || '', job.skills || '',
    job.status || 'saved', job.source || 'agent', now()
  );
  res.status(201).json(db.prepare('SELECT * FROM jobs WHERE id = ?').get(result.lastInsertRowid));
});

router.put('/jobs/:id', (req, res) => {
  const userId = parseUserId(req);
  const row = db.prepare('SELECT * FROM jobs WHERE id = ? AND user_id = ?').get(req.params.id, userId);
  if (!row) return res.status(404).json({ error: 'Job not found' });
  const job = req.body.job || req.body;
  const setters = [];
  const vals = [];
  for (const f of ['title', 'company', 'city', 'salary', 'url', 'description', 'requirements', 'skills', 'status', 'source']) {
    if (job[f] !== undefined) {
      setters.push(`${f} = ?`);
      vals.push(job[f]);
    }
  }
  if (setters.length) {
    db.prepare(`UPDATE jobs SET ${setters.join(', ')}, updated_at = ? WHERE id = ?`).run(...vals, now(), row.id);
  }
  res.json(db.prepare('SELECT * FROM jobs WHERE id = ?').get(row.id));
});

// ---- Interviews ----
router.post('/interviews', (req, res) => {
  const userId = parseUserId(req);
  const jobId = parseInt(req.body.job_id, 10) || null;
  const mode = String(req.body.mode || 'agent');
  const result = db.prepare(
    'INSERT INTO interview_sessions (user_id, job_id, mode, status) VALUES (?, ?, ?, ?)'
  ).run(userId, jobId, mode, 'in_progress');
  res.status(201).json(db.prepare('SELECT * FROM interview_sessions WHERE id = ?').get(result.lastInsertRowid));
});

router.get('/interviews/:id', (req, res) => {
  // GET /interviews/:id 契约不带 user_id，Agent 经 Service Token 鉴权后可信
  const row = db.prepare('SELECT * FROM interview_sessions WHERE id = ?').get(req.params.id);
  if (!row) return res.status(404).json({ error: 'Interview not found' });
  const answers = db.prepare('SELECT * FROM interview_answers WHERE interview_id = ? ORDER BY id').all(row.id);
  res.json({ ...row, answers });
});

router.post('/interviews/:id/answer', (req, res) => {
  // 契约：POST /interviews/{id}/answer {answer}，user_id 由会话记录决定
  const row = db.prepare('SELECT * FROM interview_sessions WHERE id = ?').get(req.params.id);
  if (!row) return res.status(404).json({ error: 'Interview not found' });
  const answer = String(req.body.answer || '');
  const question = String(req.body.question || '');
  const result = db.prepare(
    'INSERT INTO interview_answers (interview_id, question, answer) VALUES (?, ?, ?)'
  ).run(row.id, question, answer);
  res.status(201).json(db.prepare('SELECT * FROM interview_answers WHERE id = ?').get(result.lastInsertRowid));
});

router.post('/interviews/:id/finish', (req, res) => {
  const row = db.prepare('SELECT * FROM interview_sessions WHERE id = ?').get(req.params.id);
  if (!row) return res.status(404).json({ error: 'Interview not found' });
  db.prepare('UPDATE interview_sessions SET status = ?, finished_at = ? WHERE id = ?').run('completed', now(), row.id);
  res.json(db.prepare('SELECT * FROM interview_sessions WHERE id = ?').get(row.id));
});

// ---- Knowledge ----
router.get('/knowledge', (req, res) => {
  const userId = parseUserId(req);
  const { category, search } = req.query;
  if (category) {
    return res.json(db.prepare('SELECT * FROM knowledge WHERE user_id = ? AND category = ? ORDER BY updated_at DESC, id DESC').all(userId, category));
  }
  if (search) {
    return res.json(db.prepare('SELECT * FROM knowledge WHERE user_id = ? AND (title LIKE ? OR content LIKE ?) ORDER BY updated_at DESC, id DESC').all(userId, `%${search}%`, `%${search}%`));
  }
  res.json(db.prepare('SELECT * FROM knowledge WHERE user_id = ? ORDER BY updated_at DESC, id DESC').all(userId));
});

router.post('/knowledge', (req, res) => {
  const userId = parseUserId(req);
  const title = String(req.body.title || '').trim();
  if (!title) return res.status(400).json({ error: 'title is required' });
  const result = db.prepare(
    'INSERT INTO knowledge (user_id, title, content, category, source, updated_at) VALUES (?, ?, ?, ?, ?, ?)'
  ).run(userId, title, String(req.body.content || ''), String(req.body.category || ''), String(req.body.source || 'agent'), now());
  res.status(201).json(db.prepare('SELECT * FROM knowledge WHERE id = ?').get(result.lastInsertRowid));
});

router.put('/knowledge/:id', (req, res) => {
  const userId = parseUserId(req);
  const row = db.prepare('SELECT * FROM knowledge WHERE id = ? AND user_id = ?').get(req.params.id, userId);
  if (!row) return res.status(404).json({ error: 'Knowledge not found' });
  const setters = [];
  const vals = [];
  for (const f of ['title', 'content', 'category', 'source']) {
    if (req.body[f] !== undefined) {
      setters.push(`${f} = ?`);
      vals.push(req.body[f]);
    }
  }
  if (setters.length) {
    db.prepare(`UPDATE knowledge SET ${setters.join(', ')}, updated_at = ? WHERE id = ?`).run(...vals, now(), row.id);
  }
  res.json(db.prepare('SELECT * FROM knowledge WHERE id = ?').get(row.id));
});

module.exports = router;
