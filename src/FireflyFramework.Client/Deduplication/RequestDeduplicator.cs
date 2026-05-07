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

namespace FireflyFramework.Client.Deduplication;

/// <summary>
/// Coalesces concurrent in-flight requests with the same key into a single execution and
/// hands the same <see cref="Task{T}"/> to every caller, keeping the result in cache for
/// <see cref="CacheTtl"/>. Mirrors Java <c>RequestDeduplicationManager</c>.
///
/// <para>Use this around expensive, idempotent calls — search queries, configuration
/// fetches, identity look-ups — to avoid "stampede" load on the upstream service.</para>
/// </summary>
public sealed class RequestDeduplicator
{
    private readonly ConcurrentDictionary<string, InFlightEntry> _inFlight = new();
    private readonly ConcurrentDictionary<string, CachedResult> _results = new();

    /// <summary>
    /// How long a successful result is cached after completion. Subsequent requests with
    /// the same key bypass the underlying call entirely until the cache entry expires.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan CacheTtl { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Executes <paramref name="factory"/> at most once per <paramref name="key"/>; concurrent
    /// callers receive the same task. Successful results are cached for <see cref="CacheTtl"/>;
    /// failed results are not cached, so the next caller retries.
    /// </summary>
    public Task<T> ExecuteAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);

        if (_results.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return Task.FromResult((T)cached.Value!);
        }

        // Use Lazy<Task<T>> so concurrent callers share the same task and the factory runs at most
        // once. The continuation runs after the factory completes (success or failure) and is
        // responsible for both removing the in-flight entry and (only on success) caching the
        // result for CacheTtl.
        var entry = _inFlight.GetOrAdd(key, k =>
        {
            var lazy = new Lazy<Task<T>>(() => RunAndCacheAsync(k, factory, ct), LazyThreadSafetyMode.ExecutionAndPublication);
            return new InFlightEntry(() => lazy.Value);
        });

        return (Task<T>)entry.Materialize();
    }

    private async Task<T> RunAndCacheAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken ct)
    {
        try
        {
            var result = await factory(ct).ConfigureAwait(false);
            _results[key] = new CachedResult(result!, DateTimeOffset.UtcNow + CacheTtl);
            return result;
        }
        finally
        {
            _inFlight.TryRemove(key, out _);
        }
    }

    /// <summary>Removes the cached result for <paramref name="key"/> (if any).</summary>
    public void Invalidate(string key)
    {
        _results.TryRemove(key, out _);
    }

    /// <summary>Removes every cached entry whose key starts with <paramref name="prefix"/>.</summary>
    public int InvalidateByPrefix(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        var removed = 0;
        foreach (var key in _results.Keys.ToList())
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal) && _results.TryRemove(key, out _)) removed++;
        }
        return removed;
    }

    private sealed record InFlightEntry(Func<Task> Materialize);
    private sealed record CachedResult(object Value, DateTimeOffset ExpiresAt);
}
