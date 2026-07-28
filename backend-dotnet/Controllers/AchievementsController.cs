using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DayLoop.Api.Data;
using DayLoop.Api.Models;
using DayLoop.Api.Services;

namespace DayLoop.Api.Controllers;

[ApiController]
[Route("api/achievements")]
public class AchievementsController : ControllerBase
{
    private long GetUserId()
    {
        return UserIdFilter.GetUserId(Request) ?? 0;
    }

    [HttpGet]
    public IActionResult GetAchievements([FromQuery] string? category)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();

        if (!string.IsNullOrEmpty(category))
        {
            cmd.CommandText = "SELECT * FROM tasks WHERE user_id = @uid AND achievement != '' AND achievement IS NOT NULL AND category = @p0 ORDER BY date DESC, start_time DESC";
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@p0", category);
        }
        else
        {
            cmd.CommandText = "SELECT * FROM tasks WHERE user_id = @uid AND achievement != '' AND achievement IS NOT NULL ORDER BY date DESC, start_time DESC";
            cmd.Parameters.AddWithValue("@uid", userId);
        }

        return Ok(TasksController.ReadTasksStatic(cmd));
    }

    [HttpGet("categories")]
    public IActionResult GetCategories()
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT category FROM tasks WHERE user_id = @uid AND achievement != '' AND achievement IS NOT NULL AND category != '' ORDER BY category";
        cmd.Parameters.AddWithValue("@uid", userId);
        var list = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(reader.GetString(0));
        return Ok(list);
    }

    [HttpGet("{id}")]
    public IActionResult GetAchievement(long id)
    {
        var userId = GetUserId();
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM tasks WHERE id = @p0 AND user_id = @uid";
        cmd.Parameters.AddWithValue("@p0", id);
        cmd.Parameters.AddWithValue("@uid", userId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return NotFound(new { error = "Task not found" });
        return Ok(TasksController.ReadTaskStatic(reader));
    }
}
