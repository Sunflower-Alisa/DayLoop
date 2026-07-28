import type { Task, DailyReview, RecurringTemplate, Note, Question } from '../types'
import { auth } from '../store/auth'

function getBase(): string {
  return localStorage.getItem('dayloop_server_url') || ''
}

function apiUrl(path: string): string {
  const base = getBase()
  return base ? `${base}/api${path}` : `/api${path}`
}

function fullUrl(path: string): string {
  const base = getBase()
  return base ? `${base}${path}` : path
}

export function setServerUrl(url: string) {
  localStorage.setItem('dayloop_server_url', url.replace(/\/+$/, ''))
}

export function getServerUrl(): string {
  return getBase() || '(本机)'
}

function authHeaders(): Record<string, string> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (auth.token) {
    headers['Authorization'] = `Bearer ${auth.token}`
  }
  return headers
}

async function request<T>(url: string, options?: RequestInit): Promise<T> {
  const res = await fetch(apiUrl(url), {
    headers: authHeaders(),
    ...options,
  })
  if (res.status === 401) {
    auth.logout()
    window.location.hash = '#/login'
    throw new Error('未登录')
  }
  if (!res.ok) throw new Error(`HTTP ${res.status}`)
  return res.json()
}

export const api = {
  getVersion(): Promise<{ version: string; server: string; lanIP: string; port: number }> {
    return fetch(fullUrl('/api/version')).then(r => r.json())
  },

  register(username: string, password: string): Promise<{ token: string; user: { id: number; username: string } }> {
    return request('/auth/register', { method: 'POST', body: JSON.stringify({ username, password }) })
  },

  login(username: string, password: string): Promise<{ token: string; user: { id: number; username: string } }> {
    return request('/auth/login', { method: 'POST', body: JSON.stringify({ username, password }) })
  },

  getMe(): Promise<{ id: number; username: string; created_at: string }> {
    return request('/auth/me')
  },

  changePassword(oldPassword: string, newPassword: string): Promise<{ message: string }> {
    return request('/auth/password', { method: 'PUT', body: JSON.stringify({ oldPassword, newPassword }) })
  },

  deleteAccount(): Promise<{ message: string }> {
    return request('/auth/account', { method: 'DELETE' })
  },

  getTasks(date?: string): Promise<Task[]> {
    const q = date ? `?date=${date}` : ''
    return request(`/tasks${q}`)
  },

  getTask(id: number): Promise<Task> {
    return request(`/tasks/${id}`)
  },

  createTask(task: {
    date: string; title: string; start_time?: string; end_time?: string;
    planned_duration?: number; category?: string; priority?: number;
    note?: string; is_recurring?: boolean; is_planned?: boolean
  }): Promise<Task> {
    return request('/tasks', { method: 'POST', body: JSON.stringify(task) })
  },

  updateTask(id: number, data: Partial<Task>): Promise<Task> {
    return request(`/tasks/${id}`, { method: 'PUT', body: JSON.stringify(data) })
  },

  deleteTask(id: number): Promise<void> {
    return request(`/tasks/${id}`, { method: 'DELETE' })
  },

  copyTask(id: number, date?: string): Promise<Task> {
    return request(`/tasks/${id}/copy`, { method: 'POST', body: JSON.stringify({ date }) })
  },

  getReview(date: string): Promise<DailyReview | null> {
    return request(`/reviews?date=${date}`)
  },

  saveReview(date: string, content: string): Promise<DailyReview> {
    return request(`/reviews/${date}`, { method: 'PUT', body: JSON.stringify({ content }) })
  },

  getRecurringTemplates(): Promise<RecurringTemplate[]> {
    return request('/recurring')
  },

  createRecurringTemplate(data: Partial<RecurringTemplate>): Promise<RecurringTemplate> {
    return request('/recurring', { method: 'POST', body: JSON.stringify(data) })
  },

  updateRecurringTemplate(id: number, data: Partial<RecurringTemplate>): Promise<RecurringTemplate> {
    return request(`/recurring/${id}`, { method: 'PUT', body: JSON.stringify(data) })
  },

  deleteRecurringTemplate(id: number): Promise<void> {
    return request(`/recurring/${id}`, { method: 'DELETE' })
  },

  generateRecurringTasks(date: string): Promise<Task[]> {
    return request('/recurring/generate', { method: 'POST', body: JSON.stringify({ date }) })
  },

  getAchievements(category?: string): Promise<Task[]> {
    const q = category ? `?category=${encodeURIComponent(category)}` : ''
    return request(`/achievements${q}`)
  },

  getAchievementCategories(): Promise<string[]> {
    return request('/achievements/categories')
  },

  uploadImage(dataUrl: string): Promise<{ url: string }> {
    return request('/upload/image', { method: 'POST', body: JSON.stringify({ dataUrl }) })
  },

  getNotes(category?: string, search?: string): Promise<Note[]> {
    const params = new URLSearchParams()
    if (category) params.set('category', category)
    if (search) params.set('search', search)
    const q = params.toString() ? `?${params.toString()}` : ''
    return request(`/notes${q}`)
  },

  getNote(id: number): Promise<Note> {
    return request(`/notes/${id}`)
  },

  createNote(data: { title: string; content?: string; category?: string; tags?: string; task_ids?: number[] }): Promise<Note> {
    return request('/notes', { method: 'POST', body: JSON.stringify(data) })
  },

  updateNote(id: number, data: Partial<Note>): Promise<Note> {
    return request(`/notes/${id}`, { method: 'PUT', body: JSON.stringify(data) })
  },

  deleteNote(id: number): Promise<void> {
    return request(`/notes/${id}`, { method: 'DELETE' })
  },

  getNoteCategories(): Promise<string[]> {
    return request('/notes/categories')
  },

  createNoteCategory(name: string): Promise<{ name: string }> {
    return request('/notes/categories', { method: 'POST', body: JSON.stringify({ name }) })
  },

  deleteNoteCategory(name: string): Promise<void> {
    return request(`/notes/categories/${encodeURIComponent(name)}`, { method: 'DELETE' })
  },

  getQuestions(category?: string, search?: string): Promise<Question[]> {
    const params = new URLSearchParams()
    if (category) params.set('category', category)
    if (search) params.set('search', search)
    const q = params.toString() ? `?${params.toString()}` : ''
    return request(`/questions${q}`)
  },

  getQuestion(id: number): Promise<Question> {
    return request(`/questions/${id}`)
  },

  createQuestion(data: { title: string; content?: string; answer?: string; answer_source?: string; category?: string; tags?: string; task_ids?: number[] }): Promise<Question> {
    return request('/questions', { method: 'POST', body: JSON.stringify(data) })
  },

  updateQuestion(id: number, data: Partial<Question>): Promise<Question> {
    return request(`/questions/${id}`, { method: 'PUT', body: JSON.stringify(data) })
  },

  deleteQuestion(id: number): Promise<void> {
    return request(`/questions/${id}`, { method: 'DELETE' })
  },

  getQuestionCategories(): Promise<string[]> {
    return request('/questions/categories')
  },

  createQuestionCategory(name: string): Promise<{ name: string }> {
    return request('/questions/categories', { method: 'POST', body: JSON.stringify({ name }) })
  },

  deleteQuestionCategory(name: string): Promise<void> {
    return request(`/questions/categories/${encodeURIComponent(name)}`, { method: 'DELETE' })
  },

  exportJson(): Promise<Blob> {
    return fetch(apiUrl('/export/json'), { headers: authHeaders() }).then(r => r.blob())
  },

  getSettings(): Promise<Record<string, string>> {
    return request('/settings')
  },

  updateSetting(key: string, value: string): Promise<{ key: string; value: string }> {
    return request('/settings', { method: 'PUT', body: JSON.stringify({ key, value }) })
  },

  syncAllObsidian(): Promise<{ message: string; notes: number; reviews: number; achievements: number }> {
    return request('/settings/sync-all', { method: 'POST' })
  },

  getStats(): Promise<{
    totalTasks: number
    completedTasks: number
    cancelledTasks: number
    inProgressTasks: number
    plannedTasks: number
    completionRate: number
    totalNotes: number
    totalReviews: number
    weeklyStats: { week: string; total: number; completed: number }[]
  }> {
    return request('/stats')
  }
}
