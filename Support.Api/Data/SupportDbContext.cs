using Microsoft.EntityFrameworkCore;
using Support.Api.Models;

namespace Support.Api.Data;

public sealed class SupportDbContext(DbContextOptions<SupportDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<User> Users => Set<User>();

    public DbSet<TicketComment> TicketComments => Set<TicketComment>();

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

            entity.Property(ticket => ticket.DueAt);

            entity.HasOne(ticket => ticket.Assignee)
                .WithMany(user => user.AssignedTickets)
                .HasForeignKey(ticket => ticket.AssigneeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(ticket => ticket.Comments)
                .WithOne(comment => comment.Ticket)
                .HasForeignKey(comment => comment.TicketId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);

            entity.Property(user => user.FullName)
                .HasMaxLength(200)
                .IsUnicode()
                .IsRequired();

            entity.Property(user => user.IsSupportEmployee)
                .IsRequired();

            entity.HasData(
                new User { Id = 1, FullName = "Олена Коваленко", IsSupportEmployee = false },
                new User { Id = 2, FullName = "Іван Петренко", IsSupportEmployee = true });
        });

        modelBuilder.Entity<TicketComment>(entity =>
        {
            entity.HasKey(comment => comment.Id);

            entity.Property(comment => comment.Text)
                .HasMaxLength(2000)
                .IsUnicode()
                .IsRequired();

            entity.Property(comment => comment.CreatedAt)
                .IsRequired();

            entity.HasOne(comment => comment.Author)
                .WithMany(user => user.Comments)
                .HasForeignKey(comment => comment.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
