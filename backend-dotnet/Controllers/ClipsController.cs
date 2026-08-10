using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DayLoop.Api.Data;
using DayLoop.Api.Models;
using DayLoop.Api.Services;

namespace DayLoop.Api.Controllers;

[ApiController]
[Route("api/clips")]
public class ClipsController : ControllerBase
{
    private long GetUserId() => UserIdFilter.GetUserId(Request) ?? 0;

    [HttpGet]
    public IActionResult GetClips([FromQuery] string? source, [FromQuery] string? level)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT c.*,
                   (SELECT COUNT(*) FROM clip_lines cl WHERE cl.clip_id = c.id) as line_count
            FROM video_clips c
            WHERE (c.user_id = 0 OR c.user_id = @uid)
        """;
        if (!string.IsNullOrEmpty(source))
            cmd.CommandText += " AND c.source = @src";
        if (!string.IsNullOrEmpty(level))
            cmd.CommandText += " AND c.level = @lv";
        cmd.CommandText += " ORDER BY c.id";
        cmd.Parameters.AddWithValue("@uid", userId);
        if (!string.IsNullOrEmpty(source))
            cmd.Parameters.AddWithValue("@src", source);
        if (!string.IsNullOrEmpty(level))
            cmd.Parameters.AddWithValue("@lv", level);

        var list = new List<VideoClip>();
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
                list.Add(ReadClip(r));
        }
        return Ok(list);
    }

    [HttpGet("{id}")]
    public IActionResult GetClip(long id)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT c.*,
                   (SELECT COUNT(*) FROM clip_lines cl WHERE cl.clip_id = c.id) as line_count
            FROM video_clips c WHERE c.id = @id
        """;
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return NotFound(new { error = "Clip not found" });
        var clip = ReadClip(r);

        using var lcmd = conn.CreateCommand();
        lcmd.CommandText = "SELECT * FROM clip_lines WHERE clip_id = @id ORDER BY ord";
        lcmd.Parameters.AddWithValue("@id", id);
        using var lr = lcmd.ExecuteReader();
        var lines = new List<ClipLine>();
        while (lr.Read())
        {
            lines.Add(new ClipLine
            {
                Id = lr.GetInt64(lr.GetOrdinal("id")),
                ClipId = lr.GetInt64(lr.GetOrdinal("clip_id")),
                Order = lr.IsDBNull(lr.GetOrdinal("ord")) ? 0 : (int)lr.GetInt64(lr.GetOrdinal("ord")),
                Speaker = lr.IsDBNull(lr.GetOrdinal("speaker")) ? "" : lr.GetString(lr.GetOrdinal("speaker")),
                EnText = lr.IsDBNull(lr.GetOrdinal("en_text")) ? "" : lr.GetString(lr.GetOrdinal("en_text")),
                CnText = lr.IsDBNull(lr.GetOrdinal("cn_text")) ? "" : lr.GetString(lr.GetOrdinal("cn_text")),
                StartTime = lr.IsDBNull(lr.GetOrdinal("start_time")) ? 0 : lr.GetDouble(lr.GetOrdinal("start_time")),
                EndTime = lr.IsDBNull(lr.GetOrdinal("end_time")) ? 0 : lr.GetDouble(lr.GetOrdinal("end_time")),
            });
        }
        return Ok(new { clip, lines });
    }

    private static VideoClip ReadClip(SqliteDataReader r)
    {
        return new VideoClip
        {
            Id = r.GetInt64(r.GetOrdinal("id")),
            Title = r.IsDBNull(r.GetOrdinal("title")) ? "" : r.GetString(r.GetOrdinal("title")),
            Source = r.IsDBNull(r.GetOrdinal("source")) ? "" : r.GetString(r.GetOrdinal("source")),
            CoverUrl = r.IsDBNull(r.GetOrdinal("cover_url")) ? "" : r.GetString(r.GetOrdinal("cover_url")),
            Path = r.IsDBNull(r.GetOrdinal("path")) ? "" : r.GetString(r.GetOrdinal("path")),
            Duration = r.IsDBNull(r.GetOrdinal("duration")) ? 0 : (int)r.GetInt64(r.GetOrdinal("duration")),
            Level = r.IsDBNull(r.GetOrdinal("level")) ? "medium" : r.GetString(r.GetOrdinal("level")),
            Tags = r.IsDBNull(r.GetOrdinal("tags")) ? "" : r.GetString(r.GetOrdinal("tags")),
            Description = r.IsDBNull(r.GetOrdinal("description")) ? "" : r.GetString(r.GetOrdinal("description")),
            LineCount = r.IsDBNull(r.GetOrdinal("line_count")) ? 0 : (int)r.GetInt64(r.GetOrdinal("line_count")),
        };
    }
}