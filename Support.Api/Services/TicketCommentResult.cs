using Support.Api.DTOs;

namespace Support.Api.Services;

public enum TicketCommentError
{
    None,
    InvalidText,
    TicketNotFound,
    AuthorNotFound,
    TicketClosed
}

public sealed record TicketCommentResult(
    TicketCommentResponse? Comment,
    TicketCommentError Error)
{
    public bool IsSuccessful => Error == TicketCommentError.None;
}
