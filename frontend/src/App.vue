<script setup lang="ts">
import { provide, onMounted, onUnmounted, ref } from 'vue'
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
  // interval is managed in onMounted with cleanup
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
  { name: 'agent', label: 'AI 助手', icon: '🤖' },
  { name: 'plan', label: '今日计划', icon: '📋' },
  { name: 'notes', label: '备忘录', icon: '📝' },
  { name: 'questions', label: '问题库', icon: '❓' },
  { name: 'review', label: '复盘', icon: '📊' },
  { name: 'summary', label: '总结', icon: '📑' },
  { name: 'calendar', label: '日历预览', icon: '📅' },
  { name: 'achievements', label: '成果', icon: '🏆' },
  { name: 'statistics', label: '统计', icon: '📈' },
]

const englishNav = [
  { name: 'english', label: '英语学习', icon: '🇬🇧' },
  { name: 'english-words', label: '单词背诵', icon: '🔤' },
  { name: 'english-scenarios', label: '场景英语', icon: '💬' },
  { name: 'english-speaking', label: '口语跟读', icon: '🎙️' },
  { name: 'english-clips', label: '影视切片', icon: '🎬' },
  { name: 'english-statistics', label: '学习统计', icon: '📈' },
]

const todayStr = ref(today())
provide('today', todayStr)

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
  const reminderInterval = setInterval(checkTaskReminders, 60000)
  const updateInterval = setInterval(checkForUpdates, 30000)
  
  onUnmounted(() => {
    clearInterval(reminderInterval)
    clearInterval(updateInterval)
  })
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
          <div class="sidebar-section-title">英语学习</div>
          <button v-for="item in englishNav" :key="item.name" class="sidebar-item" @click="navigateTo(item.name)">
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
  /* Primary palette */
  --primary: #6366f1;
  --primary-light: #a5b4fc;
  --primary-dark: #4338ca;
  --primary-bg: #eef2ff;
  
  /* Neutral palette */
  --bg: #f8fafc;
  --bg-secondary: #f1f5f9;
  --card: #ffffff;
  --card-hover: #fafbfc;
  
  /* Text */
  --text: #0f172a;
  --text-secondary: #64748b;
  --text-tertiary: #94a3b8;
  --text-inverse: #ffffff;
  
  /* Border */
  --border: #e2e8f0;
  --border-light: #f1f5f9;
  --border-focus: #6366f1;
  
  /* Semantic colors */
  --success: #10b981;
  --success-bg: #ecfdf5;
  --success-text: #065f46;
  --warning: #f59e0b;
  --warning-bg: #fffbeb;
  --warning-text: #92400e;
  --danger: #ef4444;
  --danger-bg: #fef2f2;
  --danger-text: #991b1b;
  --info: #3b82f6;
  --info-bg: #eff6ff;
  --info-text: #1e40af;
  
  /* Spacing scale */
  --space-xs: 4px;
  --space-sm: 8px;
  --space-md: 12px;
  --space-lg: 16px;
  --space-xl: 20px;
  --space-2xl: 24px;
  --space-3xl: 32px;
  
  /* Typography */
  --text-xs: 11px;
  --text-sm: 13px;
  --text-base: 14px;
  --text-md: 15px;
  --text-lg: 16px;
  --text-xl: 18px;
  --text-2xl: 22px;
  --text-3xl: 28px;
  --font-normal: 400;
  --font-medium: 500;
  --font-semibold: 600;
  --font-bold: 700;
  --font-extrabold: 800;
  
  /* Shadows */
  --shadow-xs: 0 1px 2px rgba(0,0,0,0.04);
  --shadow-sm: 0 1px 3px rgba(0,0,0,0.06), 0 1px 2px rgba(0,0,0,0.04);
  --shadow-md: 0 4px 6px rgba(0,0,0,0.05), 0 2px 4px rgba(0,0,0,0.04);
  --shadow-lg: 0 10px 15px rgba(0,0,0,0.06), 0 4px 6px rgba(0,0,0,0.04);
  --shadow-xl: 0 20px 25px rgba(0,0,0,0.08), 0 8px 10px rgba(0,0,0,0.04);
  
  /* Layout */
  --radius-xs: 6px;
  --radius-sm: 8px;
  --radius: 12px;
  --radius-lg: 16px;
  --radius-xl: 20px;
  --radius-full: 9999px;
  --top-bar-height: 56px;
  
  /* Transitions */
  --transition-fast: 0.15s ease;
  --transition: 0.2s ease;
  --transition-slow: 0.3s ease;
  
  /* Safe areas */
  --safe-bottom: env(safe-area-inset-bottom, 0px);
}

@media (prefers-color-scheme: dark) {
  :root {
    --bg: #0f172a;
    --bg-secondary: #1e293b;
    --card: #1e293b;
    --card-hover: #273449;
    --text: #f1f5f9;
    --text-secondary: #94a3b8;
    --text-tertiary: #64748b;
    --border: #334155;
    --border-light: #273449;
    --primary-bg: #1e1b4b;
    --success-bg: #022c22;
    --warning-bg: #451a03;
    --danger-bg: #450a0a;
    --info-bg: #172554;
    --shadow-xs: 0 1px 2px rgba(0,0,0,0.2);
    --shadow-sm: 0 1px 3px rgba(0,0,0,0.3);
    --shadow-md: 0 4px 6px rgba(0,0,0,0.3);
    --shadow-lg: 0 10px 15px rgba(0,0,0,0.35);
    --shadow-xl: 0 20px 25px rgba(0,0,0,0.4);
  }
}

@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
  }
}

body {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', system-ui, sans-serif;
  background: var(--bg);
  color: var(--text);
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
  line-height: 1.5;
}

.app {
  max-width: 480px;
  margin: 0 auto;
  min-height: 100dvh;
  display: flex;
  flex-direction: column;
  background: var(--bg);
  position: relative;
}

@media (min-width: 540px) {
  .app { max-width: 90vw; }
}

@media (min-width: 768px) {
  .app { max-width: 720px; }
  .main { padding: var(--space-2xl); }
}

@media (min-width: 1024px) {
  .app { max-width: 860px; }
}

/* --- Update banner --- */
.update-banner {
  background: linear-gradient(135deg, var(--warning), #d97706);
  color: white;
  text-align: center;
  padding: 10px;
  font-size: var(--text-base);
  font-weight: var(--font-semibold);
  cursor: pointer;
  animation: bannerPulse 2s infinite;
  position: sticky;
  top: 0;
  z-index: 60;
}

@keyframes bannerPulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.85; }
}

/* --- Top bar --- */
.top-bar {
  height: var(--top-bar-height);
  background: linear-gradient(135deg, var(--primary) 0%, var(--primary-dark) 100%);
  color: var(--text-inverse);
  padding: 0 var(--space-lg);
  display: flex;
  align-items: center;
  justify-content: space-between;
  position: sticky;
  top: 0;
  z-index: 50;
  box-shadow: 0 4px 12px rgba(99, 102, 241, 0.25);
  -webkit-backdrop-filter: blur(10px);
}

.top-bar-left {
  display: flex;
  align-items: center;
  gap: var(--space-md);
}

.hamburger {
  width: 40px;
  height: 40px;
  border: none;
  background: transparent;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: 5px;
  padding: 8px;
  border-radius: var(--radius-sm);
  transition: background var(--transition-fast);
}

.hamburger:hover {
  background: rgba(255,255,255,0.15);
}

.hamburger-line {
  display: block;
  height: 2px;
  background: var(--text-inverse);
  border-radius: 1px;
  transition: all var(--transition);
}

.brand {
  font-size: var(--text-xl);
  font-weight: var(--font-bold);
  letter-spacing: -0.3px;
}

.top-bar-right {
  display: flex;
  align-items: center;
  gap: var(--space-sm);
}

.connection-status {
  display: flex;
  align-items: center;
  gap: 5px;
  font-size: var(--text-xs);
  opacity: 0.9;
  padding: 4px 8px;
  border-radius: var(--radius-full);
  background: rgba(255,255,255,0.1);
}

.status-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--danger);
  flex-shrink: 0;
  transition: background var(--transition);
}

.connection-status.connected .status-dot {
  background: #4ade80;
  box-shadow: 0 0 6px rgba(74, 222, 128, 0.5);
}

.status-text {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 100px;
}

.version {
  font-size: 10px;
  opacity: 0.7;
  background: rgba(255,255,255,0.12);
  padding: 2px 8px;
  border-radius: var(--radius-full);
  white-space: nowrap;
}

.sync-btn {
  width: 34px;
  height: 34px;
  border: none;
  background: rgba(255,255,255,0.1);
  cursor: pointer;
  font-size: 15px;
  border-radius: var(--radius-full);
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background var(--transition-fast);
}

.sync-btn:hover {
  background: rgba(255,255,255,0.2);
}

.sync-btn.syncing {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

/* --- Sidebar --- */
.sidebar-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(15, 23, 42, 0.5);
  z-index: 100;
  animation: fadeIn 0.2s ease;
  backdrop-filter: blur(2px);
}

@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
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
  transition: transform var(--transition-slow);
  display: flex;
  flex-direction: column;
  box-shadow: var(--shadow-xl);
}

.sidebar.open {
  transform: translateX(0);
}

.sidebar-user {
  display: flex;
  align-items: center;
  gap: var(--space-md);
  padding: var(--space-xl) var(--space-lg) var(--space-md);
  cursor: pointer;
  transition: background var(--transition-fast);
}

.sidebar-user:hover {
  background: var(--bg-secondary);
}

.sidebar-avatar {
  width: 44px;
  height: 44px;
  border-radius: var(--radius-full);
  background: linear-gradient(135deg, var(--primary), var(--primary-dark));
  color: var(--text-inverse);
  font-size: var(--text-xl);
  font-weight: var(--font-bold);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  box-shadow: 0 2px 8px rgba(99, 102, 241, 0.3);
}

.sidebar-user-info {
  flex: 1;
  min-width: 0;
}

.sidebar-user-name {
  font-size: var(--text-md);
  font-weight: var(--font-semibold);
  color: var(--text);
  display: block;
}

.sidebar-user-sub {
  font-size: var(--text-xs);
  color: var(--text-secondary);
  display: block;
  margin-top: 2px;
}

.sidebar-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--space-lg);
  border-bottom: 1px solid var(--border);
}

.sidebar-brand {
  font-size: var(--text-2xl);
  font-weight: var(--font-extrabold);
  color: var(--primary);
  letter-spacing: -0.5px;
}

.sidebar-close {
  width: 32px;
  height: 32px;
  border: none;
  background: transparent;
  cursor: pointer;
  font-size: var(--text-xl);
  color: var(--text-secondary);
  border-radius: var(--radius-sm);
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all var(--transition-fast);
}

.sidebar-close:hover {
  background: var(--bg-secondary);
  color: var(--text);
}

.sidebar-nav {
  flex: 1;
  overflow-y: auto;
  padding: var(--space-sm) 0;
  padding-bottom: calc(var(--space-sm) + var(--safe-bottom));
}

.sidebar-section {
  padding: 0 var(--space-md);
}

.sidebar-section-title {
  font-size: 10px;
  font-weight: var(--font-semibold);
  color: var(--text-tertiary);
  text-transform: uppercase;
  letter-spacing: 1px;
  padding: var(--space-sm) var(--space-md) var(--space-xs);
}

.sidebar-item {
  display: flex;
  align-items: center;
  gap: var(--space-md);
  width: 100%;
  padding: var(--space-md);
  border: none;
  background: transparent;
  cursor: pointer;
  font-size: var(--text-md);
  color: var(--text);
  border-radius: var(--radius-sm);
  text-align: left;
  transition: all var(--transition-fast);
}

.sidebar-item:hover {
  background: var(--bg-secondary);
}

.sidebar-item:active {
  transform: scale(0.98);
}

.sidebar-item-icon {
  font-size: var(--text-xl);
  width: 24px;
  text-align: center;
  flex-shrink: 0;
}

.sidebar-divider {
  height: 1px;
  background: var(--border);
  margin: var(--space-sm) var(--space-lg);
}

/* --- Main content --- */
.main {
  flex: 1;
  padding: var(--space-lg);
  overflow-y: auto;
  padding-bottom: calc(var(--space-lg) + var(--safe-bottom));
}

/* Page transition */
.main > * {
  animation: pageEnter 0.25s ease;
}

@keyframes pageEnter {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: translateY(0); }
}

/* --- Global component styles --- */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(15, 23, 42, 0.5);
  display: flex;
  align-items: flex-end;
  z-index: 200;
  animation: fadeIn 0.2s ease;
  backdrop-filter: blur(2px);
}

.modal {
  background: var(--card);
  width: 100%;
  max-width: 480px;
  margin: 0 auto;
  border-radius: var(--radius-xl) var(--radius-xl) 0 0;
  padding: var(--space-2xl);
  max-height: 85vh;
  overflow-y: auto;
  animation: slideUp 0.3s ease;
  box-shadow: var(--shadow-xl);
}

@keyframes slideUp {
  from { transform: translateY(100%); }
  to { transform: translateY(0); }
}

.modal h3 {
  font-size: var(--text-xl);
  font-weight: var(--font-semibold);
  margin-bottom: var(--space-md);
  color: var(--text);
}

.modal-desc {
  font-size: var(--text-sm);
  color: var(--text-secondary);
  margin-bottom: var(--space-lg);
  line-height: 1.6;
}

.modal-hint {
  font-size: var(--text-xs);
  color: var(--text-tertiary);
  margin-top: var(--space-sm);
}

/* Forms */
.form-group {
  margin-bottom: var(--space-md);
}

.form-group label {
  display: block;
  font-size: var(--text-sm);
  color: var(--text-secondary);
  margin-bottom: var(--space-xs);
  font-weight: var(--font-medium);
}

.form-group input,
.form-group textarea,
.form-group select {
  width: 100%;
  padding: var(--space-md);
  border: 1.5px solid var(--border);
  border-radius: var(--radius-sm);
  font-size: var(--text-base);
  color: var(--text);
  background: var(--card);
  outline: none;
  transition: border-color var(--transition-fast), box-shadow var(--transition-fast);
  font-family: inherit;
  line-height: 1.5;
}

.form-group input:focus,
.form-group textarea:focus,
.form-group select:focus {
  border-color: var(--primary);
  box-shadow: 0 0 0 3px rgba(99, 102, 241, 0.1);
}

.form-group input::placeholder,
.form-group textarea::placeholder {
  color: var(--text-tertiary);
}

.form-actions {
  display: flex;
  gap: var(--space-md);
  margin-top: var(--space-xl);
}

/* Buttons */
.btn {
  padding: var(--space-md) var(--space-xl);
  border: 1.5px solid var(--border);
  border-radius: var(--radius-sm);
  font-size: var(--text-base);
  font-weight: var(--font-medium);
  cursor: pointer;
  background: var(--card);
  color: var(--text);
  transition: all var(--transition-fast);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-xs);
  white-space: nowrap;
}

.btn:hover {
  background: var(--bg-secondary);
  border-color: var(--text-tertiary);
}

.btn:active {
  transform: scale(0.97);
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
}

.btn-primary {
  background: var(--primary);
  color: var(--text-inverse);
  border-color: var(--primary);
}

.btn-primary:hover {
  background: var(--primary-dark);
  border-color: var(--primary-dark);
  box-shadow: var(--shadow-md);
}

.btn-outline {
  background: transparent;
  border-color: var(--primary);
  color: var(--primary);
}

.btn-outline:hover {
  background: var(--primary-bg);
}

.btn-danger {
  color: var(--danger);
  border-color: var(--danger);
}

.btn-danger:hover {
  background: var(--danger-bg);
}

.btn-sm {
  padding: var(--space-xs) var(--space-md);
  font-size: var(--text-sm);
  border-radius: var(--radius-xs);
}

.btn-icon {
  width: 36px;
  height: 36px;
  padding: 0;
  border-radius: var(--radius-full);
}

/* Links */
a {
  color: var(--primary);
  text-decoration: none;
  transition: color var(--transition-fast);
}

a:hover {
  color: var(--primary-dark);
}

/* Scrollbar styling */
::-webkit-scrollbar {
  width: 6px;
}

::-webkit-scrollbar-track {
  background: transparent;
}

::-webkit-scrollbar-thumb {
  background: var(--text-tertiary);
  border-radius: 3px;
}

::-webkit-scrollbar-thumb:hover {
  background: var(--text-secondary);
}

/* Focus visible for accessibility */
:focus-visible {
  outline: 2px solid var(--primary);
  outline-offset: 2px;
}

/* Selection */
::selection {
  background: rgba(99, 102, 241, 0.2);
  color: var(--text);
}
</style>
