<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '../api'
import type { Task } from '../types'
import { formatDate, renderContent } from '../utils/format'

const router = useRouter()
const tasks = ref<Task[]>([])
const categories = ref<string[]>([])
const selectedCategory = ref('')
const loading = ref(true)
const page = ref(1)
const PAGE_SIZE = 5

const totalPages = computed(() => Math.max(1, Math.ceil(tasks.value.length / PAGE_SIZE)))

const pagedTasks = computed(() => {
  const start = (page.value - 1) * PAGE_SIZE
  return tasks.value.slice(start, start + PAGE_SIZE)
})

function goToPage(p: number) {
  page.value = Math.max(1, Math.min(p, totalPages.value))
}

onMounted(async () => {
  const [allTasks, allCats] = await Promise.all([
    api.getAchievements(),
    api.getAchievementCategories()
  ])
  tasks.value = allTasks
  categories.value = allCats
  loading.value = false
})

async function filterByCategory(cat: string) {
  selectedCategory.value = cat
  page.value = 1
  loading.value = true
  tasks.value = await api.getAchievements(cat || undefined)
  loading.value = false
}

function truncate(text: string, len: number): string {
  if (text.length <= len) return text
  return text.slice(0, len) + '...'
}
</script>

<template>
  <div class="achievements">
    <div class="page-header">
      <h2>🏆 成果查看</h2>
    </div>

    <div class="filter-bar">
      <button :class="['filter-btn', { active: !selectedCategory }]" @click="filterByCategory('')">全部</button>
      <button
        v-for="cat in categories"
        :key="cat"
        :class="['filter-btn', { active: selectedCategory === cat }]"
        @click="filterByCategory(cat)"
      >{{ cat }}</button>
    </div>

    <div v-if="loading" class="loading">加载中...</div>

    <div v-else-if="tasks.length === 0" class="empty">
      <p>还没有成果记录，完成任务时填写成果吧</p>
    </div>

    <div v-else class="list">
      <div
        v-for="task in pagedTasks"
        :key="task.id"
        class="achievement-card"
        @click="router.push('/achievements/' + task.id)"
      >
        <div class="card-header">
          <span class="card-date">{{ formatDate(task.date) }}</span>
          <span v-if="task.category" class="category-tag">{{ task.category }}</span>
        </div>
        <h3 class="card-title">{{ task.title }}</h3>
        <p v-if="task.achievement" class="card-preview" v-html="renderContent(task.achievement)"></p>
        <div class="card-footer">
          <span v-if="task.start_time && task.end_time" class="time">{{ task.start_time }}-{{ task.end_time }}</span>
          <span v-if="task.actual_duration" class="duration">实际 {{ task.actual_duration }}分钟</span>
        </div>
      </div>
    </div>
    <div v-if="totalPages > 1" class="pagination">
      <button :disabled="page === 1" @click="goToPage(page - 1)">‹</button>
      <button v-for="p in totalPages" :key="p" :class="{ active: p === page }" @click="goToPage(p)">{{ p }}</button>
      <button :disabled="page === totalPages" @click="goToPage(page + 1)">›</button>
    </div>
  </div>
</template>

<style scoped>
.page-header {
  margin-bottom: 16px;
}

.page-header h2 {
  font-size: 20px;
}

.filter-bar {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  margin-bottom: 16px;
}

.filter-btn {
  padding: 6px 14px;
  border: 1px solid var(--border);
  border-radius: 20px;
  background: var(--card);
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s;
}

.filter-btn.active {
  background: var(--primary);
  color: white;
  border-color: var(--primary);
}

.loading {
  text-align: center;
  padding: 60px 20px;
  color: var(--text-secondary);
}

.empty {
  text-align: center;
  padding: 60px 20px;
  color: var(--text-secondary);
  font-size: 14px;
}

.list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.achievement-card {
  background: var(--card);
  border-radius: var(--radius);
  padding: 16px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
  cursor: pointer;
  transition: all 0.2s;
}

.achievement-card:hover {
  box-shadow: 0 2px 8px rgba(0,0,0,0.12);
}

.card-header {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 6px;
}

.card-date {
  font-size: 12px;
  color: var(--text-secondary);
}

.category-tag {
  font-size: 11px;
  background: var(--bg);
  padding: 2px 8px;
  border-radius: 10px;
  color: var(--text-secondary);
}

.card-title {
  font-size: 16px;
  font-weight: 600;
  margin-bottom: 6px;
}

.card-preview {
  font-size: 13px;
  color: var(--text-secondary);
  line-height: 1.5;
  margin-bottom: 8px;
  white-space: pre-wrap;
}

.card-footer {
  display: flex;
  gap: 12px;
  font-size: 12px;
  color: var(--text-secondary);
}

.pagination {
  display: flex;
  justify-content: center;
  gap: 6px;
  margin-top: 16px;
}

.pagination button {
  width: 36px;
  height: 36px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--card);
  color: var(--text);
  font-size: 14px;
  cursor: pointer;
  transition: all 0.15s;
}

.pagination button:hover:not(:disabled) {
  border-color: var(--primary);
  color: var(--primary);
}

.pagination button.active {
  background: var(--primary);
  color: white;
  border-color: var(--primary);
}

.pagination button:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}
</style>

