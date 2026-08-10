using System.Net;
using System.Text.Json;
using DayLoop.Api.Models;

namespace DayLoop.Api.Tests;

[Collection("api")]
public class NoteTests
{
    private readonly ApiFixture _fixture;
    public NoteTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreateAndGetNote()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();

        var note = await api.PostJsonAsync<Note>("/api/notes", new { title = "学习笔记", content = "这是内容", category = "学习", tags = "英语,单词" });

        Assert.True(note.Id > 0);
        Assert.Equal("学习笔记", note.Title);
        Assert.Equal("学习", note.Category);

        var fetched = await api.GetJsonAsync<Note>($"/api/notes/{note.Id}");
        Assert.Equal("这是内容", fetched.Content);
    }

    [Fact]
    public async Task ListNotes_ByCategory()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        await api.PostAsync("/api/notes", new { title = "工作笔记1", category = "工作" });
        await api.PostAsync("/api/notes", new { title = "工作笔记2", category = "工作" });
        await api.PostAsync("/api/notes", new { title = "学习笔记", category = "学习" });

        var work = await api.GetJsonAsync<List<Note>>("/api/notes?category=工作");
        Assert.Equal(2, work.Count);
    }

    [Fact]
    public async Task UpdateNote()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var note = await api.PostJsonAsync<Note>("/api/notes", new { title = "旧标题", content = "旧内容" });

        var updated = await api.PutJsonAsync<Note>($"/api/notes/{note.Id}", new { title = "新标题", content = "新内容", category = "学习" });

        Assert.Equal("新标题", updated.Title);
        Assert.Equal("新内容", updated.Content);
        Assert.Equal("学习", updated.Category);
    }

    [Fact]
    public async Task DeleteNote()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var note = await api.PostJsonAsync<Note>("/api/notes", new { title = "待删" });

        var resp = await api.DeleteAsync($"/api/notes/{note.Id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var getResp = await api.GetAsync($"/api/notes/{note.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task NoteCategories_CreateListDelete()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();

        var created = await api.PostJsonAsync<JsonElement>("/api/notes/categories", new { name = "自定义分类" });
        Assert.Equal("自定义分类", created.GetProperty("name").GetString());

        var cats = await api.GetJsonAsync<List<string>>("/api/notes/categories");
        Assert.Contains("自定义分类", cats);

        var resp = await api.DeleteAsync("/api/notes/categories/自定义分类");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task NoteLinksToTask_ByTaskIds()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var task = await api.PostJsonAsync<TaskItem>("/api/tasks", new { date = "2026-08-05", title = "关联任务" });

        var note = await api.PostJsonAsync<Note>("/api/notes", new { title = "任务笔记", content = "内容", task_ids = new[] { task.Id } });

        var fetched = await api.GetJsonAsync<Note>($"/api/notes/{note.Id}");
        Assert.Contains(task.Id, fetched.LinkedTasks.Select(t => t.Id));
    }
}