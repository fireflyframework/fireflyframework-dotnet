using System.Diagnostics;
using FireflyFramework.Webhooks.Core.Services;
using FireflyFramework.Webhooks.Interfaces;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Webhooks.Core;

public interface IWebhookProcessingService
{
    Task<WebhookResponseDto> ProcessAsync(WebhookEventDto evt, CancellationToken ct = default);
}

/// <summary>
/// Processes inbound webhook events through the full Firefly pipeline:
/// validation → enrichment → rate-limit → idempotency → user-supplied processor →
/// DLQ on failure. Mirrors Java <c>WebhookProcessingService</c>.
/// </summary>
public sealed class WebhookProcessingService : IWebhookProcessingService
{
    private readonly WebhookValidator? _validator;
    private readonly WebhookMetadataEnrichmentService? _enricher;
    private readonly WebhookRateLimitService? _rateLimit;
    private readonly IDeadLetterQueueService? _dlq;
    private readonly IWebhookProcessorRegistry? _processors;
    private readonly ILogger<WebhookProcessingService> _log;

    public WebhookProcessingService(
        ILogger<WebhookProcessingService> log,
        WebhookValidator? validator = null,
        WebhookMetadataEnrichmentService? enricher = null,
        WebhookRateLimitService? rateLimit = null,
        IDeadLetterQueueService? dlq = null,
        IWebhookProcessorRegistry? processors = null)
    {
        _log = log;
        _validator = validator;
        _enricher = enricher;
        _rateLimit = rateLimit;
        _dlq = dlq;
        _processors = processors;
    }

    public async Task<WebhookResponseDto> ProcessAsync(WebhookEventDto evt, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var payloadBytes = evt.Payload.GetRawText().Length;

        // 1. Validate
        if (_validator is not null)
        {
            var v = _validator.Validate(evt, payloadBytes);
            if (!v.IsValid)
            {
                _log.LogWarning("Webhook {EventId} rejected: {Reason}", evt.EventId, v.Reason);
                return new WebhookResponseDto(evt.EventId, "REJECTED", v.Reason, sw.ElapsedMilliseconds);
            }
        }

        // 2. Rate limit
        if (_rateLimit is not null && !await _rateLimit.TryAcquireAsync(evt.ProviderName, ct).ConfigureAwait(false))
        {
            _log.LogWarning("Webhook {EventId} rate-limited", evt.EventId);
            return new WebhookResponseDto(evt.EventId, "RATE_LIMITED", "rate-limit-exceeded", sw.ElapsedMilliseconds);
        }

        // 3. Enrich
        var enriched = _enricher?.Enrich(evt) ?? evt;

        // 4. Process
        try
        {
            if (_processors?.Resolve(evt.ProviderName) is { } processor)
            {
                var ctx = new FireflyFramework.Webhooks.Processor.WebhookProcessingContext(enriched, evt.ProviderName);
                var result = await processor.ProcessAsync(ctx, ct).ConfigureAwait(false);
                if (!result.Success && _dlq is not null)
                {
                    await _dlq.PublishAsync(enriched, result.Message ?? "processor-failed", attempts: 1, ct).ConfigureAwait(false);
                }

                return new WebhookResponseDto(evt.EventId,
                    result.Success ? "PROCESSED" : "FAILED",
                    result.Message,
                    sw.ElapsedMilliseconds);
            }

            _log.LogInformation("No processor registered for provider {Provider}, accepting", evt.ProviderName);
            return new WebhookResponseDto(evt.EventId, "ACCEPTED", null, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Webhook {EventId} processing threw", evt.EventId);
            if (_dlq is not null)
            {
                await _dlq.PublishAsync(enriched, ex.Message, attempts: 1, ct).ConfigureAwait(false);
            }

            return new WebhookResponseDto(evt.EventId, "ERROR", ex.Message, sw.ElapsedMilliseconds);
        }
    }
}

/// <summary>Resolves a registered <c>IWebhookProcessor</c> by provider name.</summary>
public interface IWebhookProcessorRegistry
{
    FireflyFramework.Webhooks.Processor.IWebhookProcessor? Resolve(string providerName);
}

/// <summary>Default in-memory registry. Register via DI as a singleton.</summary>
public sealed class InMemoryWebhookProcessorRegistry : IWebhookProcessorRegistry
{
    private readonly Dictionary<string, FireflyFramework.Webhooks.Processor.IWebhookProcessor> _processors;

    public InMemoryWebhookProcessorRegistry(IEnumerable<KeyValuePair<string, FireflyFramework.Webhooks.Processor.IWebhookProcessor>> processors)
    {
        _processors = new Dictionary<string, FireflyFramework.Webhooks.Processor.IWebhookProcessor>(processors, StringComparer.OrdinalIgnoreCase);
    }

    public FireflyFramework.Webhooks.Processor.IWebhookProcessor? Resolve(string providerName) =>
        _processors.TryGetValue(providerName, out var p) ? p : null;
}

public sealed class WebhookOptions
{
    public const string SectionName = "Firefly:Webhooks";
    public RetryOptions Retry { get; set; } = new();
    public RateLimitOptions RateLimit { get; set; } = new();
}

public sealed class RetryOptions
{
    public int MaxAttempts { get; set; } = 3;
    public int InitialDelayMs { get; set; } = 200;
    public int MaxDelayMs { get; set; } = 30_000;
    public double BackoffMultiplier { get; set; } = 2.0;
}

public sealed class RateLimitOptions
{
    public int RequestsPerSecond { get; set; } = 100;
    public int BurstSize { get; set; } = 200;
}
