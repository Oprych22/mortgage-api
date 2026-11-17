namespace Mortgage.Domain.Assessments;

public sealed record AffordabilityResult(
    decimal LoanAmount,
    decimal Ltv,
    decimal MonthlyRepayment,
    decimal Dti,
    decimal MaxBorrowing,
    Decision Decision,
    IReadOnlyList<RuleResult> Rules);
