import { createRouter, createWebHashHistory } from 'vue-router'
import { auth } from '../store/auth'
import Home from '../views/Home.vue'
import DailyPlan from '../views/DailyPlan.vue'
import Review from '../views/Review.vue'
import History from '../views/History.vue'
import Achievements from '../views/Achievements.vue'
import AchievementDetail from '../views/AchievementDetail.vue'
import Notes from '../views/Notes.vue'
import NoteDetail from '../views/NoteDetail.vue'
import CategoryManage from '../views/CategoryManage.vue'
import Statistics from '../views/Statistics.vue'
import Login from '../views/Login.vue'
import Register from '../views/Register.vue'
import Profile from '../views/Profile.vue'
import RecurringTemplates from '../views/RecurringTemplates.vue'
import Questions from '../views/Questions.vue'
import QuestionDetail from '../views/QuestionDetail.vue'

const routes = [
  { path: '/login', name: 'login', component: Login, meta: { guest: true } },
  { path: '/register', name: 'register', component: Register, meta: { guest: true } },
  { path: '/profile', name: 'profile', component: Profile, meta: { auth: true } },
  { path: '/', name: 'home', component: Home, meta: { auth: true } },
  { path: '/plan', name: 'plan', component: DailyPlan, meta: { auth: true } },
  { path: '/review', name: 'review', component: Review, meta: { auth: true } },
  { path: '/history', name: 'history', component: History, meta: { auth: true } },
  { path: '/achievements', name: 'achievements', component: Achievements, meta: { auth: true } },
  { path: '/achievements/:id', name: 'achievement-detail', component: AchievementDetail, meta: { auth: true } },
  { path: '/notes', name: 'notes', component: Notes, meta: { auth: true } },
  { path: '/notes/:id', name: 'note-detail', component: NoteDetail, meta: { auth: true } },
  { path: '/notes/new', name: 'note-new', component: NoteDetail, meta: { auth: true } },
  { path: '/notes/categories', name: 'note-categories', component: CategoryManage, meta: { auth: true } },
  { path: '/statistics', name: 'statistics', component: Statistics, meta: { auth: true } },
  { path: '/templates', name: 'templates', component: RecurringTemplates, meta: { auth: true } },
  { path: '/questions', name: 'questions', component: Questions, meta: { auth: true } },
  { path: '/questions/categories', name: 'question-categories', component: CategoryManage, meta: { auth: true } },
  { path: '/questions/new', name: 'question-new', component: QuestionDetail, meta: { auth: true } },
  { path: '/questions/:id', name: 'question-detail', component: QuestionDetail, meta: { auth: true } },
]

const router = createRouter({
  history: createWebHashHistory(),
  routes,
})

router.beforeEach((to) => {
  if (to.meta.auth && !auth.isLoggedIn) {
    return { name: 'login' }
  }
  if (to.meta.guest && auth.isLoggedIn) {
    return { name: 'home' }
  }
})

export default router
