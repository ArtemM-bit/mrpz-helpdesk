using Microsoft.EntityFrameworkCore;
using Support.Api.Models;

namespace Support.Api.Data;

public sealed class SupportDbContext(DbContextOptions<SupportDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(category => category.Id);

            entity.Property(category => category.Name)
                .HasMaxLength(100)
                .IsUnicode()
                .IsRequired();

            entity.HasData(
                new Category { Id = 1, Name = "обладнання" },
                new Category { Id = 2, Name = "програмне забезпечення" },
                new Category { Id = 3, Name = "мережа" },
                new Category { Id = 4, Name = "доступ до облікового запису" },
                new Category { Id = 5, Name = "інше" });

            entity.HasMany(category => category.Tickets)
                .WithOne(ticket => ticket.Category)
                .HasForeignKey(ticket => ticket.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(ticket => ticket.Id);

            entity.Property(ticket => ticket.Title)
                .HasMaxLength(200)
                .IsUnicode()
                .IsRequired();

            entity.Property(ticket => ticket.Description)
                .HasMaxLength(4000)
                .IsUnicode()
                .IsRequired();

            entity.Property(ticket => ticket.CategoryId)
                .IsRequired();

            entity.Property(ticket => ticket.Priority)
                .IsRequired();

            entity.Property(ticket => ticket.Status)
                .IsRequired();

            entity.Property(ticket => ticket.CreatedAt)
                .IsRequired();
        });
    }
}
