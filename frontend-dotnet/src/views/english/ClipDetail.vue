<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { englishApi } from '../../api/english'
import type { VideoClip, ClipLine } from '../../types/english'
import { speak, stopSpeaking, StudyTimer } from '../../utils/speech'
import AudioButton from '../../components/english/AudioButton.vue'

const router = useRouter()
const route = useRoute()
const clip = ref<VideoClip | null>(null)
const lines = ref<ClipLine[]>([])
const loading = ref(true)
const activeLine = ref(0)
const videoRef = ref<HTMLVideoElement | null>(null)

const timer = new StudyTimer('clips')
let sessionSaved = false

const lineCount = computed(() => lines.value.length)

async function load() {
  loading.value = true
  try {
    const data = await englishApi.getClip(Number(route.params.id))
    clip.value = data.clip
    lines.value = data.lines
  } catch (e) {}
  loading.value = false
}

function onTimeUpdate() {
  if (!videoRef.value) return
  const t = videoRef.value.currentTime
  const idx = lines.value.findIndex(l => t >= l.start_time && (l.end_time === 0 || t <= l.end_time))
  if (idx >= 0 && idx !== activeLine.value) activeLine.value = idx
}

function seekTo(l: ClipLine) {
  if (videoRef.value) videoRef.value.currentTime = l.startTime
  activeLine.value = lines.value.indexOf(l)
}

function finish() {
  if (sessionSaved) return
  sessionSaved = true
  englishApi.saveSession('clips', timer.stamp(), timer.stamp(), timer.seconds())
  router.push({ name: 'english-clips' })
}

async function loadVideo() {
  if (!clip.value?.path) return
  const v = videoRef.value
  if (!v) return
  v.load()
}

onMounted(load)

onBeforeUnmount(() => {
  stopSpeaking()
  if (!sessionSaved) {
    sessionSaved = true
    englishApi.saveSession('clips', timer.stamp(), timer.stamp(), timer.seconds())
  }
})
</script>

<template>
  <div class="e-page" v-if="clip">
    <div class="e-header">
      <button class="e-btn" @click="finish">← 返回</button>
      <span class="e-chip">{{ clip.source }} · {{ clip.level }}</span>
    </div>

    <div v-if="loading" class="e-loading">加载中...</div>

    <template v-else>
      <h2 class="e-title">{{ clip.title }}</h2>
      <div class="e-subtitle">{{ clip.description }}</div>

      <video
        v-if="clip.path"
        ref="videoRef"
        class="clip-video"
        controls
        :src="clip.path"
        @timeupdate="onTimeUpdate"
        @loadedmetadata="loadVideo"
      ></video>
      <div v-else class="e-empty">暂无视频文件，请将视频放入 backend/data/uploads/clips/</div>

      <div class="lines-list">
        <button
          v-for="(l, i) in lines"
          :key="l.id"
          class="line-row"
          :class="{ active: i === activeLine }"
          @click="seekTo(l)"
        >
          <span class="line-no">{{ String(i + 1).padStart(2, '0') }}</span>
          <div class="line-text">
            <div class="ls">{{ l.speaker }}</div>
            <div class="le">{{ l.en_text }}</div>
            <div class="lc">{{ l.cn_text }}</div>
          </div>
          <AudioButton :text="l.en_text" />
        </button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.clip-video { width: 100%; border-radius: var(--radius); background: #000; }
.lines-list { display: flex; flex-direction: column; gap: 8px; }
.line-row { display: flex; align-items: center; gap: 10px; padding: 10px 12px; background: var(--card); border: 1.5px solid var(--border); border-radius: var(--radius); cursor: pointer; text-align: left; width: 100%; transition: all 0.15s; }
.line-row.active { border-color: var(--primary); background: #eef2ff; }
.line-no { width: 26px; color: var(--text-secondary); font-size: 12px; font-weight: 700; flex-shrink: 0; }
.line-text { flex: 1; min-width: 0; }
.ls { font-size: 11px; color: var(--primary); font-weight: 700; }
.le { font-size: 14px; font-weight: 600; }
.lc { font-size: 12px; color: var(--text-secondary); }
</style>