using Microsoft.AspNetCore.Mvc;
using DayLoop.Api.Models;
using DayLoop.Api.Services;

namespace DayLoop.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterRequest req)
    {
        var (user, error) = AuthService.Register(req.Username, req.Password);
        if (error != null)
            return BadRequest(new { error });

        var token = AuthService.GenerateToken(user!);
        return Ok(new LoginResponse { Token = token, User = user! });
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        var (user, error) = AuthService.Login(req.Username, req.Password);
        if (error != null)
            return Unauthorized(new { error });

        var token = AuthService.GenerateToken(user!);
        return Ok(new LoginResponse { Token = token, User = user! });
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId = AuthService.GetUserIdFromToken(Request.Headers.Authorization);
        if (userId == null)
            return Unauthorized(new { error = "未登录" });

        var user = AuthService.GetUser(userId.Value);
        if (user == null)
            return Unauthorized(new { error = "用户不存在" });

        return Ok(user);
    }

    [HttpPut("password")]
    public IActionResult ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var userId = AuthService.GetUserIdFromToken(Request.Headers.Authorization);
        if (userId == null)
            return Unauthorized(new { error = "未登录" });

        var error = AuthService.ChangePassword(userId.Value, req.OldPassword, req.NewPassword);
        if (error != null)
            return BadRequest(new { error });

        return Ok(new { message = "密码修改成功" });
    }

    [HttpDelete("account")]
    public IActionResult DeleteAccount()
    {
        var userId = AuthService.GetUserIdFromToken(Request.Headers.Authorization);
        if (userId == null)
            return Unauthorized(new { error = "未登录" });

        var error = AuthService.DeleteAccount(userId.Value);
        if (error != null)
            return BadRequest(new { error });

        return Ok(new { message = "账号已删除" });
    }
}
