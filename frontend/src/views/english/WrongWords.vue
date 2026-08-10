<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { englishApi } from '../../api/english'
import type { Word } from '../../types/english'
import WordCard from '../../components/english/WordCard.vue'

const router = useRouter()
const words = ref<Word[]>([])
const loading = ref(true)

async function load() {
  loading.value = true
  try {
    words.value = await englishApi.getWrongWords()
  } catch (e) {}
  loading.value = false
}

async function remove(w: Word) {
  try {
    await englishApi.removeWrongWord(w.id)
    words.value = words.value.filter(x => x.id !== w.id)
  } catch (e) {}
}

function goDetail(w: Word) {
  router.push({ name: 'english-word-detail', params: { id: w.id } })
}

onMounted(load)
</script>

<template>
  <div class="e-page">
    <div class="e-header">
      <h2 class="e-title">📕 错词本</h2>
      <span class="e-subtitle">{{ words.length }} 个错词</span>
    </div>

    <div v-if="loading" class="e-loading">加载中...</div>
    <div v-else-if="!words.length" class="e-empty">太棒了，没有错词 🎉</div>

    <div v-else class="e-grid">
      <div v-for="w in words" :key="w.id" class="card-wrap">
        <WordCard :word="w" @click="goDetail(w)" />
        <button class="remove-btn" @click="remove(w)">✕ 移除</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.card-wrap { position: relative; }
.remove-btn {
  width: 100%;
  margin-top: 6px;
  padding: 6px;
  border: none;
  border-radius: 8px;
  background: var(--bg);
  color: var(--text-secondary);
  font-size: 12px;
  cursor: pointer;
}
.remove-btn:hover { color: var(--danger); }
</style>