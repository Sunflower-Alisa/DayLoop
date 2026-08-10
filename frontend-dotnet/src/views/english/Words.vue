<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { englishApi } from '../../api/english'
import type { DailyWordTask } from '../../types/english'

const router = useRouter()
const task = ref<DailyWordTask | null>(null)
const loading = ref(true)

const newPct = computed(() =>
  task.value && task.value.new_goal > 0
    ? Math.min(100, Math.round((task.value.new_done / task.value.new_goal) * 100))
    : 0
)

async function load() {
  loading.value = true
  try {
    task.value = await englishApi.getDaily()
  } catch (e) {
    // ignore
  } finally {
    loading.value = false
  }
}

function go(name: string) {
  router.push({ name })
}

onMounted(load)
</script>

<template>
  <div class="e-page">
    <div class="e-header">
      <h2 class="e-title">单词背诵</h2>
      <button class="e-chip" @click="go('english-wordbooks')">📚 词书</button>
    </div>

    <div v-if="loading" class="e-loading">加载中...</div>

    <template v-if="task">
      <div v-if="!task.has_book" class="e-card">
        <div class="e-empty" style="border: none">
          <p style="font-size: 15px">还没有选择词书</p>
          <button class="e-btn e-btn-primary" style="margin-top: 12px" @click="go('english-wordbooks')">选择词书</button>
        </div>
      </div>

      <template v-else>
        <div class="e-card">
          <div class="task-row">
            <div>
              <div class="task-num">{{ task.new_done }}<span>/{{ task.new_goal }}</span></div>
              <div class="task-label">今日新词</div>
            </div>
            <div class="task-mid">
              <div class="e-progress">
                <div class="e-progress-fill" :style="{ width: newPct + '%' }"></div>
              </div>
              <div class="task-sub">{{ task.review_done }} 个词已复习</div>
            </div>
          </div>
        </div>

        <button
          class="e-btn e-btn-primary e-btn-block start-btn"
          :disabled="task.new_words.length === 0"
          @click="go('english-words-learn')"
        >
          🔤 开始学习新词（{{ task.new_words.length }}）
        </button>
        <button
          class="e-btn e-btn-block start-btn"
          :disabled="task.review_words.length === 0"
          @click="go('english-words-review')"
        >
          🔁 开始复习（{{ task.review_words.length }}）
        </button>

        <div class="e-grid" style="margin-top: 4px">
          <button class="e-list-item" @click="go('english-words-wrong')">
            <span class="icon">📕</span>
            <div class="body"><span class="title">错词本</span><span class="desc">集中回顾易错词</span></div>
            <span class="arrow">→</span>
          </button>
          <button class="e-list-item" @click="go('english-statistics')">
            <span class="icon">📈</span>
            <div class="body"><span class="title">学习统计</span><span class="desc">单词量与掌握率</span></div>
            <span class="arrow">→</span>
          </button>
        </div>

        <template v-if="task.new_words.length">
          <div class="section-label">今日新词预览</div>
          <div class="chip-row">
            <span v-for="w in task.new_words" :key="w.id" class="e-chip">{{ w.word }}</span>
          </div>
        </template>
        <template v-if="task.review_words.length">
          <div class="section-label" style="margin-top: 8px">待复习</div>
          <div class="chip-row">
            <span v-for="w in task.review_words" :key="w.id" class="e-chip e-chip-amber">{{ w.word }}</span>
          </div>
        </template>
      </template>
    </template>
  </div>
</template>

<style scoped>
.start-btn { padding: 14px; font-size: 15px; }
.start-btn.e-btn-green {
  background: var(--success);
  color: #fff;
  border-color: var(--success);
}
.start-btn.e-btn-green:hover:not(:disabled) { background: #059669; color: #fff; }
.task-row { display: flex; align-items: center; gap: 16px; }
.task-num { font-size: 28px; font-weight: 800; color: var(--primary); }
.task-num span { font-size: 15px; color: var(--text-secondary); }
.task-label { font-size: 13px; color: var(--text-secondary); }
.task-mid { flex: 1; }
.task-sub { font-size: 12px; color: var(--text-secondary); margin-top: 6px; }
.section-label { font-size: 13px; font-weight: 600; color: var(--text-secondary); margin-top: 4px; }
.chip-row { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 8px; }
</style>