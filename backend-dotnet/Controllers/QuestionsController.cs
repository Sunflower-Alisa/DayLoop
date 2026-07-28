using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DayLoop.Api.Data;
using DayLoop.Api.Models;
using DayLoop.Api.Services;

namespace DayLoop.Api.Controllers;

[ApiController]
[Route("api/questions")]
public class QuestionsController : ControllerBase
{
    private long GetUserId()
    {
        return UserIdFilter.GetUserId(Request) ?? 0;
    }

    [HttpGet]
    public IActionResult GetQuestions([FromQuery] string? category, [FromQuery] string? search)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();

        if (!string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(search))
        {
            cmd.CommandText = "SELECT * FROM questions WHERE user_id = @uid AND category = @p0 AND (title LIKE @p1 OR content LIKE @p2) ORDER BY created_at DESC";
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@p0", category);
            cmd.Parameters.AddWithValue("@p1", $"%{search}%");
            cmd.Parameters.AddWithValue("@p2", $"%{search}%");
        }
        else if (!string.IsNullOrEmpty(category))
        {
            cmd.CommandText = "SELECT * FROM questions WHERE user_id = @uid AND category = @p0 ORDER BY created_at DESC";
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@p0", category);
        }
        else if (!string.IsNullOrEmpty(search))
        {
            cmd.CommandText = "SELECT * FROM questions WHERE user_id = @uid AND (title LIKE @p0 OR content LIKE @p1) ORDER BY created_at DESC";
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@p0", $"%{search}%");
            cmd.Parameters.AddWithValue("@p1", $"%{search}%");
        }
        else
        {
            cmd.CommandText = "SELECT * FROM questions WHERE user_id = @uid ORDER BY created_at DESC";
            cmd.Parameters.AddWithValue("@uid", userId);
        }

        var questions = ReadQuestions(cmd);
        return Ok(EnrichQuestions(questions, conn));
    }

    [HttpPost]
    public IActionResult CreateQuestion([FromBody] CreateQuestionRequest req)
    {
        var userId = UserIdFilter.RequireUserId(Request);
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { error = "Title is required" });

        var allowedSources = new[] { "self", "ai", "web" };
        var source = allowedSources.Contains(req.AnswerSource ?? "") ? req.AnswerSource : "self";

        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO questions (title, content, answer, answer_source, category, tags, user_id) VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6)";
        cmd.Parameters.AddWithValue("@p0", req.Title.Trim());
        cmd.Parameters.AddWithValue("@p1", req.Content ?? "");
        cmd.Parameters.AddWithValue("@p2", req.Answer ?? "");
        cmd.Parameters.AddWithValue("@p3", source);
        cmd.Parameters.AddWithValue("@p4", req.Category ?? "");
        cmd.Parameters.AddWithValue("@p5", req.Tags ?? "");
        cmd.Parameters.AddWithValue("@p6", userId);
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT last_insert_rowid()";
        var id = (long)cmd.ExecuteScalar()!;

        if (req.TaskIds != null && req.TaskIds.Count > 0)
        {
            foreach (var tid in req.TaskIds)
            {
                using var linkCmd = conn.CreateCommand();
                linkCmd.CommandText = "INSERT OR IGNORE INTO question_task_links (question_id, task_id) VALUES (@p0, @p1)";
                linkCmd.Parameters.AddWithValue("@p0", id);
                linkCmd.Parameters.AddWithValue("@p1", tid);
                linkCmd.ExecuteNonQuery();
            }
        }

        cmd.CommandText = "SELECT * FROM questions WHERE id = @p0";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", id);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        var question = ReadQuestion(reader);
        return CreatedAtAction(nameof(GetQuestion), new { id }, question);
    }

    [HttpGet("categories")]
    public IActionResult GetCategories()
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT DISTINCT category FROM questions WHERE user_id = @uid AND category != '' ORDER BY category";
        cmd.Parameters.AddWithValue("@uid", userId);
        var questionCats = new List<string>();
        using (var r = cmd.ExecuteReader())
            while (r.Read()) questionCats.Add(r.GetString(0));

        cmd.CommandText = "SELECT name FROM question_categories WHERE user_id = @uid ORDER BY name";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@uid", userId);
        var namedCats = new List<string>();
        using (var r = cmd.ExecuteReader())
            while (r.Read()) namedCats.Add(r.GetString(0));

        var all = new HashSet<string>(questionCats);
        foreach (var c in namedCats) all.Add(c);
        var sorted = all.OrderBy(x => x).ToList();
        return Ok(sorted);
    }

    [HttpPost("categories")]
    public IActionResult CreateCategory([FromBody] CreateQuestionCategoryRequest req)
    {
        var userId = UserIdFilter.RequireUserId(Request);
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Name is required" });

        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO question_categories (name, user_id) VALUES (@p0, @p1)";
        cmd.Parameters.AddWithValue("@p0", req.Name.Trim());
        cmd.Parameters.AddWithValue("@p1", userId);
        try
        {
            cmd.ExecuteNonQuery();
            return Ok(new { name = req.Name.Trim() });
        }
        catch (SqliteException ex) when (ex.Message.Contains("UNIQUE"))
        {
            return Conflict(new { error = "Category already exists" });
        }
    }

    [HttpDelete("categories/{name}")]
    public IActionResult DeleteCategory(string name)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM question_categories WHERE name = @p0 AND user_id = @uid";
        cmd.Parameters.AddWithValue("@p0", name);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.ExecuteNonQuery();
        return Ok(new { message = "Category deleted" });
    }

    [HttpGet("{id}")]
    public IActionResult GetQuestion(long id)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM questions WHERE id = @p0 AND user_id = @uid";
        cmd.Parameters.AddWithValue("@p0", id);
        cmd.Parameters.AddWithValue("@uid", userId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return NotFound(new { error = "Question not found" });
        var question = ReadQuestion(reader);
        var enriched = EnrichQuestion(question, conn);
        return Ok(enriched);
    }

    [HttpPut("{id}")]
    public IActionResult UpdateQuestion(long id, [FromBody] CreateQuestionRequest req)
    {
        var userId = GetUserId();
        var allowedSources = new[] { "self", "ai", "web" };
        var source = allowedSources.Contains(req.AnswerSource ?? "") ? req.AnswerSource : null;

        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE questions SET
                title = COALESCE(@p0, title),
                content = COALESCE(@p1, content),
                answer = COALESCE(@p2, answer),
                answer_source = COALESCE(@p3, answer_source),
                category = COALESCE(@p4, category),
                tags = COALESCE(@p5, tags),
                updated_at = datetime('now','localtime')
            WHERE id = @p6 AND user_id = @uid
        """;
        cmd.Parameters.AddWithValue("@p0", (object?)req.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p1", (object?)req.Content ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p2", (object?)req.Answer ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p3", (object?)source ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p4", (object?)req.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p5", (object?)req.Tags ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p6", id);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.ExecuteNonQuery();

        if (req.TaskIds != null)
        {
            using var delCmd = conn.CreateCommand();
            delCmd.CommandText = "DELETE FROM question_task_links WHERE question_id = @p0";
            delCmd.Parameters.AddWithValue("@p0", id);
            delCmd.ExecuteNonQuery();

            foreach (var tid in req.TaskIds)
            {
                using var linkCmd = conn.CreateCommand();
                linkCmd.CommandText = "INSERT OR IGNORE INTO question_task_links (question_id, task_id) VALUES (@p0, @p1)";
                linkCmd.Parameters.AddWithValue("@p0", id);
                linkCmd.Parameters.AddWithValue("@p1", tid);
                linkCmd.ExecuteNonQuery();
            }
        }

        cmd.CommandText = "SELECT * FROM questions WHERE id = @p0";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return NotFound(new { error = "Question not found" });
        var question = ReadQuestion(reader);
        return Ok(EnrichQuestion(question, conn));
    }

    [HttpDelete("{id}")]
    public IActionResult DeleteQuestion(long id)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "DELETE FROM question_task_links WHERE question_id = @p0";
        cmd.Parameters.AddWithValue("@p0", id);
        cmd.ExecuteNonQuery();

        cmd.CommandText = "DELETE FROM questions WHERE id = @p0 AND user_id = @uid";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", id);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.ExecuteNonQuery();
        return Ok(new { message = "Question deleted" });
    }

    private static List<Question> ReadQuestions(SqliteCommand cmd)
    {
        var list = new List<Question>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(ReadQuestion(reader));
        return list;
    }

    private static Question ReadQuestion(SqliteDataReader r)
    {
        return new Question
        {
            Id = r.GetInt64(r.GetOrdinal("id")),
            Title = r.IsDBNull(r.GetOrdinal("title")) ? "" : r.GetString(r.GetOrdinal("title")),
            Content = r.IsDBNull(r.GetOrdinal("content")) ? "" : r.GetString(r.GetOrdinal("content")),
            Answer = r.IsDBNull(r.GetOrdinal("answer")) ? "" : r.GetString(r.GetOrdinal("answer")),
            AnswerSource = r.IsDBNull(r.GetOrdinal("answer_source")) ? "self" : r.GetString(r.GetOrdinal("answer_source")),
            Category = r.IsDBNull(r.GetOrdinal("category")) ? "" : r.GetString(r.GetOrdinal("category")),
            Tags = r.IsDBNull(r.GetOrdinal("tags")) ? "" : r.GetString(r.GetOrdinal("tags")),
            TaskId = r.IsDBNull(r.GetOrdinal("task_id")) ? null : r.GetInt64(r.GetOrdinal("task_id")),
            CreatedAt = r.IsDBNull(r.GetOrdinal("created_at")) ? "" : r.GetString(r.GetOrdinal("created_at")),
            UpdatedAt = r.IsDBNull(r.GetOrdinal("updated_at")) ? "" : r.GetString(r.GetOrdinal("updated_at")),
        };
    }

    private List<Question> EnrichQuestions(List<Question> questions, SqliteConnection conn)
    {
        foreach (var q in questions)
            EnrichQuestion(q, conn);
        return questions;
    }

    private Question EnrichQuestion(Question question, SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT t.id, t.title, t.date, t.start_time, t.end_time, t.status, t.category
            FROM tasks t
            INNER JOIN question_task_links qtl ON qtl.task_id = t.id
            WHERE qtl.question_id = @p0
            ORDER BY t.date DESC, t.start_time";
        cmd.Parameters.AddWithValue("@p0", question.Id);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            question.LinkedTasks.Add(new LinkedTask
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                Title = reader.IsDBNull(reader.GetOrdinal("title")) ? "" : reader.GetString(reader.GetOrdinal("title")),
                Date = reader.IsDBNull(reader.GetOrdinal("date")) ? "" : reader.GetString(reader.GetOrdinal("date")),
                StartTime = reader.IsDBNull(reader.GetOrdinal("start_time")) ? "" : reader.GetString(reader.GetOrdinal("start_time")),
                EndTime = reader.IsDBNull(reader.GetOrdinal("end_time")) ? "" : reader.GetString(reader.GetOrdinal("end_time")),
                Status = reader.IsDBNull(reader.GetOrdinal("status")) ? "" : reader.GetString(reader.GetOrdinal("status")),
                Category = reader.IsDBNull(reader.GetOrdinal("category")) ? "" : reader.GetString(reader.GetOrdinal("category")),
            });
        }
        if (question.LinkedTasks.Count == 0 && question.TaskId.HasValue)
        {
            using var fallback = conn.CreateCommand();
            fallback.CommandText = "SELECT id, title, date, start_time, end_time, status, category FROM tasks WHERE id = @p0";
            fallback.Parameters.AddWithValue("@p0", question.TaskId.Value);
            using var fr = fallback.ExecuteReader();
            if (fr.Read())
            {
                question.LinkedTasks.Add(new LinkedTask
                {
                    Id = fr.GetInt64(fr.GetOrdinal("id")),
                    Title = fr.IsDBNull(fr.GetOrdinal("title")) ? "" : fr.GetString(fr.GetOrdinal("title")),
                    Date = fr.IsDBNull(fr.GetOrdinal("date")) ? "" : fr.GetString(fr.GetOrdinal("date")),
                    StartTime = fr.IsDBNull(fr.GetOrdinal("start_time")) ? "" : fr.GetString(fr.GetOrdinal("start_time")),
                    EndTime = fr.IsDBNull(fr.GetOrdinal("end_time")) ? "" : fr.GetString(fr.GetOrdinal("end_time")),
                    Status = fr.IsDBNull(fr.GetOrdinal("status")) ? "" : fr.GetString(fr.GetOrdinal("status")),
                    Category = fr.IsDBNull(fr.GetOrdinal("category")) ? "" : fr.GetString(fr.GetOrdinal("category")),
                });
            }
        }
        return question;
    }
}
