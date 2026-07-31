const express = require('express');
const cors = require('cors');
const path = require('path');
const os = require('os');
const cron = require('node-cron');
const db = require('./database');
const authRouter = require('./routes/auth');
const tasksRouter = require('./routes/tasks');
const reviewsRouter = require('./routes/reviews');
const recurringRouter = require('./routes/recurring');
const achievementsRouter = require('./routes/achievements');
const notesRouter = require('./routes/note');
const uploadRouter = require('./routes/upload');
const exportRouter = require('./routes/export');
const statsRouter = require('./routes/stats');
const settingsRouter = require('./routes/settings');
const questionsRouter = require('./routes/questions');
const summariesRouter = require('./routes/summaries');
const taskSummariesRouter = require('./routes/task-summaries');

const app = express();
const PORT = process.env.PORT || 3001;
const VERSION = '2.0.0';

function getLANIP() {
  const interfaces = os.networkInterfaces();
  for (const name of Object.keys(interfaces)) {
    for (const iface of interfaces[name]) {
      if (iface.family === 'IPv4' && !iface.internal) {
        return iface.address;
      }
    }
  }
  return 'localhost';
}

app.use(cors({ origin: true, credentials: true, limit: '50mb' }));
app.use(express.json({ limit: '50mb' }));

let publicUrl = '';

app.get('/api/version', (req, res) => {
  res.json({
    version: VERSION,
    server: os.hostname(),
    lanIP: getLANIP(),
    port: PORT,
    publicUrl: publicUrl || undefined,
  });
});

app.use('/api/auth', authRouter);
app.use('/api/tasks', tasksRouter);
app.use('/api/reviews', reviewsRouter);
app.use('/api/recurring', recurringRouter);
app.use('/api/achievements', achievementsRouter);
app.use('/api/notes', notesRouter);
app.use('/api/upload', uploadRouter);
app.use('/api/export', exportRouter);
app.use('/api/stats', statsRouter);
app.use('/api/settings', settingsRouter);
app.use('/api/questions', questionsRouter);
app.use('/api/summaries', summariesRouter);
app.use('/api/task-summaries', taskSummariesRouter);

app.use('/uploads', express.static(path.join(__dirname, '..', 'data', 'uploads')));
app.use(express.static(path.join(__dirname, '..', '..', 'frontend', 'dist')));

app.get('*', (req, res) => {
  if (!req.path.startsWith('/api')) {
    res.sendFile(path.join(__dirname, '..', '..', 'frontend', 'dist', 'index.html'));
  }
});

// Auto-generate recurring tasks every midnight
const generateNextDayTasks = () => {
  const tomorrow = new Date();
  tomorrow.setDate(tomorrow.getDate() + 1);
  const dateStr = tomorrow.toISOString().slice(0, 10);
  try {
    const templates = db.prepare('SELECT * FROM recurring_templates').all();
    let count = 0;
    for (const t of templates) {
      if (!t.recurring_enabled) continue;
      // Check recurrence settings
      if (t.recurrence_type === 'weekly') {
        const tomorrowDow = tomorrow.getDay(); // 0=Sunday
        const days = (t.recurrence_days || '').split(',').map(s => s.trim()).filter(Boolean);
        if (!days.includes(String(tomorrowDow))) continue;
      }
      const existing = db.prepare('SELECT id FROM tasks WHERE date = ? AND recurring_template_id = ?').get(dateStr, t.id);
      if (existing) continue;
      const tPlannedDays = t.planned_days || 1;
      const taskCount = db.prepare('SELECT COUNT(DISTINCT date) as cnt FROM tasks WHERE recurring_template_id = ?').get(t.id);
      if (taskCount && taskCount.cnt >= tPlannedDays) continue;
      db.prepare(
        `INSERT INTO tasks (date, title, start_time, end_time, planned_duration, category, priority, note, is_recurring, recurring_template_id, sync_enabled, planned_days, overall_status)
         VALUES (?, ?, ?, ?, ?, ?, ?, ?, 1, ?, ?, ?, 'pending')`
      ).run(dateStr, t.title, t.start_time, t.end_time, t.planned_duration, t.category, t.priority, t.note, t.id, t.sync_enabled !== false ? 1 : 0, tPlannedDays);
      count++;
    }
    if (count > 0) console.log(`[Scheduler] Generated ${count} recurring tasks for ${dateStr}`);
  } catch (e) {
    console.error('[Scheduler] Error:', e.message);
  }
};

cron.schedule('0 9 * * *', generateNextDayTasks);
console.log('[Scheduler] Registered: auto-generate recurring tasks at 09:00');

// Auto-generate summaries at 22:00 on last day of each period
const autoGenerateSummaries = () => {
  const today = new Date();
  const todayStr = today.toISOString().slice(0, 10);
  const year = today.getFullYear();
  const month = today.getMonth() + 1;
  const day = today.getDate();
  const dow = today.getDay(); // 0=Sun
  const lastDayOfMonth = new Date(year, month, 0).getDate();
  const quarter = Math.ceil(month / 3);
  const lastDayOfQuarter = new Date(year, quarter * 3, 0).getDate();
  const lastMonthOfQuarter = quarter * 3;

  const periods = [];

  // Weekly: Sunday (dow === 0) → generate previous week summary
  if (dow === 0) {
    const jan1 = new Date(year, 0, 1);
    const days = Math.floor((today.getTime() - jan1.getTime()) / 86400000);
    const weekNum = Math.ceil((days + jan1.getDay() + 1) / 7);
    periods.push({ type: 'weekly', period: `${year}-W${String(weekNum).padStart(2, '0')}` });
  }

  // Monthly: last day of month
  if (day === lastDayOfMonth) {
    periods.push({ type: 'monthly', period: `${year}-${String(month).padStart(2, '0')}` });
  }

  // Quarterly: last day of quarter
  if (day === lastDayOfQuarter && month === lastMonthOfQuarter) {
    periods.push({ type: 'quarterly', period: `${year}-Q${quarter}` });
  }

  // Yearly: Dec 31
  if (month === 12 && day === 31) {
    periods.push({ type: 'yearly', period: `${year}` });
  }

  if (periods.length === 0) return;

  const users = db.prepare('SELECT id FROM users').all();
  for (const user of users) {
    for (const p of periods) {
      try {
        const existing = db.prepare('SELECT id FROM summaries WHERE user_id = ? AND type = ? AND period_key = ?').get(user.id, p.type, p.period);
        if (existing) continue;

                const autoSummary = summariesRouter.generateAutoSummary(user.id, p.type, p.period);
        db.prepare('INSERT INTO summaries (type, period_key, auto_summary, user_id) VALUES (?, ?, ?, ?)').run(p.type, p.period, autoSummary, user.id);
        console.log(`[SummaryScheduler] Auto-generated ${p.type} summary for user ${user.id} period ${p.period}`);
      } catch (e) {
        console.error(`[SummaryScheduler] Error for user ${user.id} ${p.type} ${p.period}:`, e.message);
      }
    }
  }
};

cron.schedule('0 22 * * *', autoGenerateSummaries);
console.log('[SummaryScheduler] Registered: auto-generate summaries at 22:00');

const lanIP = getLANIP();
app.listen(PORT, async () => {
  console.log('');
  console.log('╔═══════════════════════════════════════════╗');
  console.log('║           DayLoop v' + VERSION + '                  ║');
  console.log('╠═══════════════════════════════════════════╣');
  console.log('║                                           ║');
  console.log('║  Local:  http://localhost:' + String(PORT).padEnd(4) + '                   ║');
  console.log('║  LAN:    http://' + (lanIP + ':' + PORT).padEnd(30) + '      ║');
  console.log('║                                           ║');

  if (process.env.TUNNEL === 'true') {
    try {
      const ngrok = require('@ngrok/ngrok');
      const listener = await ngrok.forward({ addr: PORT, authtoken_from_env: true });
      publicUrl = listener.url();
      console.log('║  🌐 Public: ' + publicUrl.padEnd(34) + ' ║');
      console.log('║  (Anyone with this URL can access)       ║');
    } catch (e) {
      console.log('║  ⚠️  Tunnel failed: ' + (e.message || e).padEnd(24) + ' ║');
      console.log('║  Set NGROK_AUTH_TOKEN env var if needed   ║');
    }
  }

  console.log('║                                           ║');
  console.log('║  Phone: open browser, menu "Add to Home"   ║');
  console.log('╚═══════════════════════════════════════════╝');
  console.log('');
});
