<script setup lang="ts">
import { ref, onMounted, inject, computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api } from '../api'
import type { Task, Note } from '../types'
import { formatDate, renderContent } from '../utils/format'

const route = useRoute()
const router = useRouter()
const today = inject<string>('today')!
const selectedDate = ref((route.query as any).date || today)
const tasks = ref<Task[]>([])
const showForm = ref(false)
const editingTask = ref<Task | null>(null)
const showCompleteDialog = ref(false)
const completingTask = ref<Task | null>(null)

const notes = ref<Note[]>([])
const selectedNoteId = ref<number | null>(null)
const taskCategories = ref<string[]>([])

const form = ref({
  title: '',
  date: selectedDate.value,
  start_time: '',
  end_time: '',
  planned_duration: 30,
  category: '',
  priority: 2 as 1 | 2 | 3,
  note: '',
  is_recurring: false,
  is_planned: true,
  note_id: null as number | null,
  sync_enabled: true,
  planned_days: 1,
})

const completeForm = ref({
  actual_start: '',
  actual_end: '',
  actual_duration: 0,
  achievement: '',
  sync_enabled: true,
  overall_completed: false,
})

const uploading = ref(false)
const summaryForm = ref({ content: '' })
const loadingSummary = ref(false)

async function insertAchievementImage() {
  const input = document.createElement('input')
  input.type = 'file'
  input.accept = 'image/*'
  input.onchange = async () => {
    const file = input.files?.[0]
    if (!file) return
    uploading.value = true
    const reader = new FileReader()
    reader.onload = async (e) => {
      const dataUrl = e.target?.result as string
      try {
        const result = await api.uploadImage(dataUrl)
        completeForm.value.achievement += `\n![${file.name}](${result.url})\n`
      } catch (err: any) {
        alert('图片上传失败: ' + err.message)
      } finally {
        uploading.value = false
      }
    }
    reader.readAsDataURL(file)
  }
  input.click()
}

function calcDuration(start: string, end: string): number {
  if (!start || !end) return 0
  const [sh, sm] = start.split(':').map(Number)
  const [eh, em] = end.split(':').map(Number)
  const duration = (eh * 60 + em) - (sh * 60 + sm)
  return duration < 0 ? duration + 24 * 60 : duration
}

function getPlannedDuration(task: Task): number {
  const duration = calcDuration(task.start_time, task.end_time)
  return duration > 0 ? duration : task.planned_duration
}

watch(selectedDate, () => {
  loadTasks()
})

watch(() => [form.value.start_time, form.value.end_time], ([s, e]) => {
  const d = calcDuration(s, e)
  if (d > 0) form.value.planned_duration = d
})

watch(() => [completeForm.value.actual_start, completeForm.value.actual_end], ([s, e]) => {
  const d = calcDuration(s, e)
  if (d > 0) completeForm.value.actual_duration = d
})

const plannedTasks = computed(() => tasks.value.filter(t => t.is_planned))
const unplannedTasks = computed(() => tasks.value.filter(t => !t.is_planned))

onMounted(async () => {
  if (selectedDate.value === today) {
    await api.generateRecurringTasks(today)
  }
  await Promise.all([loadTasks(), loadNotes(), loadTaskCategories()])
  const query = route.query as any
  if (query.title) {
    form.value.title = query.title as string
    form.value.date = selectedDate.value
    if (query.time) {
      form.value.start_time = query.time as string
    }
    showForm.value = true
  }
})

async function loadTasks() {
  tasks.value = await api.getTasks(selectedDate.value)
}

async function loadNotes() {
  notes.value = await api.getNotes()
}

async function loadTaskCategories() {
  const allTasks = await api.getTasks()
  const cats = new Set(allTasks.map(t => t.category).filter(Boolean))
  taskCategories.value = Array.from(cats).sort()
}

function openNew(isPlanned: boolean) {
  editingTask.value = null
  form.value = { title: '', date: selectedDate.value, start_time: '', end_time: '', planned_duration: 30, category: '', priority: 2, note: '', is_recurring: false, is_planned: isPlanned, note_id: null, sync_enabled: true, planned_days: 1 }
  selectedNoteId.value = null
  showForm.value = true
}

function openEdit(task: Task) {
  editingTask.value = task
  form.value = {
    title: task.title,
    date: task.date,
    start_time: task.start_time,
    end_time: task.end_time,
    planned_duration: task.planned_duration,
    category: task.category,
    priority: task.priority,
    note: task.note,
    is_recurring: task.is_recurring,
    is_planned: task.is_planned,
    note_id: task.note_id,
    sync_enabled: task.sync_enabled !== false,
    planned_days: task.planned_days || 1,
  }
  selectedNoteId.value = task.note_id
  showForm.value = true
}

async function saveTask() {
  if (!form.value.title.trim()) return
  const data = { ...form.value, note_id: selectedNoteId.value }
  if (editingTask.value) {
    await api.updateTask(editingTask.value.id, data)
  } else {
    await api.createTask(data)
  }
  showForm.value = false
  await loadTasks()
}

async function updateStatus(task: Task, status: Task['status']) {
  if (status === 'completed') {
    completingTask.value = task
    completeForm.value = {
      actual_start: task.actual_start || task.start_time || '',
      actual_end: task.actual_end || '',
      actual_duration: task.actual_duration || task.planned_duration,
      achievement: task.achievement || '',
      sync_enabled: task.sync_enabled !== false,
      overall_completed: task.overall_status === 'completed',
    }
    summaryForm.value = { content: '' }
    loadingSummary.value = true
    try {
      const existing = await api.getTaskSummary(task.title)
      if (existing) {
        summaryForm.value.content = existing.content
      }
    } catch { }
    loadingSummary.value = false
    showCompleteDialog.value = true
    return
  }
  await api.updateTask(task.id, { status })
  await loadTasks()
}

async function confirmComplete() {
  if (!completingTask.value) return
  const d = calcDuration(completeForm.value.actual_start, completeForm.value.actual_end)
  const updateData: Record<string, any> = {
    status: 'completed',
    actual_start: completeForm.value.actual_start || null,
    actual_end: completeForm.value.actual_end || null,
    actual_duration: d > 0 ? d : completeForm.value.actual_duration,
    achievement: completeForm.value.achievement,
    sync_enabled: completeForm.value.sync_enabled,
  }
  if (completeForm.value.overall_completed) {
    updateData.overall_status = 'completed'
  }
  await api.updateTask(completingTask.value.id, updateData)
  if (summaryForm.value.content.trim()) {
    await api.saveTaskSummary(completingTask.value.title, summaryForm.value.content)
  }
  if (completeForm.value.overall_completed) {
    const templates = await api.getRecurringTemplates()
    const tmpl = templates.find(t => t.title === completingTask.value!.title)
    if (tmpl) {
      const allTasks = await api.getTasks()
      const templateTasks = allTasks.filter(t => t.title === tmpl.title && t.status === 'completed')
      const actualDays = new Set(templateTasks.map(t => t.date)).size
      if (actualDays > 0 && actualDays < tmpl.planned_days) {
        await api.updateRecurringTemplate(tmpl.id, { planned_days: actualDays })
      }
    }
  }
  showCompleteDialog.value = false
  completingTask.value = null
  await loadTasks()
}

async function copyTask(task: Task) {
  try {
    await api.copyTask(task.id, selectedDate.value)
  } catch (e: any) {
    alert('复制失败: ' + (e.message || e))
    return
  }
  await loadTasks()
}

async function deleteTask(id: number) {
  if (!confirm('确定删除此任务？')) return
  await api.deleteTask(id)
  await loadTasks()
}

async function deleteTasksByName(title: string) {
  if (!confirm(`确定删除所有名为「${title}」的任务？此操作不可撤销！`)) return
  try {
    const result = await api.deleteTasksByName(title)
    alert(`已删除 ${result.count} 个任务`)
    await loadTasks()
  } catch (e: any) {
    alert('删除失败: ' + (e.message || e))
  }
}

function changeDate(delta: number) {
  const d = new Date(selectedDate.value + 'T00:00:00')
  d.setDate(d.getDate() + delta)
  selectedDate.value = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function formatDuration(min: number): string {
  if (!min) return ''
  if (min < 60) return `${min}分钟`
  return `${Math.floor(min / 60)}小时${min % 60 ? min % 60 + '分钟' : ''}`
}
</script>

<template>
  <div class="daily-plan">
    <div class="section-header">
      <div class="date-nav">
        <button class="btn-icon" @click="changeDate(-1)" title="前一天">‹</button>
        <input type="date" v-model="selectedDate" class="date-input" />
        <button class="btn-icon" @click="changeDate(1)" title="后一天">›</button>
        <button v-if="selectedDate !== today" class="btn-today" @click="selectedDate = today">今天</button>
      </div>
      <div class="header-actions">
        <button class="btn btn-outline" @click="openNew(false)">+ 计划外</button>
        <button class="btn btn-primary" @click="openNew(true)">+ 计划</button>
      </div>
    </div>

    <div v-if="tasks.length === 0" class="empty">
      <p>{{ selectedDate === today ? '今天还没有计划，添加一个任务开始吧' : formatDate(selectedDate) + ' 还没有计划' }}</p>
    </div>

    <div v-if="plannedTasks.length > 0" class="task-section">
      <h3 class="section-title">计划内 ({{ plannedTasks.length }})</h3>
      <div v-for="task in plannedTasks" :key="task.id" :class="['task-card', task.status]">
        <div class="task-header">
          <span :class="['priority-dot', 'p' + task.priority]"></span>
          <span v-if="task.start_time || task.end_time" class="time-badge">
            {{ task.start_time || '??' }}-{{ task.end_time || '??' }}
          </span>
          <span class="task-title" :class="{ done: task.status === 'completed' }">{{ task.title }}</span>
          <span v-if="task.category" class="category-tag">{{ task.category }}</span>
          <span v-if="task.is_recurring" class="badge-recurring" title="循环任务">🔄</span>
          <span v-if="task.sync_enabled === false" class="badge-nosync" title="不同步到知识库">🚫</span>
          <span v-if="task.overall_status === 'completed'" class="badge-overall" title="整体已完成">✅</span>
        </div>
        <div class="task-meta">
          <span>计划: {{ formatDuration(getPlannedDuration(task)) }}</span>
          <span v-if="task.actual_duration">实际: {{ formatDuration(task.actual_duration) }}</span>
          <span v-if="task.note_id" class="linked-note" title="关联备忘录">📝 已关联</span>
          <span v-if="task.sync_enabled === false" class="nosync-tag">不同步</span>
        </div>
        <div v-if="task.note" class="task-note">{{ task.note }}</div>
        <div v-if="task.achievement" class="task-achievement" v-html="renderContent(task.achievement.slice(0, 100) + (task.achievement.length > 100 ? '...' : ''))"></div>
        <div class="task-actions">
          <select :value="task.status" @change="updateStatus(task, ($event.target as HTMLSelectElement).value as any)">
            <option value="planned">计划中</option>
            <option value="in_progress">进行中</option>
            <option value="completed">已完成</option>
            <option value="cancelled">已取消</option>
          </select>
          <button class="btn-sm" @click="copyTask(task)">复制</button>
          <button class="btn-sm" @click="openEdit(task)">编辑</button>
          <button class="btn-sm btn-danger" @click="deleteTask(task.id)" :disabled="task.status === 'completed'">删除</button>
          <button class="btn-sm btn-outline-danger" @click="deleteTasksByName(task.title)" :disabled="task.status === 'completed'">删除同名</button>
        </div>
      </div>
    </div>

    <div v-if="unplannedTasks.length > 0" class="task-section">
      <h3 class="section-title">⚡ 计划外</h3>
      <div v-for="task in unplannedTasks" :key="task.id" :class="['task-card', 'task-unplanned', task.status]">
        <div class="task-header">
          <span class="task-title" :class="{ done: task.status === 'completed' }">{{ task.title }}</span>
          <span v-if="task.category" class="category-tag">{{ task.category }}</span>
          <span v-if="task.sync_enabled === false" class="badge-nosync" title="不同步到知识库">🚫</span>
          <span v-if="task.overall_status === 'completed'" class="badge-overall" title="整体已完成">✅</span>
        </div>
        <div class="task-meta">
          <span v-if="task.actual_duration">实际: {{ formatDuration(task.actual_duration) }}</span>
          <span v-if="task.sync_enabled === false" class="nosync-tag">不同步</span>
        </div>
        <div v-if="task.note" class="task-note">{{ task.note }}</div>
        <div v-if="task.achievement" class="task-achievement" v-html="renderContent(task.achievement.slice(0, 100) + (task.achievement.length > 100 ? '...' : ''))"></div>
        <div class="task-actions">
          <select :value="task.status" @change="updateStatus(task, ($event.target as HTMLSelectElement).value as any)">
            <option value="planned">计划中</option>
            <option value="in_progress">进行中</option>
            <option value="completed">已完成</option>
            <option value="cancelled">已取消</option>
          </select>
          <button class="btn-sm" @click="copyTask(task)">复制</button>
          <button class="btn-sm" @click="openEdit(task)">编辑</button>
          <button class="btn-sm btn-danger" @click="deleteTask(task.id)" :disabled="task.status === 'completed'">删除</button>
        </div>
      </div>
    </div>

    <div v-if="showForm" class="modal-overlay" @click.self="showForm = false">
      <div class="modal">
        <h3>{{ editingTask ? '编辑任务' : '添加任务' }}</h3>
        <div class="form-group">
          <label>任务名称</label>
          <input v-model="form.title" placeholder="输入任务名称" />
        </div>
        <div class="form-group">
          <label>日期</label>
          <input type="date" v-model="form.date" />
        </div>
        <div class="form-row">
          <div class="form-group">
            <label>开始时间</label>
            <input type="time" v-model="form.start_time" />
          </div>
          <div class="form-group">
            <label>结束时间</label>
            <input type="time" v-model="form.end_time" />
          </div>
        </div>
        <div class="form-group">
          <label>计划时长（分钟）</label>
          <input type="number" v-model.number="form.planned_duration" min="0" />
        </div>
        <div class="form-group">
          <label>计划完成天数</label>
          <input type="number" v-model.number="form.planned_days" min="1" />
        </div>
        <div class="form-group">
          <label>分类</label>
          <input v-model="form.category" placeholder="如：工作、学习、运动" list="task-cat-list" />
          <datalist id="task-cat-list">
            <option v-for="c in taskCategories" :key="c" :value="c" />
          </datalist>
        </div>
        <div class="form-group">
          <label>优先级</label>
          <select v-model.number="form.priority">
            <option :value="1">高</option>
            <option :value="2">中</option>
            <option :value="3">低</option>
          </select>
        </div>
        <div class="form-group">
          <label>备注</label>
          <textarea v-model="form.note" placeholder="备注信息" rows="2"></textarea>
        </div>
        <div class="form-group">
          <label>关联备忘录</label>
          <select v-model.number="selectedNoteId">
            <option :value="null">无</option>
            <option v-for="n in notes" :key="n.id" :value="n.id">{{ n.title }}</option>
          </select>
        </div>
        <div class="form-row">
          <label class="checkbox-label">
            <input type="checkbox" v-model="form.is_recurring" />
            <span>循环任务（每天自动生成）</span>
          </label>
          <label class="checkbox-label">
            <input type="checkbox" v-model="form.is_planned" />
            <span>计划内任务</span>
          </label>
        </div>
        <div class="form-group">
          <label class="toggle-row">
            <span>同步到知识库</span>
            <label class="toggle-label">
              <input type="checkbox" v-model="form.sync_enabled" />
              <span class="toggle-slider"></span>
            </label>
          </label>
        </div>
        <div class="form-actions">
          <button class="btn" @click="showForm = false">取消</button>
          <button class="btn btn-primary" @click="saveTask">保存</button>
        </div>
      </div>
    </div>

    <div v-if="showCompleteDialog" class="modal-overlay" @click.self="showCompleteDialog = false">
      <div class="modal">
        <h3>完成任务</h3>
        <p class="complete-title">{{ completingTask?.title }}</p>
        <div class="form-row">
          <div class="form-group">
            <label>实际开始时间</label>
            <input type="time" v-model="completeForm.actual_start" />
          </div>
          <div class="form-group">
            <label>实际结束时间</label>
            <input type="time" v-model="completeForm.actual_end" />
          </div>
        </div>
        <div class="form-group">
          <label>实际时长（分钟）</label>
          <input type="number" v-model.number="completeForm.actual_duration" min="0" />
        </div>
        <div class="form-group">
          <label>成果记录 <span class="text-muted">（可选，支持文字和图片）</span></label>
          <div class="editor-toolbar">
            <button class="toolbar-btn" @click="insertAchievementImage" :disabled="uploading" title="插入图片">{{ uploading ? '⏳ 上传中...' : '🖼️ 图片' }}</button>
          </div>
          <textarea v-model="completeForm.achievement" placeholder="记录本次任务的成果、输出、收获...&#10;如：阅读了哪些章节、完成了什么输出、有什么心得" rows="5" maxlength="5000"></textarea>
          <span class="char-count">{{ completeForm.achievement.length }}/5000</span>
        </div>
        <div class="form-group">
          <label>任务总结 <span class="text-muted">（跨天任务的整体总结，可选）</span></label>
          <textarea v-model="summaryForm.content" placeholder="对于跨多天的任务，在此记录整体总结、心得、收获..." rows="4" maxlength="5000" :disabled="loadingSummary"></textarea>
          <span class="char-count">{{ summaryForm.content.length }}/5000</span>
        </div>
        <div class="form-group">
          <label class="toggle-row">
            <span>整体任务已完成</span>
            <label class="toggle-label">
              <input type="checkbox" v-model="completeForm.overall_completed" />
              <span class="toggle-slider"></span>
            </label>
          </label>
          <span class="text-muted" style="font-size:12px">标记此任务整体完成，将调整模板的计划天数为已实际完成天数</span>
        </div>
        <div class="form-group">
          <label class="toggle-row">
            <span>同步到知识库</span>
            <label class="toggle-label">
              <input type="checkbox" v-model="completeForm.sync_enabled" />
              <span class="toggle-slider"></span>
            </label>
          </label>
        </div>
        <div class="form-actions">
          <button class="btn" @click="showCompleteDialog = false">取消</button>
          <button class="btn btn-primary" @click="confirmComplete">确认完成</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.section-header h2 {
  font-size: 16px;
  color: var(--text-secondary);
  font-weight: 500;
}

.header-actions {
  display: flex;
  gap: 8px;
}

.task-section {
  margin-bottom: 16px;
}

.section-title {
  font-size: 14px;
  color: var(--text-secondary);
  margin-bottom: 8px;
  font-weight: 600;
}

.task-card {
  background: var(--card);
  border-radius: var(--radius);
  padding: 12px 16px;
  margin-bottom: 10px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
  border-left: 4px solid var(--primary-light);
}

.task-card.completed {
  border-left-color: var(--success);
  opacity: 0.7;
}

.task-card.cancelled {
  border-left-color: var(--danger);
  opacity: 0.5;
}

.task-card.in_progress {
  border-left-color: var(--warning);
}

.task-card.task-unplanned {
  border-left-color: var(--warning);
  border-style: dashed;
}

.task-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
  flex-wrap: wrap;
}

.priority-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.p1 { background: var(--danger); }
.p2 { background: var(--warning); }
.p3 { background: var(--success); }

.time-badge {
  font-size: 12px;
  background: var(--bg);
  padding: 2px 8px;
  border-radius: 6px;
  color: var(--text-secondary);
  font-family: monospace;
  white-space: nowrap;
}

.task-title {
  flex: 1;
  font-size: 15px;
  font-weight: 500;
}

.task-title.done {
  text-decoration: line-through;
  color: var(--text-secondary);
}

.category-tag {
  font-size: 11px;
  background: var(--bg);
  padding: 2px 8px;
  border-radius: 10px;
  color: var(--text-secondary);
}

.badge-recurring {
  font-size: 14px;
  cursor: help;
}

.badge-nosync {
  font-size: 14px;
  cursor: help;
}

.badge-overall {
  font-size: 14px;
  cursor: help;
}

.nosync-tag {
  font-size: 11px;
  background: var(--danger);
  color: white;
  padding: 1px 6px;
  border-radius: 6px;
}

.task-meta {
  font-size: 12px;
  color: var(--text-secondary);
  margin-bottom: 8px;
  display: flex;
  gap: 12px;
}

.task-note {
  margin: 0 0 10px;
  padding: 8px 10px;
  border-radius: 8px;
  background: var(--bg);
  color: var(--text-secondary);
  font-size: 13px;
  line-height: 1.55;
  white-space: pre-line;
}

.task-actions {
  display: flex;
  gap: 8px;
  align-items: center;
}

.task-actions select {
  font-size: 12px;
  padding: 4px 8px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: white;
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
  margin-bottom: 16px;
}

.complete-title {
  font-size: 15px;
  font-weight: 500;
  margin-bottom: 16px;
  color: var(--text);
}

.form-group {
  margin-bottom: 12px;
}

.form-group label {
  display: block;
  font-size: 13px;
  color: var(--text-secondary);
  margin-bottom: 4px;
}

.form-group input,
.form-group select,
.form-group textarea {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: 8px;
  font-size: 14px;
  outline: none;
  transition: border-color 0.2s;
}

.form-group input:focus,
.form-group select:focus,
.form-group textarea:focus {
  border-color: var(--primary);
}

.form-row {
  display: flex;
  gap: 10px;
}

.form-row .form-group {
  flex: 1;
}

.form-actions {
  display: flex;
  gap: 10px;
  margin-top: 16px;
}

.form-actions .btn {
  flex: 1;
}

.empty {
  text-align: center;
  padding: 60px 20px;
  color: var(--text-secondary);
  font-size: 14px;
}

.btn {
  padding: 10px 20px;
  border: 1px solid var(--border);
  border-radius: 8px;
  font-size: 14px;
  cursor: pointer;
  background: var(--card);
  color: var(--text);
  transition: all 0.2s;
}

.btn-primary {
  background: var(--primary);
  color: white;
  border-color: var(--primary);
}

.btn-outline {
  background: transparent;
  color: var(--warning);
  border-color: var(--warning);
}

.btn-sm {
  padding: 4px 10px;
  font-size: 12px;
  border: 1px solid var(--border);
  border-radius: 6px;
  cursor: pointer;
  background: var(--card);
  color: var(--text);
}

.btn-sm:disabled {
  opacity: 0.4;
  cursor: not-allowed;
  pointer-events: none;
}

.btn-danger {
  color: var(--danger);
  border-color: var(--danger);
}

.task-actions select {
  font-size: 12px;
  padding: 4px 8px;
  border: 1px solid var(--border);
  border-radius: 6px;
}

.char-count {
  display: block;
  text-align: right;
  font-size: 11px;
  color: var(--text-secondary);
  margin-top: 4px;
}

.text-muted {
  font-weight: normal;
  color: var(--text-secondary);
  font-size: 12px;
}

.date-nav {
  display: flex;
  align-items: center;
  gap: 6px;
}

.date-input {
  font-size: 15px;
  padding: 4px 8px;
  border: 1px solid var(--border);
  border-radius: 8px;
  outline: none;
  background: var(--card);
  font-family: inherit;
}

.btn-icon {
  width: 32px;
  height: 32px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--card);
  font-size: 18px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  line-height: 1;
  color: var(--text);
}

.btn-today {
  font-size: 12px;
  padding: 4px 10px;
  border: 1px solid var(--primary);
  border-radius: 8px;
  background: transparent;
  color: var(--primary);
  cursor: pointer;
  white-space: nowrap;
}

.section-header {
  flex-wrap: wrap;
  gap: 10px;
}

.editor-toolbar {
  display: flex;
  gap: 6px;
  margin-bottom: 6px;
}

.toolbar-btn {
  font-size: 13px;
  padding: 4px 12px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--card);
  color: var(--text);
  cursor: pointer;
}

.toolbar-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.task-achievement {
  margin: 8px 0 4px;
  padding: 6px 10px;
  background: var(--bg);
  border-radius: 6px;
  font-size: 13px;
  color: var(--text-secondary);
  line-height: 1.5;
}

.toggle-label {
  position: relative;
  display: inline-block;
  width: 36px;
  height: 20px;
  cursor: pointer;
}
.toggle-label input {
  display: none;
}
.toggle-slider {
  position: absolute;
  top: 0; left: 0; right: 0; bottom: 0;
  background: #ccc;
  border-radius: 20px;
  transition: 0.3s;
}
.toggle-slider::before {
  content: '';
  position: absolute;
  width: 16px;
  height: 16px;
  left: 2px;
  bottom: 2px;
  background: white;
  border-radius: 50%;
  transition: 0.3s;
}
.toggle-label input:checked + .toggle-slider {
  background: var(--primary);
}
.toggle-label input:checked + .toggle-slider::before {
  transform: translateX(16px);
}
.toggle-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.toggle-row span {
  font-size: 13px;
  color: var(--text-secondary);
}
</style>
