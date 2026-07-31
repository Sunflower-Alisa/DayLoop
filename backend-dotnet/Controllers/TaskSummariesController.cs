using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DayLoop.Api.Data;
using DayLoop.Api.Services;

namespace DayLoop.Api.Controllers;

[ApiController]
[Route("api/task-summaries")]
public class TaskSummariesController : ControllerBase
{
    private long GetUserId()
    {
        return UserIdFilter.GetUserId(Request) ?? 0;
    }

    private static SqliteConnection OpenDb()
    {
        return Database.CreateConnection();
    }

    [HttpGet]
    public IActionResult GetTaskSummary([FromQuery] string? title)
    {
        if (string.IsNullOrEmpty(title))
            return BadRequest(new { error = "title is required" });

        var userId = GetUserId();
        using var conn = OpenDb();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM task_summaries WHERE title = @p0 AND user_id = @uid";
        cmd.Parameters.AddWithValue("@p0", title);
        cmd.Parameters.AddWithValue("@uid", userId);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return Ok(new
            {
                id = reader.GetInt64(reader.GetOrdinal("id")),
                title = reader.IsDBNull(reader.GetOrdinal("title")) ? "" : reader.GetString(reader.GetOrdinal("title")),
                content = reader.IsDBNull(reader.GetOrdinal("content")) ? "" : reader.GetString(reader.GetOrdinal("content")),
                user_id = reader.GetInt64(reader.GetOrdinal("user_id")),
                created_at = reader.IsDBNull(reader.GetOrdinal("created_at")) ? "" : reader.GetString(reader.GetOrdinal("created_at")),
                updated_at = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? "" : reader.GetString(reader.GetOrdinal("updated_at")),
            });
        }
        return new JsonResult((object?)null) { StatusCode = 200 };
    }

    [HttpPut("{title}")]
    public IActionResult SaveTaskSummary(string title, [FromBody] SaveTaskSummaryRequest req)
    {
        var userId = GetUserId();
        using var conn = OpenDb();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT id FROM task_summaries WHERE title = @p0 AND user_id = @uid";
        cmd.Parameters.AddWithValue("@p0", title);
        cmd.Parameters.AddWithValue("@uid", userId);
        var existingId = cmd.ExecuteScalar();

        if (existingId != null)
        {
            cmd.CommandText = "UPDATE task_summaries SET content = @content, updated_at = datetime('now','localtime') WHERE id = @id";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@content", req.Content ?? "");
            cmd.Parameters.AddWithValue("@id", (long)existingId);
            cmd.ExecuteNonQuery();
        }
        else
        {
            cmd.CommandText = "INSERT INTO task_summaries (title, content, user_id) VALUES (@p0, @content, @uid)";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@p0", title);
            cmd.Parameters.AddWithValue("@content", req.Content ?? "");
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.ExecuteNonQuery();
        }

        cmd.CommandText = "SELECT * FROM task_summaries WHERE title = @p0 AND user_id = @uid";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", title);
        cmd.Parameters.AddWithValue("@uid", userId);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return Ok(new
        {
            id = reader.GetInt64(reader.GetOrdinal("id")),
            title = reader.IsDBNull(reader.GetOrdinal("title")) ? "" : reader.GetString(reader.GetOrdinal("title")),
            content = reader.IsDBNull(reader.GetOrdinal("content")) ? "" : reader.GetString(reader.GetOrdinal("content")),
            user_id = reader.GetInt64(reader.GetOrdinal("user_id")),
            created_at = reader.IsDBNull(reader.GetOrdinal("created_at")) ? "" : reader.GetString(reader.GetOrdinal("created_at")),
            updated_at = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? "" : reader.GetString(reader.GetOrdinal("updated_at")),
        });
    }
}

public class SaveTaskSummaryRequest
{
    public string? Content { get; set; }
}
