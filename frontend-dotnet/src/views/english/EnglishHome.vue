<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { englishApi } from '../../api/english'
import type { EnglishDashboard } from '../../types/english'
import { formatDuration } from '../../utils/speech'

const router = useRouter()
const dash = ref<EnglishDashboard | null>(null)
const loading = ref(true)

const entries = [
  { name: 'english-words', icon: '🔤', title: '单词背诵', desc: '百词斩式看图学词', color: '#4f46e5' },
  { name: 'english-scenarios', icon: '💬', title: '场景英语', desc: '真实场景开口说', color: '#0891b2' },
  { name: 'english-speaking', icon: '🎤', title: '口语练习', desc: '跟读与录音评测', color: '#7c3aed' },
  { name: 'english-clips', icon: '🎬', title: '影视切片', desc: '观影学地道表达', color: '#db2777' },
]

const startPct = computed(() =>
  dash.value && dash.value.new_goal > 0
    ? Math.min(100, Math.round((dash.value.new_done / dash.value.new_goal) * 100))
    : 0
)

async function load() {
  loading.value = true
  try {
    dash.value = await englishApi.dashboard()
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
    <div v-if="loading" class="e-loading">加载中...</div>

    <template v-if="dash">
      <div class="hero">
        <div class="hero-row">
          <div>
            <div class="hero-title">英语学习</div>
            <div class="hero-sub">学 · 练 · 测 · 复习 · 沉淀</div>
          </div>
          <button class="streak-badge" @click="go('english-statistics')">
            🔥 {{ dash.streak }} 天
          </button>
        </div>

        <div class="hero-progress">
          <div class="hero-line">
            <span>今日进度</span>
            <span>{{ dash.new_done }}/{{ dash.new_goal }} 新词 · {{ dash.review_done }} 复习</span>
          </div>
          <div class="e-progress hero-track">
            <div class="e-progress-fill" :style="{ width: startPct + '%', background: '#fff' }"></div>
          </div>
          <div class="hero-time">
            <span>⏱ 今日 {{ formatDuration(dash.today_seconds) }}</span>
            <span>本周 {{ formatDuration(dash.week_seconds) }}</span>
          </div>
        </div>
      </div>

      <div class="e-grid">
        <div class="e-stat"><div class="num">{{ dash.total_words }}</div><div class="label">累计学习</div></div>
        <div class="e-stat"><div class="num" style="color: var(--success)">{{ dash.mastered_words }}</div><div class="label">已掌握</div></div>
        <div class="e-stat"><div class="num" style="color: var(--warning)">{{ dash.learning_words }}</div><div class="label">待复习</div></div>
        <div class="e-stat"><div class="num" style="color: var(--danger)">{{ dash.wrong_count }}</div><div class="label">错词本</div></div>
      </div>

      <div class="section-label">功能入口</div>
      <div class="e-grid">
        <button v-for="e in entries" :key="e.name" class="entry-btn" :style="{ '--c': e.color }" @click="go(e.name)">
          <span class="entry-icon">{{ e.icon }}</span>
          <span class="entry-title">{{ e.title }}</span>
          <span class="entry-desc">{{ e.desc }}</span>
        </button>
      </div>

      <div class="e-card">
        <div class="e-header" style="margin-bottom: 10px">
          <span style="font-weight: 700">今日学习</span>
          <button class="e-chip" @click="go('english-words')">
            {{ dash.new_done >= dash.new_goal ? '今日达标 ✓' : '去背单词 →' }}
          </button>
        </div>
        <div style="font-size: 13px; color: var(--text-secondary); line-height: 1.8">
          <div>场景完成 {{ dash.scenario_mastered }}/{{ dash.scenario_count }}</div>
          <div>口语平均分 {{ dash.speaking_avg }} · 影视切片 {{ dash.clip_count }}</div>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.hero {
  background: linear-gradient(135deg, #4f46e5, #3730a3);
  padding: 18px;
  border-radius: var(--radius);
  color: #fff;
  box-shadow: 0 4px 12px rgba(79,70,229,0.25);
}
.hero-row { display: flex; align-items: center; justify-content: space-between; }
.hero-title { font-size: 20px; font-weight: 800; }
.hero-sub { font-size: 12px; opacity: 0.85; margin-top: 2px; }
.streak-badge { background: rgba(255,255,255,0.2); border: none; color: #fff; font-weight: 700; padding: 6px 14px; border-radius: 999px; font-size: 14px; cursor: pointer; }
.hero-progress { margin-top: 14px; }
.hero-line { display: flex; justify-content: space-between; font-size: 13px; opacity: 0.9; }
.hero-track { margin-top: 8px; background: rgba(255,255,255,0.25); }
.hero-time { font-size: 12px; opacity: 0.85; margin-top: 10px; display: flex; gap: 14px; }
.section-label { font-size: 13px; font-weight: 600; color: var(--text-secondary); margin-top: 4px; }
.entry-btn { display: flex; flex-direction: column; align-items: flex-start; gap: 2px; padding: 16px; border: 1px solid var(--border); border-radius: var(--radius); background: var(--card); cursor: pointer; text-align: left; transition: all 0.2s; }
.entry-btn:hover { border-color: var(--c); transform: translateY(-1px); box-shadow: 0 3px 10px rgba(0,0,0,0.08); }
.entry-icon { font-size: 26px; }
.entry-title { font-size: 15px; font-weight: 700; color: var(--c); margin-top: 4px; }
.entry-desc { font-size: 12px; color: var(--text-secondary); margin-top: 2px; }
</style>