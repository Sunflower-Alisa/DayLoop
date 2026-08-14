const express = require('express');
const router = express.Router();
const db = require('../database');
const { getUserId } = require('../middleware/auth');
const { syncAchievement } = require('../sync/obsidian');

function getUserIdOrZero(req) {
  return getUserId(req) || 0;
}

router.get('/', (req, res) => {
  const { date, search } = req.query;
  const userId = getUserIdOrZero(req);
  if (date && search) {
    const tasks = db.prepare('SELECT * FROM tasks WHERE user_id = ? AND date = ? AND (title LIKE ? OR note LIKE ?) ORDER BY is_planned DESC, priority, start_time, id').all(userId, date, `%${search}%`, `%${search}%`);
    res.json(tasks);
  } else if (date) {
    const tasks = db.prepare('SELECT * FROM tasks WHERE user_id = ? AND date = ? ORDER BY is_planned DESC, priority, start_time, id').all(userId, date);
    res.json(tasks);
  } else if (search) {
    res.json(db.prepare("SELECT * FROM tasks WHERE user_id = ? AND (title LIKE ? OR note LIKE ?) ORDER BY date DESC, is_planned DESC, priority, start_time, id").all(userId, `%${search}%`, `%${search}%`));
  } else {
    res.json(db.prepare('SELECT * FROM tasks WHERE user_id = ? ORDER BY date DESC, is_planned DESC, priority, start_time, id').all(userId));
  }
});

router.get('/range', (req, res) => {
  const userId = getUserIdOrZero(req);
  const { start, end } = req.query;
  if (!start || !end) return res.status(400).json({ error: 'start and end are required' });
  const tasks = db.prepare('SELECT * FROM tasks WHERE user_id = ? AND date >= ? AND date <= ? ORDER BY date, start_time').all(userId, start, end);
  res.json(tasks);
});

router.post('/', (req, res) => {
  const userId = getUserId(req) || 0;
  const { date, title, start_time, end_time, planned_duration, category, priority, note, is_recurring, is_planned, achievement, note_id, sync_enabled, planned_days } = req.body;
  if (!date || !title) {
    return res.status(400).json({ error: 'date and title are required' });
  }
  const stmt = db.prepare(
    `INSERT INTO tasks (date, title, start_time, end_time, planned_duration, category, priority, note, is_recurring, is_planned, achievement, note_id, sync_enabled, planned_days, overall_status, user_id)
     VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`
  );
  const result = stmt.run(
    date, title || '', start_time || '', end_time || '', planned_duration || 0,
    category || '', priority || 2, note || '',
    is_recurring ? 1 : 0, is_planned !== undefined ? (is_planned ? 1 : 0) : 1,
    achievement || '', note_id || null,
    sync_enabled !== undefined ? (sync_enabled ? 1 : 0) : 1, planned_days || 1, 'pending', userId
  );
  const taskId = result.lastInsertRowid;

  if (is_recurring) {
    const existing = db.prepare('SELECT id FROM recurring_templates WHERE user_id = ? AND title = ?').get(userId, title);
    if (!existing) {
      db.prepare(
        `INSERT INTO recurring_templates (title, start_time, end_time, planned_duration, category, priority, note, user_id, planned_days, sync_enabled)
         VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`
      ).run(title, start_time || '', end_time || '', planned_duration || 0, category || '', priority || 2, note || '', userId, planned_days || 1, sync_enabled !== undefined ? (sync_enabled ? 1 : 0) : 1);
    }
    const tmpl = db.prepare('SELECT id FROM recurring_templates WHERE user_id = ? AND title = ?').get(userId, title);
    if (tmpl) {
      db.prepare('UPDATE tasks SET recurring_template_id = ? WHERE id = ?').run(tmpl.id, taskId);
    }
  }

  if (note_id) {
    db.prepare('UPDATE notes SET task_id = ? WHERE id = ?').run(taskId, note_id);
  }
  const task = db.prepare('SELECT * FROM tasks WHERE id = ?').get(taskId);
  res.status(201).json(task);
});

router.put('/:id', (req, res) => {
  const { id } = req.params;
  const userId = getUserId(req) || 0;
  const existing = db.prepare('SELECT * FROM tasks WHERE id = ? AND user_id = ?').get(id, userId);
  if (!existing) return res.status(404).json({ error: 'Task not found' });

  const { title, date, start_time, end_time, planned_duration, actual_duration, actual_start, actual_end, status, category, priority, note, is_recurring, is_planned, achievement, sync_enabled, planned_days, overall_status } = req.body;
  const stmt = db.prepare(`
    UPDATE tasks SET
      title = COALESCE(?, title),
      date = COALESCE(?, date),
      start_time = COALESCE(?, start_time),
      end_time = COALESCE(?, end_time),
      planned_duration = COALESCE(?, planned_duration),
      actual_duration = COALESCE(?, actual_duration),
      actual_start = COALESCE(?, actual_start),
      actual_end = COALESCE(?, actual_end),
      status = COALESCE(?, status),
      category = COALESCE(?, category),
      priority = COALESCE(?, priority),
      note = COALESCE(?, note),
      is_recurring = COALESCE(?, is_recurring),
      is_planned = COALESCE(?, is_planned),
      achievement = COALESCE(?, achievement),
      sync_enabled = COALESCE(?, sync_enabled),
      planned_days = COALESCE(?, planned_days),
      overall_status = COALESCE(?, overall_status),
      updated_at = datetime('now','localtime')
    WHERE id = ? AND user_id = ?
  `);
  stmt.run(
    title, date, start_time, end_time, planned_duration, actual_duration, actual_start, actual_end,
    status, category, priority, note,
    is_recurring !== undefined ? (is_recurring ? 1 : 0) : undefined,
    is_planned !== undefined ? (is_planned ? 1 : 0) : undefined,
    achievement,
    sync_enabled !== undefined ? (sync_enabled ? 1 : 0) : undefined,
    planned_days,
    overall_status,
    id, existing ? existing.user_id : 0
  );
  const task = db.prepare('SELECT * FROM tasks WHERE id = ?').get(id);
  if (!task) return res.status(404).json({ error: 'Task not found' });
  syncAchievement();

  if (is_recurring !== undefined && is_recurring) {
    const existing = db.prepare('SELECT id FROM recurring_templates WHERE user_id = ? AND title = ?').get(userId, task.title);
    if (!existing) {
      db.prepare(
        `INSERT INTO recurring_templates (title, start_time, end_time, planned_duration, category, priority, note, user_id, planned_days, sync_enabled)
         VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`
      ).run(task.title, task.start_time, task.end_time, task.planned_duration, task.category, task.priority, task.note, userId, task.planned_days || 1, task.sync_enabled ? 1 : 0);
    }
    const tmpl = db.prepare('SELECT id FROM recurring_templates WHERE user_id = ? AND title = ?').get(userId, task.title);
    if (tmpl) {
      db.prepare('UPDATE tasks SET recurring_template_id = ? WHERE id = ?').run(tmpl.id, id);
    }
  }

  const { note_id } = req.body;
  if (note_id !== undefined) {
    // Read old note_id BEFORE updating the task
    const oldNote = db.prepare('SELECT note_id FROM tasks WHERE id = ?').get(id);
    const oldNoteId = oldNote ? oldNote.note_id : null;
    
    db.prepare('UPDATE tasks SET note_id = ? WHERE id = ?').run(note_id, id);
    if (note_id) {
      // Clear old note's task_id if switching notes
      if (oldNoteId && oldNoteId !== note_id) {
        db.prepare('UPDATE notes SET task_id = NULL WHERE id = ?').run(oldNoteId);
      }
      db.prepare('UPDATE notes SET task_id = ? WHERE id = ?').run(id, note_id);
    } else {
      // note_id cleared: release old note association
      if (oldNoteId) {
        db.prepare('UPDATE notes SET task_id = NULL WHERE id = ?').run(oldNoteId);
      }
    }
  }
  res.json(task);
});

router.get('/:id', (req, res) => {
  const { id } = req.params;
  const userId = getUserId(req) || 0;
  const task = db.prepare('SELECT * FROM tasks WHERE id = ? AND user_id = ?').get(id, userId);
  if (!task) return res.status(404).json({ error: 'Task not found' });
  res.json(task);
});

router.post('/:id/copy', (req, res) => {
  const { id } = req.params;
  const { date } = req.body;
  const userId = getUserId(req) || 0;
  const original = db.prepare('SELECT * FROM tasks WHERE id = ? AND user_id = ?').get(id, userId);
  if (!original) return res.status(404).json({ error: 'Task not found' });
  const targetDate = date || new Date().toLocaleDateString('en-CA'); // YYYY-MM-DD in local time
  const stmt = db.prepare(
    `INSERT INTO tasks (date, title, start_time, end_time, planned_duration, category, priority, note, is_recurring, is_planned, note_id, planned_days, overall_status, user_id)
     VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`
  );
  const result = stmt.run(
    targetDate, original.title, original.start_time, original.end_time,
    original.planned_duration, original.category, original.priority, original.note,
    original.is_recurring, original.is_planned, original.note_id, original.planned_days || 1, original.overall_status || 'pending', userId
  );
  const task = db.prepare('SELECT * FROM tasks WHERE id = ?').get(result.lastInsertRowid);
  res.status(201).json(task);
});

router.delete('/:id', (req, res) => {
  const { id } = req.params;
  const userId = getUserId(req) || 0;
  db.prepare('DELETE FROM tasks WHERE id = ? AND user_id = ?').run(id, userId);
  res.json({ message: 'Task deleted' });
});

router.delete('/by-name/:title', (req, res) => {
  const userId = getUserId(req) || 0;
  const { title } = req.params;
  const today = new Date().toLocaleDateString('en-CA');
  const result = db.prepare("DELETE FROM tasks WHERE title = ? AND user_id = ? AND date >= ? AND status != 'completed'").run(title, userId, today);
  res.json({ message: `Deleted ${result.changes} task(s) with name "${title}"`, count: result.changes });
});

module.exports = router;
