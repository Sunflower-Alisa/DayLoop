using System.Text.Json.Serialization;

namespace DayLoop.Api.Models;

public class RecurringTemplate
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("start_time")] public string StartTime { get; set; } = "";
    [JsonPropertyName("end_time")] public string EndTime { get; set; } = "";
    [JsonPropertyName("planned_duration")] public int PlannedDuration { get; set; }
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("priority")] public int Priority { get; set; } = 2;
    [JsonPropertyName("note")] public string Note { get; set; } = "";
    [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
    [JsonPropertyName("recurrence_type")] public string RecurrenceType { get; set; } = "daily";
    [JsonPropertyName("recurrence_days")] public string RecurrenceDays { get; set; } = "";
    [JsonPropertyName("recurring_enabled")] public bool RecurringEnabled { get; set; } = true;
    [JsonPropertyName("sync_enabled")] public bool SyncEnabled { get; set; } = true;
}

public class CreateRecurringRequest
{
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("start_time")] public string? StartTime { get; set; }
    [JsonPropertyName("end_time")] public string? EndTime { get; set; }
    [JsonPropertyName("planned_duration")] public int? PlannedDuration { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("priority")] public int? Priority { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
    [JsonPropertyName("recurrence_type")] public string? RecurrenceType { get; set; }
    [JsonPropertyName("recurrence_days")] public string? RecurrenceDays { get; set; }
    [JsonPropertyName("recurring_enabled")] public bool? RecurringEnabled { get; set; }
    [JsonPropertyName("sync_enabled")] public bool? SyncEnabled { get; set; }
}

public class GenerateRecurringRequest
{
    [JsonPropertyName("date")] public string Date { get; set; } = "";
}
