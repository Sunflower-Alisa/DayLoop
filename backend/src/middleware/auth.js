const jwt = require('jsonwebtoken');

const JWT_SECRET = process.env.JWT_SECRET || 'DayLoop-Default-Secret-Key-2026-Change-In-Production!';

function generateToken(user) {
  return jwt.sign({ id: user.id, username: user.username }, JWT_SECRET, { expiresIn: '30d' });
}

function getUserId(req) {
  const auth = req.headers.authorization;
  if (!auth || !auth.startsWith('Bearer ')) return null;
  try {
    const decoded = jwt.verify(auth.slice(7), JWT_SECRET);
    return decoded.id;
  } catch {
    return null;
  }
}

function requireAuth(req, res, next) {
  const userId = getUserId(req);
  if (!userId) return res.status(401).json({ error: '未登录' });
  req.userId = userId;
  next();
}

module.exports = { generateToken, getUserId, requireAuth };
