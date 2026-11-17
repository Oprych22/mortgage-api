using Mortgage.Domain.Applications;

namespace Mortgage.Api.Applications;

public sealed record ApplicationResponse(
    Guid Id, decimal AnnualIncome, decimal MonthlyExpenses, decimal DepositAmount,
    decimal PropertyValue, int TermYears, decimal LoanAmount, DateTimeOffset CreatedAt)
{
    public static ApplicationResponse From(MortgageApplication a) => new(
        a.Id, a.AnnualIncome, a.MonthlyExpenses, a.DepositAmount,
        a.PropertyValue, a.TermYears, a.LoanAmount, a.CreatedAt);
}
