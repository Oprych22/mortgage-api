using System.Globalization;

namespace Mortgage.Domain.Assessments;

/// <summary>The three affordability rules. Each passes when its value is at or under the limit, and explains itself in one line.</summary>
public static class AffordabilityRules
{
    public static RuleResult Ltv(decimal ltv, AffordabilityPolicy policy)
    {
        var passed = ltv <= policy.MaxLtv;
        return new RuleResult("LTV", passed,
            $"LTV {Percent(ltv, policy.MaxLtv)}% {Verdict(passed)} the {Limit(policy.MaxLtv * 100)}% limit");
    }

    public static RuleResult Dti(decimal dti, AffordabilityPolicy policy)
    {
        var passed = dti <= policy.MaxDti;
        return new RuleResult("DTI", passed,
            $"Debt-to-income {Percent(dti, policy.MaxDti)}% {Verdict(passed)} the {Limit(policy.MaxDti * 100)}% limit");
    }

    public static RuleResult IncomeMultiple(decimal loan, decimal annualIncome, AffordabilityPolicy policy)
    {
        var maxLoan = policy.MaxIncomeMultiple * annualIncome;
        var passed = loan <= maxLoan;
        return new RuleResult("IncomeMultiple", passed,
            $"Loan of {Money(loan)} {Verdict(passed)} {Limit(policy.MaxIncomeMultiple)}x income ({Money(maxLoan)})");
    }

    private static string Verdict(bool passed) => passed ? "is within" : "exceeds";

    // Rounded only here, in the text; the pass/fail test above uses the exact value.
    // A failing value that would round down to the limit ("90.0% exceeds the 90% limit") is rounded up instead, so a fail never reads as equal to its limit.
    private static string Percent(decimal ratio, decimal maxRatio)
    {
        var percent = Math.Round(ratio * 100, 1, MidpointRounding.AwayFromZero);
        if (ratio > maxRatio && percent <= maxRatio * 100)
            percent = Math.Ceiling(ratio * 1000) / 10;
        return percent.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static string Limit(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Money(decimal amount) =>
        "£" + Math.Round(amount, 2, MidpointRounding.AwayFromZero).ToString("N2", CultureInfo.InvariantCulture);
}
