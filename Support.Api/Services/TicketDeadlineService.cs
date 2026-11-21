using System.Globalization;
using Support.Api.Data;
using Support.Api.DTOs;
using Support.Api.Models;

namespace Support.Api.Services;

public sealed class TicketDeadlineService(
    ITicketRepository ticketRepository,
    IClock clock) : ITicketDeadlineService
{
    private static readonly string[] DueAtFormats =
    [
        "yyyy-MM-dd'T'HH:mmzzz",
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"
    ];

    public async Task<TicketDeadlineResult> UpdateAsync(
        Guid ticketId,
        UpdateDeadlineRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ticket = await ticketRepository.GetForAssignmentAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            return new TicketDeadlineResult(null, TicketDeadlineError.TicketNotFound, EmptyErrors());
        }

        if (ticket.Status is Status.Resolved or Status.Closed)
        {
            return new TicketDeadlineResult(null, TicketDeadlineError.InvalidStatus, EmptyErrors());
        }

        if (!TryParseDueAt(request.DueAt, out var dueAt))
        {
            return InvalidDueAt("Дата має бути у форматі ISO 8601 з часовим зміщенням або Z.");
        }

        var now = clock.UtcNow.ToUniversalTime();
        dueAt = dueAt.ToUniversalTime();
        if (dueAt <= now)
        {
            return InvalidDueAt("Термін виконання має бути пізніше за поточний час.");
        }

        ticket.DueAt = dueAt;
        await ticketRepository.SaveChangesAsync(cancellationToken);

        return new TicketDeadlineResult(ticket.ToResponse(now), TicketDeadlineError.None, EmptyErrors());
    }

    private static bool TryParseDueAt(string? value, out DateTimeOffset dueAt)
    {
        dueAt = default;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var normalizedValue = value.EndsWith('Z')
            ? value[..^1] + "+00:00"
            : value;

        return DateTimeOffset.TryParseExact(
            normalizedValue,
            DueAtFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out dueAt);
    }

    private static TicketDeadlineResult InvalidDueAt(string message) =>
        new(
            null,
            TicketDeadlineError.InvalidDueAt,
            new Dictionary<string, string[]>
            {
                [nameof(UpdateDeadlineRequest.DueAt)] = [message]
            });

    private static IReadOnlyDictionary<string, string[]> EmptyErrors() =>
        new Dictionary<string, string[]>();
}
