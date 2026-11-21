using System.ComponentModel.DataAnnotations;

namespace Support.Api.DTOs;

public sealed record UpdateDeadlineRequest
{
    [Required]
    public string? DueAt { get; init; }
}
