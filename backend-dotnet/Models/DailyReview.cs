using System.Text.Json.Serialization;

namespace DayLoop.Api.Models;

public class DailyReview
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("date")] public string Date { get; set; } = "";
    [JsonPropertyName("content")] public string Content { get; set; } = "";
    [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
    [JsonPropertyName("updated_at")] public string UpdatedAt { get; set; } = "";
}

public class SaveReviewRequest
{
    [JsonPropertyName("content")] public string Content { get; set; } = "";
}
