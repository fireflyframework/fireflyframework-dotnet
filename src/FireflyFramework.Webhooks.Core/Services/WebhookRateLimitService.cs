using System.Threading.RateLimiting;
using FireflyFramework.Cache.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Webhooks.Core.Services;

public sealed class RateLimitOptionsConfiguration
{
    public bool Enabled { get; set; } = true;
    public int RequestsPerSecond { get; set; } = 100;
    public int BurstSize { get; set; } = 200;
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Per-provider sliding window rate limiter. Default uses
/// <see cref="System.Threading.RateLimiting"/>; for distributed enforcement supply a
/// shared <see cref="ICacheAdapter"/> and the limiter switches to a Redis-counting
/// algorithm.
/// </summary>
public sealed class WebhookRateLimitService
{
    private readonly Dictionary<string, RateLimiter> _limiters = new();
    private readonly RateLimitOptionsConfiguration _options;
    private readonly ICacheAdapter? _distributedCache;
    private readonly ILogger<WebhookRateLimitService> _log;

    public WebhookRateLimitService(
        IOptions<RateLimitOptionsConfiguration> options,
        ILogger<WebhookRateLimitService> log,
        ICacheAdapter? cache = null)
    {
        _options = options.Value;
        _log = log;
        _distributedCache = cache;
    }

    public async Task<bool> TryAcquireAsync(string provider, CancellationToken ct = default)
    {
        if (!_options.Enabled) return true;

        if (_distributedCache is not null && _distributedCache.CacheType == CacheType.Redis)
        {
            return await TryAcquireDistributedAsync(provider, ct).ConfigureAwait(false);
        }

        var limiter = GetOrCreate(provider);
        using var lease = await limiter.AcquireAsync(1, ct).ConfigureAwait(false);
        return lease.IsAcquired;
    }

    private RateLimiter GetOrCreate(string provider)
    {
        lock (_limiters)
        {
            if (_limiters.TryGetValue(provider, out var existing))
            {
                return existing;
            }

            var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = _options.BurstSize,
                TokensPerPeriod = _options.RequestsPerSecond,
                ReplenishmentPeriod = _options.Window,
                AutoReplenishment = true,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            });
            _limiters[provider] = limiter;
            return limiter;
        }
    }

    private async Task<bool> TryAcquireDistributedAsync(string provider, CancellationToken ct)
    {
        // Simple fixed-window counter using cache.PutIfAbsent + manual increments.
        var bucket = $"webhook:ratelimit:{provider}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds() / (long)_options.Window.TotalSeconds}";
        var limit = _options.RequestsPerSecond * (long)_options.Window.TotalSeconds;

        var current = await _distributedCache!.GetAsync<long?>(bucket, ct).ConfigureAwait(false) ?? 0;
        if (current >= limit)
        {
            return false;
        }

        await _distributedCache.PutAsync(bucket, current + 1, _options.Window, ct).ConfigureAwait(false);
        return true;
    }
}
