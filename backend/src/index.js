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
      if (!existing) {
        db.prepare(
          `INSERT INTO tasks (date, title, start_time, end_time, planned_duration, category, priority, note, is_recurring, recurring_template_id, sync_enabled)
           VALUES (?, ?, ?, ?, ?, ?, ?, ?, 1, ?, ?)`
        ).run(dateStr, t.title, t.start_time, t.end_time, t.planned_duration, t.category, t.priority, t.note, t.id, t.sync_enabled !== false ? 1 : 0);
        count++;
      }
    }
    if (count > 0) console.log(`[Scheduler] Generated ${count} recurring tasks for ${dateStr}`);
  } catch (e) {
    console.error('[Scheduler] Error:', e.message);
  }
};

cron.schedule('0 9 * * *', generateNextDayTasks);
console.log('[Scheduler] Registered: auto-generate recurring tasks at 09:00');

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
