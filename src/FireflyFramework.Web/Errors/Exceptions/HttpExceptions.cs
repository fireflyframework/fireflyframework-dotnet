using FireflyFramework.Web.Errors.Models;

namespace FireflyFramework.Web.Errors.Exceptions;

/// <summary>400 Bad Request — malformed request.</summary>
public class InvalidRequestException : ServiceException
{
    public InvalidRequestException(string message, Exception? cause = null)
        : base(message, 400, "INVALID_REQUEST", ErrorCategory.Validation, ErrorSeverity.Low, false, null, cause) { }
}

/// <summary>401 Unauthorized — missing or invalid authentication.</summary>
public class UnauthorizedException : ServiceException
{
    public UnauthorizedException(string message = "Authentication required", Exception? cause = null)
        : base(message, 401, "UNAUTHORIZED", ErrorCategory.Security, ErrorSeverity.Medium, false, null, cause) { }
}

/// <summary>403 Forbidden — insufficient permissions.</summary>
public class ForbiddenException : ServiceException
{
    public ForbiddenException(string message = "Operation forbidden", Exception? cause = null)
        : base(message, 403, "FORBIDDEN", ErrorCategory.Security, ErrorSeverity.Medium, false, null, cause) { }
}

/// <summary>403 Forbidden — explicit authorization decision.</summary>
public class AuthorizationException : ServiceException
{
    public AuthorizationException(string message, Exception? cause = null)
        : base(message, 403, "AUTHORIZATION_DENIED", ErrorCategory.Security, ErrorSeverity.High, false, null, cause) { }
}

/// <summary>404 Not Found.</summary>
public class ResourceNotFoundException : ServiceException
{
    public ResourceNotFoundException(string message, Exception? cause = null)
        : base(message, 404, "RESOURCE_NOT_FOUND", ErrorCategory.Resource, ErrorSeverity.Low, false, null, cause) { }
}

/// <summary>405 Method Not Allowed.</summary>
public class MethodNotAllowedException : ServiceException
{
    public MethodNotAllowedException(string message, Exception? cause = null)
        : base(message, 405, "METHOD_NOT_ALLOWED", ErrorCategory.Validation, ErrorSeverity.Low, false, null, cause) { }
}

/// <summary>409 Conflict — duplicate resource or state collision.</summary>
public class ConflictException : ServiceException
{
    public ConflictException(string message, Exception? cause = null)
        : base(message, 409, "CONFLICT", ErrorCategory.Resource, ErrorSeverity.Medium, false, null, cause) { }
}

/// <summary>409 Conflict — optimistic concurrency failure.</summary>
public class ConcurrencyException : ServiceException
{
    public ConcurrencyException(string message, Exception? cause = null)
        : base(message, 409, "CONCURRENCY_CONFLICT", ErrorCategory.Technical, ErrorSeverity.Medium, true, null, cause) { }
}

/// <summary>409 Conflict — database constraint violation.</summary>
public class DataIntegrityException : ServiceException
{
    public DataIntegrityException(string message, Exception? cause = null)
        : base(message, 409, "DATA_INTEGRITY_VIOLATION", ErrorCategory.Technical, ErrorSeverity.High, false, null, cause) { }
}

/// <summary>410 Gone — resource permanently deleted.</summary>
public class GoneException : ServiceException
{
    public GoneException(string message, Exception? cause = null)
        : base(message, 410, "RESOURCE_GONE", ErrorCategory.Resource, ErrorSeverity.Low, false, null, cause) { }
}

/// <summary>412 Precondition Failed.</summary>
public class PreconditionFailedException : ServiceException
{
    public PreconditionFailedException(string message, Exception? cause = null)
        : base(message, 412, "PRECONDITION_FAILED", ErrorCategory.Validation, ErrorSeverity.Low, false, null, cause) { }
}

/// <summary>413 Payload Too Large.</summary>
public class PayloadTooLargeException : ServiceException
{
    public PayloadTooLargeException(string message, Exception? cause = null)
        : base(message, 413, "PAYLOAD_TOO_LARGE", ErrorCategory.Validation, ErrorSeverity.Low, false, null, cause) { }
}

/// <summary>415 Unsupported Media Type.</summary>
public class UnsupportedMediaTypeException : ServiceException
{
    public UnsupportedMediaTypeException(string message, Exception? cause = null)
        : base(message, 415, "UNSUPPORTED_MEDIA_TYPE", ErrorCategory.Validation, ErrorSeverity.Low, false, null, cause) { }
}

/// <summary>423 Locked.</summary>
public class LockedResourceException : ServiceException
{
    public LockedResourceException(string message, Exception? cause = null)
        : base(message, 423, "RESOURCE_LOCKED", ErrorCategory.Resource, ErrorSeverity.Medium, true, null, cause) { }
}

/// <summary>429 Too Many Requests.</summary>
public class RateLimitException : ServiceException
{
    public RateLimitInfo? Info { get; }

    public RateLimitException(string message, int retryAfter, RateLimitInfo? info = null, Exception? cause = null)
        : base(message, 429, "RATE_LIMIT_EXCEEDED", ErrorCategory.RateLimit, ErrorSeverity.Medium, true, retryAfter, cause)
    {
        Info = info;
    }
}

/// <summary>429 Too Many Requests — usage quota exhausted.</summary>
public class QuotaExceededException : ServiceException
{
    public QuotaExceededException(string message, Exception? cause = null)
        : base(message, 429, "QUOTA_EXCEEDED", ErrorCategory.RateLimit, ErrorSeverity.Medium, false, null, cause) { }
}

/// <summary>500 — retries were attempted and all failed.</summary>
public class RetryExhaustedException : ServiceException
{
    public RetryExhaustedException(string message, Exception? cause = null)
        : base(message, 500, "RETRY_EXHAUSTED", ErrorCategory.Technical, ErrorSeverity.High, false, null, cause) { }
}

/// <summary>501 Not Implemented.</summary>
public class NotImplementedException : ServiceException
{
    public NotImplementedException(string message = "Not implemented", Exception? cause = null)
        : base(message, 501, "NOT_IMPLEMENTED", ErrorCategory.Technical, ErrorSeverity.Low, false, null, cause) { }
}

/// <summary>502 Bad Gateway — upstream returned an invalid response.</summary>
public class BadGatewayException : ServiceException
{
    public BadGatewayException(string message, Exception? cause = null)
        : base(message, 502, "BAD_GATEWAY", ErrorCategory.External, ErrorSeverity.High, true, null, cause) { }
}

/// <summary>502 Bad Gateway — third-party API error.</summary>
public class ThirdPartyServiceException : ServiceException
{
    public ThirdPartyServiceException(string message, Exception? cause = null)
        : base(message, 502, "THIRD_PARTY_ERROR", ErrorCategory.External, ErrorSeverity.High, true, null, cause) { }
}

/// <summary>503 Service Unavailable.</summary>
public class ServiceUnavailableException : ServiceException
{
    public ServiceUnavailableException(string message, int? retryAfter = null, Exception? cause = null)
        : base(message, 503, "SERVICE_UNAVAILABLE", ErrorCategory.External, ErrorSeverity.High, true, retryAfter, cause) { }
}

/// <summary>503 — circuit breaker is open.</summary>
public class CircuitBreakerException : ServiceException
{
    public CircuitBreakerInfo? Info { get; }

    public CircuitBreakerException(string message, CircuitBreakerInfo? info = null, Exception? cause = null)
        : base(message, 503, "CIRCUIT_BREAKER_OPEN", ErrorCategory.CircuitBreaker, ErrorSeverity.High, true, 30, cause)
    {
        Info = info;
    }
}

/// <summary>503 — bulkhead exhausted.</summary>
public class BulkheadException : ServiceException
{
    public BulkheadException(string message, Exception? cause = null)
        : base(message, 503, "BULKHEAD_FULL", ErrorCategory.Resilience, ErrorSeverity.High, true, 5, cause) { }
}

/// <summary>503 — service operating in degraded mode.</summary>
public class DegradedServiceException : ServiceException
{
    public DegradedServiceException(string message, Exception? cause = null)
        : base(message, 503, "SERVICE_DEGRADED", ErrorCategory.Technical, ErrorSeverity.Medium, true, null, cause) { }
}

/// <summary>504 Gateway Timeout — operation took too long.</summary>
public class OperationTimeoutException : ServiceException
{
    public OperationTimeoutException(string message, Exception? cause = null)
        : base(message, 504, "OPERATION_TIMEOUT", ErrorCategory.Technical, ErrorSeverity.High, true, null, cause) { }
}

/// <summary>504 Gateway Timeout.</summary>
public class GatewayTimeoutException : ServiceException
{
    public GatewayTimeoutException(string message, Exception? cause = null)
        : base(message, 504, "GATEWAY_TIMEOUT", ErrorCategory.External, ErrorSeverity.High, true, null, cause) { }
}
