using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Support.Api.Services;

public sealed class HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
