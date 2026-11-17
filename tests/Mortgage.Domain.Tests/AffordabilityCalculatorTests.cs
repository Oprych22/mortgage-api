using Mortgage.Domain.Applications;
using Mortgage.Domain.Assessments;

namespace Mortgage.Domain.Tests;

public class AffordabilityCalculatorTests
{
    private static readonly AffordabilityPolicy Policy = new(0.90m, 0.50m, 4.5m, 0.07m);

    private static AffordabilityResult Assess(decimal income, decimal expenses, decimal deposit, decimal property) =>
        AffordabilityCalculator.Assess(
            MortgageApplication.Create(income, expenses, deposit, property, 25, DateTimeOffset.UnixEpoch), Policy);

    private static decimal Money(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);
    private static decimal Percent(decimal x) => Math.Round(x * 100, 1, MidpointRounding.AwayFromZero);

    private static void Check(AffordabilityResult r, decimal loan, decimal ltv, decimal repay, decimal dti,
        decimal maxBorrow, Decision decision, params string[] failed)
    {
        Assert.Equal(loan, r.LoanAmount);
        Assert.Equal(ltv, Percent(r.Ltv));
        Assert.Equal(repay, Money(r.MonthlyRepayment));
        Assert.Equal(dti, Percent(r.Dti));
        Assert.Equal(maxBorrow, Money(r.MaxBorrowing));
        Assert.Equal(decision, r.Decision);
        Assert.Equal(["LTV", "DTI", "IncomeMultiple"], r.Rules.Select(x => x.Rule));
        Assert.Equal(failed, r.Rules.Where(x => !x.Passed).Select(x => x.Rule));
    }

    [Fact]
    public void Approve_example()
    {
        var r = Assess(60_000m, 500m, 50_000m, 300_000m);
        Check(r, 250_000m, 83.3m, 1766.95m, 45.3m, 270_000.00m, Decision.Approved);
        Assert.Equal(
        [
            "LTV 83.3% is within the 90% limit",
            "Debt-to-income 45.3% is within the 50% limit",
            "Loan of £250,000.00 is within 4.5x income (£270,000.00)",
        ], r.Rules.Select(x => x.Message));
    }

    [Fact]
    public void Decline_ltv_and_income_multiple()
    {
        var r = Assess(60_000m, 500m, 20_000m, 300_000m);
        Check(r, 280_000m, 93.3m, 1978.98m, 49.6m, 270_000.00m, Decision.Declined, "LTV", "IncomeMultiple");
        Assert.Equal(
        [
            "LTV 93.3% exceeds the 90% limit",
            "Debt-to-income 49.6% is within the 50% limit",
            "Loan of £280,000.00 exceeds 4.5x income (£270,000.00)",
        ], r.Rules.Select(x => x.Message));
    }

    [Fact]
    public void Decline_dti_only()
    {
        var r = Assess(60_000m, 1_200m, 100_000m, 300_000m);
        Check(r, 200_000m, 66.7m, 1413.56m, 52.3m, 183_932.97m, Decision.Declined, "DTI");
    }

    [Fact]
    public void Max_borrowing_floored_at_zero()
    {
        var r = Assess(30_000m, 2_000m, 50_000m, 200_000m);
        Check(r, 150_000m, 75.0m, 1060.17m, 122.4m, 0.00m, Decision.Declined, "DTI", "IncomeMultiple");
    }

    [Fact]
    public void Exactly_90_ltv_and_4_5x_is_approved()
    {
        var r = Assess(60_000m, 500m, 30_000m, 300_000m);
        Check(r, 270_000m, 90.0m, 1908.30m, 48.2m, 270_000.00m, Decision.Approved);
    }
}
