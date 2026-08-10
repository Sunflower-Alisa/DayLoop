#!/usr/bin/env node
/* DayLoop 词书资源下载脚本
 * 为词书单词下载图片 + 发音音频，并更新数据库。
 *
 * 数据源（均无需 API key）：
 *   - 图片  : Wikimedia Commons (https://commons.wikimedia.org/w/api.php)
 *   - 词发音: dictionaryapi.dev  (https://api.dictionaryapi.dev) 真实英/美发音 mp3
 *
 * 说明：场景/口语台词在 UI 中由浏览器 speechSynthesis 直接朗读，
 *       无需下载音频文件，因此本脚本只处理词书词条资源。
 *
 * 用法:
 *   node scripts/download-resources.cjs [dbPath] [--limit=N] [--images-only] [--audio-only]
 *   默认 dbPath = backend/data/dayloop.db
 *
 * 幂等：已配置 image_url/audio_url 的条目自动跳过，可随时断点续跑。
 */
const path = require('path');
const fs = require('fs');
const https = require('https');
const http = require('http');

const ROOT = path.resolve(__dirname, '..');
const args = process.argv.slice(2);
const positionals = args.filter(a => !a.startsWith('--'));
const DB_PATH = path.resolve(ROOT, positionals[0] || 'backend/data/dayloop.db');
const UPLOAD_DIR = path.resolve(ROOT, 'backend/data/uploads');

const limit = parseInt((args.find(a => a.startsWith('--limit=')) || '').split('=')[1] || '0', 10) || 0;
const DO_IMAGES = !args.includes('--audio-only');
const DO_WORD_AUDIO = !args.includes('--images-only');

const requireBsl = require(path.join(ROOT, 'backend/node_modules/better-sqlite3'));
const db = new requireBsl(DB_PATH);

const UA = 'DayLoop-ResourceFetcher/1.0 (educational use)';
const TIMEOUT = 10000;

let images = 0, wordAudio = 0, failed = 0, skipped = 0;

const sleep = (ms) => new Promise(r => setTimeout(r, ms));
const log = (s) => process.stdout.write(s + '\n');

function download(url, timeout = TIMEOUT) {
  return new Promise((resolve, reject) => {
    const mod = url.startsWith('https') ? https : http;
    const req = mod.get(url, { headers: { 'User-Agent': UA } }, res => {
      if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
        res.resume();
        return resolve(download(res.headers.location, timeout));
      }
      if (res.statusCode !== 200) {
        res.resume();
        return reject(new Error(`HTTP ${res.statusCode}`));
      }
      const chunks = [];
      res.on('data', c => chunks.push(c));
      res.on('end', () => resolve(Buffer.concat(chunks)));
    });
    req.setTimeout(timeout, () => req.destroy(new Error('timeout')));
    req.on('error', reject);
  });
}

// Retry with backoff for transient 429/5xx/network errors
async function downloadRetry(url, attempts = 4) {
  let lastErr;
  for (let i = 0; i < attempts; i++) {
    try {
      return await download(url);
    } catch (e) {
      lastErr = e;
      await sleep(800 * (i + 1));
    }
  }
  throw lastErr;
}

async function fetchJson(url) {
  const buf = await downloadRetry(url);
  return JSON.parse(buf.toString());
}

// —— image: Wikimedia Commons ——
async function findImage(query) {
  const url =
    `https://commons.wikimedia.org/w/api.php?action=query&generator=search` +
    `&gsrsearch=filetype:bitmap ${encodeURIComponent(query)}&gsrnamespace=6&gsrlimit=8` +
    `&prop=imageinfo&iiprop=url|mime&iiurlwidth=480&format=json&origin=*`;
  const data = await fetchJson(url);
  const pages = data && data.query && data.query.pages ? data.query.pages : {};
  const ordered = Object.keys(pages).sort((a, b) => Number(a) - Number(b));
  for (const k of ordered) {
    const ii = pages[k].imageinfo && pages[k].imageinfo[0];
    if (!ii || !ii.thumburl) continue;
    const name = (pages[k].title || '').toLowerCase();
    if (name.includes('.svg') || name.includes('.tif')) continue;
    const mime = ii.mime || '';
    if (mime !== 'image/jpeg' && mime !== 'image/png' && mime !== 'image/webp') continue;
    return ii.thumburl;
  }
  return null;
}

// —— word audio: dictionaryapi.dev (US preferred, fallback UK) ——
async function findWordAudio(word) {
  const data = await fetchJson(`https://api.dictionaryapi.dev/api/v2/entries/en/${encodeURIComponent(word)}`);
  const arr = Array.isArray(data) ? data : [];
  const audios = arr.flatMap(p => (p.phonetics || []).map(x => x.audio).filter(a => a));
  if (audios.length === 0) return null;
  const us = audios.find(a => a.includes('-us')) || audios.find(a => a.includes('us'));
  const uk = audios.find(a => a.includes('-uk')) || audios.find(a => a.includes('uk'));
  return us || uk || audios[0];
}

async function processWords() {
  const rows = db.prepare(
    "SELECT id, word FROM words WHERE image_url = '' OR audio_url = '' ORDER BY id LIMIT " + (limit || -1)
  ).all();
  for (const w of rows) {
    const slug = w.word.toLowerCase().replace(/[^a-z0-9]+/g, '_');
    const cur = db.prepare('SELECT image_url, audio_url FROM words WHERE id = ?').get(w.id);
    let changed = false;

    if (DO_IMAGES && !cur.image_url) {
      try {
        const imgUrl = await findImage(w.word);
        if (imgUrl) {
          const ext = (path.extname(new URL(imgUrl).pathname).split('?')[0]) || '.jpg';
          const file = path.join(UPLOAD_DIR, 'words', `${w.id}_${slug}${ext}`);
          fs.mkdirSync(path.dirname(file), { recursive: true });
          fs.writeFileSync(file, await downloadRetry(imgUrl));
          const rel = `/uploads/words/${w.id}_${slug}${ext}`;
          db.prepare('UPDATE words SET image_url = ? WHERE id = ?').run(rel, w.id);
          images++; changed = true;
          log(`  img  [${w.id}] ${w.word} -> ${rel}`);
        } else {
          log(`  img  [${w.id}] ${w.word} no result`);
        }
      } catch (e) { failed++; log(`  img  [${w.id}] ${w.word} FAIL: ${e.message}`); }
      await sleep(500);
    }

    if (DO_WORD_AUDIO && !cur.audio_url) {
      try {
        const audioUrl = await findWordAudio(w.word);
        if (audioUrl) {
          const file = path.join(UPLOAD_DIR, 'words', `${w.id}_${slug}.mp3`);
          fs.mkdirSync(path.dirname(file), { recursive: true });
          fs.writeFileSync(file, await downloadRetry(audioUrl));
          const rel = `/uploads/words/${w.id}_${slug}.mp3`;
          db.prepare('UPDATE words SET audio_url = ? WHERE id = ?').run(rel, w.id);
          wordAudio++; changed = true;
          log(`  audio[${w.id}] ${w.word} -> ${rel}`);
        } else {
          log(`  audio[${w.id}] ${w.word} no audio`);
        }
      } catch (e) { failed++; log(`  audio[${w.id}] ${w.word} FAIL: ${e.message}`); }
      await sleep(500);
    }

    if (!changed) skipped++;
  }
}

(async () => {
  if (!fs.existsSync(UPLOAD_DIR)) fs.mkdirSync(UPLOAD_DIR, { recursive: true });
  db.prepare('SELECT 1 FROM words LIMIT 1').get();

  log(`DB path   : ${DB_PATH}`);
  log(`Upload dir: ${UPLOAD_DIR}`);
  log(`limit     : ${limit || 'all'}`);
  log(`images    : ${DO_IMAGES ? 'yes' : 'no'}`);
  log(`word audio: ${DO_WORD_AUDIO ? 'yes' : 'no'}\n`);

  log('--- words ---');
  await processWords();

  log(`\nDone. images=${images} word_audio=${wordAudio} failed=${failed} skipped=${skipped}`);
  db.close();
  process.exit(0);
})().catch(e => { log('Fatal: ' + e.message); process.exit(2); });