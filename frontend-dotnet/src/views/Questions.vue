<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '../api'
import type { Question } from '../types'
import { formatDate } from '../utils/format'

const router = useRouter()
const questions = ref<Question[]>([])
const categories = ref<string[]>([])
const selectedCategory = ref('')
const searchQuery = ref('')
const searchTimer = ref<ReturnType<typeof setTimeout> | null>(null)

const filteredQuestions = computed(() => {
  if (!selectedCategory.value && !searchQuery.value) return questions.value
  return questions.value.filter(q => {
    if (selectedCategory.value && q.category !== selectedCategory.value) return false
    if (searchQuery.value) {
      const s = searchQuery.value.toLowerCase()
      if (!q.title.toLowerCase().includes(s) && !(q.content || '').toLowerCase().includes(s)) return false
    }
    return true
  })
})

const page = ref(1)
const PAGE_SIZE = 5

const totalPages = computed(() => Math.max(1, Math.ceil(filteredQuestions.value.length / PAGE_SIZE)))

const pagedQuestions = computed(() => {
  const start = (page.value - 1) * PAGE_SIZE
  return filteredQuestions.value.slice(start, start + PAGE_SIZE)
})

function goToPage(p: number) {
  page.value = Math.max(1, Math.min(p, totalPages.value))
}

onMounted(load)

async function load() {
  const [all, cats] = await Promise.all([api.getQuestions(), api.getQuestionCategories()])
  questions.value = all
  categories.value = cats
}

async function filterByCategory(cat: string) {
  selectedCategory.value = cat
  page.value = 1
  questions.value = await api.getQuestions(cat || undefined, searchQuery.value || undefined)
}

function onSearchInput() {
  if (searchTimer.value) clearTimeout(searchTimer.value)
  searchTimer.value = setTimeout(async () => {
    page.value = 1
    questions.value = await api.getQuestions(selectedCategory.value || undefined, searchQuery.value || undefined)
  }, 300)
}

function statusLabel(s: string): string {
  const map: Record<string, string> = { planned: '计划中', in_progress: '进行中', completed: '已完成', cancelled: '已取消' }
  return map[s] || s
}

function answerSourceLabel(s: string): string {
  const map: Record<string, string> = { self: '自答', ai: 'AI', web: '网络' }
  return map[s] || s
}

async function deleteQuestion(id: number) {
  if (!confirm('确定删除此问题？')) return
  await api.deleteQuestion(id)
  await load()
}
</script>

<template>
  <div class="questions-page">
    <div class="page-header">
      <h2>❓ 问题库</h2>
      <div class="header-actions">
        <button class="btn btn-outline" @click="router.push('/questions/categories')">分类管理</button>
        <button class="btn btn-primary" @click="router.push('/questions/new')">+ 新建</button>
      </div>
    </div>

    <div class="search-bar">
      <input v-model="searchQuery" placeholder="搜索问题..." class="search-input" @input="onSearchInput" />
    </div>

    <div class="filter-bar">
      <button :class="['filter-btn', { active: !selectedCategory }]" @click="filterByCategory('')">全部</button>
      <button v-for="cat in categories" :key="cat" :class="['filter-btn', { active: selectedCategory === cat }]" @click="filterByCategory(cat)">{{ cat }}</button>
    </div>

    <div v-if="questions.length === 0" class="empty">
      <p>还没有问题，新建一个吧</p>
    </div>

    <div v-else class="question-list">
      <div v-for="q in pagedQuestions" :key="q.id" class="question-card" @click="router.push('/questions/' + q.id)">
        <div class="question-header">
          <h3 class="question-title">{{ q.title }}</h3>
          <span v-if="q.category" class="category-tag">{{ q.category }}</span>
        </div>
        <p v-if="q.content" class="question-preview">{{ q.content.replace(/<[^>]*>/g,'').replace(/!\[([^\]]*)\]\(([^)]+)\)/g, '').slice(0, 80) }}{{ q.content.length > 80 ? '...' : '' }}</p>
        <div v-if="q.answer" class="answer-badge">
          <span class="answer-source" :class="'source-' + q.answer_source">{{ answerSourceLabel(q.answer_source) }}</span>
          <span class="answer-preview">{{ q.answer.slice(0, 60) }}{{ q.answer.length > 60 ? '...' : '' }}</span>
        </div>
        <div v-if="q.tags" class="question-tags">
          <span v-for="tag in (q.tags || '').split(',').filter(Boolean)" :key="tag" class="tag">#{{ tag.trim() }}</span>
        </div>
        <div class="question-footer">
          <span class="question-date">{{ formatDate(q.created_at?.slice(0, 10)) }}</span>
          <div v-if="q.linked_tasks?.length" class="linked-tasks">
            <span v-for="task in q.linked_tasks" :key="task.id" class="linked-task" :class="task.status">
              🔗 {{ task.title }} ({{ statusLabel(task.status) }})
            </span>
          </div>
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
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.page-header h2 { font-size: 20px; }

.header-actions {
  display: flex;
  gap: 8px;
}

.search-bar {
  margin-bottom: 12px;
}

.search-input {
  width: 100%;
  padding: 10px 14px;
  border: 1px solid var(--border);
  border-radius: 24px;
  font-size: 14px;
  outline: none;
  background: var(--card);
  transition: border-color 0.2s;
}

.search-input:focus {
  border-color: var(--primary);
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

.empty {
  text-align: center;
  padding: 60px 20px;
  color: var(--text-secondary);
  font-size: 14px;
}

.question-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.question-card {
  background: var(--card);
  border-radius: var(--radius);
  padding: 14px 16px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
  cursor: pointer;
  transition: all 0.2s;
}

.question-card:hover {
  box-shadow: 0 2px 8px rgba(0,0,0,0.12);
}

.question-header {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 6px;
}

.question-title {
  flex: 1;
  font-size: 15px;
  font-weight: 600;
}

.category-tag {
  font-size: 11px;
  background: var(--bg);
  padding: 2px 8px;
  border-radius: 10px;
  color: var(--text-secondary);
}

.question-preview {
  font-size: 13px;
  color: var(--text-secondary);
  line-height: 1.5;
  margin-bottom: 8px;
}

.answer-badge {
  display: flex;
  gap: 6px;
  align-items: center;
  margin-bottom: 6px;
  font-size: 13px;
}

.answer-source {
  font-size: 11px;
  padding: 2px 6px;
  border-radius: 4px;
  font-weight: 500;
}

.source-self { background: #dbeafe; color: #1e40af; }
.source-ai { background: #fce7f3; color: #9d174d; }
.source-web { background: #d1fae5; color: #065f46; }

.answer-preview {
  color: var(--text-secondary);
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.question-tags {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
  margin-bottom: 6px;
}

.tag {
  font-size: 11px;
  color: var(--primary);
  background: #eef2ff;
  padding: 2px 8px;
  border-radius: 10px;
}

.question-footer {
  display: flex;
  gap: 12px;
  font-size: 12px;
  color: var(--text-secondary);
  flex-wrap: wrap;
}

.linked-tasks {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.linked-task.completed { color: var(--success); }
.linked-task.in_progress { color: var(--warning); }

.btn {
  padding: 10px 20px;
  border: 1px solid var(--border);
  border-radius: 8px;
  font-size: 14px;
  cursor: pointer;
  background: var(--card);
  color: var(--text);
  transition: all 0.2s;
}

.btn-primary {
  background: var(--primary);
  color: white;
  border-color: var(--primary);
}

.btn-outline {
  background: transparent;
  color: var(--primary);
  border-color: var(--primary);
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
