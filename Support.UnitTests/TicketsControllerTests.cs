using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Support.Api.Controllers;
using Support.Api.Data;
using Support.Api.DTOs;
using Support.Api.Models;
using Support.Api.Services;

namespace Support.UnitTests;

public sealed class TicketsControllerTests
{
    [Fact]
    public void Create_HasPostRouteAtApiTickets()
    {
        var controllerRoute = typeof(TicketsController).GetCustomAttribute<RouteAttribute>();
        var createMethod = typeof(TicketsController).GetMethod(nameof(TicketsController.Create));
        var getByIdMethod = typeof(TicketsController).GetMethod(nameof(TicketsController.GetById));

        Assert.Equal("api/tickets", controllerRoute?.Template);
        Assert.NotNull(createMethod);
        Assert.NotNull(createMethod.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(getByIdMethod);
        Assert.Equal("{id:guid}", getByIdMethod.GetCustomAttribute<HttpGetAttribute>()?.Template);
    }

    [Fact]
    public async Task Create_ValidRequest_Returns201WithTicketAndLocation()
    {
        var ticket = CreateResponse();
        var controller = new TicketsController(new StubTicketCreationService(
            _ => Task.FromResult(new CreateTicketResult(ticket, EmptyErrors()))),
            new StubTicketRepository());

        var actionResult = await controller.Create(ValidRequest(), CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(actionResult);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal($"/api/tickets/{ticket.Id}", created.Location);
        Assert.Same(ticket, created.Value);
    }

    [Fact]
    public async Task Create_InvalidRequest_Returns400WithFieldErrors()
    {
        var errors = new Dictionary<string, string[]>
        {
            [nameof(CreateTicketRequest.Title)] = ["Title is required."]
        };
        var controller = new TicketsController(new StubTicketCreationService(
            _ => Task.FromResult(new CreateTicketResult(null, errors))),
            new StubTicketRepository());

        var actionResult = await controller.Create(ValidRequest(), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Same(errors, badRequest.Value);
    }

    [Fact]
    public async Task Create_StorageFailure_ReturnsSafeNon201Response()
    {
        const string privateDatabaseDetails = "private database connection details";
        var controller = new TicketsController(new StubTicketCreationService(
            _ => Task.FromException<CreateTicketResult>(new DbUpdateException(privateDatabaseDetails))),
            new StubTicketRepository());

        var actionResult = await controller.Create(ValidRequest(), CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status500InternalServerError, error.StatusCode);
        Assert.NotEqual(StatusCodes.Status201Created, error.StatusCode);
        Assert.DoesNotContain(privateDatabaseDetails, JsonSerializer.Serialize(error.Value));
    }

    [Fact]
    public async Task GetById_ExistingTicket_Returns200WithTicketResponse()
    {
        var ticket = CreateResponse();
        var controller = new TicketsController(
            new StubTicketCreationService(_ => throw new InvalidOperationException()),
            new StubTicketRepository(_ => Task.FromResult<Ticket?>(new Ticket
            {
                Id = ticket.Id,
                Title = ticket.Title,
                Description = ticket.Description,
                CategoryId = ticket.CategoryId,
                Priority = ticket.Priority,
                Status = ticket.Status,
                CreatedAt = ticket.CreatedAt
            })));

        var result = await controller.GetById(ticket.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TicketResponse>(ok.Value);
        Assert.Equal(ticket, response);
    }

    [Fact]
    public async Task GetById_MissingTicket_Returns404()
    {
        var controller = new TicketsController(
            new StubTicketCreationService(_ => throw new InvalidOperationException()),
            new StubTicketRepository(_ => Task.FromResult<Ticket?>(null)));

        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static CreateTicketRequest ValidRequest() => new()
    {
        Title = "Printer issue",
        Description = "The printer is not working.",
        CategoryId = 1,
        Priority = Priority.High
    };

    private static TicketResponse CreateResponse() => new(
        Guid.NewGuid(),
        "Printer issue",
        "The printer is not working.",
        1,
        Priority.High,
        Status.New,
        DateTimeOffset.UtcNow);

    private static IReadOnlyDictionary<string, string[]> EmptyErrors() =>
        new Dictionary<string, string[]>();

    private sealed class StubTicketCreationService(
        Func<CreateTicketRequest, Task<CreateTicketResult>> createAsync) : ITicketCreationService
    {
        public Task<CreateTicketResult> CreateAsync(
            CreateTicketRequest request,
            CancellationToken cancellationToken = default) => createAsync(request);
    }

    private sealed class StubTicketRepository(
        Func<Guid, Task<Ticket?>>? getByIdAsync = null) : ITicketRepository
    {
        public Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            getByIdAsync?.Invoke(id) ?? Task.FromResult<Ticket?>(null);
    }
}
