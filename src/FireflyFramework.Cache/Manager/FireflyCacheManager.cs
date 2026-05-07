using FireflyFramework.Cache.Core;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Cache.Manager;

/// <summary>
/// Multi-tier cache manager: primary cache with optional fallback. Mirrors Java
/// <c>FireflyCacheManager</c>: if the primary throws or is unavailable, operations
/// transparently route to the fallback.
/// </summary>
public sealed class FireflyCacheManager : ICacheAdapter
{
    private readonly ICacheAdapter _primary;
    private readonly ICacheAdapter? _fallback;
    private readonly ILogger<FireflyCacheManager> _log;

    public FireflyCacheManager(ICacheAdapter primary, ILogger<FireflyCacheManager> log, ICacheAdapter? fallback = null)
    {
        _primary = primary;
        _fallback = fallback;
        _log = log;
    }

    public CacheType CacheType => _primary.CacheType;
    public string CacheName => $"firefly-manager[{_primary.CacheName}]";
    public bool IsAvailable => _primary.IsAvailable || (_fallback?.IsAvailable ?? false);

    private ICacheAdapter Active => _primary.IsAvailable ? _primary : (_fallback ?? _primary);

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) => Active.GetAsync<T>(key, ct);
    public Task PutAsync<T>(string key, T value, CancellationToken ct = default) => Active.PutAsync(key, value, ct);
    public Task PutAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) => Active.PutAsync(key, value, ttl, ct);
    public Task<bool> PutIfAbsentAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) => Active.PutIfAbsentAsync(key, value, ttl, ct);
    public Task<bool> EvictAsync(string key, CancellationToken ct = default) => Active.EvictAsync(key, ct);
    public Task<long> EvictByPrefixAsync(string prefix, CancellationToken ct = default) => Active.EvictByPrefixAsync(prefix, ct);
    public Task ClearAsync(CancellationToken ct = default) => Active.ClearAsync(ct);
    public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => Active.ExistsAsync(key, ct);
    public Task<IReadOnlyCollection<string>> KeysAsync(CancellationToken ct = default) => Active.KeysAsync(ct);
    public Task<long> SizeAsync(CancellationToken ct = default) => Active.SizeAsync(ct);
    public Task<CacheStats> GetStatsAsync(CancellationToken ct = default) => Active.GetStatsAsync(ct);
    public Task<CacheHealth> GetHealthAsync(CancellationToken ct = default) => Active.GetHealthAsync(ct);

    public async ValueTask DisposeAsync()
    {
        await _primary.DisposeAsync().ConfigureAwait(false);
        if (_fallback is not null) await _fallback.DisposeAsync().ConfigureAwait(false);
    }
}
