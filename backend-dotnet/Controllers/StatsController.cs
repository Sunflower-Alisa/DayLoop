using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DayLoop.Api.Data;
using DayLoop.Api.Models;
using DayLoop.Api.Services;

namespace DayLoop.Api.Controllers;

[ApiController]
[Route("api/stats")]
public class StatsController : ControllerBase
{
    private long GetUserId()
    {
        return UserIdFilter.GetUserId(Request) ?? 0;
    }

    [HttpGet]
    public IActionResult GetStats()
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();

        var totalTasks = Scalar(conn, "SELECT COUNT(*) FROM tasks WHERE user_id = " + userId);
        var completedTasks = Scalar(conn, "SELECT COUNT(*) FROM tasks WHERE user_id = " + userId + " AND status = 'completed'");
        var cancelledTasks = Scalar(conn, "SELECT COUNT(*) FROM tasks WHERE user_id = " + userId + " AND status = 'cancelled'");
        var inProgressTasks = Scalar(conn, "SELECT COUNT(*) FROM tasks WHERE user_id = " + userId + " AND status = 'in_progress'");
        var plannedTasks = Scalar(conn, "SELECT COUNT(*) FROM tasks WHERE user_id = " + userId + " AND status = 'planned'");
        var totalNotes = Scalar(conn, "SELECT COUNT(*) FROM notes WHERE user_id = " + userId);
        var totalReviews = Scalar(conn, "SELECT COUNT(*) FROM daily_reviews WHERE user_id = " + userId);

        var weeklyStats = new List<WeeklyStat>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT strftime('%Y-%W', date) as week,
                       COUNT(*) as total,
                       SUM(CASE WHEN status = 'completed' THEN 1 ELSE 0 END) as completed
                FROM tasks
                WHERE user_id = @uid
                GROUP BY strftime('%Y-%W', date)
                ORDER BY week DESC
                LIMIT 12
            """;
            cmd.Parameters.AddWithValue("@uid", userId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                weeklyStats.Add(new WeeklyStat
                {
                    Week = reader.GetString(0),
                    Total = reader.GetInt64(1),
                    Completed = reader.GetInt64(2),
                });
            }
        }

        var total = (long)totalTasks;
        return Ok(new StatsResponse
        {
            TotalTasks = total,
            CompletedTasks = (long)completedTasks,
            CancelledTasks = (long)cancelledTasks,
            InProgressTasks = (long)inProgressTasks,
            PlannedTasks = (long)plannedTasks,
            CompletionRate = total > 0 ? (int)Math.Round((double)(long)completedTasks / total * 100) : 0,
            TotalNotes = (long)totalNotes,
            TotalReviews = (long)totalReviews,
            WeeklyStats = weeklyStats,
        });
    }

    private static object Scalar(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar()!;
    }
}
