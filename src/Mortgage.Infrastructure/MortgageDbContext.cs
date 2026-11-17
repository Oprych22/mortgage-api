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
        // Trade-off: SQLite keeps decimals exact as TEXT but cannot order or range-filter them, or DateTimeOffset, in SQL; SQL Server or PostgreSQL can.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MortgageDbContext).Assembly);
    }
}
