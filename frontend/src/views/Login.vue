<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '../api'
import { auth } from '../store/auth'

const router = useRouter()
const username = ref('')
const password = ref('')
const error = ref('')
const loading = ref(false)

async function handleLogin() {
  error.value = ''
  if (!username.value || !password.value) {
    error.value = '请输入用户名和密码'
    return
  }
  loading.value = true
  try {
    const res = await api.login(username.value, password.value)
    auth.setToken(res.token)
    auth.setUser(res.user)
    router.push('/')
  } catch (e: any) {
    error.value = e.message || '登录失败'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-page">
    <div class="auth-card">
      <h1 class="logo">DayLoop</h1>
      <p class="subtitle">登录以继续</p>
      <form @submit.prevent="handleLogin">
        <div class="field">
          <label>用户名</label>
          <input v-model="username" type="text" placeholder="输入用户名" autocomplete="username" />
        </div>
        <div class="field">
          <label>密码</label>
          <input v-model="password" type="password" placeholder="输入密码" autocomplete="current-password" />
        </div>
        <p v-if="error" class="error">{{ error }}</p>
        <button type="submit" class="btn-primary" :disabled="loading">
          {{ loading ? '登录中...' : '登录' }}
        </button>
      </form>
      <p class="switch">
        还没有账号？<router-link to="/register">去注册</router-link>
      </p>
    </div>
  </div>
</template>

<style scoped>
.login-page {
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
