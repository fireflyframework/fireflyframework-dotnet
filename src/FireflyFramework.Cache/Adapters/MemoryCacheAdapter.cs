// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Concurrent;
using System.Threading;
using FireflyFramework.Cache.Core;
using Microsoft.Extensions.Caching.Memory;

namespace FireflyFramework.Cache.Adapters;

/// <summary>
/// In-process cache backed by <see cref="IMemoryCache"/>. Equivalent to the Caffeine
/// adapter on the Java side.
/// </summary>
public sealed class MemoryCacheAdapter : ICacheAdapter
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    private long _hits;
    private long _misses;
    private long _puts;
    private DateTimeOffset _lastSuccess = DateTimeOffset.UtcNow;

    public MemoryCacheAdapter(IMemoryCache cache, string name = "memory")
    {
        _cache = cache;
        CacheName = name;
    }

    public CacheType CacheType => CacheType.Memory;

    public string CacheName { get; }

    public bool IsAvailable => true;

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(key, out var value))
        {
            Interlocked.Increment(ref _hits);
            _lastSuccess = DateTimeOffset.UtcNow;
            return Task.FromResult((T?)value);
        }

        Interlocked.Increment(ref _misses);
        return Task.FromResult<T?>(default);
    }

    public Task PutAsync<T>(string key, T value, CancellationToken ct = default)
    {
        _cache.Set(key, value);
        _keys.TryAdd(key, 0);
        Interlocked.Increment(ref _puts);
        _lastSuccess = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task PutAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        _cache.Set(key, value, ttl);
        _keys.TryAdd(key, 0);
        Interlocked.Increment(ref _puts);
        return Task.CompletedTask;
    }

    public Task<bool> PutIfAbsentAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(key, out _))
        {
            return Task.FromResult(false);
        }

        if (ttl is not null)
        {
            _cache.Set(key, value, ttl.Value);
        }
        else
        {
            _cache.Set(key, value);
        }

        _keys.TryAdd(key, 0);
        return Task.FromResult(true);
    }

    public Task<bool> EvictAsync(string key, CancellationToken ct = default)
    {
        var present = _cache.TryGetValue(key, out _);
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
        return Task.FromResult(present);
    }

    public Task<long> EvictByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        long evicted = 0;
        foreach (var key in _keys.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
            evicted++;
        }

        return Task.FromResult(evicted);
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        foreach (var key in _keys.Keys.ToList())
        {
            _cache.Remove(key);
        }

        _keys.Clear();
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(_cache.TryGetValue(key, out _));

    public Task<IReadOnlyCollection<string>> KeysAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyCollection<string>>(_keys.Keys.ToList());

    public Task<long> SizeAsync(CancellationToken ct = default) => Task.FromResult((long)_keys.Count);

    public Task<CacheStats> GetStatsAsync(CancellationToken ct = default) => Task.FromResult(new CacheStats(
        CacheType, CacheName, _hits + _misses, _hits, _misses, _puts, 0, _keys.Count, TimeSpan.Zero, 0, DateTimeOffset.UtcNow));

    public Task<CacheHealth> GetHealthAsync(CancellationToken ct = default) => Task.FromResult(
        CacheHealth.Healthy(CacheType, CacheName) with { LastSuccessfulOperation = _lastSuccess });

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
