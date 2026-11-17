using Mortgage.Domain.Applications;

namespace Mortgage.Domain.Assessments;

/// <summary>Judges an application against a policy. Pure: same inputs, same answer, nothing stored.</summary>
public static class AffordabilityCalculator
{
    public static AffordabilityResult Assess(MortgageApplication application, AffordabilityPolicy policy)
    {
        var loan = application.LoanAmount;
        var monthlyIncome = application.AnnualIncome / 12;

        var ltv = loan / application.PropertyValue;
        // Repayment is stress-tested at the policy's StressRate, not the rate the customer will actually pay.
        var repayment = Repayment.Monthly(loan, policy.StressRate, application.TermYears);
        var dti = (application.MonthlyExpenses + repayment) / monthlyIncome;

        var rules = new[]
        {
            AffordabilityRules.Ltv(ltv, policy),
            AffordabilityRules.Dti(dti, policy),
            AffordabilityRules.IncomeMultiple(loan, application.AnnualIncome, policy),
        };

        return new AffordabilityResult(
            loan, ltv, repayment, dti,
            MaxBorrowing(application, policy, monthlyIncome),
            rules.All(r => r.Passed) ? Decision.Approved : Decision.Declined,
            rules);
    }

    // The smaller of what the income multiple allows and what the DTI limit's payment can repay.
    private static decimal MaxBorrowing(MortgageApplication application, AffordabilityPolicy policy, decimal monthlyIncome)
    {
        var byIncomeMultiple = policy.MaxIncomeMultiple * application.AnnualIncome;
        var paymentHeadroom = policy.MaxDti * monthlyIncome - application.MonthlyExpenses;
        if (paymentHeadroom <= 0) return 0m;

        var byDti = Repayment.MaxPrincipal(paymentHeadroom, policy.StressRate, application.TermYears);
        return Math.Min(byIncomeMultiple, byDti);
    }
}
