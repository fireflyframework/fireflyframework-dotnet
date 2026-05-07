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

using FireflyFramework.Client.Deduplication;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// Tests for <see cref="RequestDeduplicator"/>. Pin the contract: same key → same task for
/// concurrent callers; cached result reuse within TTL; failures are not cached;
/// invalidation works.
/// </summary>
public sealed class RequestDeduplicatorTests
{
    [Fact]
    public async Task ExecuteAsync_DedupesConcurrentCallers_ToSingleFactoryInvocation()
    {
        var dedup = new RequestDeduplicator();
        var calls = 0;
        var gate = new TaskCompletionSource();

        async Task<int> Factory(CancellationToken ct)
        {
            Interlocked.Increment(ref calls);
            await gate.Task;
            return 42;
        }

        var t1 = dedup.ExecuteAsync("key", Factory);
        var t2 = dedup.ExecuteAsync("key", Factory);
        var t3 = dedup.ExecuteAsync("key", Factory);

        gate.SetResult();
        var results = await Task.WhenAll(t1, t2, t3);

        Assert.Equal(new[] { 42, 42, 42 }, results);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReusesCachedResult_WithinTtl()
    {
        var dedup = new RequestDeduplicator { CacheTtl = TimeSpan.FromSeconds(5) };
        var calls = 0;

        Task<int> Factory(CancellationToken ct)
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(99);
        }

        await dedup.ExecuteAsync("k", Factory);
        await dedup.ExecuteAsync("k", Factory);
        await dedup.ExecuteAsync("k", Factory);

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExecuteAsync_ExceptionFromFactory_IsNotCached()
    {
        var dedup = new RequestDeduplicator();
        var calls = 0;

        Task<int> Factory(CancellationToken ct)
        {
            Interlocked.Increment(ref calls);
            throw new InvalidOperationException("boom");
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => dedup.ExecuteAsync("k", Factory));
        await Assert.ThrowsAsync<InvalidOperationException>(() => dedup.ExecuteAsync("k", Factory));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Invalidate_ForcesRefactoryOnNextCall()
    {
        var dedup = new RequestDeduplicator();
        var calls = 0;
        Task<int> Factory(CancellationToken ct) { Interlocked.Increment(ref calls); return Task.FromResult(7); }

        await dedup.ExecuteAsync("k", Factory);
        dedup.Invalidate("k");
        await dedup.ExecuteAsync("k", Factory);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task InvalidateByPrefix_RemovesEveryMatchingKey()
    {
        var dedup = new RequestDeduplicator();

        await dedup.ExecuteAsync("user:1", _ => Task.FromResult(1));
        await dedup.ExecuteAsync("user:2", _ => Task.FromResult(2));
        await dedup.ExecuteAsync("order:1", _ => Task.FromResult(3));

        var removed = dedup.InvalidateByPrefix("user:");
        Assert.Equal(2, removed);

        // Re-fetch the invalidated keys and confirm a fresh factory is invoked.
        var calls = 0;
        Task<int> Counted(CancellationToken ct) { Interlocked.Increment(ref calls); return Task.FromResult(0); }

        await dedup.ExecuteAsync("user:1", Counted);
        await dedup.ExecuteAsync("user:2", Counted);
        await dedup.ExecuteAsync("order:1", Counted); // still cached
        Assert.Equal(2, calls);
    }
}
