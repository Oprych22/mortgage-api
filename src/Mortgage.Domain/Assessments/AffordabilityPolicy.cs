namespace Mortgage.Domain.Assessments;

/// <summary>The thresholds an assessment is judged against. Ratios are fractions (0.9 = 90%).</summary>
public sealed record AffordabilityPolicy(decimal MaxLtv, decimal MaxDti, decimal MaxIncomeMultiple, decimal StressRate);
