using Microsoft.EntityFrameworkCore;
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
builder.Services.AddOpenApi();
builder.Services.AddDbContext<SupportDbContext>(options => options.UseSqlServer(supportDatabaseConnection));
builder.Services.AddApplicationServices();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
