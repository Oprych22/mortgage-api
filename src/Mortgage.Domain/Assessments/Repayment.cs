namespace Mortgage.Domain.Assessments;

/// <summary>Standard repayment-mortgage maths. Results are unrounded; callers round at the edge.</summary>
public static class Repayment
{
    /// <summary>The fixed monthly payment that clears <paramref name="principal"/> over the term.</summary>
    public static decimal Monthly(decimal principal, decimal annualRate, int termYears)
    {
        var r = annualRate / 12;
        var f = GrowthFactor(r, termYears * 12);
        return principal * r * f / (f - 1);
    }

    /// <summary>The largest loan a given monthly payment can clear: <see cref="Monthly"/> run backwards.</summary>
    public static decimal MaxPrincipal(decimal monthlyPayment, decimal annualRate, int termYears)
    {
        var r = annualRate / 12;
        var f = GrowthFactor(r, termYears * 12);
        return monthlyPayment * (f - 1) / (r * f);
    }

    // (1 + r) ^ n by repeated multiplication. Math.Pow works in double, which would turn
    // every money figure into a binary approximation; this stays in decimal, and n is at
    // most 480 (40 years), so the loop is trivial.
    private static decimal GrowthFactor(decimal monthlyRate, int months)
    {
        var result = 1m;
        for (var i = 0; i < months; i++)
            result *= 1 + monthlyRate;
        return result;
    }
}
