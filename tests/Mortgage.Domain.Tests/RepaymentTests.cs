using Mortgage.Domain.Assessments;

namespace Mortgage.Domain.Tests;

public class RepaymentTests
{
    [Fact]
    public void Monthly_repayment_for_250k_at_7_percent_over_25_years()
    {
        var monthly = Repayment.Monthly(250_000m, 0.07m, 25);
        Assert.Equal(1766.95m, Math.Round(monthly, 2, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void MaxPrincipal_for_2000_a_month_at_7_percent_over_25_years()
    {
        var principal = Repayment.MaxPrincipal(2000m, 0.07m, 25);
        Assert.Equal(282973.81m, Math.Round(principal, 2, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void MaxPrincipal_undoes_Monthly()
    {
        var monthly = Repayment.Monthly(100_000m, 0.07m, 30);
        var principal = Repayment.MaxPrincipal(monthly, 0.07m, 30);
        Assert.InRange(principal, 99_999.99m, 100_000.01m);
    }
}
