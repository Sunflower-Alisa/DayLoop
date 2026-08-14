const express = require('express');
const router = express.Router();
const { getUserId } = require('../middleware/auth');

// Agent Service 基地址（§11 Chat API），可由环境变量覆盖。
// 用 127.0.0.1 而非 localhost：Windows 上 Node fetch 会把 localhost 解析成 IPv6 ::1，
// 而 uvicorn 默认只监听 127.0.0.1（IPv4），导致 fetch failed。
const AGENT_SERVICE_URL = process.env.AGENT_SERVICE_URL || 'http://127.0.0.1:5173/api/v1/chat';
const AGENT_SERVICE_TIMEOUT = parseInt(process.env.AGENT_SERVICE_TIMEOUT || '60', 10);

// POST /api/agent/chat —— 前端经同源代理访问 Agent Service，避免暴露内部地址与 CORS
router.post('/chat', async (req, res) => {
  const userId = getUserId(req) || 0;
  const { message, session_id, extra } = req.body || {};
  if (!message || !String(message).trim()) {
    return res.status(400).json({ error: 'message 不能为空' });
  }

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), AGENT_SERVICE_TIMEOUT * 1000);

  try {
    const upstream = await fetch(AGENT_SERVICE_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        user_id: String(userId),
        session_id: session_id || '',
        message: String(message),
        extra: extra || {},
      }),
      signal: controller.signal,
    });
    clearTimeout(timer);
    const text = await upstream.text();
    res.status(upstream.status).type('application/json').send(text || '{}');
  } catch (e) {
    clearTimeout(timer);
    console.error('[AgentProxy] 转发失败:', e.message);
    const status = e.name === 'AbortError' ? 504 : 502;
    res.status(status).json({ error: 'Agent Service 不可用', detail: e.message });
  }
});

router.get('/status', async (_req, res) => {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 5000);
  try {
    const base = AGENT_SERVICE_URL.replace(/\/chat$/, '/health');
    const upstream = await fetch(base, { signal: controller.signal });
    clearTimeout(timer);
    const text = await upstream.text();
    res.status(upstream.status).type('application/json').send(text || '{}');
  } catch (e) {
    clearTimeout(timer);
    res.status(502).json({ error: 'Agent Service 不可用', detail: e.message });
  }
});

module.exports = router;
