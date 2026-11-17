namespace Mortgage.Domain.Assessments;

/// <summary>
/// How figures are rounded for display. Used only at the edge (response numbers and explanation text), by both,
/// so a number in the response and the same number in the explanation always agree.
/// </summary>
public static class DisplayRounding
{
    /// <summary>A ratio as a percentage to 1 dp, half-up. A value over its limit never displays as at or under it: it is rounded up.</summary>
    public static decimal Percent(decimal ratio, decimal maxRatio)
    {
        var percent = Math.Round(ratio * 100, 1, MidpointRounding.AwayFromZero);
        if (ratio > maxRatio && percent <= maxRatio * 100)
            percent = Math.Round(ratio * 100, 1, MidpointRounding.ToPositiveInfinity);
        return percent;
    }

    /// <summary>An amount to the penny, half-up.</summary>
    public static decimal Money(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// <summary>A borrowing limit to the penny, rounded down: a limit is never overstated, so a loan over it never displays as equal to it.</summary>
    public static decimal MoneyLimit(decimal amount) => Math.Round(amount, 2, MidpointRounding.ToZero);
}
