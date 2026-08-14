<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api } from '../api'
import type { Task } from '../types'
import { formatDate, renderContent } from '../utils/format'

const route = useRoute()
const router = useRouter()
const task = ref<Task | null>(null)
const loading = ref(true)

onMounted(async () => {
  const id = Number(route.params.id)
  try {
    task.value = await api.getTask(id)
  } catch (e) {
    task.value = null
  }
  loading.value = false
})

function formatDuration(min: number): string {
  if (!min) return ''
  if (min < 60) return `${min}分钟`
  return `${Math.floor(min / 60)}小时${min % 60 ? min % 60 + '分钟' : ''}`
}
</script>

<template>
  <div class="detail">
    <button class="back-btn" @click="router.push('/achievements')">← 返回</button>

    <div v-if="loading" class="loading">加载中...</div>

    <div v-else-if="!task" class="empty">
      <p>未找到该成果</p>
    </div>

    <div v-else class="content">
      <div class="meta-row">
        <span class="date">{{ formatDate(task.date) }}</span>
        <span v-if="task.category" class="category-tag">{{ task.category }}</span>
      </div>
      <h2 class="title">{{ task.title }}</h2>
      <div class="info-row">
        <span v-if="task.start_time && task.end_time" class="info-item">⏱ {{ task.start_time }}-{{ task.end_time }}</span>
        <span v-if="task.actual_duration" class="info-item">实际: {{ formatDuration(task.actual_duration) }}</span>
        <span v-if="task.planned_duration" class="info-item">计划: {{ formatDuration(task.planned_duration) }}</span>
      </div>

      <div class="section">
        <h3>📝 成果记录</h3>
        <div class="achievement-content" v-html="renderContent(task.achievement)"></div>
      </div>

      <div v-if="task.note" class="section">
        <h3>📌 备注</h3>
        <p class="note">{{ task.note }}</p>
      </div>
    </div>
  </div>
</template>

<style scoped>
.detail {
  padding-bottom: 20px;
}

.back-btn {
  padding: 8px 16px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--card);
  font-size: 14px;
  cursor: pointer;
  margin-bottom: 16px;
}

.loading, .empty {
  text-align: center;
  padding: 60px 20px;
  color: var(--text-secondary);
}

.content {
  background: var(--card);
  border-radius: var(--radius);
  padding: 20px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
}

.meta-row {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 8px;
}

.date {
  font-size: 13px;
  color: var(--text-secondary);
}

.category-tag {
  font-size: 11px;
  background: var(--bg);
  padding: 2px 8px;
  border-radius: 10px;
  color: var(--text-secondary);
}

.title {
  font-size: 22px;
  font-weight: 700;
  margin-bottom: 12px;
}

.info-row {
  display: flex;
  gap: 16px;
  font-size: 13px;
  color: var(--text-secondary);
  margin-bottom: 20px;
  flex-wrap: wrap;
}

.section {
  margin-bottom: 20px;
}

.section h3 {
  font-size: 15px;
  margin-bottom: 8px;
  color: var(--text);
}

.achievement-content {
  font-size: 14px;
  line-height: 1.8;
  white-space: pre-wrap;
  color: var(--text);
}

.note {
  font-size: 14px;
  color: var(--text-secondary);
  line-height: 1.6;
}
</style>

