using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DayLoop.Api.Data;
using DayLoop.Api.Models;
using DayLoop.Api.Services;

namespace DayLoop.Api.Controllers;

// ReSharper disable once InconsistentNaming
using static ObsidianSyncService;

[ApiController]
[Route("api/notes")]
public class NotesController : ControllerBase
{
    private long GetUserId()
    {
        return UserIdFilter.GetUserId(Request) ?? 0;
    }

    [HttpGet]
    public IActionResult GetNotes([FromQuery] string? category, [FromQuery] string? search)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();

        if (!string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(search))
        {
            cmd.CommandText = "SELECT * FROM notes WHERE user_id = @uid AND category = @p0 AND (title LIKE @p1 OR content LIKE @p2) ORDER BY created_at DESC";
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@p0", category);
            cmd.Parameters.AddWithValue("@p1", $"%{search}%");
            cmd.Parameters.AddWithValue("@p2", $"%{search}%");
        }
        else if (!string.IsNullOrEmpty(category))
        {
            cmd.CommandText = "SELECT * FROM notes WHERE user_id = @uid AND category = @p0 ORDER BY created_at DESC";
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@p0", category);
        }
        else if (!string.IsNullOrEmpty(search))
        {
            cmd.CommandText = "SELECT * FROM notes WHERE user_id = @uid AND (title LIKE @p0 OR content LIKE @p1) ORDER BY created_at DESC";
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@p0", $"%{search}%");
            cmd.Parameters.AddWithValue("@p1", $"%{search}%");
        }
        else
        {
            cmd.CommandText = "SELECT * FROM notes WHERE user_id = @uid ORDER BY created_at DESC";
            cmd.Parameters.AddWithValue("@uid", userId);
        }

        var notes = ReadNotes(cmd);
        return Ok(EnrichNotes(notes, conn));
    }

    [HttpPost]
    public IActionResult CreateNote([FromBody] CreateNoteRequest req)
    {
        var userId = UserIdFilter.RequireUserId(Request);
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { error = "Title is required" });

        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO notes (title, content, category, tags, user_id) VALUES (@p0, @p1, @p2, @p3, @p4)";
        cmd.Parameters.AddWithValue("@p0", req.Title.Trim());
        cmd.Parameters.AddWithValue("@p1", req.Content ?? "");
        cmd.Parameters.AddWithValue("@p2", req.Category ?? "");
        cmd.Parameters.AddWithValue("@p3", req.Tags ?? "");
        cmd.Parameters.AddWithValue("@p4", userId);
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT last_insert_rowid()";
        var id = (long)cmd.ExecuteScalar()!;

        if (req.TaskIds != null && req.TaskIds.Count > 0)
        {
            foreach (var tid in req.TaskIds)
            {
                using var linkCmd = conn.CreateCommand();
                linkCmd.CommandText = "INSERT OR IGNORE INTO note_task_links (note_id, task_id) VALUES (@p0, @p1)";
                linkCmd.Parameters.AddWithValue("@p0", id);
                linkCmd.Parameters.AddWithValue("@p1", tid);
                linkCmd.ExecuteNonQuery();

                using var updateCmd = conn.CreateCommand();
                updateCmd.CommandText = "UPDATE tasks SET note_id = @p0 WHERE id = @p1";
                updateCmd.Parameters.AddWithValue("@p0", id);
                updateCmd.Parameters.AddWithValue("@p1", tid);
                updateCmd.ExecuteNonQuery();
            }
        }

        cmd.CommandText = "SELECT * FROM notes WHERE id = @p0";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", id);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        var note = ReadNote(reader);
        SyncNotes();
        return CreatedAtAction(nameof(GetNote), new { id }, note);
    }

    [HttpGet("categories")]
    public IActionResult GetCategories()
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT DISTINCT category FROM notes WHERE user_id = @uid AND category != '' ORDER BY category";
        cmd.Parameters.AddWithValue("@uid", userId);
        var noteCats = new List<string>();
        using (var r = cmd.ExecuteReader())
            while (r.Read()) noteCats.Add(r.GetString(0));

        cmd.CommandText = "SELECT name FROM note_categories WHERE user_id = @uid ORDER BY name";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@uid", userId);
        var namedCats = new List<string>();
        using (var r = cmd.ExecuteReader())
            while (r.Read()) namedCats.Add(r.GetString(0));

        var all = new HashSet<string>(noteCats);
        foreach (var c in namedCats) all.Add(c);
        var sorted = all.OrderBy(x => x).ToList();
        return Ok(sorted);
    }

    [HttpPost("categories")]
    public IActionResult CreateCategory([FromBody] CreateNoteCategoryRequest req)
    {
        var userId = UserIdFilter.RequireUserId(Request);
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Name is required" });

        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO note_categories (name, user_id) VALUES (@p0, @p1)";
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
        cmd.CommandText = "DELETE FROM note_categories WHERE name = @p0 AND user_id = @uid";
        cmd.Parameters.AddWithValue("@p0", name);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.ExecuteNonQuery();
        return Ok(new { message = "Category deleted" });
    }

    [HttpGet("{id}")]
    public IActionResult GetNote(long id)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM notes WHERE id = @p0 AND user_id = @uid";
        cmd.Parameters.AddWithValue("@p0", id);
        cmd.Parameters.AddWithValue("@uid", userId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return NotFound(new { error = "Note not found" });
        var note = ReadNote(reader);
        var enriched = EnrichNote(note, conn);
        return Ok(enriched);
    }

    [HttpPut("{id}")]
    public IActionResult UpdateNote(long id, [FromBody] CreateNoteRequest req)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE notes SET
                title = COALESCE(@p0, title),
                content = COALESCE(@p1, content),
                category = COALESCE(@p2, category),
                tags = COALESCE(@p3, tags),
                updated_at = datetime('now','localtime')
            WHERE id = @p4 AND user_id = @uid
        """;
        cmd.Parameters.AddWithValue("@p0", (object?)req.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p1", (object?)req.Content ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p2", (object?)req.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p3", (object?)req.Tags ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p4", id);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.ExecuteNonQuery();

        if (req.TaskIds != null)
        {
            using var delCmd = conn.CreateCommand();
            delCmd.CommandText = "DELETE FROM note_task_links WHERE note_id = @p0";
            delCmd.Parameters.AddWithValue("@p0", id);
            delCmd.ExecuteNonQuery();

            foreach (var tid in req.TaskIds)
            {
                using var linkCmd = conn.CreateCommand();
                linkCmd.CommandText = "INSERT OR IGNORE INTO note_task_links (note_id, task_id) VALUES (@p0, @p1)";
                linkCmd.Parameters.AddWithValue("@p0", id);
                linkCmd.Parameters.AddWithValue("@p1", tid);
                linkCmd.ExecuteNonQuery();

                using var updateCmd = conn.CreateCommand();
                updateCmd.CommandText = "UPDATE tasks SET note_id = @p0 WHERE id = @p1";
                updateCmd.Parameters.AddWithValue("@p0", id);
                updateCmd.Parameters.AddWithValue("@p1", tid);
                updateCmd.ExecuteNonQuery();
            }
        }

        cmd.CommandText = "SELECT * FROM notes WHERE id = @p0";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return NotFound(new { error = "Note not found" });
        var note = ReadNote(reader);
        SyncNotes();
        return Ok(EnrichNote(note, conn));
    }

    [HttpDelete("{id}")]
    public IActionResult DeleteNote(long id)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT task_id FROM note_task_links WHERE note_id = @p0";
        cmd.Parameters.AddWithValue("@p0", id);
        var taskIds = new List<long>();
        using (var r = cmd.ExecuteReader())
            while (r.Read()) taskIds.Add(r.GetInt64(0));

        foreach (var tid in taskIds)
        {
            using var updateCmd = conn.CreateCommand();
            updateCmd.CommandText = "UPDATE tasks SET note_id = NULL WHERE id = @p0";
            updateCmd.Parameters.AddWithValue("@p0", tid);
            updateCmd.ExecuteNonQuery();
        }

        cmd.CommandText = "DELETE FROM note_task_links WHERE note_id = @p0";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", id);
        cmd.ExecuteNonQuery();

        cmd.CommandText = "DELETE FROM notes WHERE id = @p0 AND user_id = @uid";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", id);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.ExecuteNonQuery();
        return Ok(new { message = "Note deleted" });
    }

    private static List<Note> ReadNotes(SqliteCommand cmd)
    {
        var list = new List<Note>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(ReadNote(reader));
        return list;
    }

    private static Note ReadNote(SqliteDataReader r)
    {
        return new Note
        {
            Id = r.GetInt64(r.GetOrdinal("id")),
            Title = r.IsDBNull(r.GetOrdinal("title")) ? "" : r.GetString(r.GetOrdinal("title")),
            Content = r.IsDBNull(r.GetOrdinal("content")) ? "" : r.GetString(r.GetOrdinal("content")),
            Category = r.IsDBNull(r.GetOrdinal("category")) ? "" : r.GetString(r.GetOrdinal("category")),
            Tags = r.IsDBNull(r.GetOrdinal("tags")) ? "" : r.GetString(r.GetOrdinal("tags")),
            TaskId = r.IsDBNull(r.GetOrdinal("task_id")) ? null : r.GetInt64(r.GetOrdinal("task_id")),
            CreatedAt = r.IsDBNull(r.GetOrdinal("created_at")) ? "" : r.GetString(r.GetOrdinal("created_at")),
            UpdatedAt = r.IsDBNull(r.GetOrdinal("updated_at")) ? "" : r.GetString(r.GetOrdinal("updated_at")),
        };
    }

    private List<Note> EnrichNotes(List<Note> notes, SqliteConnection conn)
    {
        foreach (var note in notes)
            EnrichNote(note, conn);
        return notes;
    }

    private Note EnrichNote(Note note, SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT t.id, t.title, t.date, t.start_time, t.end_time, t.status, t.category
            FROM tasks t
            INNER JOIN note_task_links ntl ON ntl.task_id = t.id
            WHERE ntl.note_id = @p0
            ORDER BY t.date DESC, t.start_time";
        cmd.Parameters.AddWithValue("@p0", note.Id);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            note.LinkedTasks.Add(new LinkedTask
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
        if (note.LinkedTasks.Count == 0)
        {
            using var fallback2 = conn.CreateCommand();
            fallback2.CommandText = "SELECT id, title, date, start_time, end_time, status, category FROM tasks WHERE note_id = @p0 ORDER BY date DESC, start_time";
            fallback2.Parameters.AddWithValue("@p0", note.Id);
            using var fr2 = fallback2.ExecuteReader();
            while (fr2.Read())
            {
                note.LinkedTasks.Add(new LinkedTask
                {
                    Id = fr2.GetInt64(fr2.GetOrdinal("id")),
                    Title = fr2.IsDBNull(fr2.GetOrdinal("title")) ? "" : fr2.GetString(fr2.GetOrdinal("title")),
                    Date = fr2.IsDBNull(fr2.GetOrdinal("date")) ? "" : fr2.GetString(fr2.GetOrdinal("date")),
                    StartTime = fr2.IsDBNull(fr2.GetOrdinal("start_time")) ? "" : fr2.GetString(fr2.GetOrdinal("start_time")),
                    EndTime = fr2.IsDBNull(fr2.GetOrdinal("end_time")) ? "" : fr2.GetString(fr2.GetOrdinal("end_time")),
                    Status = fr2.IsDBNull(fr2.GetOrdinal("status")) ? "" : fr2.GetString(fr2.GetOrdinal("status")),
                    Category = fr2.IsDBNull(fr2.GetOrdinal("category")) ? "" : fr2.GetString(fr2.GetOrdinal("category")),
                });
            }
        }
        if (note.LinkedTasks.Count == 0 && note.TaskId.HasValue)
        {
            using var fallback = conn.CreateCommand();
            fallback.CommandText = "SELECT id, title, date, start_time, end_time, status, category FROM tasks WHERE id = @p0";
            fallback.Parameters.AddWithValue("@p0", note.TaskId.Value);
            using var fr = fallback.ExecuteReader();
            if (fr.Read())
            {
                note.LinkedTasks.Add(new LinkedTask
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
        return note;
    }
}