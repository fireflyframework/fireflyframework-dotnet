using System.Text.Json.Serialization;

namespace FireflyFramework.Web.Errors.Models;

/// <summary>
/// RFC 7807 Problem Details for HTTP APIs. Mirrors Java <c>ProblemDetail</c>. Use this
/// when you want a strict RFC 7807 response; <see cref="ErrorResponse"/> is the
/// recommended superset.
/// </summary>
public sealed class ProblemDetail
{
    [JsonPropertyName("type")]
    public Uri? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("status")]
    public int? Status { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("instance")]
    public Uri? Instance { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extensions { get; set; }

    public static ProblemDetail FromErrorResponse(ErrorResponse r) => new()
    {
        Type = r.Code is null ? null : new Uri($"https://errors.fireflyframework.org/{r.Code}", UriKind.Absolute),
        Title = r.Error ?? r.Message,
        Status = r.Status,
        Detail = r.Message,
        Instance = r.Path is null ? null : new Uri(r.Path, UriKind.Relative),
        Extensions = new Dictionary<string, object?>
        {
            ["timestamp"] = r.Timestamp,
            ["code"] = r.Code,
            ["traceId"] = r.TraceId,
            ["spanId"] = r.SpanId,
            ["correlationId"] = r.CorrelationId,
            ["category"] = r.Category.ToString(),
            ["severity"] = r.Severity.ToString(),
            ["retryable"] = r.Retryable,
            ["retryAfter"] = r.RetryAfter,
            ["errors"] = r.Errors,
            ["metadata"] = r.Metadata,
        }
    };
}
