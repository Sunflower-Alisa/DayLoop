const express = require('express');
const router = express.Router();
const db = require('../database');
const { getUserId } = require('../middleware/auth');

function getUserIdOrZero(req) {
  return getUserId(req) || 0;
}

router.get('/', (req, res) => {
  const userId = getUserIdOrZero(req);
  const totalTasks = db.prepare('SELECT COUNT(*) as count FROM tasks WHERE user_id = ?').get(userId);
  const completedTasks = db.prepare("SELECT COUNT(*) as count FROM tasks WHERE user_id = ? AND status = 'completed'").get(userId);
  const cancelledTasks = db.prepare("SELECT COUNT(*) as count FROM tasks WHERE user_id = ? AND status = 'cancelled'").get(userId);
  const inProgressTasks = db.prepare("SELECT COUNT(*) as count FROM tasks WHERE user_id = ? AND status = 'in_progress'").get(userId);
  const plannedTasks = db.prepare("SELECT COUNT(*) as count FROM tasks WHERE user_id = ? AND status = 'planned'").get(userId);

  const categoryStats = db.prepare("SELECT category, COUNT(*) as count FROM tasks WHERE user_id = ? AND category != '' GROUP BY category ORDER BY count DESC").all(userId);
  const dailyStats = db.prepare("SELECT date, COUNT(*) as total, SUM(CASE WHEN status = 'completed' THEN 1 ELSE 0 END) as completed FROM tasks WHERE user_id = ? GROUP BY date ORDER BY date DESC LIMIT 30").all(userId);
  const totalNotes = db.prepare('SELECT COUNT(*) as count FROM notes WHERE user_id = ?').get(userId);
  const totalReviews = db.prepare('SELECT COUNT(*) as count FROM daily_reviews WHERE user_id = ?').get(userId);
  const totalTemplates = db.prepare('SELECT COUNT(*) as count FROM recurring_templates WHERE user_id = ?').get(userId);

  const weeklyStats = db.prepare(`
    SELECT
      strftime('%Y-%W', date) as week,
      COUNT(*) as total,
      SUM(CASE WHEN status = 'completed' THEN 1 ELSE 0 END) as completed
    FROM tasks
    WHERE user_id = ?
    GROUP BY strftime('%Y-%W', date)
    ORDER BY week DESC
    LIMIT 12
  `).all(userId);

  res.json({
    totalTasks: totalTasks.count,
    completedTasks: completedTasks.count,
    cancelledTasks: cancelledTasks.count,
    inProgressTasks: inProgressTasks.count,
    plannedTasks: plannedTasks.count,
    completionRate: totalTasks.count > 0 ? Math.round(completedTasks.count / totalTasks.count * 100) : 0,
    totalNotes: totalNotes.count,
    totalReviews: totalReviews.count,
    weeklyStats,
  });
});

module.exports = router;