const express = require('express');
const router = express.Router();
const path = require('path');
const fs = require('fs');

const uploadDir = path.join(__dirname, '..', '..', 'data', 'uploads');
if (!fs.existsSync(uploadDir)) {
  fs.mkdirSync(uploadDir, { recursive: true });
}

router.post('/image', (req, res) => {
  const { dataUrl } = req.body;
  if (!dataUrl) return res.status(400).json({ error: 'dataUrl is required' });
  const matches = dataUrl.match(/^data:image\/(\w+);base64,(.+)$/);
  if (!matches) return res.status(400).json({ error: 'Invalid data URL' });
  const ext = matches[1] === 'jpeg' ? 'jpg' : matches[1];
  const data = matches[2];
  const filename = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}.${ext}`;
  const filepath = path.join(__dirname, '..', '..', 'data', 'uploads', filename);
  fs.writeFileSync(filepath, Buffer.from(data, 'base64'));
  res.json({ url: `/uploads/${filename}` });
});

module.exports = router;
