<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { api } from '../api'
import type { RecurringTemplate } from '../types'

const templates = ref<RecurringTemplate[]>([])
const dailyTemplates = computed(() => templates.value.filter(t => t.recurrence_type !== 'weekly'))
const weeklyTemplates = computed(() => templates.value.filter(t => t.recurrence_type === 'weekly'))
const showForm = ref(false)
const editingId = ref<number | null>(null)
const form = ref({
  title: '',
  start_time: '',
  end_time: '',
  planned_duration: 30,
  category: '',
  priority: 2,
  note: '',
  recurrence_type: 'daily',
  recurrence_days: '',
  recurring_enabled: true,
  sync_enabled: true,
  planned_days: 1,
})

const weekDays = ['周日', '周一', '周二', '周三', '周四', '周五', '周六']

function formatWeekDays(days: string) {
  if (!days) return ''
  return days.split(',').map(d => weekDays[Number(d)]).filter(Boolean).join('、')
}

onMounted(async () => {
  await loadTemplates()
})

async function loadTemplates() {
  templates.value = await api.getRecurringTemplates()
}

function openCreate() {
  editingId.value = null
  form.value = { title: '', start_time: '', end_time: '', planned_duration: 30, category: '', priority: 2, note: '', recurrence_type: 'daily', recurrence_days: '', recurring_enabled: true, sync_enabled: true, planned_days: 1 }
  showForm.value = true
}

function openEdit(t: RecurringTemplate) {
  editingId.value = t.id
  form.value = {
    title: t.title,
    start_time: t.start_time,
    end_time: t.end_time,
    planned_duration: t.planned_duration,
    category: t.category,
    priority: t.priority,
    note: t.note,
    recurrence_type: t.recurrence_type || 'daily',
    recurrence_days: t.recurrence_days || '',
    recurring_enabled: t.recurring_enabled,
    sync_enabled: t.sync_enabled !== false,
    planned_days: t.planned_days || 1,
  }
  showForm.value = true
}

async function save() {
  if (!form.value.title.trim()) return
  try {
    if (editingId.value) {
      await api.updateRecurringTemplate(editingId.value, form.value)
    } else {
      await api.createRecurringTemplate(form.value)
    }
    showForm.value = false
    await loadTemplates()
  } catch (e: any) {
    alert('保存失败: ' + (e.message || e))
  }
}

function toggleDay(i: number) {
  const days = form.value.recurrence_days ? form.value.recurrence_days.split(',').filter(Boolean) : []
  const idx = days.indexOf(String(i))
  if (idx >= 0) days.splice(idx, 1)
  else days.push(String(i))
  days.sort((a, b) => Number(a) - Number(b))
  form.value.recurrence_days = days.join(',')
}

async function remove(id: number) {
  if (!confirm('确定删除此循环模板？')) return
  await api.deleteRecurringTemplate(id)
  await loadTemplates()
}

async function toggleEnabled(t: RecurringTemplate) {
  await api.updateRecurringTemplate(t.id, { recurring_enabled: !t.recurring_enabled })
  await loadTemplates()
}
</script>

<template>
  <div class="page">
    <div class="page-header">
      <h2>循环模板</h2>
      <button class="btn btn-primary" @click="openCreate">+ 新建</button>
    </div>

    <div v-if="templates.length === 0" class="empty">
      暂无循环模板，创建任务时勾选"循环任务"会自动创建，也可手动添加。
    </div>

    <template v-if="dailyTemplates.length > 0">
      <div class="section-header">每天</div>
      <div v-for="t in dailyTemplates" :key="t.id" class="template-card">
        <div class="t-info">
          <div class="t-title">{{ t.title }}</div>
          <div class="t-meta">
            <span v-if="t.start_time">{{ t.start_time }}{{ t.end_time ? ' - ' + t.end_time : '' }}</span>
            <span v-if="t.category">· {{ t.category }}</span>
            <span v-if="t.priority">· 优先级{{ t.priority }}</span>
            <span v-if="t.planned_duration">· {{ t.planned_duration }}分钟</span>
            <span v-if="t.planned_days">· {{ t.planned_days }}天</span>
          </div>
          <div v-if="t.note" class="t-note">{{ t.note }}</div>
        </div>
        <div class="t-actions">
          <label class="toggle-label" title="启用循环生成">
            <input type="checkbox" :checked="t.recurring_enabled" @change="toggleEnabled(t)" />
            <span class="toggle-slider"></span>
          </label>
          <button class="btn-small" @click="openEdit(t)">编辑</button>
          <button class="btn-small btn-danger-text" @click="remove(t.id)">删除</button>
        </div>
      </div>
    </template>

    <template v-if="weeklyTemplates.length > 0">
      <div class="section-header">每周</div>
      <div v-for="t in weeklyTemplates" :key="t.id" class="template-card">
        <div class="t-info">
          <div class="t-title">{{ t.title }}</div>
          <div class="t-meta">
            <span class="recurrence-badge">{{ formatWeekDays(t.recurrence_days) }}</span>
            <span v-if="t.start_time">{{ t.start_time }}{{ t.end_time ? ' - ' + t.end_time : '' }}</span>
            <span v-if="t.category">· {{ t.category }}</span>
            <span v-if="t.priority">· 优先级{{ t.priority }}</span>
            <span v-if="t.planned_duration">· {{ t.planned_duration }}分钟</span>
            <span v-if="t.planned_days">· {{ t.planned_days }}天</span>
          </div>
          <div v-if="t.note" class="t-note">{{ t.note }}</div>
        </div>
        <div class="t-actions">
          <label class="toggle-label" title="启用循环生成">
            <input type="checkbox" :checked="t.recurring_enabled" @change="toggleEnabled(t)" />
            <span class="toggle-slider"></span>
          </label>
          <button class="btn-small" @click="openEdit(t)">编辑</button>
          <button class="btn-small btn-danger-text" @click="remove(t.id)">删除</button>
        </div>
      </div>
    </template>

    <div v-if="showForm" class="modal-overlay" @click.self="showForm = false">
      <div class="modal">
        <h3>{{ editingId ? '编辑' : '新建' }}循环模板</h3>
        <div class="form-group">
          <label>标题</label>
          <input v-model="form.title" placeholder="任务标题" />
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
        <div class="form-row">
          <div class="form-group">
            <label>计划时长(分钟)</label>
            <input type="number" v-model.number="form.planned_duration" min="0" />
          </div>
          <div class="form-group">
            <label>优先级(1高/2中/3低)</label>
            <select v-model.number="form.priority">
              <option :value="1">高</option>
              <option :value="2">中</option>
              <option :value="3">低</option>
            </select>
          </div>
        </div>
        <div class="form-row">
          <div class="form-group">
            <label>分类</label>
            <input v-model="form.category" placeholder="如：工作、学习" />
          </div>
          <div class="form-group">
            <label>计划完成天数</label>
            <input type="number" v-model.number="form.planned_days" min="1" />
          </div>
        </div>
        <div class="form-group">
          <label>重复方式</label>
          <div class="recurrence-type">
            <label class="radio-label">
              <input type="radio" v-model="form.recurrence_type" value="daily" /> 每天
            </label>
            <label class="radio-label">
              <input type="radio" v-model="form.recurrence_type" value="weekly" /> 每周
            </label>
          </div>
        </div>
        <div v-if="form.recurrence_type === 'weekly'" class="form-group">
          <label>重复星期</label>
          <div class="weekday-grid">
            <label v-for="(name, i) in weekDays" :key="i" class="weekday-label" :class="{ active: form.recurrence_days.includes(String(i)) }">
              <input type="checkbox" :value="i" :checked="form.recurrence_days.includes(String(i))" @change="toggleDay(i)" />
              {{ name }}
            </label>
          </div>
        </div>
        <div class="form-group">
          <label>备注</label>
          <textarea v-model="form.note" rows="2"></textarea>
        </div>
        <div class="form-group">
          <label class="toggle-row">
            <span>启用循环生成</span>
            <label class="toggle-label">
              <input type="checkbox" v-model="form.recurring_enabled" />
              <span class="toggle-slider"></span>
            </label>
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
          <button class="btn btn-primary" @click="save">保存</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.page {
  padding: 20px 0;
}
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
.page-header h2 {
  font-size: 20px;
}
.empty {
  text-align: center;
  color: var(--text-secondary);
  padding: 40px 20px;
  font-size: 14px;
  line-height: 1.6;
}
.template-card {
  background: var(--card);
  border-radius: var(--radius);
  padding: 14px 16px;
  margin-bottom: 10px;
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 12px;
}
.t-info {
  flex: 1;
  min-width: 0;
}
.t-title {
  font-size: 15px;
  font-weight: 600;
  margin-bottom: 4px;
}
.t-meta {
  font-size: 12px;
  color: var(--text-secondary);
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}
.t-note {
  font-size: 12px;
  color: var(--text-secondary);
  margin-top: 4px;
  white-space: pre-wrap;
}
.t-actions {
  display: flex;
  gap: 8px;
  flex-shrink: 0;
}
.btn-small {
  padding: 4px 10px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--card);
  color: var(--text);
  font-size: 12px;
  cursor: pointer;
}
.btn-danger-text {
  color: #dc2626;
  border-color: #fca5a5;
}
.section-header {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-secondary);
  padding: 12px 0 6px;
  margin-top: 4px;
  border-bottom: 1px solid var(--border);
}
.section-header:first-of-type {
  margin-top: 0;
}
.recurrence-badge {
  display: inline-block;
  background: var(--primary);
  color: #fff;
  font-size: 11px;
  padding: 1px 6px;
  border-radius: 4px;
  margin-right: 4px;
}
.modal-overlay {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  background: rgba(0,0,0,0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 100;
}
.modal {
  background: var(--card);
  border-radius: var(--radius);
  padding: 24px;
  max-width: 400px;
  width: 90%;
  max-height: 80vh;
  overflow-y: auto;
}
.modal h3 {
  font-size: 18px;
  margin-bottom: 16px;
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
  padding: 8px 10px;
  border: 1px solid var(--border);
  border-radius: 6px;
  font-size: 13px;
  outline: none;
  box-sizing: border-box;
}
.form-group input:focus,
.form-group select:focus,
.form-group textarea:focus {
  border-color: var(--primary);
}
.form-row {
  display: flex;
  gap: 12px;
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
  padding: 10px;
  border: 1px solid var(--border);
  border-radius: 8px;
  font-size: 14px;
  cursor: pointer;
  text-align: center;
  background: var(--card);
  color: var(--text);
}
.btn-primary {
  background: var(--primary);
  color: white;
  border-color: var(--primary);
}
.recurrence-type {
  display: flex;
  gap: 16px;
}
.radio-label {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 14px;
  cursor: pointer;
}
.weekday-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}
.weekday-label {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 6px 10px;
  border: 1px solid var(--border);
  border-radius: 6px;
  font-size: 12px;
  cursor: pointer;
  user-select: none;
}
.weekday-label.active {
  background: var(--primary);
  color: white;
  border-color: var(--primary);
}
.weekday-label input {
  display: none;
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
.t-actions .toggle-label {
  margin-top: 2px;
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