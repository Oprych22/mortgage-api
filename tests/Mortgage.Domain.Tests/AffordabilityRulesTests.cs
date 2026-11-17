using Mortgage.Domain.Assessments;

namespace Mortgage.Domain.Tests;

public class AffordabilityRulesTests
{
    private static readonly AffordabilityPolicy Policy = new(0.90m, 0.50m, 4.5m, 0.07m);

    [Fact] public void Dti_at_the_limit_passes() => Assert.True(AffordabilityRules.Dti(0.50m, Policy).Passed);
    [Fact] public void Dti_above_the_limit_fails() => Assert.False(AffordabilityRules.Dti(0.5001m, Policy).Passed);
    [Fact] public void Ltv_at_the_limit_passes() => Assert.True(AffordabilityRules.Ltv(0.90m, Policy).Passed);
    [Fact] public void Ltv_above_the_limit_fails() => Assert.False(AffordabilityRules.Ltv(0.9001m, Policy).Passed);
    [Fact] public void Income_multiple_at_the_limit_passes() => Assert.True(AffordabilityRules.IncomeMultiple(270_000m, 60_000m, Policy).Passed);
    [Fact] public void Income_multiple_above_the_limit_fails() => Assert.False(AffordabilityRules.IncomeMultiple(270_000.01m, 60_000m, Policy).Passed);

    [Fact]
    public void Ltv_just_over_the_limit_never_prints_the_limit()
    {
        // Deposit 29,999.99 on 300,000: LTV is 90.00003%, which fails but would round to "90.0%".
        var result = AffordabilityRules.Ltv(270_000.01m / 300_000m, Policy);

        Assert.False(result.Passed);
        Assert.Equal("LTV 90.1% exceeds the 90% limit", result.Message);
    }

    [Fact]
    public void Dti_just_over_the_limit_never_prints_the_limit()
    {
        var result = AffordabilityRules.Dti(0.50001m, Policy);

        Assert.False(result.Passed);
        Assert.Equal("Debt-to-income 50.1% exceeds the 50% limit", result.Message);
    }

    [Fact]
    public void Passing_rules_keep_ordinary_rounding()
    {
        Assert.Equal("LTV 83.3% is within the 90% limit", AffordabilityRules.Ltv(0.83333m, Policy).Message);
        Assert.Equal("LTV 90.0% is within the 90% limit", AffordabilityRules.Ltv(0.90m, Policy).Message);
    }
}
