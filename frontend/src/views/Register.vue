<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '../api'
import { auth } from '../store/auth'

const router = useRouter()
const username = ref('')
const password = ref('')
const confirmPassword = ref('')
const error = ref('')
const loading = ref(false)

async function handleRegister() {
  error.value = ''
  if (!username.value || !password.value) {
    error.value = '请输入用户名和密码'
    return
  }
  if (password.value !== confirmPassword.value) {
    error.value = '两次密码不一致'
    return
  }
  loading.value = true
  try {
    const res = await api.register(username.value, password.value)
    auth.setToken(res.token)
    auth.setUser(res.user)
    router.push('/')
  } catch (e: any) {
    error.value = e.message || '注册失败'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="register-page">
    <div class="auth-card">
      <h1 class="logo">DayLoop</h1>
      <p class="subtitle">创建新账号</p>
      <form @submit.prevent="handleRegister">
        <div class="field">
          <label>用户名</label>
          <input v-model="username" type="text" placeholder="至少2个字符" autocomplete="username" />
        </div>
        <div class="field">
          <label>密码</label>
          <input v-model="password" type="password" placeholder="至少4个字符" autocomplete="new-password" />
        </div>
        <div class="field">
          <label>确认密码</label>
          <input v-model="confirmPassword" type="password" placeholder="再次输入密码" autocomplete="new-password" />
        </div>
        <p v-if="error" class="error">{{ error }}</p>
        <button type="submit" class="btn-primary" :disabled="loading">
          {{ loading ? '注册中...' : '注册' }}
        </button>
      </form>
      <p class="switch">
        已有账号？<router-link to="/login">去登录</router-link>
      </p>
    </div>
  </div>
</template>

<style scoped>
.register-page {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 80vh;
  padding: 20px;
}
.auth-card {
  background: var(--card);
  border-radius: var(--radius);
  padding: 32px 24px;
  width: 100%;
  max-width: 360px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.08);
  text-align: center;
}
.logo {
  font-size: 28px;
  color: var(--primary);
  margin-bottom: 4px;
}
.subtitle {
  color: var(--text-secondary);
  font-size: 14px;
  margin-bottom: 24px;
}
.field {
  margin-bottom: 16px;
  text-align: left;
}
.field label {
  display: block;
  font-size: 13px;
  color: var(--text-secondary);
  margin-bottom: 4px;
}
.field input {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: 8px;
  font-size: 15px;
  outline: none;
  transition: border-color 0.2s;
}
.field input:focus {
  border-color: var(--primary);
}
.btn-primary {
  width: 100%;
  padding: 12px;
  background: var(--primary);
  color: white;
  border: none;
  border-radius: 8px;
  font-size: 16px;
  cursor: pointer;
  margin-top: 8px;
}
.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
.error {
  color: var(--danger);
  font-size: 13px;
  margin: 8px 0;
}
.switch {
  margin-top: 16px;
  font-size: 13px;
  color: var(--text-secondary);
}
.switch a {
  color: var(--primary);
  text-decoration: none;
  font-weight: 600;
}
</style>
