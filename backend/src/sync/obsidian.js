const fs = require('fs');
const path = require('path');
const db = require('../database');

function getSetting(key) {
  const row = db.prepare('SELECT value FROM app_settings WHERE key = ?').get(key);
  return row ? row.value : null;
}

function getVaultPath() {
  return process.env.OBSIDIAN_VAULT_PATH || getSetting('obsidian_vault_path') || '';
}

function slugify(text) {
  return text
    .replace(/[\\/:*?"<>|]/g, '')
    .replace(/\s+/g, '-')
    .replace(/-+/g, '-')
    .replace(/^-|-$/g, '')
    .substring(0, 100);
}

function ensureDir(dir) {
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
}

function writeFile(filePath, content) {
  try {
    ensureDir(path.dirname(filePath));
    fs.writeFileSync(filePath, content, 'utf-8');
  } catch (e) {
    console.error(`[ObsidianSync] Failed to write ${filePath}: ${e.message}`);
  }
}

function deleteDir(dir) {
  if (fs.existsSync(dir)) {
    fs.rmSync(dir, { recursive: true, force: true });
  }
}

function extractBookName(title) {
  const m = title.match(/《([^》]+)》/);
  return m ? m[1].trim() : null;
}

function padNum(n, total) {
  const digits = String(total).length;
  return String(n).padStart(digits, '0');
}

function syncAllNotes() {
  const vaultPath = getVaultPath();
  if (!vaultPath) return 0;

  const dir = path.join(vaultPath, 'DayLoop', '备忘录');
  deleteDir(dir);
  ensureDir(dir);

  const allNotes = db.prepare('SELECT * FROM notes ORDER BY created_at ASC, id ASC').all();
  if (allNotes.length === 0) return 0;

  const total = allNotes.length;

  // Group book notes, collect standalone notes
  const bookGroups = new Map(); // bookName -> notes[]
  const standaloneNotes = [];

  for (const note of allNotes) {
    const book = extractBookName(note.title);
    if (book) {
      if (!bookGroups.has(book)) bookGroups.set(book, []);
      bookGroups.get(book).push(note);
    } else {
      standaloneNotes.push(note);
    }
  }

  let idx = 0;

  // Write standalone notes with numbers
  for (const note of standaloneNotes) {
    idx++;
    const slug = slugify(note.title || `note-${note.id}`);
    const filePath = path.join(dir, `${padNum(idx, total)}-${slug}.md`);
    const tags = (note.tags || '').split(',').map(t => t.trim()).filter(Boolean);

    const frontmatter = [
      '---',
      `created: ${note.created_at || ''}`,
      `updated: ${note.updated_at || ''}`,
      note.category ? `category: ${note.category}` : null,
      tags.length > 0 ? `tags: [${tags.join(', ')}]` : null,
      'source: DayLoop',
      '---',
      '',
    ].filter(Boolean).join('\n');

    writeFile(filePath, frontmatter + (note.content || ''));
  }

  // Write book-group notes as merged files
  for (const [book, notes] of bookGroups) {
    idx++;
    const slug = slugify(`读书笔记-${book}`);
    const filePath = path.join(dir, `${padNum(idx, total)}-${slug}.md`);

    const entries = notes.map(n => {
      const date = (n.created_at || '').slice(0, 10);
      return `## ${date}\n\n${n.content || ''}`;
    }).join('\n\n---\n\n');

    const allTags = [...new Set(notes.flatMap(n =>
      (n.tags || '').split(',').map(t => t.trim()).filter(Boolean)
    ))];

    const frontmatter = [
      '---',
      `book: 《${book}》`,
      allTags.length > 0 ? `tags: [${allTags.join(', ')}]` : null,
      'source: DayLoop',
      'type: book-notes',
      '---',
      '',
    ].filter(Boolean).join('\n');

    const body = `# 《${book}》读书笔记\n\n${entries}`;
    writeFile(filePath, frontmatter + body);
  }

  return total;
}

function syncAllReviews() {
  const vaultPath = getVaultPath();
  if (!vaultPath) return 0;

  const dir = path.join(vaultPath, 'DayLoop', '每日复盘');
  deleteDir(dir);
  ensureDir(dir);

  const allReviews = db.prepare('SELECT * FROM daily_reviews ORDER BY date ASC').all();
  for (const review of allReviews) {
    const filePath = path.join(dir, `${review.date}-每日复盘.md`);
    const frontmatter = [
      '---',
      `date: ${review.date}`,
      `created: ${review.created_at || ''}`,
      `updated: ${review.updated_at || ''}`,
      'type: daily-review',
      'source: DayLoop',
      '---',
      '',
    ].join('\n');
    writeFile(filePath, frontmatter + (review.content || ''));
  }
  return allReviews.length;
}

function syncAllAchievements() {
  const vaultPath = getVaultPath();
  if (!vaultPath) return 0;

  const dir = path.join(vaultPath, 'DayLoop', '每日成果');
  deleteDir(dir);
  ensureDir(dir);

  const allAchievements = db.prepare(
    "SELECT * FROM tasks WHERE achievement != '' AND achievement IS NOT NULL AND title != '今日复盘' AND sync_enabled != 0 ORDER BY date ASC, id ASC"
  ).all();
  if (allAchievements.length === 0) return 0;

  // Group by book name, collect standalone
  const bookGroups = new Map();
  const standalone = [];

  for (const task of allAchievements) {
    const book = extractBookName(task.title);
    if (book) {
      if (!bookGroups.has(book)) bookGroups.set(book, []);
      bookGroups.get(book).push(task);
    } else {
      standalone.push(task);
    }
  }

  // Group remaining tasks by title so same-named tasks get merged
  const titleGroups = new Map();
  for (const task of standalone) {
    const key = task.title || '';
    if (!titleGroups.has(key)) titleGroups.set(key, []);
    titleGroups.get(key).push(task);
  }

  // Process title groups
  for (const [title, tasks] of titleGroups) {
    const slug = slugify(title);

    if (tasks.length === 1) {
      // Single task — standalone file with date prefix
      const task = tasks[0];
      const filePath = path.join(dir, `${task.date}-${slug}.md`);

      const tags = (task.tags || '').split(',').map(t => t.trim()).filter(Boolean);
      const frontmatter = [
        '---',
        `date: ${task.date}`,
        `category: ${task.category || ''}`,
        `priority: ${task.priority || 2}`,
        `status: ${task.status || ''}`,
        tags.length > 0 ? `tags: [${tags.join(', ')}]` : null,
        'source: DayLoop',
        'type: achievement',
        '---',
        '',
      ].filter(Boolean).join('\n');

      const body = [
        `# ${title}`,
        '',
        task.achievement ? `> ${task.achievement}` : '',
        '',
        task.note ? `**备注**: ${task.note}` : '',
        '',
        task.start_time || task.end_time ? `**时间**: ${task.start_time || ''}${task.end_time ? ' - ' + task.end_time : ''}` : null,
        task.planned_duration ? `**计划时长**: ${task.planned_duration}分钟` : null,
      ].filter(Boolean).join('\n');

      writeFile(filePath, frontmatter + body);
    } else {
      // Multiple tasks with same title → merged file without date prefix
      const filePath = path.join(dir, `${slug}.md`);

      const entries = tasks.map(t => {
        const date = (t.date || '').slice(0, 10);
        const parts = [
          `## ${date}`,
          '',
          t.achievement ? `> ${t.achievement}` : '',
          '',
          t.note ? `**备注**: ${t.note}` : '',
          '',
          t.start_time || t.end_time ? `**时间**: ${t.start_time || ''}${t.end_time ? ' - ' + t.end_time : ''}` : null,
          t.planned_duration ? `**计划时长**: ${t.planned_duration}分钟` : null,
        ].filter(Boolean);
        return parts.join('\n');
      });

      const allTags = [...new Set(tasks.flatMap(t =>
        (t.tags || '').split(',').map(s => s.trim()).filter(Boolean)
      ))];

      const dates = tasks.map(t => t.date).filter(Boolean);
      const dateRange = dates.length === 1 ? dates[0] : `${dates[0]} ~ ${dates[dates.length - 1]}`;

      const frontmatter = [
        '---',
        `title: ${title}`,
        `date: ${dateRange}`,
        allTags.length > 0 ? `tags: [${allTags.join(', ')}]` : null,
        'source: DayLoop',
        'type: achievement',
        '---',
        '',
      ].filter(Boolean).join('\n');

      writeFile(filePath, frontmatter + `# ${title}\n\n${entries.join('\n\n---\n\n')}`);
    }
  }

  // Book-grouped achievements → merged file
  for (const [book, tasks] of bookGroups) {
    const slug = slugify(`读书笔记-${book}`);
    const filePath = path.join(dir, `${tasks[0].date}-${slug}.md`);

    const entries = tasks.map(t => {
      const date = (t.date || '').slice(0, 10);
      return [
        `## ${date}：${t.title}`,
        '',
        t.achievement ? `> ${t.achievement}` : '',
        '',
        t.note ? `**备注**: ${t.note}` : '',
      ].filter(Boolean).join('\n');
    }).join('\n\n---\n\n');

    const allTags = [...new Set(tasks.flatMap(t =>
      (t.tags || '').split(',').map(s => s.trim()).filter(Boolean)
    ))];

    const frontmatter = [
      '---',
      `book: 《${book}》`,
      allTags.length > 0 ? `tags: [${allTags.join(', ')}]` : null,
      'source: DayLoop',
      'type: book-notes',
      '---',
      '',
    ].filter(Boolean).join('\n');

    writeFile(filePath, frontmatter + `# 《${book}》读书笔记\n\n${entries}`);
  }

  return allAchievements.length;
}

function syncAll() {
  const vaultPath = getVaultPath();
  if (!vaultPath) return { notes: 0, reviews: 0, achievements: 0, error: 'vault path not set' };

  // Clean entire DayLoop folder
  const baseDir = path.join(vaultPath, 'DayLoop');
  deleteDir(baseDir);

  const notes = syncAllNotes();
  const reviews = syncAllReviews();
  const achievements = syncAllAchievements();

  return { notes, reviews, achievements };
}

// Real-time hooks: re-sync just the affected type
function syncNote() { return syncAllNotes(); }
function syncReview() { return syncAllReviews(); }
function syncAchievement() { return syncAllAchievements(); }

module.exports = { syncNote, syncReview, syncAchievement, syncAll };
