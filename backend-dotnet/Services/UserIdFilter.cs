namespace DayLoop.Api.Services;

/// <summary>
/// Helper to extract the current user_id from the request's Authorization header.
/// Every controller action should call GetUserId() and error if null.
/// </summary>
public static class UserIdFilter
{
    public static long? GetUserId(HttpRequest request)
    {
        return AuthService.GetUserIdFromToken(request.Headers.Authorization);
    }

    public static long RequireUserId(HttpRequest request)
    {
        return GetUserId(request) ?? throw new UnauthorizedAccessException("未登录");
    }
}
