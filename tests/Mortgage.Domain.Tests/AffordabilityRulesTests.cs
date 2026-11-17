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
}
