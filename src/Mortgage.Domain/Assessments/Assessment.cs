namespace Mortgage.Domain.Assessments;

/// <summary>
/// The record of one decision at one point in time. It stores the policy it was judged
/// against, so the decision stays explainable after the policy changes.
/// </summary>
public sealed class Assessment
{
    private Assessment() { } // For EF Core.

    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public AssessmentStatus Status { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public decimal? LoanAmount { get; private set; }
    public decimal? Ltv { get; private set; }
    public decimal? MonthlyRepayment { get; private set; }
    public decimal? Dti { get; private set; }
    public decimal? MaxBorrowing { get; private set; }
    public Decision? Decision { get; private set; }
    public List<string> Explanation { get; private set; } = [];

    public decimal? PolicyMaxLtv { get; private set; }
    public decimal? PolicyMaxDti { get; private set; }
    public decimal? PolicyMaxIncomeMultiple { get; private set; }
    public decimal? PolicyStressRate { get; private set; }

    public string? FailureReason { get; private set; }

    public static Assessment CreatePending(Guid applicationId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        ApplicationId = applicationId,
        Status = AssessmentStatus.Pending,
        RequestedAt = now,
    };

    public void Complete(AffordabilityResult result, AffordabilityPolicy policy, DateTimeOffset now)
    {
        EnsurePending();
        LoanAmount = result.LoanAmount;
        Ltv = result.Ltv;
        MonthlyRepayment = result.MonthlyRepayment;
        Dti = result.Dti;
        MaxBorrowing = result.MaxBorrowing;
        Decision = result.Decision;
        Explanation = result.Rules.Select(r => r.Message).ToList();
        PolicyMaxLtv = policy.MaxLtv;
        PolicyMaxDti = policy.MaxDti;
        PolicyMaxIncomeMultiple = policy.MaxIncomeMultiple;
        PolicyStressRate = policy.StressRate;
        Status = AssessmentStatus.Completed;
        CompletedAt = now;
    }

    public void Fail(string reason, DateTimeOffset now)
    {
        EnsurePending();
        FailureReason = reason;
        Status = AssessmentStatus.Failed;
        CompletedAt = now;
    }

    // A finished assessment is a record of a decision; it is never rewritten.
    // Trade-off: this guards one in-memory instance only. A concurrency token (row version) would make it safe across concurrent workers.
    private void EnsurePending()
    {
        if (Status != AssessmentStatus.Pending)
            throw new InvalidOperationException($"Assessment is already {Status}.");
    }
}
