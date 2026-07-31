const express = require('express');
const router = express.Router();
const db = require('../database');
const { getUserId } = require('../middleware/auth');

function getPeriodDateRange(type, periodKey) {
  const parts = periodKey.split('-');
  if (type === 'weekly') {
    const year = parseInt(parts[0]);
    const week = parseInt(parts[1].replace('W', ''));
    const jan1 = new Date(year, 0, 1);
    const daysOffset = (week - 1) * 7;
    const start = new Date(jan1);
    start.setDate(jan1.getDate() + daysOffset - jan1.getDay() + 1);
    const end = new Date(start);
    end.setDate(start.getDate() + 6);
    return { start, end };
  }
  if (type === 'monthly') {
    const year = parseInt(parts[0]), mon = parseInt(parts[1]);
    return { start: new Date(year, mon - 1, 1), end: new Date(year, mon, 0) };
  }
  if (type === 'quarterly') {
    const year = parseInt(parts[0]), q = parseInt(parts[1].replace('Q', ''));
    return { start: new Date(year, (q - 1) * 3, 1), end: new Date(year, q * 3, 0) };
  }
  if (type === 'yearly') {
    const year = parseInt(parts[0]);
    return { start: new Date(year, 0, 1), end: new Date(year, 11, 31) };
  }
  return { start: new Date(), end: new Date() };
}

function fmt(d) {
  const y = d.getFullYear(), m = String(d.getMonth() + 1).padStart(2, '0'), dd = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${dd}`;
}

function generateAutoSummary(userId, type, periodKey) {
  const range = getPeriodDateRange(type, periodKey);
  const startStr = fmt(range.start);
  const endStr = fmt(range.end);

  const tasks = db.prepare(`
    SELECT status, planned_duration, actual_duration, category, title FROM tasks
    WHERE user_id = ? AND date >= ? AND date <= ? ORDER BY date
  `).all(userId, startStr, endStr);

  if (tasks.length === 0) return '该时间段内没有任务记录。';

  const total = tasks.length;
  const completed = tasks.filter(t => t.status === 'completed').length;
  const cancelled = tasks.filter(t => t.status === 'cancelled').length;
  const plannedDur = tasks.reduce((s, t) => s + (t.planned_duration || 0), 0);
  const actualDur = tasks.reduce((s, t) => s + (t.actual_duration || 0), 0);
  const rate = total > 0 ? Math.round((completed / total) * 100) : 0;

  const catStats = {};
  for (const t of tasks) {
    if (!catStats[t.category]) catStats[t.category] = { total: 0, completed: 0, dur: 0 };
    catStats[t.category].total++;
    if (t.status === 'completed') catStats[t.category].completed++;
    catStats[t.category].dur += (t.actual_duration || 0);
  }

  let summary = `## ${periodKey} 总结\n\n`;
  summary += `**概览**：共 ${total} 个任务，完成 ${completed} 个`;
  if (cancelled) summary += `，取消 ${cancelled} 个`;
  summary += `，完成率 ${rate}%。\n\n`;
  summary += `**时长**：计划 ${plannedDur} 分钟，实际 ${actualDur} 分钟。\n\n`;

  const cats = Object.entries(catStats).filter(([k]) => k);
  if (cats.length) {
    summary += `**分类统计**：\n`;
    for (const [cat, s] of cats.sort((a, b) => b[1].total - a[1].total)) {
      const cr = s.total > 0 ? Math.round((s.completed / s.total) * 100) : 0;
      summary += `- ${cat}：${s.completed}/${s.total} (${cr}%)，${s.dur} 分钟\n`;
    }
    summary += '\n';
  }

  const maxDur = 5;
  const topTasks = tasks.filter(t => t.actual_duration && t.status === 'completed').sort((a, b) => (b.actual_duration || 0) - (a.actual_duration || 0)).slice(0, maxDur);
  if (topTasks.length) {
    summary += `**耗时最多的任务**：\n`;
    for (const t of topTasks) {
      summary += `- ${t.title}（${t.actual_duration} 分钟）\n`;
    }
  }

  return summary.trim();
}

// GET /api/summaries?type=weekly&period=2026-W34
router.get('/', (req, res) => {
  const userId = getUserId(req) || 0;
  const { type, period } = req.query;
  if (!type || !period) return res.status(400).json({ error: 'type and period are required' });

  const row = db.prepare('SELECT * FROM summaries WHERE user_id = ? AND type = ? AND period_key = ?').get(userId, type, period);
  res.json(row || null);
});

// PUT /api/summaries/:type/:period
router.put('/:type/:period', (req, res) => {
  const userId = getUserId(req) || 0;
  const { type, period } = req.params;
  const { content } = req.body;

  const existing = db.prepare('SELECT id FROM summaries WHERE user_id = ? AND type = ? AND period_key = ?').get(userId, type, period);
  if (existing) {
    db.prepare("UPDATE summaries SET content = ?, updated_at = datetime('now','localtime') WHERE id = ?").run(content || '', existing.id);
  } else {
    const autoSummary = generateAutoSummary(userId, type, period);
    db.prepare('INSERT INTO summaries (type, period_key, content, auto_summary, user_id) VALUES (?, ?, ?, ?, ?)').run(type, period, content || '', autoSummary, userId);
  }

  const row = db.prepare('SELECT * FROM summaries WHERE user_id = ? AND type = ? AND period_key = ?').get(userId, type, period);
  res.json(row);
});

// POST /api/summaries/generate
router.post('/generate', (req, res) => {
  const userId = getUserId(req) || 0;
  const { type, period } = req.body;
  if (!type || !period) return res.status(400).json({ error: 'type and period are required' });

  const autoSummary = generateAutoSummary(userId, type, period);
  const existing = db.prepare('SELECT id FROM summaries WHERE user_id = ? AND type = ? AND period_key = ?').get(userId, type, period);
  if (existing) {
    db.prepare('UPDATE summaries SET auto_summary = ?, updated_at = datetime(\'now\',\'localtime\') WHERE id = ?').run(autoSummary, existing.id);
  } else {
    db.prepare('INSERT INTO summaries (type, period_key, auto_summary, user_id) VALUES (?, ?, ?, ?)').run(type, period, autoSummary, userId);
  }

  const row = db.prepare('SELECT * FROM summaries WHERE user_id = ? AND type = ? AND period_key = ?').get(userId, type, period);
  res.json(row);
});

// GET /api/summaries/list?type=weekly -> list all period_keys for a type
router.get('/list', (req, res) => {
  const userId = getUserId(req) || 0;
  const { type } = req.query;
  if (!type) return res.status(400).json({ error: 'type is required' });

  const rows = db.prepare('SELECT period_key, updated_at FROM summaries WHERE user_id = ? AND type = ? ORDER BY period_key DESC').all(userId, type);
  res.json(rows);
});

module.exports = router;
module.exports.generateAutoSummary = generateAutoSummary;
