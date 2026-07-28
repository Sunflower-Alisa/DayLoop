using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DayLoop.Api.Data;
using DayLoop.Api.Models;
using DayLoop.Api.Services;

namespace DayLoop.Api.Controllers;

[ApiController]
[Route("api/recurring")]
public class RecurringController : ControllerBase
{
    private long GetUserId()
    {
        return UserIdFilter.GetUserId(Request) ?? 0;
    }

    [HttpGet]
    public IActionResult GetTemplates()
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM recurring_templates WHERE user_id = @uid ORDER BY start_time, id";
        cmd.Parameters.AddWithValue("@uid", userId);
        return Ok(ReadTemplates(cmd));
    }

    [HttpPost]
    public IActionResult CreateTemplate([FromBody] CreateRecurringRequest req)
    {
        var userId = UserIdFilter.RequireUserId(Request);
        if (string.IsNullOrEmpty(req.Title))
            return BadRequest(new { error = "title is required" });

        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO recurring_templates (title, start_time, end_time, planned_duration, category, priority, note, user_id, recurrence_type, recurrence_days, recurring_enabled, sync_enabled)
            VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11)
        """;
        cmd.Parameters.AddWithValue("@p0", req.Title);
        cmd.Parameters.AddWithValue("@p1", req.StartTime ?? "");
        cmd.Parameters.AddWithValue("@p2", req.EndTime ?? "");
        cmd.Parameters.AddWithValue("@p3", req.PlannedDuration ?? 0);
        cmd.Parameters.AddWithValue("@p4", req.Category ?? "");
        cmd.Parameters.AddWithValue("@p5", req.Priority ?? 2);
        cmd.Parameters.AddWithValue("@p6", req.Note ?? "");
        cmd.Parameters.AddWithValue("@p7", userId);
        cmd.Parameters.AddWithValue("@p8", req.RecurrenceType ?? "daily");
        cmd.Parameters.AddWithValue("@p9", req.RecurrenceDays ?? "");
        cmd.Parameters.AddWithValue("@p10", req.RecurringEnabled ?? true);
        cmd.Parameters.AddWithValue("@p11", req.SyncEnabled ?? true);
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT last_insert_rowid()";
        var id = (long)cmd.ExecuteScalar()!;

        cmd.CommandText = "SELECT * FROM recurring_templates WHERE id = @p0";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", id);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return CreatedAtAction(nameof(GetTemplates), null, ReadTemplate(reader));
    }

    [HttpPut("{id}")]
    public IActionResult UpdateTemplate(long id, [FromBody] CreateRecurringRequest req)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE recurring_templates SET
                title = COALESCE(@p0, title),
                start_time = COALESCE(@p1, start_time),
                end_time = COALESCE(@p2, end_time),
                planned_duration = COALESCE(@p3, planned_duration),
                category = COALESCE(@p4, category),
                priority = COALESCE(@p5, priority),
                note = COALESCE(@p6, note),
                recurrence_type = COALESCE(@p8, recurrence_type),
                recurrence_days = COALESCE(@p9, recurrence_days),
                recurring_enabled = COALESCE(@p10, recurring_enabled),
                sync_enabled = COALESCE(@p11, sync_enabled)
            WHERE id = @p7 AND user_id = @uid
        """;
        cmd.Parameters.AddWithValue("@p0", (object?)req.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p1", (object?)req.StartTime ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p2", (object?)req.EndTime ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p3", (object?)req.PlannedDuration ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p4", (object?)req.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p5", (object?)req.Priority ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p6", (object?)req.Note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p8", (object?)req.RecurrenceType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p9", (object?)req.RecurrenceDays ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p10", (object?)req.RecurringEnabled ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p11", req.SyncEnabled.HasValue ? (req.SyncEnabled.Value ? 1 : 0) : DBNull.Value);
        cmd.Parameters.AddWithValue("@p7", id);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT * FROM recurring_templates WHERE id = @p0";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return NotFound(new { error = "Template not found" });
        return Ok(ReadTemplate(reader));
    }

    [HttpDelete("{id}")]
    public IActionResult DeleteTemplate(long id)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM recurring_templates WHERE id = @p0 AND user_id = @uid";
        cmd.Parameters.AddWithValue("@p0", id);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.ExecuteNonQuery();
        return Ok(new { message = "Template deleted" });
    }

    [HttpPost("generate")]
    public IActionResult GenerateTasks([FromBody] GenerateRecurringRequest req)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(req.Date))
            return BadRequest(new { error = "date is required" });

        using var conn = Database.CreateConnection();

        using var tCmd = conn.CreateCommand();
        tCmd.CommandText = "SELECT * FROM recurring_templates WHERE user_id = @uid";
        tCmd.Parameters.AddWithValue("@uid", userId);
        var templates = ReadTemplates(tCmd);

        var createdIds = new List<long>();
        using var cmd = conn.CreateCommand();
        var dateDow = (int)DateTime.Parse(req.Date).DayOfWeek; // 0=Sunday

        foreach (var t in templates)
        {
            if (!t.RecurringEnabled) continue;
            if (t.RecurrenceType == "weekly")
            {
                var days = t.RecurrenceDays.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (!days.Contains(dateDow.ToString()))
                    continue;
            }
            cmd.CommandText = "SELECT id FROM tasks WHERE user_id = @uid AND date = @p0 AND recurring_template_id = @p1";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@p0", req.Date);
            cmd.Parameters.AddWithValue("@p1", t.Id);
            var existing = cmd.ExecuteScalar();

            if (existing == null)
            {
                cmd.CommandText = """
                    INSERT INTO tasks (date, title, start_time, end_time, planned_duration, category, priority, note, is_recurring, recurring_template_id, user_id, sync_enabled)
                    VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, 1, @p8, @uid, @p_sync)
                """;
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@p0", req.Date);
                cmd.Parameters.AddWithValue("@p1", t.Title);
                cmd.Parameters.AddWithValue("@p2", t.StartTime);
                cmd.Parameters.AddWithValue("@p3", t.EndTime);
                cmd.Parameters.AddWithValue("@p4", t.PlannedDuration);
                cmd.Parameters.AddWithValue("@p5", t.Category);
                cmd.Parameters.AddWithValue("@p6", t.Priority);
                cmd.Parameters.AddWithValue("@p7", t.Note);
                cmd.Parameters.AddWithValue("@p8", t.Id);
                cmd.Parameters.AddWithValue("@p_sync", t.SyncEnabled ? 1 : 0);
                cmd.Parameters.AddWithValue("@uid", userId);
                cmd.ExecuteNonQuery();

                cmd.CommandText = "SELECT last_insert_rowid()";
                cmd.Parameters.Clear();
                createdIds.Add((long)cmd.ExecuteScalar()!);
            }
        }

        if (createdIds.Count == 0)
            return Ok(new List<TaskItem>());

        var placeholders = string.Join(",", createdIds.Select((_, i) => $"@p{i}"));
        cmd.CommandText = $"SELECT * FROM tasks WHERE id IN ({placeholders})";
        cmd.Parameters.Clear();
        for (int i = 0; i < createdIds.Count; i++)
            cmd.Parameters.AddWithValue($"@p{i}", createdIds[i]);

        return Ok(TasksController.ReadTasksStatic(cmd));
    }

    private static List<RecurringTemplate> ReadTemplates(SqliteCommand cmd)
    {
        var list = new List<RecurringTemplate>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(ReadTemplate(reader));
        return list;
    }

    private static RecurringTemplate ReadTemplate(SqliteDataReader r)
    {
        return new RecurringTemplate
        {
            Id = r.GetInt64(r.GetOrdinal("id")),
            Title = r.GetString(r.GetOrdinal("title")),
            StartTime = r.IsDBNull(r.GetOrdinal("start_time")) ? "" : r.GetString(r.GetOrdinal("start_time")),
            EndTime = r.IsDBNull(r.GetOrdinal("end_time")) ? "" : r.GetString(r.GetOrdinal("end_time")),
            PlannedDuration = r.IsDBNull(r.GetOrdinal("planned_duration")) ? 0 : r.GetInt32(r.GetOrdinal("planned_duration")),
            Category = r.IsDBNull(r.GetOrdinal("category")) ? "" : r.GetString(r.GetOrdinal("category")),
            Priority = r.IsDBNull(r.GetOrdinal("priority")) ? 2 : r.GetInt32(r.GetOrdinal("priority")),
            Note = r.IsDBNull(r.GetOrdinal("note")) ? "" : r.GetString(r.GetOrdinal("note")),
            CreatedAt = r.IsDBNull(r.GetOrdinal("created_at")) ? "" : r.GetString(r.GetOrdinal("created_at")),
            RecurrenceType = r.IsDBNull(r.GetOrdinal("recurrence_type")) ? "daily" : r.GetString(r.GetOrdinal("recurrence_type")),
            RecurrenceDays = r.IsDBNull(r.GetOrdinal("recurrence_days")) ? "" : r.GetString(r.GetOrdinal("recurrence_days")),
            RecurringEnabled = r.IsDBNull(r.GetOrdinal("recurring_enabled")) || r.GetInt32(r.GetOrdinal("recurring_enabled")) == 1,
            SyncEnabled = r.IsDBNull(r.GetOrdinal("sync_enabled")) || r.GetInt32(r.GetOrdinal("sync_enabled")) == 1,
        };
    }
}
