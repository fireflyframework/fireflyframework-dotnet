using FireflyFramework.Webhooks.Interfaces;

namespace FireflyFramework.Webhooks.Processor;

public sealed record WebhookProcessingContext(WebhookEventDto Event, string ProviderName);

public sealed record WebhookProcessingResult(bool Success, bool ShouldRetry, TimeSpan? RetryAfter, string? Message);

public interface IWebhookProcessor
{
    Task<WebhookProcessingResult> ProcessAsync(WebhookProcessingContext context, CancellationToken ct = default);
    Task BeforeProcessAsync(WebhookProcessingContext context, CancellationToken ct = default) => Task.CompletedTask;
    Task AfterProcessAsync(WebhookProcessingContext context, WebhookProcessingResult result, CancellationToken ct = default) => Task.CompletedTask;
    Task OnErrorAsync(WebhookProcessingContext context, Exception error, CancellationToken ct = default) => Task.CompletedTask;
}

public interface IWebhookSignatureValidator
{
    Task<bool> ValidateSignatureAsync(string payload, IReadOnlyDictionary<string, string> headers, string secret, CancellationToken ct = default);
}

public interface IWebhookIdempotencyService
{
    Task<bool> TryAcquireAsync(string eventId, string provider, TimeSpan ttl, CancellationToken ct = default);
}
