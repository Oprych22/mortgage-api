using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mortgage.Domain.Applications;

namespace Mortgage.Infrastructure.Configurations;

public class MortgageApplicationConfiguration : IEntityTypeConfiguration<MortgageApplication>
{
    public void Configure(EntityTypeBuilder<MortgageApplication> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever(); // The domain assigns the id.

        // Trade-off: SQLite stores decimals as TEXT. That keeps them exact, but SQL cannot
        // order or range-filter them. Nothing here needs that; SQL Server's decimal type would.
        builder.Property(a => a.AnnualIncome);
        builder.Property(a => a.MonthlyExpenses);
        builder.Property(a => a.DepositAmount);
        builder.Property(a => a.PropertyValue);

        // LoanAmount is calculated from the two stored values, so it is not a column.
        builder.Ignore(a => a.LoanAmount);

        // Assessments is a read-only view over a private list, so EF fills the field directly.
        builder.HasMany(a => a.Assessments)
            .WithOne()
            .HasForeignKey(a => a.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Assessments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
