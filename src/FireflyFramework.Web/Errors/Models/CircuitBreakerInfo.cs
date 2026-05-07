using System.Text.Json.Serialization;

namespace FireflyFramework.Web.Errors.Models;

/// <summary>Circuit-breaker context for 503 responses. Mirrors Java <c>CircuitBreakerInfo</c>.</summary>
public sealed class CircuitBreakerInfo
{
    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("failureRate")]
    public double FailureRate { get; set; }

    [JsonPropertyName("failureRateThreshold")]
    public double FailureRateThreshold { get; set; }

    [JsonPropertyName("failureCount")]
    public long FailureCount { get; set; }

    [JsonPropertyName("nextAttemptTime")]
    public DateTimeOffset? NextAttemptTime { get; set; }

    [JsonPropertyName("fallbackSuggestion")]
    public string? FallbackSuggestion { get; set; }
}
