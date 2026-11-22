using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Support.Api.Services;

namespace Support.UnitTests;

public sealed class HttpCurrentUserContextTests
{
    [Fact]
    public void Development_AnonymousRequest_UsesHeaderWithoutAuthenticating()
    {
        var context = CreateContext(Environments.Development, "2");

        Assert.Equal("2", context.UserId);
        Assert.False(context.IsAuthenticated);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void OutsideDevelopment_IgnoresHeader(string environment)
    {
        Assert.Null(CreateContext(environment, "2").UserId);
    }

    [Fact]
    public void Development_MissingOrMultipleHeaderValues_ReturnsNoUser()
    {
        Assert.Null(CreateContext(Environments.Development, StringValues.Empty).UserId);
        Assert.Null(CreateContext(Environments.Development, new[] { "1", "2" }).UserId);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void AuthenticatedRequest_UsesClaimInsteadOfHeader(string environment)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "1")], "Test"));
        var context = CreateContext(environment, "2", user);

        Assert.Equal("1", context.UserId);
        Assert.True(context.IsAuthenticated);
    }

    [Fact]
    public void AuthenticatedRequestWithoutId_DoesNotFallBackToHeader()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([], "Test"));

        Assert.Null(CreateContext(Environments.Development, "2", user).UserId);
    }

    private static HttpCurrentUserContext CreateContext(
        string environment, StringValues header, ClaimsPrincipal? user = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[HttpCurrentUserContext.DevelopmentUserIdHeader] = header;
        if (user is not null)
        {
            httpContext.User = user;
        }

        return new HttpCurrentUserContext(
            new HttpContextAccessor { HttpContext = httpContext },
            new TestEnvironment { EnvironmentName = environment });
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Support.UnitTests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
