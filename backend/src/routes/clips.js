const express = require('express');
const router = express.Router();
const db = require('../database');
const { getUserId } = require('../middleware/auth');

const userIdOrZero = (req) => getUserId(req) || 0;

router.get('/', (req, res) => {
  const userId = userIdOrZero(req);
  const { source, level } = req.query;
  let sql = `
    SELECT c.*,
           (SELECT COUNT(*) FROM clip_lines cl WHERE cl.clip_id = c.id) as line_count
    FROM video_clips c
    WHERE (c.user_id = 0 OR c.user_id = ?)
  `;
  const params = [userId];
  if (source) { sql += ' AND c.source = ?'; params.push(source); }
  if (level) { sql += ' AND c.level = ?'; params.push(level); }
  sql += ' ORDER BY c.id';
  res.json(db.prepare(sql).all(...params));
});

router.get('/:id', (req, res) => {
  const id = Number(req.params.id);
  const clip = db.prepare(`
    SELECT c.*,
           (SELECT COUNT(*) FROM clip_lines cl WHERE cl.clip_id = c.id) as line_count
    FROM video_clips c WHERE c.id = ?
  `).get(id);
  if (!clip) return res.status(404).json({ error: 'Clip not found' });
  const lines = db.prepare('SELECT * FROM clip_lines WHERE clip_id = ? ORDER BY ord').all(id);
  res.json({ clip, lines });
});

module.exports = router;