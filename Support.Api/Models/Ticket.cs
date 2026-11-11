namespace Support.Api.Models;

public sealed class Ticket
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int CategoryId { get; set; }

    public Priority Priority { get; set; }

    public Status Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Category Category { get; set; } = null!;
}
