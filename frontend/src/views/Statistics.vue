<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { api } from '../api'

const stats = ref({
  totalTasks: 0,
  completedTasks: 0,
  cancelledTasks: 0,
  inProgressTasks: 0,
  plannedTasks: 0,
  completionRate: 0,
  totalNotes: 0,
  totalReviews: 0,
  totalPlannedDuration: 0,
  totalActualDuration: 0,
  weeklyStats: [] as { week: string; total: number; completed: number }[],
})

const loading = ref(true)

onMounted(async () => {
  try {
    stats.value = await api.getStats()
  } catch (e) {
    // ignore
  }
  loading.value = false
})

function maxCompleted(stats: { completed: number }[]): number {
  return Math.max(...stats.map(s => s.completed), 1)
}

function formatDuration(minutes: number): string {
  if (minutes <= 0) return '0m'
  const h = Math.floor(minutes / 60)
  const m = minutes % 60
  return h > 0 ? `${h}h ${m}m` : `${m}m`
}
</script>

<template>
  <div class="stats-page">
    <div class="page-header">
      <h2>📊 数据统计</h2>
    </div>

    <div v-if="loading" class="loading">加载中...</div>

    <template v-else>
      <div class="stats-grid">
        <div class="stat-card primary">
          <span class="stat-value">{{ stats.totalTasks }}</span>
          <span class="stat-label">总任务数</span>
        </div>
        <div class="stat-card success">
          <span class="stat-value">{{ stats.completedTasks }}</span>
          <span class="stat-label">已完成</span>
        </div>
        <div class="stat-card warning">
          <span class="stat-value">{{ stats.inProgressTasks }}</span>
          <span class="stat-label">进行中</span>
        </div>
        <div class="stat-card danger">
          <span class="stat-value">{{ stats.cancelledTasks }}</span>
          <span class="stat-label">已取消</span>
        </div>
        <div class="stat-card info">
          <span class="stat-value">{{ stats.completionRate }}%</span>
          <span class="stat-label">完成率</span>
        </div>
        <div class="stat-card">
          <span class="stat-value">{{ stats.totalNotes }}</span>
          <span class="stat-label">备忘录</span>
        </div>
        <div class="stat-card">
          <span class="stat-value">{{ stats.totalReviews }}</span>
          <span class="stat-label">复盘记录</span>
        </div>
        <div class="stat-card duration">
          <span class="stat-value">{{ formatDuration(stats.totalPlannedDuration) }}</span>
          <span class="stat-label">计划时长</span>
        </div>
        <div class="stat-card duration-actual">
          <span class="stat-value">{{ formatDuration(stats.totalActualDuration) }}</span>
          <span class="stat-label">实际耗时</span>
        </div>
      </div>

      <div class="chart-section">
        <h3>📊 周完成趋势</h3>
        <div class="bar-chart">
          <div v-for="w in stats.weeklyStats" :key="w.week" class="bar-row">
            <span class="bar-label">{{ w.week.slice(-2) }}周</span>
            <div class="bar-track">
              <div class="bar-fill" :style="{ width: (w.total > 0 ? w.completed / w.total * 100 : 0) + '%' }"></div>
            </div>
            <span class="bar-nums">{{ w.completed }}/{{ w.total }}</span>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.stats-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.page-header h2 {
  font-size: 20px;
}

.loading {
  text-align: center;
  padding: 60px 20px;
  color: var(--text-secondary);
}

.stats-grid {
  display: grid;
  grid-template-columns: 1fr 1fr 1fr;
  gap: 10px;
}

.stat-card {
  background: var(--card);
  border-radius: var(--radius);
  padding: 16px 8px;
  text-align: center;
  box-shadow: 0 1px 3px rgba(0,0,0,0.06);
  transition: transform 0.2s, box-shadow 0.2s;
  position: relative;
  overflow: hidden;
}

.stat-card::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 3px;
  background: var(--primary);
  opacity: 0.3;
}

.stat-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0,0,0,0.1);
}

.stat-value {
  display: block;
  font-size: 22px;
  font-weight: 700;
  color: var(--primary);
}

.stat-label {
  display: block;
  font-size: 11px;
  color: var(--text-secondary);
  margin-top: 6px;
  font-weight: 500;
}

.stat-card.success::before { background: var(--success); }
.stat-card.warning::before { background: var(--warning); }
.stat-card.danger::before { background: var(--danger); }
.stat-card.info::before { background: var(--primary-light); }
.stat-card.duration::before { background: #8b5cf6; }
.stat-card.duration-actual::before { background: #06b6d4; }

.stat-card.success .stat-value { color: var(--success); }
.stat-card.warning .stat-value { color: var(--warning); }
.stat-card.danger .stat-value { color: var(--danger); }
.stat-card.info .stat-value { color: var(--primary-light); }
.stat-card.duration .stat-value { color: #8b5cf6; }
.stat-card.duration-actual .stat-value { color: #06b6d4; }

.chart-section {
  background: var(--card);
  border-radius: var(--radius);
  padding: 20px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.06);
  transition: box-shadow 0.2s;
}

.chart-section:hover {
  box-shadow: 0 4px 12px rgba(0,0,0,0.1);
}

.chart-section h3 {
  font-size: 15px;
  margin-bottom: 16px;
  font-weight: 600;
}

.bar-chart {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.bar-row {
  display: flex;
  align-items: center;
  gap: 10px;
}

.bar-label {
  width: 50px;
  font-size: 12px;
  color: var(--text-secondary);
  text-align: right;
  flex-shrink: 0;
  font-weight: 500;
}

.bar-track {
  flex: 1;
  height: 22px;
  background: var(--bg);
  border-radius: 11px;
  overflow: hidden;
}

.bar-fill {
  height: 100%;
  background: linear-gradient(90deg, var(--primary), var(--primary-light));
  border-radius: 11px;
  transition: width 0.6s ease;
  min-width: 4px;
}

.bar-nums {
  width: 60px;
  font-size: 12px;
  color: var(--text-secondary);
  text-align: right;
  flex-shrink: 0;
  font-weight: 500;
}

@media (max-width: 400px) {
  .stats-grid {
    grid-template-columns: 1fr 1fr;
  }
}
</style>
