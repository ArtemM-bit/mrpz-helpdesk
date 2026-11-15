using Microsoft.Extensions.DependencyInjection;

namespace Support.Api.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
        services.AddScoped<IStatusService, StatusService>();
        services.AddScoped<CreateTicketRequestValidator>();
        services.AddScoped<ITicketCreationService, TicketCreationService>();

        return services;
    }
}
