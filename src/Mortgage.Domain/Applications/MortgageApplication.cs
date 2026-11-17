using Mortgage.Domain.Assessments;

namespace Mortgage.Domain.Applications;

/// <summary>
/// A mortgage application. It can only be built through <see cref="Create"/>, which
/// rejects invalid input, and it never changes afterwards.
/// </summary>
public sealed class MortgageApplication
{
    private readonly List<Assessment> _assessments = [];

    private MortgageApplication() { } // For EF Core.

    public Guid Id { get; private set; }
    public decimal AnnualIncome { get; private set; }
    public decimal MonthlyExpenses { get; private set; }
    public decimal DepositAmount { get; private set; }
    public decimal PropertyValue { get; private set; }
    public int TermYears { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<Assessment> Assessments => _assessments;

    public decimal LoanAmount => PropertyValue - DepositAmount;

    public static MortgageApplication Create(
        decimal annualIncome, decimal monthlyExpenses, decimal depositAmount,
        decimal propertyValue, int termYears, DateTimeOffset now)
    {
        // The upper bounds keep every later calculation far inside decimal's range.
        if (annualIncome <= 0 || annualIncome > 10_000_000m)
            throw new DomainException("annualIncome", "Annual income must be greater than 0 and at most 10,000,000.");
        if (monthlyExpenses < 0 || monthlyExpenses > 1_000_000m)
            throw new DomainException("monthlyExpenses", "Monthly expenses must be between 0 and 1,000,000.");
        if (depositAmount < 0 || depositAmount > 100_000_000m)
            throw new DomainException("depositAmount", "Deposit must be between 0 and 100,000,000.");
        if (propertyValue <= 0 || propertyValue > 100_000_000m)
            throw new DomainException("propertyValue", "Property value must be greater than 0 and at most 100,000,000.");
        if (termYears is < 5 or > 40)
            throw new DomainException("termYears", "Term must be between 5 and 40 years.");
        if (depositAmount >= propertyValue)
            throw new DomainException("depositAmount", "Deposit must be less than the property value.");

        return new MortgageApplication
        {
            Id = Guid.CreateVersion7(),
            AnnualIncome = annualIncome,
            MonthlyExpenses = monthlyExpenses,
            DepositAmount = depositAmount,
            PropertyValue = propertyValue,
            TermYears = termYears,
            CreatedAt = now,
        };
    }
}
