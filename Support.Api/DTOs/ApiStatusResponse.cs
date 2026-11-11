namespace Support.Api.DTOs;

public sealed record ApiStatusResponse(string Name, string Environment, DateTimeOffset CheckedAtUtc);
