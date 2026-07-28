using System.Text.Json.Serialization;

namespace DayLoop.Api.Models;

public class Note
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("content")] public string Content { get; set; } = "";
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("tags")] public string Tags { get; set; } = "";
    [JsonPropertyName("task_id")] public long? TaskId { get; set; }
    [JsonPropertyName("linked_tasks")] public List<LinkedTask> LinkedTasks { get; set; } = new();
    [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
    [JsonPropertyName("updated_at")] public string UpdatedAt { get; set; } = "";
}

public class LinkedTask
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("date")] public string Date { get; set; } = "";
    [JsonPropertyName("start_time")] public string StartTime { get; set; } = "";
    [JsonPropertyName("end_time")] public string EndTime { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("category")] public string Category { get; set; } = "";
}

public class CreateNoteRequest
{
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("content")] public string? Content { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("tags")] public string? Tags { get; set; }
    [JsonPropertyName("task_ids")] public List<long>? TaskIds { get; set; }
}

public class CreateNoteCategoryRequest
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}