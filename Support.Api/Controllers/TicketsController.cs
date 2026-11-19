using System.ComponentModel.DataAnnotations;
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
    ITicketAssignmentService ticketAssignmentService,
    ITicketStatusService ticketStatusService,
    ITicketCommentService ticketCommentService) : ControllerBase
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

    /// <summary>Шукає заявки за необов'язковими фільтрами та повертає їх у заданому порядку.</summary>
    /// <remarks>
    /// Різні фільтри комбінуються через AND. Пошуковий текст шукається без урахування регістру
    /// в заголовку або описі (OR); значення, що складається лише з пробілів, ігнорується.
    /// За замовчуванням заявки сортуються за датою створення за спаданням.
    /// </remarks>
    /// <param name="request">Необов'язкові параметри пошуку: search, categoryId, priority, status, assigneeId і sortDirection.</param>
    /// <param name="cancellationToken">Токен скасування запиту.</param>
    /// <response code="200">Список заявок, що відповідають фільтрам; якщо збігів немає, повертається порожній масив.</response>
    /// <response code="400">Параметр невалідний; відповідь містить опис помилки для кожного некоректного поля.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TicketResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TicketResponse>>> Search(
        [FromQuery] TicketSearchRequest? request,
        CancellationToken cancellationToken)
    {
        var search = request ?? new TicketSearchRequest();
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(
            search,
            new ValidationContext(search),
            validationResults,
            validateAllProperties: true))
        {
            var errors = validationResults
                .SelectMany(result => result.MemberNames
                    .DefaultIfEmpty(nameof(TicketSearchRequest))
                    .Select(member => new
                    {
                        Member = member,
                        Message = result.ErrorMessage ?? "Значення некоректне."
                    }))
                .GroupBy(error => error.Member)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.Message).ToArray());
            return BadRequest(new ValidationProblemDetails(errors));
        }

        var normalized = search with
        {
            Search = string.IsNullOrWhiteSpace(search.Search) ? null : search.Search.Trim(),
            SortDirection = string.IsNullOrWhiteSpace(search.SortDirection)
                ? "desc"
                : search.SortDirection.Trim().ToLowerInvariant()
        };
        var tickets = await ticketRepository.SearchAsync(normalized, cancellationToken);
        return Ok(tickets.Select(ticket => ticket.ToResponse()).ToList());
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

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TicketResponse>> ChangeStatus(
        Guid id,
        [FromBody] ChangeTicketStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await ticketStatusService.ChangeStatusAsync(id, request, cancellationToken);
            return result.Error switch
            {
                TicketStatusError.InvalidStatus => BadRequest(),
                TicketStatusError.TicketNotFound => NotFound(),
                TicketStatusError.InvalidTransition or TicketStatusError.AssigneeRequired => Conflict(),
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

    [HttpPost("{id:guid}/comments")]
    [ProducesResponseType(typeof(TicketCommentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TicketCommentResponse>> CreateComment(
        Guid id,
        [FromBody] CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await ticketCommentService.CreateAsync(id, request, cancellationToken);
            return result.Error switch
            {
                TicketCommentError.InvalidText => BadRequest(),
                TicketCommentError.TicketNotFound or TicketCommentError.AuthorNotFound => NotFound(),
                TicketCommentError.TicketClosed => Conflict(),
                _ => Created($"/api/tickets/{id}/comments", result.Comment)
            };
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Не вдалося зберегти коментар.");
        }
    }

    [HttpGet("{id:guid}/comments")]
    [ProducesResponseType(typeof(IReadOnlyList<TicketCommentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TicketCommentResponse>>> GetComments(
        Guid id,
        CancellationToken cancellationToken)
    {
        var comments = await ticketRepository.GetCommentsByTicketIdAsync(id, cancellationToken);
        var response = comments
            .Select(comment => new TicketCommentResponse(
                comment.Id,
                comment.Text,
                comment.AuthorId,
                comment.CreatedAt))
            .ToList();

        return Ok(response);
    }
}
