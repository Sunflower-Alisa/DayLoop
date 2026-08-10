import { createApp } from 'vue'
import App from './App.vue'
import router from './router'
import './styles/english.css'

const app = createApp(App)
app.use(router)
app.mount('#app')

if ('serviceWorker' in navigator) {
  navigator.serviceWorker.register('/sw.js')
}