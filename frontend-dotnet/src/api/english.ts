import { auth } from '../store/auth'
import type {
  WordBook, Word, DailyWordTask, Scenario, ScenarioDetail,
  SpeakingTopic, SpeakingRecord, SpeakingLine, VideoClip, ClipLine,
  EnglishDashboard, SessionStats, LearnResult,
} from '../types/english'

function getBase(): string {
  return localStorage.getItem('dayloop_server_url') || ''
}

function apiUrl(path: string): string {
  const base = getBase()
  return base ? `${base}/api${path}` : `/api${path}`
}

function authHeaders(): Record<string, string> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (auth.token) headers['Authorization'] = `Bearer ${auth.token}`
  return headers
}

async function request<T>(url: string, options?: RequestInit): Promise<T> {
  const res = await fetch(apiUrl(url), { headers: authHeaders(), ...options })
  if (res.status === 401) {
    auth.logout()
    window.location.hash = '#/login'
    throw new Error('未登录')
  }
  if (!res.ok) throw new Error(`HTTP ${res.status}`)
  const text = await res.text()
  if (!text) return null as T
  return JSON.parse(text)
}

function body(data: unknown): RequestInit {
  return { method: 'POST', body: JSON.stringify(data) }
}

export const englishApi = {
  dashboard(): Promise<EnglishDashboard> {
    return request('/english/dashboard')
  },
  streak(): Promise<{ streak: number }> {
    return request('/english/streak')
  },
  saveSession(module: string, startTime: string, endTime: string, durationSeconds: number): Promise<{ ok: boolean }> {
    return request('/english/sessions', body({ module, start_time: startTime, end_time: endTime, duration_seconds: durationSeconds }))
  },
  sessions(): Promise<SessionStats> {
    return request('/english/sessions')
  },

  getBooks(): Promise<WordBook[]> {
    return request('/words/books')
  },
  createBook(data: { name: string; level?: string; description?: string; cover_color?: string }): Promise<{ id: number }> {
    return request('/words/books', body(data))
  },
  getBookWords(id: number): Promise<{ book_id: number; words: Word[] }> {
    return request(`/words/books/${id}`)
  },
  setGoal(id: number, dailyGoal: number): Promise<{ daily_goal: number }> {
    return request(`/words/books/${id}/goal`, { method: 'PUT', body: JSON.stringify({ daily_goal: dailyGoal }) })
  },
  getDaily(): Promise<DailyWordTask> {
    return request('/words/daily')
  },
  submitLearn(r: LearnResult): Promise<{ ok: boolean }> {
    return request('/words/learn', body(r))
  },
  getWrongWords(): Promise<Word[]> {
    return request('/words/wrong')
  },
  removeWrongWord(wordId: number): Promise<{ ok: boolean }> {
    return request(`/words/wrong/${wordId}`, { method: 'DELETE' })
  },
  getWord(id: number): Promise<Word> {
    return request(`/words/${id}`)
  },

  getScenarios(category?: string): Promise<Scenario[]> {
    const q = category ? `?category=${encodeURIComponent(category)}` : ''
    return request(`/scenarios${q}`)
  },
  getScenario(id: number): Promise<ScenarioDetail> {
    return request(`/scenarios/${id}`)
  },
  submitQuiz(scenarioId: number, total: number, correct: number): Promise<{ mastered: boolean }> {
    return request(`/scenarios/${scenarioId}/quiz`, body({ scenario_id: scenarioId, total, correct }))
  },

  getSpeakingTopics(category?: string): Promise<SpeakingTopic[]> {
    const q = category ? `?category=${encodeURIComponent(category)}` : ''
    return request(`/speaking/topics${q}`)
  },
  getSpeakingTopic(id: number): Promise<SpeakingTopic> {
    return request(`/speaking/topics/${id}`)
  },
  saveSpeakingRecord(r: {
    topic_id: number; line_index: number; audio_url?: string;
    accuracy: number; fluency: number; completeness: number; overall: number
  }): Promise<{ ok: boolean }> {
    return request('/speaking/records', body(r))
  },

  getClips(source?: string, level?: string): Promise<VideoClip[]> {
    const params = new URLSearchParams()
    if (source) params.set('source', source)
    if (level) params.set('level', level)
    const q = params.toString() ? `?${params.toString()}` : ''
    return request(`/clips${q}`)
  },
  getClip(id: number): Promise<{ clip: VideoClip; lines: ClipLine[] }> {
    return request(`/clips/${id}`)
  },
}