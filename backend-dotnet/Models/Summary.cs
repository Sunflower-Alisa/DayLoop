using System.Text.Json.Serialization;

namespace DayLoop.Api.Models;

public class Summary
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("period_key")]
    public string PeriodKey { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("auto_summary")]
    public string AutoSummary { get; set; } = "";

    [JsonPropertyName("user_id")]
    public long UserId { get; set; }

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; set; } = "";
}

public class SaveSummaryRequest
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

public class GenerateSummaryRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("period")]
    public string Period { get; set; } = "";
}
