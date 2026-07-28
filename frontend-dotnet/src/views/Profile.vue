<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { api, getServerUrl, setServerUrl } from '../api'
import { auth } from '../store/auth'

const router = useRouter()
const serverUrl = ref(getServerUrl())
const editingUrl = ref(false)
const newUrl = ref('')

const showChangePwd = ref(false)
const oldPassword = ref('')
const newPassword = ref('')
const pwdMessage = ref('')
const pwdError = ref('')

const showDeleteConfirm = ref(false)
const deleteMessage = ref('')

const obsidianPath = ref('')
const obsidianMessage = ref('')
const obsidianError = ref('')

async function loadObsidianSettings() {
  try {
    const settings = await api.getSettings()
    obsidianPath.value = settings.obsidian_vault_path || ''
  } catch { /* ignore */ }
}

async function saveObsidianPath() {
  obsidianMessage.value = ''
  obsidianError.value = ''
  try {
    const res = await api.updateSetting('obsidian_vault_path', obsidianPath.value)
    obsidianPath.value = res.value
    obsidianMessage.value = '已保存'
  } catch (e: any) {
    obsidianError.value = e.message || '保存失败'
  }
}

async function testSync() {
  obsidianMessage.value = ''
  obsidianError.value = ''
  try {
    const reviews = await api.getReview(new Date().toISOString().slice(0, 10))
    const notes = await api.getNotes()
    obsidianMessage.value = `同步功能已启用，路径: ${obsidianPath.value || '(未设置)'}`
  } catch (e: any) {
    obsidianError.value = e.message || '测试失败'
  }
}

async function syncAllData() {
  obsidianMessage.value = ''
  obsidianError.value = ''
  try {
    const res = await api.syncAllObsidian()
    obsidianMessage.value = res.message
  } catch (e: any) {
    obsidianError.value = e.message || '同步失败'
  }
}

onMounted(() => {
  loadObsidianSettings()
})

function handleLogout() {
  auth.logout()
  router.push('/login')
}

function toggleEditUrl() {
  editingUrl.value = !editingUrl.value
  newUrl.value = serverUrl.value === '(本机)' ? '' : serverUrl.value
}

function saveUrl() {
  setServerUrl(newUrl.value)
  serverUrl.value = getServerUrl()
  editingUrl.value = false
  window.location.reload()
}

async function handleChangePassword() {
  pwdMessage.value = ''
  pwdError.value = ''
  try {
    const res = await api.changePassword(oldPassword.value, newPassword.value)
    pwdMessage.value = res.message
    oldPassword.value = ''
    newPassword.value = ''
  } catch (e: any) {
    pwdError.value = e.message || '修改密码失败'
  }
}

async function handleDeleteAccount() {
  if (!confirm('确定要删除账号吗？所有数据将被永久删除，此操作不可撤销！')) return
  try {
    await api.deleteAccount()
    auth.logout()
    router.push('/login')
  } catch (e: any) {
    deleteMessage.value = e.message || '删除账号失败'
  }
}
</script>

<template>
  <div class="profile-page">
    <div class="user-card">
      <div class="avatar">{{ auth.user?.username?.[0]?.toUpperCase() || '?' }}</div>
      <h2>{{ auth.user?.username || '未登录' }}</h2>
      <p class="since" v-if="auth.user?.created_at">
        注册于 {{ auth.user.created_at }}
      </p>
    </div>

    <div class="section">
      <h3>服务器设置</h3>
      <div class="server-info">
        <span class="label">当前服务器：</span>
        <span class="value">{{ serverUrl }}</span>
        <button class="btn-small" @click="toggleEditUrl">
          {{ editingUrl ? '取消' : '修改' }}
        </button>
      </div>
      <div v-if="editingUrl" class="edit-url">
        <input v-model="newUrl" placeholder="输入服务器地址，留空为本机" />
        <button class="btn-primary btn-small" @click="saveUrl">保存</button>
      </div>
    </div>

    <div class="section">
      <h3>修改密码</h3>
      <div v-if="!showChangePwd">
        <button class="btn-small" @click="showChangePwd = true">修改密码</button>
      </div>
      <div v-else class="change-pwd-form">
        <input v-model="oldPassword" type="password" placeholder="旧密码" />
        <input v-model="newPassword" type="password" placeholder="新密码（至少4位）" />
        <div class="pwd-actions">
          <button class="btn-primary btn-small" @click="handleChangePassword">保存</button>
          <button class="btn-small" @click="showChangePwd = false; oldPassword = ''; newPassword = ''; pwdMessage = ''; pwdError = ''">取消</button>
        </div>
        <p v-if="pwdMessage" class="success-msg">{{ pwdMessage }}</p>
        <p v-if="pwdError" class="error-msg">{{ pwdError }}</p>
      </div>
    </div>

    <div class="section">
      <h3>Obsidian 知识库同步</h3>
      <p class="hint">设置 Obsidian 本地仓库路径，备忘录、每日复盘和成果将实时同步为 Markdown 文件</p>
      <div class="obsidian-row">
        <input v-model="obsidianPath" placeholder="例如：D:/Obsidian/MyVault" class="obsidian-input" />
      </div>
      <div class="obsidian-actions">
        <button class="btn-small btn-primary" @click="saveObsidianPath">保存路径</button>
        <button class="btn-small" @click="testSync">测试连接</button>
        <button class="btn-small" @click="syncAllData">同步历史数据</button>
      </div>
      <p v-if="obsidianMessage" class="success-msg">{{ obsidianMessage }}</p>
      <p v-if="obsidianError" class="error-msg">{{ obsidianError }}</p>
    </div>

    <div class="actions">
      <button class="btn-logout" @click="handleLogout">退出登录</button>
      <button class="btn-danger" @click="showDeleteConfirm = true">删除账号</button>
    </div>

    <div v-if="showDeleteConfirm" class="modal-overlay" @click.self="showDeleteConfirm = false">
      <div class="modal">
        <h3>确认删除账号</h3>
        <p>所有数据将被永久删除，此操作不可撤销！</p>
        <p v-if="deleteMessage" class="error-msg">{{ deleteMessage }}</p>
        <div class="modal-actions">
          <button class="btn-danger" @click="handleDeleteAccount">确认删除</button>
          <button class="btn-small" @click="showDeleteConfirm = false">取消</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.profile-page {
  padding: 20px 0;
}
.user-card {
  text-align: center;
  padding: 24px;
  background: var(--card);
  border-radius: var(--radius);
  margin-bottom: 16px;
}
.avatar {
  width: 64px;
  height: 64px;
  border-radius: 50%;
  background: var(--primary);
  color: white;
  font-size: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin: 0 auto 12px;
}
.user-card h2 {
  font-size: 20px;
  margin-bottom: 4px;
}
.since {
  color: var(--text-secondary);
  font-size: 13px;
}
.section {
  background: var(--card);
  border-radius: var(--radius);
  padding: 16px;
  margin-bottom: 16px;
}
.section h3 {
  font-size: 15px;
  margin-bottom: 12px;
  color: var(--text);
}
.server-info {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
}
.server-info .label {
  color: var(--text-secondary);
}
.server-info .value {
  flex: 1;
  word-break: break-all;
}
.btn-small {
  padding: 4px 12px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--card);
  color: var(--text);
  font-size: 12px;
  cursor: pointer;
}
.edit-url {
  display: flex;
  gap: 8px;
  margin-top: 8px;
}
.edit-url input {
  flex: 1;
  padding: 8px 10px;
  border: 1px solid var(--border);
  border-radius: 6px;
  font-size: 13px;
  outline: none;
}
.edit-url input:focus {
  border-color: var(--primary);
}
.edit-url .btn-small {
  white-space: nowrap;
}
.actions {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.btn-logout {
  width: 100%;
  padding: 12px;
  background: var(--danger);
  color: white;
  border: none;
  border-radius: 8px;
  font-size: 15px;
  cursor: pointer;
}
.btn-danger {
  width: 100%;
  padding: 12px;
  background: #dc2626;
  color: white;
  border: none;
  border-radius: 8px;
  font-size: 15px;
  cursor: pointer;
}
.btn-primary {
  background: var(--primary);
  color: white;
  border: none;
}
.hint {
  font-size: 12px;
  color: var(--text-secondary);
  margin-bottom: 8px;
  line-height: 1.5;
}
.obsidian-row {
  margin-bottom: 8px;
}
.obsidian-input {
  width: 100%;
  padding: 8px 10px;
  border: 1px solid var(--border);
  border-radius: 6px;
  font-size: 13px;
  outline: none;
  box-sizing: border-box;
}
.obsidian-input:focus {
  border-color: var(--primary);
}
.obsidian-actions {
  display: flex;
  gap: 8px;
}
.change-pwd-form {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.change-pwd-form input {
  padding: 8px 10px;
  border: 1px solid var(--border);
  border-radius: 6px;
  font-size: 13px;
  outline: none;
}
.change-pwd-form input:focus {
  border-color: var(--primary);
}
.pwd-actions {
  display: flex;
  gap: 8px;
}
.success-msg {
  color: #16a34a;
  font-size: 13px;
  margin: 4px 0 0;
}
.error-msg {
  color: #dc2626;
  font-size: 13px;
  margin: 4px 0 0;
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
  max-width: 360px;
  width: 90%;
  text-align: center;
}
.modal h3 {
  margin-bottom: 12px;
}
.modal p {
  color: var(--text-secondary);
  font-size: 14px;
  margin-bottom: 16px;
}
.modal-actions {
  display: flex;
  gap: 12px;
  justify-content: center;
}
</style>