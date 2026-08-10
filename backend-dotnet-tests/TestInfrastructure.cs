using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DayLoop.Api.Tests;

/// <summary>
/// WebApplicationFactory that runs the .NET API in-memory (TestServer) against an isolated temp SQLite DB.
/// A fixed temp DB path is shared across the test run (WAL handles concurrency); fresh users isolate test data.
/// </summary>
public sealed class DayLoopApiFactory : WebApplicationFactory<Program>
{
    static DayLoopApiFactory()
    {
        TestDbPath = Path.Combine(Path.GetTempPath(), "dayloop_api_tests.db");
        try { File.Delete(TestDbPath); } catch { }
        Environment.SetEnvironmentVariable("DAYLOOP_DB_PATH", TestDbPath);
    }

    public static readonly string TestDbPath;
}

/// <summary>
/// JSON options matching the API output (camelCase) and accepting camelCase or snake_case input.
/// </summary>
public static class JsonOpts
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}

/// <summary>
/// Thin HTTP client wrapper that handles register/login + Authorization header + typed JSON helpers.
/// </summary>
public sealed class TestApi : IDisposable
{
    private readonly HttpClient _client;

    public TestApi(HttpClient client)
    {
        _client = client;
    }

    public string Username { get; private set; } = "";
    public string Token { get; private set; } = "";

    /// <summary>Register a fresh unique user and attach the bearer token.</summary>
    public async Task RegisterAsync()
    {
        await RegisterAsync("u_" + Guid.NewGuid().ToString("N")[..12], "test1234");
    }

    /// <summary>Register with explicit credentials and attach the bearer token.</summary>
    public async Task RegisterAsync(string username, string password)
    {
        Username = username;
        var json = await PostJsonAsync("/api/auth/register", new { username, password });
        Token = json.GetProperty("token").GetString()!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
    }

    /// <summary>Login with explicit credentials and attach the bearer token.</summary>
    public async Task LoginAsync(string username, string password)
    {
        Username = username;
        var json = await PostJsonAsync("/api/auth/login", new { username, password });
        Token = json.GetProperty("token").GetString()!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
    }

    public async Task<HttpResponseMessage> GetAsync(string path) => await _client.GetAsync(path);
    public async Task<HttpResponseMessage> PostAsync(string path, object? body = null) =>
        await _client.PostAsJsonAsync(path, body, JsonOpts.Web);
    public async Task<HttpResponseMessage> PutAsync(string path, object? body = null) =>
        await _client.PutAsJsonAsync(path, body, JsonOpts.Web);
    public async Task<HttpResponseMessage> DeleteAsync(string path) => await _client.DeleteAsync(path);

    public async Task<T> GetJsonAsync<T>(string path)
    {
        var resp = await GetAsync(path);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<T>(JsonOpts.Web))!;
    }

    public async Task<T> PostJsonAsync<T>(string path, object? body = null)
    {
        var resp = await PostAsync(path, body);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<T>(JsonOpts.Web))!;
    }

    public async Task<T> PutJsonAsync<T>(string path, object? body = null)
    {
        var resp = await PutAsync(path, body);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<T>(JsonOpts.Web))!;
    }

    /// <summary>Send an arbitrary-method JSON request (e.g. DELETE with a body) and parse the response.</summary>
    public async Task<JsonElement> SendJsonAsync(HttpMethod method, string path, object? body = null)
    {
        using var req = new HttpRequestMessage(method, path);
        if (body != null)
            req.Content = JsonContent.Create(body, options: JsonOpts.Web);
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    public async Task<JsonElement> GetJsonAsync(string path)
    {
        var resp = await GetAsync(path);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    public async Task<JsonElement> PostJsonAsync(string path, object? body = null)
    {
        var resp = await PostAsync(path, body);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    public async Task<JsonElement> PutJsonAsync(string path, object? body = null)
    {
        var resp = await PutAsync(path, body);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    public void Dispose() => _client.Dispose();
}

/// <summary>
/// Shared collection fixture: single factory (single host + isolated temp DB) reused by every test class,
/// executed sequentially (no cross-class parallel conflicts).
/// </summary>
[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
}

public sealed class ApiFixture : IDisposable
{
    public DayLoopApiFactory Factory { get; } = new();

    public TestApi NewUser() => new(Factory.CreateClient());

    public void Dispose() => Factory.Dispose();
}
