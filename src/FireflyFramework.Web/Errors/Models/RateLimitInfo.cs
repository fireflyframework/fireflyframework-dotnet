using System.Text.Json.Serialization;

namespace FireflyFramework.Web.Errors.Models;

/// <summary>Rate-limit context for 429 responses. Mirrors Java <c>RateLimitInfo</c>.</summary>
public sealed class RateLimitInfo
{
    [JsonPropertyName("limit")]
    public long Limit { get; set; }

    [JsonPropertyName("remaining")]
    public long Remaining { get; set; }

    [JsonPropertyName("resetTime")]
    public DateTimeOffset? ResetTime { get; set; }

    [JsonPropertyName("windowSeconds")]
    public int WindowSeconds { get; set; }

    [JsonPropertyName("limitType")]
    public string? LimitType { get; set; }
}
