using Support.Api.Models;

namespace Support.Api.DTOs;

/// <summary>Заявка, повернута API пошуку або читання.</summary>
/// <param name="Id">UUID заявки.</param>
/// <param name="Title">Заголовок заявки.</param>
/// <param name="Description">Опис заявки.</param>
/// <param name="CategoryId">ID категорії.</param>
/// <param name="Priority">Числовий код пріоритету: 0 Low, 1 Medium, 2 High або 3 Critical.</param>
/// <param name="Status">Числовий код статусу: 0 New, 1 InProgress, 2 Resolved або 3 Closed.</param>
/// <param name="CreatedAt">Час створення у форматі ISO 8601.</param>
/// <param name="AssigneeId">ID призначеного працівника або null; параметра фільтрації за відповідальним немає.</param>
/// <param name="ResolvedAt">Час переходу в Resolved у форматі ISO 8601 або null.</param>
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
