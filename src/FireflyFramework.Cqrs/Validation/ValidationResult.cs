namespace FireflyFramework.Cqrs.Validation;

public sealed class ValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<ValidationFailure> Failures { get; }

    private ValidationResult(bool isValid, IReadOnlyList<ValidationFailure> failures)
    {
        IsValid = isValid;
        Failures = failures;
    }

    public static ValidationResult Successful() => new(true, Array.Empty<ValidationFailure>());

    public static ValidationResult Failed(IEnumerable<ValidationFailure> failures) => new(false, failures.ToList());

    public static ValidationResult Failed(string field, string message) =>
        new(false, new[] { new ValidationFailure(field, message) });
}

public sealed record ValidationFailure(string Field, string Message, string? Code = null);
