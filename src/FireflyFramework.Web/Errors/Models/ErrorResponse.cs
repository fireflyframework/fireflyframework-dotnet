using System.Text.Json.Serialization;

namespace FireflyFramework.Web.Errors.Models;

/// <summary>
/// Enterprise error response: RFC 7807 superset with distributed tracing, classification,
/// resilience metadata and actionable guidance. Mirrors Java <c>ErrorResponse</c>.
/// Serialized as the body of every <c>application/problem+json</c> response produced by
/// <see cref="Middleware.GlobalExceptionHandlerMiddleware"/>.
/// </summary>
public sealed class ErrorResponse
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("status")]
    public int? Status { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; set; }

    [JsonPropertyName("spanId")]
    public string? SpanId { get; set; }

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }

    [JsonPropertyName("requestId")]
    public string? RequestId { get; set; }

    [JsonPropertyName("details")]
    public string? Details { get; set; }

    [JsonPropertyName("suggestion")]
    public string? Suggestion { get; set; }

    [JsonPropertyName("documentation")]
    public string? Documentation { get; set; }

    [JsonPropertyName("helpUrl")]
    public string? HelpUrl { get; set; }

    [JsonPropertyName("instance")]
    public string? Instance { get; set; }

    [JsonPropertyName("category")]
    public ErrorCategory Category { get; set; } = ErrorCategory.Unknown;

    [JsonPropertyName("severity")]
    public ErrorSeverity Severity { get; set; } = ErrorSeverity.Medium;

    [JsonPropertyName("retryable")]
    public bool? Retryable { get; set; }

    [JsonPropertyName("retryAfter")]
    public int? RetryAfter { get; set; }

    [JsonPropertyName("rateLimitInfo")]
    public RateLimitInfo? RateLimitInfo { get; set; }

    [JsonPropertyName("circuitBreakerInfo")]
    public CircuitBreakerInfo? CircuitBreakerInfo { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; set; }

    [JsonPropertyName("errors")]
    public List<ValidationError>? Errors { get; set; }

    [JsonPropertyName("stackTrace")]
    public string? StackTrace { get; set; }

    [JsonPropertyName("debugInfo")]
    public Dictionary<string, object?>? DebugInfo { get; set; }
}
