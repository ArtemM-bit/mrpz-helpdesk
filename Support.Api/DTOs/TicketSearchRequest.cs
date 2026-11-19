using System.ComponentModel.DataAnnotations;
using Support.Api.Models;

namespace Support.Api.DTOs;

/// <summary>Необов'язкові query-фільтри для пошуку заявок.</summary>
/// <param name="Search">Необов'язковий текст для пошуку без урахування регістру в Title або Description; зовнішні пробіли обрізаються, пробільний запит ігнорується.</param>
/// <param name="CategoryId">Необов'язковий додатний ID категорії; початкові категорії мають ID 1–5.</param>
/// <param name="Priority">Необов'язковий визначений цілий код пріоритету: 0 Low, 1 Medium, 2 High або 3 Critical.</param>
/// <param name="Status">Необов'язковий визначений цілий код статусу: 0 New, 1 InProgress, 2 Resolved або 3 Closed.</param>
/// <param name="AssigneeId">Необов'язковий додатний ID відповідального працівника.</param>
/// <param name="SortDirection">Необов'язкове asc або desc; за замовчуванням desc. Порядок при однаковій даті створення стабільний за ID за зростанням.</param>
public sealed record TicketSearchRequest(
    string? Search = null,
    int? CategoryId = null,
    Priority? Priority = null,
    Status? Status = null,
    int? AssigneeId = null,
    string? SortDirection = "desc") : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CategoryId is <= 0)
        {
            yield return new ValidationResult(
                "CategoryId має бути більшим за 0.",
                [nameof(CategoryId)]);
        }

        if (AssigneeId is <= 0)
        {
            yield return new ValidationResult(
                "AssigneeId має бути більшим за 0.",
                [nameof(AssigneeId)]);
        }

        if (Priority.HasValue && !Enum.IsDefined(Priority.Value))
        {
            yield return new ValidationResult(
                "Priority має бути визначеним значенням: 0, 1, 2 або 3.",
                [nameof(Priority)]);
        }

        if (Status.HasValue && !Enum.IsDefined(Status.Value))
        {
            yield return new ValidationResult(
                "Status має бути визначеним значенням: 0, 1, 2 або 3.",
                [nameof(Status)]);
        }

        var sortDirection = SortDirection?.Trim();
        if (sortDirection is not null
            && !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "SortDirection має бути 'asc' або 'desc'.",
                [nameof(SortDirection)]);
        }
    }
}
