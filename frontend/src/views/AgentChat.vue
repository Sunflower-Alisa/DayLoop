<script setup lang="ts">
import { ref, nextTick, onMounted } from 'vue'
import { api } from '../api'

interface Msg {
  role: 'user' | 'agent'
  content: string
  intent?: string
  time: string
  error?: boolean
}

const messages = ref<Msg[]>([])
const input = ref('')
const sending = ref(false)
const agentOnline = ref<boolean | null>(null)
const sessionId = ref('')

function now(): string {
  const d = new Date()
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

function pushUser(text: string) {
  messages.value.push({ role: 'user', content: text, time: now() })
}

function pushAgent(text: string, intent?: string, error = false) {
  messages.value.push({ role: 'agent', content: text, intent, time: now(), error })
}

const SUGGESTIONS = [
  '帮我优化我的简历',
  '看看今天有什么任务',
  '帮我搜索 AI Agent 岗位',
  '总结一下我的学习记录',
]

async function send(text?: string) {
  const msg = (text ?? input.value).trim()
  if (!msg || sending.value) return
  input.value = ''
  sending.value = true
  pushUser(msg)
  scrollToBottom()
  try {
    const res = await api.agentChat(msg, sessionId.value)
    sessionId.value = res.session_id || sessionId.value
    pushAgent(res.message || '处理完成', res.intent)
  } catch (e) {
    pushAgent('Agent 服务暂不可用，请确认 AI Agent Service 已启动（端口 5173）。', undefined, true)
  } finally {
    sending.value = false
    scrollToBottom()
  }
}

async function checkStatus() {
  try {
    const s = await api.agentStatus()
    agentOnline.value = !!s && s.status !== 'error'
  } catch {
    agentOnline.value = false
  }
}

function clearChat() {
  sessionId.value = ''
  messages.value = []
}

function scrollToBottom() {
  nextTick(() => {
    const box = document.querySelector('.chat-body')
    if (box) box.scrollTop = box.scrollHeight
  })
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault()
    send()
  }
}

onMounted(checkStatus)
</script>

<template>
  <div class="agent-page">
    <div class="agent-header">
      <div>
        <h2 class="agent-title">🤖 AI Agent 助手</h2>
        <p class="agent-subtitle">求职 / 简历 / 任务 / 学习分析</p>
      </div>
      <div class="agent-header-right">
        <span class="agent-status" :class="{ online: agentOnline === true, offline: agentOnline === false }">
          {{ agentOnline === null ? '检测中...' : agentOnline ? 'Agent 在线' : 'Agent 离线' }}
        </span>
        <button class="agent-clear" @click="clearChat" :disabled="messages.length === 0">清空</button>
      </div>
    </div>

    <div class="agent-card">
      <div class="chat-body">
        <div v-if="messages.length === 0" class="chat-empty">
          <div class="chat-empty-icon">🤖</div>
          <p class="chat-empty-text">我是你的 AI 助手，可以帮你：</p>
          <ul class="chat-empty-list">
            <li>📄 优化简历、更新技能</li>
            <li>💼 搜索招聘岗位、评估匹配度</li>
            <li>📋 查询今日任务、学习记录</li>
            <li>🔍 检索行业知识</li>
          </ul>
        </div>

        <div v-for="(m, i) in messages" :key="i" class="msg" :class="m.role">
          <div class="msg-avatar">{{ m.role === 'user' ? '我' : 'AI' }}</div>
          <div class="msg-bubble" :class="{ error: m.error }">
            <div class="msg-intent" v-if="m.intent">{{ m.intent }}</div>
            <div class="msg-content" style="white-space: pre-wrap">{{ m.content }}</div>
            <div class="msg-time">{{ m.time }}</div>
          </div>
        </div>

        <div v-if="sending" class="msg agent">
          <div class="msg-avatar">AI</div>
          <div class="msg-bubble">
            <span class="typing"><i></i><i></i><i></i></span>
          </div>
        </div>
      </div>

      <div class="chat-suggest" v-if="messages.length === 0">
        <button v-for="s in SUGGESTIONS" :key="s" class="suggest-chip" @click="send(s)">{{ s }}</button>
      </div>

      <div class="chat-input-row">
        <textarea
          v-model="input"
          class="chat-input"
          rows="1"
          placeholder="输入你的问题，Enter 发送，Shift+Enter 换行"
          @keydown="onKeydown"
        ></textarea>
        <button class="chat-send" @click="send()" :disabled="sending || !input.trim()">发送</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.agent-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  height: calc(100vh - 56px - 32px);
}

.agent-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
}

.agent-title {
  margin: 0;
  font-size: 20px;
  font-weight: 700;
}

.agent-subtitle {
  margin: 4px 0 0;
  font-size: 13px;
  color: var(--text-secondary);
}

.agent-header-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.agent-status {
  font-size: 12px;
  padding: 4px 10px;
  border-radius: 20px;
  background: var(--border);
  color: var(--text-secondary);
}

.agent-status.online {
  background: #dcfce7;
  color: #15803d;
}

.agent-status.offline {
  background: #fee2e2;
  color: #b91c1c;
}

.agent-clear {
  border: 1px solid var(--border);
  background: var(--card);
  color: var(--text);
  border-radius: 8px;
  padding: 5px 12px;
  cursor: pointer;
  font-size: 13px;
}

.agent-clear:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.agent-card {
  flex: 1;
  display: flex;
  flex-direction: column;
  background: var(--card);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  overflow: hidden;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.06);
}

.chat-body {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.chat-empty {
  margin: auto;
  text-align: center;
  color: var(--text-secondary);
}

.chat-empty-icon {
  font-size: 44px;
}

.chat-empty-text {
  font-size: 14px;
}

.chat-empty-list {
  text-align: left;
  font-size: 13px;
  line-height: 1.8;
  color: var(--text-secondary);
  padding-left: 20px;
}

.msg {
  display: flex;
  gap: 8px;
  max-width: 85%;
}

.msg.user {
  align-self: flex-end;
  flex-direction: row-reverse;
}

.msg-avatar {
  width: 28px;
  height: 28px;
  flex-shrink: 0;
  border-radius: 50%;
  background: var(--primary-bg);
  color: var(--primary);
  font-size: 12px;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
}

.msg.agent .msg-avatar {
  background: #f1f5f9;
  color: #334155;
}

.msg-bubble {
  background: #f1f5f9;
  border-radius: 12px;
  padding: 10px 12px;
  font-size: 14px;
  line-height: 1.6;
}

.msg.user .msg-bubble {
  background: var(--primary);
  color: #fff;
}

.msg-bubble.error {
  background: #fee2e2;
  color: #b91c1c;
}

.msg-intent {
  font-size: 11px;
  opacity: 0.7;
  margin-bottom: 4px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.msg-time {
  font-size: 11px;
  opacity: 0.6;
  margin-top: 4px;
  text-align: right;
}

.typing {
  display: inline-flex;
  gap: 4px;
  padding: 4px 2px;
}

.typing i {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--text-secondary);
  animation: blink 1.2s infinite;
}

.typing i:nth-child(2) { animation-delay: 0.2s; }
.typing i:nth-child(3) { animation-delay: 0.4s; }

@keyframes blink {
  0%, 80%, 100% { opacity: 0.3; }
  40% { opacity: 1; }
}

.chat-suggest {
  padding: 8px 16px;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  border-top: 1px solid var(--border);
}

.suggest-chip {
  border: 1px solid var(--border);
  background: var(--card);
  color: var(--text);
  border-radius: 16px;
  padding: 6px 14px;
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s;
}

.suggest-chip:hover {
  border-color: var(--primary);
  color: var(--primary);
}

.chat-input-row {
  display: flex;
  gap: 8px;
  padding: 12px 16px;
  border-top: 1px solid var(--border);
  background: var(--card);
}

.chat-input {
  flex: 1;
  border: 1px solid var(--border);
  border-radius: 10px;
  padding: 10px 12px;
  font-size: 14px;
  resize: none;
  outline: none;
  font-family: inherit;
  background: var(--card);
  color: var(--text);
}

.chat-input:focus {
  border-color: var(--primary);
}

.chat-send {
  border: none;
  background: var(--primary);
  color: #fff;
  border-radius: 10px;
  padding: 10px 20px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
}

.chat-send:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
