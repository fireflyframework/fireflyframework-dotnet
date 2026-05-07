using FireflyFramework.Webhooks.Interfaces;

namespace FireflyFramework.Webhooks.Sdk;

/// <summary>
/// Typed contract for the inbound-webhook ingestion endpoint exposed by
/// <c>FireflyFramework.Webhooks.Web</c>.
/// </summary>
public interface IWebhookClient
{
    /// <summary>
    /// Posts <paramref name="payload"/> to <c>POST /api/webhooks/{provider}</c>
    /// and returns the framework's <see cref="WebhookResponseDto"/>.
    /// </summary>
    Task<WebhookResponseDto?> SendAsync(string provider, object payload, CancellationToken ct = default);
}
