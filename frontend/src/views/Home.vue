<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '../api'
import { englishApi } from '../api/english'
import type { Task } from '../types'
import type { EnglishDashboard } from '../types/english'
import { formatDuration } from '../utils/speech'

const router = useRouter()

function today(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

const weekdays = ['星期日', '星期一', '星期二', '星期三', '星期四', '星期五', '星期六']
const now = new Date()
const dateLabel = `${now.getFullYear()}年${now.getMonth() + 1}月${now.getDate()}日`
const weekdayLabel = weekdays[now.getDay()]

// Quick plan
const quickTitle = ref('')
const quickTime = ref('')
const quickDate = ref('')

function quickPlan() {
  if (!quickTitle.value.trim()) return
  const date = quickDate.value || today()
  router.push({ path: '/plan', query: { date, title: quickTitle.value, time: quickTime.value } })
}

// Tasks
const tasks = ref<Task[]>([])
const loading = ref(true)

const progress = computed(() => {
  const total = tasks.value.length
  const completed = tasks.value.filter(t => t.status === 'completed').length
  return { total, completed, percent: total > 0 ? Math.round((completed / total) * 100) : 0 }
})

async function fetchTasks() {
  loading.value = true
  try {
    tasks.value = await api.getTasks(today())
  } catch (e) {
    // ignore
  } finally {
    loading.value = false
  }
}

// Complete dialog
const showCompleteDialog = ref(false)
const completingTask = ref<Task | null>(null)
const completionForm = ref({
  achievement: '',
  note: '',
  actual_duration: null as number | null,
  actual_start: '',
  actual_end: '',
  sync_enabled: true,
})

function openCompleteDialog(task: Task) {
  const now = new Date()
  const timeStr = `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`
  completingTask.value = task
  completionForm.value = {
    achievement: task.achievement || '',
    note: task.note || '',
    actual_duration: task.actual_duration,
    actual_start: task.actual_start || task.start_time || timeStr,
    actual_end: task.actual_end || timeStr,
    sync_enabled: task.sync_enabled,
  }
  showCompleteDialog.value = true
}

async function confirmComplete() {
  if (!completingTask.value) return
  const task = completingTask.value
  try {
    await api.updateTask(task.id, {
      status: 'completed',
      achievement: completionForm.value.achievement,
      note: completionForm.value.note,
      actual_duration: completionForm.value.actual_duration,
      actual_start: completionForm.value.actual_start || null,
      actual_end: completionForm.value.actual_end || null,
      sync_enabled: completionForm.value.sync_enabled,
    } as Partial<Task>)
    task.achievement = completionForm.value.achievement
    task.note = completionForm.value.note
    task.actual_duration = completionForm.value.actual_duration
    task.sync_enabled = completionForm.value.sync_enabled
    showCompleteDialog.value = false
    completingTask.value = null
  } catch (e) {
    task.status = 'planned'
    showCompleteDialog.value = false
    completingTask.value = null
  }
}

function cancelComplete() {
  if (completingTask.value) {
    completingTask.value.status = 'planned'
  }
  showCompleteDialog.value = false
  completingTask.value = null
}

async function uncompleteTask(task: Task) {
  try {
    await api.updateTask(task.id, { status: 'planned' } as Partial<Task>)
    task.status = 'planned'
  } catch (e) {
    // ignore
  }
}

function handleCheckboxClick(task: Task) {
  if (task.status === 'completed') {
    uncompleteTask(task)
  } else {
    task.status = 'completed'
    openCompleteDialog(task)
  }
}

// Image upload
const imageInputRef = ref<HTMLInputElement | null>(null)
const uploadingImage = ref(false)

function triggerImageUpload() {
  imageInputRef.value?.click()
}

async function onImageSelected(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  uploadingImage.value = true
  try {
    const reader = new FileReader()
    reader.onload = async () => {
      const dataUrl = reader.result as string
      const result = await api.uploadImage(dataUrl)
      completionForm.value.achievement += `\n![image](${result.url})`
      uploadingImage.value = false
    }
    reader.onerror = () => { uploadingImage.value = false }
    reader.readAsDataURL(file)
  } catch (e) {
    uploadingImage.value = false
  }
  input.value = ''
}

// Status helpers
function getStatusLabel(status: string) {
  switch (status) {
    case 'planned': return '待开始'
    case 'in_progress': return '进行中'
    case 'completed': return '已完成'
    case 'cancelled': return '已取消'
    default: return ''
  }
}

function getStatusClass(status: string) {
  switch (status) {
    case 'planned': return 'status-planned'
    case 'in_progress': return 'status-progress'
    case 'completed': return 'status-done'
    case 'cancelled': return 'status-cancelled'
    default: return ''
  }
}

onMounted(() => {
  fetchTasks()
  fetchEnglish()
})

// English module summary
const english = ref<EnglishDashboard | null>(null)

const englishNewPct = computed(() =>
  english.value && english.value.new_goal > 0
    ? Math.min(100, Math.round((english.value.new_done / english.value.new_goal) * 100))
    : 0
)

async function fetchEnglish() {
  try {
    english.value = await englishApi.dashboard()
  } catch (e) {
    // ignore
  }
}

function goEnglish(name: string) {
  router.push({ name })
}
</script>

<template>
  <div class="home">
    <div class="today-overview">
      <div class="today-date">
        <span class="date-text">{{ dateLabel }}</span>
        <span class="weekday">{{ weekdayLabel }}</span>
      </div>
      <div class="progress-section">
        <div class="progress-header">
          <span class="progress-label">今日进度</span>
          <span class="progress-count">{{ progress.completed }}/{{ progress.total }}</span>
        </div>
        <div class="progress-bar">
          <div class="progress-fill" :style="{ width: progress.percent + '%' }"></div>
        </div>
      </div>
    </div>

    <div class="quick-plan-card">
      <div class="qp-row">
        <input v-model="quickTitle" placeholder="快速添加任务..." class="qp-input" @keyup.enter="quickPlan" />
        <button class="qp-btn" @click="quickPlan">添加</button>
      </div>
      <div class="qp-details">
        <input type="date" v-model="quickDate" class="qp-sm-input" />
        <input type="time" v-model="quickTime" class="qp-sm-input" />
      </div>
    </div>

    <div v-if="english" class="section-header">
      <h3>英语学习</h3>
      <button class="section-link" @click="goEnglish('english')">进入 →</button>
    </div>

    <div v-if="english" class="english-module">
      <div class="em-head">
        <div class="em-left">
          <span class="em-label">今日新词</span>
          <div class="em-num">{{ english.new_done }}<span>/{{ english.new_goal }}</span></div>
        </div>
        <div class="em-right">
          <span v-if="english.streak" class="em-streak">🔥 {{ english.streak }} 天</span>
          <span class="em-time">⏱ {{ formatDuration(english.today_seconds) }}</span>
        </div>
      </div>
      <div class="progress-bar">
        <div class="progress-fill" :style="{ width: englishNewPct + '%' }"></div>
      </div>
      <div class="em-stats">
        <div class="ems-item"><span class="ems-num">{{ english.total_words }}</span><span class="ems-label">总词数</span></div>
        <div class="ems-item"><span class="ems-num" style="color: var(--success)">{{ english.mastered_words }}</span><span class="ems-label">已掌握</span></div>
        <div class="ems-item"><span class="ems-num">{{ english.scenario_mastered }}/{{ english.scenario_count }}</span><span class="ems-label">场景掌握</span></div>
        <div class="ems-item"><span class="ems-num">{{ english.speaking_avg || '—' }}</span><span class="ems-label">口语均分</span></div>
      </div>
      <div class="em-actions">
        <button class="em-btn" @click="goEnglish('english-words')">🔤 背单词</button>
        <button class="em-btn" @click="goEnglish('english-scenarios')">💬 场景</button>
        <button class="em-btn" @click="goEnglish('english-speaking')">🎙️ 口语</button>
      </div>
    </div>

    <div class="section-header">
      <h3>今日任务</h3>
      <button class="section-link" @click="router.push('/plan')">查看全部 →</button>
    </div>

    <div v-if="loading" class="loading">加载中...</div>
    <div v-else-if="tasks.length === 0" class="empty">
      <p>还没有任务，添加一个开始吧！</p>
    </div>
    <div v-else class="task-list">
      <div
        v-for="task in tasks"
        :key="task.id"
        :class="['task-item', { 'is-done': task.status === 'completed' }]"
      >
        <label class="task-checkbox">
          <input
            type="checkbox"
            :checked="task.status === 'completed'"
            @click.prevent="handleCheckboxClick(task)"
          />
          <span class="checkmark"></span>
        </label>
        <div class="task-body" @click="router.push(`/plan?highlight=${task.id}`)">
          <span class="task-title">{{ task.title }}</span>
          <div class="task-meta">
            <span v-if="task.start_time" class="task-time">{{ task.start_time }}</span>
            <span :class="['task-status-badge', getStatusClass(task.status)]">{{ getStatusLabel(task.status) }}</span>
          </div>
        </div>
      </div>
    </div>

    <div class="quick-entries">
      <button class="entry-card review" @click="router.push('/review')">
        <span class="entry-icon">📊</span>
        <div class="entry-info">
          <span class="entry-title">今日复盘</span>
          <span class="entry-desc">回顾今日得失，记录改进</span>
        </div>
        <span class="entry-arrow">→</span>
      </button>
      <button class="entry-card achievement" @click="router.push('/achievements')">
        <span class="entry-icon">🏆</span>
        <div class="entry-info">
          <span class="entry-title">今日成果</span>
          <span class="entry-desc">记录完成的输出与成果</span>
        </div>
        <span class="entry-arrow">→</span>
      </button>
    </div>

    <div v-if="showCompleteDialog && completingTask" class="modal-overlay" @click.self="cancelComplete">
      <div class="modal">
        <h3>✅ 完成任务</h3>
        <p class="modal-task-title">{{ completingTask.title }}</p>
        <div class="form-group">
          <div class="label-row">
            <label>成果描述</label>
            <button class="upload-btn" :disabled="uploadingImage" @click="triggerImageUpload">
              {{ uploadingImage ? '上传中...' : '🖼 上传图片' }}
            </button>
          </div>
          <input ref="imageInputRef" type="file" accept="image/*" style="display:none" @change="onImageSelected" />
          <textarea v-model="completionForm.achievement" placeholder="记录完成了什么、有什么产出..." rows="3"></textarea>
        </div>
        <div class="form-group">
          <label>备注</label>
          <textarea v-model="completionForm.note" placeholder="补充说明..." rows="2"></textarea>
        </div>
        <div class="form-row">
          <div class="form-group">
            <label>实际开始</label>
            <input type="time" v-model="completionForm.actual_start" />
          </div>
          <div class="form-group">
            <label>实际结束</label>
            <input type="time" v-model="completionForm.actual_end" />
          </div>
          <div class="form-group">
            <label>时长(分钟)</label>
            <input type="number" v-model.number="completionForm.actual_duration" placeholder="如: 30" min="0" />
          </div>
        </div>
        <div class="form-group">
          <label class="toggle-row">
            <input type="checkbox" v-model="completionForm.sync_enabled" />
            <span class="toggle-track">
              <span class="toggle-thumb"></span>
            </span>
            <span class="toggle-label-text">同步到知识库 (Obsidian)</span>
          </label>
        </div>
        <div class="form-actions">
          <button class="btn" @click="cancelComplete">取消</button>
          <button class="btn btn-primary" @click="confirmComplete">确认完成</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.home {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding-top: 0;
}

.today-overview {
  background: var(--card);
  border-radius: var(--radius);
  padding: 20px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
}

.today-date {
  display: flex;
  align-items: baseline;
  gap: 10px;
  margin-bottom: 16px;
}

.date-text {
  font-size: 22px;
  font-weight: 700;
  color: var(--text);
}

.weekday {
  font-size: 14px;
  color: var(--text-secondary);
}

.progress-section {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.progress-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.progress-label {
  font-size: 13px;
  color: var(--text-secondary);
}

.progress-count {
  font-size: 13px;
  font-weight: 600;
  color: var(--primary);
}

.progress-bar {
  height: 8px;
  background: var(--border);
  border-radius: 4px;
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background: linear-gradient(90deg, var(--primary), var(--primary-light));
  border-radius: 4px;
  transition: width 0.5s ease;
}

.quick-plan-card {
  background: var(--card);
  border-radius: var(--radius);
  padding: 16px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
  border: 2px dashed var(--primary-light);
}

.qp-row {
  display: flex;
  gap: 8px;
}

.qp-input {
  flex: 1;
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: 8px;
  font-size: 14px;
  outline: none;
}

.qp-input:focus {
  border-color: var(--primary);
}

.qp-btn {
  padding: 10px 18px;
  background: var(--primary);
  color: white;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  white-space: nowrap;
}

.qp-btn:hover {
  background: #4338ca;
}

.qp-details {
  display: flex;
  gap: 8px;
  margin-top: 8px;
}

.qp-sm-input {
  flex: 1;
  padding: 8px 10px;
  border: 1px solid var(--border);
  border-radius: 6px;
  font-size: 13px;
  outline: none;
}

.qp-sm-input:focus {
  border-color: var(--primary);
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.section-header h3 {
  font-size: 16px;
  font-weight: 700;
}

.section-link {
  font-size: 13px;
  color: var(--primary);
  background: none;
  border: none;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 6px;
}

.section-link:hover {
  background: #eef2ff;
}

.loading {
  text-align: center;
  padding: 32px;
  color: var(--text-secondary);
  font-size: 14px;
}

.empty {
  text-align: center;
  padding: 32px;
  color: var(--text-secondary);
  font-size: 14px;
  background: var(--card);
  border-radius: var(--radius);
  border: 1px dashed var(--border);
}

.task-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.task-item {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  background: var(--card);
  border-radius: var(--radius);
  padding: 14px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.06);
  transition: all 0.2s;
}

.task-item:hover {
  box-shadow: 0 2px 6px rgba(0,0,0,0.1);
}

.task-item.is-done {
  opacity: 0.6;
}

.task-checkbox {
  position: relative;
  width: 22px;
  height: 22px;
  flex-shrink: 0;
  margin-top: 1px;
  cursor: pointer;
}

.task-checkbox input {
  position: absolute;
  opacity: 0;
  width: 0;
  height: 0;
}

.checkmark {
  display: block;
  width: 22px;
  height: 22px;
  border: 2px solid var(--border);
  border-radius: 6px;
  transition: all 0.2s;
}

.task-checkbox input:checked + .checkmark {
  background: var(--primary);
  border-color: var(--primary);
  background-image: url("data:image/svg+xml,%3Csvg viewBox='0 0 16 16' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath d='M12.207 4.793a1 1 0 010 1.414l-5 5a1 1 0 01-1.414 0l-2-2a1 1 0 011.414-1.414L6.5 9.086l4.293-4.293a1 1 0 011.414 0z' fill='white'/%3E%3C/svg%3E");
  background-size: 14px;
  background-position: center;
  background-repeat: no-repeat;
}

.task-body {
  flex: 1;
  cursor: pointer;
  min-width: 0;
}

.task-title {
  font-size: 15px;
  font-weight: 500;
  display: block;
  line-height: 1.4;
}

.task-item.is-done .task-title {
  text-decoration: line-through;
}

.task-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 4px;
}

.task-time {
  font-size: 12px;
  color: var(--text-secondary);
}

.task-status-badge {
  font-size: 11px;
  padding: 1px 6px;
  border-radius: 4px;
  font-weight: 500;
}

.status-planned {
  background: #eef2ff;
  color: var(--primary);
}

.status-progress {
  background: #fef3c7;
  color: #92400e;
}

.status-done {
  background: #ecfdf5;
  color: var(--success);
}

.status-cancelled {
  background: #fef2f2;
  color: var(--danger);
}

.quick-entries {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.entry-card {
  display: flex;
  align-items: center;
  gap: 14px;
  background: var(--card);
  border: 1.5px solid var(--border);
  border-radius: var(--radius);
  padding: 16px;
  cursor: pointer;
  transition: all 0.2s;
  text-align: left;
  width: 100%;
}

.entry-card:hover {
  transform: translateY(-1px);
  box-shadow: 0 3px 10px rgba(0,0,0,0.08);
}

.entry-card.review:hover {
  border-color: var(--warning);
}

.entry-card.achievement:hover {
  border-color: #f59e0b;
}

.entry-icon {
  font-size: 28px;
  flex-shrink: 0;
}

.entry-info {
  flex: 1;
  min-width: 0;
}

.entry-title {
  font-size: 15px;
  font-weight: 600;
  display: block;
}

.entry-desc {
  font-size: 12px;
  color: var(--text-secondary);
  margin-top: 2px;
}

.entry-arrow {
  font-size: 18px;
  color: var(--text-secondary);
  flex-shrink: 0;
}

.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,0.4);
  display: flex;
  align-items: flex-end;
  z-index: 200;
}

.modal {
  background: var(--card);
  width: 100%;
  max-width: 480px;
  margin: 0 auto;
  border-radius: 16px 16px 0 0;
  padding: 20px;
  max-height: 80vh;
  overflow-y: auto;
}

.modal h3 {
  font-size: 18px;
  margin-bottom: 4px;
}

.modal-task-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--text);
  margin-bottom: 16px;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--border);
}

.form-group {
  margin-bottom: 14px;
}

.form-group label {
  display: block;
  font-size: 12px;
  font-weight: 600;
  color: var(--text-secondary);
  margin-bottom: 4px;
}

.label-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 4px;
}

.label-row label {
  margin-bottom: 0;
}

.upload-btn {
  font-size: 12px;
  padding: 4px 10px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--card);
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.2s;
}

.upload-btn:hover:not(:disabled) {
  border-color: var(--primary);
  color: var(--primary);
}

.upload-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.form-group textarea,
.form-group input[type="time"],
.form-group input[type="number"] {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: 8px;
  font-size: 14px;
  outline: none;
  font-family: inherit;
}

.form-group textarea:focus,
.form-group input:focus {
  border-color: var(--primary);
}

.form-group textarea {
  resize: vertical;
}

.form-row {
  display: flex;
  gap: 10px;
}

.form-row .form-group {
  flex: 1;
}

.toggle-row {
  display: flex !important;
  align-items: center;
  gap: 10px;
  cursor: pointer;
  padding: 8px 0;
}

.toggle-row input {
  position: absolute;
  opacity: 0;
  width: 0;
  height: 0;
}

.toggle-track {
  width: 40px;
  height: 22px;
  background: var(--border);
  border-radius: 11px;
  position: relative;
  transition: background 0.2s;
  flex-shrink: 0;
}

.toggle-row input:checked + .toggle-track {
  background: var(--primary);
}

.toggle-thumb {
  position: absolute;
  top: 2px;
  left: 2px;
  width: 18px;
  height: 18px;
  background: white;
  border-radius: 50%;
  transition: transform 0.2s;
  box-shadow: 0 1px 3px rgba(0,0,0,0.2);
}

.toggle-row input:checked + .toggle-track .toggle-thumb {
  transform: translateX(18px);
}

.toggle-label-text {
  font-size: 13px;
  color: var(--text);
  font-weight: 400;
}

.form-actions {
  display: flex;
  gap: 10px;
  margin-top: 8px;
}

.form-actions .btn {
  flex: 1;
  padding: 10px 20px;
  border: 1px solid var(--border);
  border-radius: 8px;
  font-size: 14px;
  cursor: pointer;
  background: var(--card);
  color: var(--text);
  transition: all 0.2s;
}

.form-actions .btn-primary {
  background: var(--primary);
  color: white;
  border-color: var(--primary);
}

.english-module {
  background: var(--card);
  border-radius: var(--radius);
  padding: 16px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.06);
  border: 1.5px solid var(--primary-light);
  margin-bottom: 20px;
}
.em-head { display: flex; align-items: flex-start; justify-content: space-between; margin-bottom: 10px; }
.em-label { font-size: 12px; color: var(--text-secondary); }
.em-num { font-size: 24px; font-weight: 800; color: var(--primary); }
.em-num span { font-size: 13px; color: var(--text-secondary); }
.em-right { display: flex; flex-direction: column; align-items: flex-end; gap: 4px; }
.em-streak { font-size: 12px; font-weight: 600; color: #92400e; background: #fef3c7; padding: 2px 8px; border-radius: 999px; }
.em-time { font-size: 12px; color: var(--text-secondary); }
.em-stats { display: flex; margin-top: 12px; }
.ems-item { flex: 1; text-align: center; }
.ems-num { display: block; font-size: 17px; font-weight: 700; color: var(--primary); }
.ems-label { display: block; font-size: 11px; color: var(--text-secondary); margin-top: 2px; }
.em-actions { display: flex; gap: 8px; margin-top: 12px; }
.em-btn { flex: 1; padding: 9px; border: 1px solid var(--border); border-radius: 8px; background: var(--card); color: var(--text); font-size: 13px; font-weight: 600; cursor: pointer; transition: all 0.15s; }
.em-btn:hover { border-color: var(--primary); color: var(--primary); background: #eef2ff; }
</style>
