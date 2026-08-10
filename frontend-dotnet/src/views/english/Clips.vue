<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { englishApi } from '../../api/english'
import type { VideoClip } from '../../types/english'

const router = useRouter()
const clips = ref<VideoClip[]>([])
const loading = ref(true)
const sourceFilter = ref('全部')

const sources = computed(() => ['全部', ...new Set(clips.value.map(c => c.source || '其他'))])
const filtered = computed(() =>
  sourceFilter.value === '全部' ? clips.value : clips.value.filter(c => (c.source || '其他') === sourceFilter.value)
)

function goDetail(c: VideoClip) {
  router.push({ name: 'english-clip-detail', params: { id: c.id } })
}

async function load() {
  loading.value = true
  try {
    clips.value = await englishApi.getClips()
  } catch (e) {}
  loading.value = false
}

onMounted(load)
</script>

<template>
  <div class="e-page">
    <div class="e-header">
      <h2 class="e-title">🎬 影视切片</h2>
      <span class="e-subtitle">边看边学地道表达</span>
    </div>

    <div class="e-tabs">
      <button
        v-for="s in sources"
        :key="s"
        class="e-tab"
        :class="{ active: sourceFilter === s }"
        @click="sourceFilter = s"
      >
        {{ s }}
      </button>
    </div>

    <div v-if="loading" class="e-loading">加载中...</div>
    <div v-else-if="!filtered.length" class="e-empty">暂无影视切片</div>

    <button v-for="c in filtered" :key="c.id" class="clip-card" @click="goDetail(c)">
      <div class="clip-cover">
        <img v-if="c.cover_url" :src="c.cover_url" alt="" />
        <span v-else class="play-icon">▶</span>
        <span class="duration">{{ Math.floor(c.duration / 60) }}:{{ String(Math.floor(c.duration % 60)).padStart(2, '0') }}</span>
      </div>
      <div class="clip-body">
        <span class="clip-title">{{ c.title }}</span>
        <span class="clip-desc">{{ c.source }} · {{ c.level }} · {{ c.line_count }} 句</span>
        <div class="e-progress" style="margin-top: 6px">
          <div class="e-progress-fill" :style="{ width: c.line_count ? Math.min(100, Math.round((c.learned_count / c.line_count) * 100)) + '%' : '0%' }"></div>
        </div>
      </div>
      <span class="arrow">→</span>
    </button>
  </div>
</template>

<style scoped>
.clip-card {
  display: flex;
  gap: 12px;
  padding: 12px;
  background: var(--card);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  cursor: pointer;
  transition: all 0.2s;
  text-align: left;
  width: 100%;
}
.clip-card:hover { border-color: var(--primary); box-shadow: 0 2px 8px rgba(0,0,0,0.06); }
.clip-cover {
  width: 92px;
  height: 54px;
  border-radius: 8px;
  background: #111;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  position: relative;
  flex-shrink: 0;
  overflow: hidden;
}
.clip-cover img { width: 100%; height: 100%; object-fit: cover; }
.play-icon { font-size: 22px; }
.duration { position: absolute; right: 4px; bottom: 4px; font-size: 9px; background: rgba(0,0,0,0.7); padding: 1px 4px; border-radius: 4px; }
.clip-body { flex: 1; min-width: 0; display: flex; flex-direction: column; }
.clip-title { font-size: 14px; font-weight: 700; }
.clip-desc { font-size: 12px; color: var(--text-secondary); margin-top: 2px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.arrow { color: var(--text-secondary); font-size: 18px; align-self: center; }
</style>