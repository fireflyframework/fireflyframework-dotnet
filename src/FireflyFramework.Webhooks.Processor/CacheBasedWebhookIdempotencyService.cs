using FireflyFramework.Cache.Core;

namespace FireflyFramework.Webhooks.Processor;

public sealed class CacheBasedWebhookIdempotencyService : IWebhookIdempotencyService
{
    private readonly ICacheAdapter _cache;
    public CacheBasedWebhookIdempotencyService(ICacheAdapter cache) => _cache = cache;

    public async Task<bool> TryAcquireAsync(string eventId, string provider, TimeSpan ttl, CancellationToken ct = default) =>
        await _cache.PutIfAbsentAsync($"webhook:{provider}:{eventId}", true, ttl, ct).ConfigureAwait(false);
}
