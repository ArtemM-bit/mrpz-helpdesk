namespace Support.Api.DTOs;

/// <summary>Кількість заявок у категорії за всіма статусами.</summary>
/// <param name="CategoryId">Цілий ID категорії.</param>
/// <param name="CategoryName">Назва категорії.</param>
/// <param name="TicketCount">Кількість заявок зі статусом New, InProgress, Resolved або Closed; для порожньої категорії дорівнює нулю.</param>
public sealed record CategoryStatisticsResponse(
    int CategoryId,
    string CategoryName,
    int TicketCount);
