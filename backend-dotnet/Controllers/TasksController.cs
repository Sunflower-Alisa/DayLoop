using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DayLoop.Api.Data;
using DayLoop.Api.Models;
using DayLoop.Api.Services;

namespace DayLoop.Api.Controllers;

using static ObsidianSyncService;

[ApiController]
[Route("api/tasks")]
public class TasksController : ControllerBase
{
    private long GetUserId()
    {
        return UserIdFilter.GetUserId(Request) ?? 0;
    }

    private static SqliteConnection OpenDb()
    {
        var conn = Database.CreateConnection();
        return conn;
    }

    [HttpGet("range")]
    public IActionResult GetTasksRange([FromQuery] string? start, [FromQuery] string? end)
    {
        if (string.IsNullOrEmpty(start) || string.IsNullOrEmpty(end))
            return BadRequest(new { error = "start and end are required" });
        var userId = GetUserId();
        using var conn = OpenDb();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM tasks WHERE user_id = @uid AND date >= @start AND date <= @end ORDER BY date, start_time";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@start", start);
        cmd.Parameters.AddWithValue("@end", end);
        return Ok(ReadTasksStatic(cmd));
    }

    [HttpGet]
    public IActionResult GetTasks([FromQuery] string? date, [FromQuery] string? search)
    {
        var userId = GetUserId();
        using var conn = OpenDb();
        using var cmd = conn.CreateCommand();

        if (!string.IsNullOrEmpty(date) && !string.IsNullOrEmpty(search))
        {
            cmd.CommandText = "SELECT * FROM tasks WHERE user_id = @uid AND date = @p0 AND (title LIKE @p1 OR note LIKE @p2) ORDER BY is_planned DESC, priority, start_time, id";
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@p0", date);
            cmd.Parameters.AddWithValue("@p1", $"%{search}%");
            cmd.Parameters.AddWithValue("@p2", $"%{search}%");
        }
        else if (!string.IsNullOrEmpty(date))
        {
            cmd.CommandText = "SELECT * FROM tasks WHERE user_id = @uid AND date = @p0 ORDER BY is_planned DESC, priority, start_time, id";
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@p0", date);
        }
        else if (!string.IsNullOrEmpty(search))
        {
            cmd.CommandText = "SELECT * FROM tasks WHERE user_id = @uid AND (title LIKE @p0 OR note LIKE @p1) ORDER BY date DESC, is_planned DESC, priority, start_time, id";
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@p0", $"%{search}%");
            cmd.Parameters.AddWithValue("@p1", $"%{search}%");
        }
        else
        {
            cmd.CommandText = "SELECT * FROM tasks WHERE user_id = @uid ORDER BY date DESC, is_planned DESC, priority, start_time, id";
            cmd.Parameters.AddWithValue("@uid", userId);
        }

        return Ok(ReadTasksStatic(cmd));
    }

    [HttpPost]
    public IActionResult CreateTask([FromBody] CreateTaskRequest req)
    {
        var userId = UserIdFilter.RequireUserId(Request);
        if (string.IsNullOrEmpty(req.Date) || string.IsNullOrEmpty(req.Title))
            return BadRequest(new { error = "date and title are required" });

        using var conn = OpenDb();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO tasks (date, title, start_time, end_time, planned_duration, category, priority, note, is_recurring, is_planned, achievement, note_id, sync_enabled, planned_days, overall_status, user_id)
            VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p_days, 'pending', @p13)
        """;
        cmd.Parameters.AddWithValue("@p0", req.Date);
        cmd.Parameters.AddWithValue("@p1", req.Title);
        cmd.Parameters.AddWithValue("@p2", req.StartTime ?? "");
        cmd.Parameters.AddWithValue("@p3", req.EndTime ?? "");
        cmd.Parameters.AddWithValue("@p4", req.PlannedDuration ?? 0);
        cmd.Parameters.AddWithValue("@p5", req.Category ?? "");
        cmd.Parameters.AddWithValue("@p6", req.Priority ?? 2);
        cmd.Parameters.AddWithValue("@p7", req.Note ?? "");
        cmd.Parameters.AddWithValue("@p8", req.IsRecurring == true ? 1 : 0);
        cmd.Parameters.AddWithValue("@p9", req.IsPlanned ?? true ? 1 : 0);
        cmd.Parameters.AddWithValue("@p10", req.Achievement ?? "");
        cmd.Parameters.AddWithValue("@p11", (object?)req.NoteId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p12", req.SyncEnabled ?? true ? 1 : 0);
        cmd.Parameters.AddWithValue("@p_days", req.PlannedDays ?? 1);
        cmd.Parameters.AddWithValue("@p13", userId);
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT last_insert_rowid()";
        var taskId = (long)cmd.ExecuteScalar()!;

        if (req.NoteId.HasValue)
        {
            cmd.CommandText = "UPDATE notes SET task_id = @p0 WHERE id = @p1";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@p0", taskId);
            cmd.Parameters.AddWithValue("@p1", req.NoteId.Value);
            cmd.ExecuteNonQuery();
        }

        if (req.IsRecurring == true)
        {
            cmd.CommandText = "SELECT id FROM recurring_templates WHERE user_id = @uid AND title = @p0";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@p0", req.Title);
            var existing = cmd.ExecuteScalar();
            if (existing == null)
            {
                cmd.CommandText = """
                    INSERT INTO recurring_templates (title, start_time, end_time, planned_duration, category, priority, note, user_id, planned_days, sync_enabled)
                    VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p_days, @p_sync)
                """;
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@p0", req.Title);
                cmd.Parameters.AddWithValue("@p1", req.StartTime ?? "");
                cmd.Parameters.AddWithValue("@p2", req.EndTime ?? "");
                cmd.Parameters.AddWithValue("@p3", req.PlannedDuration ?? 0);
                cmd.Parameters.AddWithValue("@p4", req.Category ?? "");
                cmd.Parameters.AddWithValue("@p5", req.Priority ?? 2);
                cmd.Parameters.AddWithValue("@p6", req.Note ?? "");
                cmd.Parameters.AddWithValue("@p7", userId);
                cmd.Parameters.AddWithValue("@p_days", req.PlannedDays ?? 1);
                cmd.Parameters.AddWithValue("@p_sync", req.SyncEnabled ?? true ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
            cmd.CommandText = "SELECT id FROM recurring_templates WHERE user_id = @uid AND title = @p0";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@p0", req.Title);
            var tmplId = cmd.ExecuteScalar();
            if (tmplId != null)
            {
                cmd.CommandText = "UPDATE tasks SET recurring_template_id = @tid WHERE id = @taskId";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@tid", (long)tmplId);
                cmd.Parameters.AddWithValue("@taskId", taskId);
                cmd.ExecuteNonQuery();
            }
        }

        cmd.CommandText = "SELECT * FROM tasks WHERE id = @p0";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", taskId);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        var newTask = ReadTaskStatic(reader);
        SyncAchievements();
        return CreatedAtAction(nameof(GetTask), new { id = taskId }, newTask);
    }

    [HttpGet("{id}")]
    public IActionResult GetTask(long id)
    {
        var userId = GetUserId();
        using var conn = OpenDb();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM tasks WHERE id = @p0 AND user_id = @uid";
        cmd.Parameters.AddWithValue("@p0", id);
        cmd.Parameters.AddWithValue("@uid", userId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return NotFound(new { error = "Task not found" });
        return Ok(ReadTaskStatic(reader));
    }

    [HttpPut("{id}")]
    public IActionResult UpdateTask(long id, [FromBody] UpdateTaskRequest req)
    {
        var userId = GetUserId();
        using var conn = OpenDb();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE tasks SET
                title = COALESCE(@p0, title),
                date = COALESCE(@p_date, date),
                start_time = COALESCE(@p1, start_time),
                end_time = COALESCE(@p2, end_time),
                planned_duration = COALESCE(@p3, planned_duration),
                actual_duration = COALESCE(@p4, actual_duration),
                actual_start = COALESCE(@p5, actual_start),
                actual_end = COALESCE(@p6, actual_end),
                status = COALESCE(@p7, status),
                category = COALESCE(@p8, category),
                priority = COALESCE(@p9, priority),
                note = COALESCE(@p10, note),
                is_recurring = COALESCE(@p11, is_recurring),
                is_planned = COALESCE(@p12, is_planned),
                achievement = COALESCE(@p13, achievement),
                sync_enabled = COALESCE(@p_sync, sync_enabled),
                planned_days = COALESCE(@p_days, planned_days),
                overall_status = COALESCE(@p_overall, overall_status),
                updated_at = datetime('now','localtime')
            WHERE id = @p14 AND user_id = @uid
        """;
        cmd.Parameters.AddWithValue("@p0", (object?)req.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p_date", (object?)req.Date ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p1", (object?)req.StartTime ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p2", (object?)req.EndTime ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p3", (object?)req.PlannedDuration ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p4", (object?)req.ActualDuration ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p5", (object?)req.ActualStart ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p6", (object?)req.ActualEnd ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p7", (object?)req.Status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p8", (object?)req.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p9", (object?)req.Priority ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p10", (object?)req.Note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p11", req.IsRecurring.HasValue ? (req.IsRecurring.Value ? 1 : 0) : DBNull.Value);
        cmd.Parameters.AddWithValue("@p12", req.IsPlanned.HasValue ? (req.IsPlanned.Value ? 1 : 0) : DBNull.Value);
        cmd.Parameters.AddWithValue("@p13", (object?)req.Achievement ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p_sync", req.SyncEnabled.HasValue ? (req.SyncEnabled.Value ? 1 : 0) : DBNull.Value);
        cmd.Parameters.AddWithValue("@p_days", (object?)req.PlannedDays ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p_overall", (object?)req.OverallStatus ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p14", id);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT * FROM tasks WHERE id = @p0";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return NotFound(new { error = "Task not found" });

        var task = ReadTaskStatic(reader);
        reader.Close();

        SyncAchievements();

        if (req.IsRecurring == true)
        {
            cmd.CommandText = "SELECT id FROM recurring_templates WHERE user_id = @uid AND title = @p0";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@p0", task.Title);
            var existing = cmd.ExecuteScalar();
            if (existing == null)
            {
                cmd.CommandText = """
                    INSERT INTO recurring_templates (title, start_time, end_time, planned_duration, category, priority, note, user_id, planned_days, sync_enabled)
                    VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p_days, @p_sync)
                """;
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@p0", task.Title);
                cmd.Parameters.AddWithValue("@p1", task.StartTime);
                cmd.Parameters.AddWithValue("@p2", task.EndTime);
                cmd.Parameters.AddWithValue("@p3", task.PlannedDuration);
                cmd.Parameters.AddWithValue("@p4", task.Category);
                cmd.Parameters.AddWithValue("@p5", task.Priority);
                cmd.Parameters.AddWithValue("@p6", task.Note);
                cmd.Parameters.AddWithValue("@p7", userId);
                cmd.Parameters.AddWithValue("@p_days", task.PlannedDays);
                cmd.Parameters.AddWithValue("@p_sync", task.SyncEnabled ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
            cmd.CommandText = "SELECT id FROM recurring_templates WHERE user_id = @uid AND title = @p0";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@p0", task.Title);
            var tmplId = cmd.ExecuteScalar();
            if (tmplId != null)
            {
                cmd.CommandText = "UPDATE tasks SET recurring_template_id = @tid WHERE id = @taskId";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@tid", (long)tmplId);
                cmd.Parameters.AddWithValue("@taskId", id);
                cmd.ExecuteNonQuery();
            }
        }

        cmd.CommandText = "SELECT note_id FROM tasks WHERE id = @p0";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", id);
        var oldNoteRaw = cmd.ExecuteScalar();
        var oldNoteId = oldNoteRaw != null && oldNoteRaw is long l ? l : (long?)null;

        cmd.CommandText = "UPDATE tasks SET note_id = @p_note WHERE id = @p0";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p_note", (object?)req.NoteId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p0", id);
        cmd.ExecuteNonQuery();

        if (oldNoteId.HasValue)
        {
            cmd.CommandText = "UPDATE notes SET task_id = NULL WHERE id = @p0";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@p0", oldNoteId.Value);
            cmd.ExecuteNonQuery();
        }

        if (req.NoteId.HasValue && req.NoteId.Value > 0)
        {
            cmd.CommandText = "UPDATE notes SET task_id = @p0 WHERE id = @p1";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@p0", id);
            cmd.Parameters.AddWithValue("@p1", req.NoteId.Value);
            cmd.ExecuteNonQuery();
        }

        return Ok(task);
    }

    [HttpPost("{id}/copy")]
    public IActionResult CopyTask(long id, [FromBody] CopyTaskRequest? req)
    {
        var userId = GetUserId();
        using var conn = OpenDb();
        using var cmd = conn.CreateCommand();

        TaskItem original;
        {
            cmd.CommandText = "SELECT * FROM tasks WHERE id = @p0 AND user_id = @uid";
            cmd.Parameters.AddWithValue("@p0", id);
            cmd.Parameters.AddWithValue("@uid", userId);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return NotFound(new { error = "Task not found" });
            original = ReadTaskStatic(reader);
        }

        var targetDate = !string.IsNullOrEmpty(req?.Date) ? req.Date : DateTime.Now.ToString("yyyy-MM-dd");

        cmd.CommandText = """
            INSERT INTO tasks (date, title, start_time, end_time, planned_duration, category, priority, note, is_recurring, is_planned, note_id, planned_days, overall_status, user_id)
            VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p_days, @p_overall, @p11)
        """;
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", targetDate);
        cmd.Parameters.AddWithValue("@p1", original.Title);
        cmd.Parameters.AddWithValue("@p2", original.StartTime);
        cmd.Parameters.AddWithValue("@p3", original.EndTime);
        cmd.Parameters.AddWithValue("@p4", original.PlannedDuration);
        cmd.Parameters.AddWithValue("@p5", original.Category);
        cmd.Parameters.AddWithValue("@p6", original.Priority);
        cmd.Parameters.AddWithValue("@p7", original.Note);
        cmd.Parameters.AddWithValue("@p8", original.IsRecurring ? 1 : 0);
        cmd.Parameters.AddWithValue("@p9", original.IsPlanned ? 1 : 0);
        cmd.Parameters.AddWithValue("@p10", (object?)original.NoteId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p_days", original.PlannedDays);
        cmd.Parameters.AddWithValue("@p_overall", original.OverallStatus);
        cmd.Parameters.AddWithValue("@p11", userId);
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT last_insert_rowid()";
        cmd.Parameters.Clear();
        var newId = (long)cmd.ExecuteScalar()!;

        cmd.CommandText = "SELECT * FROM tasks WHERE id = @p0";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", newId);
        using var r2 = cmd.ExecuteReader();
        r2.Read();
        return CreatedAtAction(nameof(GetTask), new { id = newId }, ReadTaskStatic(r2));
    }

    [HttpDelete("{id}")]
    public IActionResult DeleteTask(long id)
    {
        var userId = GetUserId();
        using var conn = OpenDb();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM tasks WHERE id = @p0 AND user_id = @uid";
        cmd.Parameters.AddWithValue("@p0", id);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.ExecuteNonQuery();
        return Ok(new { message = "Task deleted" });
    }

    [HttpDelete("by-name/{title}")]
    public IActionResult DeleteTasksByName(string title)
    {
        var userId = GetUserId();
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        using var conn = OpenDb();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM tasks WHERE title = @p0 AND user_id = @uid AND date >= @p1 AND status != @p2";
        cmd.Parameters.AddWithValue("@p0", title);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@p1", today);
        cmd.Parameters.AddWithValue("@p2", "completed");
        var count = cmd.ExecuteNonQuery();
        return Ok(new { message = $"Deleted {count} task(s) with name \"{title}\"", count });
    }

    public static List<TaskItem> ReadTasksStatic(SqliteCommand cmd)
    {
        var list = new List<TaskItem>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(ReadTaskStatic(reader));
        return list;
    }

    public static TaskItem ReadTaskStatic(SqliteDataReader r)
    {
        return new TaskItem
        {
            Id = r.GetInt64(r.GetOrdinal("id")),
            Date = r.IsDBNull(r.GetOrdinal("date")) ? "" : r.GetString(r.GetOrdinal("date")),
            Title = r.IsDBNull(r.GetOrdinal("title")) ? "" : r.GetString(r.GetOrdinal("title")),
            StartTime = r.IsDBNull(r.GetOrdinal("start_time")) ? "" : r.GetString(r.GetOrdinal("start_time")),
            EndTime = r.IsDBNull(r.GetOrdinal("end_time")) ? "" : r.GetString(r.GetOrdinal("end_time")),
            PlannedDuration = r.IsDBNull(r.GetOrdinal("planned_duration")) ? 0 : r.GetInt32(r.GetOrdinal("planned_duration")),
            ActualDuration = r.IsDBNull(r.GetOrdinal("actual_duration")) ? null : r.GetInt32(r.GetOrdinal("actual_duration")),
            ActualStart = r.IsDBNull(r.GetOrdinal("actual_start")) ? null : r.GetString(r.GetOrdinal("actual_start")),
            ActualEnd = r.IsDBNull(r.GetOrdinal("actual_end")) ? null : r.GetString(r.GetOrdinal("actual_end")),
            Status = r.IsDBNull(r.GetOrdinal("status")) ? "planned" : r.GetString(r.GetOrdinal("status")),
            Category = r.IsDBNull(r.GetOrdinal("category")) ? "" : r.GetString(r.GetOrdinal("category")),
            Priority = r.IsDBNull(r.GetOrdinal("priority")) ? 2 : r.GetInt32(r.GetOrdinal("priority")),
            Note = r.IsDBNull(r.GetOrdinal("note")) ? "" : r.GetString(r.GetOrdinal("note")),
            IsRecurring = !r.IsDBNull(r.GetOrdinal("is_recurring")) && r.GetInt32(r.GetOrdinal("is_recurring")) == 1,
            IsPlanned = r.IsDBNull(r.GetOrdinal("is_planned")) || r.GetInt32(r.GetOrdinal("is_planned")) == 1,
            RecurringTemplateId = r.IsDBNull(r.GetOrdinal("recurring_template_id")) ? null : r.GetInt64(r.GetOrdinal("recurring_template_id")),
            Achievement = r.IsDBNull(r.GetOrdinal("achievement")) ? "" : r.GetString(r.GetOrdinal("achievement")),
            NoteId = r.IsDBNull(r.GetOrdinal("note_id")) ? null : r.GetInt64(r.GetOrdinal("note_id")),
            SyncEnabled = r.IsDBNull(r.GetOrdinal("sync_enabled")) || r.GetInt32(r.GetOrdinal("sync_enabled")) == 1,
            PlannedDays = r.IsDBNull(r.GetOrdinal("planned_days")) ? 1 : r.GetInt32(r.GetOrdinal("planned_days")),
            OverallStatus = r.IsDBNull(r.GetOrdinal("overall_status")) ? "pending" : r.GetString(r.GetOrdinal("overall_status")),
            Tags = r.IsDBNull(r.GetOrdinal("tags")) ? "" : r.GetString(r.GetOrdinal("tags")),
            CreatedAt = r.IsDBNull(r.GetOrdinal("created_at")) ? "" : r.GetString(r.GetOrdinal("created_at")),
            UpdatedAt = r.IsDBNull(r.GetOrdinal("updated_at")) ? "" : r.GetString(r.GetOrdinal("updated_at")),
        };
    }
}
