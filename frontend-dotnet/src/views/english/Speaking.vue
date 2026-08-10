<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { englishApi } from '../../api/english'
import type { SpeakingTopic } from '../../types/english'

const router = useRouter()
const topics = ref<SpeakingTopic[]>([])
const loading = ref(true)
const activeCategory = ref('全部')

const categories = computed(() => ['全部', ...new Set(topics.value.map(t => t.category || '其他'))])
const filtered = computed(() =>
  activeCategory.value === '全部' ? topics.value : topics.value.filter(t => (t.category || '其他') === activeCategory.value)
)

function goPractice(t: SpeakingTopic) {
  router.push({ name: 'english-speaking-detail', params: { id: t.id } })
}

async function load() {
  loading.value = true
  try {
    topics.value = await englishApi.getSpeakingTopics()
  } catch (e) {}
  loading.value = false
}

onMounted(load)
</script>

<template>
  <div class="e-page">
    <div class="e-header">
      <h2 class="e-title">🎙️ 口语跟读</h2>
      <span class="e-subtitle">AI 语音评测</span>
    </div>

    <div class="e-tabs">
      <button
        v-for="c in categories"
        :key="c"
        class="e-tab"
        :class="{ active: activeCategory === c }"
        @click="activeCategory = c"
      >
        {{ c }}
      </button>
    </div>

    <div v-if="loading" class="e-loading">加载中...</div>
    <div v-else-if="!filtered.length" class="e-empty">暂无口语话题</div>

    <button v-for="t in filtered" :key="t.id" class="e-list-item" @click="goPractice(t)">
      <span class="icon">🎧</span>
      <div class="body">
        <span class="title">{{ t.title }}</span>
        <span class="desc">{{ t.category }} · {{ t.lines.length }} 句 · 练习 {{ t.practice_count }} 次</span>
      </div>
      <span v-if="t.best_score" class="e-chip e-chip-green">{{ t.best_score }} 分</span>
      <span class="arrow">→</span>
    </button>
  </div>
</template>