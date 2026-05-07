using FireflyFramework.Cache.Core;

namespace FireflyFramework.Cache.Adapters;

/// <summary>No-op adapter; useful for tests or to disable caching globally.</summary>
public sealed class NoopCacheAdapter : ICacheAdapter
{
    public CacheType CacheType => CacheType.NoOp;
    public string CacheName => "noop";
    public bool IsAvailable => true;

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) => Task.FromResult<T?>(default);
    public Task PutAsync<T>(string key, T value, CancellationToken ct = default) => Task.CompletedTask;
    public Task PutAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) => Task.CompletedTask;
    public Task<bool> PutIfAbsentAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) => Task.FromResult(true);
    public Task<bool> EvictAsync(string key, CancellationToken ct = default) => Task.FromResult(false);
    public Task<long> EvictByPrefixAsync(string prefix, CancellationToken ct = default) => Task.FromResult(0L);
    public Task ClearAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(false);
    public Task<IReadOnlyCollection<string>> KeysAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>());
    public Task<long> SizeAsync(CancellationToken ct = default) => Task.FromResult(0L);
    public Task<CacheStats> GetStatsAsync(CancellationToken ct = default) => Task.FromResult(CacheStats.Empty(CacheType, CacheName));
    public Task<CacheHealth> GetHealthAsync(CancellationToken ct = default) => Task.FromResult(CacheHealth.Healthy(CacheType, CacheName));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
