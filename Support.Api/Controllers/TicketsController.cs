using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Support.Api.Data;
using Support.Api.DTOs;
using Support.Api.Services;

namespace Support.Api.Controllers;

[ApiController]
[Route("api/tickets")]
public sealed class TicketsController(
    ITicketCreationService ticketCreationService,
    ITicketRepository ticketRepository,
    ITicketAssignmentService ticketAssignmentService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Dictionary<string, string[]>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await ticketCreationService.CreateAsync(request, cancellationToken);
            if (!result.IsValid)
            {
                return BadRequest(result.Errors);
            }

            var ticket = result.Ticket!;
            return Created($"/api/tickets/{ticket.Id}", ticket);
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Не вдалося зберегти заявку.");
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetByIdAsync(id, cancellationToken);
        return ticket is null ? NotFound() : Ok(ticket.ToResponse());
    }

    [HttpPatch("{id:guid}/assignee")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TicketResponse>> Assign(
        Guid id,
        [FromBody] AssignTicketRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await ticketAssignmentService.AssignAsync(id, request, cancellationToken);
            return result.Error switch
            {
                TicketAssignmentError.TicketNotFound => NotFound(),
                TicketAssignmentError.InvalidAssignee => BadRequest(),
                TicketAssignmentError.TicketClosed => Conflict(),
                _ => Ok(result.Ticket)
            };
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Не вдалося зберегти заявку.");
        }
    }
}
