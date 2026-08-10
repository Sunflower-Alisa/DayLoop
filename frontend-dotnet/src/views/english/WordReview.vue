<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useRouter } from 'vue-router'
import { englishApi } from '../../api/english'
import type { Word } from '../../types/english'
import AudioButton from '../../components/english/AudioButton.vue'
import { speak, stopSpeaking, StudyTimer } from '../../utils/speech'

const router = useRouter()
const queue = ref<Word[]>([])
const index = ref(0)
const loadDone = ref(false)
const phase = ref<'study' | 'quiz' | 'done'>('study')

interface Quiz {
  type: 'meaning' | 'word'
  question: string
  prompt: string
  options: string[]
  optionIndex: number
  answer: string
  playWord?: string
}
const quiz = ref<Quiz | null>(null)
const pickedIndex = ref<number | null>(null)
const stats = ref({ correct: 0, wrong: 0, know: 0 })
const timer = new StudyTimer('review')
let sessionSaved = false

const current = computed<Word | null>(() => queue.value[index.value] ?? null)
const progressPct = computed(() => (queue.value.length ? Math.min(100, Math.round((index.value / queue.value.length) * 100)) : 0))

function shuffle<T>(arr: T[]): T[] {
  const a = [...arr]
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    ;[a[i], a[j]] = [a[j], a[i]]
  }
  return a
}

function buildOptions(pool: string[], answer: string): { list: string[]; index: number } {
  const others = shuffle(pool.filter(x => x !== answer))
  const list = shuffle([answer, ...others].slice(0, 4))
  return { list, index: list.indexOf(answer) }
}

function poolOf(field: 'meaning' | 'word'): string[] {
  const s = new Set<string>()
  for (const w of queue.value) {
    if (field === 'meaning' && w.meaning) s.add(w.meaning)
    else if (field === 'word' && w.word) s.add(w.word)
  }
  return [...s]
}

function makeQuiz(): Quiz {
  const w = current.value!
  const wordPool = poolOf('word')
  const meaningPool = poolOf('meaning')
  const kind: 'meaning' | 'word' =
    meaningPool.length >= 4 ? (Math.random() < 0.5 ? 'meaning' : 'word') : wordPool.length >= 4 ? 'word' : 'meaning'

  if (kind === 'word') {
    const o = buildOptions(wordPool, w.word)
    return { type: 'word', question: w.meaning, prompt: `选择与「${w.meaning}」对应的单词`, options: o.list, optionIndex: o.index, answer: w.word, playWord: w.word }
  }
  const o = buildOptions(meaningPool, w.meaning)
  return { type: 'meaning', question: w.word, prompt: `${w.pos ? w.pos + ' ' : ''}选择正确释义`, options: o.list, optionIndex: o.index, answer: w.meaning, playWord: w.word }
}

async function submit(r: { correct: boolean; know?: boolean }) {
  const w = current.value
  if (!w) return
  try {
    await englishApi.submitLearn({ word_id: w.id, correct: r.correct, know: r.know, is_review: true })
  } catch (e) {}
}

function startStudy() {
  stopSpeaking()
  quiz.value = null
  pickedIndex.value = null
  if (current.value) setTimeout(() => speak(current.value!.word), 350)
}

async function goQuiz() {
  quiz.value = makeQuiz()
  phase.value = 'quiz'
  if (quiz.value.playWord) setTimeout(() => speak(quiz.value.playWord!, 0.95), 250)
}

async function choose(i: number) {
  if (pickedIndex.value !== null) return
  pickedIndex.value = i
  const correct = i === quiz.value!.optionIndex
  if (correct) stats.value.correct++
  else stats.value.wrong++
  await submit({ correct, know: false })
}

async function nextWord() {
  pickedIndex.value = null
  quiz.value = null
  const next = index.value + 1
  if (next >= queue.value.length) {
    phase.value = 'done'
    return
  }
  index.value = next
  phase.value = 'study'
  startStudy()
}

async function markKnow() {
  stats.value.know++
  await submit({ correct: true, know: true })
  await nextWord()
}

function finish() {
  if (sessionSaved) return
  sessionSaved = true
  englishApi.saveSession('review', timer.stamp(), timer.stamp(), timer.seconds())
}

function goHome() { router.push({ name: 'english-words' }) }
function goLearn() { router.push({ name: 'english-words-learn' }) }

onMounted(async () => {
  try {
    const task = await englishApi.getDaily()
    queue.value = task.review_words
  } catch (e) {}
  loadDone.value = true
  if (queue.value.length === 0) {
    phase.value = 'done'
  } else {
    startStudy()
  }
})

onBeforeUnmount(() => {
  stopSpeaking()
})
</script>

<template>
  <div class="learn-page">
    <div v-if="!loadDone" class="e-loading">准备中...</div>

    <template v-else-if="phase === 'done'">
      <div class="goal-card">
        <div class="goal-icon">✅</div>
        <div class="goal-title">复习完成</div>
        <div class="goal-sub">
          <template v-if="queue.length">共复习 {{ queue.length }} 个词</template>
          <template v-else>今日暂无需要复习的单词</template>
        </div>
        <div class="goal-stats" v-if="queue.length">
          <div class="stat"><div class="num ok">+{{ stats.correct + stats.know }}</div><div class="label">掌握</div></div>
          <div class="stat"><div class="num bad">{{ stats.wrong }}</div><div class="label">有待巩固</div></div>
        </div>
        <div class="goal-actions">
          <button class="btn" @click="goHome">返回</button>
          <button class="btn btn-primary" @click="goLearn">去学新词</button>
        </div>
      </div>
    </template>

    <template v-else-if="current">
      <div class="topbar">
        <button class="close-btn" @click="finish(); goHome()">✕</button>
        <div class="e-progress topbar-progress">
          <div class="e-progress-fill amber" :style="{ width: progressPct + '%' }"></div>
        </div>
        <span class="count">{{ index + 1 }}/{{ queue.length }}</span>
      </div>

      <div v-if="phase === 'study'" class="study-wrap">
        <div class="word-card">
          <div class="img-ban" :style="{ background: '#059669' }">
            <span v-if="current.image_url"><img :src="current.image_url" alt="" /></span>
            <span v-else class="big-letter">{{ current.word[0].toUpperCase() }}</span>
          </div>
          <div class="word-main">
            <span class="word">{{ current.word }}</span>
            <AudioButton :text="current.word" />
          </div>
          <div class="word-meta">
            <span v-if="current.phonetic" class="phonetic">{{ current.phonetic }}</span>
            <span v-if="current.pos" class="pos">{{ current.pos }}</span>
          </div>
          <div class="meaning">{{ current.meaning }}</div>
          <div v-if="current.example_en" class="example">
            <div class="en">{{ current.example_en }}</div>
            <div class="cn">{{ current.example_cn }}</div>
          </div>
        </div>
      </div>

      <div v-else-if="phase === 'quiz' && quiz" class="quiz-wrap">
        <div class="quiz-question">
          <span class="q-type">{{ quiz.type === 'word' ? '看义选词' : '看词选义' }}</span>
          <div class="q-text">{{ quiz.question }}</div>
          <div class="q-hint">{{ quiz.prompt }}</div>
        </div>
        <div class="options">
          <button
            v-for="(opt, i) in quiz.options"
            :key="i"
            class="option"
            :class="{
              picked: pickedIndex === i,
              correct: pickedIndex !== null && i === quiz.optionIndex,
              wrong: pickedIndex === i && i !== quiz.optionIndex,
            }"
            @click="choose(i)"
          >
            {{ opt }}
          </button>
        </div>
        <div v-if="pickedIndex !== null" class="quiz-feedback">
          <div :class="['fb', pickedIndex === quiz.optionIndex ? 'ok' : 'no']">
            {{ pickedIndex === quiz.optionIndex ? '✓ 记忆成功！' : '✗ 正确是：' + quiz.answer }}
          </div>
          <button class="e-btn e-btn-primary e-btn-block" @click="nextWord">下一词 →</button>
        </div>
      </div>

      <div v-if="phase === 'study'" class="study-actions">
        <button class="btn btn-ghost" @click="markKnow">😄 记住了</button>
        <button class="btn btn-primary" @click="goQuiz()">去答题 →</button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.learn-page { min-height: calc(100dvh - var(--top-bar-height)); display: flex; flex-direction: column; }
.topbar-progress { flex: 1; margin: 0 12px; }
.close-btn { background: none; border: none; font-size: 18px; color: var(--text-secondary); cursor: pointer; }
.count { font-size: 13px; color: var(--text-secondary); font-weight: 600; }
.topbar { display: flex; align-items: center; padding: 12px 0; }
.study-wrap, .quiz-wrap { flex: 1; display: flex; flex-direction: column; justify-content: center; }
.word-card { background: var(--card); border-radius: 16px; overflow: hidden; box-shadow: 0 4px 16px rgba(0,0,0,0.08); }
.img-ban { height: 180px; display: flex; align-items: flex-end; justify-content: center; padding-bottom: 12px; }
.img-ban img { width: 100%; height: 100%; object-fit: cover; }
.big-letter { color: #fff; font-size: 90px; font-weight: 800; }
.word-card { padding-bottom: 18px; }
.word-main { display: flex; align-items: center; gap: 8px; padding: 16px 18px 0; }
.word { font-size: 30px; font-weight: 800; }
.word-meta { padding: 4px 18px 0; display: flex; gap: 8px; }
.phonetic { color: var(--text-secondary); font-size: 14px; }
.pos { color: var(--primary); font-weight: 600; font-size: 13px; }
.meaning { padding: 10px 18px 0; font-size: 18px; font-weight: 600; }
.example { margin: 12px 18px 0; padding: 10px 12px; background: var(--bg); border-radius: 8px; font-size: 13px; line-height: 1.6; }
.example .cn { color: var(--text-secondary); }
.study-actions { display: flex; gap: 10px; padding: 16px 0; }
.study-actions .btn { flex: 1; padding: 12px; }
.btn { border-radius: 10px; font-size: 14px; font-weight: 600; cursor: pointer; border: none; }
.btn-primary { background: var(--primary); color: #fff; }
.btn-ghost { background: var(--bg); color: var(--text-secondary); }
.quiz-question { text-align: center; margin-bottom: 20px; }
.q-type { display: inline-block; font-size: 12px; color: var(--primary); background: #eef2ff; padding: 3px 10px; border-radius: 999px; }
.q-text { font-size: 22px; font-weight: 700; margin-top: 10px; }
.q-hint { font-size: 13px; color: var(--text-secondary); margin-top: 6px; }
.options { display: flex; flex-direction: column; gap: 10px; }
.option { padding: 14px; border: 1.5px solid var(--border); border-radius: 12px; font-size: 15px; background: var(--card); cursor: pointer; text-align: center; transition: all 0.15s; }
.option:not(:disabled):hover { border-color: var(--primary); }
.option.picked { border-color: var(--primary); }
.option.correct { border-color: var(--success); background: #ecfdf5; color: var(--success); font-weight: 700; }
.option.wrong { border-color: var(--danger); background: #fef2f2; color: var(--danger); }
.quiz-feedback { margin-top: 16px; }
.fb { text-align: center; font-size: 16px; font-weight: 700; margin-bottom: 12px; }
.fb.ok { color: var(--success); }
.fb.no { color: var(--danger); }
.goal-card { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center; }
.goal-icon { font-size: 60px; }
.goal-title { font-size: 22px; font-weight: 800; margin-top: 10px; }
.goal-sub { color: var(--text-secondary); margin-top: 6px; }
.goal-stats { display: flex; gap: 24px; margin-top: 24px; }
.goal-stats .num { font-size: 26px; font-weight: 800; }
.goal-stats .num.ok { color: var(--success); }
.goal-stats .num.bad { color: var(--danger); }
.goal-stats .label { font-size: 12px; color: var(--text-secondary); }
.goal-actions { display: flex; gap: 10px; margin-top: 28px; width: 100%; max-width: 320px; }
.goal-actions .btn { flex: 1; padding: 12px; border-radius: 10px; font-weight: 600; cursor: pointer; }
.goal-actions .btn-primary { background: var(--primary); color: #fff; }
.goal-actions .btn-ghost { background: var(--bg); color: var(--text); }
</style>