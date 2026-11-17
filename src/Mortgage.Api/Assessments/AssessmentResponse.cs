using Mortgage.Domain.Assessments;

namespace Mortgage.Api.Assessments;

public sealed record AssessmentResponse(
    Guid Id, Guid ApplicationId, string Status, DateTimeOffset RequestedAt, DateTimeOffset? CompletedAt,
    AssessmentResult? Result, string? FailureReason)
{
    public static AssessmentResponse From(Assessment a) => new(
        a.Id, a.ApplicationId, a.Status.ToString(), a.RequestedAt, a.CompletedAt,
        a.Status == AssessmentStatus.Completed ? AssessmentResult.From(a) : null,
        a.FailureReason);
}

public sealed record AssessmentResult(
    decimal LoanAmount, decimal LtvPercent, decimal MonthlyRepayment, decimal DtiPercent, string Decision,
    decimal MaxBorrowing, IReadOnlyList<string> Explanation, PolicyUsed Policy)
{
    // Rounding happens here, at the edge: the domain and the database keep full precision.
    public static AssessmentResult From(Assessment a) => new(
        Money(a.LoanAmount!.Value), Percent(a.Ltv!.Value), Money(a.MonthlyRepayment!.Value),
        Percent(a.Dti!.Value), a.Decision!.Value.ToString(), Money(a.MaxBorrowing!.Value), a.Explanation,
        new PolicyUsed(
            Percent(a.PolicyMaxLtv!.Value), Percent(a.PolicyMaxDti!.Value),
            a.PolicyMaxIncomeMultiple!.Value, Percent(a.PolicyStressRate!.Value)));

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal Percent(decimal fraction) => Math.Round(fraction * 100, 1, MidpointRounding.AwayFromZero);
}

public sealed record PolicyUsed(
    decimal MaxLtvPercent, decimal MaxDtiPercent, decimal MaxIncomeMultiple, decimal StressRatePercent);
