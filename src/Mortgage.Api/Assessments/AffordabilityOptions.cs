using System.ComponentModel.DataAnnotations;
using Mortgage.Domain.Assessments;

namespace Mortgage.Api.Assessments;

// The lending policy, read from the "Affordability" section of appsettings.json.
// The ranges stop a typo (a stress rate of 7 instead of 0.07, or 0) from starting the app:
// 0 would divide by zero in the repayment formula and 7 would overflow decimal.
public sealed class AffordabilityOptions
{
    public const string SectionName = "Affordability";

    [Range(typeof(decimal), "0.01", "1", ParseLimitsInInvariantCulture = true)]
    public decimal MaxLtv { get; init; }

    [Range(typeof(decimal), "0.01", "1", ParseLimitsInInvariantCulture = true)]
    public decimal MaxDti { get; init; }

    [Range(typeof(decimal), "1", "10", ParseLimitsInInvariantCulture = true)]
    public decimal MaxIncomeMultiple { get; init; }

    [Range(typeof(decimal), "0.001", "0.25", ParseLimitsInInvariantCulture = true)]
    public decimal StressRate { get; init; }

    public AffordabilityPolicy ToPolicy() => new(MaxLtv, MaxDti, MaxIncomeMultiple, StressRate);
}
