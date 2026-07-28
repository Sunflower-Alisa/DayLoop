using Microsoft.Data.Sqlite;
using DayLoop.Api.Data;

namespace DayLoop.Api.Services;

public class RecurringTaskService : BackgroundService
{
    private readonly ILogger<RecurringTaskService> _logger;

    public RecurringTaskService(ILogger<RecurringTaskService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Scheduler] Registered: auto-generate recurring tasks at 09:00");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var next9Am = now.Date.AddHours(9);
            if (now >= next9Am)
                next9Am = next9Am.AddDays(1);
            var delay = next9Am - now;

            await Task.Delay(delay, stoppingToken);

            GenerateNextDayTasks();
        }
    }

    private void GenerateNextDayTasks()
    {
        var tomorrow = DateTime.Now.Date.AddDays(1);
        var dateStr = tomorrow.ToString("yyyy-MM-dd");

        try
        {
            using var conn = Database.CreateConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT * FROM recurring_templates";
            var templates = new List<(long Id, string Title, string Start, string End, int Duration, string Category, int Priority, string Note, long UserId, string RecurrenceType, string RecurrenceDays, bool RecurringEnabled, bool SyncEnabled)>();

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    templates.Add((
                        reader.GetInt64(reader.GetOrdinal("id")),
                        reader.GetString(reader.GetOrdinal("title")),
                        reader.IsDBNull(reader.GetOrdinal("start_time")) ? "" : reader.GetString(reader.GetOrdinal("start_time")),
                        reader.IsDBNull(reader.GetOrdinal("end_time")) ? "" : reader.GetString(reader.GetOrdinal("end_time")),
                        reader.IsDBNull(reader.GetOrdinal("planned_duration")) ? 0 : reader.GetInt32(reader.GetOrdinal("planned_duration")),
                        reader.IsDBNull(reader.GetOrdinal("category")) ? "" : reader.GetString(reader.GetOrdinal("category")),
                        reader.IsDBNull(reader.GetOrdinal("priority")) ? 2 : reader.GetInt32(reader.GetOrdinal("priority")),
                        reader.IsDBNull(reader.GetOrdinal("note")) ? "" : reader.GetString(reader.GetOrdinal("note")),
                        reader.IsDBNull(reader.GetOrdinal("user_id")) ? 0 : reader.GetInt64(reader.GetOrdinal("user_id")),
                        reader.IsDBNull(reader.GetOrdinal("recurrence_type")) ? "daily" : reader.GetString(reader.GetOrdinal("recurrence_type")),
                        reader.IsDBNull(reader.GetOrdinal("recurrence_days")) ? "" : reader.GetString(reader.GetOrdinal("recurrence_days")),
                        reader.IsDBNull(reader.GetOrdinal("recurring_enabled")) || reader.GetInt32(reader.GetOrdinal("recurring_enabled")) == 1,
                        reader.IsDBNull(reader.GetOrdinal("sync_enabled")) || reader.GetInt32(reader.GetOrdinal("sync_enabled")) == 1
                    ));
                }
            }

            int count = 0;
            foreach (var t in templates)
            {
                if (!t.RecurringEnabled) continue;
                // Check recurrence settings
                if (t.RecurrenceType == "weekly")
                {
                    var tomorrowDow = (int)DateTime.Now.AddDays(1).DayOfWeek; // 0=Sunday
                    var days = t.RecurrenceDays.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (!days.Contains(tomorrowDow.ToString()))
                        continue;
                }
                // For "daily", always proceed
                cmd.CommandText = "SELECT id FROM tasks WHERE user_id = @uid AND date = @p0 AND recurring_template_id = @p1";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@uid", t.UserId);
                cmd.Parameters.AddWithValue("@p0", dateStr);
                cmd.Parameters.AddWithValue("@p1", t.Id);
                var existing = cmd.ExecuteScalar();

                if (existing == null)
                {
                    cmd.CommandText = """
                        INSERT INTO tasks (date, title, start_time, end_time, planned_duration, category, priority, note, is_recurring, recurring_template_id, user_id, sync_enabled)
                        VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, 1, @p8, @uid, @p_sync)
                    """;
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@p0", dateStr);
                    cmd.Parameters.AddWithValue("@p1", t.Title);
                    cmd.Parameters.AddWithValue("@p2", t.Start);
                    cmd.Parameters.AddWithValue("@p3", t.End);
                    cmd.Parameters.AddWithValue("@p4", t.Duration);
                    cmd.Parameters.AddWithValue("@p5", t.Category);
                    cmd.Parameters.AddWithValue("@p6", t.Priority);
                    cmd.Parameters.AddWithValue("@p7", t.Note);
                    cmd.Parameters.AddWithValue("@p8", t.Id);
                    cmd.Parameters.AddWithValue("@p_sync", t.SyncEnabled ? 1 : 0);
                    cmd.Parameters.AddWithValue("@uid", t.UserId);
                    cmd.ExecuteNonQuery();
                    count++;
                }
            }

            if (count > 0)
                _logger.LogInformation("[Scheduler] Generated {Count} recurring tasks for {Date}", count, dateStr);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Scheduler] Error generating recurring tasks");
        }
    }
}
