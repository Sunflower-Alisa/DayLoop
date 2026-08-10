using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DayLoop.Api.Data;
using DayLoop.Api.Models;
using DayLoop.Api.Services;

namespace DayLoop.Api.Controllers;

[ApiController]
[Route("api/words")]
public class WordsController : ControllerBase
{
    private static readonly int[] Intervals = { 1, 2, 4, 7, 15 };
    private const string MaxMastered = "mastered";

    private long GetUserId() => UserIdFilter.GetUserId(Request) ?? 0;
    private static string Today() => DateTime.Now.ToString("yyyy-MM-dd");
    private static string AddDays(int days) => DateTime.Now.AddDays(days).ToString("yyyy-MM-dd");

    private long CurrentBookId(SqliteConnection conn, long userId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM app_settings WHERE key = @p0";
        cmd.Parameters.AddWithValue("@p0", $"word_book_{userId}");
        var val = cmd.ExecuteScalar() as string;
        if (long.TryParse(val, out var id))
            return id;
        cmd.CommandText = "SELECT id FROM word_books WHERE is_default = 1 OR user_id = @uid ORDER BY is_default DESC, id LIMIT 1";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@uid", userId);
        var d = cmd.ExecuteScalar();
        return d != null ? (long)d : 0;
    }

    private int DailyGoal(SqliteConnection conn, long userId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM app_settings WHERE key = @p0";
        cmd.Parameters.AddWithValue("@p0", $"word_goal_{userId}");
        var val = cmd.ExecuteScalar() as string;
        return int.TryParse(val, out var goal) && goal > 0 ? goal : 20;
    }

    private void SetSetting(SqliteConnection conn, string key, string value)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO app_settings (key, value) VALUES (@p0, @p1) ON CONFLICT(key) DO UPDATE SET value = excluded.value";
        cmd.Parameters.AddWithValue("@p0", key);
        cmd.Parameters.AddWithValue("@p1", value);
        cmd.ExecuteNonQuery();
    }

    [HttpGet("books")]
    public IActionResult GetBooks()
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT b.*,
                   (SELECT COUNT(*) FROM words w WHERE w.book_id = b.id) as word_count,
                   (SELECT COUNT(*) FROM word_progress wp WHERE wp.word_id IN (SELECT id FROM words WHERE book_id = b.id) AND wp.user_id = @uid) as learned_count,
                   (SELECT COUNT(*) FROM word_progress wp WHERE wp.word_id IN (SELECT id FROM words WHERE book_id = b.id) AND wp.user_id = @uid AND wp.status = 'mastered') as mastered_count
            FROM word_books b
            WHERE b.user_id = 0 OR b.user_id = @uid
            ORDER BY b.is_default DESC, b.id
        """;
        cmd.Parameters.AddWithValue("@uid", userId);
        var books = new List<WordBook>();
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                books.Add(new WordBook
                {
                    Id = r.GetInt64(r.GetOrdinal("id")),
                    Name = r.IsDBNull(r.GetOrdinal("name")) ? "" : r.GetString(r.GetOrdinal("name")),
                    Level = r.IsDBNull(r.GetOrdinal("level")) ? "" : r.GetString(r.GetOrdinal("level")),
                    Description = r.IsDBNull(r.GetOrdinal("description")) ? "" : r.GetString(r.GetOrdinal("description")),
                    CoverColor = r.IsDBNull(r.GetOrdinal("cover_color")) ? "#4f46e5" : r.GetString(r.GetOrdinal("cover_color")),
                    IsDefault = r.GetInt64(r.GetOrdinal("is_default")) == 1,
                    WordCount = (int)r.GetInt64(r.GetOrdinal("word_count")),
                    LearnedCount = (int)r.GetInt64(r.GetOrdinal("learned_count")),
                    MasteredCount = (int)r.GetInt64(r.GetOrdinal("mastered_count")),
                });
            }
        }
        var current = CurrentBookId(conn, userId);
        foreach (var b in books)
            b.DailyGoal = DailyGoal(conn, userId);
        var selected = books.FirstOrDefault(b => b.Id == current);
        if (selected != null)
            selected.DailyGoal = DailyGoal(conn, userId);
        return Ok(books);
    }

    [HttpPost("books")]
    public IActionResult CreateBook([FromBody] CreateBookRequest req)
    {
        var userId = UserIdFilter.RequireUserId(Request);
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Name is required" });
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO word_books (name, level, description, cover_color, user_id) VALUES (@p0, @p1, @p2, @p3, @p4)";
        cmd.Parameters.AddWithValue("@p0", req.Name.Trim());
        cmd.Parameters.AddWithValue("@p1", req.Level ?? "beginner");
        cmd.Parameters.AddWithValue("@p2", req.Description ?? "");
        cmd.Parameters.AddWithValue("@p3", req.CoverColor ?? "#4f46e5");
        cmd.Parameters.AddWithValue("@p4", userId);
        cmd.ExecuteNonQuery();
        cmd.CommandText = "SELECT last_insert_rowid()";
        var id = (long)cmd.ExecuteScalar()!;
        SetSetting(conn, $"word_book_{userId}", id.ToString());
        return Ok(new { id });
    }

    [HttpPut("books/{id}/goal")]
    public IActionResult SetGoal(long id, [FromBody] SetGoalRequest req)
    {
        var userId = UserIdFilter.RequireUserId(Request);
        if (req.DailyGoal < 5 || req.DailyGoal > 200)
            return BadRequest(new { error = "Daily goal must be 5-200" });
        using var conn = Database.CreateConnection();
        SetSetting(conn, $"word_goal_{userId}", req.DailyGoal.ToString());
        SetSetting(conn, $"word_book_{userId}", id.ToString());
        return Ok(new { daily_goal = req.DailyGoal });
    }

    [HttpGet("books/{id}")]
    public IActionResult GetBook(long id)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT w.id, w.word, w.phonetic, w.pos, w.meaning, w.example_en, w.example_cn, w.image_url, w.audio_url, w.book_id,
                   COALESCE(wp.status, 'new') as status,
                   COALESCE(wp.stage, 0) as stage,
                   (SELECT COUNT(*) FROM wrong_words ww WHERE ww.word_id = w.id AND ww.user_id = @uid) as in_wrong
            FROM words w
            LEFT JOIN word_progress wp ON wp.word_id = w.id AND wp.user_id = @uid
            WHERE w.book_id = @bid
            ORDER BY w.id
        """;
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@bid", id);
        var words = ReadWords(cmd);
        return Ok(new { book_id = id, words });
    }

    [HttpGet("daily")]
    public IActionResult GetDaily()
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        var bookId = CurrentBookId(conn, userId);
        var goal = DailyGoal(conn, userId);
        var today = Today();

        var newDone = CountLog(conn, userId, today, "new");
        var reviewDone = CountLog(conn, userId, today, "review");

        var result = new DailyWordTask
        {
            HasBook = bookId > 0,
            NewGoal = goal,
            NewDone = newDone,
            ReviewDone = reviewDone,
        };

        if (bookId == 0)
            return Ok(result);

        // Review queue first (due words)
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT w.id, w.word, w.phonetic, w.pos, w.meaning, w.example_en, w.example_cn, w.image_url, w.audio_url, w.book_id,
                       wp.status, wp.stage,
                       (SELECT COUNT(*) FROM wrong_words ww WHERE ww.word_id = w.id AND ww.user_id = @uid) as in_wrong
                FROM word_progress wp
                JOIN words w ON w.id = wp.word_id
                WHERE wp.user_id = @uid AND wp.status IN ('learning','reviewing') AND wp.next_review_at <= @today
                ORDER BY wp.next_review_at ASC
                LIMIT 30
            """;
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@today", today);
            result.ReviewWords = ReadWords(cmd);
        }

        // New words queue
        var remainNew = Math.Max(0, goal - newDone);
        if (remainNew > 0)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT w.id, w.word, w.phonetic, w.pos, w.meaning, w.example_en, w.example_cn, w.image_url, w.audio_url, w.book_id,
                       'new' as status, 0 as stage,
                       (SELECT COUNT(*) FROM wrong_words ww WHERE ww.word_id = w.id AND ww.user_id = @uid) as in_wrong
                FROM words w
                WHERE w.book_id = @bid
                  AND NOT EXISTS (SELECT 1 FROM word_progress wp WHERE wp.word_id = w.id AND wp.user_id = @uid)
                ORDER BY w.id
                LIMIT @lim
            """;
            cmd.Parameters.AddWithValue("@bid", bookId);
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@lim", remainNew);
            result.NewWords = ReadWords(cmd);
        }

        return Ok(result);
    }

    [HttpPost("learn")]
    public IActionResult SubmitLearn([FromBody] LearnResultRequest req)
    {
        var userId = UserIdFilter.RequireUserId(Request);
        using var conn = Database.CreateConnection();
        var today = Today();

        // Load progress if any
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT status, stage FROM word_progress WHERE user_id = @uid AND word_id = @wid";
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@wid", req.WordId);
            using var r = cmd.ExecuteReader();
            var exists = r.Read();
            var status = exists ? (r.IsDBNull(0) ? "learning" : r.GetString(0)) : "";
            var stage = exists ? (int)(r.IsDBNull(1) ? 0 : r.GetInt64(1)) : 0;

            string newStatus;
            int newStage;

            if (req.Know)
            {
                newStatus = MaxMastered;
                newStage = 5;
            }
            else if (!exists || status == "learning")
            {
                newStatus = req.Correct ? "reviewing" : "learning";
                newStage = 0;
            }
            else if (status == "reviewing")
            {
                if (req.Correct)
                {
                    newStage = stage + 1;
                    newStatus = newStage >= Intervals.Length ? MaxMastered : "reviewing";
                }
                else
                {
                    newStatus = "learning";
                    newStage = 0;
                }
            }
            else
            {
                newStatus = status;
                newStage = stage;
            }

            var nextReview = newStatus == MaxMastered ? "" : AddDays(Intervals[Math.Min(newStage, Intervals.Length - 1)]);
            var streak = exists ? 0 : 0;

            // upsert progress
            using (var upsert = conn.CreateCommand())
            {
                upsert.CommandText = """
                    INSERT INTO word_progress (user_id, word_id, status, stage, correct_streak, wrong_count, last_review_at, next_review_at)
                    VALUES (@uid, @wid, @st, @sg, @sc, @wc, @lr, @nr)
                    ON CONFLICT(user_id, word_id) DO UPDATE SET
                        status = excluded.status,
                        stage = excluded.stage,
                        correct_streak = CASE WHEN excluded.stage = 0 THEN 0 ELSE word_progress.correct_streak + 1 END,
                        wrong_count = CASE WHEN @wrong THEN word_progress.wrong_count + 1 ELSE word_progress.wrong_count END,
                        last_review_at = excluded.last_review_at,
                        next_review_at = excluded.next_review_at
                """;
                upsert.Parameters.AddWithValue("@uid", userId);
                upsert.Parameters.AddWithValue("@wid", req.WordId);
                upsert.Parameters.AddWithValue("@st", newStatus);
                upsert.Parameters.AddWithValue("@sg", newStage);
                upsert.Parameters.AddWithValue("@sc", 0);
                upsert.Parameters.AddWithValue("@wc", 0);
                upsert.Parameters.AddWithValue("@lr", today);
                upsert.Parameters.AddWithValue("@nr", nextReview);
                upsert.Parameters.AddWithValue("@wrong", !req.Correct && !req.Know);
                upsert.ExecuteNonQuery();
            }

            // wrong word book
            if (!req.Correct && !req.Know)
            {
                using var ww = conn.CreateCommand();
                ww.CommandText = "INSERT OR IGNORE INTO wrong_words (user_id, word_id) VALUES (@uid, @wid)";
                ww.Parameters.AddWithValue("@uid", userId);
                ww.Parameters.AddWithValue("@wid", req.WordId);
                ww.ExecuteNonQuery();
            }
            else
            {
                using var ww = conn.CreateCommand();
                ww.CommandText = "DELETE FROM wrong_words WHERE user_id = @uid AND word_id = @wid";
                ww.Parameters.AddWithValue("@uid", userId);
                ww.Parameters.AddWithValue("@wid", req.WordId);
                ww.ExecuteNonQuery();
            }

            // log
            using var log = conn.CreateCommand();
            log.CommandText = "INSERT INTO learning_logs (user_id, date, type, word_id, result) VALUES (@uid, @date, @type, @wid, @res)";
            log.Parameters.AddWithValue("@uid", userId);
            log.Parameters.AddWithValue("@date", today);
            log.Parameters.AddWithValue("@type", req.IsReview ? "review" : "new");
            log.Parameters.AddWithValue("@wid", req.WordId);
            log.Parameters.AddWithValue("@res", req.Know ? "know" : (req.Correct ? "correct" : "wrong"));
            log.ExecuteNonQuery();
        }

        return Ok(new { word_id = req.WordId, ok = true });
    }

    [HttpGet("wrong")]
    public IActionResult GetWrongWords()
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT w.id, w.word, w.phonetic, w.pos, w.meaning, w.example_en, w.example_cn, w.image_url, w.audio_url, w.book_id,
                   COALESCE(wp.status, 'learning') as status, COALESCE(wp.stage, 0) as stage, 1 as in_wrong
            FROM wrong_words ww
            JOIN words w ON w.id = ww.word_id
            LEFT JOIN word_progress wp ON wp.word_id = w.id AND wp.user_id = @uid
            WHERE ww.user_id = @uid
            ORDER BY ww.created_at DESC
        """;
        cmd.Parameters.AddWithValue("@uid", userId);
        return Ok(ReadWords(cmd));
    }

    [HttpDelete("wrong/{wordId}")]
    public IActionResult RemoveWrongWord(long wordId)
    {
        var userId = UserIdFilter.RequireUserId(Request);
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM wrong_words WHERE user_id = @uid AND word_id = @wid";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@wid", wordId);
        cmd.ExecuteNonQuery();
        return Ok(new { ok = true });
    }

    [HttpGet("{id}")]
    public IActionResult GetWord(long id)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT w.id, w.word, w.phonetic, w.pos, w.meaning, w.example_en, w.example_cn, w.image_url, w.audio_url, w.book_id,
                   COALESCE(wp.status, 'new') as status, COALESCE(wp.stage, 0) as stage,
                   (SELECT COUNT(*) FROM wrong_words ww WHERE ww.word_id = w.id AND ww.user_id = @uid) as in_wrong
            FROM words w
            LEFT JOIN word_progress wp ON wp.word_id = w.id AND wp.user_id = @uid
            WHERE w.id = @wid
        """;
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@wid", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return NotFound(new { error = "Word not found" });
        return Ok(ReadWord(r));
    }

    private static int CountLog(SqliteConnection conn, long userId, string date, string type)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM learning_logs WHERE user_id = @uid AND date = @date AND type = @type";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@date", date);
        cmd.Parameters.AddWithValue("@type", type);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static List<Word> ReadWords(SqliteCommand cmd)
    {
        var list = new List<Word>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(ReadWord(r));
        return list;
    }

    private static Word ReadWord(SqliteDataReader r)
    {
        return new Word
        {
            Id = r.GetInt64(r.GetOrdinal("id")),
            WordText = r.IsDBNull(r.GetOrdinal("word")) ? "" : r.GetString(r.GetOrdinal("word")),
            Phonetic = r.IsDBNull(r.GetOrdinal("phonetic")) ? "" : r.GetString(r.GetOrdinal("phonetic")),
            Pos = r.IsDBNull(r.GetOrdinal("pos")) ? "" : r.GetString(r.GetOrdinal("pos")),
            Meaning = r.IsDBNull(r.GetOrdinal("meaning")) ? "" : r.GetString(r.GetOrdinal("meaning")),
            ExampleEn = r.IsDBNull(r.GetOrdinal("example_en")) ? "" : r.GetString(r.GetOrdinal("example_en")),
            ExampleCn = r.IsDBNull(r.GetOrdinal("example_cn")) ? "" : r.GetString(r.GetOrdinal("example_cn")),
            ImageUrl = r.IsDBNull(r.GetOrdinal("image_url")) ? "" : r.GetString(r.GetOrdinal("image_url")),
            AudioUrl = r.IsDBNull(r.GetOrdinal("audio_url")) ? "" : r.GetString(r.GetOrdinal("audio_url")),
            BookId = r.IsDBNull(r.GetOrdinal("book_id")) ? 0 : r.GetInt64(r.GetOrdinal("book_id")),
            Status = r.IsDBNull(r.GetOrdinal("status")) ? "new" : r.GetString(r.GetOrdinal("status")),
            Stage = r.IsDBNull(r.GetOrdinal("stage")) ? 0 : (int)r.GetInt64(r.GetOrdinal("stage")),
            InWrongBook = !r.IsDBNull(r.GetOrdinal("in_wrong")) && r.GetInt64(r.GetOrdinal("in_wrong")) == 1,
        };
    }
}

public class CreateBookRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("name")] public string Name { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("level")] public string? Level { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("description")] public string? Description { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("cover_color")] public string? CoverColor { get; set; }
}
