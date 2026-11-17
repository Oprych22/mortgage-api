using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mortgage.Domain.Assessments;

namespace Mortgage.Infrastructure.Configurations;

public class AssessmentConfiguration : IEntityTypeConfiguration<Assessment>
{
    public void Configure(EntityTypeBuilder<Assessment> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        // Enums are stored as their names, so the database is readable and survives reordering.
        builder.Property(a => a.Status).HasConversion<string>();
        builder.Property(a => a.Decision).HasConversion<string>();

        // The list of strings is a primitive collection: EF stores it as one JSON column.
        builder.PrimitiveCollection(a => a.Explanation);
    }
}
