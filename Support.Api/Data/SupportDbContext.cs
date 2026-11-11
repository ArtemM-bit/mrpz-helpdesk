using Microsoft.EntityFrameworkCore;

namespace Support.Api.Data;

public sealed class SupportDbContext(DbContextOptions<SupportDbContext> options) : DbContext(options)
{
}
