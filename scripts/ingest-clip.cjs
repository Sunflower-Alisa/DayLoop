#!/usr/bin/env node
/* DayLoop 影视切片素材入库脚本
 * 将一个影视切片（视频文件/URL + 字幕行 + 封面）写入数据库。
 *
 * 用法:
 *   node scripts/ingest-clip.cjs <metadata.json> [--replace]
 *   node scripts/ingest-clip.cjs --template        # 生成示例模板
 *
 * 元数据 JSON 格式:
 *   {
 *     "title": "公园散步对话",
 *     "source": "纪录片",
 *     "level": "medium",              // beginner | medium | advanced
 *     "tags": "日常,对话",
 *     "description": "一段日常对话，适合中级跟读。",
 *     "video": "C:/path/video.mp4",   // 本地文件路径 或 http(s) URL
 *     "cover": "https://.../cover.jpg",  // 可选封面
 *     "duration": 45,
 *     "lines": [
 *       { "speaker": "A", "en_text": "Hello.", "cn_text": "你好。", "start_time": 0, "end_time": 3.5 }
 *     ]
 *   }
 *
 * 说明：
 *   - 视频/封面复制到 backend/data/uploads/clips/ 并通过 /uploads/clips/... 访问
 *   - 默认跳过已存在（按 title）的切片；加 --replace 则更新现有记录
 *   - 纯文本字幕可在 line 中省略 start_time/end_time（自动按前一行累加）
 */
const path = require('path');
const fs = require('fs');
const https = require('https');
const http = require('http');

const ROOT = path.resolve(__dirname, '..');
const UPLOAD_DIR = path.resolve(ROOT, 'backend/data/uploads/clips');
const requireBsl = require(path.join(ROOT, 'backend/node_modules/better-sqlite3'));

const args = process.argv.slice(2);
const REPLACE = args.includes('--replace');

const UA = 'DayLoop-Ingester/1.0 (educational use)';

function die(msg) { console.error('✗ ' + msg); process.exit(2); }

function download(url, timeout = 60000) {
  return new Promise((resolve, reject) => {
    const mod = url.startsWith('https') ? https : http;
    const req = mod.get(url, { headers: { 'User-Agent': UA } }, res => {
      if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
        res.resume();
        return resolve(download(res.headers.location, timeout));
      }
      if (res.statusCode !== 200) { res.resume(); return reject(new Error(`HTTP ${res.statusCode}`)); }
      const chunks = [];
      res.on('data', c => chunks.push(c));
      res.on('end', () => resolve(Buffer.concat(chunks)));
    });
    req.setTimeout(timeout, () => req.destroy(new Error('timeout')));
    req.on('error', reject);
  });
}

function sanitize(name) {
  const n = name.toLowerCase().replace(/[^\w\u4e00-\u9fa5.-]+/g, '_').slice(0, 80);
  return n || 'clip';
}

function ensureFile(input, subdir, preferredName) {
  // returns relative upload URL
  const dir = path.join(UPLOAD_DIR, subdir || '');
  fs.mkdirSync(dir, { recursive: true });
  if (/^https?:\/\//i.test(input)) {
    const filename = preferredName + '_' + path.basename(new URL(input).pathname);
    const dest = path.join(dir, filename);
    fs.writeFileSync(dest, download(input));
    return `/uploads/clips${subdir ? '/' + subdir : ''}/${filename}`;
  }
  const src = path.resolve(ROOT, input);
  if (!fs.existsSync(src)) die(`视频/封面文件不存在: ${src}`);
  const filename = preferredName + path.extname(src);
  fs.copyFileSync(src, path.join(dir, filename));
  return `/uploads/clips${subdir ? '/' + subdir : ''}/${filename}`;
}

async function writeTemplate() {
  const template = {
    title: '示例片段',
    source: '本地素材',
    level: 'medium',
    tags: '日常,对话',
    description: '在这里填写片段简介。',
    video: 'C:/path/to/video.mp4',
    cover: '',
    duration: 30,
    lines: [
      { speaker: 'A', en_text: 'Hi there!', cn_text: '你好！', start_time: 0, end_time: 2.5 },
      { speaker: 'B', en_text: 'Hey, long time no see.', cn_text: '嘿，好久不见。', start_time: 2.5, end_time: 5 },
    ],
  };
  const file = path.join(ROOT, 'scripts', 'clips', 'clip-template.json');
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, JSON.stringify(template, null, 2) + '\n');
  console.log('✓ 模板已写入 ' + path.relative(ROOT, file));
  process.exit(0);
}

(async () => {
  if (args.includes('--template')) await writeTemplate();
  const metaFile = args.find(a => !a.startsWith('--'));
  if (!metaFile) die('缺少元数据 JSON 文件参数');
  const metaPath = path.resolve(ROOT, metaFile);
  if (!fs.existsSync(metaPath)) die(`元数据文件不存在: ${metaFile}`);
  const meta = JSON.parse(fs.readFileSync(metaPath, 'utf8'));

  if (!meta.title) die('缺少 title');
  if (!Array.isArray(meta.lines) || meta.lines.length === 0) die('lines 不能为空');

  const db = new requireBsl(path.resolve(ROOT, 'backend/data/dayloop.db'));
  const existing = db.prepare('SELECT id FROM video_clips WHERE title = ?').get(meta.title);
  if (existing && !REPLACE) die(`切片「${meta.title}」已存在（用 --replace 更新）`);

  fs.mkdirSync(UPLOAD_DIR, { recursive: true });
  const slug = sanitize(meta.title);

  const videoUrl = meta.video ? ensureFile(meta.video, '', slug) : '';
  const coverUrl = meta.cover ? ensureFile(meta.cover, 'covers', slug + '_cover') : '';
  console.log(`视频: ${videoUrl || '(无)'}`);
  console.log(`封面: ${coverUrl || '(无)'}`);

  // normalize line timestamps (auto-fill when missing)
  let cursor = 0;
  const lines = meta.lines.map(l => {
    const st = l.start_time !== undefined ? Number(l.start_time) : cursor;
    const et = l.end_time !== undefined ? Number(l.end_time) : st + 3;
    cursor = et;
    return { speaker: l.speaker || '', en_text: l.en_text || '', cn_text: l.cn_text || '', start_time: st, end_time: et };
  });

  if (existing) {
    db.prepare('UPDATE video_clips SET source=?, cover_url=?, path=?, duration=?, level=?, tags=?, description=? WHERE id=?')
      .run(meta.source || '', coverUrl, videoUrl, meta.duration || 0, meta.level || 'medium', meta.tags || '', meta.description || '', existing.id);
    db.prepare('DELETE FROM clip_lines WHERE clip_id = ?').run(existing.id);
    const cid = existing.id;
    const stmt = db.prepare('INSERT INTO clip_lines (clip_id, ord, speaker, en_text, cn_text, start_time, end_time) VALUES (?, ?, ?, ?, ?, ?, ?)');
    const tx = db.transaction(rows => { rows.forEach((l, i) => stmt.run(cid, i, l.speaker, l.en_text, l.cn_text, l.start_time, l.end_time)); });
    tx(lines);
    console.log(`✓ 已更新切片 #${cid}「${meta.title}」，台词 ${lines.length} 行`);
  } else {
    const r = db.prepare('INSERT INTO video_clips (title, source, cover_url, path, duration, level, tags, description, user_id) VALUES (?, ?, ?, ?, ?, ?, ?, ?, 0)')
      .run(meta.title, meta.source || '', coverUrl, videoUrl, meta.duration || 0, meta.level || 'medium', meta.tags || '', meta.description || '');
    const cid = r.lastInsertRowid;
    const stmt = db.prepare('INSERT INTO clip_lines (clip_id, ord, speaker, en_text, cn_text, start_time, end_time) VALUES (?, ?, ?, ?, ?, ?, ?)');
    const tx = db.transaction(rows => { rows.forEach((l, i) => stmt.run(cid, i, l.speaker, l.en_text, l.cn_text, l.start_time, l.end_time)); });
    tx(lines);
    console.log(`✓ 已入库切片 #${cid}「${meta.title}」，台词 ${lines.length} 行`);
  }

  db.close();
})().catch(e => { console.error('✗ ' + e.message); process.exit(2); });