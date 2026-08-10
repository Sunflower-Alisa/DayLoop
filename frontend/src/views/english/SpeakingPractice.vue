<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { englishApi } from '../../api/english'
import type { SpeakingTopic } from '../../types/english'
import type { ScoreResult } from '../../utils/speech'
import { speak, stopSpeaking, evaluateUtterance, recordUtterance, blobToDataUrl, StudyTimer } from '../../utils/speech'

const router = useRouter()
const route = useRoute()
const topic = ref<SpeakingTopic | null>(null)
const loading = ref(true)
const lineIndex = ref(0)
const listening = ref(false)
const recording = ref(false)
const evaluating = ref(false)
const result = ref<ScoreResult | null>(null)
const audioUrl = ref('')
const timer = new StudyTimer('speaking')
let sessionSaved = false

const line = computed(() => topic.value?.lines[lineIndex.value])

async function load() {
  loading.value = true
  try {
    topic.value = await englishApi.getSpeakingTopic(Number(route.params.id))
  } catch (e) {}
  loading.value = false
}

function playLine() {
  const l = line.value
  if (l) speak(l.en)
}

async function practice() {
  const l = line.value
  if (!l || recording.value || evaluating.value) return
  recording.value = true
  result.value = null
  audioUrl.value = ''
  try {
    const blob = await recordUtterance(Math.min(4000, Math.max(1500, l.en.split(/\s+/).length * 500)))
    recording.value = false
    evaluating.value = true
    const [score, dataUrl] = await Promise.all([evaluateUtterance(l.en), blobToDataUrl(blob)])
    result.value = score
    audioUrl.value = dataUrl
    await englishApi.saveSpeakingRecord({
      topic_id: topic.value!.id,
      line_index: lineIndex.value,
      audio_url: '',
      accuracy: score.accuracy,
      fluency: score.fluency,
      completeness: score.completeness,
      overall: score.overall,
    })
    if (topic.value && score.overall > (topic.value.best_score || 0)) {
      topic.value.best_score = score.overall
    }
  } catch (e) {
    // permission denied or unsupported
  } finally {
    recording.value = false
    evaluating.value = false
  }
}

async function next() {
  result.value = null
  audioUrl.value = ''
  const next = lineIndex.value + 1
  if (next >= (topic.value?.lines.length ?? 0)) {
    if (!sessionSaved) {
      sessionSaved = true
      englishApi.saveSession('speaking', timer.stamp(), timer.stamp(), timer.seconds())
    }
    router.push({ name: 'english-speaking' })
    return
  }
  lineIndex.value = next
}

function exit() {
  if (!sessionSaved) {
    sessionSaved = true
    englishApi.saveSession('speaking', timer.stamp(), timer.stamp(), timer.seconds())
  }
  router.push({ name: 'english-speaking' })
}

function toggleListen() {
  if (listening.value) {
    stopSpeaking()
    listening.value = false
  } else {
    playLine()
    listening.value = true
    setTimeout(() => (listening.value = false), 4000)
  }
}

onMounted(async () => {
  await load()
  playLine()
})

onBeforeUnmount(() => {
  stopSpeaking()
  if (!sessionSaved) {
    sessionSaved = true
    englishApi.saveSession('speaking', timer.stamp(), timer.stamp(), timer.seconds())
  }
})
</script>

<template>
  <div class="e-page" v-if="topic">
    <div class="e-header">
      <button class="e-btn" @click="exit">← 返回</button>
      <h2 class="e-title" style="flex: 1; text-align: center">🎙️ 跟读 {{ lineIndex + 1 }}/{{ topic.lines.length }}</h2>
      <span v-if="topic.best_score" class="e-chip e-chip-green">最高 {{ topic.best_score }} 分</span>
    </div>

    <div v-if="loading" class="e-loading">加载中...</div>

    <template v-else-if="line">
      <div class="listen-card">
        <div class="line-en">{{ line.en }}</div>
        <div class="line-cn">{{ line.cn }}</div>
        <div class="listen-actions">
          <button class="e-btn" @click="toggleListen">{{ listening ? '⏹ 停止' : '🔊 听原音' }}</button>
        </div>
      </div>

      <div class="mic-card">
        <button
          class="mic-btn"
          :class="{ active: recording, disabled: recording || evaluating }"
          @click="practice"
        >
          {{ evaluating ? '⏳ 评测中...' : recording ? '🔴 录音中...' : '🎤 点击跟读' }}
        </button>
        <div class="e-subtitle" style="margin-top: 8px">需要麦克风权限，请用 Chrome/Edge 打开</div>
      </div>

      <div v-if="result" class="score-card">
        <div class="score-main">
          <div class="score-num" :class="{ good: result.overall >= 80, mid: result.overall >= 60 && result.overall < 80 }">
            {{ result.overall }}
          </div>
          <div class="score-label">综合得分</div>
        </div>
        <div class="score-bars">
          <div class="bar-row"><span class="bar-label">准确度</span><div class="e-progress" style="flex: 1"><div class="e-progress-fill" :style="{ width: result.accuracy + '%' }"></div></div><span class="bar-val">{{ result.accuracy }}</span></div>
          <div class="bar-row"><span class="bar-label">流利度</span><div class="e-progress" style="flex: 1"><div class="e-progress-fill" :style="{ width: result.fluency + '%' }"></div></div><span class="bar-val">{{ result.fluency }}</span></div>
          <div class="bar-row"><span class="bar-label">完整度</span><div class="e-progress" style="flex: 1"><div class="e-progress-fill" :style="{ width: result.completeness + '%' }"></div></div><span class="bar-val">{{ result.completeness }}</span></div>
        </div>
        <div v-if="audioUrl" style="margin-top: 12px"><audio :src="audioUrl" controls style="width: 100%" /></div>
        <button class="e-btn e-btn-primary e-btn-block" style="margin-top: 14px" @click="next">
          {{ lineIndex + 1 >= topic.lines.length ? '完成练习 →' : '下一句 →' }}
        </button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.listen-card { background: var(--card); border: 1px solid var(--border); border-radius: var(--radius); padding: 18px; text-align: center; }
.line-en { font-size: 20px; font-weight: 700; line-height: 1.5; }
.line-cn { color: var(--text-secondary); font-size: 14px; margin-top: 6px; }
.listen-actions { margin-top: 12px; }
.mic-card { text-align: center; margin-top: 16px; }
.mic-btn { width: 120px; height: 120px; border-radius: 50%; border: none; font-size: 14px; font-weight: 700; background: var(--primary); color: #fff; cursor: pointer; transition: all 0.2s; }
.mic-btn.active { background: var(--danger); animation: pulse 1.2s infinite; }
.mic-btn.disabled { opacity: 0.6; cursor: wait; }
@keyframes pulse { 0% { box-shadow: 0 0 0 0 rgba(220,38,38,0.5); } 100% { box-shadow: 0 0 0 18px rgba(220,38,38,0); } }
.score-card { background: var(--card); border: 1px solid var(--border); border-radius: var(--radius); padding: 18px; margin-top: 16px; }
.score-main { text-align: center; }
.score-num { font-size: 52px; font-weight: 800; }
.score-num.good { color: var(--success); }
.score-num.mid { color: #f59e0b; }
.score-label { color: var(--text-secondary); font-size: 13px; }
.score-bars { display: flex; flex-direction: column; gap: 8px; margin-top: 14px; }
.bar-row { display: flex; align-items: center; gap: 8px; font-size: 12px; color: var(--text-secondary); }
.bar-val { width: 28px; text-align: right; font-weight: 600; }
</style>