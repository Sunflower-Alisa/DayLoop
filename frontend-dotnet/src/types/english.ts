export interface WordBook {
  id: number
  name: string
  level: string
  description: string
  cover_color: string
  is_default: boolean
  word_count: number
  learned_count: number
  mastered_count: number
  daily_goal: number
  created_at: string
}

export interface Word {
  id: number
  word: string
  phonetic: string
  pos: string
  meaning: string
  example_en: string
  example_cn: string
  image_url: string
  audio_url: string
  book_id: number
  status: 'new' | 'learning' | 'reviewing' | 'mastered'
  stage: number
  in_wrong_book: boolean
}

export interface DailyWordTask {
  new_words: Word[]
  review_words: Word[]
  new_goal: number
  new_done: number
  review_done: number
  has_book: boolean
}

export interface Scenario {
  id: number
  title: string
  category: string
  level: number
  icon: string
  description: string
  line_count: number
  mastered: boolean
  created_at: string
}

export interface ScenarioLine {
  id: number
  scenario_id: number
  order: number
  speaker: string
  en_text: string
  cn_text: string
  audio_url: string
}

export interface ScenarioPhrase {
  id: number
  scenario_id: number
  phrase: string
  meaning: string
  example_en: string
  example_cn: string
}

export interface ScenarioQuiz {
  id: number
  scenario_id: number
  question_en: string
  question_cn: string
  options: string[]
  answer_index: number
  explanation: string
}

export interface ScenarioDetail {
  scenario: Scenario
  lines: ScenarioLine[]
  phrases: ScenarioPhrase[]
  quizzes: ScenarioQuiz[]
}

export interface SpeakingLine {
  en: string
  cn: string
  audio_url: string
}

export interface SpeakingTopic {
  id: number
  title: string
  category: string
  level: string
  lines: SpeakingLine[]
  source_type: string
  source_id: number
  best_score: number
  practice_count: number
}

export interface SpeakingRecord {
  id: number
  topic_id: number
  line_index: number
  accuracy: number
  fluency: number
  completeness: number
  overall: number
  created_at: string
}

export interface VideoClip {
  id: number
  title: string
  source: string
  cover_url: string
  path: string
  duration: number
  level: string
  tags: string
  description: string
  line_count: number
  learned_count: number
}

export interface ClipLine {
  id: number
  clip_id: number
  order: number
  speaker: string
  en_text: string
  cn_text: string
  start_time: number
  end_time: number
}

export interface EnglishDashboard {
  streak: number
  checked_in_today: boolean
  new_goal: number
  new_done: number
  review_done: number
  today_seconds: number
  week_seconds: number
  total_seconds: number
  total_words: number
  mastered_words: number
  learning_words: number
  wrong_count: number
  scenario_count: number
  scenario_mastered: number
  speaking_avg: number
  clip_count: number
}

export interface SessionStats {
  today: number
  week: number
  total: number
}

export interface LearnResult {
  word_id: number
  correct: boolean
  is_review: boolean
  know?: boolean
}
