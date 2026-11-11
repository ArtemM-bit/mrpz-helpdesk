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
                .IsRequired();

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
                .IsRequired();

            entity.Property(ticket => ticket.Description)
                .HasMaxLength(4000)
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
