<script setup lang="ts">
import { ref, onMounted, inject, computed, watch } from 'vue'
import { api } from '../api'
import type { Task, DailyReview } from '../types'
import { formatDate } from '../utils/format'

const today = inject<string>('today')!
const selectedDate = ref(today)
const tasks = ref<Task[]>([])
const reviewContent = ref('')
const saved = ref(false)
const autoSummary = ref('')

const plannedTasks = computed(() => tasks.value.filter(t => t.is_planned))
const unplannedTasks = computed(() => tasks.value.filter(t => !t.is_planned))
const plannedCompleted = computed(() => plannedTasks.value.filter(t => t.status === 'completed').length)
const plannedTotal = computed(() => plannedTasks.value.length)
const plannedRate = computed(() => plannedTotal.value > 0 ? Math.round(plannedCompleted.value / plannedTotal.value * 100) : 0)
const plannedDuration = computed(() => plannedTasks.value.reduce((s, t) => s + t.planned_duration, 0))
const actualDuration = computed(() => tasks.value.reduce((s, t) => s + (t.actual_duration || 0), 0))
const unplannedCount = computed(() => unplannedTasks.value.length)
const durationDiff = computed(() => actualDuration.value - plannedDuration.value)

onMounted(() => loadData(selectedDate.value))
watch(selectedDate, (date) => loadData(date))

function shiftDay(delta: number) {
  const d = new Date(selectedDate.value + 'T00:00:00')
  d.setDate(d.getDate() + delta)
  selectedDate.value = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

async function loadData(date: string) {
  tasks.value = await api.getTasks(date)
  const r = await api.getReview(date)
  reviewContent.value = r ? r.content : ''
  generateSummary()
}

function generateSummary() {
  let s = `今日共计划 ${plannedTotal.value} 项计划内任务`
  s += `，已完成 ${plannedCompleted.value} 项`
  s += `，完成率 ${plannedRate.value}%。\n`

  if (unplannedCount.value > 0) {
    s += `计划外任务 ${unplannedCount.value} 项。\n`
  }

  s += `\n计划总时长: ${formatDuration(plannedDuration.value)}`
  s += `\n实际总时长: ${formatDuration(actualDuration.value)}`

  if (durationDiff.value > 0) {
    s += `\n实际比计划多出 ${formatDuration(durationDiff.value)}`
  } else if (durationDiff.value < 0) {
    s += `\n实际比计划少 ${formatDuration(Math.abs(durationDiff.value))}`
  } else {
    s += `\n实际时长与计划一致`
  }

  s += `\n\n任务详情:\n`
  for (const task of plannedTasks.value) {
    const icon = task.status === 'completed' ? '✅' : task.status === 'in_progress' ? '⏳' : task.status === 'cancelled' ? '❌' : '📋'
    const time = task.start_time || task.end_time ? ` (${task.start_time || '?'}-${task.end_time || '?'})` : ''
    let line = `  ${icon} ${task.title}${time}`
    if (task.actual_duration) line += ` 计划${formatDuration(task.planned_duration)} 实际${formatDuration(task.actual_duration)}`
    if (task.achievement) line += ` [有成果]`
    s += line + '\n'
  }
  for (const task of unplannedTasks.value) {
    const icon = task.status === 'completed' ? '✅' : task.status === 'in_progress' ? '⏳' : '📋'
    let line = `  ➕ ${icon} ${task.title}`
    if (task.actual_duration) line += ` 实际${formatDuration(task.actual_duration)}`
    if (task.achievement) line += ` [有成果]`
    s += line + '\n'
  }

  autoSummary.value = s
}

async function saveReview() {
  await api.saveReview(selectedDate.value, reviewContent.value)
  saved.value = true
  setTimeout(() => saved.value = false, 2000)
}

function statusLabel(s: string): string {
  const map: Record<string, string> = { planned: '计划中', in_progress: '进行中', completed: '已完成', cancelled: '已取消' }
  return map[s] || s
}

function renderContent(text: string): string {
  return text.replace(/!\[([^\]]*)\]\(([^)]+)\)/g, '<img src="$2" alt="$1" style="max-width:100%;border-radius:8px;margin:8px 0">')
    .replace(/\n/g, '<br>')
}

function formatDuration(min: number): string {
  if (!min) return '0分钟'
  if (min < 60) return `${min}分钟`
  return `${Math.floor(min / 60)}小时${min % 60 ? min % 60 + '分钟' : ''}`
}
</script>

<template>
  <div class="review">
    <div class="section-header">
      <div class="date-nav">
        <button class="btn-nav" @click="shiftDay(-1)">&lt;</button>
        <h2>{{ formatDate(selectedDate) }}</h2>
        <button class="btn-nav" @click="shiftDay(1)">&gt;</button>
      </div>
    </div>

    <div class="stats-grid">
      <div class="stat-card">
        <span class="stat-value">{{ plannedCompleted }}/{{ plannedTotal }}</span>
        <span class="stat-label">计划内完成</span>
      </div>
      <div class="stat-card">
        <span class="stat-value">{{ plannedRate }}%</span>
        <span class="stat-label">完成率</span>
      </div>
      <div class="stat-card">
        <span class="stat-value">{{ formatDuration(plannedDuration) }}</span>
        <span class="stat-label">计划总时长</span>
      </div>
      <div class="stat-card">
        <span class="stat-value">{{ formatDuration(actualDuration) }}</span>
        <span class="stat-label">实际总时长</span>
      </div>
      <div class="stat-card" v-if="unplannedCount > 0">
        <span class="stat-value">{{ unplannedCount }}</span>
        <span class="stat-label">计划外任务</span>
      </div>
      <div class="stat-card">
        <span :class="['stat-value', durationDiff > 0 ? 'text-warning' : 'text-success']">
          {{ durationDiff > 0 ? '+' : '' }}{{ formatDuration(durationDiff) }}
        </span>
        <span class="stat-label">差异</span>
      </div>
    </div>

    <div class="auto-summary">
      <h3>📊 自动总结</h3>
      <pre class="summary-content">{{ autoSummary }}</pre>
    </div>

    <div class="task-list">
      <h3>{{ selectedDate }} 任务</h3>
      <div v-for="task in tasks" :key="task.id" :class="['task-item', task.status]">
        <div class="task-info">
          <span :class="['task-name', { done: task.status === 'completed' }]">{{ task.title }}</span>
          <span class="task-status-badge" :class="task.status">{{ statusLabel(task.status) }}</span>
          <span v-if="!task.is_planned" class="badge-unplanned">计划外</span>
        </div>
        <div class="task-duration">
          <span v-if="task.is_planned">计划: {{ formatDuration(task.planned_duration) }}</span>
          <span v-if="task.actual_duration">实际: {{ formatDuration(task.actual_duration) }}</span>
          <span v-else class="text-muted">未记录实际时长</span>
        </div>
        <div v-if="task.achievement" class="task-achievement">
          <span class="achievement-label">📝 成果:</span>
          <span class="achievement-text" v-html="renderContent(task.achievement.slice(0, 200))"></span>
        </div>
      </div>
    </div>

    <div class="review-section">
      <h3>📝 你的复盘</h3>
      <textarea
        v-model="reviewContent"
        placeholder="今天做得怎么样？有哪些可以改进的地方？&#10;计划与实际的差距在哪里？&#10;明天如何优化？"
        rows="6"
      ></textarea>
      <button class="btn btn-primary" @click="saveReview">
        {{ saved ? '已保存 ✓' : '保存复盘' }}
      </button>
    </div>
  </div>
</template>

<style scoped>
.section-header {
  margin-bottom: 16px;
}

.date-nav {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
}

.date-nav h2 {
  font-size: 16px;
  color: var(--text-secondary);
  font-weight: 500;
  min-width: 180px;
  text-align: center;
}

.btn-nav {
  width: 32px;
  height: 32px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--card);
  color: var(--text);
  font-size: 16px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  line-height: 1;
}

.btn-nav:hover {
  background: var(--primary);
  color: white;
  border-color: var(--primary);
}

.stats-grid {
  display: grid;
  grid-template-columns: 1fr 1fr 1fr;
  gap: 8px;
  margin-bottom: 16px;
}

.stat-card {
  background: var(--card);
  border-radius: var(--radius);
  padding: 12px 8px;
  text-align: center;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
}

.stat-value {
  display: block;
  font-size: 18px;
  font-weight: 700;
  color: var(--primary);
}

.stat-label {
  display: block;
  font-size: 11px;
  color: var(--text-secondary);
  margin-top: 4px;
}

.text-warning { color: var(--warning); }
.text-success { color: var(--success); }

.auto-summary {
  background: var(--card);
  border-radius: var(--radius);
  padding: 16px;
  margin-bottom: 16px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
}

.auto-summary h3 {
  font-size: 15px;
  margin-bottom: 8px;
}

.summary-content {
  font-size: 13px;
  line-height: 1.7;
  white-space: pre-wrap;
  font-family: inherit;
  color: var(--text);
}

.task-list {
  background: var(--card);
  border-radius: var(--radius);
  padding: 16px;
  margin-bottom: 16px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
}

.task-list h3 {
  font-size: 15px;
  margin-bottom: 12px;
}

.task-item {
  padding: 8px 0;
  border-bottom: 1px solid var(--border);
}

.task-item:last-child {
  border-bottom: none;
}

.task-info {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 4px;
}

.task-name {
  font-size: 14px;
  flex: 1;
}

.task-name.done {
  text-decoration: line-through;
  color: var(--text-secondary);
}

.task-status-badge {
  font-size: 11px;
  padding: 2px 6px;
  border-radius: 4px;
}

.task-duration {
  font-size: 12px;
  color: var(--text-secondary);
  display: flex;
  gap: 8px;
}

.task-achievement {
  font-size: 12px;
  color: var(--text-secondary);
  margin-top: 4px;
  display: flex;
  gap: 4px;
}

.achievement-label {
  color: var(--warning);
  flex-shrink: 0;
}

.achievement-text {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.badge-unplanned {
  font-size: 10px;
  background: #fef3c7;
  color: #d97706;
  padding: 1px 6px;
  border-radius: 4px;
}

.review-section {
  background: var(--card);
  border-radius: var(--radius);
  padding: 16px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
}

.review-section h3 {
  font-size: 15px;
  margin-bottom: 12px;
}

.review-section textarea {
  width: 100%;
  padding: 12px;
  border: 1px solid var(--border);
  border-radius: 8px;
  font-size: 14px;
  line-height: 1.6;
  resize: vertical;
  outline: none;
  font-family: inherit;
}

.review-section textarea:focus {
  border-color: var(--primary);
}

.review-section .btn {
  margin-top: 12px;
  width: 100%;
}
</style>

