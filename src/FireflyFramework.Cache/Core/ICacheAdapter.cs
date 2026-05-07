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
