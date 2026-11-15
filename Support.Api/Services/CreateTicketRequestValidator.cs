using Microsoft.EntityFrameworkCore;
using Support.Api.Data;
using Support.Api.DTOs;
using Support.Api.Models;

namespace Support.Api.Services;

public sealed class CreateTicketRequestValidator(SupportDbContext dbContext)
{
    public async Task<CreateTicketRequestValidationResult> ValidateAsync(
        CreateTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new Dictionary<string, List<string>>();
        var title = request.Title?.Trim();
        var description = request.Description?.Trim();

        if (string.IsNullOrEmpty(title))
        {
            AddError(nameof(CreateTicketRequest.Title), "Заголовок є обов'язковим.");
        }
        else if (title.Length > 150)
        {
            AddError(nameof(CreateTicketRequest.Title), "Заголовок має містити не більше 150 символів.");
        }

        if (string.IsNullOrEmpty(description))
        {
            AddError(nameof(CreateTicketRequest.Description), "Опис є обов'язковим.");
        }
        else if (description.Length > 4000)
        {
            AddError(nameof(CreateTicketRequest.Description), "Опис має містити не більше 4000 символів.");
        }

        if (!await dbContext.Categories.AnyAsync(category => category.Id == request.CategoryId, cancellationToken))
        {
            AddError(nameof(CreateTicketRequest.CategoryId), "Вказано невідому категорію.");
        }

        if (!Enum.IsDefined(request.Priority))
        {
            AddError(nameof(CreateTicketRequest.Priority), "Вказано непідтримуваний пріоритет.");
        }

        var fieldErrors = errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToArray());
        var normalizedRequest = fieldErrors.Count == 0
            ? request with { Title = title!, Description = description! }
            : null;

        return new CreateTicketRequestValidationResult(normalizedRequest, fieldErrors);

        void AddError(string field, string message)
        {
            if (!errors.TryGetValue(field, out var messages))
            {
                messages = [];
                errors.Add(field, messages);
            }

            messages.Add(message);
        }
    }
}

public sealed record CreateTicketRequestValidationResult(
    CreateTicketRequest? NormalizedRequest,
    IReadOnlyDictionary<string, string[]> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
