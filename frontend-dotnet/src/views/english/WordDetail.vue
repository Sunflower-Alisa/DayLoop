<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { englishApi } from '../../api/english'
import type { Word } from '../../types/english'
import AudioButton from '../../components/english/AudioButton.vue'

const router = useRouter()
const route = useRoute()
const word = ref<Word | null>(null)
const loading = ref(true)

onMounted(async () => {
  try {
    word.value = await englishApi.getWord(Number(route.params.id))
  } catch (e) {}
  loading.value = false
})
</script>

<template>
  <div class="e-page">
    <button class="e-btn" style="align-self: flex-start" @click="router.back()">← 返回</button>

    <div v-if="loading" class="e-loading">加载中...</div>

    <template v-else-if="word">
      <div class="e-word-card">
        <div class="e-word-image" style="background: var(--primary)">
          <img v-if="word.image_url" :src="word.image_url" alt="" />
          <span v-else class="fallback-letter">{{ word.word[0].toUpperCase() }}</span>
        </div>
        <div class="e-word-body">
          <div class="head">
            <div class="e-word-main">
              <span class="word">{{ word.word }}</span>
              <AudioButton :text="word.word" />
            </div>
            <span v-if="word.status === 'mastered'" class="e-chip e-chip-green">已掌握</span>
            <span v-else-if="word.status === 'reviewing'" class="e-chip e-chip-amber">复习中</span>
          </div>
          <div class="meta">
            <span v-if="word.phonetic" class="phonetic">{{ word.phonetic }}</span>
            <span v-if="word.pos" class="pos">{{ word.pos }}</span>
          </div>
          <div class="meaning">{{ word.meaning }}</div>
          <div v-if="word.in_wrong_book" class="e-chip e-chip-red" style="margin-top: 10px">📕 在错词本中</div>
        </div>
      </div>

      <div class="e-card">
        <div class="section-title">例句</div>
        <div class="e-example">
          <div class="en">{{ word.example_en || '暂无例句' }}</div>
          <div class="cn">{{ word.example_cn }}</div>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.head { display: flex; align-items: flex-start; justify-content: space-between; gap: 8px; }
.head .e-word-main { flex: 1; }
.meta { display: flex; gap: 8px; margin-top: 4px; }
.phonetic { font-size: 14px; color: var(--text-secondary); }
.meaning { font-size: 17px; font-weight: 600; margin-top: 6px; }
.section-title { font-size: 14px; font-weight: 700; color: var(--text-secondary); }
</style>