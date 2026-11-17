namespace Mortgage.Domain.Assessments;

public sealed record RuleResult(string Rule, bool Passed, string Message);
