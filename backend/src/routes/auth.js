const express = require('express');
const router = express.Router();
const bcrypt = require('bcryptjs');
const db = require('../database');
const { generateToken, getUserId } = require('../middleware/auth');

router.post('/register', (req, res) => {
  const { username, password } = req.body;
  if (!username || username.trim().length < 2) return res.status(400).json({ error: '用户名至少2个字符' });
  if (!password || password.length < 4) return res.status(400).json({ error: '密码至少4个字符' });

  const existing = db.prepare('SELECT id FROM users WHERE username = ?').get(username.trim());
  if (existing) return res.status(400).json({ error: '用户名已存在' });

  const hash = bcrypt.hashSync(password, 10);
  const result = db.prepare('INSERT INTO users (username, password_hash) VALUES (?, ?)').run(username.trim(), hash);
  const user = { id: result.lastInsertRowid, username: username.trim() };
  const token = generateToken(user);
  res.json({ token, user });
});

router.post('/login', (req, res) => {
  const { username, password } = req.body;
  const user = db.prepare('SELECT id, username, password_hash, created_at FROM users WHERE username = ?').get(username);
  if (!user) return res.status(401).json({ error: '用户名或密码错误' });
  if (!bcrypt.compareSync(password, user.password_hash)) return res.status(401).json({ error: '用户名或密码错误' });
  const token = generateToken(user);
  res.json({ token, user: { id: user.id, username: user.username, created_at: user.created_at } });
});

router.get('/me', (req, res) => {
  const userId = getUserId(req);
  if (!userId) return res.status(401).json({ error: '未登录' });
  const user = db.prepare('SELECT id, username, created_at FROM users WHERE id = ?').get(userId);
  if (!user) return res.status(401).json({ error: '用户不存在' });
  res.json(user);
});

router.put('/password', (req, res) => {
  const userId = getUserId(req);
  if (!userId) return res.status(401).json({ error: '未登录' });
  const { oldPassword, newPassword } = req.body;
  if (!oldPassword || !newPassword) return res.status(400).json({ error: '请提供旧密码和新密码' });
  if (newPassword.length < 4) return res.status(400).json({ error: '新密码至少4个字符' });

  const user = db.prepare('SELECT password_hash FROM users WHERE id = ?').get(userId);
  if (!user) return res.status(401).json({ error: '用户不存在' });
  if (!bcrypt.compareSync(oldPassword, user.password_hash)) return res.status(400).json({ error: '旧密码错误' });

  const hash = bcrypt.hashSync(newPassword, 10);
  db.prepare('UPDATE users SET password_hash = ? WHERE id = ?').run(hash, userId);
  res.json({ message: '密码修改成功' });
});

router.delete('/account', (req, res) => {
  const userId = getUserId(req);
  if (!userId) return res.status(401).json({ error: '未登录' });

  const user = db.prepare('SELECT id FROM users WHERE id = ?').get(userId);
  if (!user) return res.status(401).json({ error: '用户不存在' });

  const deleteAll = db.transaction(() => {
    db.prepare('DELETE FROM tasks WHERE user_id = ?').run(userId);
    db.prepare('DELETE FROM daily_reviews WHERE user_id = ?').run(userId);
    db.prepare('DELETE FROM recurring_templates WHERE user_id = ?').run(userId);
    db.prepare('DELETE FROM notes WHERE user_id = ?').run(userId);
    db.prepare('DELETE FROM note_categories WHERE user_id = ?').run(userId);
    db.prepare('DELETE FROM users WHERE id = ?').run(userId);
  });
  deleteAll();
  res.json({ message: '账号已删除' });
});

module.exports = router;
