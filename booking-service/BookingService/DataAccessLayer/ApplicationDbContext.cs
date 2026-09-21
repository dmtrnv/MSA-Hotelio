using Microsoft.EntityFrameworkCore;

namespace BookingService.DataAccessLayer;

public sealed class ApplicationDbContext : DbContext
{
    public DbSet<Entities.Booking> Bookings { get; set; }
    public DbSet<Entities.BookingOutbox> BookingOutbox { get; set; }

    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Entities.Booking>()
            .Property(x => x.CreatedAt)
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<Entities.BookingOutbox>()
            .HasIndex(x => x.IsSent);
    }
}