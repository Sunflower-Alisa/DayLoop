const express = require('express');
const router = express.Router();
const db = require('../database');
const { getUserId } = require('../middleware/auth');

function getUserIdOrZero(req) {
  return getUserId(req) || 0;
}

router.get('/', (req, res) => {
  const userId = getUserIdOrZero(req);
  res.json(db.prepare('SELECT * FROM recurring_templates WHERE user_id = ? ORDER BY start_time, id').all(userId));
});

router.post('/', (req, res) => {
  const userId = getUserId(req) || 0;
  const { title, start_time, end_time, planned_duration, category, priority, note, recurrence_type, recurrence_days, recurring_enabled, sync_enabled, planned_days } = req.body;
  if (!title) return res.status(400).json({ error: 'title is required' });
  const stmt = db.prepare(
    `INSERT INTO recurring_templates (title, start_time, end_time, planned_duration, category, priority, note, user_id, recurrence_type, recurrence_days, recurring_enabled, sync_enabled, planned_days)
     VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`
  );
  const result = stmt.run(title, start_time || '', end_time || '', planned_duration || 0, category || '', priority || 2, note || '', userId, recurrence_type || 'daily', recurrence_days || '', recurring_enabled == null ? 1 : recurring_enabled ? 1 : 0, sync_enabled == null ? 1 : sync_enabled ? 1 : 0, planned_days || 1);
  const template = db.prepare('SELECT * FROM recurring_templates WHERE id = ?').get(result.lastInsertRowid);
  res.status(201).json(template);
});

router.put('/:id', (req, res) => {
  const { id } = req.params;
  const userId = getUserId(req) || 0;
  const { title, start_time, end_time, planned_duration, category, priority, note, recurrence_type, recurrence_days, recurring_enabled, sync_enabled, planned_days } = req.body;
  db.prepare(`
    UPDATE recurring_templates SET
      title = COALESCE(?, title),
      start_time = COALESCE(?, start_time),
      end_time = COALESCE(?, end_time),
      planned_duration = COALESCE(?, planned_duration),
      category = COALESCE(?, category),
      priority = COALESCE(?, priority),
      note = COALESCE(?, note),
      recurrence_type = COALESCE(?, recurrence_type),
      recurrence_days = COALESCE(?, recurrence_days),
      recurring_enabled = COALESCE(?, recurring_enabled),
      sync_enabled = COALESCE(?, sync_enabled),
      planned_days = COALESCE(?, planned_days)
    WHERE id = ? AND user_id = ?
  `).run(title ?? null, start_time ?? null, end_time ?? null, planned_duration ?? null, category ?? null, priority ?? null, note ?? null, recurrence_type ?? null, recurrence_days ?? null, recurring_enabled ?? null, sync_enabled ?? null, planned_days ?? null, id, userId);
  const template = db.prepare('SELECT * FROM recurring_templates WHERE id = ?').get(id);
  if (!template) return res.status(404).json({ error: 'Template not found' });
  res.json(template);
});

router.delete('/:id', (req, res) => {
  const { id } = req.params;
  const userId = getUserId(req) || 0;
  db.prepare('DELETE FROM recurring_templates WHERE id = ? AND user_id = ?').run(id, userId);
  res.json({ message: 'Template deleted' });
});

router.post('/generate', (req, res) => {
  const { date } = req.body;
  const userId = getUserId(req) || 0;
  if (!date) return res.status(400).json({ error: 'date is required' });
  const templates = db.prepare('SELECT * FROM recurring_templates WHERE user_id = ?').all(userId);
  const dateDow = new Date(date + 'T00:00:00').getDay(); // 0=Sunday
  const created = [];
  for (const t of templates) {
    if (!t.recurring_enabled) continue;
    if (t.recurrence_type === 'weekly') {
      const days = (t.recurrence_days || '').split(',').map(s => s.trim()).filter(Boolean);
      if (!days.includes(String(dateDow))) continue;
    }
    const existing = db.prepare('SELECT id FROM tasks WHERE user_id = ? AND date = ? AND recurring_template_id = ?').get(userId, date, t.id);
    if (existing) continue;
    const tPlannedDays = t.planned_days || 1;
    const taskCount = db.prepare("SELECT COUNT(DISTINCT date) as cnt FROM tasks WHERE user_id = ? AND recurring_template_id = ? AND status != 'cancelled'").get(userId, t.id);
    if (taskCount && taskCount.cnt >= tPlannedDays) continue;
    const result = db.prepare(
      `INSERT INTO tasks (date, title, start_time, end_time, planned_duration, category, priority, note, is_recurring, recurring_template_id, user_id, sync_enabled, planned_days, overall_status)
       VALUES (?, ?, ?, ?, ?, ?, ?, ?, 1, ?, ?, ?, ?, 'pending')`
    ).run(date, t.title, t.start_time, t.end_time, t.planned_duration, t.category, t.priority, t.note, t.id, userId, t.sync_enabled ? 1 : 0, tPlannedDays);
    created.push(result.lastInsertRowid);
  }
  if (created.length === 0) {
    return res.json([]);
  }
  const tasks = db.prepare('SELECT * FROM tasks WHERE id IN (' + created.map(() => '?').join(',') + ')').all(...created);
  res.json(tasks);
});

module.exports = router;
