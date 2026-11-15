namespace Support.Api.Models;

public sealed class TicketComment
{
    public Guid Id { get; set; }

    public Guid TicketId { get; set; }

    public int AuthorId { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public Ticket Ticket { get; set; } = null!;

    public User Author { get; set; } = null!;
}
