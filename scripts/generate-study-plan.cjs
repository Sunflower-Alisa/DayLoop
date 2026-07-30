const API = 'http://localhost:3001/api'
const TOKEN = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6MSwidXNlcm5hbWUiOiJhbGlzYSIsImlhdCI6MTc4NTM3NDM1NywiZXhwIjoxNzg3OTY2MzU3fQ.epKB5xn6pBFrqQDDGYE-b4R1oq9srTHdEBjqzdzLQKM'

const headers = { 'Content-Type': 'application/json', 'Authorization': `Bearer ${TOKEN}` }

async function api(path, opts = {}) {
  const res = await fetch(`${API}${path}`, { headers, ...opts })
  if (!res.ok) {
    let msg = ''
    try { msg = JSON.stringify(await res.json()) } catch { msg = res.statusText }
    console.error(`ERROR ${res.status}:`, msg); process.exit(1)
  }
  if (res.status === 204) return null
  return res.json()
}

function fmtDate(d) {
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

// Week definitions
const weeks = [
  {
    name: '第1周(适应期)',
    morning: { title: '🌅 听力唤醒·听写训练', note: `【0-5min】热身：播放一段英语播客或慢速英语，让大脑进入英语状态
【5-30min】核心训练·听音写词：选一段1-2分钟VOA慢速英语，只听声音不看文本，把你听到的、认识的单词写下来。写完后对照文本，标出"听出来了但拼写犹豫"和"完全没听出来"的词
【30-45min】影子跟读：回放音频，不看文本，延迟0.5秒跟读（像回声一样），重点模仿发音和语调
【45-60min】复习闪卡：把标注的词加入生词本，看英文→想中文，快速过一遍
【最后5min】总结：记录今天新"听出来"的单词数` },
    evening: { title: '🌙 主题词汇拓展', note: `【0-5min】回顾早上音频：闭上眼睛回想早上那段音频的旋律和出现的词，激活记忆
【5-35min】核心训练·中译英闪卡：加载早上的生词，只显示中文→努力说出英文。说不出/说错的点击"重来"，直到能秒答。每天新增15个词
【35-45min】主动造句：从今天的生词中随机抽5个，每个词造1个简单句（用你自己的生活场景）
【45-55min】泛听背景音：播放一集你熟悉的英文情景喜剧或英语播客，不看画面，只当背景音
【最后5min】明日计划：选好明天早上要听的音频，提前准备好` },
  },
  {
    name: '第2周(提速期)',
    morning: { title: '🌅 盲听+跟读训练', note: `【0-5min】热身：播放英语播客，让大脑进入英语状态
【5-30min】核心训练·盲听写词：选一段1-2分钟BBC 6 Minute English，只听声音不看文本，写听到的词。写完后对照文本标出问题词
【30-45min】影子跟读：回放音频，不看文本，延迟0.5秒跟读，模仿发音和语调
【45-60min】复习闪卡：把标注的词加入生词本，看英文→想中文
【最后5min】总结：记录今天新"听出来"的单词数` },
    evening: { title: '🌙 主动造句训练', note: `【0-5min】回顾早上音频：回想早上那段音频的旋律和出现的词
【5-35min】核心训练·中译英闪卡：加载早上的生词，只显示中文→努力说出英文。每天新增20个词
【35-45min】主动造句：从今天的生词中随机抽5个，每个词造1个完整句（哪怕很简单），写在纸上或手机备忘录里
【45-55min】泛听背景音：播放熟悉的英文情景喜剧或英语播客，不看画面
【最后5min】明日计划：选好明天早上要听的音频` },
  },
  {
    name: '第3周(挑战期)',
    morning: { title: '🌅 快速闪卡+听音辨词', note: `【0-5min】热身：播放英语播客，进入英语状态
【5-30min】核心训练·听音辨词：选一段TED-Ed短片（5分钟左右），第1遍关字幕听写，写出听到的词。开字幕对照，标出没听出来的词
【30-45min】影子跟读：回放短片，延迟0.5秒跟读，重点模仿发音和语调
【45-60min】复习闪卡：把标注的词加入生词本，看英文→想中文
【最后5min】总结：记录今天新"听出来"的单词数` },
    evening: { title: '🌙 情景对话听写', note: `【0-5min】回顾早上音频：回想早上的TED-Ed内容
【5-35min】核心训练·中译英闪卡：加载早上的生词，只显示中文→努力说出英文。每天新增25个词
【35-45min】主动造句：从今天的生词中随机抽5个，尝试用2个生词组合在一句话里
【45-55min】泛听背景音：播放熟悉的英文情景喜剧，不看画面
【最后5min】明日计划：选好明天要听的TED-Ed短片` },
  },
  {
    name: '第4周(实战期)',
    morning: { title: '🌅 无字幕听力+复述', note: `【0-5min】热身：播放英语播客，进入英语状态
【5-30min】核心训练·听写全文：听一段1分钟音频，尝试完整听写全文。写完后对照文本标出遗漏和错误
【30-45min】影子跟读+复述：回放音频跟读，然后尝试完整复述大意（用中文即可）
【45-60min】复习闪卡：把标注的词加入生词本，看英文→想中文
【最后5min】总结：记录今天新"听出来"的单词数` },
    evening: { title: '🌙 主题口语输出', note: `【0-5min】回顾早上音频：回想早上听写的全文内容
【5-35min】核心训练·中译英闪卡：加载早上的生词，只显示中文→努力说出英文。每天新增25个词
【35-45min】口述日记：用当天学的词说一段话（口述日记），记录在手机上
【45-55min】泛听背景音：播放熟悉的英文情景喜剧或播客，不看画面
【最后5min】明日计划：选好明天要听写的音频` },
  },
]

async function run() {
  // Delete existing tasks for the next 30 days
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  for (let day = 0; day < 30; day++) {
    const d = new Date(today)
    d.setDate(d.getDate() + day)
    const dateStr = fmtDate(d)
    const existing = await api(`/tasks?date=${dateStr}`)
    for (const t of existing) {
      if (t.category === '英语学习') {
        await api(`/tasks/${t.id}`, { method: 'DELETE' })
      }
    }
  }
  // Also delete old templates with '英语学习' category
  const oldTemplates = await api('/recurring')
  for (const t of oldTemplates) {
    if (t.category === '英语学习') {
      await api(`/recurring/${t.id}`, { method: 'DELETE' })
      console.log(`[Cleanup] Deleted old template: ${t.title} (id=${t.id})`)
    }
  }

  // Create 8 recurring templates (disabled)
  const templates = []
  for (let w = 0; w < weeks.length; w++) {
    const wk = weeks[w]
    for (const session of ['morning', 'evening']) {
      const s = wk[session]
      const isMorning = session === 'morning'
      const t = await api('/recurring', {
        method: 'POST',
        body: JSON.stringify({
          title: s.title,
          start_time: isMorning ? '07:30' : '22:00',
          end_time: isMorning ? '08:30' : '23:00',
          planned_duration: 60,
          category: '英语学习',
          priority: 2,
          note: s.note,
          recurrence_type: 'daily',
          recurrence_days: '',
          recurring_enabled: 0,
          sync_enabled: 0,
        })
      })
      templates.push(t)
      console.log(`[Template] Created: ${s.title} (id=${t.id})`)
    }
  }

  // Create daily tasks for next 30 days
  for (let day = 0; day < 30; day++) {
    const d = new Date(today)
    d.setDate(d.getDate() + day)

    // Determine which week this day falls in
    const weekIndex = Math.min(Math.floor(day / 7), 3)
    const wk = weeks[weekIndex]
    const dateStr = fmtDate(d)

    for (const session of ['morning', 'evening']) {
      const s = wk[session]
      const isMorning = session === 'morning'
      const task = await api('/tasks', {
        method: 'POST',
        body: JSON.stringify({
          date: dateStr,
          title: s.title,
          start_time: isMorning ? '07:30' : '22:00',
          end_time: isMorning ? '08:30' : '23:00',
          planned_duration: 60,
          category: '英语学习',
          priority: 2,
          note: s.note,
          is_planned: true,
        })
      })
      console.log(`[Task] ${dateStr} ${isMorning ? 'AM' : 'PM'}: ${s.title} (id=${task.id})`)
    }
  }

  console.log('\nDone! Created', templates.length, 'templates and 60 tasks.')
}

run().catch(e => { console.error(e); process.exit(1) })
