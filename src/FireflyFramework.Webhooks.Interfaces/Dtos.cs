using System.Text.Json;

namespace FireflyFramework.Webhooks.Interfaces;

public sealed record WebhookEventDto(
    string EventId,
    string ProviderName,
    JsonElement Payload,
    Dictionary<string, string> Headers,
    Dictionary<string, string> QueryParams,
    DateTimeOffset ReceivedAt,
    string? SourceIp,
    string HttpMethod,
    Dictionary<string, object?>? EnrichedMetadata = null);

public sealed record WebhookResponseDto(
    string EventId,
    string Status,
    string? Message,
    long ProcessingTimeMs);
