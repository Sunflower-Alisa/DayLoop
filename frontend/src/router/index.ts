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
import Summary from '../views/Summary.vue'
import Calendar from '../views/Calendar.vue'
import EnglishHome from '../views/english/EnglishHome.vue'
import Words from '../views/english/Words.vue'
import WordLearn from '../views/english/WordLearn.vue'
import WordReview from '../views/english/WordReview.vue'
import WordDetail from '../views/english/WordDetail.vue'
import WrongWords from '../views/english/WrongWords.vue'
import WordBooks from '../views/english/WordBooks.vue'
import WordBookWords from '../views/english/WordBookWords.vue'
import Scenarios from '../views/english/Scenarios.vue'
import ScenarioDetail from '../views/english/ScenarioDetail.vue'
import Speaking from '../views/english/Speaking.vue'
import SpeakingPractice from '../views/english/SpeakingPractice.vue'
import Clips from '../views/english/Clips.vue'
import ClipDetail from '../views/english/ClipDetail.vue'
import EnglishStatistics from '../views/english/EnglishStatistics.vue'
import AgentChat from '../views/AgentChat.vue'

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
  { path: '/summary', name: 'summary', component: Summary, meta: { auth: true } },
  { path: '/calendar', name: 'calendar', component: Calendar, meta: { auth: true } },
  { path: '/english', name: 'english', component: EnglishHome, meta: { auth: true } },
  { path: '/english/words', name: 'english-words', component: Words, meta: { auth: true } },
  { path: '/english/words/learn', name: 'english-words-learn', component: WordLearn, meta: { auth: true, fullscreen: true } },
  { path: '/english/words/review', name: 'english-words-review', component: WordReview, meta: { auth: true, fullscreen: true } },
  { path: '/english/words/wrong', name: 'english-words-wrong', component: WrongWords, meta: { auth: true } },
  { path: '/english/words/:id', name: 'english-word-detail', component: WordDetail, meta: { auth: true } },
  { path: '/english/wordbooks', name: 'english-wordbooks', component: WordBooks, meta: { auth: true } },
  { path: '/english/wordbooks/:id/words', name: 'english-wordbooks-words', component: WordBookWords, meta: { auth: true } },
  { path: '/english/scenarios', name: 'english-scenarios', component: Scenarios, meta: { auth: true } },
  { path: '/english/scenarios/:id', name: 'english-scenario-detail', component: ScenarioDetail, meta: { auth: true } },
  { path: '/english/speaking', name: 'english-speaking', component: Speaking, meta: { auth: true } },
  { path: '/english/speaking/:id', name: 'english-speaking-detail', component: SpeakingPractice, meta: { auth: true } },
  { path: '/english/clips', name: 'english-clips', component: Clips, meta: { auth: true } },
  { path: '/english/clips/:id', name: 'english-clip-detail', component: ClipDetail, meta: { auth: true } },
  { path: '/english/statistics', name: 'english-statistics', component: EnglishStatistics, meta: { auth: true } },
  { path: '/agent', name: 'agent', component: AgentChat, meta: { auth: true } },
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
