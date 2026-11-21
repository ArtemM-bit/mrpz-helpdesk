using System.ComponentModel.DataAnnotations;

namespace Support.Api.DTOs;

/// <summary>Запит на встановлення або заміну терміну виконання заявки.</summary>
public sealed record UpdateDeadlineRequest
{
    /// <summary>
    /// ISO 8601 час з часовим зміщенням або Z. Новий термін має бути строго пізніше за поточний UTC-час сервера.
    /// </summary>
    [Required]
    public string? DueAt { get; init; }
}
