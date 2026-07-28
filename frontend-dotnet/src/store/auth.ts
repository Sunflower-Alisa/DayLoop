import { reactive } from 'vue'

const TOKEN_KEY = 'dayloop_token'
const USER_KEY = 'dayloop_user'

export interface User {
  id: number
  username: string
  created_at?: string
}

const state = reactive({
  token: localStorage.getItem(TOKEN_KEY) || '',
  user: JSON.parse(localStorage.getItem(USER_KEY) || 'null') as User | null,
})

export const auth = {
  get token() { return state.token },
  get user() { return state.user },
  get isLoggedIn() { return !!state.token },

  setToken(token: string) {
    state.token = token
    localStorage.setItem(TOKEN_KEY, token)
  },

  setUser(user: User) {
    state.user = user
    localStorage.setItem(USER_KEY, JSON.stringify(user))
  },

  logout() {
    state.token = ''
    state.user = null
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
  }
}
