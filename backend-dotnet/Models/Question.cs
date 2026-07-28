using System.Text.Json.Serialization;

namespace DayLoop.Api.Models;

public class Question
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("content")] public string Content { get; set; } = "";
    [JsonPropertyName("answer")] public string Answer { get; set; } = "";
    [JsonPropertyName("answer_source")] public string AnswerSource { get; set; } = "self";
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("tags")] public string Tags { get; set; } = "";
    [JsonPropertyName("task_id")] public long? TaskId { get; set; }
    [JsonPropertyName("linked_tasks")] public List<LinkedTask> LinkedTasks { get; set; } = new();
    [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
    [JsonPropertyName("updated_at")] public string UpdatedAt { get; set; } = "";
}

public class CreateQuestionRequest
{
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("content")] public string? Content { get; set; }
    [JsonPropertyName("answer")] public string? Answer { get; set; }
    [JsonPropertyName("answer_source")] public string? AnswerSource { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("tags")] public string? Tags { get; set; }
    [JsonPropertyName("task_ids")] public List<long>? TaskIds { get; set; }
}

public class CreateQuestionCategoryRequest
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}
