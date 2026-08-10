using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DayLoop.Api.Data;
using DayLoop.Api.Models;
using DayLoop.Api.Services;

namespace DayLoop.Api.Controllers;

[ApiController]
[Route("api/english")]
public class EnglishController : ControllerBase
{
    private long GetUserId() => UserIdFilter.GetUserId(Request) ?? 0;
    private static string Today() => DateTime.Now.ToString("yyyy-MM-dd");
    private static string AddDays(int days) => DateTime.Now.AddDays(days).ToString("yyyy-MM-dd");

    [HttpGet("dashboard")]
    public IActionResult GetDashboard()
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        var today = Today();

        var dash = new EnglishDashboard();
        dash.Streak = ComputeStreak(conn, userId);
        dash.CheckedInToday = HasLog(conn, userId, today);
        dash.TotalWords = Count(conn, "SELECT COUNT(*) FROM word_progress WHERE user_id = @uid", userId);
        dash.MasteredWords = Count(conn, "SELECT COUNT(*) FROM word_progress WHERE user_id = @uid AND status = 'mastered'", userId);
        dash.LearningWords = Count(conn, "SELECT COUNT(*) FROM word_progress WHERE user_id = @uid AND status IN ('learning','reviewing')", userId);
        dash.WrongCount = Count(conn, "SELECT COUNT(*) FROM wrong_words WHERE user_id = @uid", userId);
        dash.ScenarioCount = Count(conn, "SELECT COUNT(*) FROM scenarios WHERE user_id = 0 OR user_id = @uid", userId);
        dash.ScenarioMastered = Count(conn, "SELECT COUNT(*) FROM scenario_progress WHERE user_id = @uid AND mastered = 1", userId);
        dash.ClipCount = Count(conn, "SELECT COUNT(*) FROM video_clips WHERE user_id = 0 OR user_id = @uid", userId);

        // word plan
        dash.NewGoal = DailyGoal(conn, userId);
        dash.NewDone = CountLog(conn, userId, today, "new");
        dash.ReviewDone = CountLog(conn, userId, today, "review");

        // durations
        dash.TodaySeconds = SumDuration(conn, userId, today, today);
        dash.WeekSeconds = SumDuration(conn, userId, AddDays(-6), today);
        dash.TotalSeconds = SumDuration(conn, userId, "", "");

        // speaking avg
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COALESCE(AVG(overall), 0) as avg_score,
                       COALESCE(MAX(overall), 0) as best
                FROM speaking_records WHERE user_id = @uid
            """;
            cmd.Parameters.AddWithValue("@uid", userId);
            using var r = cmd.ExecuteReader();
            if (r.Read())
                dash.SpeakingAvg = (int)Math.Round(r.IsDBNull(0) ? 0 : r.GetDouble(0));
        }

        return Ok(dash);
    }

    [HttpGet("streak")]
    public IActionResult GetStreak()
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        return Ok(new { streak = ComputeStreak(conn, userId) });
    }

    [HttpPost("sessions")]
    public IActionResult SaveSession([FromBody] StudySessionRequest req)
    {
        var userId = UserIdFilter.RequireUserId(Request);
        if (req.DurationSeconds <= 0)
            return Ok(new { ok = true });
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO study_sessions (user_id, date, module, start_time, end_time, duration_seconds) VALUES (@uid, @d, @m, @st, @et, @dur)";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@d", Today());
        cmd.Parameters.AddWithValue("@m", req.Module ?? "");
        cmd.Parameters.AddWithValue("@st", req.StartTime ?? "");
        cmd.Parameters.AddWithValue("@et", req.EndTime ?? "");
        cmd.Parameters.AddWithValue("@dur", req.DurationSeconds);
        cmd.ExecuteNonQuery();
        return Ok(new { ok = true });
    }

    [HttpGet("sessions")]
    public IActionResult GetSessions()
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        var today = Today();
        return Ok(new
        {
            today = SumDuration(conn, userId, today, today),
            week = SumDuration(conn, userId, AddDays(-6), today),
            total = SumDuration(conn, userId, "", ""),
        });
    }

    private int DailyGoal(SqliteConnection conn, long userId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM app_settings WHERE key = @p0";
        cmd.Parameters.AddWithValue("@p0", $"word_goal_{userId}");
        var val = cmd.ExecuteScalar() as string;
        return int.TryParse(val, out var goal) && goal > 0 ? goal : 20;
    }

    private int ComputeStreak(SqliteConnection conn, long userId)
    {
        // count consecutive days ending today (or yesterday if not yet today)
        var streak = 0;
        var cursor = Today();
        // if not checked in today, start from yesterday
        if (!HasLog(conn, userId, Today()))
            cursor = AddDays(-1);
        for (var i = 0; i < 366; i++)
        {
            if (HasLog(conn, userId, cursor))
            {
                streak++;
                cursor = AddDays(-(i + 1));
            }
            else
            {
                break;
            }
        }
        return streak;
    }

    private static bool HasLog(SqliteConnection conn, long userId, string date)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM learning_logs WHERE user_id = @uid AND date = @d";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@d", date);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static int Count(SqliteConnection conn, string sql, long userId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@uid", userId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static int SumDuration(SqliteConnection conn, long userId, string start, string end)
    {
        using var cmd = conn.CreateCommand();
        if (string.IsNullOrEmpty(start))
        {
            cmd.CommandText = "SELECT COALESCE(SUM(duration_seconds), 0) FROM study_sessions WHERE user_id = @uid";
        }
        else
        {
            cmd.CommandText = "SELECT COALESCE(SUM(duration_seconds), 0) FROM study_sessions WHERE user_id = @uid AND date BETWEEN @s AND @e";
            cmd.Parameters.AddWithValue("@s", start);
            cmd.Parameters.AddWithValue("@e", end);
        }
        cmd.Parameters.AddWithValue("@uid", userId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static int CountLog(SqliteConnection conn, long userId, string date, string type)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM learning_logs WHERE user_id = @uid AND date = @date AND type = @type";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@date", date);
        cmd.Parameters.AddWithValue("@type", type);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}