using Microsoft.EntityFrameworkCore;
using Mortgage.Domain.Applications;
using Mortgage.Domain.Assessments;

namespace Mortgage.Infrastructure;

public class MortgageDbContext(DbContextOptions<MortgageDbContext> options) : DbContext(options)
{
    public DbSet<MortgageApplication> Applications => Set<MortgageApplication>();
    public DbSet<Assessment> Assessments => Set<Assessment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MortgageDbContext).Assembly);
}
