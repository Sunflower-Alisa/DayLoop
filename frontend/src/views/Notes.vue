<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '../api'
import type { Note } from '../types'
import { formatDate } from '../utils/format'

const router = useRouter()
const notes = ref<Note[]>([])
const categories = ref<string[]>([])
const selectedCategory = ref('')
const searchQuery = ref('')
const searchTimer = ref<ReturnType<typeof setTimeout> | null>(null)

const filteredNotes = computed(() => {
  if (!selectedCategory.value && !searchQuery.value) return notes.value
  return notes.value.filter(n => {
    if (selectedCategory.value && n.category !== selectedCategory.value) return false
    if (searchQuery.value) {
      const q = searchQuery.value.toLowerCase()
      if (!n.title.toLowerCase().includes(q) && !(n.content || '').toLowerCase().includes(q)) return false
    }
    return true
  })
})

const page = ref(1)
const PAGE_SIZE = 5

const totalPages = computed(() => Math.max(1, Math.ceil(filteredNotes.value.length / PAGE_SIZE)))

const pagedNotes = computed(() => {
  const start = (page.value - 1) * PAGE_SIZE
  return filteredNotes.value.slice(start, start + PAGE_SIZE)
})

function goToPage(p: number) {
  page.value = Math.max(1, Math.min(p, totalPages.value))
}

onMounted(load)

async function load() {
  const [allNotes, cats] = await Promise.all([api.getNotes(), api.getNoteCategories()])
  notes.value = allNotes
  categories.value = cats
}

async function filterByCategory(cat: string) {
  selectedCategory.value = cat
  page.value = 1
  notes.value = await api.getNotes(cat || undefined, searchQuery.value || undefined)
}

function onSearchInput() {
  if (searchTimer.value) clearTimeout(searchTimer.value)
  searchTimer.value = setTimeout(async () => {
    page.value = 1
    notes.value = await api.getNotes(selectedCategory.value || undefined, searchQuery.value || undefined)
  }, 300)
}

function statusLabel(s: string): string {
  const map: Record<string, string> = { planned: '计划中', in_progress: '进行中', completed: '已完成', cancelled: '已取消' }
  return map[s] || s
}

async function deleteNote(id: number) {
  if (!confirm('确定删除此备忘录？')) return
  await api.deleteNote(id)
  await load()
}

function extractFirstImage(content: string): string | null {
  const match = content.match(/!\[([^\]]*)\]\(([^)]+)\)/)
  return match ? match[2] : null
}
</script>

<template>
  <div class="notes-page">
    <div class="page-header">
      <h2>📝 备忘录</h2>
      <div class="header-actions">
        <button class="btn btn-outline" @click="router.push('/notes/categories')">分类管理</button>
        <button class="btn btn-primary" @click="router.push('/notes/new')">+ 新建</button>
      </div>
    </div>

    <div class="search-bar">
      <input v-model="searchQuery" placeholder="搜索备忘录..." class="search-input" @input="onSearchInput" />
    </div>

    <div class="filter-bar">
      <button :class="['filter-btn', { active: !selectedCategory }]" @click="filterByCategory('')">全部</button>
      <button v-for="cat in categories" :key="cat" :class="['filter-btn', { active: selectedCategory === cat }]" @click="filterByCategory(cat)">{{ cat }}</button>
    </div>

    <div v-if="notes.length === 0" class="empty">
      <p>还没有备忘录，新建一个吧</p>
    </div>

    <div v-else class="note-list">
      <div v-for="note in pagedNotes" :key="note.id" class="note-card" @click="router.push('/notes/' + note.id)">
        <div class="note-header">
          <h3 class="note-title">{{ note.title }}</h3>
          <span v-if="note.category" class="category-tag">{{ note.category }}</span>
        </div>
        <p v-if="note.content" class="note-preview">{{ note.content.replace(/<[^>]*>/g,'').replace(/!\[([^\]]*)\]\(([^)]+)\)/g, '').slice(0, 80) }}{{ note.content.length > 80 ? '...' : '' }}</p>
        <div v-if="note.content && note.content.match(/!\[([^\]]*)\]\(([^)]+)\)/)" class="note-thumbnails">
          <img v-for="(img, i) in note.content.match(/!\[([^\]]*)\]\(([^)]+)\)/g)?.slice(0, 3)" :key="i" :src="img.replace(/!\[([^\]]*)\]\(([^)]+)\)/, '$2')" class="thumb-img" />
          <span v-if="(note.content.match(/!\[([^\]]*)\]\(([^)]+)\)/g)?.length || 0) > 3" class="more-imgs">+{{ (note.content.match(/!\[([^\]]*)\]\(([^)]+)\)/g)?.length || 0) - 3 }}</span>
        </div>
        <div v-if="note.tags" class="note-tags">
          <span v-for="tag in (note.tags || '').split(',').filter(Boolean)" :key="tag" class="tag">#{{ tag.trim() }}</span>
        </div>
        <div class="note-footer">
          <span class="note-date">{{ formatDate(note.created_at?.slice(0, 10)) }}</span>
          <div v-if="note.linked_tasks?.length" class="linked-tasks">
            <span v-for="task in note.linked_tasks" :key="task.id" class="linked-task" :class="task.status">
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

.note-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.note-card {
  background: var(--card);
  border-radius: var(--radius);
  padding: 14px 16px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
  cursor: pointer;
  transition: all 0.2s;
}

.note-card:hover {
  box-shadow: 0 2px 8px rgba(0,0,0,0.12);
}

.note-header {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 6px;
}

.note-title {
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

.note-preview {
  font-size: 13px;
  color: var(--text-secondary);
  line-height: 1.5;
  margin-bottom: 8px;
}

.note-thumbnails {
  display: flex;
  gap: 6px;
  margin-bottom: 8px;
  flex-wrap: wrap;
}

.thumb-img {
  width: 60px;
  height: 60px;
  object-fit: cover;
  border-radius: 6px;
  border: 1px solid var(--border);
}

.more-imgs {
  width: 60px;
  height: 60px;
  border-radius: 6px;
  border: 1px solid var(--border);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  color: var(--text-secondary);
  background: var(--bg);
}

.note-tags {
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

.note-footer {
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
