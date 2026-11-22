using Microsoft.EntityFrameworkCore;
using Support.Api;
using Support.Api.Data;
using Support.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var supportDatabaseConnection = builder.Configuration.GetConnectionString("SupportDatabase");
if (string.IsNullOrWhiteSpace(supportDatabaseConnection))
{
    throw new InvalidOperationException(
        "ConnectionStrings:SupportDatabase не задана. Укажите строку подключения в конфигурации " +
        "или через переменную окружения ConnectionStrings__SupportDatabase.");
}

builder.Services.AddControllers();
builder.Services.AddOpenApi(options => options.AddOperationTransformer(TicketOpenApiTransformer.TransformAsync));
builder.Services.AddDbContext<SupportDbContext>(options => options.UseSqlServer(supportDatabaseConnection));
builder.Services.AddApplicationServices();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("../openapi/v1.json", "Support Helpdesk API v1");
        options.DocumentTitle = "Support Helpdesk API — Swagger UI";
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
