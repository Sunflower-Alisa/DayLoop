using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.IdentityModel.Tokens;
using DayLoop.Api.Data;
using DayLoop.Api.Models;

namespace DayLoop.Api.Services;

public static class AuthService
{
    private static readonly string JwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "DayLoop-Default-Secret-Key-2026-Change-In-Production!";
    private static readonly string Issuer = "DayLoop";
    private static readonly string Audience = "DayLoop-Users";

    public static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 10);
    }

    public static bool VerifyPassword(string password, string stored)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, stored);
        }
        catch
        {
            return false;
        }
    }

    public static string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static long? GetUserIdFromToken(string? authHeader)
    {
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return null;

        var tokenStr = authHeader["Bearer ".Length..].Trim();
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
            var result = handler.ValidateToken(tokenStr, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                ValidAudience = Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
            }, out _);

            var idClaim = result.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim != null && long.TryParse(idClaim.Value, out var userId))
                return userId;
        }
        catch { }
        return null;
    }

    public static User? GetUser(long userId)
    {
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, username, created_at FROM users WHERE id = @p0";
        cmd.Parameters.AddWithValue("@p0", userId);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new User
            {
                Id = reader.GetInt64(0),
                Username = reader.GetString(1),
                CreatedAt = reader.IsDBNull(2) ? "" : reader.GetString(2),
            };
        }
        return null;
    }

    public static (User? user, string? error) Register(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length < 2)
            return (null, "用户名至少2个字符");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            return (null, "密码至少4个字符");

        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT id FROM users WHERE username = @p0";
        cmd.Parameters.AddWithValue("@p0", username);
        if (cmd.ExecuteScalar() != null)
            return (null, "用户名已存在");

        cmd.CommandText = "INSERT INTO users (username, password_hash) VALUES (@p0, @p1)";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", username);
        cmd.Parameters.AddWithValue("@p1", HashPassword(password));
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT last_insert_rowid()";
        var id = (long)cmd.ExecuteScalar()!;

        return (new User { Id = id, Username = username }, null);
    }

    public static (User? user, string? error) Login(string username, string password)
    {
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, username, password_hash, created_at FROM users WHERE username = @p0";
        cmd.Parameters.AddWithValue("@p0", username);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return (null, "用户名或密码错误");

        var hash = reader.IsDBNull(2) ? "" : reader.GetString(2);
        if (!VerifyPassword(password, hash))
            return (null, "用户名或密码错误");

        return (new User
        {
            Id = reader.GetInt64(0),
            Username = reader.GetString(1),
            CreatedAt = reader.IsDBNull(3) ? "" : reader.GetString(3),
        }, null);
    }

    public static string? ChangePassword(long userId, string oldPassword, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
            return "新密码至少4个字符";

        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT password_hash FROM users WHERE id = @p0";
        cmd.Parameters.AddWithValue("@p0", userId);
        var hash = cmd.ExecuteScalar() as string;
        if (hash == null) return "用户不存在";
        if (!VerifyPassword(oldPassword, hash)) return "旧密码错误";

        cmd.CommandText = "UPDATE users SET password_hash = @p0 WHERE id = @p1";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@p0", HashPassword(newPassword));
        cmd.Parameters.AddWithValue("@p1", userId);
        cmd.ExecuteNonQuery();
        return null;
    }

    public static string? DeleteAccount(long userId)
    {
        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM users WHERE id = @p0";
        cmd.Parameters.AddWithValue("@p0", userId);
        if (cmd.ExecuteScalar() == null)
            return "用户不存在";

        using var tx = conn.BeginTransaction();
        try
        {
            cmd.CommandText = "DELETE FROM tasks WHERE user_id = @p0";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "DELETE FROM daily_reviews WHERE user_id = @p0";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "DELETE FROM recurring_templates WHERE user_id = @p0";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "DELETE FROM notes WHERE user_id = @p0";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "DELETE FROM note_categories WHERE user_id = @p0";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "DELETE FROM users WHERE id = @p0";
            cmd.ExecuteNonQuery();
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            return "删除账号失败";
        }
        return null;
    }
}
