namespace Support.Api.Services;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
