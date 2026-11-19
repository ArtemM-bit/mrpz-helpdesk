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
        var searchMethod = typeof(TicketsController).GetMethod(nameof(TicketsController.Search));
        var getByIdMethod = typeof(TicketsController).GetMethod(nameof(TicketsController.GetById));
        var assignMethod = typeof(TicketsController).GetMethod(nameof(TicketsController.Assign));
        var changeStatusMethod = typeof(TicketsController).GetMethod(nameof(TicketsController.ChangeStatus));
        var createCommentMethod = typeof(TicketsController).GetMethod(nameof(TicketsController.CreateComment));
        var getCommentsMethod = typeof(TicketsController).GetMethod(nameof(TicketsController.GetComments));

        Assert.Equal("api/tickets", controllerRoute?.Template);
        Assert.NotNull(createMethod);
        Assert.NotNull(createMethod.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(searchMethod);
        Assert.NotNull(searchMethod.GetCustomAttribute<HttpGetAttribute>());
        Assert.NotNull(getByIdMethod);
        Assert.Equal("{id:guid}", getByIdMethod.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.NotNull(assignMethod);
        Assert.Equal("{id:guid}/assignee", assignMethod.GetCustomAttribute<HttpPatchAttribute>()?.Template);
        Assert.NotNull(changeStatusMethod);
        Assert.Equal("{id:guid}/status", changeStatusMethod.GetCustomAttribute<HttpPatchAttribute>()?.Template);
        Assert.NotNull(createCommentMethod);
        Assert.Equal("{id:guid}/comments", createCommentMethod.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.NotNull(getCommentsMethod);
        Assert.Equal("{id:guid}/comments", getCommentsMethod.GetCustomAttribute<HttpGetAttribute>()?.Template);
    }

    [Fact]
    public async Task Create_ValidRequest_Returns201WithTicketAndLocation()
    {
        var ticket = CreateResponse();
        var controller = new TicketsController(new StubTicketCreationService(
            _ => Task.FromResult(new CreateTicketResult(ticket, EmptyErrors()))),
            new StubTicketRepository(),
            new StubTicketAssignmentService(_ => throw new InvalidOperationException()),
            UnusedStatusService(),
            UnusedCommentService());

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
            new StubTicketRepository(),
            new StubTicketAssignmentService(_ => throw new InvalidOperationException()),
            UnusedStatusService(),
            UnusedCommentService());

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
            new StubTicketRepository(),
            new StubTicketAssignmentService(_ => throw new InvalidOperationException()),
            UnusedStatusService(),
            UnusedCommentService());

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
            })),
            new StubTicketAssignmentService(_ => throw new InvalidOperationException()),
            UnusedStatusService(),
            UnusedCommentService());

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
            new StubTicketRepository(_ => Task.FromResult<Ticket?>(null)),
            new StubTicketAssignmentService(_ => throw new InvalidOperationException()),
            UnusedStatusService(),
            UnusedCommentService());

        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Assign_ValidRequest_Returns200WithUpdatedTicket()
    {
        var response = CreateResponse() with { AssigneeId = 2, Status = Status.InProgress };
        var controller = new TicketsController(
            new StubTicketCreationService(_ => throw new InvalidOperationException()),
            new StubTicketRepository(),
            new StubTicketAssignmentService(_ => Task.FromResult(
                new TicketAssignmentResult(response, TicketAssignmentError.None))),
            UnusedStatusService(),
            UnusedCommentService());

        var result = await controller.Assign(response.Id, new AssignTicketRequest(2), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        Assert.Equal(response, ok.Value);
    }

    [Fact]
    public async Task CreateComment_ValidRequest_Returns201WithCommentAndLocation()
    {
        var comment = new TicketCommentResponse(Guid.NewGuid(), "Коментар", 1, DateTimeOffset.UtcNow);
        var controller = new TicketsController(
            new StubTicketCreationService(_ => throw new InvalidOperationException()),
            new StubTicketRepository(),
            new StubTicketAssignmentService(_ => throw new InvalidOperationException()),
            UnusedStatusService(),
            new StubTicketCommentService((_, _) => Task.FromResult(
                new TicketCommentResult(comment, TicketCommentError.None))));

        var ticketId = Guid.NewGuid();
        var result = await controller.CreateComment(ticketId, new CreateCommentRequest(comment.Text), CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal($"/api/tickets/{ticketId}/comments", created.Location);
        Assert.Equal(comment, created.Value);
    }

    [Theory]
    [InlineData(TicketCommentError.InvalidText, StatusCodes.Status400BadRequest)]
    [InlineData(TicketCommentError.TicketNotFound, StatusCodes.Status404NotFound)]
    [InlineData(TicketCommentError.AuthorNotFound, StatusCodes.Status404NotFound)]
    [InlineData(TicketCommentError.TicketClosed, StatusCodes.Status409Conflict)]
    public async Task CreateComment_FailedOperation_ReturnsMappedStatus(
        TicketCommentError commentError,
        int expectedStatus)
    {
        var controller = new TicketsController(
            new StubTicketCreationService(_ => throw new InvalidOperationException()),
            new StubTicketRepository(),
            new StubTicketAssignmentService(_ => throw new InvalidOperationException()),
            UnusedStatusService(),
            new StubTicketCommentService((_, _) => Task.FromResult(
                new TicketCommentResult(null, commentError))));

        var result = await controller.CreateComment(
            Guid.NewGuid(),
            new CreateCommentRequest("Коментар"),
            CancellationToken.None);

        var status = result.Result switch
        {
            ObjectResult objectResult => objectResult.StatusCode,
            StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
            _ => null
        };
        Assert.Equal(expectedStatus, status);
    }

    [Fact]
    public async Task GetComments_NoComments_Returns200WithEmptyList()
    {
        var controller = new TicketsController(
            new StubTicketCreationService(_ => throw new InvalidOperationException()),
            new StubTicketRepository(),
            new StubTicketAssignmentService(_ => throw new InvalidOperationException()),
            UnusedStatusService(),
            UnusedCommentService());

        var result = await controller.GetComments(Guid.NewGuid(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<TicketCommentResponse>>(ok.Value));
    }

    [Theory]
    [InlineData(TicketAssignmentError.TicketNotFound, StatusCodes.Status404NotFound)]
    [InlineData(TicketAssignmentError.InvalidAssignee, StatusCodes.Status400BadRequest)]
    [InlineData(TicketAssignmentError.TicketClosed, StatusCodes.Status409Conflict)]
    public async Task Assign_FailedOperation_ReturnsMappedStatus(
        TicketAssignmentError assignmentError,
        int expectedStatus)
    {
        var controller = new TicketsController(
            new StubTicketCreationService(_ => throw new InvalidOperationException()),
            new StubTicketRepository(),
            new StubTicketAssignmentService(_ => Task.FromResult(
                new TicketAssignmentResult(null, assignmentError))),
            UnusedStatusService(),
            UnusedCommentService());

        var result = await controller.Assign(Guid.NewGuid(), new AssignTicketRequest(2), CancellationToken.None);

        var status = result.Result switch
        {
            ObjectResult objectResult => objectResult.StatusCode,
            StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
            _ => null
        };
        Assert.Equal(expectedStatus, status);
    }

    [Fact]
    public async Task ChangeStatus_ValidRequest_Returns200WithUpdatedTicket()
    {
        var response = CreateResponse() with { Status = Status.InProgress, AssigneeId = 2 };
        var controller = new TicketsController(
            new StubTicketCreationService(_ => throw new InvalidOperationException()),
            new StubTicketRepository(),
            new StubTicketAssignmentService(_ => throw new InvalidOperationException()),
            new StubTicketStatusService((_, _) => Task.FromResult(
                new TicketStatusResult(response, TicketStatusError.None))),
            UnusedCommentService());

        var result = await controller.ChangeStatus(
            response.Id,
            new ChangeTicketStatusRequest(Status.InProgress),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        Assert.Equal(response, ok.Value);
    }

    [Theory]
    [InlineData(TicketStatusError.InvalidStatus, StatusCodes.Status400BadRequest)]
    [InlineData(TicketStatusError.TicketNotFound, StatusCodes.Status404NotFound)]
    [InlineData(TicketStatusError.InvalidTransition, StatusCodes.Status409Conflict)]
    [InlineData(TicketStatusError.AssigneeRequired, StatusCodes.Status409Conflict)]
    public async Task ChangeStatus_FailedOperation_ReturnsMappedStatus(
        TicketStatusError statusError,
        int expectedStatus)
    {
        var controller = new TicketsController(
            new StubTicketCreationService(_ => throw new InvalidOperationException()),
            new StubTicketRepository(),
            new StubTicketAssignmentService(_ => throw new InvalidOperationException()),
            new StubTicketStatusService((_, _) => Task.FromResult(
                new TicketStatusResult(null, statusError))),
            UnusedCommentService());

        var result = await controller.ChangeStatus(
            Guid.NewGuid(),
            new ChangeTicketStatusRequest(Status.InProgress),
            CancellationToken.None);

        var status = result.Result switch
        {
            ObjectResult objectResult => objectResult.StatusCode,
            StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
            _ => null
        };
        Assert.Equal(expectedStatus, status);
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
        DateTimeOffset.UtcNow,
        null,
        null);

    private static IReadOnlyDictionary<string, string[]> EmptyErrors() =>
        new Dictionary<string, string[]>();

    private static StubTicketStatusService UnusedStatusService() =>
        new((_, _) => throw new InvalidOperationException());

    private static StubTicketCommentService UnusedCommentService() =>
        new((_, _) => throw new InvalidOperationException());

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

        public Task<Ticket?> GetForAssignmentAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Ticket?>(null);

        public Task<IReadOnlyList<CategoryStatisticsResponse>> GetCategoryStatisticsAsync(
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task AddCommentAsync(TicketComment comment, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<TicketComment>> GetCommentsByTicketIdAsync(
            Guid ticketId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TicketComment>>([]);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubTicketAssignmentService(
        Func<Guid, Task<TicketAssignmentResult>> assignAsync) : ITicketAssignmentService
    {
        public Task<TicketAssignmentResult> AssignAsync(
            Guid ticketId,
            AssignTicketRequest request,
            CancellationToken cancellationToken = default) => assignAsync(ticketId);
    }

    private sealed class StubTicketStatusService(
        Func<Guid, ChangeTicketStatusRequest, Task<TicketStatusResult>> changeStatusAsync) : ITicketStatusService
    {
        public Task<TicketStatusResult> ChangeStatusAsync(
            Guid ticketId,
            ChangeTicketStatusRequest request,
            CancellationToken cancellationToken = default) => changeStatusAsync(ticketId, request);
    }

    private sealed class StubTicketCommentService(
        Func<Guid, CreateCommentRequest, Task<TicketCommentResult>> createAsync) : ITicketCommentService
    {
        public Task<TicketCommentResult> CreateAsync(
            Guid ticketId,
            CreateCommentRequest request,
            CancellationToken cancellationToken = default) => createAsync(ticketId, request);
    }
}
