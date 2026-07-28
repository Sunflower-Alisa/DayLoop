using System.Text.Json.Serialization;

namespace DayLoop.Api.Models;

public class TaskItem
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("date")] public string Date { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("start_time")] public string StartTime { get; set; } = "";
    [JsonPropertyName("end_time")] public string EndTime { get; set; } = "";
    [JsonPropertyName("planned_duration")] public int PlannedDuration { get; set; }
    [JsonPropertyName("actual_duration")] public int? ActualDuration { get; set; }
    [JsonPropertyName("actual_start")] public string? ActualStart { get; set; }
    [JsonPropertyName("actual_end")] public string? ActualEnd { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "planned";
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("priority")] public int Priority { get; set; } = 2;
    [JsonPropertyName("note")] public string Note { get; set; } = "";
    [JsonPropertyName("is_recurring")] public bool IsRecurring { get; set; }
    [JsonPropertyName("is_planned")] public bool IsPlanned { get; set; } = true;
    [JsonPropertyName("recurring_template_id")] public long? RecurringTemplateId { get; set; }
    [JsonPropertyName("achievement")] public string Achievement { get; set; } = "";
    [JsonPropertyName("note_id")] public long? NoteId { get; set; }
    [JsonPropertyName("sync_enabled")] public bool SyncEnabled { get; set; } = true;
    [JsonPropertyName("tags")] public string Tags { get; set; } = "";
    [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
    [JsonPropertyName("updated_at")] public string UpdatedAt { get; set; } = "";
}

public class CreateTaskRequest
{
    [JsonPropertyName("date")] public string Date { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("start_time")] public string? StartTime { get; set; }
    [JsonPropertyName("end_time")] public string? EndTime { get; set; }
    [JsonPropertyName("planned_duration")] public int? PlannedDuration { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("priority")] public int? Priority { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
    [JsonPropertyName("is_recurring")] public bool? IsRecurring { get; set; }
    [JsonPropertyName("is_planned")] public bool? IsPlanned { get; set; }
    [JsonPropertyName("achievement")] public string? Achievement { get; set; }
    [JsonPropertyName("note_id")] public long? NoteId { get; set; }
    [JsonPropertyName("sync_enabled")] public bool? SyncEnabled { get; set; }
}

public class UpdateTaskRequest
{
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("date")] public string? Date { get; set; }
    [JsonPropertyName("start_time")] public string? StartTime { get; set; }
    [JsonPropertyName("end_time")] public string? EndTime { get; set; }
    [JsonPropertyName("planned_duration")] public int? PlannedDuration { get; set; }
    [JsonPropertyName("actual_duration")] public int? ActualDuration { get; set; }
    [JsonPropertyName("actual_start")] public string? ActualStart { get; set; }
    [JsonPropertyName("actual_end")] public string? ActualEnd { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("priority")] public int? Priority { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
    [JsonPropertyName("is_recurring")] public bool? IsRecurring { get; set; }
    [JsonPropertyName("is_planned")] public bool? IsPlanned { get; set; }
    [JsonPropertyName("achievement")] public string? Achievement { get; set; }
    [JsonPropertyName("note_id")] public long? NoteId { get; set; }
    [JsonPropertyName("sync_enabled")] public bool? SyncEnabled { get; set; }
}

public class CopyTaskRequest
{
    [JsonPropertyName("date")] public string? Date { get; set; }
}
