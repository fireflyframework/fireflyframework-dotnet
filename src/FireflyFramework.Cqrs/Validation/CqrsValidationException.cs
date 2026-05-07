using FireflyFramework.Kernel.Exceptions;

namespace FireflyFramework.Cqrs.Validation;

/// <summary>Thrown by buses when a command fails validation. Translated to HTTP 400 by the web layer.</summary>
public sealed class CqrsValidationException : FireflyException
{
    public IReadOnlyList<ValidationFailure> Failures { get; }

    public CqrsValidationException(string message, IEnumerable<ValidationFailure> failures)
        : base(message, "CQRS_VALIDATION_FAILED")
    {
        Failures = failures.ToList();
    }
}
