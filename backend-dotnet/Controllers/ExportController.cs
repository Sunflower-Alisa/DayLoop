using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DayLoop.Api.Data;
using DayLoop.Api.Services;

namespace DayLoop.Api.Controllers;

[ApiController]
[Route("api/export")]
public class ExportController : ControllerBase
{
    private long GetUserId()
    {
        return UserIdFilter.GetUserId(Request) ?? 0;
    }

    [HttpGet("json")]
    public IActionResult ExportJson()
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();

        var tasks = GetAllTasks(conn, userId);
        var notes = GetAllNotes(conn, userId);
        var enrichedNotes = EnrichNotes(notes, conn);

        var exportData = new
        {
            version = "1.0",
            exported_at = DateTime.UtcNow.ToString("o"),
            tasks,
            notes = enrichedNotes,
            reviews = GetAllReviews(conn, userId),
            templates = GetAllTemplates(conn, userId),
        };

        Response.Headers["Content-Disposition"] = $"attachment; filename=dayloop-export-{DateTime.Now:yyyy-MM-dd}.json";
        return Ok(exportData);
    }

    private static List<object> GetAllTasks(SqliteConnection conn, long userId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM tasks WHERE user_id = @uid ORDER BY date DESC, id";
        cmd.Parameters.AddWithValue("@uid", userId);
        var list = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var dict = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                dict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            list.Add(dict);
        }
        return list;
    }

    private static List<Dictionary<string, object?>> GetAllNotes(SqliteConnection conn, long userId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM notes WHERE user_id = @uid ORDER BY created_at DESC";
        cmd.Parameters.AddWithValue("@uid", userId);
        var list = new List<Dictionary<string, object?>>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var dict = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                dict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            list.Add(dict);
        }
        return list;
    }

    private static List<Dictionary<string, object?>> EnrichNotes(List<Dictionary<string, object?>> notes, SqliteConnection conn)
    {
        var result = new List<Dictionary<string, object?>>();
        foreach (var note in notes)
        {
            if (note.TryGetValue("task_id", out var taskId) && taskId != null)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT id, title, date, start_time, end_time, status, category FROM tasks WHERE id = @p0";
                cmd.Parameters.AddWithValue("@p0", (long)taskId);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    var task = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                        task[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    note["linked_task"] = task;
                }
            }
            result.Add(note);
        }
        return result;
    }

    private static List<object> GetAllReviews(SqliteConnection conn, long userId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM daily_reviews WHERE user_id = @uid ORDER BY date DESC";
        cmd.Parameters.AddWithValue("@uid", userId);
        var list = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var dict = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                dict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            list.Add(dict);
        }
        return list;
    }

    private static List<object> GetAllTemplates(SqliteConnection conn, long userId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM recurring_templates WHERE user_id = @uid ORDER BY id";
        cmd.Parameters.AddWithValue("@uid", userId);
        var list = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var dict = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                dict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            list.Add(dict);
        }
        return list;
    }
}
