using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using DayLoop.Api.Data;
using DayLoop.Api.Models;
using DayLoop.Api.Services;

namespace DayLoop.Api.Controllers;

[ApiController]
[Route("api/speaking")]
public class SpeakingController : ControllerBase
{
    private long GetUserId() => UserIdFilter.GetUserId(Request) ?? 0;

    [HttpGet("topics")]
    public IActionResult GetTopics([FromQuery] string? category)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT t.*,
                   (SELECT COALESCE(MAX(overall), 0) FROM speaking_records sr WHERE sr.topic_id = t.id AND sr.user_id = @uid) as best_score,
                   (SELECT COUNT(*) FROM speaking_records sr WHERE sr.topic_id = t.id AND sr.user_id = @uid) as practice_count
            FROM speaking_topics t
            WHERE (t.user_id = 0 OR t.user_id = @uid)
        """;
        if (!string.IsNullOrEmpty(category))
            cmd.CommandText += " AND t.category = @cat";
        cmd.CommandText += " ORDER BY t.id";
        cmd.Parameters.AddWithValue("@uid", userId);
        if (!string.IsNullOrEmpty(category))
            cmd.Parameters.AddWithValue("@cat", category);

        var list = new List<SpeakingTopic>();
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var t = new SpeakingTopic
                {
                    Id = r.GetInt64(r.GetOrdinal("id")),
                    Title = r.IsDBNull(r.GetOrdinal("title")) ? "" : r.GetString(r.GetOrdinal("title")),
                    Category = r.IsDBNull(r.GetOrdinal("category")) ? "" : r.GetString(r.GetOrdinal("category")),
                    Level = r.IsDBNull(r.GetOrdinal("level")) ? "beginner" : r.GetString(r.GetOrdinal("level")),
                    SourceType = r.IsDBNull(r.GetOrdinal("source_type")) ? "topic" : r.GetString(r.GetOrdinal("source_type")),
                    SourceId = r.IsDBNull(r.GetOrdinal("source_id")) ? 0 : r.GetInt64(r.GetOrdinal("source_id")),
                    BestScore = r.IsDBNull(r.GetOrdinal("best_score")) ? 0 : (int)r.GetInt64(r.GetOrdinal("best_score")),
                    PracticeCount = r.IsDBNull(r.GetOrdinal("practice_count")) ? 0 : (int)r.GetInt64(r.GetOrdinal("practice_count")),
                };
                t.Lines = ParseLines(r.IsDBNull(r.GetOrdinal("lines")) ? "" : r.GetString(r.GetOrdinal("lines")));
                list.Add(t);
            }
        }
        return Ok(list);
    }

    [HttpGet("topics/{id}")]
    public IActionResult GetTopic(long id)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM speaking_topics WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return NotFound(new { error = "Topic not found" });
        var t = new SpeakingTopic
        {
            Id = r.GetInt64(r.GetOrdinal("id")),
            Title = r.IsDBNull(r.GetOrdinal("title")) ? "" : r.GetString(r.GetOrdinal("title")),
            Category = r.IsDBNull(r.GetOrdinal("category")) ? "" : r.GetString(r.GetOrdinal("category")),
            Level = r.IsDBNull(r.GetOrdinal("level")) ? "beginner" : r.GetString(r.GetOrdinal("level")),
            SourceType = r.IsDBNull(r.GetOrdinal("source_type")) ? "topic" : r.GetString(r.GetOrdinal("source_type")),
            SourceId = r.IsDBNull(r.GetOrdinal("source_id")) ? 0 : r.GetInt64(r.GetOrdinal("source_id")),
            Lines = ParseLines(r.IsDBNull(r.GetOrdinal("lines")) ? "" : r.GetString(r.GetOrdinal("lines"))),
        };
        return Ok(t);
    }

    [HttpPost("records")]
    public IActionResult SaveRecord([FromBody] SpeakingRecordRequest req)
    {
        var userId = UserIdFilter.RequireUserId(Request);
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO speaking_records (user_id, topic_id, line_index, audio_url, accuracy, fluency, completeness, overall) VALUES (@uid, @tid, @li, @au, @ac, @fl, @co, @ov)";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@tid", req.TopicId);
        cmd.Parameters.AddWithValue("@li", req.LineIndex);
        cmd.Parameters.AddWithValue("@au", req.AudioUrl ?? "");
        cmd.Parameters.AddWithValue("@ac", req.Accuracy);
        cmd.Parameters.AddWithValue("@fl", req.Fluency);
        cmd.Parameters.AddWithValue("@co", req.Completeness);
        cmd.Parameters.AddWithValue("@ov", req.Overall);
        cmd.ExecuteNonQuery();
        return Ok(new { ok = true });
    }

    [HttpGet("records")]
    public IActionResult GetRecords()
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM speaking_records WHERE user_id = @uid ORDER BY created_at DESC LIMIT 100";
        cmd.Parameters.AddWithValue("@uid", userId);
        using var r = cmd.ExecuteReader();
        var records = new List<object>();
        while (r.Read())
        {
            records.Add(new
            {
                id = r.GetInt64(r.GetOrdinal("id")),
                topic_id = r.GetInt64(r.GetOrdinal("topic_id")),
                line_index = r.IsDBNull(r.GetOrdinal("line_index")) ? 0 : (int)r.GetInt64(r.GetOrdinal("line_index")),
                accuracy = (int)r.GetInt64(r.GetOrdinal("accuracy")),
                fluency = (int)r.GetInt64(r.GetOrdinal("fluency")),
                completeness = (int)r.GetInt64(r.GetOrdinal("completeness")),
                overall = (int)r.GetInt64(r.GetOrdinal("overall")),
                created_at = r.IsDBNull(r.GetOrdinal("created_at")) ? "" : r.GetString(r.GetOrdinal("created_at")),
            });
        }
        return Ok(records);
    }

    private static List<SpeakingLine> ParseLines(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<SpeakingLine>();
        try { return JsonSerializer.Deserialize<List<SpeakingLine>>(json) ?? new List<SpeakingLine>(); }
        catch { return new List<SpeakingLine>(); }
    }
}