# FireflyFramework.Cache

Unified async cache abstraction with Memory, Redis and Hazelcast adapters, multi-tier strategies, statistics and health. Mirrors `fireflyframework-cache`.

## Quick start

```csharp
builder.Services.AddFireflyCache(builder.Configuration);

var cache = sp.GetRequiredService<ICacheAdapter>();
await cache.PutAsync("user:42", user, TimeSpan.FromMinutes(15));
var hit = await cache.GetAsync<User>("user:42");
```

## What's inside

| Type | Purpose |
|---|---|
| `ICacheAdapter` | Unified contract: get / put / putIfAbsent / evict / evictByPrefix / clear / exists / keys / size / stats / health. |
| `MemoryCacheAdapter` | In-process backed by `IMemoryCache`. |
| `RedisCacheAdapter` | StackExchange.Redis-backed with prefix-aware key management and async health. |
| `NoopCacheAdapter` | Useful in tests / when caching should be disabled. |
| `FireflyCacheManager` | Composite primary + fallback adapter — transparent failover when the primary is unavailable. |
| `ICacheSerializer` + `JsonCacheSerializer` | Serializer SPI; default is `System.Text.Json`. |
| `CacheStats` / `CacheHealth` / `CacheType` | Observability primitives. |

## Configuration

```jsonc
{
  "Firefly": {
    "Cache": {
      "Provider": "Redis",         // Memory | Redis | Hazelcast | NoOp | Auto
      "Name": "default",
      "KeyPrefix": "firefly:cache:",
      "Redis": {
        "ConnectionString": "localhost:6379"
      }
    }
  }
}
```
