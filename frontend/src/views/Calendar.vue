<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import { api } from '../api'
import type { Task } from '../types'

const views = ['weekly', 'monthly', 'quarterly'] as const
type ViewType = typeof views[number]
const activeView = ref<ViewType>('monthly')

const now = new Date()
const currentYear = ref(now.getFullYear())
const currentMonth = ref(now.getMonth() + 1)
const currentWeekStart = ref(getWeekStart(now))
const tasks = ref<Task[]>([])

function getWeekStart(d: Date): string {
  const day = d.getDay()
  const diff = d.getDate() - day + (day === 0 ? -6 : 1)
  const m = new Date(d); m.setDate(diff)
  return `${m.getFullYear()}-${String(m.getMonth() + 1).padStart(2, '0')}-${String(m.getDate()).padStart(2, '0')}`
}

function getWeekEnd(start: string): string {
  const d = new Date(start + 'T00:00:00')
  d.setDate(d.getDate() + 6)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function fmt(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function viewTitle(): string {
  if (activeView.value === 'weekly') {
    const end = getWeekEnd(currentWeekStart.value)
    return `${currentWeekStart.value} ~ ${end}`
  }
  if (activeView.value === 'monthly') return `${currentYear.value}年${currentMonth.value}月`
  const q = Math.ceil(currentMonth.value / 3)
  return `${currentYear.value}年第${q}季度`
}

function shiftView(delta: number) {
  if (activeView.value === 'weekly') {
    const d = new Date(currentWeekStart.value + 'T00:00:00')
    d.setDate(d.getDate() + delta * 7)
    currentWeekStart.value = fmt(d)
  } else if (activeView.value === 'monthly') {
    currentMonth.value += delta
    if (currentMonth.value < 1) { currentMonth.value = 12; currentYear.value-- }
    if (currentMonth.value > 12) { currentMonth.value = 1; currentYear.value++ }
  } else {
    currentMonth.value += delta * 3
    if (currentMonth.value < 1) { currentMonth.value = 12; currentYear.value-- }
    if (currentMonth.value > 12) { currentMonth.value = 1; currentYear.value++ }
  }
  loadTasks()
}

function getDateRange(): { start: string; end: string } {
  if (activeView.value === 'weekly') {
    return { start: currentWeekStart.value, end: getWeekEnd(currentWeekStart.value) }
  }
  if (activeView.value === 'monthly') {
    const start = new Date(currentYear.value, currentMonth.value - 1, 1)
    const end = new Date(currentYear.value, currentMonth.value, 0)
    return { start: fmt(start), end: fmt(end) }
  }
  const q = Math.ceil(currentMonth.value / 3)
  const start = new Date(currentYear.value, (q - 1) * 3, 1)
  const end = new Date(currentYear.value, q * 3, 0)
  return { start: fmt(start), end: fmt(end) }
}

function isToday(d: string): boolean {
  const t = new Date()
  return d === fmt(t)
}

function getDays(): { date: string; day: number; weekday: number; isCurrentMonth: boolean }[] {
  const { start, end } = getDateRange()
  const s = new Date(start + 'T00:00:00')
  const e = new Date(end + 'T00:00:00')
  const days: { date: string; day: number; weekday: number; isCurrentMonth: boolean }[] = []

  if (activeView.value === 'monthly') {
    const firstWeekday = s.getDay() === 0 ? 6 : s.getDay() - 1
    const prevMonth = new Date(s); prevMonth.setDate(0)
    for (let i = firstWeekday - 1; i >= 0; i--) {
      const d = new Date(prevMonth); d.setDate(prevMonth.getDate() - i)
      days.push({ date: fmt(d), day: d.getDate(), weekday: d.getDay(), isCurrentMonth: false })
    }
  }

  const d = new Date(s)
  while (d <= e) {
    days.push({ date: fmt(d), day: d.getDate(), weekday: d.getDay(), isCurrentMonth: true })
    d.setDate(d.getDate() + 1)
  }

  if (activeView.value === 'monthly' && days.length < 42) {
    const last = new Date(days[days.length - 1].date + 'T00:00:00')
    for (let i = 1; days.length < 42; i++) {
      const nd = new Date(last); nd.setDate(last.getDate() + i)
      days.push({ date: fmt(nd), day: nd.getDate(), weekday: nd.getDay(), isCurrentMonth: false })
    }
  }

  return days
}

const weekHeaders = ['一', '二', '三', '四', '五', '六', '日']

function tasksForDate(date: string): Task[] {
  return tasks.value.filter(t => t.date === date)
}

function totalDuration(tasks: Task[]): number {
  return tasks.reduce((s, t) => s + (t.planned_duration || 0), 0)
}

function freeTime(date: string): string {
  const dayTasks = tasksForDate(date).filter(t => t.start_time && t.end_time)
  if (dayTasks.length === 0) return '全天空闲'
  
  // Merge overlapping intervals per FEATURES.md spec
  const intervals: { start: number; end: number }[] = []
  for (const t of dayTasks) {
    const [sh, sm] = t.start_time!.split(':').map(Number)
    const [eh, em] = t.end_time!.split(':').map(Number)
    intervals.push({ start: sh * 60 + sm, end: eh * 60 + em })
  }
  intervals.sort((a, b) => a.start - b.start)
  
  // Merge overlapping
  const merged = [intervals[0]]
  for (let i = 1; i < intervals.length; i++) {
    const last = merged[merged.length - 1]
    if (intervals[i].start <= last.end) {
      last.end = Math.max(last.end, intervals[i].end)
    } else {
      merged.push(intervals[i])
    }
  }
  
  const occupied = merged.reduce((sum, m) => sum + (m.end - m.start), 0)
  const totalWindow = 720 // 12h window per spec (6:00-18:00)
  const freeMin = totalWindow - occupied
  if (freeMin >= 480) return '充裕'
  if (freeMin >= 240) return '较多'
  if (freeMin >= 120) return '适中'
  if (freeMin > 0) return '较紧'
  return '已满'
}

const days = computed(() => getDays())

function goToday() {
  const t = new Date()
  currentYear.value = t.getFullYear()
  currentMonth.value = t.getMonth() + 1
  currentWeekStart.value = getWeekStart(t)
  activeView.value = 'monthly'
  loadTasks()
}

async function loadTasks() {
  const { start, end } = getDateRange()
  tasks.value = await api.getTasksRange(start, end)
}

function priorityColor(p: number): string {
  if (p === 1) return 'var(--danger)'
  if (p === 2) return 'var(--warning)'
  return 'var(--primary)'
}

function statusIcon(s: string): string {
  return s === 'completed' ? '✅' : s === 'in_progress' ? '⏳' : s === 'cancelled' ? '❌' : '📋'
}

onMounted(loadTasks)
watch(activeView, loadTasks)
</script>

<template>
  <div class="calendar-page">
    <div class="section-header">
      <div class="view-tabs">
        <button v-for="v in views" :key="v" :class="['tab', { active: activeView === v }]" @click="activeView = v">
          {{ { weekly: '周视图', monthly: '月视图', quarterly: '季度视图' }[v] }}
        </button>
      </div>
      <div class="nav-row">
        <button class="btn-nav" @click="shiftView(-1)">&lt;</button>
        <h2>{{ viewTitle() }}</h2>
        <button class="btn-nav" @click="shiftView(1)">&gt;</button>
        <button class="btn btn-sm" @click="goToday">今天</button>
      </div>
    </div>

    <!-- Weekly View -->
    <div v-if="activeView === 'weekly'" class="week-view">
      <div class="week-grid">
        <div v-for="day in days" :key="day.date" :class="['day-col', { today: isToday(day.date) }]">
          <div class="day-header">
            <span class="weekday">{{ weekHeaders[day.weekday === 0 ? 6 : day.weekday - 1] }}</span>
            <span class="daynum">{{ day.day }}</span>
            <span :class="['free-badge', freeTime(day.date)]">{{ freeTime(day.date) }}</span>
          </div>
          <div class="day-tasks">
            <div v-for="task in tasksForDate(day.date)" :key="task.id" class="task-chip" :style="{ borderLeftColor: priorityColor(task.priority) }">
              <span class="task-icon">{{ statusIcon(task.status) }}</span>
              <span class="task-title">{{ task.title }}</span>
              <span v-if="task.start_time" class="task-time">{{ task.start_time }}-{{ task.end_time }}</span>
            </div>
            <div v-if="tasksForDate(day.date).length === 0" class="no-tasks">无任务</div>
          </div>
        </div>
      </div>
    </div>

    <!-- Monthly / Quarterly View -->
    <div v-else class="month-view">
      <div class="weekday-header">
        <span v-for="w in weekHeaders" :key="w">{{ w }}</span>
      </div>
      <div class="day-grid">
        <div v-for="day in days" :key="day.date" :class="['day-cell', { today: isToday(day.date), 'other-month': !day.isCurrentMonth }]">
          <div class="day-cell-header">
            <span class="day-num">{{ day.day }}</span>
            <span :class="['free-badge-sm', freeTime(day.date)]">{{ freeTime(day.date) }}</span>
          </div>
          <div class="day-cell-tasks">
            <div v-for="task in tasksForDate(day.date).slice(0, 3)" :key="task.id" class="task-dot" :style="{ background: priorityColor(task.priority) }" :title="task.title">
              {{ task.title.slice(0, 6) }}{{ task.title.length > 6 ? '…' : '' }}
            </div>
            <div v-if="tasksForDate(day.date).length > 3" class="more-tasks">+{{ tasksForDate(day.date).length - 3 }}</div>
          </div>
        </div>
      </div>
    </div>

    <!-- Legend -->
    <div class="legend">
      <span><span class="dot" style="background:var(--danger)"></span> 高优先级</span>
      <span><span class="dot" style="background:var(--warning)"></span> 中优先级</span>
      <span><span class="dot" style="background:var(--primary)"></span> 低优先级</span>
      <span class="sep">|</span>
      <span class="free-lbl">空闲(按10h):</span>
      <span><span class="badge-free">充裕</span> ≥8h</span>
      <span><span class="badge-free">较多</span> 4-8h</span>
      <span><span class="badge-free">适中</span> 2-4h</span>
      <span><span class="badge-free">较紧</span> &lt;2h</span>
      <span><span class="badge-free">已满</span> 0h</span>
    </div>
  </div>
</template>

<style scoped>
.section-header {
  margin-bottom: 16px;
}

.view-tabs {
  display: flex;
  gap: 4px;
  margin-bottom: 12px;
  background: var(--card);
  border-radius: var(--radius);
  padding: 4px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
}

.tab {
  flex: 1;
  padding: 8px 4px;
  border: none;
  border-radius: 6px;
  background: transparent;
  color: var(--text-secondary);
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s;
}

.tab.active {
  background: var(--primary);
  color: white;
  font-weight: 600;
}

.nav-row {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
}

.nav-row h2 {
  font-size: 15px;
  color: var(--text-secondary);
  font-weight: 500;
  min-width: 200px;
  text-align: center;
}

.btn-nav {
  width: 32px; height: 32px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--card);
  color: var(--text);
  font-size: 16px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}

.btn-nav:hover {
  background: var(--primary);
  color: white;
  border-color: var(--primary);
}

.btn-sm {
  padding: 4px 12px;
  font-size: 12px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--card);
  color: var(--text-secondary);
  cursor: pointer;
}

.btn-sm:hover {
  background: var(--primary);
  color: white;
}

/* Weekly View */
.week-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 6px;
  background: var(--card);
  border-radius: var(--radius);
  padding: 12px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
  overflow: hidden;
}

.day-col {
  min-height: 200px;
  max-height: calc(100vh - 280px);
  overflow: hidden;
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.day-col.today .day-header {
  background: var(--primary);
  color: white;
  border-radius: 8px;
}

.day-header {
  text-align: center;
  padding: 6px 4px;
  margin-bottom: 6px;
}

.weekday {
  display: block;
  font-size: 11px;
  opacity: 0.7;
}

.daynum {
  display: block;
  font-size: 18px;
  font-weight: 700;
}

.free-badge {
  display: inline-block;
  font-size: 10px;
  padding: 1px 6px;
  border-radius: 4px;
  margin-top: 3px;
}

.free-badge.充裕 { background: #d1fae5; color: #059669; }
.free-badge.较多 { background: #dbeafe; color: #2563eb; }
.free-badge.适中 { background: #fef3c7; color: #d97706; }
.free-badge.较紧 { background: #fee2e2; color: #dc2626; }
.free-badge.已满 { background: #fecaca; color: #b91c1c; }
.free-badge.全天空闲 { background: #d1fae5; color: #059669; }

.day-tasks {
  display: flex;
  flex-direction: column;
  gap: 4px;
  overflow-y: auto;
  flex: 1;
}

.task-chip {
  font-size: 11px;
  padding: 3px 6px;
  border-left: 3px solid var(--primary);
  background: var(--bg);
  border-radius: 4px;
  line-height: 1.3;
  overflow: hidden;
}

.task-title {
  display: block;
  word-break: break-word;
  overflow-wrap: break-word;
  line-height: 1.3;
}

.task-time {
  font-size: 10px;
  color: var(--text-secondary);
}

.no-tasks {
  font-size: 11px;
  color: var(--text-secondary);
  text-align: center;
  padding: 12px 0;
}

/* Monthly / Quarterly View */
.weekday-header {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  text-align: center;
  font-size: 12px;
  color: var(--text-secondary);
  padding: 8px 0;
  font-weight: 600;
}

.day-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 2px;
}

.day-cell {
  background: var(--card);
  border-radius: 6px;
  padding: 4px;
  min-height: 90px;
  box-shadow: 0 1px 2px rgba(0,0,0,0.04);
}

.day-cell.today {
  border: 2px solid var(--primary);
}

.day-cell.other-month {
  opacity: 0.35;
}

.day-cell-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 4px;
}

.day-num {
  font-size: 13px;
  font-weight: 600;
}

.free-badge-sm {
  font-size: 9px;
  padding: 0 4px;
  border-radius: 3px;
}

.free-badge-sm.充裕 { background: #d1fae5; color: #059669; }
.free-badge-sm.较多 { background: #dbeafe; color: #2563eb; }
.free-badge-sm.适中 { background: #fef3c7; color: #d97706; }
.free-badge-sm.较紧 { background: #fee2e2; color: #dc2626; }
.free-badge-sm.已满 { background: #fecaca; color: #b91c1c; }
.free-badge-sm.全天空闲 { background: #d1fae5; color: #059669; }

.day-cell-tasks {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.task-dot {
  font-size: 10px;
  padding: 1px 4px;
  border-radius: 3px;
  color: white;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.more-tasks {
  font-size: 10px;
  color: var(--text-secondary);
  text-align: center;
}

/* Legend */
.legend {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 16px;
  font-size: 12px;
  color: var(--text-secondary);
  align-items: center;
  background: var(--card);
  padding: 10px 16px;
  border-radius: var(--radius);
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
}

.dot {
  display: inline-block;
  width: 8px; height: 8px;
  border-radius: 50%;
  margin-right: 2px;
}

.sep { color: var(--border); }

.free-lbl { font-weight: 600; }
</style>
