namespace Support.Api.Services;

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
}
