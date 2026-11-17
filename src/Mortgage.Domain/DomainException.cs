namespace Mortgage.Domain;

/// <summary>A business rule was broken. Field is the camelCase JSON name so the API can report it as a validation error.</summary>
public sealed class DomainException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
