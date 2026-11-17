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
    // DisplayRounding is the same rounding the explanation text uses, so the numbers and the text agree.
    public static AssessmentResult From(Assessment a) => new(
        DisplayRounding.Money(a.LoanAmount!.Value),
        DisplayRounding.Percent(a.Ltv!.Value, a.PolicyMaxLtv!.Value),
        DisplayRounding.Money(a.MonthlyRepayment!.Value),
        DisplayRounding.Percent(a.Dti!.Value, a.PolicyMaxDti!.Value),
        a.Decision!.Value.ToString(), DisplayRounding.MoneyLimit(a.MaxBorrowing!.Value), a.Explanation,
        new PolicyUsed(
            PolicyPercent(a.PolicyMaxLtv.Value), PolicyPercent(a.PolicyMaxDti.Value),
            a.PolicyMaxIncomeMultiple!.Value, PolicyPercent(a.PolicyStressRate!.Value)));

    private static decimal PolicyPercent(decimal fraction) => Math.Round(fraction * 100, 1, MidpointRounding.AwayFromZero);
}

public sealed record PolicyUsed(
    decimal MaxLtvPercent, decimal MaxDtiPercent, decimal MaxIncomeMultiple, decimal StressRatePercent);
