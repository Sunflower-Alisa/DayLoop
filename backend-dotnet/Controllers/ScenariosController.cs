using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using DayLoop.Api.Data;
using DayLoop.Api.Models;
using DayLoop.Api.Services;

namespace DayLoop.Api.Controllers;

[ApiController]
[Route("api/scenarios")]
public class ScenariosController : ControllerBase
{
    private long GetUserId() => UserIdFilter.GetUserId(Request) ?? 0;

    [HttpGet]
    public IActionResult GetScenarios([FromQuery] string? category)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT s.*,
                   (SELECT COUNT(*) FROM scenario_lines sl WHERE sl.scenario_id = s.id) as line_count,
                   COALESCE((SELECT mastered FROM scenario_progress sp WHERE sp.scenario_id = s.id AND sp.user_id = @uid), 0) as mastered
            FROM scenarios s
            WHERE (s.user_id = 0 OR s.user_id = @uid)
        """;
        if (!string.IsNullOrEmpty(category))
            cmd.CommandText += " AND s.category = @cat";
        cmd.CommandText += " ORDER BY s.category, s.id";
        cmd.Parameters.AddWithValue("@uid", userId);
        if (!string.IsNullOrEmpty(category))
            cmd.Parameters.AddWithValue("@cat", category);

        var list = new List<Scenario>();
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                list.Add(new Scenario
                {
                    Id = r.GetInt64(r.GetOrdinal("id")),
                    Title = r.IsDBNull(r.GetOrdinal("title")) ? "" : r.GetString(r.GetOrdinal("title")),
                    Category = r.IsDBNull(r.GetOrdinal("category")) ? "" : r.GetString(r.GetOrdinal("category")),
                    Level = r.IsDBNull(r.GetOrdinal("level")) ? 1 : (int)r.GetInt64(r.GetOrdinal("level")),
                    Icon = r.IsDBNull(r.GetOrdinal("icon")) ? "" : r.GetString(r.GetOrdinal("icon")),
                    Description = r.IsDBNull(r.GetOrdinal("description")) ? "" : r.GetString(r.GetOrdinal("description")),
                    LineCount = r.IsDBNull(r.GetOrdinal("line_count")) ? 0 : (int)r.GetInt64(r.GetOrdinal("line_count")),
                    Mastered = r.IsDBNull(r.GetOrdinal("mastered")) ? false : r.GetInt64(r.GetOrdinal("mastered")) == 1,
                });
            }
        }
        return Ok(list);
    }

    [HttpGet("{id}")]
    public IActionResult GetScenario(long id)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT s.*, (SELECT COUNT(*) FROM scenario_lines sl WHERE sl.scenario_id = s.id) as line_count,
                   COALESCE((SELECT mastered FROM scenario_progress sp WHERE sp.scenario_id = s.id AND sp.user_id = @uid), 0) as mastered
            FROM scenarios s WHERE s.id = @id
        """;
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return NotFound(new { error = "Scenario not found" });

        var sc = new Scenario
        {
            Id = r.GetInt64(r.GetOrdinal("id")),
            Title = r.IsDBNull(r.GetOrdinal("title")) ? "" : r.GetString(r.GetOrdinal("title")),
            Category = r.IsDBNull(r.GetOrdinal("category")) ? "" : r.GetString(r.GetOrdinal("category")),
            Level = r.IsDBNull(r.GetOrdinal("level")) ? 1 : (int)r.GetInt64(r.GetOrdinal("level")),
            Icon = r.IsDBNull(r.GetOrdinal("icon")) ? "" : r.GetString(r.GetOrdinal("icon")),
            Description = r.IsDBNull(r.GetOrdinal("description")) ? "" : r.GetString(r.GetOrdinal("description")),
            LineCount = r.IsDBNull(r.GetOrdinal("line_count")) ? 0 : (int)r.GetInt64(r.GetOrdinal("line_count")),
            Mastered = r.IsDBNull(r.GetOrdinal("mastered")) ? false : r.GetInt64(r.GetOrdinal("mastered")) == 1,
        };

        var detail = new ScenarioDetail { Scenario = sc };
        detail.Lines = QueryList<ScenarioLine>(conn, "SELECT * FROM scenario_lines WHERE scenario_id = @id ORDER BY ord", id,
            rr => new ScenarioLine
            {
                Id = rr.GetInt64(rr.GetOrdinal("id")),
                ScenarioId = rr.GetInt64(rr.GetOrdinal("scenario_id")),
                Order = rr.IsDBNull(rr.GetOrdinal("ord")) ? 0 : (int)rr.GetInt64(rr.GetOrdinal("ord")),
                Speaker = rr.IsDBNull(rr.GetOrdinal("speaker")) ? "" : rr.GetString(rr.GetOrdinal("speaker")),
                EnText = rr.IsDBNull(rr.GetOrdinal("en_text")) ? "" : rr.GetString(rr.GetOrdinal("en_text")),
                CnText = rr.IsDBNull(rr.GetOrdinal("cn_text")) ? "" : rr.GetString(rr.GetOrdinal("cn_text")),
                AudioUrl = rr.IsDBNull(rr.GetOrdinal("audio_url")) ? "" : rr.GetString(rr.GetOrdinal("audio_url")),
            });
        detail.Phrases = QueryList<ScenarioPhrase>(conn, "SELECT * FROM scenario_phrases WHERE scenario_id = @id ORDER BY id", id,
            rr => new ScenarioPhrase
            {
                Id = rr.GetInt64(rr.GetOrdinal("id")),
                ScenarioId = rr.GetInt64(rr.GetOrdinal("scenario_id")),
                Phrase = rr.IsDBNull(rr.GetOrdinal("phrase")) ? "" : rr.GetString(rr.GetOrdinal("phrase")),
                Meaning = rr.IsDBNull(rr.GetOrdinal("meaning")) ? "" : rr.GetString(rr.GetOrdinal("meaning")),
                ExampleEn = rr.IsDBNull(rr.GetOrdinal("example_en")) ? "" : rr.GetString(rr.GetOrdinal("example_en")),
                ExampleCn = rr.IsDBNull(rr.GetOrdinal("example_cn")) ? "" : rr.GetString(rr.GetOrdinal("example_cn")),
            });
        detail.Quizzes = QueryList<ScenarioQuiz>(conn, "SELECT * FROM scenario_quizzes WHERE scenario_id = @id ORDER BY id", id,
            rr =>
            {
                var options = new List<string>();
                var raw = rr.IsDBNull(rr.GetOrdinal("options")) ? "" : rr.GetString(rr.GetOrdinal("options"));
                try { options = JsonSerializer.Deserialize<List<string>>(raw) ?? new List<string>(); } catch { }
                return new ScenarioQuiz
                {
                    Id = rr.GetInt64(rr.GetOrdinal("id")),
                    ScenarioId = rr.GetInt64(rr.GetOrdinal("scenario_id")),
                    QuestionEn = rr.IsDBNull(rr.GetOrdinal("question_en")) ? "" : rr.GetString(rr.GetOrdinal("question_en")),
                    QuestionCn = rr.IsDBNull(rr.GetOrdinal("question_cn")) ? "" : rr.GetString(rr.GetOrdinal("question_cn")),
                    Options = options,
                    AnswerIndex = rr.IsDBNull(rr.GetOrdinal("answer_index")) ? 0 : (int)rr.GetInt64(rr.GetOrdinal("answer_index")),
                    Explanation = rr.IsDBNull(rr.GetOrdinal("explanation")) ? "" : rr.GetString(rr.GetOrdinal("explanation")),
                };
            });
        return Ok(detail);
    }

    [HttpPost("{id}/quiz")]
    public IActionResult SubmitQuiz(long id, [FromBody] QuizResultRequest req)
    {
        var userId = UserIdFilter.RequireUserId(Request);
        using var conn = Database.CreateConnection();
        var rate = req.Total > 0 ? (double)req.Correct / req.Total : 0;
        var mastered = req.Total > 0 && rate >= 0.6 ? 1 : 0;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO scenario_progress (user_id, scenario_id, mastered, updated_at) VALUES (@uid, @sid, @m, datetime('now','localtime')) ON CONFLICT(user_id, scenario_id) DO UPDATE SET mastered = excluded.mastered, updated_at = excluded.updated_at";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@sid", id);
        cmd.Parameters.AddWithValue("@m", mastered);
        cmd.ExecuteNonQuery();

        using var log = conn.CreateCommand();
        log.CommandText = "INSERT INTO learning_logs (user_id, date, type, topic_id, result) VALUES (@uid, @d, 'scenario', @sid, @res)";
        log.Parameters.AddWithValue("@uid", userId);
        log.Parameters.AddWithValue("@d", DateTime.Now.ToString("yyyy-MM-dd"));
        log.Parameters.AddWithValue("@sid", id);
        log.Parameters.AddWithValue("@res", mastered == 1 ? "correct" : "wrong");
        log.ExecuteNonQuery();
        return Ok(new { mastered = mastered == 1 });
    }

    private static List<T> QueryList<T>(SqliteConnection conn, string sql, long id, Func<SqliteDataReader, T> mapper)
    {
        var list = new List<T>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(mapper(r));
        return list;
    }
}