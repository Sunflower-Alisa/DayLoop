<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { api } from '../api'

const router = useRouter()
const route = useRoute()
const isQuestionMode = computed(() => route.name === 'question-categories')

const noteCategories = ref<string[]>([])
const questionCategories = ref<string[]>([])
const taskCategories = ref<string[]>([])
const newNoteCat = ref('')
const newQuestionCat = ref('')
const newTaskCat = ref('')

onMounted(async () => {
  try {
    if (isQuestionMode.value) {
      questionCategories.value = await api.getQuestionCategories()
    } else {
      const [noteCats] = await Promise.all([api.getNoteCategories()])
      noteCategories.value = noteCats
      const allTasks = await api.getTasks()
      const taskCats = new Set(allTasks.map(t => t.category).filter(Boolean))
      taskCategories.value = Array.from(taskCats).sort()
    }
  } catch (e) {
    console.error('加载分类失败', e)
  }
})

async function addNoteCategory() {
  const cat = newNoteCat.value.trim()
  if (!cat || noteCategories.value.includes(cat)) return
  try {
    await api.createNoteCategory(cat)
    noteCategories.value.push(cat)
    noteCategories.value.sort()
    newNoteCat.value = ''
  } catch (e) {
    alert('添加分类失败: ' + (e.message || e))
  }
}

async function removeNoteCategory(cat: string) {
  if (!confirm(`确定删除分类「${cat}」？此操作不会删除该分类下的备忘录。`)) return
  try {
    await api.deleteNoteCategory(cat)
    noteCategories.value = noteCategories.value.filter(c => c !== cat)
  } catch (e) {
    alert('删除分类失败: ' + (e.message || e))
  }
}

async function addQuestionCategory() {
  const cat = newQuestionCat.value.trim()
  if (!cat || questionCategories.value.includes(cat)) return
  try {
    await api.createQuestionCategory(cat)
    questionCategories.value.push(cat)
    questionCategories.value.sort()
    newQuestionCat.value = ''
  } catch (e) {
    alert('添加分类失败: ' + (e.message || e))
  }
}

async function removeQuestionCategory(cat: string) {
  if (!confirm(`确定删除分类「${cat}」？此操作不会删除该分类下的问题。`)) return
  try {
    await api.deleteQuestionCategory(cat)
    questionCategories.value = questionCategories.value.filter(c => c !== cat)
  } catch (e) {
    alert('删除分类失败: ' + (e.message || e))
  }
}
</script>

<template>
  <div class="cat-manage">
    <template v-if="isQuestionMode">
      <button class="back-btn" @click="router.push('/questions')">← 返回问题库</button>
      <h2>❓ 问题分类管理</h2>
      <div class="cat-section">
        <h3>问题分类</h3>
        <div class="cat-input-row">
          <input v-model="newQuestionCat" placeholder="新分类名称" @keyup.enter="addQuestionCategory" />
          <button class="btn btn-primary" @click="addQuestionCategory">添加</button>
        </div>
        <div class="cat-tags">
          <span v-for="cat in questionCategories" :key="cat" class="cat-tag">
            {{ cat }}
            <button class="cat-remove" @click="removeQuestionCategory(cat)">✕</button>
          </span>
          <span v-if="questionCategories.length === 0" class="text-muted">暂无分类</span>
        </div>
      </div>
    </template>
    <template v-else>
      <button class="back-btn" @click="router.push('/notes')">← 返回备忘录</button>
      <h2>分类管理</h2>
      <div class="cat-section">
        <h3>📝 备忘录分类</h3>
        <div class="cat-input-row">
          <input v-model="newNoteCat" placeholder="新分类名称" @keyup.enter="addNoteCategory" />
          <button class="btn btn-primary" @click="addNoteCategory">添加</button>
        </div>
        <div class="cat-tags">
          <span v-for="cat in noteCategories" :key="cat" class="cat-tag">
            {{ cat }}
            <button class="cat-remove" @click="removeNoteCategory(cat)">✕</button>
          </span>
          <span v-if="noteCategories.length === 0" class="text-muted">暂无分类</span>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.cat-manage {
  display: flex;
  flex-direction: column;
  gap: 20px;
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

.cat-manage h2 {
  font-size: 20px;
}

.cat-section {
  background: var(--card);
  border-radius: var(--radius);
  padding: 16px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
}

.cat-section h3 {
  font-size: 15px;
  margin-bottom: 12px;
}

.cat-input-row {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
}

.cat-input-row input {
  flex: 1;
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: 8px;
  font-size: 14px;
  outline: none;
}

.cat-input-row input:focus {
  border-color: var(--primary);
}

.cat-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.cat-tag {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  background: var(--bg);
  border-radius: 20px;
  font-size: 13px;
}

.cat-remove {
  width: 16px;
  height: 16px;
  border: none;
  border-radius: 50%;
  background: var(--danger);
  color: white;
  font-size: 10px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}

.text-muted {
  color: var(--text-secondary);
  font-size: 13px;
}
</style>
