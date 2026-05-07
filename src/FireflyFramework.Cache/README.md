# FireflyFramework.Cache

Unified async cache abstraction with Memory, Redis, and Noop adapters,
primary/fallback compositing, statistics, and health probes. Mirrors
`org.fireflyframework:firefly-common-cache`.

## Wiring

```csharp
using FireflyFramework.Cache.Core;
using FireflyFramework.Cache.DependencyInjection;

builder.Services.AddFireflyCache(builder.Configuration);

var cache = sp.GetRequiredService<ICacheAdapter>();
await cache.PutAsync("user:42", user, TimeSpan.FromMinutes(15));
var hit = await cache.GetAsync<User>("user:42");
```

## Public surface

### `ICacheAdapter`

The unified async contract every adapter implements.

| Member                       | Behaviour                                                  |
|------------------------------|------------------------------------------------------------|
| `CacheType`                  | Enum value identifying the backing store                   |
| `CacheName`                  | Friendly name for logs / metrics                           |
| `IsAvailable`                | Quick liveness flag, set false if the backend is down      |
| `GetAsync<T>(key)`           | Returns the deserialised value or `default(T)` on miss     |
| `PutAsync<T>(key, value)`    | Stores with the adapter's default TTL                      |
| `PutAsync<T>(key, v, ttl)`   | Stores with an explicit TTL                                |
| `PutIfAbsentAsync<T>`        | SETNX semantics; returns true only if the key was inserted |
| `EvictAsync(key)`            | Returns true if a key was removed                          |
| `EvictByPrefixAsync(prefix)` | Bulk-removes by prefix; returns the count                  |
| `ClearAsync`                 | Wipes every entry (use sparingly)                          |
| `ExistsAsync(key)`           | Cheaper than `GetAsync` when you only need presence        |
| `KeysAsync`                  | Returns all known keys (Redis: SCAN; Memory: index map)    |
| `SizeAsync`                  | Number of entries in the cache                             |
| `GetStatsAsync`              | `CacheStats` (hits, misses, evictions, hit ratio)          |
| `GetHealthAsync`             | `CacheHealth` (status, latency, last error)                |

### Adapters

| Adapter                | Backing store                | Notes                                                                                        |
|------------------------|------------------------------|----------------------------------------------------------------------------------------------|
| `MemoryCacheAdapter`   | `IMemoryCache` + key index   | In-process; supports TTL, prefix eviction, size accounting                                   |
| `RedisCacheAdapter`    | StackExchange.Redis          | Distributed; uses Redis SCAN for prefix eviction; JSON value serialisation                   |
| `NoopCacheAdapter`     | None                         | Drop-in for tests or environments where caching is intentionally disabled                    |
| `FireflyCacheManager`  | Composite primary + fallback | Transparent failover when the primary adapter reports unhealthy                              |

### Serialisation

`ICacheSerializer` SPI plus the default `JsonCacheSerializer`
(System.Text.Json). Replace with a binary serialiser by registering an
alternative implementation before `AddFireflyCache`.

## Configuration

```json
{
  "Firefly": {
    "Cache": {
      "Provider":  "Redis",
      "Name":      "default",
      "KeyPrefix": "firefly:cache:",
      "Redis": {
        "ConnectionString": "localhost:6379",
        "DefaultTtl":       "00:15:00"
      },
      "Memory": {
        "SizeLimit": 100000
      }
    }
  }
}
```

`Provider` accepts `Memory`, `Redis`, `NoOp`, or `Auto` (selects Redis if
a connection string is configured, Memory otherwise).

## Dependencies

| Reference                              | Used for             |
|----------------------------------------|----------------------|
| `FireflyFramework.Kernel`              | Base exception type  |
| `Microsoft.Extensions.Caching.Memory`  | `MemoryCacheAdapter` |
| `StackExchange.Redis`                  | `RedisCacheAdapter`  |

`System.Text.Json` (used by the default serialiser) ships in the .NET 10
framework — no package import needed.

## Java mapping

| .NET                  | Java                                                              |
|-----------------------|-------------------------------------------------------------------|
| `ICacheAdapter`       | `CacheAdapter`                                                    |
| `MemoryCacheAdapter`  | `CaffeineCacheAdapter`                                            |
| `RedisCacheAdapter`   | `RedisCacheAdapter`                                               |
| `NoopCacheAdapter`    | (no direct equivalent — replicates Spring's "no caching" profile) |
| `FireflyCacheManager` | `FireflyCacheManager`                                             |
| `JsonCacheSerializer` | `JsonCacheSerializer`                                             |
| `FireflyCacheOptions` | `CacheProperties`                                                 |
