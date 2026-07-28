using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DayLoop.Api.Data;
using DayLoop.Api.Models;
using DayLoop.Api.Services;

namespace DayLoop.Api.Controllers;

using static ObsidianSyncService;

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private long GetUserId()
    {
        return UserIdFilter.GetUserId(Request) ?? 0;
    }

    [HttpGet]
    public IActionResult GetReviews([FromQuery] string? date)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();

        if (!string.IsNullOrEmpty(date))
        {
            cmd.CommandText = "SELECT * FROM daily_reviews WHERE user_id = @uid AND date = @p0";
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@p0", date);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
                return Ok(ReadReview(reader));
            return Ok(new { });
        }

        cmd.CommandText = "SELECT * FROM daily_reviews WHERE user_id = @uid ORDER BY date DESC";
        cmd.Parameters.AddWithValue("@uid", userId);
        var list = new List<DailyReview>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadReview(r));
        return Ok(list);
    }

    [HttpPut("{date}")]
    public IActionResult SaveReview(string date, [FromBody] SaveReviewRequest req)
    {
        var userId = UserIdFilter.RequireUserId(Request);
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT id FROM daily_reviews WHERE user_id = @uid AND date = @p0";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@p0", date);
        var exists = cmd.ExecuteScalar();

        cmd.CommandText = exists != null
            ? "UPDATE daily_reviews SET content = @p0, updated_at = datetime('now','localtime') WHERE user_id = @uid AND date = @p1"
            : "INSERT INTO daily_reviews (date, content, user_id) VALUES (@p1, @p0, @uid)";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", req.Content);
        cmd.Parameters.AddWithValue("@p1", date);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.ExecuteNonQuery();

        // Sync: find or create "今日复盘" task, mark completed, set achievement
        cmd.CommandText = "SELECT id FROM tasks WHERE user_id = @uid AND date = @p0 AND title = '今日复盘'";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@p0", date);
        var reviewTaskId = cmd.ExecuteScalar();
        if (reviewTaskId != null)
        {
            cmd.CommandText = "UPDATE tasks SET status = 'completed', achievement = @p0, updated_at = datetime('now','localtime') WHERE id = @p1";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@p0", req.Content);
            cmd.Parameters.AddWithValue("@p1", (long)reviewTaskId);
            cmd.ExecuteNonQuery();
        }
        else
        {
            cmd.CommandText = "INSERT INTO tasks (date, title, status, achievement, is_planned, user_id) VALUES (@p0, '今日复盘', 'completed', @p1, 0, @uid)";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@p0", date);
            cmd.Parameters.AddWithValue("@p1", req.Content);
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.ExecuteNonQuery();
        }

        cmd.CommandText = "SELECT * FROM daily_reviews WHERE user_id = @uid AND date = @p0";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", date);
        cmd.Parameters.AddWithValue("@uid", userId);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        var review = ReadReview(reader);
        SyncReviews();
        return Ok(review);
    }

    private static DailyReview ReadReview(SqliteDataReader r)
    {
        return new DailyReview
        {
            Id = r.GetInt64(r.GetOrdinal("id")),
            Date = r.GetString(r.GetOrdinal("date")),
            Content = r.IsDBNull(r.GetOrdinal("content")) ? "" : r.GetString(r.GetOrdinal("content")),
            CreatedAt = r.IsDBNull(r.GetOrdinal("created_at")) ? "" : r.GetString(r.GetOrdinal("created_at")),
            UpdatedAt = r.IsDBNull(r.GetOrdinal("updated_at")) ? "" : r.GetString(r.GetOrdinal("updated_at")),
        };
    }
}
