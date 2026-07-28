using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DayLoop.Api.Data;
using DayLoop.Api.Services;

namespace DayLoop.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetSettings()
    {
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM app_settings";
        var settings = new Dictionary<string, string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            settings[reader.GetString(0)] = reader.IsDBNull(1) ? "" : reader.GetString(1);
        return Ok(settings);
    }

    [HttpPut]
    public IActionResult UpdateSetting([FromBody] UpdateSettingRequest req)
    {
        if (string.IsNullOrEmpty(req.Key))
            return BadRequest(new { error = "key is required" });

        var allowedKeys = new[] { "obsidian_vault_path" };
        if (!allowedKeys.Contains(req.Key))
            return BadRequest(new { error = "Invalid setting key" });

        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT key FROM app_settings WHERE key = @p0";
        cmd.Parameters.AddWithValue("@p0", req.Key);
        var exists = cmd.ExecuteScalar();

        cmd.CommandText = exists != null
            ? "UPDATE app_settings SET value = @p1 WHERE key = @p0"
            : "INSERT INTO app_settings (key, value) VALUES (@p0, @p1)";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", req.Key);
        cmd.Parameters.AddWithValue("@p1", req.Value ?? "");
        cmd.ExecuteNonQuery();

        return Ok(new { key = req.Key, value = req.Value ?? "" });
    }

    [HttpPost("sync-all")]
    public IActionResult SyncAll()
    {
        var (notes, reviews, achievements) = ObsidianSyncService.SyncAll();
        return Ok(new
        {
            message = $"同步完成: {notes} 条备忘录, {reviews} 条复盘, {achievements} 条成果",
            notes,
            reviews,
            achievements
        });
    }
}

public class UpdateSettingRequest
{
    public string Key { get; set; } = "";
    public string? Value { get; set; }
}
