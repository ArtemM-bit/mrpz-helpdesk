namespace Support.Api.DTOs;

public sealed record CategoryStatisticsResponse(
    int CategoryId,
    string CategoryName,
    int TicketCount);
