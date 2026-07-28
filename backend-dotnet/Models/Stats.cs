using System.Text.Json.Serialization;

namespace DayLoop.Api.Models;

public class StatsResponse
{
    [JsonPropertyName("totalTasks")] public long TotalTasks { get; set; }
    [JsonPropertyName("completedTasks")] public long CompletedTasks { get; set; }
    [JsonPropertyName("cancelledTasks")] public long CancelledTasks { get; set; }
    [JsonPropertyName("inProgressTasks")] public long InProgressTasks { get; set; }
    [JsonPropertyName("plannedTasks")] public long PlannedTasks { get; set; }
    [JsonPropertyName("completionRate")] public int CompletionRate { get; set; }
    [JsonPropertyName("totalNotes")] public long TotalNotes { get; set; }
    [JsonPropertyName("totalReviews")] public long TotalReviews { get; set; }
    [JsonPropertyName("weeklyStats")] public List<WeeklyStat> WeeklyStats { get; set; } = [];
}

public class WeeklyStat
{
    [JsonPropertyName("week")] public string Week { get; set; } = "";
    [JsonPropertyName("total")] public long Total { get; set; }
    [JsonPropertyName("completed")] public long Completed { get; set; }
}
