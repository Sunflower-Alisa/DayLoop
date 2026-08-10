<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { englishApi } from '../../api/english'
import type { Scenario } from '../../types/english'

const router = useRouter()
const scenarios = ref<Scenario[]>([])
const loading = ref(true)
const levelFilter = ref(0)
const categories = [0, 1, 2, 3]

const filtered = computed(() => {
  return levelFilter.value === 0 ? scenarios.value : scenarios.value.filter(s => s.level === levelFilter.value)
})

function goDetail(s: Scenario) {
  router.push({ name: 'english-scenario-detail', params: { id: s.id } })
}

async function load() {
  loading.value = true
  try {
    scenarios.value = await englishApi.getScenarios()
  } catch (e) {}
  loading.value = false
}

onMounted(load)
</script>

<template>
  <div class="e-page">
    <div class="e-header">
      <h2 class="e-title">🗣️ 场景英语</h2>
      <span class="e-subtitle">{{ filtered.length }} 个场景</span>
    </div>

    <div class="e-tabs">
      <button
        v-for="lv in categories"
        :key="lv"
        class="e-tab"
        :class="{ active: levelFilter === lv }"
        @click="levelFilter = lv"
      >
        {{ lv === 0 ? '全部' : 'Lv.' + lv }}
      </button>
    </div>

    <div v-if="loading" class="e-loading">加载中...</div>
    <div v-else-if="!filtered.length" class="e-empty">暂无场景</div>

    <button v-for="s in filtered" :key="s.id" class="e-list-item" @click="goDetail(s)">
      <span class="icon">{{ s.icon || '💬' }}</span>
      <div class="body">
        <span class="title">
          {{ s.title }}
          <span v-if="s.mastered" class="e-chip e-chip-green" style="margin-left: 6px">已掌握</span>
        </span>
        <span class="desc">Lv.{{ s.level }} · {{ s.line_count }} 句 · {{ s.description }}</span>
      </div>
      <span class="arrow">→</span>
    </button>
  </div>
</template>