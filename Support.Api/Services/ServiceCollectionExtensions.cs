using Microsoft.Extensions.DependencyInjection;
using Support.Api.Data;

namespace Support.Api.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
        services.AddScoped<IStatusService, StatusService>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<CreateTicketRequestValidator>();
        services.AddScoped<ITicketCreationService, TicketCreationService>();
        services.AddScoped<ITicketAssignmentService, TicketAssignmentService>();
        services.AddScoped<ITicketStatusService, TicketStatusService>();
        services.AddScoped<ITicketCommentService, TicketCommentService>();

        return services;
    }
}
