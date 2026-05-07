using FireflyFramework.Kernel.Exceptions;
using FireflyFramework.Web.Errors.Models;

namespace FireflyFramework.Web.Errors.Exceptions;

/// <summary>
/// Base class for HTTP-aware business exceptions. Carries the HTTP status, error
/// classification, and optional resilience metadata used by
/// <see cref="Middleware.GlobalExceptionHandlerMiddleware"/> when shaping the
/// response. Mirrors Java <c>ServiceException</c>.
/// </summary>
public class ServiceException : FireflyException
{
    public int HttpStatus { get; }

    public ErrorCategory Category { get; }

    public ErrorSeverity Severity { get; }

    public bool Retryable { get; }

    public int? RetryAfter { get; }

    public ServiceException(
        string message,
        int httpStatus = 500,
        string errorCode = "SERVICE_ERROR",
        ErrorCategory category = ErrorCategory.Technical,
        ErrorSeverity severity = ErrorSeverity.High,
        bool retryable = false,
        int? retryAfter = null,
        Exception? cause = null)
        : base(message, errorCode, cause)
    {
        HttpStatus = httpStatus;
        Category = category;
        Severity = severity;
        Retryable = retryable;
        RetryAfter = retryAfter;
    }
}
