namespace FireflyFramework.Cache.Core;

/// <summary>
/// Unified async cache contract. Mirrors Java <c>CacheAdapter</c>: every operation
/// returns a Task so adapters can wrap blocking back-ends (Redis, Hazelcast) without
/// pulling threads.
/// </summary>
public interface ICacheAdapter : IAsyncDisposable
{
    CacheType CacheType { get; }
    string CacheName { get; }
    bool IsAvailable { get; }

    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task PutAsync<T>(string key, T value, CancellationToken ct = default);
    Task PutAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);
    Task<bool> PutIfAbsentAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);
    Task<bool> EvictAsync(string key, CancellationToken ct = default);
    Task<long> EvictByPrefixAsync(string prefix, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyCollection<string>> KeysAsync(CancellationToken ct = default);
    Task<long> SizeAsync(CancellationToken ct = default);
    Task<CacheStats> GetStatsAsync(CancellationToken ct = default);
    Task<CacheHealth> GetHealthAsync(CancellationToken ct = default);
}
