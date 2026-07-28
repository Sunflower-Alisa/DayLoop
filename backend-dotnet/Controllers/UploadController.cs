using Microsoft.AspNetCore.Mvc;
using DayLoop.Api.Models;

namespace DayLoop.Api.Controllers;

[ApiController]
[Route("api/upload")]
public class UploadController : ControllerBase
{
    private static readonly string UploadDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "backend", "data", "uploads");

    public UploadController()
    {
        if (!Directory.Exists(UploadDir))
            Directory.CreateDirectory(UploadDir);
    }

    [HttpPost("image")]
    public IActionResult UploadImage([FromBody] UploadImageRequest req)
    {
        if (string.IsNullOrEmpty(req.DataUrl))
            return BadRequest(new { error = "dataUrl is required" });

        var match = System.Text.RegularExpressions.Regex.Match(req.DataUrl, @"^data:image/(\w+);base64,(.+)$");
        if (!match.Success)
            return BadRequest(new { error = "Invalid data URL" });

        var ext = match.Groups[1].Value == "jpeg" ? "jpg" : match.Groups[1].Value;
        var base64Data = match.Groups[2].Value;
        var filename = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid().ToString("N")[..6]}.{ext}";
        var filepath = Path.Combine(UploadDir, filename);

        System.IO.File.WriteAllBytes(filepath, Convert.FromBase64String(base64Data));

        return Ok(new { url = $"/uploads/{filename}" });
    }
}
