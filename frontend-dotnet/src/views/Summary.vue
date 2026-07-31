<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { api } from '../api'
import type { Summary } from '../types'
import { formatDate, renderMarkdown } from '../utils/format'

const types = ['weekly', 'monthly', 'quarterly', 'yearly'] as const
type PeriodType = typeof types[number]
const activeType = ref<PeriodType>('weekly')

const now = new Date()
const currentYear = ref(now.getFullYear())
const currentMonth = ref(now.getMonth() + 1)
const currentWeek = ref(getWeekNumber(now))
const currentQuarter = ref(Math.ceil((now.getMonth() + 1) / 3))
const summary = ref<Summary | null>(null)
const content = ref('')
const saved = ref(false)
const generating = ref(false)
const loading = ref(false)
const error = ref('')

function getWeekNumber(d: Date): number {
  const jan1 = new Date(d.getFullYear(), 0, 1)
  const days = Math.floor((d.getTime() - jan1.getTime()) / 86400000)
  return Math.ceil((days + jan1.getDay() + 1) / 7)
}

function periodKey(): string {
  if (activeType.value === 'weekly') return `${currentYear.value}-W${String(currentWeek.value).padStart(2, '0')}`
  if (activeType.value === 'monthly') return `${currentYear.value}-${String(currentMonth.value).padStart(2, '0')}`
  if (activeType.value === 'quarterly') return `${currentYear.value}-Q${currentQuarter.value}`
  return `${currentYear.value}`
}

function periodLabel(): string {
  if (activeType.value === 'weekly') {
    const jan1 = new Date(currentYear.value, 0, 1)
    const days = (currentWeek.value - 1) * 7
    const mon = new Date(jan1); mon.setDate(jan1.getDate() + days - jan1.getDay() + 1)
    const sun = new Date(mon); sun.setDate(mon.getDate() + 6)
    const ds = `${mon.getFullYear()}-${String(mon.getMonth() + 1).padStart(2, '0')}-${String(mon.getDate()).padStart(2, '0')}`
    const de = `${sun.getFullYear()}-${String(sun.getMonth() + 1).padStart(2, '0')}-${String(sun.getDate()).padStart(2, '0')}`
    return `${formatDate(ds)} - ${formatDate(de)}`
  }
  if (activeType.value === 'monthly') return `${currentYear.value}年${currentMonth.value}月`
  if (activeType.value === 'quarterly') return `${currentYear.value}年第${currentQuarter.value}季度`
  return `${currentYear.value}年`
}

function shiftPeriod(delta: number) {
  if (activeType.value === 'weekly') {
    currentWeek.value += delta
    if (currentWeek.value < 1) { currentWeek.value = 52; currentYear.value-- }
    if (currentWeek.value > 52) { currentWeek.value = 1; currentYear.value++ }
  } else if (activeType.value === 'monthly') {
    currentMonth.value += delta
    if (currentMonth.value < 1) { currentMonth.value = 12; currentYear.value-- }
    if (currentMonth.value > 12) { currentMonth.value = 1; currentYear.value++ }
  } else if (activeType.value === 'quarterly') {
    currentQuarter.value += delta
    if (currentQuarter.value < 1) { currentQuarter.value = 4; currentYear.value-- }
    if (currentQuarter.value > 4) { currentQuarter.value = 1; currentYear.value++ }
  } else {
    currentYear.value += delta
  }
  loadSummary()
}

async function loadSummary() {
  error.value = ''
  loading.value = true
  try {
    const key = periodKey()
    summary.value = await api.getSummary(activeType.value, key)
    content.value = summary.value?.content || ''
  } catch (e: any) {
    error.value = '加载失败: ' + (e.message || e)
  } finally {
    loading.value = false
  }
}

async function saveSummary() {
  error.value = ''
  try {
    const key = periodKey()
    summary.value = await api.saveSummary(activeType.value, key, content.value)
    saved.value = true
    setTimeout(() => saved.value = false, 2000)
  } catch (e: any) {
    error.value = '保存失败: ' + (e.message || e)
  }
}

async function generateSummary() {
  error.value = ''
  generating.value = true
  try {
    const key = periodKey()
    summary.value = await api.generateSummary(activeType.value, key)
    content.value = summary.value?.content || ''
  } catch (e: any) {
    error.value = '生成失败: ' + (e.message || e)
  } finally {
    generating.value = false
  }
}

onMounted(loadSummary)
</script>

<template>
  <div class="summary-page">
    <div v-if="error" class="error-banner" @click="error = ''">{{ error }}</div>
    <div v-if="loading" class="loading-indicator">加载中…</div>

    <div class="section-header">
      <div class="type-tabs">
        <button v-for="t in types" :key="t" :class="['tab', { active: activeType === t }]" @click="activeType = t; loadSummary()">
          {{ { weekly: '周总结', monthly: '月总结', quarterly: '季度总结', yearly: '年总结' }[t] }}
        </button>
      </div>
      <div class="period-nav">
        <button class="btn-nav" @click="shiftPeriod(-1)">&lt;</button>
        <h2>{{ periodLabel() }}</h2>
        <button class="btn-nav" @click="shiftPeriod(1)">&gt;</button>
      </div>
    </div>

    <div class="auto-summary" v-if="summary?.auto_summary">
      <h3>📊 自动生成总结</h3>
      <div class="summary-text" v-html="renderMarkdown(summary.auto_summary)"></div>
    </div>

    <div class="manual-section">
      <h3>✏️ 手动总结</h3>
      <textarea
        v-model="content"
        :placeholder="'写下你的' + { weekly: '周', monthly: '月', quarterly: '季度', yearly: '年' }[activeType] + '总结…'"
        rows="8"
      ></textarea>
      <div class="btn-row">
        <button class="btn btn-secondary" @click="generateSummary" :disabled="generating">
          {{ generating ? '生成中…' : '🔄 重新生成' }}
        </button>
        <button class="btn btn-primary" @click="saveSummary">
          {{ saved ? '已保存 ✓' : '保存总结' }}
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.section-header {
  margin-bottom: 16px;
}

.type-tabs {
  display: flex;
  gap: 4px;
  margin-bottom: 12px;
  background: var(--card);
  border-radius: var(--radius);
  padding: 4px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
}

.tab {
  flex: 1;
  padding: 8px 4px;
  border: none;
  border-radius: 6px;
  background: transparent;
  color: var(--text-secondary);
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s;
}

.tab.active {
  background: var(--primary);
  color: white;
  font-weight: 600;
}

.period-nav {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
}

.period-nav h2 {
  font-size: 16px;
  color: var(--text-secondary);
  font-weight: 500;
  min-width: 200px;
  text-align: center;
}

.btn-nav {
  width: 32px; height: 32px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--card);
  color: var(--text);
  font-size: 16px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}

.btn-nav:hover {
  background: var(--primary);
  color: white;
  border-color: var(--primary);
}

.auto-summary {
  background: var(--card);
  border-radius: var(--radius);
  padding: 16px;
  margin-bottom: 16px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
}

.auto-summary h3 {
  font-size: 15px;
  margin-bottom: 8px;
}

.summary-text {
  font-size: 13px;
  line-height: 1.7;
  color: var(--text);
}

.summary-text :deep(h2) {
  font-size: 16px;
  margin: 0 0 8px;
  color: var(--text);
}

.summary-text :deep(p) {
  margin: 0 0 6px;
}

.summary-text :deep(ul) {
  margin: 4px 0 8px;
  padding-left: 18px;
}

.summary-text :deep(li) {
  margin-bottom: 2px;
}

.summary-text :deep(strong) {
  font-weight: 600;
}

.manual-section {
  background: var(--card);
  border-radius: var(--radius);
  padding: 16px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
}

.manual-section h3 {
  font-size: 15px;
  margin-bottom: 12px;
}

.manual-section textarea {
  width: 100%;
  padding: 12px;
  border: 1px solid var(--border);
  border-radius: 8px;
  font-size: 14px;
  line-height: 1.6;
  resize: vertical;
  outline: none;
  font-family: inherit;
}

.manual-section textarea:focus {
  border-color: var(--primary);
}

.btn-row {
  display: flex;
  gap: 8px;
  margin-top: 12px;
}

.btn-row .btn {
  flex: 1;
}

.error-banner {
  background: #fee2e2;
  color: #dc2626;
  padding: 10px 16px;
  border-radius: var(--radius-sm);
  font-size: 13px;
  margin-bottom: 12px;
  cursor: pointer;
}

.loading-indicator {
  text-align: center;
  padding: 40px;
  color: var(--text-secondary);
  font-size: 14px;
}
</style>
