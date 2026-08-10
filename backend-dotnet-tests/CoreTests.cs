using System.Net;
using System.Text.Json;
using DayLoop.Api.Models;

namespace DayLoop.Api.Tests;

[Collection("api")]
public class QuestionTests
{
    private readonly ApiFixture _fixture;
    public QuestionTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreateAndGetQuestion()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();

        var q = await api.PostJsonAsync<Question>("/api/questions", new { title = "什么是JWT？", content = "JWT是什么", answer = "JSON Web Token", category = "后端", tags = "认证" });

        Assert.True(q.Id > 0);
        var fetched = await api.GetJsonAsync<Question>($"/api/questions/{q.Id}");
        Assert.Equal("JSON Web Token", fetched.Answer);
    }

    [Fact]
    public async Task ListQuestions_AndSearch()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        await api.PostAsync("/api/questions", new { title = "希望工程是什么？", category = "学习" });
        await api.PostAsync("/api/questions", new { title = "另外一个问题", category = "生活" });

        var all = await api.GetJsonAsync<List<Question>>("/api/questions");
        Assert.Equal(2, all.Count);

        var search = await api.GetJsonAsync<List<Question>>("/api/questions?search=希望");
        Assert.Single(search);
    }

    [Fact]
    public async Task UpdateAndDeleteQuestion()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var q = await api.PostJsonAsync<Question>("/api/questions", new { title = "问题", answer = "" });

        var updated = await api.PutJsonAsync<Question>($"/api/questions/{q.Id}", new { answer = "有了答案" });
        Assert.Equal("有了答案", updated.Answer);

        var resp = await api.DeleteAsync($"/api/questions/{q.Id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var getResp = await api.GetAsync($"/api/questions/{q.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task QuestionCategories()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        await api.PostAsync("/api/questions/categories", new { name = "面试" });

        var cats = await api.GetJsonAsync<List<string>>("/api/questions/categories");
        Assert.Contains("面试", cats);
    }
}

[Collection("api")]
public class ReviewTests
{
    private readonly ApiFixture _fixture;
    public ReviewTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SaveAndGetDailyReview()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();

        var saved = await api.PutJsonAsync<DailyReview>("/api/reviews/2026-08-05", new { content = "今天完成了不少任务" });
        Assert.Equal("今天完成了不少任务", saved.Content);

        var fetched = await api.GetJsonAsync<DailyReview>("api/reviews?date=2026-08-05".Replace("api/", "/api/"));
        Assert.Equal("今天完成了不少任务", fetched.Content);
    }

    [Fact]
    public async Task UpdateReview_Overwrites()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        await api.PutJsonAsync<DailyReview>("/api/reviews/2026-08-06", new { content = "第一版" });
        await api.PutJsonAsync<DailyReview>("/api/reviews/2026-08-06", new { content = "第二版" });

        var fetched = await api.GetJsonAsync<DailyReview>("/api/reviews?date=2026-08-06");
        Assert.Equal("第二版", fetched.Content);
    }
}

[Collection("api")]
public class RecurringTests
{
    private readonly ApiFixture _fixture;
    public RecurringTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreateAndListTemplate()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();

        var t = await api.PostJsonAsync<RecurringTemplate>("/api/recurring", new { title = "晨会", start_time = "09:00", end_time = "09:30", planned_duration = 30, category = "工作", recurrence_type = "daily" });

        Assert.True(t.Id > 0);
        var list = await api.GetJsonAsync<List<RecurringTemplate>>("/api/recurring");
        Assert.Contains(list, x => x.Id == t.Id);
    }

    [Fact]
    public async Task GenerateTasks_ForDate()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var t = await api.PostJsonAsync<RecurringTemplate>("/api/recurring", new { title = "每日复盘", start_time = "18:00", end_time = "18:30", recurrence_type = "daily" });

        var tasks = await api.PostJsonAsync<List<TaskItem>>("/api/recurring/generate", new { date = "2026-08-05" });

        Assert.Contains(tasks, x => x.Title == "每日复盘" && x.IsRecurring);
    }

    [Fact]
    public async Task DeleteTemplate()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var t = await api.PostJsonAsync<RecurringTemplate>("/api/recurring", new { title = "待删模板" });

        var resp = await api.DeleteAsync($"/api/recurring/{t.Id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}

[Collection("api")]
public class StatsAndAchievementTests
{
    private readonly ApiFixture _fixture;
    public StatsAndAchievementTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetStats()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        await api.PostAsync("/api/tasks", new { date = "2026-08-05", title = "任务1", status = "" });
        await api.PostAsync("/api/notes", new { title = "笔记" });

        var stats = await api.GetJsonAsync("/api/stats");
        Assert.True(stats.GetProperty("totalTasks").GetInt32() >= 1);
        Assert.False(string.IsNullOrWhiteSpace(stats.GetProperty("completionRate").GetRawText()));
    }

    [Fact]
    public async Task Achievements_ListByCategory()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var t = await api.PostJsonAsync<TaskItem>("/api/tasks", new { date = "2026-08-05", title = "实现功能", category = "开发" });
        await api.PutJsonAsync<TaskItem>($"/api/tasks/{t.Id}", new { status = "completed", achievement = "完成登录模块" });

        var achievements = await api.GetJsonAsync<List<TaskItem>>("/api/achievements");
        Assert.Contains(achievements, x => x.Achievement == "完成登录模块");

        var dev = await api.GetJsonAsync<List<TaskItem>>("/api/achievements?category=开发");
        Assert.Contains(dev, x => x.Id == t.Id);
    }

    [Fact]
    public async Task AchievementCategories()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var t = await api.PostJsonAsync<TaskItem>("/api/tasks", new { date = "2026-08-05", title = "x", category = "开发" });
        await api.PutJsonAsync<TaskItem>($"/api/tasks/{t.Id}", new { status = "completed", achievement = "成果" });

        var cats = await api.GetJsonAsync<List<string>>("/api/achievements/categories");
        Assert.Contains("开发", cats);
    }
}

[Collection("api")]
public class SummaryTests
{
    private readonly ApiFixture _fixture;
    public SummaryTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SaveAndGetSummary()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();

        var saved = await api.PutJsonAsync<Summary>("/api/summaries/weekly/2026-W31", new { content = "本周总结内容" });
        Assert.Equal("本周总结内容", saved.Content);

        var fetched = await api.GetJsonAsync<Summary>("/api/summaries?type=weekly&period=2026-W31");
        Assert.NotNull(fetched);
        Assert.Equal("本周总结内容", fetched.Content);
    }

    [Fact]
    public async Task ListSummaries()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        await api.PutAsync("/api/summaries/monthly/2026-08", new { content = "月总结" });

        var list = await api.GetJsonAsync("/api/summaries/list?type=monthly");
        Assert.True(list.GetArrayLength() >= 1);
    }
}