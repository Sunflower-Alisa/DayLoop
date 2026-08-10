using System.Net;
using DayLoop.Api.Models;

namespace DayLoop.Api.Tests;

[Collection("api")]
public class TaskTests
{
    private readonly ApiFixture _fixture;
    public TaskTests(ApiFixture fixture) => _fixture = fixture;

    private const string Date = "2026-08-05";

    [Fact]
    public async Task CreateTask_ReturnsCreatedTask()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();

        var task = await api.PostJsonAsync<TaskItem>("/api/tasks", new { date = Date, title = "编写测试", start_time = "09:00", end_time = "10:00", planned_duration = 60, category = "工作", priority = 1 });

        Assert.True(task.Id > 0);
        Assert.Equal("编写测试", task.Title);
        Assert.Equal(Date, task.Date);
        Assert.Equal("planned", task.Status);
        Assert.Equal("工作", task.Category);
    }

    [Fact]
    public async Task CreateTask_MissingTitle_Returns400()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var resp = await api.PostAsync("/api/tasks", new { date = Date, title = "" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task GetTasks_ByDate()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        await api.PostAsync("/api/tasks", new { date = Date, title = "任务A" });
        await api.PostAsync("/api/tasks", new { date = Date, title = "任务B" });
        await api.PostAsync("/api/tasks", new { date = "2026-08-06", title = "其他天" });

        var tasks = await api.GetJsonAsync<List<TaskItem>>($"/api/tasks?date={Date}");
        Assert.Equal(2, tasks.Count);
    }

    [Fact]
    public async Task UpdateTask_StatusAndAchievement()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var task = await api.PostJsonAsync<TaskItem>("/api/tasks", new { date = Date, title = "实现接口" });

        var updated = await api.PutJsonAsync<TaskItem>($"/api/tasks/{task.Id}", new { status = "completed", actual_duration = 45, achievement = "完成了接口开发" });

        Assert.Equal("completed", updated.Status);
        Assert.Equal(45, updated.ActualDuration);
        Assert.Equal("完成了接口开发", updated.Achievement);
    }

    [Fact]
    public async Task CopyTask_ToAnotherDate()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var task = await api.PostJsonAsync<TaskItem>("/api/tasks", new { date = Date, title = "原件", category = "学习" });

        var copy = await api.PostJsonAsync<TaskItem>($"/api/tasks/{task.Id}/copy", new { date = "2026-08-06" });

        Assert.NotEqual(task.Id, copy.Id);
        Assert.Equal("原件", copy.Title);
        Assert.Equal("2026-08-06", copy.Date);
        Assert.Equal("学习", copy.Category);
    }

    [Fact]
    public async Task DeleteTask()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var task = await api.PostJsonAsync<TaskItem>("/api/tasks", new { date = Date, title = "待删除" });

        var resp = await api.DeleteAsync($"/api/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var getResp = await api.GetAsync($"/api/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task DeleteTasksByName()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        await api.PostAsync("/api/tasks", new { date = Date, title = "重复任务" });
        await api.PostAsync("/api/tasks", new { date = "2026-08-06", title = "重复任务" });

        var json = await api.SendJsonAsync(HttpMethod.Delete, $"/api/tasks/by-name/重复任务");
        Assert.True(json.GetProperty("count").GetInt32() >= 2);
    }

    [Fact]
    public async Task GetTasksRange()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        await api.PostAsync("/api/tasks", new { date = "2026-08-01", title = "月初" });
        await api.PostAsync("/api/tasks", new { date = "2026-08-31", title = "月末" });

        var tasks = await api.GetJsonAsync<List<TaskItem>>("/api/tasks/range?start=2026-08-01&end=2026-08-31");
        Assert.Equal(2, tasks.Count);
    }

    [Fact]
    public async Task FullWorkflow_CreateAssociateComplete()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var note = await api.PostJsonAsync<Note>("/api/notes", new { title = "编码笔记", content = "实现逻辑", category = "开发" });
        var task = await api.PostJsonAsync<TaskItem>("/api/tasks", new { date = Date, title = "实现登录", start_time = "09:00", end_time = "11:00", planned_duration = 120, category = "开发", priority = 1, note_id = note.Id });

        Assert.Equal(note.Id, task.NoteId ?? 0);

        await api.PutJsonAsync<TaskItem>($"/api/tasks/{task.Id}", new { status = "in_progress", actual_start = "09:05" });
        var done = await api.PutJsonAsync<TaskItem>($"/api/tasks/{task.Id}", new { status = "completed", actual_end = "11:10", actual_duration = 125, achievement = "实现JWT认证" });

        Assert.Equal("completed", done.Status);
        Assert.Equal(125, done.ActualDuration);
        Assert.Contains("JWT", done.Achievement);
    }
}