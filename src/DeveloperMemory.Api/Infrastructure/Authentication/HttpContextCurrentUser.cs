using System.Security.Claims;
using DeveloperMemory.Application.Contracts;

namespace DeveloperMemory.Api.Infrastructure.Authentication;

/// <summary>
/// Resolves the current user from HttpContext (ClaimsPrincipal).
/// Adapts ASP.NET Core authentication into the Application layer's ICurrentUser abstraction.
/// </summary>
public class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public string UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? string.Empty;

    public string? DisplayName =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);
}
