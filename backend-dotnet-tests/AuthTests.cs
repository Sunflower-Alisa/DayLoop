using System.Net;
using System.Text.Json;

namespace DayLoop.Api.Tests;

[Collection("api")]
public class AuthTests
{
    private readonly ApiFixture _fixture;
    public AuthTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Register_ReturnsTokenAndUser()
    {
        var api = _fixture.NewUser();
        var json = await api.PostJsonAsync("/api/auth/register", new { username = "alice_" + Guid.NewGuid().ToString("N")[..6], password = "pass1234" });

        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("token").GetString()));
        Assert.Equal("alice", json.GetProperty("user").GetProperty("username").GetString()?.Substring(0, 5));
        Assert.True(json.GetProperty("user").GetProperty("id").GetInt64() > 0);
    }

    [Fact]
    public async Task Register_DuplicateUsername_Returns400()
    {
        var api = _fixture.NewUser();
        var name = "dup_" + Guid.NewGuid().ToString("N")[..6];
        await api.PostAsync("/api/auth/register", new { username = name, password = "pass1234" });
        var resp = await api.PostAsync("/api/auth/register", new { username = name, password = "pass1234" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Login_Success_ReturnsToken()
    {
        var api = _fixture.NewUser();
        var name = "login_" + Guid.NewGuid().ToString("N")[..6];
        await api.RegisterAsync(name, "pass1234");

        var json = await api.PostJsonAsync("/api/auth/login", new { username = name, password = "pass1234" });
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("token").GetString()));
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var api = _fixture.NewUser();
        var name = "bad_" + Guid.NewGuid().ToString("N")[..6];
        await api.RegisterAsync(name, "pass1234");

        var resp = await api.PostAsync("/api/auth/login", new { username = name, password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsUser()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();

        var json = await api.GetJsonAsync("/api/auth/me");
        Assert.Equal(api.Username, json.GetProperty("username").GetString());
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var api = _fixture.NewUser();
        var resp = await api.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ThenLoginWithNew()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync("cp_" + Guid.NewGuid().ToString("N")[..6], "oldpass");

        await api.PutJsonAsync("/api/auth/password", new { oldPassword = "oldpass", newPassword = "newpass" });

        var loginResp = await api.PostAsync("/api/auth/login", new { username = api.Username, password = "newpass" });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WrongOld_Returns400()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync("cpw_" + Guid.NewGuid().ToString("N")[..6], "oldpass");

        var resp = await api.PutAsync("/api/auth/password", new { oldPassword = "nope", newPassword = "newpass" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_RemovesUser()
    {
        var api = _fixture.NewUser();
        await api.RegisterAsync();

        var resp = await api.DeleteAsync("/api/auth/account");
        var body = await resp.Content.ReadAsStringAsync();
        Assert.True(resp.StatusCode == HttpStatusCode.OK, $"StatusCode={resp.StatusCode} Body={body}");

        var loginResp = await api.PostAsync("/api/auth/login", new { username = api.Username, password = "test1234" });
        Assert.Equal(HttpStatusCode.Unauthorized, loginResp.StatusCode);
    }
}