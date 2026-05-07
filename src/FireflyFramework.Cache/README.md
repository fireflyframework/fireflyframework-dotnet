# FireflyFramework.Cache

## Overview

`FireflyFramework.Cache` is the **distributed-cache abstraction tier**
of the Firefly framework. It exposes a single async port —
`ICacheAdapter` — and ships three production adapters (Memory, Redis,
NoOp) plus a transparent primary/fallback composite
(`FireflyCacheManager`). Mirrors `org.fireflyframework:firefly-common-cache`
one-to-one.

The single-port design is deliberate. Every consumer
(CQRS query cache, idempotency middleware, OAuth2 token cache, custom
service code) talks to the *same* `ICacheAdapter` interface, regardless
of whether you're running an in-memory cache during local dev or a
Redis cluster in production. Swapping the backend is a one-line
configuration change.

## Why a separate module?

Spring's `@Cacheable` and ASP.NET's `IDistributedCache` are both
*procedural* abstractions — you call them in your code, and the cache
either holds the value or doesn't. The Firefly cache adds three things
the platform abstractions don't:

1. **Transparent fallback.** If Redis is unreachable,
   `FireflyCacheManager` routes operations to a local Memory adapter
   instead of failing the request — at the cost of one process's
   worth of staleness.
2. **First-class statistics + health.** `GetStatsAsync` and
   `GetHealthAsync` are part of the contract, so observability
   wiring is uniform across adapter choices.
3. **Prefix eviction.** `EvictByPrefixAsync` is the primitive used by
   the CQRS event-driven cache invalidator and the orchestration
   query projection — neither `IDistributedCache` nor Redis's
   `KEYS *` are appropriate (the former lacks the operation, the
   latter blocks the server).

## Mental model

```
  ┌───────────────────────────────────────────────────────────────┐
  │                      ICacheAdapter (port)                     │
  └───────────────────────────────────────────────────────────────┘
            ▲                  ▲                  ▲
            │                  │                  │
   ┌────────┴───────┐  ┌───────┴───────┐  ┌───────┴────────┐
   │ MemoryAdapter  │  │ RedisAdapter  │  │ NoopAdapter    │
   │ in-process     │  │ distributed   │  │ disabled       │
   │ Microsoft.     │  │ StackExchange │  │ always misses  │
   │ Caching.Memory │  │ .Redis        │  │                │
   └────────────────┘  └───────────────┘  └────────────────┘

         (composed transparently by FireflyCacheManager)

         ┌────────────────┐    primary unhealthy    ┌────────────────┐
   ────► │ FireflyCacheManager │ ─────────────────►  │   fallback     │
         │   primary slot │                          │  (typically    │
         │                │ ◄──── primary back ────  │   Memory)      │
         └────────────────┘                          └────────────────┘
```

`FireflyCacheManager` is itself an `ICacheAdapter`, so every consumer
remains insulated from the failover behaviour.

## Quick start

```csharp
using FireflyFramework.Cache.Core;
using FireflyFramework.Cache.DependencyInjection;

builder.Services.AddFireflyCache(builder.Configuration);

// In your service:
var cache = sp.GetRequiredService<ICacheAdapter>();
await cache.PutAsync("user:42", user, TimeSpan.FromMinutes(15));
var hit = await cache.GetAsync<User>("user:42");
if (hit is null) { /* miss */ }
```

That's the whole everyday API. The configuration section
(`Firefly:Cache`) decides which adapter is used.

## Public surface

### `ICacheAdapter`

The unified async contract every adapter implements.

| Member                          | Behaviour                                                          |
|---------------------------------|--------------------------------------------------------------------|
| `CacheType`                     | Enum value identifying the backing store                           |
| `CacheName`                     | Friendly name surfaced in logs / metrics                           |
| `IsAvailable`                   | Quick liveness flag — false if the backend is down                 |
| `GetAsync<T>(key, ct)`          | Returns the deserialised value or `default(T)` on miss             |
| `PutAsync<T>(key, value, ct)`   | Stores with the adapter's default TTL                              |
| `PutAsync<T>(key, v, ttl, ct)`  | Stores with an explicit TTL                                        |
| `PutIfAbsentAsync<T>`           | SETNX semantics; returns true only if the key was inserted         |
| `EvictAsync(key, ct)`           | Returns true if a key was removed                                  |
| `EvictByPrefixAsync(prefix, ct)`| Bulk-removes by prefix; returns the count                          |
| `ClearAsync(ct)`                | Wipes every entry under the configured prefix (use sparingly)      |
| `ExistsAsync(key, ct)`          | Cheaper than `GetAsync` when you only need presence                |
| `KeysAsync(ct)`                 | Returns all known keys (Redis: SCAN; Memory: index map)            |
| `SizeAsync(ct)`                 | Number of entries in the cache                                     |
| `GetStatsAsync(ct)`             | `CacheStats` (hits, misses, evictions, hit ratio)                  |
| `GetHealthAsync(ct)`            | `CacheHealth` (status, latency, last error)                        |

### Adapters

| Adapter                | Backing store                | Notes                                                                                        |
|------------------------|------------------------------|----------------------------------------------------------------------------------------------|
| `MemoryCacheAdapter`   | `IMemoryCache` + key index   | In-process; supports TTL, prefix eviction, size accounting                                   |
| `RedisCacheAdapter`    | StackExchange.Redis          | Distributed; uses Redis SCAN (non-blocking) for prefix eviction; JSON value serialisation    |
| `NoopCacheAdapter`     | None                         | Drop-in for tests or environments where caching is intentionally disabled                    |
| `FireflyCacheManager`  | Composite primary + fallback | Transparent failover when the primary adapter reports unhealthy                              |

#### Adapter selection rule

`Firefly:Cache:Provider` accepts:

| Value      | Effect                                                                                                  |
|------------|---------------------------------------------------------------------------------------------------------|
| `Memory`   | Always uses `MemoryCacheAdapter`                                                                        |
| `Redis`    | Always uses `RedisCacheAdapter`; throws on startup if no connection string                              |
| `NoOp`     | Always uses `NoopCacheAdapter` (every Get returns default, every Put silently drops)                    |
| `Auto`     | Picks Redis if `Redis.ConnectionString` is configured, Memory otherwise                                  |

### Statistics

```csharp
public sealed record CacheStats(
    CacheType        Type,
    string           Name,
    long             Operations,
    long             Hits,
    long             Misses,
    long             Puts,
    long             Evictions,
    long             Errors,
    TimeSpan         AverageLatency,
    long             EstimatedSizeBytes,
    DateTimeOffset   AsOf);
```

`Hits / (Hits + Misses)` is the canonical hit-ratio. Surface it on
your dashboard alongside p99 latency to spot regressions when a code
change reorders cache lookups.

### Health

```csharp
public sealed record CacheHealth(
    CacheType        Type,
    string           Name,
    HealthStatus     Status,           // Healthy | Degraded | Unhealthy
    string?          LastError,
    DateTimeOffset?  LastSuccessfulOperation,
    TimeSpan?        LatencyToBackend);
```

`CacheHealth.Healthy(...)` and `CacheHealth.Unhealthy(...)` are the
canonical builders; the latter accepts a reason string surfaced
verbatim to operators.

### Serialisation

`ICacheSerializer` SPI plus the default `JsonCacheSerializer` (System.Text.Json).
Replace with a binary serialiser by registering an alternative
implementation **before** `AddFireflyCache`:

```csharp
services.AddSingleton<ICacheSerializer, MessagePackCacheSerializer>();
services.AddFireflyCache(configuration);
```

The serialiser interface is `byte[] Serialize<T>(T)` /
`T? Deserialize<T>(byte[])` — anything implementing it is composable.

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

| Property                        | Default               | Purpose                                                  |
|---------------------------------|-----------------------|----------------------------------------------------------|
| `Provider`                      | `Auto`                | Selects the active adapter                               |
| `Name`                          | `default`             | Friendly name in logs / metrics                          |
| `KeyPrefix`                     | `firefly:cache:`      | Namespacing prefix prepended to every key                |
| `Redis.ConnectionString`        | `localhost:6379`      | StackExchange.Redis configuration string                 |
| `Redis.DefaultTtl`              | (none)                | Optional default expiry when `PutAsync(key, value)` is used without an explicit TTL |
| `Memory.SizeLimit`              | (unbounded)           | Max number of in-process entries before LRU eviction      |

`KeyPrefix` is what makes it safe for two services to share a Redis —
each service uses its own prefix, and the `EvictByPrefixAsync` operation
is bounded to that prefix on the Redis SCAN side.

## Common patterns

### Cache-aside read

```csharp
public async Task<UserDto?> GetUserAsync(Guid id, CancellationToken ct)
{
    var key = $"user:{id}";
    var hit = await cache.GetAsync<UserDto>(key, ct);
    if (hit is not null) return hit;

    var fresh = await repository.GetUserAsync(id, ct);
    if (fresh is not null)
    {
        await cache.PutAsync(key, fresh, TimeSpan.FromMinutes(15), ct);
    }
    return fresh;
}
```

### Write-through invalidation

```csharp
public async Task UpdateUserAsync(UserDto user, CancellationToken ct)
{
    await repository.UpdateUserAsync(user, ct);
    await cache.EvictAsync($"user:{user.Id}", ct);
    await cache.EvictByPrefixAsync($"user:{user.Id}:", ct);   // related projections
}
```

### Distributed lock with `PutIfAbsentAsync`

`PutIfAbsentAsync` returns `true` if (and only if) the key was not
already present — i.e. SETNX semantics — so you can use it as a
poor-man's distributed lock under Redis:

```csharp
var lockKey = $"lock:user:{id}";
var acquired = await cache.PutIfAbsentAsync(lockKey, ownerId,
                                            TimeSpan.FromSeconds(30), ct);
if (!acquired) return Conflict("another writer holds the lock");

try
{
    /* critical section */
}
finally
{
    await cache.EvictAsync(lockKey, ct);
}
```

This is best-effort — in a network partition both sides may believe
they hold the lock. If you need true mutex guarantees, use a
purpose-built primitive (`Redlock.NET`, `ZooKeeper`, …).

### Prefix-scoped eviction on event

The CQRS event-driven cache invalidator uses `EvictByPrefixAsync` to
clear all derived projections of an aggregate when its state changes.
You can do the same in your own code:

```csharp
async Task OnOrderShipped(OrderShippedEvent e, CancellationToken ct)
{
    await cache.EvictByPrefixAsync($"order:{e.OrderId}:", ct);
    await cache.EvictByPrefixAsync($"customer:{e.CustomerId}:orders:", ct);
}
```

### Memory + Redis with manager

```csharp
var primary  = new RedisCacheAdapter(mux, serializer);
var fallback = new MemoryCacheAdapter(memoryCache, serializer);
var manager  = new FireflyCacheManager(primary, log, fallback);

services.AddSingleton<ICacheAdapter>(manager);
```

Operations route to Redis when it's healthy, slip to the in-process
Memory adapter when Redis is degraded, and resume on Redis the moment
`IsAvailable` flips back. Be aware: the fallback diverges from Redis
during the outage — any writes during the partition stay local. Plan
for stale reads on the rejoining instance.

## Pitfalls and gotchas

- **`KeysAsync` and `SizeAsync` enumerate the keyspace.** On Redis,
  this uses non-blocking SCAN, but it's still a full scan over the
  prefix. Don't call them on every request — they're for diagnostics
  and admin tooling, not hot-path code.
- **`ClearAsync` only clears the configured prefix.** Two services
  with different `KeyPrefix` values won't step on each other.
- **`EvictByPrefixAsync` does not pre-read the keys.** On the in-memory
  adapter the index is local; on Redis the keys are streamed via SCAN.
  Either way, the operation is bounded by the prefix's cardinality.
  Don't pass an empty prefix unless you mean it.
- **`PutAsync(key, value)` (no TTL) defers to the adapter's default.**
  Memory uses no TTL by default (entries live forever); Redis uses
  `Redis.DefaultTtl` when configured, otherwise no TTL. If you don't
  want unbounded retention, always pass an explicit TTL or set
  `Redis.DefaultTtl`.
- **Stats are best-effort and adapter-local.** The Memory adapter
  counts hits/misses in process; the Redis adapter counts only the
  ones routed *through this process*. Aggregating across instances is
  the dashboard's job, not the adapter's.
- **`FireflyCacheManager` does not synchronise primary and fallback.**
  Writes go to whichever is currently active. If the primary becomes
  healthy after the fallback served writes, those writes are not
  back-filled. Treat the fallback as a "degraded mode" cache, not a
  strict replica.
- **JSON serialisation rejects polymorphic values without converters.**
  If you cache a base type with derived instances, register a custom
  `JsonSerializerOptions` via your own `ICacheSerializer`
  implementation.

## Internals (for the curious)

- The `MemoryCacheAdapter` keeps a parallel `ConcurrentDictionary<string, byte>`
  index next to `IMemoryCache` so it can implement `KeysAsync`,
  `SizeAsync`, and `EvictByPrefixAsync` — operations the underlying
  abstraction doesn't expose. Eviction callbacks remove the index
  entry to keep them in sync.
- The `RedisCacheAdapter` uses `server.KeysAsync(pattern: ...)` rather
  than `KEYS` because the latter blocks the server on large
  keyspaces. SCAN trades latency for non-blocking semantics — the
  right call for production.
- `JsonCacheSerializer` returns `byte[]` directly to skip the
  intermediate string allocation; Redis transports binary natively.
- `FireflyCacheManager.Active` is computed per call (a single
  `IsAvailable` read) — no background polling thread, no
  state-machine. Failover happens at access time.

## Dependencies

| Reference                              | Used for             |
|----------------------------------------|----------------------|
| `FireflyFramework.Kernel`              | Base exception type  |
| `Microsoft.Extensions.Caching.Memory`  | `MemoryCacheAdapter` |
| `StackExchange.Redis`                  | `RedisCacheAdapter`  |

`System.Text.Json` (used by the default serialiser) ships in the .NET
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
| `CacheStats`          | `CacheStats`                                                      |
| `CacheHealth`         | `CacheHealth`                                                     |
