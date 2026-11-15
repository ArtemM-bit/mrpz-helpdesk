using System.Globalization;
using Support.Api.Data;
using Support.Api.DTOs;
using Support.Api.Models;

namespace Support.Api.Services;

public sealed class TicketCommentService(
    ITicketRepository ticketRepository,
    ICurrentUserContext currentUserContext,
    IClock clock) : ITicketCommentService
{
    public async Task<TicketCommentResult> CreateAsync(
        Guid ticketId,
        CreateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var text = request.Text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 2000)
        {
            return new TicketCommentResult(null, TicketCommentError.InvalidText);
        }

        var ticket = await ticketRepository.GetForAssignmentAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            return new TicketCommentResult(null, TicketCommentError.TicketNotFound);
        }

        if (!int.TryParse(currentUserContext.UserId, NumberStyles.None, CultureInfo.InvariantCulture, out var authorId))
        {
            return new TicketCommentResult(null, TicketCommentError.AuthorNotFound);
        }

        var author = await ticketRepository.GetUserByIdAsync(authorId, cancellationToken);
        if (author is null)
        {
            return new TicketCommentResult(null, TicketCommentError.AuthorNotFound);
        }

        if (ticket.Status == Status.Closed)
        {
            return new TicketCommentResult(null, TicketCommentError.TicketClosed);
        }

        var comment = new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            AuthorId = author.Id,
            Text = text,
            CreatedAt = clock.UtcNow,
            Author = author
        };

        await ticketRepository.AddCommentAsync(comment, cancellationToken);

        return new TicketCommentResult(
            new TicketCommentResponse(comment.Id, comment.Text, comment.AuthorId, comment.CreatedAt),
            TicketCommentError.None);
    }
}
