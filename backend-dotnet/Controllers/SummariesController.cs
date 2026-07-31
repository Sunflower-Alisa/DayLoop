using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DayLoop.Api.Data;
using DayLoop.Api.Models;
using DayLoop.Api.Services;

namespace DayLoop.Api.Controllers;

[ApiController]
[Route("api/summaries")]
public class SummariesController : ControllerBase
{
    private long GetUserId()
    {
        return UserIdFilter.GetUserId(Request) ?? 0;
    }

    private static SqliteConnection OpenDb()
    {
        return Database.CreateConnection();
    }

    private static (DateTime start, DateTime end) GetPeriodDateRange(string type, string periodKey)
    {
        var parts = periodKey.Split('-');
        if (type == "weekly")
        {
            var year = int.Parse(parts[0]);
            var week = int.Parse(parts[1].Replace("W", ""));
            var jan1 = new DateTime(year, 1, 1);
            var daysOffset = (week - 1) * 7;
            var start = jan1.AddDays(daysOffset);
            while (start.DayOfWeek != System.DayOfWeek.Monday)
                start = start.AddDays(-1);
            var end = start.AddDays(6);
            return (start, end);
        }
        if (type == "monthly")
        {
            var year = int.Parse(parts[0]);
            var mon = int.Parse(parts[1]);
            return (new DateTime(year, mon, 1), new DateTime(year, mon, DateTime.DaysInMonth(year, mon)));
        }
        if (type == "quarterly")
        {
            var year = int.Parse(parts[0]);
            var q = int.Parse(parts[1].Replace("Q", ""));
            var startMonth = (q - 1) * 3 + 1;
            return (new DateTime(year, startMonth, 1), new DateTime(year, startMonth + 2, DateTime.DaysInMonth(year, startMonth + 2)));
        }
        if (type == "yearly")
        {
            var year = int.Parse(parts[0]);
            return (new DateTime(year, 1, 1), new DateTime(year, 12, 31));
        }
        return (DateTime.Now, DateTime.Now);
    }

    public static string GenerateAutoSummary(long userId, string type, string periodKey)
    {
        var range = GetPeriodDateRange(type, periodKey);
        var startStr = range.start.ToString("yyyy-MM-dd");
        var endStr = range.end.ToString("yyyy-MM-dd");

        using var conn = OpenDb();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT status, planned_duration, actual_duration, category, title FROM tasks WHERE user_id = @uid AND date >= @start AND date <= @end ORDER BY date";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@start", startStr);
        cmd.Parameters.AddWithValue("@end", endStr);

        var tasks = new List<(string status, int? plannedDur, int? actualDur, string category, string title)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tasks.Add((
                reader.IsDBNull(0) ? "planned" : reader.GetString(0),
                reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1),
                reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                reader.IsDBNull(3) ? "" : reader.GetString(3),
                reader.IsDBNull(4) ? "" : reader.GetString(4)
            ));
        }

        if (tasks.Count == 0) return "该时间段内没有任务记录。";

        var total = tasks.Count;
        var completed = tasks.Count(t => t.status == "completed");
        var cancelled = tasks.Count(t => t.status == "cancelled");
        var plannedDur = tasks.Sum(t => t.plannedDur ?? 0);
        var actualDur = tasks.Sum(t => t.actualDur ?? 0);
        var rate = total > 0 ? (int)Math.Round((double)completed / total * 100) : 0;

        var catStats = new Dictionary<string, (int total, int completed, int dur)>();
        foreach (var t in tasks)
        {
            if (string.IsNullOrEmpty(t.category)) continue;
            if (!catStats.ContainsKey(t.category))
                catStats[t.category] = (0, 0, 0);
            var cur = catStats[t.category];
            cur.total++;
            if (t.status == "completed") cur.completed++;
            cur.dur += t.actualDur ?? 0;
            catStats[t.category] = cur;
        }

        var summary = $"## {periodKey} 总结\n\n";
        summary += $"**概览**：共 {total} 个任务，完成 {completed} 个";
        if (cancelled > 0) summary += $"，取消 {cancelled} 个";
        summary += $"，完成率 {rate}%。\n\n";
        summary += $"**时长**：计划 {plannedDur} 分钟，实际 {actualDur} 分钟。\n\n";

        if (catStats.Count > 0)
        {
            summary += "**分类统计**：\n";
            foreach (var kv in catStats.OrderByDescending(k => k.Value.total))
            {
                var cr = kv.Value.total > 0 ? (int)Math.Round((double)kv.Value.completed / kv.Value.total * 100) : 0;
                summary += $"- {kv.Key}：{kv.Value.completed}/{kv.Value.total} ({cr}%)，{kv.Value.dur} 分钟\n";
            }
            summary += "\n";
        }

        var topTasks = tasks.Where(t => t.actualDur.HasValue && t.status == "completed")
            .OrderByDescending(t => t.actualDur)
            .Take(5)
            .ToList();
        if (topTasks.Count > 0)
        {
            summary += "**耗时最多的任务**：\n";
            foreach (var t in topTasks)
                summary += $"- {t.title}（{t.actualDur} 分钟）\n";
        }

        return summary.Trim();
    }

    [HttpGet]
    public IActionResult GetSummary([FromQuery] string? type, [FromQuery] string? period)
    {
        if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(period))
            return BadRequest(new { error = "type and period are required" });

        var userId = GetUserId();
        using var conn = OpenDb();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM summaries WHERE user_id = @uid AND type = @type AND period_key = @period";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@period", period);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var s = ReadSummary(reader);
            return Ok(s);
        }
        return new JsonResult((object?)null) { StatusCode = 200 };
    }

    [HttpPut("{type}/{period}")]
    public IActionResult SaveSummary(string type, string period, [FromBody] SaveSummaryRequest req)
    {
        var userId = GetUserId();
        using var conn = OpenDb();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT id FROM summaries WHERE user_id = @uid AND type = @type AND period_key = @period";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@period", period);
        var existingId = cmd.ExecuteScalar();

        if (existingId != null)
        {
            cmd.CommandText = "UPDATE summaries SET content = @content, updated_at = datetime('now','localtime') WHERE id = @id";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@content", req.Content ?? "");
            cmd.Parameters.AddWithValue("@id", (long)existingId);
            cmd.ExecuteNonQuery();
        }
        else
        {
            var autoSummary = GenerateAutoSummary(userId, type, period);
            cmd.CommandText = "INSERT INTO summaries (type, period_key, content, auto_summary, user_id) VALUES (@type, @period, @content, @auto, @uid)";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@type", type);
            cmd.Parameters.AddWithValue("@period", period);
            cmd.Parameters.AddWithValue("@content", req.Content ?? "");
            cmd.Parameters.AddWithValue("@auto", autoSummary);
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.ExecuteNonQuery();
        }

        cmd.CommandText = "SELECT * FROM summaries WHERE user_id = @uid AND type = @type AND period_key = @period";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@period", period);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return Ok(ReadSummary(reader));
    }

    [HttpPost("generate")]
    public IActionResult GenerateSummary([FromBody] GenerateSummaryRequest req)
    {
        if (string.IsNullOrEmpty(req.Type) || string.IsNullOrEmpty(req.Period))
            return BadRequest(new { error = "type and period are required" });

        var userId = GetUserId();
        var autoSummary = GenerateAutoSummary(userId, req.Type, req.Period);

        using var conn = OpenDb();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM summaries WHERE user_id = @uid AND type = @type AND period_key = @period";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@type", req.Type);
        cmd.Parameters.AddWithValue("@period", req.Period);
        var existingId = cmd.ExecuteScalar();

        if (existingId != null)
        {
            cmd.CommandText = "UPDATE summaries SET auto_summary = @auto, updated_at = datetime('now','localtime') WHERE id = @id";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@auto", autoSummary);
            cmd.Parameters.AddWithValue("@id", (long)existingId);
            cmd.ExecuteNonQuery();
        }
        else
        {
            cmd.CommandText = "INSERT INTO summaries (type, period_key, auto_summary, user_id) VALUES (@type, @period, @auto, @uid)";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@type", req.Type);
            cmd.Parameters.AddWithValue("@period", req.Period);
            cmd.Parameters.AddWithValue("@auto", autoSummary);
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.ExecuteNonQuery();
        }

        cmd.CommandText = "SELECT * FROM summaries WHERE user_id = @uid AND type = @type AND period_key = @period";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@type", req.Type);
        cmd.Parameters.AddWithValue("@period", req.Period);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return Ok(ReadSummary(reader));
    }

    [HttpGet("list")]
    public IActionResult ListSummaries([FromQuery] string? type)
    {
        if (string.IsNullOrEmpty(type))
            return BadRequest(new { error = "type is required" });

        var userId = GetUserId();
        using var conn = OpenDb();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT period_key, updated_at FROM summaries WHERE user_id = @uid AND type = @type ORDER BY period_key DESC";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@type", type);
        using var reader = cmd.ExecuteReader();
        var result = new List<object>();
        while (reader.Read())
        {
            result.Add(new { period_key = reader.GetString(0), updated_at = reader.IsDBNull(1) ? "" : reader.GetString(1) });
        }
        return Ok(result);
    }

    private static Summary ReadSummary(SqliteDataReader r)
    {
        return new Summary
        {
            Id = r.GetInt64(r.GetOrdinal("id")),
            Type = r.IsDBNull(r.GetOrdinal("type")) ? "" : r.GetString(r.GetOrdinal("type")),
            PeriodKey = r.IsDBNull(r.GetOrdinal("period_key")) ? "" : r.GetString(r.GetOrdinal("period_key")),
            Content = r.IsDBNull(r.GetOrdinal("content")) ? "" : r.GetString(r.GetOrdinal("content")),
            AutoSummary = r.IsDBNull(r.GetOrdinal("auto_summary")) ? "" : r.GetString(r.GetOrdinal("auto_summary")),
            UserId = r.GetInt64(r.GetOrdinal("user_id")),
            CreatedAt = r.IsDBNull(r.GetOrdinal("created_at")) ? "" : r.GetString(r.GetOrdinal("created_at")),
            UpdatedAt = r.IsDBNull(r.GetOrdinal("updated_at")) ? "" : r.GetString(r.GetOrdinal("updated_at")),
        };
    }
}
