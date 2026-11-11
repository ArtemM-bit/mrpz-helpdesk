using Support.Api.Models;

namespace Support.Api.Services;

public sealed class StatusService(IHostEnvironment environment, IClock clock) : IStatusService
{
    public ApiStatus GetStatus() => new("Support.Api", environment.EnvironmentName, clock.UtcNow);
}
