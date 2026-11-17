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
