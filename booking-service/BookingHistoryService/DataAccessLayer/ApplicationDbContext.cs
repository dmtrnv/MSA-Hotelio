using Microsoft.EntityFrameworkCore;

namespace BookingHistoryService.DataAccessLayer;

public sealed class ApplicationDbContext : DbContext
{
    public DbSet<Entities.BookingHistory> BookingHistory { get; set; }

    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }
}