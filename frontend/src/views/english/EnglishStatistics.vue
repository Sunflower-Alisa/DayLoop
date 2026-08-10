<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { englishApi } from '../../api/english'
import type { EnglishDashboard } from '../../types/english'
import { formatDuration } from '../../utils/speech'

const dash = ref<EnglishDashboard | null>(null)
const loading = ref(true)

const masteredPct = computed(() =>
  dash.value && dash.value.total_words > 0 ? Math.round((dash.value.mastered_words / dash.value.total_words) * 100) : 0
)

async function load() {
  loading.value = true
  try {
    dash.value = await englishApi.dashboard()
  } catch (e) {}
  loading.value = false
}

onMounted(load)
</script>

<template>
  <div class="e-page">
    <div class="e-header">
      <h2 class="e-title">📈 学习统计</h2>
      <span v-if="dash && dash.streak" class="e-chip e-chip-amber">🔥 连续 {{ dash.streak }} 天</span>
    </div>

    <div v-if="loading" class="e-loading">加载中...</div>

    <template v-else-if="dash">
      <div class="section">学习时长</div>
      <div class="e-grid">
        <div class="e-stat"><div class="num">{{ formatDuration(dash.today_seconds) }}</div><div class="label">今日</div></div>
        <div class="e-stat"><div class="num">{{ formatDuration(dash.week_seconds) }}</div><div class="label">本周</div></div>
        <div class="e-stat"><div class="num">{{ formatDuration(dash.total_seconds) }}</div><div class="label">累计</div></div>
      </div>

      <div class="section">单词量</div>
      <div class="e-card">
          <div class="word-nums">
            <div class="wnum"><span class="big">{{ dash.total_words }}</span><span class="lbl">总词数</span></div>
            <div class="wnum"><span class="big" style="color: var(--success)">{{ dash.mastered_words }}</span><span class="lbl">已掌握</span></div>
            <div class="wnum"><span class="big" style="color: #d97706">{{ dash.learning_words }}</span><span class="lbl">学习中</span></div>
          </div>
          <div class="e-progress" style="margin-top: 10px">
            <div class="e-progress-fill green" :style="{ width: masteredPct + '%' }"></div>
          </div>
          <div class="e-subtitle" style="margin-top: 6px; text-align: center">掌握率 {{ masteredPct }}%</div>
      </div>

      <div class="section">功能模块</div>
      <div class="e-grid">
        <div class="module-card">
          <div class="mc-icon">📕</div>
          <div class="mc-label">错词本</div>
          <div class="mc-value" :class="{ bad: dash.wrong_count > 0 }">{{ dash.wrong_count }} 词</div>
        </div>
        <div class="module-card">
          <div class="mc-icon">🗣️</div>
          <div class="mc-label">场景英语</div>
          <div class="mc-value">{{ dash.scenario_mastered }}/{{ dash.scenario_count }} 掌握</div>
        </div>
        <div class="module-card">
          <div class="mc-icon">🎙️</div>
          <div class="mc-label">口语均分</div>
          <div class="mc-value">{{ dash.speaking_avg ? dash.speaking_avg + ' 分' : '—' }}</div>
        </div>
        <div class="module-card">
          <div class="mc-icon">🎬</div>
          <div class="mc-label">影视切片</div>
          <div class="mc-value">{{ dash.clip_count }} 个</div>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.section { font-size: 13px; font-weight: 700; color: var(--text-secondary); margin-top: 8px; }
.word-nums { display: flex; justify-content: space-around; text-align: center; }
.wnum { display: flex; flex-direction: column; }
.wnum .big { font-size: 28px; font-weight: 800; color: var(--primary); }
.wnum .lbl { font-size: 12px; color: var(--text-secondary); }
.module-card { background: var(--card); border: 1px solid var(--border); border-radius: var(--radius); padding: 14px; text-align: center; }
.mc-icon { font-size: 26px; }
.mc-label { font-size: 12px; color: var(--text-secondary); margin-top: 4px; }
.mc-value { font-size: 16px; font-weight: 700; color: var(--primary); margin-top: 2px; }
.mc-value.bad { color: var(--danger); }
</style>