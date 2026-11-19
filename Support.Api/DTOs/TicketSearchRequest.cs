using Support.Api.Models;

namespace Support.Api.DTOs;

/// <summary>Необов'язкові query-фільтри для пошуку заявок.</summary>
/// <param name="Status">Необов'язковий цілий код статусу: 0 New, 1 InProgress, 2 Resolved або 3 Closed.</param>
/// <param name="Priority">Необов'язковий цілий код пріоритету: 0 Low, 1 Medium, 2 High або 3 Critical.</param>
/// <param name="CategoryId">Необов'язковий ID категорії; початкові категорії мають ID 1–5.</param>
/// <param name="SortDirection">Необов'язкове asc або desc; за замовчуванням desc. Порядок при однаковій даті створення стабільний за ID за зростанням.</param>
/// <param name="SearchText">Необов'язковий текст для пошуку без урахування регістру в Title або Description; зовнішні пробіли обрізаються, пробільний запит ігнорується.</param>
public sealed record TicketSearchRequest(
    Status? Status = null,
    Priority? Priority = null,
    int? CategoryId = null,
    string? SortDirection = null,
    string? SearchText = null);
