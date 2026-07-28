using System.Text.Json.Serialization;

namespace DayLoop.Api.Models;

public class UploadImageRequest
{
    [JsonPropertyName("dataUrl")] public string DataUrl { get; set; } = "";
}
