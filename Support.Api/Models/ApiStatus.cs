namespace Support.Api.Models;

public sealed record ApiStatus(string Name, string Environment, DateTimeOffset CheckedAtUtc);
