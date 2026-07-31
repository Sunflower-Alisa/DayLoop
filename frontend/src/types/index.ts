export interface Task {
  id: number
  date: string
  title: string
  start_time: string
  end_time: string
  planned_duration: number
  actual_duration: number | null
  actual_start: string | null
  actual_end: string | null
  status: 'planned' | 'in_progress' | 'completed' | 'cancelled'
  category: string
  priority: 1 | 2 | 3
  note: string
  is_recurring: boolean
  is_planned: boolean
  recurring_template_id: number | null
  achievement: string
  note_id: number | null
  sync_enabled: boolean
  planned_days: number
  overall_status: 'pending' | 'completed'
  created_at: string
  updated_at: string
}

export interface DailyReview {
  id: number
  date: string
  content: string
  created_at: string
  updated_at: string
}

export interface RecurringTemplate {
  id: number
  title: string
  start_time: string
  end_time: string
  planned_duration: number
  category: string
  priority: number
  note: string
  created_at: string
  recurrence_type: string
  recurrence_days: string
  recurring_enabled: boolean
  sync_enabled: boolean
  planned_days: number
}

export interface Question {
  id: number
  title: string
  content: string
  answer: string
  answer_source: 'self' | 'ai' | 'web'
  category: string
  tags: string
  task_id: number | null
  linked_tasks: Array<{
    id: number
    title: string
    date: string
    start_time: string
    end_time: string
    status: string
    category: string
  }>
  created_at: string
  updated_at: string
}

export interface StatsResponse {
  totalTasks: number
  completedTasks: number
  cancelledTasks: number
  inProgressTasks: number
  plannedTasks: number
  completionRate: number
  totalNotes: number
  totalReviews: number
  totalPlannedDuration: number
  totalActualDuration: number
  weeklyStats: { week: string; total: number; completed: number }[]
}

export interface Summary {
  id: number
  type: string
  period_key: string
  content: string
  auto_summary: string
  user_id: number
  created_at: string
  updated_at: string
}

export interface TaskSummary {
  id: number
  title: string
  content: string
  user_id: number
  created_at: string
  updated_at: string
}

export interface Note {
  id: number
  title: string
  content: string
  category: string
  tags: string
  task_id: number | null
  linked_tasks: Array<{
    id: number
    title: string
    date: string
    start_time: string
    end_time: string
    status: string
    category: string
  }>
  created_at: string
  updated_at: string
}
