using System.ComponentModel.DataAnnotations;

namespace Mortgage.Api.Applications;

// Nullable plus [Required]: a missing number must be a 400. A plain decimal would
// quietly become 0 when absent from the JSON, and 0 is a valid expenses figure.
public sealed class CreateApplicationRequest
{
    [Required, Range(typeof(decimal), "0.01", "10000000")]
    public decimal? AnnualIncome { get; init; }

    [Required, Range(typeof(decimal), "0", "1000000")]
    public decimal? MonthlyExpenses { get; init; }

    [Required, Range(typeof(decimal), "0", "100000000")]
    public decimal? DepositAmount { get; init; }

    [Required, Range(typeof(decimal), "0.01", "100000000")]
    public decimal? PropertyValue { get; init; }

    [Required, Range(5, 40)]
    public int? TermYears { get; init; }
}
