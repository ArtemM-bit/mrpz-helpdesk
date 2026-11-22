using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Support.Api.Services;

public sealed class HttpCurrentUserContext(
    IHttpContextAccessor httpContextAccessor,
    IHostEnvironment environment) : ICurrentUserContext
{
    public const string DevelopmentUserIdHeader = "X-User-Id";

    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public string? UserId
    {
        get
        {
            if (IsAuthenticated)
            {
                return User?.FindFirstValue(ClaimTypes.NameIdentifier);
            }

            if (environment.IsDevelopment()
                && httpContextAccessor.HttpContext?.Request.Headers.TryGetValue(
                    DevelopmentUserIdHeader, out var values) == true
                && values.Count == 1)
            {
                return values[0];
            }

            return null;
        }
    }
}
