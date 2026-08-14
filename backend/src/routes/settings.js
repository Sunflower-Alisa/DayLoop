const express = require('express');
const router = express.Router();
const db = require('../database');
const { getUserId } = require('../middleware/auth');
const { syncAll } = require('../sync/obsidian');

router.get('/', (req, res) => {
  const userId = getUserId(req);
  if (!userId) return res.status(401).json({ error: '未登录' });
  const rows = db.prepare('SELECT key, value FROM app_settings').all();
  const settings = {};
  for (const row of rows) {
    settings[row.key] = row.value;
  }
  res.json(settings);
});

router.put('/', (req, res) => {
  const userId = getUserId(req);
  if (!userId) return res.status(401).json({ error: '未登录' });
  const { key, value } = req.body;
  if (!key) return res.status(400).json({ error: 'key is required' });
  const allowedKeys = ['obsidian_vault_path'];
  if (!allowedKeys.includes(key)) return res.status(400).json({ error: 'Invalid setting key' });

  const existing = db.prepare('SELECT key FROM app_settings WHERE key = ?').get(key);
  if (existing) {
    db.prepare('UPDATE app_settings SET value = ? WHERE key = ?').run(value || '', key);
  } else {
    db.prepare('INSERT INTO app_settings (key, value) VALUES (?, ?)').run(key, value || '');
  }

  res.json({ key, value: value || '' });
});

router.post('/sync-all', (req, res) => {
  const userId = getUserId(req);
  if (!userId) return res.status(401).json({ error: '未登录' });
  const result = syncAll();
  if (result.error) return res.status(400).json(result);
  res.json({ message: `同步完成: ${result.notes} 条备忘录, ${result.reviews} 条复盘, ${result.achievements} 条成果`, ...result });
});

module.exports = router;
