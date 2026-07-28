<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { api } from '../api'
import type { Task, DailyReview } from '../types'
import { formatDate } from '../utils/format'

const dates = ref<string[]>([])
const selectedDate = ref('')
const tasks = ref<Task[]>([])
const review = ref<DailyReview | null>(null)

onMounted(async () => {
  const allTasks = await api.getTasks()
  const dateSet = new Set(allTasks.map(t => t.date))
  dates.value = Array.from(dateSet).sort().reverse()
  if (dates.value.length > 0) {
    selectedDate.value = dates.value[0]
    await loadDateData()
  }
})

async function loadDateData() {
  if (!selectedDate.value) return
  tasks.value = await api.getTasks(selectedDate.value)
  const r = await api.getReview(selectedDate.value)
  review.value = r
}

function formatDuration(min: number): string {
  if (!min) return ''
  if (min < 60) return `${min}分钟`
  return `${Math.floor(min / 60)}小时${min % 60 ? min % 60 + '分钟' : ''}`
}

function renderContent(text: string): string {
  return text.replace(/!\[([^\]]*)\]\(([^)]+)\)/g, '<img src="$2" alt="$1" style="max-width:100%;border-radius:8px;margin:8px 0">')
    .replace(/\n/g, '<br>')
}

function statusLabel(s: string): string {
  const map: Record<string, string> = { planned: '计划中', in_progress: '进行中', completed: '已完成', cancelled: '已取消' }
  return map[s] || s
}
</script>

<template>
  <div class="history">
    <div class="date-selector">
      <select v-model="selectedDate" @change="loadDateData">
        <option v-for="d in dates" :key="d" :value="d">{{ formatDate(d) }}</option>
      </select>
    </div>

    <div v-if="tasks.length === 0" class="empty">
      <p>该日期没有任务记录</p>
    </div>

    <div v-else>
      <div class="stats-row">
        <span>任务数: {{ tasks.length }}</span>
        <span>计划内: {{ tasks.filter(t => t.is_planned).length }}</span>
        <span>计划外: {{ tasks.filter(t => !t.is_planned).length }}</span>
        <span>完成: {{ tasks.filter(t => t.status === 'completed').length }}</span>
      </div>

      <div class="task-list">
        <div v-for="task in tasks" :key="task.id" :class="['task-item', task.status]">
          <div class="task-info">
            <span class="time-badge" v-if="task.start_time || task.end_time">{{ task.start_time || '?' }}-{{ task.end_time || '?' }}</span>
            <span :class="['task-name', { done: task.status === 'completed' }]">{{ task.title }}</span>
            <span class="status-badge" :class="task.status">{{ statusLabel(task.status) }}</span>
            <span v-if="!task.is_planned" class="badge-up">计划外</span>
            <span v-if="task.is_recurring" class="badge-recurring">🔄</span>
          </div>
          <div class="task-duration">
            <span v-if="task.is_planned">计划: {{ formatDuration(task.planned_duration) }}</span>
            <span v-if="task.actual_duration">实际: {{ formatDuration(task.actual_duration) }}</span>
          </div>
          <div v-if="task.achievement" class="task-achievement">
            <span v-html="renderContent(task.achievement.slice(0, 200))"></span>
          </div>
        </div>
      </div>

      <div v-if="review" class="review-section">
        <h3>复盘记录</h3>
        <p class="review-content">{{ review.content }}</p>
      </div>
    </div>
  </div>
</template>

<style scoped>
.date-selector {
  margin-bottom: 16px;
}

.date-selector select {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: 8px;
  font-size: 15px;
  outline: none;
  background: var(--card);
}

.stats-row {
  display: flex;
  gap: 12px;
  font-size: 12px;
  color: var(--text-secondary);
  margin-bottom: 12px;
  flex-wrap: wrap;
}

.task-list {
  background: var(--card);
  border-radius: var(--radius);
  padding: 16px;
  margin-bottom: 16px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
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
  flex-wrap: wrap;
}

.time-badge {
  font-size: 11px;
  background: var(--bg);
  padding: 1px 6px;
  border-radius: 4px;
  color: var(--text-secondary);
  font-family: monospace;
}

.task-name {
  font-size: 14px;
  flex: 1;
}

.task-name.done {
  text-decoration: line-through;
  color: var(--text-secondary);
}

.task-duration {
  font-size: 12px;
  color: var(--text-secondary);
  display: flex;
  gap: 8px;
}

.task-achievement {
  font-size: 12px;
  color: var(--warning);
  margin-top: 4px;
}

.badge-up {
  font-size: 10px;
  background: #fef3c7;
  color: #d97706;
  padding: 1px 6px;
  border-radius: 4px;
}

.badge-recurring {
  font-size: 12px;
}

.review-section {
  background: var(--card);
  border-radius: var(--radius);
  padding: 16px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
}

.review-section h3 {
  font-size: 15px;
  margin-bottom: 8px;
}

.review-content {
  font-size: 14px;
  line-height: 1.6;
  color: var(--text);
  white-space: pre-wrap;
}

.empty {
  text-align: center;
  padding: 60px 20px;
  color: var(--text-secondary);
  font-size: 14px;
}
</style>

