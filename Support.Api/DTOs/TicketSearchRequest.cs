using Support.Api.Models;

namespace Support.Api.DTOs;

public sealed record TicketSearchRequest(
    Status? Status = null,
    Priority? Priority = null,
    int? CategoryId = null,
    string? SortDirection = null,
    string? SearchText = null);
