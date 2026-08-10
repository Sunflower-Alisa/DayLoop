<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { englishApi } from '../../api/english'
import type { ScenarioDetail } from '../../types/english'
import AudioButton from '../../components/english/AudioButton.vue'
import { speak, stopSpeaking, StudyTimer } from '../../utils/speech'

const router = useRouter()
const route = useRoute()
const detail = ref<ScenarioDetail | null>(null)
const loading = ref(true)
const tab = ref<'lines' | 'phrases' | 'quiz'>('lines')

// quiz state
const quizIndex = ref(0)
const picked = ref<number | null>(null)
const quizStats = ref({ correct: 0, total: 0 })
const mastered = ref(false)

const timer = new StudyTimer('scenarios')
let sessionSaved = false

const lines = computed(() => detail.value?.lines ?? [])
const phrases = computed(() => detail.value?.phrases ?? [])
const quizzes = computed(() => detail.value?.quizzes ?? [])
const quiz = computed(() => quizzes.value[quizIndex.value])

async function load() {
  loading.value = true
  try {
    detail.value = await englishApi.getScenario(Number(route.params.id))
  } catch (e) {}
  loading.value = false
}

function choose(i: number) {
  if (picked.value !== null) return
  picked.value = i
  if (i === quiz.value!.answer_index) quizStats.value.correct++
  quizStats.value.total++
}

async function nextQuiz() {
  const next = quizIndex.value + 1
  if (next >= quizzes.value.length) {
    let ok = false
    try {
      ok = (await englishApi.submitQuiz(detail.value!.scenario.id, quizStats.value.total, quizStats.value.correct)).mastered
    } catch (e) {}
    mastered.value = ok
    tab.value = 'lines'
    return
  }
  quizIndex.value = next
  picked.value = null
}

function finish() {
  if (sessionSaved) return
  sessionSaved = true
  englishApi.saveSession('scenarios', timer.stamp(), timer.stamp(), timer.seconds())
  router.push({ name: 'english-scenarios' })
}

onMounted(async () => {
  await load()
  if (!detail.value || !lines.value.length) return
  setTimeout(() => {
    const first = detail.value!.lines[0]
    if (first) speak(first.en_text)
  }, 400)
})

onBeforeUnmount(() => {
  stopSpeaking()
  if (!sessionSaved) {
    sessionSaved = true
    englishApi.saveSession('study', timer.stamp(), timer.stamp(), timer.seconds())
  }
})
</script>

<template>
  <div class="e-page" v-if="detail">
    <div class="e-header">
      <button class="e-btn" @click="finish()">← 返回</button>
      <span v-if="detail.scenario.mastered" class="e-chip e-chip-green">已掌握</span>
    </div>
    <div>
      <h2 class="e-title">{{ detail.scenario.icon }} {{ detail.scenario.title }}</h2>
      <div class="e-subtitle">Lv.{{ detail.scenario.level }} · {{ detail.scenario.description }}</div>
    </div>

    <div class="e-tabs">
      <button class="e-tab" :class="{ active: tab === 'lines' }" @click="tab = 'lines'">对话 ({{ lines.length }})</button>
      <button class="e-tab" :class="{ active: tab === 'phrases' }" @click="tab = 'phrases'">句型 ({{ phrases.length }})</button>
      <button class="e-tab" :class="{ active: tab === 'quiz' }" @click="tab = 'quiz'">闯关 ({{ quizzes.length }})</button>
    </div>

    <template v-if="tab === 'lines'">
      <div v-for="l in lines" :key="l.id" class="line-card">
        <div class="line-head">
          <span class="speaker">{{ l.speaker }}</span>
          <div class="line-actions">
            <button class="e-chip e-chip-gray" @click="() => { speak(l.en_text) }">🔊 跟读</button>
            <button class="e-chip" @click="() => speak(l.cn_text)">🔊 中文</button>
          </div>
        </div>
        <div class="line-en">{{ l.en_text }}</div>
        <div class="line-cn">{{ l.cn_text }}</div>
      </div>
    </template>

    <template v-else-if="tab === 'phrases'">
      <div v-for="p in phrases" :key="p.id" class="e-card">
        <div class="phrase-main">
          <span style="font-weight: 700">{{ p.phrase }}</span>
          <button class="e-chip e-chip-gray" @click="speak(p.phrase)">🔊</button>
        </div>
        <div class="e-subtitle" style="margin-top: 4px">{{ p.meaning }}</div>
        <div v-if="p.example_en" class="e-example" style="margin-top: 8px">
          <div class="en">{{ p.example_en }}</div>
          <div class="cn">{{ p.example_cn }}</div>
        </div>
      </div>
    </template>

    <template v-else-if="tab === 'quiz'">
      <div v-if="quiz" class="e-card quiz-card">
        <div class="quiz-head">
          <span class="e-chip">{{ quizIndex + 1 }}/{{ quizzes.length }}</span>
          <span class="e-chip e-chip-green">{{ quizStats.correct }} 对</span>
        </div>
        <div class="quiz-text">{{ quiz.question_en }}</div>
        <div class="quiz-cn">{{ quiz.question_cn }}</div>
        <div class="options">
          <button
            v-for="(opt, i) in quiz.options"
            :key="i"
            class="option"
            :class="{
              picked: picked === i,
              correct: picked !== null && i === quiz.answer_index,
              wrong: picked === i && i !== quiz.answer_index,
            }"
            @click="choose(i)"
          >
            {{ opt }}
          </button>
        </div>
        <div v-if="picked !== null" class="quiz-feedback">
          <div :class="['fb', picked === quiz.answer_index ? 'ok' : 'no']">
            {{ picked === quiz.answer_index ? '正确答案！' : '✗ 正确是：' + quiz.options[quiz.answer_index] }}
          </div>
          <div class="explain">{{ quiz.explanation }}</div>
          <button class="e-btn e-btn-primary e-btn-block" @click="nextQuiz">
            {{ quizIndex + 1 >= quizzes.length ? (mastered ? '完成' : '提交成绩') : '下一题 →' }}
          </button>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.line-card { padding: 14px; background: var(--card); border: 1px solid var(--border); border-radius: var(--radius); margin-bottom: 10px; }
.line-head { display: flex; align-items: center; justify-content: space-between; }
.speaker { font-size: 12px; font-weight: 700; color: var(--primary); }
.line-actions { display: flex; gap: 6px; }
.line-en { font-size: 16px; font-weight: 600; margin-top: 8px; }
.line-cn { color: var(--text-secondary); font-size: 13px; margin-top: 4px; }
.phrase-main { display: flex; align-items: center; gap: 8px; }
.quiz-card { }
.quiz-head { display: flex; gap: 8px; }
.quiz-text { font-size: 20px; font-weight: 700; margin-top: 14px; }
.quiz-cn { color: var(--text-secondary); font-size: 13px; margin-top: 4px; margin-bottom: 14px; }
.options { display: flex; flex-direction: column; gap: 10px; }
.option { padding: 14px; border: 1.5px solid var(--border); border-radius: 12px; font-size: 15px; background: var(--card); cursor: pointer; text-align: center; }
.option.picked { border-color: var(--primary); }
.option.correct { border-color: var(--success); background: #ecfdf5; color: var(--success); font-weight: 700; }
.option.wrong { border-color: var(--danger); background: #fef2f2; color: var(--danger); }
.quiz-feedback { margin-top: 16px; }
.fb { text-align: center; font-size: 16px; font-weight: 700; margin-bottom: 8px; }
.fb.ok { color: var(--success); }
.fb.no { color: var(--danger); }
.explain { font-size: 13px; color: var(--text-secondary); margin-bottom: 12px; }
</style>