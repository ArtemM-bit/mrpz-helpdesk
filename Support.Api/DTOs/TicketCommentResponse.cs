namespace Support.Api.DTOs;

public sealed record TicketCommentResponse(
    Guid Id,
    string Text,
    int AuthorId,
    DateTimeOffset CreatedAt);
