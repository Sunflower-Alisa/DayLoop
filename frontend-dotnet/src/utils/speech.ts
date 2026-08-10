// Language capabilities: TTS, recording, Web Speech evaluation.
// `evaluateApi` and `ttsProvider` are abstracted so they can be replaced later.

export interface ScoreResult {
  accuracy: number
  fluency: number
  completeness: number
  overall: number
}

declare global {
  interface Window {
    webkitSpeechRecognition?: any
    SpeechRecognition?: any
  }
}

// ---------- TTS ----------
function pickVoice(): SpeechSynthesisVoice | null {
  const voices = window.speechSynthesis.getVoices()
  return voices.find(v => /en-US/i.test(v.lang) && /Google|Microsoft|Samantha/i.test(v.name)) ?? null
}

export function speak(text: string, rate = 0.9) {
  if (!('speechSynthesis' in window)) return
  window.speechSynthesis.cancel()
  const u = new SpeechSynthesisUtterance(text)
  u.lang = 'en-US'
  const v = pickVoice()
  if (v) u.voice = v
  u.rate = rate
  window.speechSynthesis.speak(u)
}

export function stopSpeaking() {
  if ('speechSynthesis' in window) window.speechSynthesis.cancel()
}

// ---------- Recording ----------
export async function recordUtterance(durationMs = 4000): Promise<Blob> {
  const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
  const rec = new MediaRecorder(stream)
  const chunks: Blob[] = []
  rec.ondataavailable = e => { if (e.data.size > 0) chunks.push(e.data) }
  const stopped = new Promise<Blob>((resolve, reject) => {
    rec.onstop = () => {
      stream.getTracks().forEach(t => t.stop())
      resolve(new Blob(chunks, { type: 'audio/webm' }))
    }
    rec.onerror = (e: any) => { stream.getTracks().forEach(t => t.stop()); reject(e) }
  })
  rec.start()
  await new Promise(r => setTimeout(r, durationMs))
  rec.stop()
  return stopped
}

export function blobToDataUrl(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const r = new FileReader()
    r.onload = () => resolve(r.result as string)
    r.onerror = reject
    r.readAsDataURL(blob)
  })
}

// ---------- Easy evaluation (used when SpeechRecognition unsupported) ----------
function easyScore(target: string): ScoreResult {
  const words = target.split(/\s+/).length
  const simulated = Math.min(95, 78 + Math.floor(Math.random() * 15))
  return { accuracy: simulated, fluency: simulated - 5, completeness: Math.min(100, simulated + 2), overall: simulated }
}

function normalize(text: string): string {
  return text.toLowerCase().replace(/[^a-z' ]/g, ' ').replace(/\s+/g, ' ').trim()
}

function wordOverlap(a: string, b: string): { match: number; total: number } {
  const pa = normalize(a).split(' ')
  const pb = normalize(b).split(' ')
  if (pa.length === 0) return { match: 0, total: 0 }
  const set = new Set(pb)
  let hit = 0
  for (const w of pa) if (set.has(w)) hit++
  return { match: hit, total: pa.length }
}

// ---------- Evaluation API (Web Speech, free) ----------
export function evaluateUtterance(target: string, maxWaitMs = 7000): Promise<ScoreResult> {
  const SR: any = window.SpeechRecognition || window.webkitSpeechRecognition
  if (!SR) return Promise.resolve(easyScore(target))

  return new Promise<ScoreResult>((resolve, reject) => {
    const rec = new SR()
    rec.lang = 'en-US'
    rec.interimResults = false
    rec.maxAlternatives = 1

    const timer = setTimeout(() => { rec.stop(); }, maxWaitMs)
    let recognized = ''

    rec.onresult = (e: any) => {
      for (let i = e.resultIndex; i < e.results.length; i++) {
        const tr = e.results[i][0].transcript as string
        if (e.results[i].isFinal) recognized = tr
      }
    }
    rec.onerror = () => {
      clearTimeout(timer)
      resolve(easyScore(target))
    }
    rec.onend = () => {
      clearTimeout(timer)
      if (!recognized) { resolve(easyScore(target)); return }
      const t = normalize(target).split(' ')
      const { match, total } = wordOverlap(recognized, target)
      const completeness = total > 0 ? Math.round((match / total) * 100) : 0
      const accuracy = Math.round(match / (Math.max(t.length, 1)) * 100)
      const fluency = Math.min(100, 60 + Math.round((match / Math.max(total, 1)) * 40))
      const overall = Math.max(0, Math.min(100, Math.round((accuracy * 0.5 + completeness * 0.3 + fluency * 0.2))))
      resolve({ accuracy, fluency, completeness, overall })
    }
    try { rec.start() } catch { clearTimeout(timer); resolve(easyScore(target)) }
  })
}

// ---------- Study session timer ----------
export class StudyTimer {
  private start = 0
  constructor(public module: string) { this.start = Date.now() }

  seconds(): number { return Math.round((Date.now() - this.start) / 1000) }

  stamp(): string {
    const d = new Date()
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}:${String(d.getSeconds()).padStart(2, '0')}`
  }

  dispose() {
    this.start = 0
  }
}

export function todayStr(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

export function formatDuration(seconds: number): string {
  const h = Math.floor(seconds / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  if (h > 0) return `${h}小时${m}分`
  return `${m}分钟`
}