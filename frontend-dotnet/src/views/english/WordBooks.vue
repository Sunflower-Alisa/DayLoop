<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { englishApi } from '../../api/english'
import type { WordBook } from '../../types/english'

const router = useRouter()
const books = ref<WordBook[]>([])
const loading = ref(true)

async function load() {
  loading.value = true
  try {
    books.value = await englishApi.getBooks()
  } catch (e) {}
  loading.value = false
}

async function setGoal(book: WordBook) {
  const input = window.prompt('设置每日新词目标（个）', String(book.daily_goal || 10))
  if (input === null) return
  const n = parseInt(input, 10)
  if (isNaN(n) || n < 1) return
  try {
    book.daily_goal = (await englishApi.setGoal(book.id, n)).daily_goal
  } catch (e) {}
}

async function createBook() {
  const name = window.prompt('词书名称')
  if (!name) return
  try {
    await englishApi.createBook({ name })
    await load()
  } catch (e) {}
}

function goWords(bookId: number) {
  router.push({ name: 'english-wordbooks-words', params: { id: bookId } })
}

onMounted(load)
</script>

<template>
  <div class="e-page">
    <div class="e-header">
      <h2 class="e-title">📚 词书</h2>
      <button class="e-btn" @click="createBook">＋ 新建</button>
    </div>

    <div v-if="loading" class="e-loading">加载中...</div>
    <div v-else-if="!books.length" class="e-empty">还没有词书，点击右上角新建</div>

    <div v-for="b in books" :key="b.id" class="book-card" @click="goWords(b.id)">
      <div class="book-cover" :style="{ background: b.cover_color || 'var(--primary)' }">
        <span>{{ b.name[0] }}</span>
      </div>
      <div class="book-body">
        <div class="book-title-row">
          <span class="book-name">{{ b.name }}</span>
          <span v-if="b.is_default" class="e-chip e-chip-green">默认</span>
        </div>
        <div class="book-desc">{{ b.level || '通用' }} · {{ b.description || '自定义词书' }}</div>
        <div class="e-progress" style="margin-top: 8px">
          <div
            class="e-progress-fill"
            :style="{ width: b.word_count ? Math.min(100, Math.round((b.learned_count / b.word_count) * 100)) + '%' : '0%' }"
          ></div>
        </div>
        <div class="book-meta">
          <span>已学 {{ b.learned_count }}/{{ b.word_count }}</span>
          <span class="goal" @click.stop="setGoal(b)">每日 {{ b.daily_goal }} 词 ✎</span>
        </div>
      </div>
      <span class="arrow">→</span>
    </div>
  </div>
</template>

<style scoped>
.book-card {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 14px;
  background: var(--card);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  cursor: pointer;
  transition: all 0.2s;
  text-align: left;
  width: 100%;
}
.book-card:hover { border-color: var(--primary); box-shadow: 0 2px 8px rgba(0,0,0,0.06); }
.book-cover {
  width: 56px;
  height: 68px;
  border-radius: 10px;
  color: #fff;
  font-size: 24px;
  font-weight: 800;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}
.book-body { flex: 1; min-width: 0; }
.book-title-row { display: flex; align-items: center; gap: 8px; }
.book-name { font-size: 16px; font-weight: 700; }
.book-desc { font-size: 12px; color: var(--text-secondary); margin-top: 2px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.book-meta { display: flex; align-items: center; justify-content: space-between; font-size: 12px; color: var(--text-secondary); margin-top: 6px; }
.book-meta .goal { color: var(--primary); font-weight: 600; }
.arrow { color: var(--text-secondary); font-size: 18px; }
</style>