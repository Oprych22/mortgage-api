using Mortgage.Domain.Assessments;

namespace Mortgage.Domain.Tests;

public class AssessmentTests
{
    private static readonly DateTimeOffset Requested = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Finished = Requested.AddSeconds(2);
    private static readonly AffordabilityPolicy Policy = new(0.9m, 0.5m, 4.5m, 0.07m);

    private static AffordabilityResult Result() => new(
        LoanAmount: 250000m, Ltv: 0.8333m, MonthlyRepayment: 1500m, Dti: 0.3m, MaxBorrowing: 270000m,
        Decision: Decision.Approved,
        Rules:
        [
            new RuleResult("LTV", true, "LTV 83.3% is within the 90% limit"),
            new RuleResult("DTI", true, "Debt-to-income 30.0% is within the 50% limit"),
            new RuleResult("IncomeMultiple", true, "Loan of £250,000.00 is within 4.5x income (£270,000.00)"),
        ]);

    [Fact]
    public void CreatePending_is_pending_with_no_result()
    {
        var applicationId = Guid.NewGuid();
        var a = Assessment.CreatePending(applicationId, Requested);

        Assert.NotEqual(Guid.Empty, a.Id);
        Assert.Equal(applicationId, a.ApplicationId);
        Assert.Equal(AssessmentStatus.Pending, a.Status);
        Assert.Equal(Requested, a.RequestedAt);
        Assert.Null(a.CompletedAt);
        Assert.Null(a.Decision);
        Assert.Null(a.LoanAmount);
        Assert.Null(a.PolicyMaxLtv);
        Assert.Null(a.FailureReason);
        Assert.Empty(a.Explanation);
    }

    [Fact]
    public void Complete_copies_result_and_policy_snapshot()
    {
        var a = Assessment.CreatePending(Guid.NewGuid(), Requested);
        var result = Result();

        a.Complete(result, Policy, Finished);

        Assert.Equal(AssessmentStatus.Completed, a.Status);
        Assert.Equal(Finished, a.CompletedAt);
        Assert.Equal(250000m, a.LoanAmount);
        Assert.Equal(0.8333m, a.Ltv);
        Assert.Equal(1500m, a.MonthlyRepayment);
        Assert.Equal(0.3m, a.Dti);
        Assert.Equal(270000m, a.MaxBorrowing);
        Assert.Equal(Decision.Approved, a.Decision);
        Assert.Equal(0.9m, a.PolicyMaxLtv);
        Assert.Equal(0.5m, a.PolicyMaxDti);
        Assert.Equal(4.5m, a.PolicyMaxIncomeMultiple);
        Assert.Equal(0.07m, a.PolicyStressRate);
        Assert.Equal(result.Rules.Select(r => r.Message), a.Explanation);
    }

    [Fact]
    public void Complete_twice_throws()
    {
        var a = Assessment.CreatePending(Guid.NewGuid(), Requested);
        a.Complete(Result(), Policy, Finished);

        Assert.Throws<InvalidOperationException>(() => a.Complete(Result(), Policy, Finished));
    }

    [Fact]
    public void Fail_sets_reason_and_status()
    {
        var a = Assessment.CreatePending(Guid.NewGuid(), Requested);

        a.Fail("Application not found.", Finished);

        Assert.Equal(AssessmentStatus.Failed, a.Status);
        Assert.Equal("Application not found.", a.FailureReason);
        Assert.Equal(Finished, a.CompletedAt);
    }

    [Fact]
    public void Fail_after_complete_throws()
    {
        var a = Assessment.CreatePending(Guid.NewGuid(), Requested);
        a.Complete(Result(), Policy, Finished);

        Assert.Throws<InvalidOperationException>(() => a.Fail("late", Finished));
    }
}
