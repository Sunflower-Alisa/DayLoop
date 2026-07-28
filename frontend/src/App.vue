<script setup lang="ts">
import { provide, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { api, setServerUrl, getServerUrl } from './api'
import { auth } from './store/auth'

const router = useRouter()
const showUpdateBanner = ref(false)

const sidebarOpen = ref(false)
function toggleSidebar() { sidebarOpen.value = !sidebarOpen.value }
function closeSidebar() { sidebarOpen.value = false }
function navigateTo(name: string) {
  router.push({ name })
  closeSidebar()
}

const serverInfo = ref('检测中...')
const serverConnected = ref(false)
const appVersion = ref('')

async function checkVersion() {
  try {
    const v = await api.getVersion()
    serverInfo.value = `${v.lanIP}:${v.port}`
    serverConnected.value = true
    appVersion.value = v.version
  } catch (e) {
    serverInfo.value = '未连接'
    serverConnected.value = false
  }
}

const showServerDialog = ref(false)
const serverUrlInput = ref('')

function openServerDialog() {
  serverUrlInput.value = getServerUrl() === '(本机)' ? '' : getServerUrl()
  showServerDialog.value = true
  closeSidebar()
}

function saveServerUrl() {
  const url = serverUrlInput.value.trim()
  setServerUrl(url)
  showServerDialog.value = false
  setTimeout(checkVersion, 500)
  location.reload()
}

async function exportData() {
  closeSidebar()
  function today() {
    const d = new Date()
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
  }
  try {
    const blob = await api.exportJson()
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `dayloop-export-${today()}.json`
    a.click()
    URL.revokeObjectURL(url)
  } catch (e) {
    alert('导出失败: ' + (e as Error).message)
  }
}

const syncing = ref(false)
async function syncAll() {
  syncing.value = true
  try {
    const result = await api.syncAllObsidian()
    alert(`同步完成: 笔记${result.notes}条, 复盘${result.reviews}条, 成果${result.achievements}条`)
  } catch (e) {
    alert('同步失败: ' + (e as Error).message)
  } finally {
    syncing.value = false
  }
}

function today(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}
provide('today', today())

async function checkTaskReminders() {
  if (!('serviceWorker' in navigator) || !('Notification' in window)) return
  if (Notification.permission !== 'granted') return
  const now = new Date()
  const todayStr = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
  try {
    const tasks = await api.getTasks(todayStr)
    tasks.forEach(task => {
      if (task.status !== 'planned' && task.status !== 'in_progress') return
      if (!task.start_time) return
      const [h, m] = task.start_time.split(':').map(Number)
      const taskMinutes = h * 60 + m
      const currentMinutes = now.getHours() * 60 + now.getMinutes()
      const diff = taskMinutes - currentMinutes
      if (diff > 0 && diff <= 5) {
        navigator.serviceWorker.controller?.postMessage({
          type: 'SHOW_NOTIFICATION',
          title: '任务即将开始',
          body: '"' + task.title + '" 将在 ' + task.start_time + ' 开始',
          tag: 'task-reminder-' + task.id,
          url: '/#/plan',
        })
      }
    })
  } catch (e) {
    // ignore
  }
}

function startReminderCheck() {
  checkTaskReminders()
  setInterval(checkTaskReminders, 60000)
}

let lastVersion = ''
async function checkForUpdates() {
  try {
    const v = await api.getVersion()
    if (lastVersion && lastVersion !== v.version) {
      showUpdateBanner.value = true
    }
    lastVersion = v.version
  } catch (e) {
    // ignore
  }
}

function applyUpdate() {
  showUpdateBanner.value = false
  if ('serviceWorker' in navigator) {
    navigator.serviceWorker.getRegistration().then(reg => {
      if (reg?.waiting) {
        reg.waiting.postMessage({ type: 'SKIP_WAITING' })
      }
    })
  }
  window.location.reload()
}

const coreNav = [
  { name: 'home', label: '首页', icon: '🏠' },
  { name: 'plan', label: '今日计划', icon: '📋' },
  { name: 'notes', label: '备忘录', icon: '📝' },
  { name: 'questions', label: '问题库', icon: '❓' },
  { name: 'review', label: '复盘', icon: '📊' },
  { name: 'achievements', label: '成果', icon: '🏆' },
  { name: 'statistics', label: '统计', icon: '📈' },
]

onMounted(() => {
  checkVersion()
  if (!('Notification' in window) || !('serviceWorker' in navigator)) return
  if (Notification.permission === 'granted') {
    startReminderCheck()
  } else if (Notification.permission === 'default') {
    Notification.requestPermission().then(perm => {
      if (perm === 'granted') startReminderCheck()
    })
  }
  checkForUpdates()
  setInterval(checkForUpdates, 30000)
})
</script>

<template>
  <div class="app">
    <div v-if="showUpdateBanner" class="update-banner" @click="applyUpdate">
      🔄 新版本可用，点击刷新
    </div>

    <div class="top-bar">
      <div class="top-bar-left">
        <button class="hamburger" @click="toggleSidebar" aria-label="菜单">
          <span class="hamburger-line"></span>
          <span class="hamburger-line"></span>
          <span class="hamburger-line"></span>
        </button>
        <span class="brand">DayLoop</span>
      </div>
      <div class="top-bar-right">
        <div class="connection-status" :class="{ connected: serverConnected }" title="服务器状态">
          <span class="status-dot"></span>
          <span class="status-text">{{ serverInfo }}</span>
        </div>
        <span v-if="appVersion" class="version">v{{ appVersion }}</span>
        <button class="sync-btn" :class="{ syncing }" @click="syncAll" title="同步到 Obsidian">🔄</button>
      </div>
    </div>

    <div v-if="sidebarOpen" class="sidebar-backdrop" @click="closeSidebar"></div>
    <aside :class="['sidebar', { open: sidebarOpen }]">
      <div class="sidebar-user" v-if="auth.isLoggedIn" @click="navigateTo('profile')">
        <div class="sidebar-avatar">
          {{ auth.user?.username?.[0]?.toUpperCase() || '?' }}
        </div>
        <div class="sidebar-user-info">
          <span class="sidebar-user-name">{{ auth.user?.username || '用户' }}</span>
          <span class="sidebar-user-sub">个人设置</span>
        </div>
      </div>
      <div class="sidebar-header">
        <span class="sidebar-brand">DayLoop</span>
        <button class="sidebar-close" @click="closeSidebar">✕</button>
      </div>
      <nav class="sidebar-nav">
        <div class="sidebar-section">
          <div class="sidebar-section-title">核心功能</div>
          <button v-for="item in coreNav" :key="item.name" class="sidebar-item" @click="navigateTo(item.name)">
            <span class="sidebar-item-icon">{{ item.icon }}</span>
            <span>{{ item.label }}</span>
          </button>
        </div>
        <div class="sidebar-divider"></div>
        <div class="sidebar-section">
          <div class="sidebar-section-title">系统</div>
          <button class="sidebar-item" @click="navigateTo('history')">
            <span class="sidebar-item-icon">📅</span><span>历史记录</span>
          </button>
          <button class="sidebar-item" @click="navigateTo('templates')">
            <span class="sidebar-item-icon">🔄</span><span>循环模板</span>
          </button>
          <button class="sidebar-item" @click="openServerDialog">
            <span class="sidebar-item-icon">🌐</span><span>服务器配置</span>
          </button>
          <button class="sidebar-item" @click="exportData">
            <span class="sidebar-item-icon">📤</span><span>导出数据</span>
          </button>
        </div>
      </nav>
    </aside>

    <main class="main">
      <router-view />
    </main>

    <div v-if="showServerDialog" class="modal-overlay" @click.self="showServerDialog = false">
      <div class="modal">
        <h3>🌐 服务器配置</h3>
        <p class="modal-desc">手机/其他设备访问时，输入后端服务器的 LAN IP 地址。</p>
        <div class="form-group">
          <label>服务器地址</label>
          <input v-model="serverUrlInput" placeholder="留空使用本机，或输入 http://192.168.x.x:3001" />
        </div>
        <p class="modal-hint">提示：后端启动时会显示 LAN 访问地址，如 http://192.168.1.100:3001</p>
        <div class="form-actions">
          <button class="btn" @click="showServerDialog = false">取消</button>
          <button class="btn btn-primary" @click="saveServerUrl">保存并刷新</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style>
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

:root {
  --primary: #4f46e5;
  --primary-light: #818cf8;
  --bg: #f5f5f5;
  --card: #ffffff;
  --text: #1f2937;
  --text-secondary: #6b7280;
  --border: #e5e7eb;
  --success: #10b981;
  --warning: #f59e0b;
  --danger: #ef4444;
  --radius: 12px;
  --top-bar-height: 52px;
}

body {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  background: var(--bg);
  color: var(--text);
  -webkit-font-smoothing: antialiased;
}

.app {
  max-width: 480px;
  margin: 0 auto;
  min-height: 100dvh;
  display: flex;
  flex-direction: column;
  background: var(--bg);
}

@media (min-width: 540px) {
  .app { max-width: 90vw; }
}

@media (min-width: 768px) {
  .app { max-width: 720px; }
}

@media (min-width: 1024px) {
  .app { max-width: 860px; }
}

.update-banner {
  background: var(--warning);
  color: white;
  text-align: center;
  padding: 10px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  animation: pulse 1.5s infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.8; }
}

.top-bar {
  height: var(--top-bar-height);
  background: var(--primary);
  color: white;
  padding: 0 12px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  position: sticky;
  top: 0;
  z-index: 50;
}

.top-bar-left {
  display: flex;
  align-items: center;
  gap: 10px;
}

.hamburger {
  width: 36px;
  height: 36px;
  border: none;
  background: transparent;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: 4px;
  padding: 6px;
  border-radius: 6px;
}

.hamburger:hover {
  background: rgba(255,255,255,0.15);
}

.hamburger-line {
  display: block;
  height: 2px;
  background: white;
  border-radius: 1px;
}

.brand {
  font-size: 18px;
  font-weight: 700;
}

.top-bar-right {
  display: flex;
  align-items: center;
  gap: 10px;
}

.connection-status {
  display: flex;
  align-items: center;
  gap: 5px;
  font-size: 11px;
  opacity: 0.9;
}

.status-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: var(--danger);
  flex-shrink: 0;
}

.connection-status.connected .status-dot {
  background: var(--success);
}

.status-text {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 120px;
}

.version {
  font-size: 11px;
  opacity: 0.8;
  background: rgba(255,255,255,0.15);
  padding: 2px 6px;
  border-radius: 4px;
  white-space: nowrap;
}

.sync-btn {
  width: 30px;
  height: 30px;
  border: none;
  background: transparent;
  cursor: pointer;
  font-size: 16px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
}

.sync-btn:hover {
  background: rgba(255,255,255,0.15);
}

.sync-btn.syncing {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

.sidebar-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,0.4);
  z-index: 100;
}

.sidebar {
  position: fixed;
  top: 0;
  left: 0;
  bottom: 0;
  width: 280px;
  max-width: 80vw;
  background: var(--card);
  z-index: 101;
  transform: translateX(-100%);
  transition: transform 0.3s ease;
  display: flex;
  flex-direction: column;
  box-shadow: 2px 0 12px rgba(0,0,0,0.15);
}

.sidebar.open {
  transform: translateX(0);
}

.sidebar-user {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 20px 16px 12px;
  cursor: pointer;
  transition: background 0.15s;
}

.sidebar-user:hover {
  background: var(--bg);
}

.sidebar-avatar {
  width: 42px;
  height: 42px;
  border-radius: 50%;
  background: var(--primary);
  color: white;
  font-size: 18px;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.sidebar-user-info {
  flex: 1;
  min-width: 0;
}

.sidebar-user-name {
  font-size: 15px;
  font-weight: 600;
  color: var(--text);
  display: block;
}

.sidebar-user-sub {
  font-size: 12px;
  color: var(--text-secondary);
  display: block;
  margin-top: 1px;
}

.sidebar-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px;
  border-bottom: 1px solid var(--border);
}

.sidebar-brand {
  font-size: 20px;
  font-weight: 800;
  color: var(--primary);
}

.sidebar-close {
  width: 32px;
  height: 32px;
  border: none;
  background: transparent;
  cursor: pointer;
  font-size: 18px;
  color: var(--text-secondary);
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.sidebar-close:hover {
  background: var(--bg);
}

.sidebar-nav {
  flex: 1;
  overflow-y: auto;
  padding: 12px 0;
}

.sidebar-section {
  padding: 0 12px;
}

.sidebar-section-title {
  font-size: 11px;
  font-weight: 600;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  padding: 8px 12px 6px;
}

.sidebar-item {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  padding: 10px 12px;
  border: none;
  background: transparent;
  cursor: pointer;
  font-size: 15px;
  color: var(--text);
  border-radius: 8px;
  text-align: left;
  transition: background 0.15s;
}

.sidebar-item:hover {
  background: var(--bg);
}

.sidebar-item-icon {
  font-size: 18px;
  width: 24px;
  text-align: center;
  flex-shrink: 0;
}

.sidebar-divider {
  height: 1px;
  background: var(--border);
  margin: 8px 16px;
}

.main {
  flex: 1;
  padding: 16px;
  overflow-y: auto;
}

.main > :deep(.home) {
  padding-top: 0;
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
  margin-bottom: 12px;
}

.modal-desc {
  font-size: 13px;
  color: var(--text-secondary);
  margin-bottom: 16px;
  line-height: 1.5;
}

.modal-hint {
  font-size: 12px;
  color: var(--text-secondary);
  margin-top: 8px;
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

.form-group input {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: 8px;
  font-size: 14px;
  outline: none;
}

.form-group input:focus {
  border-color: var(--primary);
}

.form-actions {
  display: flex;
  gap: 10px;
  margin-top: 16px;
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

.btn-primary {
  background: var(--primary);
  color: white;
  border-color: var(--primary);
}
</style>
