<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api } from '../api'
import type { Note } from '../types'
import { formatDate, renderContent } from '../utils/format'

const route = useRoute()
const router = useRouter()
const isNew = computed(() => route.name === 'note-new')
const note = ref<Note | null>(null)
const loading = ref(true)
const saving = ref(false)
const previewMode = ref(false)
const uploading = ref(false)

const form = ref({
  title: '',
  content: '',
  category: '',
  tags: '',
})

const categories = ref<string[]>([])
const images = ref<string[]>([])

onMounted(async () => {
  categories.value = await api.getNoteCategories()
  if (isNew.value) {
    loading.value = false
    return
  }
  const id = Number(route.params.id)
  const n = await api.getNote(id)
  note.value = n
  form.value = { title: n.title, content: n.content, category: n.category, tags: n.tags || '' }
  extractImages(n.content)
  loading.value = false
})

function extractImages(content: string) {
  const regex = /!\[([^\]]*)\]\(([^)]+)\)/g
  const imgs: string[] = []
  let match
  while ((match = regex.exec(content)) !== null) {
    imgs.push(match[2])
  }
  images.value = imgs
}

async function insertImage() {
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
        const markdown = `\n![${file.name}](${result.url})\n`
        form.value.content += markdown
        extractImages(form.value.content)
      } catch (err) {
        alert('图片上传失败: ' + (err instanceof Error ? err.message : '未知错误'))
      } finally {
        uploading.value = false
      }
    }
    reader.readAsDataURL(file)
  }
  input.click()
}

function removeImage(url: string) {
  const escaped = url.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const regex = new RegExp(`\\n?!\\[([^\\]]*)\\]\\(${escaped}\\)\\n?`, 'g')
  form.value.content = form.value.content.replace(regex, '')
  extractImages(form.value.content)
}

async function save() {
  if (!form.value.title.trim()) return
  saving.value = true
  if (isNew.value) {
    const n = await api.createNote(form.value)
    router.replace('/notes/' + n.id)
  } else {
    await api.updateNote(note.value!.id, form.value)
    note.value = await api.getNote(note.value!.id)
  }
  saving.value = false
}

async function deleteNote() {
  if (!confirm('确定删除此备忘录？')) return
  await api.deleteNote(note.value!.id)
  router.push('/notes')
}

function statusLabel(s: string): string {
  const map: Record<string, string> = { planned: '计划中', in_progress: '进行中', completed: '已完成', cancelled: '已取消' }
  return map[s] || s
}

function previewImage(url: string) {
  window.open(url, '_blank')
}
</script>

<template>
  <div class="note-detail">
    <button class="back-btn" @click="router.push('/notes')">← 返回</button>

    <div v-if="loading" class="loading">加载中...</div>

    <template v-else>
      <div class="form-group">
        <label>标题</label>
        <input v-model="form.title" placeholder="备忘录标题" />
      </div>
      <div class="form-group">
        <label>分类</label>
        <div class="cat-row">
          <input v-model="form.category" placeholder="输入分类名称" list="cat-list" />
          <datalist id="cat-list">
            <option v-for="c in categories" :key="c" :value="c" />
          </datalist>
        </div>
      </div>
      <div class="form-group">
        <label>标签 <span class="text-muted">（逗号分隔，如：重要,待办,灵感）</span></label>
        <input v-model="form.tags" placeholder="标签1,标签2,标签3" />
      </div>
      <div class="form-group">
        <label>内容 <span class="text-muted">（支持文字和图片）</span></label>
        <div class="editor-toolbar">
          <button class="toolbar-btn" @click="insertImage" :disabled="uploading" title="插入图片">
            {{ uploading ? '⏳ 上传中...' : '🖼️ 图片' }}
          </button>
          <button class="toolbar-btn" @click="previewMode = !previewMode">{{ previewMode ? '✏️ 编辑' : '👁️ 预览' }}</button>
        </div>
        <textarea v-if="!previewMode" v-model="form.content" placeholder="输入备忘录内容，支持插入图片" rows="12"></textarea>
        <div v-else class="preview-content" v-html="renderContent(form.content)"></div>
      </div>

      <div v-if="images.length > 0 && !previewMode" class="image-gallery">
        <h4>已插入图片 ({{ images.length }})</h4>
        <div class="gallery-grid">
          <div v-for="(img, i) in images" :key="i" class="gallery-item">
            <img :src="img" class="gallery-thumb" @click="previewImage(img)" />
            <button class="gallery-remove" @click="removeImage(img)" title="移除图片">✕</button>
          </div>
        </div>
      </div>

      <div v-if="note?.linked_tasks?.length" class="linked-section">
        <h3>🔗 关联任务 ({{ note.linked_tasks.length }})</h3>
        <div v-for="task in note.linked_tasks" :key="task.id" class="linked-task-card">
          <div class="lt-header">
            <span class="lt-title">{{ task.title }}</span>
            <span class="lt-status" :class="task.status">{{ statusLabel(task.status) }}</span>
          </div>
          <div class="lt-meta">
            <span>{{ formatDate(task.date) }}</span>
            <span v-if="task.start_time">{{ task.start_time }}-{{ task.end_time }}</span>
          </div>
        </div>
      </div>

      <div class="form-actions">
        <button v-if="!isNew" class="btn btn-danger" @click="deleteNote">删除</button>
        <button class="btn btn-primary" @click="save" :disabled="saving">{{ saving ? '保存中...' : '保存' }}</button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.note-detail {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.back-btn {
  padding: 8px 16px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--card);
  font-size: 14px;
  cursor: pointer;
  align-self: flex-start;
}

.loading {
  text-align: center;
  padding: 60px 20px;
  color: var(--text-secondary);
}

.form-group label {
  display: block;
  font-size: 13px;
  color: var(--text-secondary);
  margin-bottom: 4px;
}

.form-group input,
.form-group textarea,
.form-group select {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: 8px;
  font-size: 14px;
  outline: none;
  font-family: inherit;
}

.form-group textarea {
  resize: vertical;
  min-height: 200px;
  line-height: 1.6;
}

.form-group input:focus,
.form-group textarea:focus {
  border-color: var(--primary);
}

.cat-row {
  display: flex;
  gap: 8px;
}

.editor-toolbar {
  display: flex;
  gap: 8px;
  margin-bottom: 8px;
}

.toolbar-btn {
  padding: 6px 12px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--card);
  font-size: 13px;
  cursor: pointer;
}

.toolbar-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.preview-content {
  padding: 12px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--card);
  min-height: 200px;
  line-height: 1.8;
  font-size: 14px;
}

.image-gallery {
  background: var(--card);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 12px;
}

.image-gallery h4 {
  font-size: 13px;
  color: var(--text-secondary);
  margin-bottom: 8px;
}

.gallery-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(80px, 1fr));
  gap: 8px;
}

.gallery-item {
  position: relative;
  border-radius: 6px;
  overflow: hidden;
  aspect-ratio: 1;
  background: var(--bg);
}

.gallery-thumb {
  width: 100%;
  height: 100%;
  object-fit: cover;
  cursor: pointer;
  transition: transform 0.2s;
}

.gallery-thumb:hover {
  transform: scale(1.05);
}

.gallery-remove {
  position: absolute;
  top: 2px;
  right: 2px;
  width: 20px;
  height: 20px;
  border: none;
  border-radius: 50%;
  background: rgba(0,0,0,0.6);
  color: white;
  font-size: 12px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  opacity: 0;
  transition: opacity 0.2s;
}

.gallery-item:hover .gallery-remove {
  opacity: 1;
}

.linked-section {
  background: #eef2ff;
  border-radius: var(--radius);
  padding: 14px 16px;
}

.linked-section h3 {
  font-size: 14px;
  margin-bottom: 8px;
}

.linked-task-card {
  background: var(--card);
  border-radius: 8px;
  padding: 10px 12px;
}

.lt-header {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 4px;
}

.lt-title {
  font-weight: 500;
  font-size: 14px;
}

.lt-status {
  font-size: 11px;
  padding: 2px 6px;
  border-radius: 4px;
}

.lt-status.completed { background: #d1fae5; color: #065f46; }
.lt-status.in_progress { background: #fef3c7; color: #92400e; }
.lt-status.planned { background: #e0e7ff; color: #3730a3; }
.lt-status.cancelled { background: #fce4ec; color: #c62828; }

.lt-meta {
  font-size: 12px;
  color: var(--text-secondary);
  display: flex;
  gap: 12px;
}

.form-actions {
  display: flex;
  gap: 10px;
}

.form-actions .btn { flex: 1; }

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

.btn-danger {
  color: var(--danger);
  border-color: var(--danger);
}

.text-muted {
  font-weight: normal;
  color: var(--text-secondary);
  font-size: 12px;
}
</style>
