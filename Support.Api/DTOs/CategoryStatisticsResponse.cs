namespace Support.Api.DTOs;

/// <summary>Кількість усіх збережених заявок у категорії, незалежно від статусу чи фільтрів пошуку.</summary>
/// <param name="CategoryId">Цілий ID категорії.</param>
/// <param name="CategoryName">Назва категорії.</param>
/// <param name="TicketCount">Кількість усіх збережених заявок у категорії, незалежно від статусу чи фільтрів; для порожньої категорії дорівнює нулю.</param>
public sealed record CategoryStatisticsResponse(
    int CategoryId,
    string CategoryName,
    int TicketCount);
