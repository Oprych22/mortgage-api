using Microsoft.EntityFrameworkCore;
using Mortgage.Domain.Applications;
using Mortgage.Domain.Assessments;

namespace Mortgage.Infrastructure;

public class MortgageDbContext(DbContextOptions<MortgageDbContext> options) : DbContext(options)
{
    public DbSet<MortgageApplication> Applications => Set<MortgageApplication>();
    public DbSet<Assessment> Assessments => Set<Assessment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Trade-off: SQLite stores every decimal property on both entities as TEXT. That keeps them
        // exact, but SQL cannot use an index to order or range-filter them, and DateTimeOffset
        // cannot be ordered or compared in SQL at all. Nothing here needs either; SQL Server or
        // PostgreSQL would.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MortgageDbContext).Assembly);
    }
}
