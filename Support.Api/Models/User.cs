namespace Support.Api.Models;

public sealed class User
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public bool IsSupportEmployee { get; set; }

    public ICollection<Ticket> AssignedTickets { get; set; } = [];

    public ICollection<TicketComment> Comments { get; set; } = [];
}
