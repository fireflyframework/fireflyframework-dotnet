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

using FireflyFramework.Cache.Core;
using StackExchange.Redis;

namespace FireflyFramework.Cache.Adapters;

/// <summary>Redis-backed adapter using StackExchange.Redis. Equivalent to the Lettuce/Spring Redis adapter.</summary>
public sealed class RedisCacheAdapter : ICacheAdapter
{
    private readonly IConnectionMultiplexer _mux;
    private readonly IDatabase _db;
    private readonly ICacheSerializer _serializer;
    private readonly string _prefix;
    private long _hits;
    private long _misses;
    private long _puts;
    private DateTimeOffset _lastSuccess = DateTimeOffset.UtcNow;

    public RedisCacheAdapter(IConnectionMultiplexer mux, ICacheSerializer serializer, string name = "redis", string keyPrefix = "firefly:cache:")
    {
        _mux = mux;
        _db = mux.GetDatabase();
        _serializer = serializer;
        CacheName = name;
        _prefix = keyPrefix;
    }

    public CacheType CacheType => CacheType.Redis;

    public string CacheName { get; }

    public bool IsAvailable => _mux.IsConnected;

    private RedisKey Key(string key) => $"{_prefix}{key}";

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var data = await _db.StringGetAsync(Key(key)).ConfigureAwait(false);
        if (data.IsNullOrEmpty)
        {
            Interlocked.Increment(ref _misses);
            return default;
        }

        Interlocked.Increment(ref _hits);
        _lastSuccess = DateTimeOffset.UtcNow;
        return _serializer.Deserialize<T>((byte[])data!);
    }

    public Task PutAsync<T>(string key, T value, CancellationToken ct = default) => InternalPutAsync(key, value, null);

    public Task PutAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) => InternalPutAsync(key, value, ttl);

    private async Task InternalPutAsync<T>(string key, T value, TimeSpan? ttl)
    {
        await _db.StringSetAsync(Key(key), _serializer.Serialize(value), ttl).ConfigureAwait(false);
        Interlocked.Increment(ref _puts);
        _lastSuccess = DateTimeOffset.UtcNow;
    }

    public async Task<bool> PutIfAbsentAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) =>
        await _db.StringSetAsync(Key(key), _serializer.Serialize(value), ttl, When.NotExists).ConfigureAwait(false);

    public Task<bool> EvictAsync(string key, CancellationToken ct = default) =>
        _db.KeyDeleteAsync(Key(key));

    public async Task<long> EvictByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        var server = _mux.GetServers().FirstOrDefault(s => s.IsConnected);
        if (server is null)
        {
            return 0;
        }

        long evicted = 0;
        await foreach (var key in server.KeysAsync(pattern: $"{_prefix}{prefix}*"))
        {
            if (await _db.KeyDeleteAsync(key).ConfigureAwait(false))
            {
                evicted++;
            }
        }

        return evicted;
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        var server = _mux.GetServers().FirstOrDefault(s => s.IsConnected);
        if (server is null) return;

        await foreach (var key in server.KeysAsync(pattern: $"{_prefix}*"))
        {
            await _db.KeyDeleteAsync(key).ConfigureAwait(false);
        }
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => _db.KeyExistsAsync(Key(key));

    public async Task<IReadOnlyCollection<string>> KeysAsync(CancellationToken ct = default)
    {
        var server = _mux.GetServers().FirstOrDefault(s => s.IsConnected);
        if (server is null) return Array.Empty<string>();
        var prefixLen = _prefix.Length;
        var list = new List<string>();
        await foreach (var key in server.KeysAsync(pattern: $"{_prefix}*"))
        {
            list.Add(key.ToString()[prefixLen..]);
        }

        return list;
    }

    public async Task<long> SizeAsync(CancellationToken ct = default)
    {
        var server = _mux.GetServers().FirstOrDefault(s => s.IsConnected);
        if (server is null) return 0;
        long count = 0;
        await foreach (var _ in server.KeysAsync(pattern: $"{_prefix}*"))
        {
            count++;
        }

        return count;
    }

    public Task<CacheStats> GetStatsAsync(CancellationToken ct = default) => Task.FromResult(new CacheStats(
        CacheType, CacheName, _hits + _misses, _hits, _misses, _puts, 0, 0, TimeSpan.Zero, 0, DateTimeOffset.UtcNow));

    public Task<CacheHealth> GetHealthAsync(CancellationToken ct = default)
    {
        if (!_mux.IsConnected)
        {
            return Task.FromResult(CacheHealth.Unhealthy(CacheType, CacheName, "Redis disconnected"));
        }

        return Task.FromResult(CacheHealth.Healthy(CacheType, CacheName) with { LastSuccessfulOperation = _lastSuccess });
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
