using Mortgage.Domain;
using Mortgage.Domain.Applications;

namespace Mortgage.Domain.Tests;

public class MortgageApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    private static MortgageApplication Create(
        decimal income = 60000m, decimal expenses = 500m, decimal deposit = 50000m,
        decimal property = 300000m, int term = 25) =>
        MortgageApplication.Create(income, expenses, deposit, property, term, Now);

    [Fact]
    public void Create_with_valid_inputs_sets_fields_and_loan()
    {
        var application = Create();

        Assert.NotEqual(Guid.Empty, application.Id);
        Assert.Equal(60000m, application.AnnualIncome);
        Assert.Equal(500m, application.MonthlyExpenses);
        Assert.Equal(50000m, application.DepositAmount);
        Assert.Equal(300000m, application.PropertyValue);
        Assert.Equal(25, application.TermYears);
        Assert.Equal(Now, application.CreatedAt);
        Assert.Equal(250000m, application.LoanAmount);
        Assert.Empty(application.Assessments);
    }

    [Fact]
    public void Create_rejects_deposit_equal_to_property_value()
    {
        var ex = Assert.Throws<DomainException>(() => Create(deposit: 300000m, property: 300000m));
        Assert.Equal("depositAmount", ex.Field);
        Assert.Equal("Deposit must be less than the property value.", ex.Message);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(41)]
    public void Create_rejects_term_out_of_range(int term)
    {
        var ex = Assert.Throws<DomainException>(() => Create(term: term));
        Assert.Equal("termYears", ex.Field);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(40)]
    public void Create_accepts_term_bounds(int term) =>
        Assert.Equal(term, Create(term: term).TermYears);

    [Fact]
    public void Create_rejects_zero_income()
    {
        var ex = Assert.Throws<DomainException>(() => Create(income: 0m));
        Assert.Equal("annualIncome", ex.Field);
    }

    [Theory]
    [InlineData(10_000_001, 500, 50000, 300000, "annualIncome")]
    [InlineData(60000, -1, 50000, 300000, "monthlyExpenses")]
    [InlineData(60000, 1_000_001, 50000, 300000, "monthlyExpenses")]
    [InlineData(60000, 500, -1, 300000, "depositAmount")]
    [InlineData(60000, 500, 100_000_001, 100_000_000, "depositAmount")]
    [InlineData(60000, 500, 50000, 0, "propertyValue")]
    [InlineData(60000, 500, 50000, 100_000_001, "propertyValue")]
    public void Create_rejects_values_outside_bounds(
        decimal income, decimal expenses, decimal deposit, decimal property, string field)
    {
        var ex = Assert.Throws<DomainException>(() => Create(income, expenses, deposit, property));
        Assert.Equal(field, ex.Field);
    }

    [Theory]
    [InlineData(60000.001, 500, 50000, 300000, "annualIncome")]
    [InlineData(60000, 500.005, 50000, 300000, "monthlyExpenses")]
    [InlineData(60000, 500, 50000.001, 300000, "depositAmount")]
    [InlineData(60000, 500, 50000, 300000.001, "propertyValue")]
    public void Create_rejects_more_than_two_decimal_places(
        decimal income, decimal expenses, decimal deposit, decimal property, string field)
    {
        var ex = Assert.Throws<DomainException>(() => Create(income, expenses, deposit, property));
        Assert.Equal(field, ex.Field);
        Assert.EndsWith("must have at most 2 decimal places.", ex.Message);
    }

    [Fact]
    public void Create_accepts_pounds_and_pence()
    {
        var application = Create(60000.50m, 500.25m, 50000.10m, 300000.99m);
        Assert.Equal(300000.99m - 50000.10m, application.LoanAmount);
    }

    [Fact]
    public void Create_accepts_upper_bounds_with_zero_deposit()
    {
        var application = Create(10_000_000m, 1_000_000m, 0m, 100_000_000m);
        Assert.Equal(100_000_000m, application.LoanAmount);
        Assert.Equal(1_000_000m, application.MonthlyExpenses);
    }
}
