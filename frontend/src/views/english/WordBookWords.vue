<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { englishApi } from '../../api/english'
import type { Word } from '../../types/english'
import WordCard from '../../components/english/WordCard.vue'

const router = useRouter()
const route = useRoute()
const bookId = Number(route.params.id)
const words = ref<Word[]>([])
const loading = ref(true)
const bookName = ref('')

async function load() {
  loading.value = true
  try {
    const data = await englishApi.getBookWords(bookId)
    words.value = data.words
    const books = await englishApi.getBooks()
    const found = books.find(b => b.id === bookId)
    if (found) bookName.value = found.name
  } catch (e) {}
  loading.value = false
}

function goDetail(w: Word) {
  router.push({ name: 'english-word-detail', params: { id: w.id } })
}

onMounted(load)
</script>

<template>
  <div class="e-page">
    <button class="e-btn" style="align-self: flex-start" @click="router.back()">← 返回</button>
    <div class="e-header">
      <h2 class="e-title">{{ bookName }}</h2>
      <span class="e-subtitle">{{ words.length }} 个词</span>
    </div>

    <div v-if="loading" class="e-loading">加载中...</div>
    <div v-else-if="!words.length" class="e-empty">该词书暂无单词</div>

    <div v-else class="e-grid">
      <WordCard v-for="w in words" :key="w.id" :word="w" @click="goDetail(w)" />
    </div>
  </div>
</template>