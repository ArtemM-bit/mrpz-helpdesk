using Support.Api.Models;

namespace Support.Api.DTOs;

public sealed record TicketResponse(
    Guid Id,
    string Title,
    string Description,
    int CategoryId,
    Priority Priority,
    Status Status,
    DateTimeOffset CreatedAt,
    int? AssigneeId,
    DateTimeOffset? ResolvedAt);
