using FireflyFramework.Cache.Adapters;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace FireflyFramework.Tests;

public class CacheTests
{
    [Fact]
    public async Task MemoryCacheAdapter_round_trip()
    {
        var cache = new MemoryCacheAdapter(new MemoryCache(new MemoryCacheOptions()));
        await cache.PutAsync("k", "v");
        (await cache.GetAsync<string>("k")).Should().Be("v");
    }

    [Fact]
    public async Task PutIfAbsent_returns_false_when_key_exists()
    {
        var cache = new MemoryCacheAdapter(new MemoryCache(new MemoryCacheOptions()));
        (await cache.PutIfAbsentAsync("k", "v")).Should().BeTrue();
        (await cache.PutIfAbsentAsync("k", "v2")).Should().BeFalse();
        (await cache.GetAsync<string>("k")).Should().Be("v");
    }

    [Fact]
    public async Task EvictByPrefix_removes_matching_keys()
    {
        var cache = new MemoryCacheAdapter(new MemoryCache(new MemoryCacheOptions()));
        await cache.PutAsync("a:1", 1);
        await cache.PutAsync("a:2", 2);
        await cache.PutAsync("b:1", 3);
        var evicted = await cache.EvictByPrefixAsync("a:");
        evicted.Should().Be(2);
        (await cache.SizeAsync()).Should().Be(1);
    }
}
