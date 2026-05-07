namespace FireflyFramework.Web.Errors.Models;

/// <summary>Error category classification. Mirrors Java <c>ErrorCategory</c>.</summary>
public enum ErrorCategory
{
    Validation,
    Business,
    Technical,
    Security,
    External,
    Resource,
    RateLimit,
    CircuitBreaker,
    Resilience,
    Unknown,
}

/// <summary>Error severity. Mirrors Java <c>ErrorSeverity</c>.</summary>
public enum ErrorSeverity
{
    Low,
    Medium,
    High,
    Critical,
}
