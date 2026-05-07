using FireflyFramework.Web.Errors.Models;

namespace FireflyFramework.Web.Errors.Exceptions;

/// <summary>400 Bad Request — input validation failure with optional per-field errors.</summary>
public class ValidationException : ServiceException
{
    public IReadOnlyList<ValidationError> Errors { get; }

    public ValidationException(string message, IEnumerable<ValidationError>? errors = null, Exception? cause = null)
        : base(message, 400, "VALIDATION_FAILED", ErrorCategory.Validation, ErrorSeverity.Medium, false, null, cause)
    {
        Errors = (errors ?? Array.Empty<ValidationError>()).ToList();
    }
}
