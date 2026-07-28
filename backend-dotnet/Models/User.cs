using System.Text.Json.Serialization;

namespace DayLoop.Api.Models;

public class User
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("username")] public string Username { get; set; } = "";
    [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
}

public class RegisterRequest
{
    [JsonPropertyName("username")] public string Username { get; set; } = "";
    [JsonPropertyName("password")] public string Password { get; set; } = "";
}

public class LoginRequest
{
    [JsonPropertyName("username")] public string Username { get; set; } = "";
    [JsonPropertyName("password")] public string Password { get; set; } = "";
}

public class LoginResponse
{
    [JsonPropertyName("token")] public string Token { get; set; } = "";
    [JsonPropertyName("user")] public User User { get; set; } = new();
}

public class ChangePasswordRequest
{
    [JsonPropertyName("oldPassword")] public string OldPassword { get; set; } = "";
    [JsonPropertyName("newPassword")] public string NewPassword { get; set; } = "";
}
