using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DayLoop.Api.Models;

namespace DayLoop.Api.Tests;

[Collection("api")]
public class EnglishWordTests
{
    private readonly ApiFixture _fixture;
    public EnglishWordTests(ApiFixture fixture) => _fixture = fixture;

    private async Task<(TestApi api, Word first)> NewUserWithFirstWord()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var daily = await api.GetJsonAsync<DailyWordTask>("/api/words/daily");
        Assert.True(daily.NewWords.Count >= 1, "种子数据应包含可学单词");
        return (api, daily.NewWords[0]);
    }

    [Fact]
    public async Task GetBooks_HasSeededDefaultBook()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();

        var books = await api.GetJsonAsync<List<WordBook>>("/api/words/books");
        Assert.NotEmpty(books);
        Assert.Contains(books, b => b.IsDefault);
    }

    [Fact]
    public async Task CreateBook_AndSetGoal()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();

        var created = await api.PostJsonAsync("api/words/books".Replace("api/", "/api/"), new { name = "我的词书", level = "beginner" });
        var id = created.GetProperty("id").GetInt64();
        Assert.True(id > 0);

        var goal = await api.PutJsonAsync("/api/words/books/{id}/goal".Replace("{id}", id.ToString()), new { daily_goal = 15 });
        Assert.Equal(15, goal.GetProperty("daily_goal").GetInt32());

        var books = await api.GetJsonAsync<List<WordBook>>("/api/words/books");
        Assert.Contains(books, b => b.Id == id);
    }

    [Fact]
    public async Task GetDaily_ReturnsNewWords()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();

        var daily = await api.GetJsonAsync<DailyWordTask>("/api/words/daily");

        Assert.True(daily.HasBook);
        Assert.Equal(20, daily.NewGoal);
        Assert.NotEmpty(daily.NewWords);
        Assert.All(daily.NewWords, w => Assert.False(string.IsNullOrWhiteSpace(w.WordText)));
    }

    [Fact]
    public async Task SubmitLearn_Correct_PromotesToReviewing()
    {
        var (api, word) = await NewUserWithFirstWord();

        var resp = await api.PostAsync("/api/words/learn", new { word_id = word.Id, correct = true, is_review = false, know = false });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var fetched = await api.GetJsonAsync<Word>($"/api/words/{word.Id}");
        Assert.Equal("reviewing", fetched.Status);

        var daily = await api.GetJsonAsync<DailyWordTask>("/api/words/daily");
        Assert.True(daily.NewDone >= 1);
    }

    [Fact]
    public async Task SubmitLearn_Know_MarksMastered()
    {
        var (api, word) = await NewUserWithFirstWord();

        await api.PostAsync("/api/words/learn", new { word_id = word.Id, correct = true, is_review = false, know = true });

        var fetched = await api.GetJsonAsync<Word>($"/api/words/{word.Id}");
        Assert.Equal("mastered", fetched.Status);
    }

    [Fact]
    public async Task SubmitLearn_Wrong_AddsToWrongBook()
    {
        var (api, word) = await NewUserWithFirstWord();

        await api.PostAsync("/api/words/learn", new { word_id = word.Id, correct = false, is_review = false, know = false });

        var wrong = await api.GetJsonAsync<List<Word>>("/api/words/wrong");
        Assert.Contains(wrong, w => w.Id == word.Id && w.InWrongBook);

        var fetched = await api.GetJsonAsync<Word>($"/api/words/{word.Id}");
        Assert.True(fetched.InWrongBook);
    }

    [Fact]
    public async Task RemoveWrongWord()
    {
        var (api, word) = await NewUserWithFirstWord();
        await api.PostAsync("/api/words/learn", new { word_id = word.Id, correct = false, is_review = false, know = false });

        var resp = await api.DeleteAsync($"/api/words/wrong/{word.Id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var wrong = await api.GetJsonAsync<List<Word>>("/api/words/wrong");
        Assert.DoesNotContain(wrong, w => w.Id == word.Id);
    }

    [Fact]
    public async Task GetWord_Unknown_Returns404()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var resp = await api.GetAsync("/api/words/99999999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetBookWords()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var books = await api.GetJsonAsync<List<WordBook>>("/api/words/books");
        var bookId = books.First(b => b.IsDefault).Id;

        var data = await api.GetJsonAsync("/api/words/books/{id}".Replace("{id}", bookId.ToString()));
        var words = data.GetProperty("words");
        Assert.True(words.GetArrayLength() > 0);
    }

    [Fact]
    public async Task SetGoal_Invalid_Returns400()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var resp = await api.PutAsync("/api/words/books/1/goal", new { daily_goal = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}

[Collection("api")]
public class EnglishScenarioTests
{
    private readonly ApiFixture _fixture;
    public EnglishScenarioTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ListScenarios_NonEmpty()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();

        var list = await api.GetJsonAsync<List<Scenario>>("/api/scenarios");
        Assert.NotEmpty(list);
    }

    [Fact]
    public async Task GetScenarioDetail_HasLinesPhrasesQuizzes()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var list = await api.GetJsonAsync<List<Scenario>>("/api/scenarios");
        var id = list[0].Id;

        var detail = await api.GetJsonAsync<ScenarioDetail>($"/api/scenarios/{id}");

        Assert.True(detail.Lines.Count > 0);
        Assert.True(detail.Phrases.Count > 0);
        Assert.True(detail.Quizzes.Count > 0);
    }

    [Fact]
    public async Task SubmitQuiz_Perfect_MarksMastered()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var list = await api.GetJsonAsync<List<Scenario>>("/api/scenarios");
        var id = list[0].Id;
        var detail = await api.GetJsonAsync<ScenarioDetail>($"/api/scenarios/{id}");
        var total = detail.Quizzes.Count;

        var resp = await api.PostAsync("/api/scenarios/{id}/quiz".Replace("{id}", id.ToString()), new { scenario_id = id, total, correct = total });
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts.Web);
        Assert.True(result.GetProperty("mastered").GetBoolean());

        var updated = await api.GetJsonAsync<ScenarioDetail>($"/api/scenarios/{id}");
        Assert.True(updated.Scenario.Mastered);
    }

    [Fact]
    public async Task SubmitQuiz_Poor_NotMastered()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var list = await api.GetJsonAsync<List<Scenario>>("/api/scenarios");
        var id = list[0].Id;
        var detail = await api.GetJsonAsync<ScenarioDetail>($"/api/scenarios/{id}");

        var result = await api.PostJsonAsync("/api/scenarios/{id}/quiz".Replace("{id}", id.ToString()), new { scenario_id = id, total = detail.Quizzes.Count, correct = 0 });
        Assert.False(result.GetProperty("mastered").GetBoolean());
    }

    [Fact]
    public async Task GetScenario_Unknown_Returns404()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var resp = await api.GetAsync("/api/scenarios/99999999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}

[Collection("api")]
public class EnglishSpeakingTests
{
    private readonly ApiFixture _fixture;
    public EnglishSpeakingTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ListTopics_NonEmpty()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();

        var topics = await api.GetJsonAsync<List<SpeakingTopic>>("/api/speaking/topics");
        Assert.NotEmpty(topics);
    }

    [Fact]
    public async Task GetTopicDetail_HasLines()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var topics = await api.GetJsonAsync<List<SpeakingTopic>>("/api/speaking/topics");

        var topic = await api.GetJsonAsync<SpeakingTopic>("/api/speaking/topics/{id}".Replace("{id}", topics[0].Id.ToString()));
        Assert.NotEmpty(topic.Lines);
    }

    [Fact]
    public async Task SaveRecord_UpdatesBestScore()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var topics = await api.GetJsonAsync<List<SpeakingTopic>>("/api/speaking/topics");
        var topicId = topics[0].Id;

        await api.PostAsync("/api/speaking/records", new { topic_id = topicId, line_index = 0, audio_url = "", accuracy = 85, fluency = 80, completeness = 90, overall = 88 });

        var records = await api.GetJsonAsync<List<JsonElement>>("/api/speaking/records");
        Assert.Single(records);
        Assert.Equal(88, records[0].GetProperty("overall").GetInt32());

        var updated = await api.GetJsonAsync<List<SpeakingTopic>>("/api/speaking/topics");
        Assert.Equal(88, updated.First(t => t.Id == topicId).BestScore);
        Assert.Equal(1, updated.First(t => t.Id == topicId).PracticeCount);
    }

    [Fact]
    public async Task GetTopic_Unknown_Returns404()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var resp = await api.GetAsync("/api/speaking/topics/99999999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}

[Collection("api")]
public class EnglishClipTests
{
    private readonly ApiFixture _fixture;
    public EnglishClipTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ListClips_NonEmpty()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();

        var clips = await api.GetJsonAsync<List<VideoClip>>("/api/clips");
        Assert.NotEmpty(clips);
    }

    [Fact]
    public async Task GetClipDetail_HasLines()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var clips = await api.GetJsonAsync<List<VideoClip>>("/api/clips");
        var id = clips[0].Id;

        var json = await api.GetJsonAsync("/api/clips/{id}".Replace("{id}", id.ToString()));
        Assert.Equal(clips[0].Title, json.GetProperty("clip").GetProperty("title").GetString());
        Assert.True(json.GetProperty("lines").GetArrayLength() > 0);
    }
}

[Collection("api")]
public class EnglishDashboardTests
{
    private readonly ApiFixture _fixture;
    public EnglishDashboardTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Dashboard_ReturnsSummary()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();

        var dash = await api.GetJsonAsync<EnglishDashboard>("/api/english/dashboard");

        Assert.True(dash.TotalWords >= 0);
        Assert.Equal(20, dash.NewGoal);
        Assert.True(dash.ScenarioCount > 0, "应包含种子场景");
        Assert.True(dash.ClipCount > 0, "应包含种子影视切片");
        Assert.True(dash.TotalSeconds >= 0);
    }

    [Fact]
    public async Task SaveSession_AddsDuration()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();

        await api.PostAsync("/api/english/sessions", new { module = "words", start_time = "10:00", end_time = "10:01", duration_seconds = 60 });

        var dash = await api.GetJsonAsync<EnglishDashboard>("/api/english/dashboard");
        Assert.True(dash.TodaySeconds >= 60);
        Assert.True(dash.WeekSeconds >= 60);
        Assert.True(dash.TotalSeconds >= 60);

        var sessions = await api.GetJsonAsync("/api/english/sessions");
        Assert.True(sessions.GetProperty("today").GetInt32() >= 60);
    }

    [Fact]
    public async Task Learn_StartsStreak()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var daily = await api.GetJsonAsync<DailyWordTask>("/api/words/daily");
        var word = daily.NewWords[0];

        var streakBefore = (await api.GetJsonAsync("/api/english/streak")).GetProperty("streak").GetInt32();

        await api.PostAsync("/api/words/learn", new { word_id = word.Id, correct = true, is_review = false, know = false });

        var streakAfter = (await api.GetJsonAsync("/api/english/streak")).GetProperty("streak").GetInt32();
        Assert.True(streakAfter > streakBefore, "学习后连续天数应增加");
    }

    [Fact]
    public async Task Sessions_RejectsZeroDurationGracefully()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();
        var resp = await api.PostAsync("/api/english/sessions", new { module = "words", duration_seconds = 0 });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}