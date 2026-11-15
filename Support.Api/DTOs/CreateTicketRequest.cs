using System.ComponentModel.DataAnnotations;
using Support.Api.Models;

namespace Support.Api.DTOs;

public sealed record CreateTicketRequest
{
    [Required]
    [MaxLength(150)]
    public required string Title { get; init; }

    [Required]
    [MaxLength(4000)]
    public required string Description { get; init; }

    public int CategoryId { get; init; }

    public Priority Priority { get; init; }
}
